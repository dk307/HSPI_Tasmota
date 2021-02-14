using MQTTnet.Diagnostics;
using NLog;
using System;

#nullable enable

namespace Hspi.DeviceData
{
    internal sealed class MqttNetLogger : IMqttNetLogger, IMqttNetScopedLogger
    {
        public MqttNetLogger(string source = "[MQTT]")
        {
            this.source = source;
        }

        public IMqttNetScopedLogger CreateScopedLogger(string source)
        {
            return new MqttNetLogger($"{this.source}[{source}]");
        }

        public void Publish(MqttNetLogLevel logLevel,
                            string source,
                            string message, object[] parameters, System.Exception? exception)
        {
            LogEventInfo logEventInfo = new LogEventInfo();

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

        private readonly static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly string source;
    }
}