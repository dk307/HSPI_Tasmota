using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Identification;
using Hspi.Utils;
using Nito.AsyncEx;
using NullGuard;
using System;
using System.Collections.Generic;
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

        private int CreateFeature(TasmotaStatus deviceStatus, string sensorName)
        {
            var newFeatureData = FeatureFactory.CreateFeature(PlugInData.PlugInId)
                .WithName(sensorName)
                .WithLocation(PlugInData.PlugInName)
                .WithMiscFlags(EMiscFlag.SetDoesNotChangeLastChange, EMiscFlag.StatusOnly)
                .AsType(EFeatureType.Generic, 0)
                .WithExtraData(HSDeviceHelper.CreatePlugInExtraDataForDeviceType(sensorName))
                .PrepareForHsDevice(RefId);

            return HS.CreateFeatureForDevice(newFeatureData);
        }

        private async Task UpdateDeviceProperties()
        {
            try
            {
                using (var _ = await featureLock.EnterAsync())
                {
                    //update name
                    string deviceDetails = await SendWebCommandToDevice(this.Data, "STATUS 0", cancellationToken).ConfigureAwait(false);
                    var deviceStatus = new TasmotaStatus(deviceDetails);
                    HS.UpdatePropertyByRef(RefId, EProperty.Name, deviceStatus.DeviceName);

 
                    Dictionary<string, int> featuresNew = new Dictionary<string, int>();

                    var device = HS.GetDeviceWithFeaturesByRef(RefId);

                    //foreach (var sensor in sensors)
                    //{
                    //    int index = device.Features.FindIndex(
                    //        x => HSDeviceHelper.GetDeviceTypeFromPlugInData(x.PlugExtraData) == sensor.Key);

                    //    if (index == -1)
                    //    {
                    //        int featureRefId = CreateFeature(deviceStatus, sensor.Key);
                    //        featuresNew.Add(sensor.Key, RefId);
                    //        logger.Info(Invariant($"Created feature {sensor} for {device.Name}"));

                    //    }
                    //    else
                    //    {
                    //        UpdateFeature(deviceStatus, sensor.Key);
                    //    }

                    //}

                    //// delete removed sensors
                    //foreach (var feature in device.Features)
                    //{
                    //    string type = HSDeviceHelper.GetDeviceTypeFromPlugInData(feature.PlugExtraData);

                    //    if (!sensors.ContainsKey(type))
                    //    {
                    //        logger.Info(Invariant($"Deleting unknown feature {feature.Name} for {device.Name}"));
                    //        HS.DeleteFeature(feature.Ref);
                    //    }
                    //}

                    // recreate features
                    features = featuresNew;
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
        private IReadOnlyDictionary<string, int> features;
    }
}