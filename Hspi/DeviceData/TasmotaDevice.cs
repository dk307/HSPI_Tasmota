﻿using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Identification;
using Hspi.Utils;
using Nito.AsyncEx;
using NullGuard;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Hspi.DeviceData
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class TasmotaDevice : DeviceBase<TasmotaDeviceInfo>
    {
        public TasmotaDevice(IHsController HS, int refId, CancellationToken cancellationToken) : base(HS, refId)
        {
            this.cancellationToken = cancellationToken;
            Utils.TaskHelper.StartAsyncWithErrorChecking("Device Start", UpdateDeviceProperties, cancellationToken);
        }

        public static string RootDeviceType => "tasmota-root";

        public override string DeviceType => RootDeviceType;

        public static async Task<int> CreateNew(IHsController HS,
                                                TasmotaDeviceInfo data,
                                                CancellationToken cancellationToken)
        {
            var deviceStatus = await TasmotaDeviceInterface.GetStatus(data, cancellationToken).ConfigureAwait(false);

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

        public async Task<TasmotaFullStatus> GetStatus()
        {
            return await TasmotaDeviceInterface.GetStatus(this.Data, cancellationToken).ConfigureAwait(false);
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

        private async Task<IDictionary<TasmotaDeviceFeature, int>> CreateAndUpdateFeatures(TasmotaDeviceInfo data,
                                                                               TasmotaFullStatus deviceStatus,
                                                                               HsDevice device)
        {
            var featuresNew = new Dictionary<TasmotaDeviceFeature, int>();
            foreach (var feature in data.EnabledFeatures)
            {
                int index = device.Features.FindIndex(
                    x => HSDeviceHelper.GetDeviceTypeFromPlugInData(x.PlugExtraData) == feature.FullUniqueId);

                if (index == -1)
                {
                    int featureRefId = await CreateFeature(feature).ConfigureAwait(false);
                    featuresNew.Add(feature, featureRefId);
                    logger.Info(Invariant($"Created feature {feature.Name} for {device.Name}"));
                }
                else
                {
                    logger.Debug(Invariant($"Found feature {feature.Name} for {device.Name}"));
                    featuresNew.Add(feature, device.Features[index].Ref);
                    UpdateFeature(deviceStatus, feature.Id);
                }
            }

            // delete removed sensors
            foreach (var feature in device.Features)
            {
                if (!featuresNew.ContainsValue(feature.Ref))
                {
                    logger.Info(Invariant($"Deleting unknown feature {feature.Name} for {device.Name}"));
                    HS.DeleteFeature(feature.Ref);
                }
            }
            return featuresNew;
        }

        private async Task<int> CreateFeature(TasmotaDeviceFeature feature)
        {
            string featureName = feature.DataType.ToString().ToLowerInvariant();
            string imagePath = Path.ChangeExtension(Path.Combine(PlugInData.PlugInId, "images", featureName), "png");

            FeatureFactory newFeatureData = null;

            switch (feature.DataType)
            {
                case TasmotaDeviceFeature.FeatureDataType.None:
                    throw new Exception("Invalid Feature Type");

                case TasmotaDeviceFeature.FeatureDataType.OnOffStateSensor:
                    {
                        var statusTexts = await TasmotaDeviceInterface.GetSwitchText(Data, cancellationToken).ConfigureAwait(false);
                        newFeatureData = FeatureFactory.CreateGenericBinarySensor(PlugInData.PlugInId, feature.Name,
                                                                                  statusTexts.GetValueOrDefault("StateText1", "On"),
                                                                                  statusTexts.GetValueOrDefault("StateText2", "Off"), OnValue, OffValue)
                                                       .WithMiscFlags(EMiscFlag.SetDoesNotChangeLastChange, EMiscFlag.StatusOnly);
                    }
                    break;

                case TasmotaDeviceFeature.FeatureDataType.OnOffStateControl:
                    {
                        var statusTexts = await TasmotaDeviceInterface.GetSwitchText(Data, cancellationToken).ConfigureAwait(false);
                        newFeatureData = FeatureFactory.CreateGenericBinaryControl(PlugInData.PlugInId, feature.Name, statusTexts.GetValueOrDefault("StateText1", "On"),
                                                                                      statusTexts.GetValueOrDefault("StateText2", "Off"), OnValue, OffValue)
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

        private async Task UpdateDeviceProperties()
        {
            try
            {
                using (var _ = await featureLock.EnterAsync().ConfigureAwait(false))
                {
                    var data = this.Data;

                    var deviceStatus = await TasmotaDeviceInterface.GetStatus(data, cancellationToken).ConfigureAwait(false);
                    HS.UpdatePropertyByRef(RefId, EProperty.Name, deviceStatus.DeviceName);

                    var device = HS.GetDeviceWithFeaturesByRef(RefId);
                    features = (await CreateAndUpdateFeatures(data, deviceStatus, device)
                                     .ConfigureAwait(false)).ToImmutableDictionary();

                    UpdateDevices(deviceStatus);
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

        private void UpdateDevices(TasmotaFullStatus deviceStatus)
        {
            //update Value
            foreach (var feature in features)
            {
                var valueType = EnumHelper.GetAttribute<ValueTypeAttribute>(feature.Key.DataType);

                switch (valueType?.Type)
                {
                    case null:
                        break;

                    case ValueType.ValueType:
                        try
                        {
                            var value = deviceStatus.GetValue<double>(feature.Key);
                            HSDeviceHelper.UpdateDeviceValue(HS, feature.Value, value);
                        }
                        catch (Exception ex)
                        {
                            logger.Warn(Invariant($"Failed to get value from Device for {Name} : {feature.Key.Name} with {ex.GetFullMessage()}"));
                            HSDeviceHelper.UpdateDeviceValue(HS, feature.Value, null);
                        }
                        break;

                    case ValueType.StringType:
                        try
                        {
                            var value = deviceStatus.GetValue<string>(feature.Key);
                            HS.UpdateFeatureValueStringByRef(feature.Value, value);
                        }
                        catch (Exception ex)
                        {
                            logger.Warn(Invariant($"Failed to get value from Device for {Name} : {feature.Key.Name} with {ex.GetFullMessage()}"));
                            HS.UpdateFeatureValueStringByRef(feature.Value, null);
                        }
                        break;

                    case ValueType.OnOff:
                        try
                        {
                            var value = deviceStatus.GetValue<string>(feature.Key);

                            if (value == "ON")
                            {
                            }
                            //HSDeviceHelper.UpdateDeviceValue(HS, feature.Value, value);
                        }
                        catch (Exception ex)
                        {
                            logger.Warn(Invariant($"Failed to get value from Device for {Name} : {feature.Key.Name} with {ex.GetFullMessage()}"));
                            HSDeviceHelper.UpdateDeviceValue(HS, feature.Value, null);
                        }
                        break;
                }
            }
        }

        private void UpdateFeature(TasmotaFullStatus deviceStatus, string sensorName)
        {
        }

        private const double OffValue = 0;
        private const double OnValue = 1;
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly CancellationToken cancellationToken;
        private readonly AsyncMonitor featureLock = new AsyncMonitor();
        private ImmutableDictionary<TasmotaDeviceFeature, int> features;
    }
}