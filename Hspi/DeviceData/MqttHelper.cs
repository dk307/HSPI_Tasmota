using MQTTnet;
using MQTTnet.Diagnostics;
using MQTTnet.Server;
using Newtonsoft.Json;
using NLog;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

#nullable enable

namespace Hspi.DeviceData
{
    internal sealed class MqttStorage : IMqttServerStorage
    {
        public MqttStorage(string filename)
        {
            Filename = filename;
        }

        public string Filename { get; }

        public Task SaveRetainedMessagesAsync(IList<MqttApplicationMessage> messages)
        {
            var directory = Path.GetDirectoryName(Filename);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string data = JsonConvert.SerializeObject(messages);
            File.WriteAllText(Filename, data);

            return Task.CompletedTask;
        }

        public Task<IList<MqttApplicationMessage>> LoadRetainedMessagesAsync()
        {
            IList<MqttApplicationMessage> retainedMessages;
            if (File.Exists(Filename))
            {
                var json = File.ReadAllText(Filename);
                retainedMessages = JsonConvert.DeserializeObject<List<MqttApplicationMessage>>(json);
            }
            else
            {
                retainedMessages = new List<MqttApplicationMessage>();
            }

            return Task.FromResult(retainedMessages);
        }
    };

    internal sealed class MqttServerInstance
    {
        public MqttServerInstance(IMqttServer? server,
                                  MQTTServerConfiguration configuration)
        {
            this.Server = server;
            this.Configuration = configuration;
        }

        public async Task Stop()
        {
            if (Server != null)
            {
                await Server.StopAsync().ConfigureAwait(false);
                Server.Dispose();
            }
        }

        public MQTTServerConfiguration Configuration { get; }
        public IMqttServer? Server { get; }
    }

    internal static class MqttHelper
    {
        public static async Task<MqttServerInstance> StartServer(MQTTServerConfiguration serverConfiguration)
        {
            if (serverConfiguration.Enabled)
            {
                logger.Info($"Starting Mqtt Server");
                string hsDir = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
                string storagefile = Path.Combine(hsDir, "data", PlugInData.PlugInId, "retained.json");

                // Configure MQTT server.
                var optionsBuilder = new MqttServerOptionsBuilder()
                    .WithConnectionBacklog(512)
                    .WithStorage(new MqttStorage(storagefile))
                    .WithDefaultEndpointPort(1883);

                if (serverConfiguration.BoundIPAddress != null)
                {
                    optionsBuilder = optionsBuilder.WithDefaultEndpointBoundIPAddress(serverConfiguration.BoundIPAddress);
                }

                var mqttServer = new MqttFactory().CreateMqttServer();
                await mqttServer.StartAsync(optionsBuilder.Build()).ConfigureAwait(false);
                return new MqttServerInstance(mqttServer, serverConfiguration);
            }
            return new MqttServerInstance(null, serverConfiguration);
        }

        public static void SetLogging()
        {
            MqttNetGlobalLogger.LogMessagePublished += (s, e) =>
            {
                var logMessage = e.LogMessage;
                LogEventInfo logEventInfo = new LogEventInfo();

                switch (e.LogMessage.Level)
                {
                    case MqttNetLogLevel.Verbose:
                        return;

                    case MqttNetLogLevel.Info:
                        logEventInfo.Level = LogLevel.Info;
                        break;

                    case MqttNetLogLevel.Warning:
                        logEventInfo.Level = LogLevel.Warn;
                        break;

                    case MqttNetLogLevel.Error:
                        logEventInfo.Level = LogLevel.Error;
                        break;
                }

                logEventInfo.Exception = logMessage.Exception;
                logEventInfo.TimeStamp = logMessage.Timestamp;
                logEventInfo.Message = "[MQTT]" + logMessage.Message;

                logger.Log(logEventInfo);
            };
        }

        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
    }
}