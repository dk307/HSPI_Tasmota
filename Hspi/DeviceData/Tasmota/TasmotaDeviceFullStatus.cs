using System.Collections.Generic;
using System.Collections.Immutable;

#nullable enable

namespace Hspi.DeviceData.Tasmota
{
    internal sealed class TasmotaDeviceFullStatus : TasmotaDeviceStatus
    {
        public TasmotaDeviceFullStatus(string jsonStatus,
                                 IDictionary<string, string> switchText,
                                 string fullTopicPrefix3) :
            base(jsonStatus)
        {
            SwitchText = switchText.ToImmutableDictionary();
            this.MQTTPrefix3FinalTopic = CalculateFinalPrefix3MQTTTopic(fullTopicPrefix3);
        }

        public string MQTTPrefix3FinalTopic { get; }

        public ImmutableDictionary<string, string> SwitchText { get; }

        private string CalculateFinalPrefix3MQTTTopic(string fullTopicPrefix3)
        {
            if (fullTopicPrefix3.Contains("%topic%"))
            {
                fullTopicPrefix3 = fullTopicPrefix3.Replace("%topic%", MQTTTopic);
            }

            if (fullTopicPrefix3.Contains("%id%"))
            {
                fullTopicPrefix3 = fullTopicPrefix3.Replace("%id%", MacAdddress?.Replace(":", ""));
            }

            if (fullTopicPrefix3.Contains("%hostname%"))
            {
                fullTopicPrefix3 = fullTopicPrefix3.Replace("%hostname%", Hostname);
            }

            if (fullTopicPrefix3[fullTopicPrefix3.Length - 1] != '/')
            {
                fullTopicPrefix3 += '/';
            }

            return fullTopicPrefix3;
        }
    }
}