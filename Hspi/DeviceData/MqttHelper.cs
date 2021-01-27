using MQTTnet.Diagnostics;
using NLog;

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
                        logEventInfo.Level = LogLevel.Debug;
                        break;

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
                logEventInfo.Message = logMessage.Message;

                logger.Log(logEventInfo);
            };
        }

        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
    }
}