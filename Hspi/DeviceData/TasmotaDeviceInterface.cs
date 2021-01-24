using Newtonsoft.Json;
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
    internal static class TasmotaDeviceInterface
    {
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

        public static async Task<TasmotaFullStatus> GetStatus(TasmotaDeviceInfo data, CancellationToken cancellationToken)
        {
            string deviceDetails = await SendWebCommandToDevice(data, "STATUS 0", cancellationToken).ConfigureAwait(false);
            return new TasmotaFullStatus(deviceDetails);
        }

        public static async Task<IDictionary<string, string>> GetSwitchText(TasmotaDeviceInfo data, CancellationToken cancellationToken)
        {
            string stateText = await SendWebCommandToDevice(data, "STATETEXT", cancellationToken).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<IDictionary<string, string>>(stateText);
        }

        private static HttpClient httpClient = new HttpClient();
    }
}