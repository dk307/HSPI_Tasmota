﻿using MQTTnet;
using MQTTnet.Server;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

#nullable enable

namespace Hspi.DeviceData
{
    internal sealed class MqttServerInstance
    {
        public MqttServerInstance(IMqttServer? server,
                                  MQTTServerConfiguration configuration)
        {
            this.Server = server;
            this.Configuration = configuration;
        }

        public MQTTServerConfiguration Configuration { get; }

        public IMqttServer? Server { get; }

        public static async Task<MqttServerInstance> StartServer(MQTTServerConfiguration serverConfiguration)
        {
            logger.Info($"Starting Mqtt Server");
            string storagefile = Path.Combine(PlugInData.HomeSeerDirectory, "data", PlugInData.PlugInId, "mqtt", "retained.json");

            // Configure MQTT server.
            var optionsBuilder = new MqttServerOptionsBuilder()
                .WithConnectionBacklog(512)
                .WithStorage(new MqttStorage(storagefile))
                .WithDefaultEndpointPort(serverConfiguration.Port);

            if (serverConfiguration.BoundIPAddress != null)
            {
                optionsBuilder = optionsBuilder.WithDefaultEndpointBoundIPAddress(serverConfiguration.BoundIPAddress);
            }

            var mqttServer = new MqttFactory().CreateMqttServer();
            await mqttServer.StartAsync(optionsBuilder.Build()).ConfigureAwait(false);
            return new MqttServerInstance(mqttServer, serverConfiguration);
        }

        public MqttServerDetails GetServerDetails()
        {
            int port = Configuration.Port;
            var host = Configuration.BoundIPAddress ?? GetLocalIPAddress();

            return new MqttServerDetails(host?.ToString() ?? throw new Exception("ip address not found"), port);
        }

        public async Task Stop()
        {
            if (Server != null)
            {
                await Server.StopAsync().ConfigureAwait(false);
                Server.Dispose();
            }
        }

        private static IPAddress? GetLocalIPAddress()
        {
            var host = default(IPHostEntry);
            string? hostname = null;
            hostname = System.Environment.MachineName;
            host = Dns.GetHostEntry(hostname);
            foreach (var IP in host.AddressList)
            {
                if (IP.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return IP;
                }
            }
            return null;
        }

        private readonly static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
    }
}