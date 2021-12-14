using MQTTnet.Diagnostics.Logger;
using NLog;
using System;

#nullable enable

namespace Hspi.DeviceData
{
    internal sealed class MqttNetLogger : IMqttNetLogger
    {
        public MqttNetLogger(string source = "[MQTT]")
        {
            this.source = source;
        }

        public void Publish(MqttNetLogLevel logLevel,
                            string source,
                            string message, object[] parameters, System.Exception? exception)
        {
            LogEventInfo logEventInfo = new();

            switch (logLevel)
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

            logEventInfo.Exception = exception;
            logEventInfo.TimeStamp = DateTime.Now;
            logEventInfo.Message = $"{source} {message}";
            logEventInfo.Parameters = parameters;

            logger.Log(logEventInfo);
        }

        public void Publish(MqttNetLogLevel logLevel, string message, object[] parameters, System.Exception? exception)
        {
            Publish(logLevel, source, message, parameters, exception);
        }

        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly string source;
    }
}