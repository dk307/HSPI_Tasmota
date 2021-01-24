using Newtonsoft.Json.Linq;
using NullGuard;
using System.Collections.Generic;
using System.Linq;

namespace Hspi.DeviceData
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]

    internal sealed class TasmotaFullStatus
    {
        public TasmotaFullStatus(string jsonStatus)
        {
            deviceStatus = JObject.Parse(jsonStatus);
        }

        public string DeviceName
        {
            get
            {
                return deviceStatus["Status"]["DeviceName"].ToString();
            }
        }

        public T GetValue<T>(TasmotaDeviceFeature feature)
        {
            var child = GetObject(feature.Source) as JObject;

            if (child != null)
            {
                var token = child.SelectToken(feature.Id);
                return token.ToObject<T>();
            }

            throw new KeyNotFoundException();
        }

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