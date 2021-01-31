using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using Hspi.DeviceData;
using Hspi.Utils;
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
                mqttServerInstance?.Stop().ResultForSync();
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

                HomeSeerSystem.RegisterEventCB(Constants.HSEvent.CONFIG_CHANGE, PlugInData.PlugInId);

                // Device Add Page
                HomeSeerSystem.RegisterDeviceIncPage(Id, "adddevice.html", "Add Tasmota Device");

                // Feature pages
                HomeSeerSystem.RegisterFeaturePage(Id, "configuration.html", "Configuration");
                HomeSeerSystem.RegisterFeaturePage(Id, "devicelist.html", "Devices");
                HomeSeerSystem.RegisterFeaturePage(Id, "mqttconfiguration.html", "MQTT Server Configuration");

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
            Utils.TaskHelper.StartAsyncWithErrorChecking("Main Task", MainTask, ShutdownCancellationToken);
        }

        private async Task MainTask()
        {
            var serverDetails  = await StartMQTTServer().ConfigureAwait(false);

            using (var sync = await deviceManagerLock.EnterAsync(ShutdownCancellationToken))
            {
                tasmotaDeviceManager?.Dispose();
                tasmotaDeviceManager = new TasmotaDeviceManager(HomeSeerSystem,
                                                                serverDetails,
                                                                ShutdownCancellationToken);
            }
            
             
        }

        private async Task<MqttServerDetails> StartMQTTServer()
        {
            using (var sync = await mqttServerLock.EnterAsync(ShutdownCancellationToken))
            {
                bool recreate = (mqttServerInstance == null) ||
                        (!mqttServerInstance.Configuration.Equals(pluginConfig!.MQTTServerConfiguration));

                if (recreate)
                {
                    if (mqttServerInstance != null)
                    {
                        await mqttServerInstance.Stop().ConfigureAwait(false);
                        mqttServerInstance = null;
                    }

                    mqttServerInstance = await MqttServerInstance.StartServer(this.pluginConfig!.MQTTServerConfiguration).ConfigureAwait(false);
                }

                return mqttServerInstance!.GetServerDetails();
            }
        }

        private void UpdateDebugLevel()
        {
            this.LogDebug = pluginConfig!.DebugLogging;
            Logger.ConfigureLogging(LogDebug, pluginConfig.LogToFile, HomeSeerSystem);
        }

        private readonly static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly AsyncMonitor deviceManagerLock = new AsyncMonitor();
        private readonly AsyncMonitor mqttServerLock = new AsyncMonitor();
        private MqttServerInstance? mqttServerInstance;
        private PluginConfig? pluginConfig;
        private TasmotaDeviceManager? tasmotaDeviceManager;
    }
}