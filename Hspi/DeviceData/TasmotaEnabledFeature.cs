using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;

namespace Hspi.DeviceData
{
    internal sealed partial class TasmotaDeviceInfo
    {
        public sealed class TasmotaEnabledFeature
        {
            public TasmotaEnabledFeature(string id, string name, FeatureType type)
            {
                Id = id;
                Name = name;
                this.Type = type;
            }

            public enum FeatureType
            {
                Sensor = 0,
                State = 1,
            };

            public string Id { get; }
            public string Name { get; }

            public FeatureType Type { get; }

            public override bool Equals(object obj)
            {
                return obj is TasmotaEnabledFeature feature &&
                       Id == feature.Id &&
                       Name == feature.Name &&
                       Type == feature.Type;
            }

            public override int GetHashCode()
            {
                int hashCode = -678952093;
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Id);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
                hashCode = hashCode * -1521134295 + Type.GetHashCode();
                return hashCode;
            }

            public override string ToString()
            {
                return $"{Id} - {Type}";
            }
        };
    }
}