using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using HomeSeer.PluginSdk.Devices.Identification;
using Hspi.Utils;
using MQTTnet;
using MQTTnet.Client.Options;
using MQTTnet.Extensions.ManagedClient;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;
using NullGuard;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Hspi.DeviceData
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class TasmotaDevice : DeviceBase<TasmotaDeviceInfo>, IDisposable
    {
        public TasmotaDevice(IHsController HS, int refId, CancellationToken cancellationToken) : base(HS, refId)
        {
            this.cancellationToken = cancellationToken;
            Utils.TaskHelper.StartAsyncWithErrorChecking("Device Start", UpdateDeviceProperties, cancellationToken);
        }

        public static string RootDeviceType => "tasmota-root";

        public override string DeviceType => RootDeviceType;
        private TasmotaFullStatus DeviceStatus { get; set; }
        private string MqttTopicPrefix => DeviceStatus.MqttStatus["Prefix3"];

        public static async Task<int> CreateNew(IHsController HS,
                                                    TasmotaDeviceInfo data,
                                            CancellationToken cancellationToken)
        {
            var deviceStatus = await TasmotaDeviceInterface.GetFullStatus(data, cancellationToken).ConfigureAwait(false);

            PlugExtraData extraData = CreatePlugInExtraData(data, RootDeviceType);
            string friendlyName = deviceStatus.DeviceName;
            var newDeviceData = DeviceFactory.CreateDevice(PlugInData.PlugInId)
                         .WithName(friendlyName)
                         .AsType(EDeviceType.Generic, 0)
                         .WithLocation(PlugInData.PlugInName)
                         .WithMiscFlags(EMiscFlag.SetDoesNotChangeLastChange)
                         .WithExtraData(extraData)
                         .PrepareForHs();

            int refId = HS.CreateDevice(newDeviceData);
            logger.Info(Invariant($"Created Tasmota Device {friendlyName}"));

            return refId;
        }

        public async Task<bool> CanProcessCommand(ControlEvent controlEvent)
        {
            using (var _ = await featureLock.EnterAsync(cancellationToken).ConfigureAwait(false))
            {
                var feature = features.FirstOrDefault(x => x.Value == controlEvent.TargetRef);

                if (feature.Key != null)
                {
                    if (controlEvent.ControlUse == EControlUse.On)
                    {
                        await SendCommand(feature, true).ConfigureAwait(false);
                    }
                    else if (controlEvent.ControlUse == EControlUse.Off)
                    {
                        await SendCommand(feature, false).ConfigureAwait(false);
                    }

                    return true;
                }
            }

            return false;

            async Task SendCommand(KeyValuePair<TasmotaDeviceFeature, int> feature, bool on)
            {
                logger.Info(Invariant($"Turning {(on ? "ON" : "OFF")} {Name} : {feature.Key.Name}"));
                await TasmotaDeviceInterface.SendOnOffCommand(Data, feature.Key.Name, on, cancellationToken).ConfigureAwait(false);
            }
        }

        void IDisposable.Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public async Task<TasmotaFullStatus> GetStatus()
        {
            return await TasmotaDeviceInterface.GetFullStatus(this.Data, cancellationToken).ConfigureAwait(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    mqttClient?.Dispose();
                }

                disposedValue = true;
            }
        }

        private static void AddSuffix(TasmotaDeviceFeature feature, NewFeatureData data)
        {
            var unitAttribute = EnumHelper.GetAttribute<UnitAttribute>(feature.DataType);
            string suffix = unitAttribute?.Unit;

            if (!string.IsNullOrWhiteSpace(suffix))
            {
                data.Feature.Add(EProperty.AdditionalStatusData, new List<string>() { suffix });

                var graphics = data.Feature[EProperty.StatusGraphics] as StatusGraphicCollection;

                if (graphics != null)
                {
                    foreach (var statusGraphic in graphics.Values)
                    {
                        statusGraphic.HasAdditionalData = true;

                        if (statusGraphic.IsRange)
                        {
                            statusGraphic.TargetRange.Suffix = " " + HsFeature.GetAdditionalDataToken(0);
                            statusGraphic.TargetRange.DecimalPlaces = 3;
                        }
                    }
                }
            }
        }

        private static string CreateImagePath(string featureName)
        {
            return Path.ChangeExtension(Path.Combine(PlugInData.PlugInId, "images", featureName), "png");
        }

        private IDictionary<TasmotaDeviceFeature, int> CreateAndUpdateFeatures(TasmotaDeviceInfo data,
                                                                                       HsDevice device)
        {
            var featuresNew = new Dictionary<TasmotaDeviceFeature, int>();
            foreach (var feature in data.EnabledFeatures)
            {
                int index = device.Features.FindIndex(
                    x => HSDeviceHelper.GetDeviceTypeFromPlugInData(x.PlugExtraData) == feature.FullUniqueId);

                if (index == -1)
                {
                    int featureRefId = CreateDeviceFeature(feature);
                    featuresNew.Add(feature, featureRefId);
                    logger.Info(Invariant($"Created feature {feature.Name} for {device.Name}"));
                }
                else
                {
                    logger.Debug(Invariant($"Found feature {feature.Name} for {device.Name}"));
                    featuresNew.Add(feature, device.Features[index].Ref);
                    UpdateFeature(DeviceStatus, feature.Id);
                }
            }

            // delete removed sensors
            foreach (var feature in device.Features)
            {
                if (!featuresNew.ContainsValue(feature.Ref) &&
                    (HSDeviceHelper.GetDeviceTypeFromPlugInData(feature.PlugExtraData) != LWTDeviceType))
                {
                    logger.Info(Invariant($"Deleting unknown feature {feature.Name} for {device.Name}"));
                    HS.DeleteFeature(feature.Ref);
                }
            }
            return featuresNew;
        }

        private int CreateAndUpdateLWTFeature(HsDevice device)
        {
            int? lwtRefId = null;
            foreach (var feature in device.Features)
            {
                if (HSDeviceHelper.GetDeviceTypeFromPlugInData(feature.PlugExtraData) == LWTDeviceType)
                {
                    return feature.Ref;
                }
            }

            if (lwtRefId == null)
            {
                lwtRefId = CreateLWTFeature();
            }

            // HSDeviceHelper.UpdateDeviceValue(HS, lwtRefId.Value, OffValue);

            return lwtRefId.Value;
        }

        private int CreateDeviceFeature(TasmotaDeviceFeature feature)
        {
            string featureName = feature.DataType.ToString().ToLowerInvariant();
            string imagePath = CreateImagePath(featureName);

            FeatureFactory newFeatureData = null;

            switch (feature.DataType)
            {
                case TasmotaDeviceFeature.FeatureDataType.None:
                    throw new Exception("Invalid Feature Type");

                case TasmotaDeviceFeature.FeatureDataType.OnOffStateSensor:
                    {
                        var statusTexts = DeviceStatus.SwitchText;
                        newFeatureData = FeatureFactory.CreateGenericBinarySensor(PlugInData.PlugInId, feature.Name,
                                                                                  statusTexts.GetValueOrDefault("StateText2", "On"),
                                                                                  statusTexts.GetValueOrDefault("StateText1", "Off"), OnValue, OffValue)
                                                       .WithMiscFlags(EMiscFlag.SetDoesNotChangeLastChange, EMiscFlag.StatusOnly);
                    }
                    break;

                case TasmotaDeviceFeature.FeatureDataType.OnOffStateControl:
                    {
                        var statusTexts = DeviceStatus.SwitchText;
                        newFeatureData = FeatureFactory.CreateGenericBinaryControl(PlugInData.PlugInId, feature.Name,
                                                                                   statusTexts.GetValueOrDefault("StateText2", "On"),
                                                                                   statusTexts.GetValueOrDefault("StateText1", "Off"), OnValue, OffValue)
                                                       .WithMiscFlags(EMiscFlag.SetDoesNotChangeLastChange);
                    }
                    break;

                default:
                    newFeatureData = FeatureFactory.CreateFeature(PlugInData.PlugInId)
                                                   .WithMiscFlags(EMiscFlag.SetDoesNotChangeLastChange, EMiscFlag.StatusOnly)
                                                   .WithName(feature.Name)
                                                   .AddGraphicForRange(imagePath, int.MinValue, int.MaxValue);

                    break;
            }

            newFeatureData = newFeatureData
              .WithLocation(PlugInData.PlugInName)
              .AsType(EFeatureType.Generic, (int)(feature.DataType ?? TasmotaDeviceFeature.FeatureDataType.None))
              .WithExtraData(HSDeviceHelper.CreatePlugInExtraDataForDeviceType(feature.FullUniqueId));

            var data = newFeatureData.PrepareForHsDevice(RefId);
            AddSuffix(feature, data);

            int featureRefId = HS.CreateFeatureForDevice(data);
            return featureRefId;
        }

        private int CreateLWTFeature()
        {
            var newFeatureData = FeatureFactory.CreateFeature(PlugInData.PlugInId)
                               .WithName("Device MQTT Status")
                               .WithMiscFlags(EMiscFlag.SetDoesNotChangeLastChange, EMiscFlag.StatusOnly)
                               .WithLocation(PlugInData.PlugInName)
                               .AsType(EFeatureType.Generic, 0)
                               .WithExtraData(HSDeviceHelper.CreatePlugInExtraDataForDeviceType(LWTDeviceType))
                               .AddGraphicForValue(CreateImagePath("online"), OnValue, LWTOnline)
                               .AddGraphicForValue(CreateImagePath("offline"), OffValue, LWTOffline)
                               .PrepareForHsDevice(RefId);

            int featureRefId = HS.CreateFeatureForDevice(newFeatureData);
            return featureRefId;
        }

        private async Task MQTTSubscribe()
        {
            // Setup and start a managed MQTT client.
            var mqttServerDetails = DeviceStatus.MqttServerDetails;

            if (!string.IsNullOrWhiteSpace(mqttServerDetails.Host))
            {
                var options = new ManagedMqttClientOptionsBuilder()
                    .WithAutoReconnectDelay(TimeSpan.FromSeconds(10))
                    .WithClientOptions(new MqttClientOptionsBuilder()
                        .WithClientId(Invariant($"Homeseer-{RefId}"))
                        .WithTcpServer(mqttServerDetails.Host, mqttServerDetails.Port)
                        .Build())
                    .Build();

                mqttClient = new MqttFactory().CreateManagedMqttClient();

                var topicFiltersBuilder3 = new MqttTopicFilterBuilder()
                                              .WithTopic(MqttTopicPrefix + "+");

                mqttClient.UseApplicationMessageReceivedHandler(async e =>
                {
                    await ProcessMQTTMessage(e.ApplicationMessage).ConfigureAwait(false);
                });
                await mqttClient.SubscribeAsync(topicFiltersBuilder3.Build()).ConfigureAwait(false);

                cancellationToken.Register(() => mqttClient.StopAsync());
                await mqttClient.StartAsync(options).ConfigureAwait(false);
            }
            else
            {
                logger.Error(Invariant($"{Name} does not have valid MQTT Configuration"));
            }
        }

        private async Task ProcessMQTTMessage(MqttApplicationMessage message)
        {
            string topic = message.Topic;

            try
            {
                string payload = Encoding.UTF8.GetString(message.Payload);
                using (var _ = await featureLock.EnterAsync(cancellationToken).ConfigureAwait(false))
                {
                    var mqttTopicPrefix = MqttTopicPrefix;

                    if ((mqttTopicPrefix + "LWT") == topic)
                    {
                        UpdateLWTValue(payload);
                    }
                    else
                    {
                        foreach (var sourceType in EnumHelper.GetValues<TasmotaDeviceFeature.FeatureSource>())
                        {
                            var sourceMQTTTopic = mqttTopicPrefix + sourceType.ToString().ToUpperInvariant();
                            if (topic == sourceMQTTTopic)
                            {
                                UpdateDevicesValues(new TasmotaStatus(sourceType, JObject.Parse(payload)));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!ex.IsCancelException())
                {
                    logger.Warn(Invariant($"Failed to process mqtt {topic} with {ExceptionHelper.GetFullMessage(ex)} for {Name}"));
                }
            }
        }

        private async Task UpdateDeviceProperties()
        {
            try
            {
                using (var _ = await featureLock.EnterAsync(cancellationToken).ConfigureAwait(false))
                {
                    var data = this.Data;

                    DeviceStatus = await TasmotaDeviceInterface.GetFullStatus(data, cancellationToken).ConfigureAwait(false);
                    HS.UpdatePropertyByRef(RefId, EProperty.Name, DeviceStatus.DeviceName);

                    var device = HS.GetDeviceWithFeaturesByRef(RefId);

                    lwtDeviceRefId = CreateAndUpdateLWTFeature(device);
                    features = CreateAndUpdateFeatures(data, device).ToImmutableDictionary();

                    foreach (var sourceType in EnumHelper.GetValues<TasmotaDeviceFeature.FeatureSource>())
                    {
                        UpdateDevicesValues(DeviceStatus.GetStatus(sourceType));
                    }

                    await MQTTSubscribe().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                if (ex.IsCancelException())
                {
                    throw;
                }

                logger.Warn(Invariant($"Failed to connect to device with {ExceptionHelper.GetFullMessage(ex)} for {Name}"));

                await Task.Delay(15000, cancellationToken).ConfigureAwait(false);
            }
        }

        private void UpdateDevicesValues(TasmotaStatus tasmotaStatus)
        {
            foreach (var featureKeyValue in features)
            {
                if (featureKeyValue.Key.SourceType != tasmotaStatus.SourceType)
                {
                    continue;
                }

                try
                {
                    var valueType = EnumHelper.GetAttribute<ValueTypeAttribute>(featureKeyValue.Key.DataType);

                    switch (valueType?.Type)
                    {
                        case null:
                            break;

                        case ValueType.ValueType:
                            UpdateDoubleValue(tasmotaStatus, featureKeyValue);
                            break;

                        case ValueType.StringType:
                            UpdateStringValue(tasmotaStatus, featureKeyValue);
                            break;

                        case ValueType.OnOff:
                            UpdateOnOffValue(tasmotaStatus, featureKeyValue);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    if (ex.IsCancelException())
                    {
                        throw;
                    }

                    logger.Warn(Invariant($"Failed to update {featureKeyValue.Key.Name} with {ExceptionHelper.GetFullMessage(ex)} for {Name}"));
                }
            }
        }

        private void UpdateDoubleValue(TasmotaStatus tasmotaStatus, KeyValuePair<TasmotaDeviceFeature, int> featureKeyPair)
        {
            try
            {
                var value = tasmotaStatus.GetFeatureValue<double>(featureKeyPair.Key);
                HSDeviceHelper.UpdateDeviceValue(HS, featureKeyPair.Value, value);
            }
            catch (Exception ex)
            {
                logger.Warn(Invariant($"Failed to get value from Device for {Name} : {featureKeyPair.Key.Name} with {ex.GetFullMessage()}"));
                HSDeviceHelper.UpdateDeviceValue(HS, featureKeyPair.Value, null);
            }
        }

        private void UpdateFeature(TasmotaFullStatus deviceStatus, string sensorName)
        {
        }

        private void UpdateLWTValue(string payload)
        {
            try
            {
                if (payload == LWTOnline)
                {
                    HSDeviceHelper.UpdateDeviceValue(HS, lwtDeviceRefId.Value, OnValue);
                }
                else if (payload == LWTOffline)
                {
                    HSDeviceHelper.UpdateDeviceValue(HS, lwtDeviceRefId.Value, OffValue);
                }
                else
                {
                    HSDeviceHelper.UpdateDeviceValue(HS, lwtDeviceRefId.Value, null);
                }
            }
            catch (Exception ex)
            {
                logger.Warn(Invariant($"Failed to get value from Device for {Name} : LWT with {ex.GetFullMessage()}"));
                HSDeviceHelper.UpdateDeviceValue(HS, lwtDeviceRefId.Value, null);
            }
        }

        private void UpdateOnOffValue(TasmotaStatus tasmotaStatus, KeyValuePair<TasmotaDeviceFeature, int> feature)
        {
            try
            {
                var value = tasmotaStatus.GetFeatureValue<string>(feature.Key);

                if (value == DeviceStatus.SwitchText["StateText2"])
                {
                    HSDeviceHelper.UpdateDeviceValue(HS, feature.Value, OnValue);
                }
                else if (value == DeviceStatus.SwitchText["StateText1"])
                {
                    HSDeviceHelper.UpdateDeviceValue(HS, feature.Value, OffValue);
                }
                else
                {
                    HSDeviceHelper.UpdateDeviceValue(HS, feature.Value, null);
                }
            }
            catch (Exception ex)
            {
                logger.Warn(Invariant($"Failed to get value from Device for {Name} : {feature.Key.Name} with {ex.GetFullMessage()}"));
                HSDeviceHelper.UpdateDeviceValue(HS, feature.Value, null);
            }
        }

        private void UpdateStringValue(TasmotaStatus tasmotaStatus, KeyValuePair<TasmotaDeviceFeature, int> feature)
        {
            try
            {
                var value = tasmotaStatus.GetFeatureValue<string>(feature.Key);
                HS.UpdateFeatureValueStringByRef(feature.Value, value);
            }
            catch (Exception ex)
            {
                logger.Warn(Invariant($"Failed to get value from Device for {Name} : {feature.Key.Name} with {ex.GetFullMessage()}"));
                HS.UpdateFeatureValueStringByRef(feature.Value, null);
            }
        }

        private const string LWTDeviceType = "LWT";
        private const string LWTOffline = "Offline";
        private const string LWTOnline = "Online";
        private const double OffValue = 0;
        private const double OnValue = 1;
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly CancellationToken cancellationToken;
        private readonly AsyncMonitor featureLock = new AsyncMonitor();
        private bool disposedValue;
        private ImmutableDictionary<TasmotaDeviceFeature, int> features;
        private int? lwtDeviceRefId;
        private IManagedMqttClient mqttClient;
    }
}