using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace Hspi.DeviceData
{
    internal class TasmotaStatus
    {
        public TasmotaStatus(string jsonStatus)
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

        public IList<TasmotaDeviceInfo.TasmotaEnabledFeature> GetPossibleFeatures()
        {
            var list = new List<TasmotaDeviceInfo.TasmotaEnabledFeature>();

            list.AddRange(AddForChild(deviceStatus["StatusSTS"].DeepClone() as JObject, 
                                      TasmotaDeviceInfo.TasmotaEnabledFeature.FeatureType.State));
            list.AddRange(AddForChild(deviceStatus["StatusSNS"].DeepClone() as JObject, 
                                      TasmotaDeviceInfo.TasmotaEnabledFeature.FeatureType.Sensor));

            return list;

            IList<TasmotaDeviceInfo.TasmotaEnabledFeature> AddForChild(JObject child,
                                                                       TasmotaDeviceInfo.TasmotaEnabledFeature.FeatureType featureType)
            {
                IEnumerable<JToken> jTokens = child.Descendants().Where(p => !p.Any());
                var childResults = jTokens.Aggregate(new List<TasmotaDeviceInfo.TasmotaEnabledFeature>(),
                                                     (paths, jToken) =>
                {
                    paths.Add(new TasmotaDeviceInfo.TasmotaEnabledFeature(jToken.Path, jToken.Path, featureType));
                    return paths;
                });

                return childResults;
            }
        }

        private readonly JObject deviceStatus;
    }
}