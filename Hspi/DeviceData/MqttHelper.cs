using MQTTnet;
using MQTTnet.Diagnostics;
using MQTTnet.Server;
using Newtonsoft.Json;
using NLog;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
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
            var buffer = Encoding.UTF8.GetBytes(data);

            using var fs = new FileStream(Filename, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, buffer.Length, true);
            return fs.WriteAsync(buffer, 0, buffer.Length);
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

    internal static class MqttHelper
    {
        public static async Task<IMqttServer> StartServer(CancellationToken cancellationToken)
        {
            string hsDir = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            string storagefile = Path.Combine(hsDir, "data", PlugInData.PlugInId, "retained.json");

            // Configure MQTT server.
            var optionsBuilder = new MqttServerOptionsBuilder()
                .WithConnectionBacklog(512)
                .WithStorage(new MqttStorage(storagefile))
                .WithDefaultEndpointPort(1883);

            var mqttServer = new MqttFactory().CreateMqttServer();
            await mqttServer.StartAsync(optionsBuilder.Build()).ConfigureAwait(false);
            cancellationToken.Register(async () => await mqttServer.StopAsync().ConfigureAwait(false));
            return mqttServer;
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
                        logEventInfo.Level = LogLevel.Debug;
                        break;

                    case MqttNetLogLevel.Info:
                        logEventInfo.Level = LogLevel.Debug;
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
                logEventInfo.Message = logMessage.Message;

                logger.Log(logEventInfo);
            };
        }

        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
    }
}