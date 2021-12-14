using MQTTnet;
using MQTTnet.Server;
using Newtonsoft.Json;
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

        public Task<IList<MqttApplicationMessage>> LoadRetainedMessagesAsync()
        {
            IList<MqttApplicationMessage>? retainedMessages = null;
            if (File.Exists(Filename))
            {
                var json = File.ReadAllText(Filename);
                retainedMessages = JsonConvert.DeserializeObject<List<MqttApplicationMessage>>(json);
            } 

            return Task.FromResult(retainedMessages ?? new List<MqttApplicationMessage>());
        }

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
    };
}