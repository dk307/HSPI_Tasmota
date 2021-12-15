using Hspi.DeviceData;
using Hspi.DeviceData.Tasmota;
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
        public IList<IDictionary<string, object>> GetDevices()
        {
            return GetDevicesAsync().ResultForSync();

            async Task<IList<IDictionary<string, object>>> GetDevicesAsync()
            {
                var list = new List<IDictionary<string, object>>();
                var statusMap = new Dictionary<int, Task<TasmotaDeviceStatus>>();

                var devices = await this.GetTasmotaDevices().ConfigureAwait(false);

                foreach (var pair in devices)
                {
                    TasmotaDeviceInfo? data = pair.Value.Data;
                    if (data != null)
                    {
                        statusMap.Add(pair.Key,
                                      TasmotaDeviceInterface.GetStatus(data, ShutdownCancellationToken));
                    }
                }

                foreach (var pair in devices)
                {
                    var data = new Dictionary<string, object>
                    {
                        { "refId", pair.Key }
                    };

                    var tasmotaData = pair.Value.Data;
                    data.Add("uri", tasmotaData?.Uri?.ToString() ?? string.Empty);

                    try
                    {
                        if (statusMap.TryGetValue(pair.Key, out var task))
                        {
                            var status = await task.ConfigureAwait(false);

                            data.Add("Version", status.Version ?? string.Empty);
                            data.Add("BuildDateTime", status.BuildDateTime ?? string.Empty);
                            data.Add("BootCount", status.BootCount ?? string.Empty);
                            data.Add("UpTime", status.Uptime ?? string.Empty);
                            data.Add("RestartReason", status.RestartReason ?? string.Empty);
                        }
                    }
                    catch
                    {
                        // Ignore errors while reading tasmota
                    }

                    list.Add(data);
                }

                return list;
            }
        }

        public IDictionary<string, object> GetGeneralInformation()
        {
            var configuration = new Dictionary<string, object>
            {
                [DebugLoggingConfiguration] = pluginConfig!.DebugLogging,
                [LogToFileConfiguration] = pluginConfig!.LogToFile
            };
            return configuration;
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

                if (!string.IsNullOrEmpty(configuration["boundipaddress"]) &&
                   !IPAddress.TryParse(configuration["boundipaddress"], out var ipAddress))
                {
                    errors.Add("IP Address is not valid");
                }

                if (errors.Count == 0)
                {
                    pluginConfig!.MQTTServerConfiguration =
                        ScribanHelper.FromDictionary<MqttServerConfiguration>(configuration);

                    PluginConfigChanged();
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex.GetFullMessage());
            }
            return errors;
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

        private const string DebugLoggingConfiguration = "debuglogging";
        private const string LogToFileConfiguration = "logtofile";
    }
}