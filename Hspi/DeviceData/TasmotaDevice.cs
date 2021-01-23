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
using System.Net;
using System.Net.Http;
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
            string deviceDetails = await SendWebCommandToDevice(data, "STATUS", cancellationToken).ConfigureAwait(false);
            var deviceStatus = new TasmotaStatus(deviceDetails);

            PlugExtraData extraData = CreatePlugInExtraData(data, RootDeviceType);
            string friendlyName = deviceStatus.DeviceName;
            var newDeviceData = DeviceFactory.CreateDevice(PlugInData.PlugInId)
                         .WithName(friendlyName)
                         .AsType(EDeviceType.Generic, 0)
                         .WithLocation(PlugInData.PlugInName)
                         .WithMiscFlags(EMiscFlag.SetDoesNotChangeLastChange, EMiscFlag.StatusOnly)
                         .WithExtraData(extraData)
                         .PrepareForHs();

            int refId = HS.CreateDevice(newDeviceData);
            logger.Info(Invariant($"Created Tasmota Device {friendlyName}"));

            return refId;
        }

        public async Task<TasmotaStatus> GetStatus()
        {
            string deviceDetails = await SendWebCommandToDevice(this.Data, "STATUS 0", cancellationToken).ConfigureAwait(false);
            return new TasmotaStatus(deviceDetails);
        }

        private static void AddDataTypeSpecficProperties(TasmotaDeviceFeature feature, NewFeatureData data)
        {
            var unitAttribute = EnumHelper.GetAttribute<UnitAttribute>(feature.DataType);
            string suffix = unitAttribute?.Unit;

            if (!string.IsNullOrWhiteSpace(suffix))
            {
                data.Feature.Add(EProperty.AdditionalStatusData, new List<string>() { suffix });

                var graphics = data.Feature[EProperty.StatusGraphics] as StatusGraphicCollection;

                foreach (var statusGraphic in graphics.Values)
                {
                    statusGraphic.HasAdditionalData = true;

                    if (statusGraphic.IsRange)
                    {
                        statusGraphic.TargetRange.Suffix = " " + HsFeature.GetAdditionalDataToken(0);
                    }
                }
            }
        }

        private static async Task<string> SendWebCommandToDevice(TasmotaDeviceInfo importDeviceData,
                                                                         string command,
                                                                 CancellationToken cancellationToken)
        {
            var queryList = new List<string>();

            if (!string.IsNullOrEmpty(importDeviceData.User))
            {
                queryList.Add(Invariant($"user={WebUtility.UrlEncode(importDeviceData.User)}"));
                queryList.Add(Invariant($"password={WebUtility.UrlEncode(importDeviceData.Password)}"));
            }

            queryList.Add(Invariant($"cmnd={WebUtility.UrlEncode(command)}"));

            var uriBuilder = new UriBuilder(importDeviceData.Uri);
            uriBuilder.Path = "/cm";
            uriBuilder.Query = String.Join("&", queryList);

            var result = await httpClient.GetAsync(uriBuilder.Uri, cancellationToken).ConfigureAwait(false);
            result.EnsureSuccessStatusCode();
            return await result.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private IDictionary<TasmotaDeviceFeature, int> CreateAndUpdateFeatures(TasmotaDeviceInfo data,
                                                                               TasmotaStatus deviceStatus,
                                                                               HsDevice device)
        {
            var featuresNew = new Dictionary<TasmotaDeviceFeature, int>();

            foreach (var feature in data.EnabledFeatures)
            {
                int index = device.Features.FindIndex(
                    x => HSDeviceHelper.GetDeviceTypeFromPlugInData(x.PlugExtraData) == feature.FullUniqueId);

                if (index == -1)
                {
                    int featureRefId = CreateFeature(feature);
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

        private int CreateFeature(TasmotaDeviceFeature feature)
        {
            string image = feature.DataType.ToString().ToLowerInvariant();
            string imagePath = Path.ChangeExtension(Path.Combine(PlugInData.PlugInId, "images", image), "png");

            var newFeatureData = FeatureFactory.CreateFeature(PlugInData.PlugInId)
                .WithName(feature.Name)
                .WithLocation(PlugInData.PlugInName)
                .WithMiscFlags(EMiscFlag.SetDoesNotChangeLastChange, EMiscFlag.StatusOnly)
                .AsType(EFeatureType.Generic, 0)
                .WithExtraData(HSDeviceHelper.CreatePlugInExtraDataForDeviceType(feature.FullUniqueId))
                .AddGraphicForRange(imagePath, int.MinValue, int.MaxValue);

            var data = newFeatureData.PrepareForHsDevice(RefId);
            AddDataTypeSpecficProperties(feature, data);

            return HS.CreateFeatureForDevice(data);
        }

        private async Task UpdateDeviceProperties()
        {
            try
            {
                using (var _ = await featureLock.EnterAsync().ConfigureAwait(false))
                {
                    var data = this.Data;

                    //update name
                    string deviceDetails = await SendWebCommandToDevice(this.Data, "STATUS 0", cancellationToken).ConfigureAwait(false);
                    var deviceStatus = new TasmotaStatus(deviceDetails);
                    HS.UpdatePropertyByRef(RefId, EProperty.Name, deviceStatus.DeviceName);

                    var device = HS.GetDeviceWithFeaturesByRef(RefId);
                    features = CreateAndUpdateFeatures(data, deviceStatus, device).ToImmutableDictionary();

                    //update Value
                    foreach (var feature in features)
                    {
                        var featureValue = deviceStatus.GetValue(feature.Key);

                        // HSDeviceHelper.UpdateDeviceValue(HS, feature.Value,  );
                    }
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

        private void UpdateFeature(TasmotaStatus deviceStatus, string sensorName)
        {
        }

        private static HttpClient httpClient = new HttpClient();
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly CancellationToken cancellationToken;
        private readonly AsyncMonitor featureLock = new AsyncMonitor();
        private ImmutableDictionary<TasmotaDeviceFeature, int> features;
    }
}