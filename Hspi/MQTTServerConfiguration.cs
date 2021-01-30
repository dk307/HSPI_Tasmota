using System.Net;

#nullable enable

namespace Hspi
{
    internal sealed record MQTTServerConfiguration
    {
        public readonly bool Enable;
        public readonly IPAddress? BoundIPAddress;

        public MQTTServerConfiguration(bool enable, IPAddress? boundIPAddress)
        {
            Enable = enable;
            BoundIPAddress = boundIPAddress;
        }
    }
}