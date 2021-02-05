using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Identification;
using Hspi.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using static System.FormattableString;

#nullable enable

namespace Hspi.DeviceData
{
    internal sealed class TasmotaDeviceManager : IDisposable
    {
        public TasmotaDeviceManager(IHsController HS,
                                    MqttServerDetails hostedMQTTServerDetails,
                                    CancellationToken cancellationToken)
        {
            this.HS = HS;
            this.hostedMQTTServerDetails = hostedMQTTServerDetails;
            this.cancellationToken = cancellationToken;
            this.combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            importDevices = GetCurrentDevices().ToImmutableDictionary();
        }

        public ImmutableDictionary<int, TasmotaDevice> ImportDevices => importDevices;

        public void Dispose()
        {
            if (!disposedValue)
            {
                combinedToken.Cancel();
                disposedValue = true;
            }
        }

        private Dictionary<int, TasmotaDevice> GetCurrentDevices()
        {
            var refIds = HS.GetRefsByInterface(PlugInData.PlugInId);

            var devices = new Dictionary<int, TasmotaDevice>();

            foreach (var refId in refIds)
            {
                combinedToken.Token.ThrowIfCancellationRequested();
                try
                {
                    var relationship = (ERelationship)HS.GetPropertyByRef(refId, EProperty.Relationship);

                    //data is stored in feature(child)
                    if (relationship == ERelationship.Device)
                    {
                        var deviceType = HSDeviceHelper.GetDeviceTypeFromPlugInData(HS, refId);

                        if (deviceType == TasmotaDevice.RootDeviceType)
                        {
                            TasmotaDevice importDevice = new TasmotaDevice(HS, refId, hostedMQTTServerDetails, combinedToken.Token);
                            devices.Add(refId, importDevice);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Warn(Invariant($"{HSDeviceHelper.GetName(HS, refId)} has invalid plugin data load failed with {ex.GetFullMessage()}. Please recreate it."));
                }
            }

            return devices;
        }

        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly CancellationToken cancellationToken;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private readonly CancellationTokenSource combinedToken;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private readonly MqttServerDetails hostedMQTTServerDetails;
        private readonly IHsController HS;
        private readonly ImmutableDictionary<int, TasmotaDevice> importDevices;
        private bool disposedValue;
    };
}