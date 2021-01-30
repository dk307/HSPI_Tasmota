using Newtonsoft.Json.Linq;
using NullGuard;
using System.Collections.Generic;

#nullable enable

namespace Hspi.DeviceData
{
     internal class TasmotaStatus
    {
        public TasmotaStatus(TasmotaDeviceFeature.FeatureSource sourceType, JObject? source)
        {
            SourceType = sourceType;
            this.source = source;
        }

        public TasmotaDeviceFeature.FeatureSource SourceType { get; }

        public T? GetFeatureValue<T>(TasmotaDeviceFeature feature)
        {
            if (source != null)
            {
                var token = source.SelectToken(feature.Id);
                if (token != null)
                {
                    return token.ToObject<T>();
                }
            }

            throw new KeyNotFoundException(feature.Id);
        }

        private readonly JObject? source;
    };
}