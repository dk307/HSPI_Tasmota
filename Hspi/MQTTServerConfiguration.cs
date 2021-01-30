using Newtonsoft.Json;
using System;
using System.Net;

#nullable enable

namespace Hspi
{
    internal sealed record MQTTServerConfiguration
    {
        public readonly bool Enabled;

        [JsonConverter(typeof(IPAddressConverter))]
        public readonly IPAddress? BoundIPAddress;
        public readonly int Port;

        public MQTTServerConfiguration(bool enabled, IPAddress? boundIPAddress, int port)
        {
            Enabled = enabled;
            BoundIPAddress = boundIPAddress;
            Port = port;
        }

        internal class IPAddressConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return (objectType == typeof(IPAddress));
            }

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            {
                writer.WriteValue(value?.ToString() ?? null);
            }

            public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            {
                var value = (string?) reader.Value;
                if (!string.IsNullOrEmpty(value))
                {
                    return IPAddress.Parse(value);
                }
                else
                {
                    return null;
                }
            }
        }
    }
}