using Hspi.DeviceData;
using Hspi.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Hspi
{
    internal partial class PlugIn : HspiBase
    {
        private const string DebugLoggingConfiguration = "debuglogging";
        private const string LogToFileConfiguration = "logtofile";

        public IDictionary<string, object> GetGeneralInformation()
        {
            var configuration = new Dictionary<string, object>();
            configuration[DebugLoggingConfiguration] = pluginConfig.DebugLogging;
            configuration[LogToFileConfiguration] = pluginConfig.LogToFile;
            return configuration;
        }

        public IList<string> UpdateGeneralConfiguration(IDictionary<string, string> configuration)
        {
            var errors = new List<string>();
            try
            {
                pluginConfig.DebugLogging = CheckBoolValue(DebugLoggingConfiguration);
                pluginConfig.LogToFile = CheckBoolValue(LogToFileConfiguration);
                PluginConfigChanged();
            }
            catch (Exception ex)
            {
                errors.Add(ex.GetFullMessage());
            }
            return errors;

            bool CheckBoolValue(string key)
            {
                return configuration.ContainsKey(key) && configuration[key] == "on";
            }
        }

        public IList<IDictionary<string, object>> GetDevices()
        {
            return GetDevicesAsync().ResultForSync();

            async Task<IList<IDictionary<string, object>>> GetDevicesAsync()
            {
                var list = new List<IDictionary<string, object>>();
                var statusMap = new Dictionary<int, Task<TasmotaFullStatus>>();

                var devices = await this.GetTasmotaDevices().ConfigureAwait(false);

                foreach (var pair in devices)
                {
                    statusMap.Add(pair.Key, pair.Value.GetStatus());
                }

                foreach (var pair in devices)
                {
                    var data = new Dictionary<string, object>();

                    data.Add("refId", pair.Key);

                    var tasmotaData = pair.Value.Data;
                    data.Add("uri", tasmotaData.Uri);

                    try
                    {
                        var status = await statusMap[pair.Key].ConfigureAwait(false);

                        data.Add("Version", status.Version);
                        data.Add("BuildDateTime", status.BuildDateTime);
                        data.Add("BootCount", status.BootCount);
                        data.Add("UpTime", status.Uptime);
                        data.Add("RestartReason", status.RestartReason);
                    }
                    catch { }

                    list.Add(data);
                }

                return list;
            }
        }
    }
}