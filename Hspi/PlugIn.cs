using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi.DeviceData;
using Hspi.Utils;
using Nito.AsyncEx;
using NullGuard;
using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Hspi
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal partial class PlugIn : HspiBase
    {
        public PlugIn()
            : base(PlugInData.PlugInId, PlugInData.PlugInName)
        {
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
            if (!disposedValue)
            {
                disposedValue = true;
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

                // Device Add Page
                HomeSeerSystem.RegisterDeviceIncPage(Id, "adddevice.html", "Add Tasmota Device");

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

        private void UpdateDebugLevel()
        {
            this.LogDebug = pluginConfig.DebugLogging;
            Logger.ConfigureLogging(LogDebug, HomeSeerSystem);
        }

        private readonly AsyncMonitor deviceManagerLock = new AsyncMonitor();
        private TasmotaDeviceManager tasmotaDeviceManager;

        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private bool disposedValue;
        private PluginConfig pluginConfig;
    }
}