﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace Hspi.DeviceData.Tasmota
{
    internal class TasmotaDeviceStatus
    {
        public TasmotaDeviceStatus(string jsonStatus)
        {
            deviceStatus = JObject.Parse(jsonStatus);
        }

        public string? BootCount => GetStringValue("StatusPRM", "BootCount");

        public string? BuildDateTime => GetStringValue("StatusFWR", "BuildDateTime");

        public string? DeviceName => GetStringValue("Status", "DeviceName");
        public string? MQTTTopic => GetStringValue("Status", "Topic");
        public string? MacAdddress => GetStringValue("StatusNET", "Mac");
        public string? Hostname => GetStringValue("StatusNET", "Hostname");

        public MqttServerDetails MqttServerDetails
        {
            get
            {
                // "MqttHost":"192.168.1.135","MqttPort":1883,"MqttClientMask":"DVES_%06X","MqttClient":"DVES_07E83D","MqttUser":"DVES_USER","MqttCount":1,"MAX_PACKET_SIZE":1200,"KEEPALIVE":30}}
                int? port = GetValue<int>("StatusMQT", "MqttPort");
                return new MqttServerDetails(GetStringValue("StatusMQT", "MqttHost") ?? throw new KeyNotFoundException(),
                                       port ?? throw new KeyNotFoundException());
            }
        }

        public string? RestartReason => GetStringValue("StatusPRM", "RestartReason");

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
                if (GetObject(featureType) is JObject child)
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

        public TasmotaFeatureSourceStatus GetStatus(TasmotaDeviceFeature.FeatureSource type)
        {
            return new TasmotaFeatureSourceStatus(type, GetObject(type) as JObject);
        }

        private JToken? GetObject(TasmotaDeviceFeature.FeatureSource type)
        {
            return type switch
            {
                TasmotaDeviceFeature.FeatureSource.Sensor => deviceStatus["StatusSNS"]?.DeepClone(),
                TasmotaDeviceFeature.FeatureSource.State => deviceStatus["StatusSTS"]?.DeepClone(),
                _ => null,
            };
        }

        private string? GetStringValue(params string[] parameters)
        {
            JToken? token = GetToken(parameters);
            return token?.ToObject<string>();
        }

        private JToken? GetToken(string[] parameters)
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

            return token;
        }

        private Nullable<T> GetValue<T>(params string[] parameters) where T : struct
        {
            JToken? token = GetToken(parameters);

            if (token != null)
            {
                return token.ToObject<T>();
            }

            return null;
        }

        private readonly JObject deviceStatus;
    }
}