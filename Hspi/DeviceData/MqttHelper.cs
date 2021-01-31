using MQTTnet.Diagnostics;
using NLog;

#nullable enable

namespace Hspi.DeviceData
{
    internal static class MqttHelper
    {
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

        private readonly static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
    }
}