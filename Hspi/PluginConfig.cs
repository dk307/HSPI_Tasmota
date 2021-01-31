using HomeSeer.PluginSdk;
using Nito.AsyncEx;
using System.Diagnostics.CodeAnalysis;
using System.Net;

#nullable enable

namespace Hspi
{
    internal sealed class PluginConfig : PluginConfigBase
    {
        public PluginConfig(IHsController HS) : base(HS)
        {
            this.mQTTServerConfiguration = LoadDBSettings();
        }

        [DisallowNull]
        public MQTTServerConfiguration MQTTServerConfiguration
        {
            get
            {
                using (var sync = configLock.ReaderLock())
                {
                    return mQTTServerConfiguration;
                }
            }

            set
            {
                using (var sync = configLock.WriterLock())
                {
                    mQTTServerConfiguration = value;
                    SetValue(MQTTServerIPAddressKey, value.BoundIPAddress);
                    SetValue(MQTTServerPortKey, value.Port);
                }
            }
        }

        private MQTTServerConfiguration LoadDBSettings()
        {
            string ipAddressString = GetValue(MQTTServerIPAddressKey, string.Empty);
            int port = GetValue(MQTTServerPortKey, 1883);

            if (!IPAddress.TryParse(ipAddressString, out var ipAddress))
            {
                ipAddress = null;
            }

            return new MQTTServerConfiguration( ipAddress, port);
        }

        private MQTTServerConfiguration mQTTServerConfiguration;
        private readonly AsyncReaderWriterLock configLock = new AsyncReaderWriterLock();
        private const string MQTTServerIPAddressKey = "MQTTServerIPAddress";
        private const string MQTTServerPortKey = "MQTTServerPort";
    }
}