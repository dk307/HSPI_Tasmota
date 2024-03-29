﻿using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;

#nullable enable

namespace Hspi.Utils
{
    internal static class ScribanHelper
    {
        public static T FromDictionary<T>(IDictionary<string, string> source) where T : class
        {
            var json = JsonConvert.SerializeObject(source, Formatting.None);
            return Deserialize<T>(json);
        }

        public static IDictionary<string, object> ToDictionary<T>(T obj)
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new LowercaseContractResolver()
            };
            var json = JsonConvert.SerializeObject(obj, Formatting.None, settings);
            var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            return dict ?? throw new InvalidOperationException();
        }

        public static IDictionary<string, string> ToDictionaryS<T>(T obj)
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new LowercaseContractResolver()
            };
            var json = JsonConvert.SerializeObject(obj, Formatting.None, settings);
            var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            return dict ?? throw new InvalidOperationException();
        }

        private static T Deserialize<T>(string json) where T : class
        {
            JsonSerializer serializer = new();
            serializer.Converters.Add(new BoolConverter());
            using StringReader stringReader = new(json);
            using JsonTextReader reader = new(stringReader);
            var obj = serializer.Deserialize<T>(reader);
            if (obj == null)
            {
                throw new InvalidOperationException("Conversion Failed");
            }
            return obj;
        }

        private sealed class BoolConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(bool);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            {
                return reader?.Value?.ToString() == "on";
            }

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            {
                if (value == null) { writer.WriteValue(Off); }
                else
                {
                    writer.WriteValue(((bool)value) ? On : Off);
                }
            }

            private const string Off = "off";
            private const string On = "on";
        }

        private sealed class LowercaseContractResolver : DefaultContractResolver
        {
            protected override string ResolvePropertyName(string propertyName)
            {
#pragma warning disable CA1308 // Normalize strings to uppercase
                return propertyName.ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase
            }
        }
    }
}