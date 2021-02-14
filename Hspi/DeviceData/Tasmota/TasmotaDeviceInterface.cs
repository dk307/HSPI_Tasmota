using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Hspi.DeviceData.Tasmota
{
    internal static class TasmotaDeviceInterface
    {
        public static async Task ForceSendMQTTStatus(TasmotaDeviceInfo data, CancellationToken cancellationToken)
        {
            await SendWebCommandForString(data, "Backlog State; Delay 10; State", cancellationToken).ConfigureAwait(false);
        }

        public static async Task<TasmotaDeviceFullStatus> GetFullStatus(TasmotaDeviceInfo data, CancellationToken cancellationToken)
        {
            return new TasmotaDeviceFullStatus(await SendWebCommandForString(data, "STATUS 0", cancellationToken).ConfigureAwait(false),
                                         await SendWebCommandToDeviceAsDict(data, "STATETEXT", cancellationToken).ConfigureAwait(false),
                                         await GetMqttFullTopicForPrefix3(data, cancellationToken).ConfigureAwait(false));
        }

        public static async Task<TasmotaDeviceStatus> GetStatus(TasmotaDeviceInfo data, CancellationToken cancellationToken)
        {
            return new TasmotaDeviceStatus(await SendWebCommandForString(data, "STATUS 0", cancellationToken).ConfigureAwait(false));
        }

        public static async Task<int> GetTelePeriod(TasmotaDeviceInfo data, CancellationToken cancellationToken)
        {
            string command = Invariant($"TelePeriod");
            var resultString = await SendWebCommandForString(data, command, cancellationToken).ConfigureAwait(false);
            var jobject = JObject.Parse(resultString);
            return jobject?[command]?.ToObject<int>() ?? throw new KeyNotFoundException("TelePeriod");
        }

        public static async Task SendOnOffCommand(TasmotaDeviceInfo data, string command, bool isOn,
                                                          CancellationToken cancellationToken)
        {
            await SendWebCommandForString(data, Invariant($"{command} {isOn}"), cancellationToken).ConfigureAwait(false);
        }

        public static async Task SetTelePeriod(TasmotaDeviceInfo data, int value, CancellationToken cancellationToken)
        {
            string command = Invariant($"TelePeriod {value}");
            await SendWebCommandForString(data, command, cancellationToken).ConfigureAwait(false);
        }

        public static async Task UpdateMqttServerDetails(TasmotaDeviceInfo data, MqttServerDetails mqttServerDetails, CancellationToken cancellationToken)
        {
            string command = Invariant($"Backlog SetOption3 1; MqttHost {mqttServerDetails.Host}; MqttPort {mqttServerDetails.Port}; MqttUser ; MqttPassword ;");
            await SendWebCommandForString(data, command, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<string> GetMqttFullTopicForPrefix3(TasmotaDeviceInfo data, CancellationToken cancellationToken)
        {
            var fullTopic = await SendWebCommandToDeviceAsString(data, "FullTopic", cancellationToken).ConfigureAwait(false);

            if (fullTopic.Contains("%prefix%"))
            {
                var prefix3 = await SendWebCommandToDeviceAsString(data, "Prefix3", cancellationToken).ConfigureAwait(false);
                fullTopic = fullTopic.Replace("%prefix%", prefix3);
            }

            return fullTopic;
        }

        private static async Task<HttpResponseMessage> SendWebCommand(TasmotaDeviceInfo data, string command, CancellationToken cancellationToken)
        {
            var queryList = new List<string>();
            if (!string.IsNullOrEmpty(data.User))
            {
                queryList.Add(Invariant($"user={WebUtility.UrlEncode(data.User)}"));
                queryList.Add(Invariant($"password={WebUtility.UrlEncode(data.Password)}"));
            }

            queryList.Add(Invariant($"cmnd={WebUtility.UrlEncode(command)}"));

            var uriBuilder = new UriBuilder(data.Uri);
            uriBuilder.Path = "/cm";
            uriBuilder.Query = String.Join("&", queryList);

            var result = await httpClient.GetAsync(uriBuilder.Uri, cancellationToken).ConfigureAwait(false);
            result.EnsureSuccessStatusCode();
            return result;
        }

        private static async Task<string> SendWebCommandForString(TasmotaDeviceInfo data,
                                                                         string command,
                                                                 CancellationToken cancellationToken)
        {
            using var result = await SendWebCommand(data, command, cancellationToken).ConfigureAwait(false);
            return await result.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
        private static async Task<IDictionary<string, string>> SendWebCommandToDeviceAsDict(TasmotaDeviceInfo data,
                                                                                            string command,
                                                                                            CancellationToken cancellationToken)
        {
            var result = await SendWebCommandForString(data, command, cancellationToken).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<IDictionary<string, string>>(result);
        }

        private static async Task<string> SendWebCommandToDeviceAsString(TasmotaDeviceInfo data,
                                                                                    string command,
                                                                                    CancellationToken cancellationToken)
        {
            var result = await SendWebCommandForString(data, command, cancellationToken).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<IDictionary<string, string>>(result)[command];
        }

        private static HttpClient httpClient = new HttpClient();
    }
}