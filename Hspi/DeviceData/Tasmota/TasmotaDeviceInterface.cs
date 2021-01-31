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
            await SendWebCommandToDevice(data, "Backlog State; Delay 10; State", cancellationToken).ConfigureAwait(false);
        }

        public static async Task<TasmotaDeviceFullStatus> GetFullStatus(TasmotaDeviceInfo data, CancellationToken cancellationToken)
        {
            return new TasmotaDeviceFullStatus(await SendWebCommandToDevice(data, "STATUS 0", cancellationToken).ConfigureAwait(false),
                                         await SendWebCommandToDeviceAsDict(data, "STATETEXT", cancellationToken).ConfigureAwait(false),
                                         await GetMqttTopics(data, cancellationToken).ConfigureAwait(false));
        }

        public static async Task<TasmotaDeviceStatus> GetStatus(TasmotaDeviceInfo data, CancellationToken cancellationToken)
        {
            return new TasmotaDeviceStatus(await SendWebCommandToDevice(data, "STATUS 0", cancellationToken).ConfigureAwait(false));
        }

        public static async Task<int> GetTelePeriod(TasmotaDeviceInfo data, CancellationToken cancellationToken)
        {
            string command = Invariant($"TelePeriod");
            var resultString = await SendWebCommandToDevice(data, command, cancellationToken).ConfigureAwait(false);
            var jobject = JObject.Parse(resultString);
            return jobject?[command]?.ToObject<int>() ?? throw new KeyNotFoundException("TelePeriod");
        }

        public static async Task SendOnOffCommand(TasmotaDeviceInfo data, string command, bool isOn,
                                                          CancellationToken cancellationToken)
        {
            await SendWebCommandToDevice(data, Invariant($"{command} {isOn}"), cancellationToken).ConfigureAwait(false);
        }
        public static async Task SetTelePeriod(TasmotaDeviceInfo data, int value, CancellationToken cancellationToken)
        {
            string command = Invariant($"TelePeriod {value}");
            await SendWebCommandToDevice(data, command, cancellationToken).ConfigureAwait(false);
        }

        public static async Task UpdateMqttServerDetails(TasmotaDeviceInfo data, MqttServerDetails mqttServerDetails, CancellationToken cancellationToken)
        {
            string command = Invariant($"Backlog MqttHost {mqttServerDetails.Host}; MqttPort {mqttServerDetails.Port}; MqttUser ; MqttPassword ;");
            await SendWebCommandToDevice(data, command, cancellationToken).ConfigureAwait(false);
        }
        private static async Task<IDictionary<string, string>> GetMqttTopics(TasmotaDeviceInfo data, CancellationToken cancellationToken)
        {
            var fullTopicResult = await SendWebCommandToDeviceAsDict(data, "FullTopic", cancellationToken).ConfigureAwait(false);
            var prefixResult = await SendWebCommandToDeviceAsDict(data, "Prefix", cancellationToken).ConfigureAwait(false);
            var topicResult = await SendWebCommandToDeviceAsDict(data, "Topic", cancellationToken).ConfigureAwait(false);

            string fullTopic = fullTopicResult["FullTopic"];
            fullTopic = fullTopic.Replace("%topic%", topicResult["Topic"]);

            var topics = new Dictionary<string, string>();

            foreach (var prefix in prefixResult)
            {
                topics.Add(prefix.Key, fullTopic.Replace("%prefix%", prefix.Value));
            }

            return topics;
        }

        private static async Task<string> SendWebCommandToDevice(TasmotaDeviceInfo data,
                                                                 string command,
                                                                 CancellationToken cancellationToken)
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
            return await result.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private static async Task<IDictionary<string, string>> SendWebCommandToDeviceAsDict(TasmotaDeviceInfo data,
                                                                                            string command,
                                                                                            CancellationToken cancellationToken)
        {
            var result = await SendWebCommandToDevice(data, command, cancellationToken).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<IDictionary<string, string>>(result);
        }

        private static HttpClient httpClient = new HttpClient();
    }
}