using Newtonsoft.Json.Linq;
using NullGuard;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Hspi.DeviceData
{
    internal readonly struct MqttDetails
    {
        public MqttDetails(string host, int port)
        {
            Host = host;
            Port = port;
        }

        public readonly string Host;
        public readonly int Port;
    }

    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class TasmotaFullStatus
    {
        public TasmotaFullStatus(string jsonStatus,
                                 IDictionary<string, string> switchText,
                                 IDictionary<string, string> mqttStatus)
        {
            deviceStatus = JObject.Parse(jsonStatus);
            SwitchText = switchText.ToImmutableDictionary();
            MqttStatus = mqttStatus.ToImmutableDictionary();
        }

        public string DeviceName => deviceStatus["Status"]["DeviceName"].ToString();
        public string Version => deviceStatus["StatusFWR"]["Version"].ToString();
        public string BuildDateTime => deviceStatus["StatusFWR"]["BuildDateTime"].ToString();
        public string RestartReason => deviceStatus["StatusPRM"]["RestartReason"].ToString();
        public string Uptime => deviceStatus["StatusPRM"]["Uptime"].ToString();
        public string BootCount => deviceStatus["StatusPRM"]["BootCount"].ToString();
 
        public MqttDetails MqttServerDetails
        {
            get
            {
                // "MqttHost":"192.168.1.135","MqttPort":1883,"MqttClientMask":"DVES_%06X","MqttClient":"DVES_07E83D","MqttUser":"DVES_USER","MqttCount":1,"MAX_PACKET_SIZE":1200,"KEEPALIVE":30}}
                var details = deviceStatus["StatusMQT"];

                return new MqttDetails(details["MqttHost"].ToObject<string>(),
                                       details["MqttPort"].ToObject<int>());
            }
        }

        public ImmutableDictionary<string, string> MqttStatus { get; }

        public ImmutableDictionary<string, string> SwitchText { get; }

        public IList<TasmotaDeviceFeature> GetPossibleFeatures()
        {
            var list = new List<TasmotaDeviceFeature>();

            list.AddRange(AddForChild(TasmotaDeviceFeature.FeatureSource.State));
            list.AddRange(AddForChild(TasmotaDeviceFeature.FeatureSource.Sensor));

            return list;

            IList<TasmotaDeviceFeature> AddForChild(TasmotaDeviceFeature.FeatureSource featureType)
            {
                var child = GetObject(featureType) as JObject;

                if (child != null)
                {
                    IEnumerable<JToken> jTokens = child.Descendants().Where(p => !p.Any());
                    var childResults = jTokens.Aggregate(new List<TasmotaDeviceFeature>(),
                                                         (paths, jToken) =>
                    {
                        paths.Add(new TasmotaDeviceFeature(jToken.Path, jToken.Path, featureType, null));
                        return paths;
                    });
                    return childResults;
                }

                return new List<TasmotaDeviceFeature>();
            }
        }

        public TasmotaStatus GetStatus(TasmotaDeviceFeature.FeatureSource type)
        {
            return new TasmotaStatus(type, GetObject(type) as JObject);
        }

        private JToken GetObject(TasmotaDeviceFeature.FeatureSource type)
        {
            switch (type)
            {
                case TasmotaDeviceFeature.FeatureSource.Sensor:
                    return deviceStatus["StatusSNS"]?.DeepClone();

                case TasmotaDeviceFeature.FeatureSource.State:
                    return deviceStatus["StatusSTS"]?.DeepClone();
            }

            return null;
        }

        private readonly JObject deviceStatus;
    }
}