using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

#nullable enable

namespace Hspi.DeviceData
{
    internal sealed record MqttDetails
    {
        public MqttDetails(string host, int port)
        {
            Host = host;
            Port = port;
        }

        public readonly string Host;
        public readonly int Port;
    }

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

        public string? BootCount => GetStringValue("StatusPRM", "BootCount");

        public string? BuildDateTime => GetStringValue("StatusFWR", "BuildDateTime");

        public string? DeviceName => GetStringValue("Status", "DeviceName");

        public MqttDetails MqttServerDetails
        {
            get
            {
                // "MqttHost":"192.168.1.135","MqttPort":1883,"MqttClientMask":"DVES_%06X","MqttClient":"DVES_07E83D","MqttUser":"DVES_USER","MqttCount":1,"MAX_PACKET_SIZE":1200,"KEEPALIVE":30}}
                int? port = GetValue<int>("StatusMQT", "MqttPort");
                return new MqttDetails(GetStringValue("StatusMQT", "MqttHost") ?? throw new KeyNotFoundException(),
                                       port.HasValue ? port.Value : throw new KeyNotFoundException());
            }
        }

        public ImmutableDictionary<string, string> MqttStatus { get; }

        public string? RestartReason => GetStringValue("StatusPRM", "RestartReason");

        public ImmutableDictionary<string, string> SwitchText { get; }

        public string? Uptime => GetStringValue("StatusPRM", "Uptime");

        public string? Version => GetStringValue("StatusFWR", "Version");

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

        private JToken? GetObject(TasmotaDeviceFeature.FeatureSource type)
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

        private Nullable<T> GetValue<T>(params string[] parameters) where T : struct
        {
            JToken? token = deviceStatus;
            foreach (var value in parameters)
            {
                if (token == null)
                {
                    break;
                }

                token = token[value];
            }

            if (token != null)
            {
                return token.ToObject<T>();
            }

            return null;
        }

        private string? GetStringValue(params string[] parameters)
        {
            JToken? token = deviceStatus;
            foreach (var value in parameters)
            {
                if (token == null)
                {
                    break;
                }

                token = token[value];
            }

            return token?.ToObject<string>();
        }

        private readonly JObject deviceStatus;
    }
}