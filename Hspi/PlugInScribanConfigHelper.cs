using Hspi.DeviceData;
using Hspi.Utils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using static System.FormattableString;

#nullable enable

namespace Hspi
{
    internal partial class PlugIn : HspiBase
    {
        private const string DebugLoggingConfiguration = "debuglogging";
        private const string LogToFileConfiguration = "logtofile";

        public IDictionary<string, object> GetGeneralInformation()
        {
            var configuration = new Dictionary<string, object>();
            configuration[DebugLoggingConfiguration] = pluginConfig!.DebugLogging;
            configuration[LogToFileConfiguration] = pluginConfig!.LogToFile;
            return configuration;
        }

        public IList<string> UpdateGeneralConfiguration(IDictionary<string, string> configuration)
        {
            var errors = new List<string>();
            try
            {
                pluginConfig!.DebugLogging = CheckBoolValue(configuration, DebugLoggingConfiguration);
                pluginConfig!.LogToFile = CheckBoolValue(configuration, LogToFileConfiguration);
                PluginConfigChanged();
            }
            catch (Exception ex)
            {
                errors.Add(ex.GetFullMessage());
            }
            return errors;
            
            static bool CheckBoolValue(IDictionary<string, string> configuration, string key)
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
                    data.Add("uri", tasmotaData?.Uri?.ToString() ?? string.Empty);

                    try
                    {
                        var status = await statusMap[pair.Key].ConfigureAwait(false);

                        data.Add("Version", status.Version ?? string.Empty);
                        data.Add("BuildDateTime", status.BuildDateTime ?? string.Empty);
                        data.Add("BootCount", status.BootCount ?? string.Empty);
                        data.Add("UpTime", status.Uptime ?? string.Empty);
                        data.Add("RestartReason", status.RestartReason ?? string.Empty);
                    }
                    catch { }

                    list.Add(data);
                }

                return list;
            }
        }

        public IDictionary<string, object> GetMQTTServerConfiguration()
        {
            return ScribanHelper.ToDictionary(pluginConfig!.MQTTServerConfiguration);
        }

        public IList<string> SaveMQTTServerConfiguration(IDictionary<string, string> configuration)
        {
            var errors = new List<string>();
            try
            {
                logger.Debug(Invariant($"Updating MQTT Server Information"));

                IPAddress? ipAddress = null;
                if (!string.IsNullOrEmpty(configuration["boundipaddress"]) &&
                    !IPAddress.TryParse(configuration["boundipaddress"], out ipAddress))
                {
                    errors.Add("IP Address is not valid");
                }

                if (errors.Count == 0)
                {
                    pluginConfig!.MQTTServerConfiguration =
                        ScribanHelper.FromDictionary<MQTTServerConfiguration>(configuration);

                    PluginConfigChanged();
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex.GetFullMessage());
            }
            return errors;
        }
    }
}