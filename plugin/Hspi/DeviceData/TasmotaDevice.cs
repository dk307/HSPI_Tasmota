using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using HomeSeer.PluginSdk.Devices.Identification;
using Hspi.DeviceData.Tasmota;
using Hspi.Utils;
using MQTTnet;
using MQTTnet.Client.Options;
using MQTTnet.Extensions.ManagedClient;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

#nullable enable

namespace Hspi.DeviceData
{
    internal class TasmotaDevice : DeviceBase<TasmotaDeviceInfo>, IDisposable
    {
        public TasmotaDevice(IHsController HS,
                             int refId,
                             MqttServerDetails hostedServerDetails,
                             CancellationToken cancellationToken)
            : base(HS, refId)
        {
            this.hostedServerDetails = hostedServerDetails;
            this.cancellationToken = cancellationToken;
            Utils.TaskHelper.StartAsyncWithErrorChecking(Invariant($"Device Start {refId}"),
                                                         UpdateDeviceProperties,
                                                         cancellationToken,
                                                         TimeSpan.FromSeconds(15));
        }

        public static string RootDeviceType => "tasmota-root";

        public TasmotaDeviceFullStatus DeviceStatus
        {
            get
            {
                if (deviceStatus == null)
                {
                    throw new InvalidOperationException($"No status avaiable for {Name}");
                }
                return deviceStatus;
            }

            private set => deviceStatus = value;
        }

        public override string DeviceType => RootDeviceType;
        private string? MqttTopicPrefix => DeviceStatus?.MQTTPrefix3FinalTopic;

        public static async Task<int> CreateNew(IHsController HS,
                                                TasmotaDeviceInfo data,
                                                CancellationToken cancellationToken)
        {
            var status = await TasmotaDeviceInterface.GetFullStatus(data, cancellationToken).ConfigureAwait(false);

            PlugExtraData extraData = CreatePlugInExtraData(data, RootDeviceType);
            string friendlyName = status.DeviceName ?? "Tasmota Device";
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

                if (controlEvent.TargetRef == deviceControlDeviceRefId)
                {
                    switch (controlEvent.ControlValue)
                    {
                        case DeviceControlBackUpId:
                            await SendBackupCommand().ConfigureAwait(false);
                            break;

                        case DeviceControlRefreshId:
                            await RefreshValuesImpl().ConfigureAwait(false);
                            break;
                    }
                    return true;
                }
            }

            return false;

