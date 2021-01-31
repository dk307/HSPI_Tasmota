using System.Collections.Generic;
using System.Collections.Immutable;

#nullable enable

namespace Hspi.DeviceData.Tasmota
{
    internal sealed class TasmotaDeviceFullStatus : TasmotaDeviceStatus
    {
        public TasmotaDeviceFullStatus(string jsonStatus,
                                 IDictionary<string, string> switchText,
                                 IDictionary<string, string> mqttStatus) :
            base(jsonStatus)
        {
            SwitchText = switchText.ToImmutableDictionary();
            MqttStatus = mqttStatus.ToImmutableDictionary();
        }

        public ImmutableDictionary<string, string> SwitchText { get; }
        public ImmutableDictionary<string, string> MqttStatus { get; }
    }
}