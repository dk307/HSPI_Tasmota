using Newtonsoft.Json;
using System;
using System.Net;

#nullable enable

namespace Hspi
{
    internal sealed record MqttServerConfiguration
    {
        [JsonConverter(typeof(IPAddressConverter))]
        public IPAddress? BoundIPAddress { get; }
        public int Port { get; }

        public MqttServerConfiguration(IPAddress? boundIPAddress, int port)
        {
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
                writer.WriteValue(value?.ToString());
            }

            public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            {
                var value = (string?)reader.Value;
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