            async Task SendCommand(KeyValuePair<TasmotaDeviceFeature, int> feature, bool on)
            {
                try
                {
                    logger.Info(Invariant($"Turning {(on ? "ON" : "OFF")} {Name} : {feature.Key.Name}"));
                    var data = GetValidatedData();
                    await TasmotaDeviceInterface.SendOnOffCommand(data, feature.Key.Name, on, cancellationToken).ConfigureAwait(false);
                    await TasmotaDeviceInterface.ForceSendMQTTStatus(data, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (ex.IsCancelException())
                    {
                        throw;
                    }

                    logger.Warn(Invariant($"Failed to Turn {(on ? "ON" : "OFF")} {Name} : {feature.Key.Name} with {ex.GetFullMessage()}"));
                }
            }

            async Task SendBackupCommand()
            {
                try
                {
                    string dir = Path.Combine(PlugInData.HomeSeerDirectory, "data", PlugInData.PlugInId, "backup");
                    string filPath = Path.Combine(dir, Path.ChangeExtension(Name, "dmp"));

                    Directory.CreateDirectory(dir);
                    logger.Info(Invariant($"Backing up {Name} to {filPath}"));
                    var data = GetValidatedData();
                    await TasmotaDeviceInterface.DownloadSettingsToFile(data,
                                                                        filPath,
                                                                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (ex.IsCancelException())
                    {
                        throw;
                    }

                    logger.Warn(Invariant($"Failed to backup {Name} with {ex.GetFullMessage()}"));
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public async Task RefreshValues()
        {
            using var _ = await featureLock.EnterAsync(cancellationToken).ConfigureAwait(false);
            await RefreshValuesImpl().ConfigureAwait(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                mqttClient?.Dispose();
            }
        }

        private static void AddSuffix(TasmotaDeviceFeature feature, NewFeatureData data)
        {
            if (feature.DataType.HasValue)
            {
                var unitAttribute = EnumHelper.GetAttribute<UnitAttribute>(feature.DataType);
                var suffix = unitAttribute?.Unit;

                if (!string.IsNullOrWhiteSpace(suffix))
                {
                    data.Feature.Add(EProperty.AdditionalStatusData, new List<string>() { suffix! });

                    if (data.Feature[EProperty.StatusGraphics] is StatusGraphicCollection graphics)
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
        }

        private static string CreateImagePath(string featureName)
        {
            return Path.ChangeExtension(Path.Combine(PlugInData.PlugInId, "images", featureName), "png");
        }

        private static bool IsInternalFeature(HsFeature feature)
        {
            string? deviceType = HSDeviceHelper.GetDeviceTypeFromPlugInData(feature.PlugExtraData);
            bool isInternalFeature = deviceType == LWTDeviceType || deviceType == DeviceControlDeviceType;
            return isInternalFeature;
        }

        private int CreateAndUpdateDeviceControlFeature(HsDevice device)
        {
            foreach (var feature in device.Features)
            {
                if (HSDeviceHelper.GetDeviceTypeFromPlugInData(feature.PlugExtraData) == DeviceControlDeviceType)
                {
                    return feature.Ref;
                }
            }

            var newFeatureData = FeatureFactory.CreateFeature(PlugInData.PlugInId)
               .WithName("Device Control")
               .WithMiscFlags(EMiscFlag.NoStatusDisplay)
               .WithLocation(PlugInData.PlugInName)
               .AsType(EFeatureType.Generic, 0)
               .WithExtraData(HSDeviceHelper.CreatePlugInExtraDataForDeviceType(DeviceControlDeviceType))
               .AddButton(DeviceControlBackUpId, "Backup")
               .AddButton(DeviceControlRefreshId, "Refresh")
               .AddGraphicForRange(CreateImagePath("devicecontrol"), int.MinValue, int.MaxValue)
               .PrepareForHsDevice(RefId);

            return HS.CreateFeatureForDevice(newFeatureData);
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
                }
            }

            // delete removed sensors
            foreach (var feature in device.Features)
            {
                if (!featuresNew.ContainsValue(feature.Ref) && !IsInternalFeature(feature))
                {
                    logger.Info(Invariant($"Deleting unknown feature {feature.Name} for {device.Name}"));
                    HS.DeleteFeature(feature.Ref);
                }
            }

            return featuresNew;
        }

        private int CreateAndUpdateLWTFeature(HsDevice device)
        {
            foreach (var feature in device.Features)
            {
                if (HSDeviceHelper.GetDeviceTypeFromPlugInData(feature.PlugExtraData) == LWTDeviceType)
                {
                    return feature.Ref;
                }
            }

            var newFeatureData = FeatureFactory.CreateFeature(PlugInData.PlugInId)
               .WithName("Device MQTT Status")
               .WithMiscFlags(EMiscFlag.StatusOnly)
               .WithLocation(PlugInData.PlugInName)
               .AsType(EFeatureType.Generic, 0)
               .WithExtraData(HSDeviceHelper.CreatePlugInExtraDataForDeviceType(LWTDeviceType))
               .AddGraphicForValue(CreateImagePath("online"), OnValue, LWTOnline)
               .AddGraphicForValue(CreateImagePath("offline"), OffValue, LWTOffline)
               .PrepareForHsDevice(RefId);

            return HS.CreateFeatureForDevice(newFeatureData);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "<Pending>")]
        private int CreateDeviceFeature(TasmotaDeviceFeature feature)
        {
            string featureName = feature.DataType.ToString().ToLowerInvariant();
            string imagePath = CreateImagePath(featureName);

            FeatureFactory? newFeatureData;
            switch (feature.DataType)
            {
                case TasmotaDeviceFeature.FeatureDataType.None:
                    throw new InvalidOperationException("Invalid Feature Type");

                case TasmotaDeviceFeature.FeatureDataType.OnOffStateSensor:
                    {
                        var statusTexts = DeviceStatus.SwitchText;
                        newFeatureData = FeatureFactory.CreateFeature(PlugInData.PlugInId)
                                                       .WithName(feature.Name)
                                                       .AddGraphicForValue(CreateImagePath("on.png"), OnValue, statusTexts.GetValueOrDefault("StateText2", "On"))
                                                       .AddGraphicForValue(CreateImagePath("off.png"), OffValue, statusTexts.GetValueOrDefault("StateText1", "Off"))
                                                       .WithMiscFlags(EMiscFlag.SetDoesNotChangeLastChange, EMiscFlag.StatusOnly);
                    }
                    break;

                case TasmotaDeviceFeature.FeatureDataType.OnOffStateControl:
                    {
                        var statusTexts = DeviceStatus.SwitchText;
                        string onText = statusTexts.GetValueOrDefault("StateText2", "On");
                        string offText = statusTexts.GetValueOrDefault("StateText1", "Off");
                        newFeatureData = FeatureFactory.CreateFeature(PlugInData.PlugInId)
                                                       .WithName(feature.Name)
                                                       .AddGraphicForValue(CreateImagePath("on.png"), OnValue, onText)
                                                       .AddGraphicForValue(CreateImagePath("off.png"), OffValue, offText)
                                                       .AddButton(OnValue, onText, controlUse: EControlUse.On)
                                                       .AddButton(OffValue, offText, controlUse: EControlUse.Off)
                                                       .WithMiscFlags(EMiscFlag.SetDoesNotChangeLastChange)
                                                       .WithDisplayType(EFeatureDisplayType.Important);

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

        private async Task MQTTSubscribe()
        {
            // Setup and start a managed MQTT client.
            var mqttServerDetails = DeviceStatus.MqttServerDetails;

            // verify it is same as this server otherwise update it
            if (!hostedServerDetails.Equals(mqttServerDetails))
            {
                await UpdateMqttServerDetails().ConfigureAwait(false);
                throw new Exception($"{Name} mqtt details do not match hosted mqtt server details. Updated it.");
            }

            var options = new ManagedMqttClientOptionsBuilder()
                                .WithAutoReconnectDelay(TimeSpan.FromSeconds(10))
                                .WithClientOptions(new MqttClientOptionsBuilder()
                                    .WithClientId(Invariant($"Homeseer-{RefId}"))
                                    .WithTcpServer(mqttServerDetails.Host, mqttServerDetails.Port)
                                    .Build())
                                .Build();

            mqttClient = new MqttFactory(new MqttNetLogger()).CreateManagedMqttClient();

            logger.Info($"Subscribing to {MqttTopicPrefix} for {Name}");

            var topicFiltersBuilderLWT = new MqttTopicFilterBuilder()
                                          .WithTopic(MqttTopicPrefix + "LWT")
                                          .WithAtLeastOnceQoS();

            var topicFiltersBuilder3 = new MqttTopicFilterBuilder()
                                          .WithTopic(MqttTopicPrefix + "+");

            mqttClient.UseApplicationMessageReceivedHandler(e =>
            {
                ProcessMQTTMessage(e.ApplicationMessage).ResultForSync();
                e.ProcessingFailed = false;
            });
            await mqttClient.SubscribeAsync(topicFiltersBuilderLWT.Build(), topicFiltersBuilder3.Build()).ConfigureAwait(false);

            cancellationToken.Register(() => mqttClient.StopAsync());
            await mqttClient.StartAsync(options).ConfigureAwait(false);
        }

        private async Task ProcessMQTTMessage(MqttApplicationMessage message)
        {
            string topic = message.Topic;

            try
            {
                byte[] payloadBytes = message.Payload;
                if (payloadBytes != null && payloadBytes.Length > 0)
                {
                    string payload = Encoding.UTF8.GetString(payloadBytes);
                    using var _ = await featureLock.EnterAsync(cancellationToken).ConfigureAwait(false);
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
                                UpdateDevicesValues(new TasmotaFeatureSourceStatus(sourceType, JObject.Parse(payload)));
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

        private async Task RefreshValuesImpl()
        {
            var data = GetValidatedData();

            DeviceStatus = await TasmotaDeviceInterface.GetFullStatus(data, cancellationToken).ConfigureAwait(false);
            UpdateDeviceName();

            foreach (var sourceType in EnumHelper.GetValues<TasmotaDeviceFeature.FeatureSource>())
            {
                UpdateDevicesValues(DeviceStatus.GetStatus(sourceType));
            }
        }

        private void UpdateDeviceName()
        {
            string? deviceName = DeviceStatus.DeviceName;
            if (deviceName != null)
            {
                HS.UpdatePropertyByRef(RefId, EProperty.Name, deviceName);
            }
        }

        private async Task UpdateDeviceProperties()
        {
            using var _ = await featureLock.EnterAsync(cancellationToken).ConfigureAwait(false);
            var data = this.GetValidatedData();

            DeviceStatus = await TasmotaDeviceInterface.GetFullStatus(data, cancellationToken).ConfigureAwait(false);
            UpdateDeviceName();

            var device = HS.GetDeviceWithFeaturesByRef(RefId);

            lwtDeviceRefId = CreateAndUpdateLWTFeature(device);
            deviceControlDeviceRefId = CreateAndUpdateDeviceControlFeature(device);
            features = CreateAndUpdateFeatures(data, device).ToImmutableDictionary();

            foreach (var sourceType in EnumHelper.GetValues<TasmotaDeviceFeature.FeatureSource>())
            {
                UpdateDevicesValues(DeviceStatus.GetStatus(sourceType));
            }

            await MQTTSubscribe().ConfigureAwait(false);
        }

        private void UpdateDevicesValues(TasmotaFeatureSourceStatus tasmotaStatus)
        {
            if (features != null)
            {
                foreach (var featureKeyValue in features)
                {
                    if (featureKeyValue.Key.SourceType != tasmotaStatus.SourceType)
                    {
                        continue;
                    }

                    try
                    {
                        var valueType = featureKeyValue.Key.DataType.HasValue ?
                                        EnumHelper.GetAttribute<ValueTypeAttribute>(featureKeyValue.Key.DataType) : null;

                        switch (valueType?.Type)
                        {
                            case null:
                                break;

                            case Tasmota.ValueType.ValueType:
                                UpdateDoubleValue(tasmotaStatus, featureKeyValue);
                                break;

                            case Tasmota.ValueType.StringType:
                                UpdateStringValue(tasmotaStatus, featureKeyValue);
                                break;

                            case Tasmota.ValueType.OnOff:
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
        }

        private void UpdateDoubleValue(TasmotaFeatureSourceStatus tasmotaStatus, KeyValuePair<TasmotaDeviceFeature, int> featureKeyPair)
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

        private void UpdateLWTValue(string payload)
        {
            if (lwtDeviceRefId.HasValue)
            {
                try
                {
                    if (payload == LWTOnline)
                    {
                        HSDeviceHelper.UpdateDeviceValue(HS, lwtDeviceRefId.Value, OnValue);
                    }
                    else if (payload == LWTOffline)
                    {
                        logger.Warn(Invariant($"{Name} is offline"));
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
        }

        private async Task UpdateMqttServerDetails()
        {
            await TasmotaDeviceInterface.UpdateMqttServerDetails(GetValidatedData(), hostedServerDetails, cancellationToken).ConfigureAwait(false);
        }

        private void UpdateOnOffValue(TasmotaFeatureSourceStatus tasmotaStatus, KeyValuePair<TasmotaDeviceFeature, int> feature)
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

        private void UpdateStringValue(TasmotaFeatureSourceStatus tasmotaStatus, KeyValuePair<TasmotaDeviceFeature, int> feature)
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

        private const int DeviceControlBackUpId = 100;
        private const string DeviceControlDeviceType = "DeviceControl";
        private const int DeviceControlRefreshId = 101;
        private const string LWTDeviceType = "LWT";

        private const string LWTOffline = "Offline";

        private const string LWTOnline = "Online";

        private const double OffValue = 0;

        private const double OnValue = 1;

        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly CancellationToken cancellationToken;
        private readonly AsyncMonitor featureLock = new();
        private readonly MqttServerDetails hostedServerDetails;
        private int? deviceControlDeviceRefId;
        private TasmotaDeviceFullStatus? deviceStatus;
        private ImmutableDictionary<TasmotaDeviceFeature, int>? features;
        private int? lwtDeviceRefId;
        private IManagedMqttClient? mqttClient;
    }
}