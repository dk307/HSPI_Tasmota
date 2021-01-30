using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using Hspi.DeviceData;
using Hspi.Utils;
using MQTTnet.Server;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading.Tasks;
using static System.FormattableString;

#nullable enable

namespace Hspi
{
    internal partial class PlugIn : HspiBase
    {
        public PlugIn()
            : base(PlugInData.PlugInId, PlugInData.PlugInName)
        {
        }

        public override void HsEvent(Constants.HSEvent eventType, object[] parameters)
        {
            if (eventType == Constants.HSEvent.CONFIG_CHANGE)
            {
                if (parameters.Length == 6)
                {
                    if (Convert.ToInt32(parameters[1], CultureInfo.InvariantCulture) == 0) // Device Type change
                    {
                        if (Convert.ToInt32(parameters[4], CultureInfo.InvariantCulture) == 2) //Delete
                        {
                            int deletedDeviceRefId = Convert.ToInt32(parameters[3], CultureInfo.InvariantCulture);

                            var devices = GetTasmotaDevices().ResultForSync();

                            if (devices.ContainsKey(deletedDeviceRefId))
                            {
                                logger.Info($"PlugIn Device refId {deletedDeviceRefId} deleted");
                                RestartProcessing();
                            }
                        }
                    }
                }
            }

            base.HsEvent(eventType, parameters);
        }

        public override void SetIOMulti(List<ControlEvent> colSends)
        {
            SetIOMultiAsync().ResultForSync();

            async Task SetIOMultiAsync()
            {
                var devices = await GetTasmotaDevices().ConfigureAwait(false);
                foreach (var colSend in colSends)
                {
                    foreach (var device in devices)
                    {
                        bool done = await device.Value.CanProcessCommand(colSend).ConfigureAwait(false);

                        if (done)
                        {
                            return;
                        }
                    }
                }
            }
        }

        public override EPollResponse UpdateStatusNow(int devOrFeatRef)
        {
            try
            {
                // bool result = ImportDeviceFromDB(devOrFeatRef).ResultForSync();
                // return result ? EPollResponse.NotFound : EPollResponse.Ok;
                return EPollResponse.Ok;
            }
            catch (Exception ex)
            {
                logger.Error(Invariant($"Failed to import value for Ref Id: {devOrFeatRef} with {ex.GetFullMessage()}"));
                return EPollResponse.ErrorGettingStatus;
            }
        }

        protected override void BeforeReturnStatus()
        {
            this.Status = PluginStatus.Ok();
        }

 
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tasmotaDeviceManager?.Dispose();
                mqttServer?.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void Initialize()
        {
            string result = string.Empty;
            try
            {
                pluginConfig = new PluginConfig(HomeSeerSystem);
                UpdateDebugLevel();

                logger.Info("Starting Plugin");

                  mqttServer = MqttHelper.StartServer(ShutdownCancellationToken).ResultForSync();

                HomeSeerSystem.RegisterEventCB(Constants.HSEvent.CONFIG_CHANGE, PlugInData.PlugInId);

                // Device Add Page
                HomeSeerSystem.RegisterDeviceIncPage(Id, "adddevice.html", "Add Tasmota Device");

                // Feature pages
                HomeSeerSystem.RegisterFeaturePage(Id, "configuration.html", "Configuration");
                HomeSeerSystem.RegisterFeaturePage(Id, "devicelist.html", "Devices");

                RestartProcessing();

                logger.Info("Plugin Started");
            }
            catch (Exception ex)
            {
                result = Invariant($"Failed to initialize PlugIn with {ex.GetFullMessage()}");
                logger.Error(result);
                throw;
            }
        }
        private async Task<ImmutableDictionary<int, TasmotaDevice>> GetTasmotaDevices()
        {
            using (var sync = await deviceManagerLock.EnterAsync(ShutdownCancellationToken))
            {
                return tasmotaDeviceManager?.ImportDevices ?? ImmutableDictionary<int, TasmotaDevice>.Empty;
            }
        }

        private void PluginConfigChanged()
        {
            UpdateDebugLevel();
            RestartProcessing();
        }

        private void RestartProcessing()
        {
            Utils.TaskHelper.StartAsyncWithErrorChecking("Device Start", StartDevices, ShutdownCancellationToken);
        }

        private async Task StartDevices()
        {
            using (var sync = await deviceManagerLock.EnterAsync(ShutdownCancellationToken))
            {
                tasmotaDeviceManager?.Dispose();
                tasmotaDeviceManager = new TasmotaDeviceManager(HomeSeerSystem,
                                                                ShutdownCancellationToken);
            }
        }
        private void UpdateDebugLevel()
        {
            this.LogDebug = pluginConfig!.DebugLogging;
            Logger.ConfigureLogging(LogDebug, pluginConfig.LogToFile, HomeSeerSystem);
        }
        private readonly static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly AsyncMonitor deviceManagerLock = new AsyncMonitor();
        private PluginConfig? pluginConfig;
        private TasmotaDeviceManager? tasmotaDeviceManager;
        private IMqttServer? mqttServer;
    }
}