#nullable enable

namespace Hspi.DeviceData
{
    internal sealed record MqttServerDetails
    {
        public MqttServerDetails(string host, int port)
        {
            Host = host;
            Port = port;
        }

        public string Host { get; }
        public int Port { get; }
    }
}