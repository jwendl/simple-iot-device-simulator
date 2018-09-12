using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClientIotHubConnectionStringBuilder = Microsoft.Azure.Devices.Client.IotHubConnectionStringBuilder;
using ClientMessage = Microsoft.Azure.Devices.Client.Message;
using IotHubConnectionStringBuilder = Microsoft.Azure.Devices.IotHubConnectionStringBuilder;

namespace SimpleDeviceSimulator
{
    public static class Program
    {
        static async Task Main(string[] args)
        {
            var interval = 1;
            var connectionString = "";
            var registryManager = RegistryManager.CreateFromConnectionString(connectionString);
            var numberOfDevices = Enumerable.Range(1, 50);

            var devices = new ConcurrentBag<DeviceItem>();
            await ForEachAsync(numberOfDevices, 10, async (deviceNumber) =>
            {
                var deviceName = $"SimulatedFridge-{deviceNumber:0000}";

                Console.WriteLine($"Registering device ({deviceName})");
                var device = await registryManager.GetDeviceAsync(deviceName);
                if (device == null)
                {
                    device = await registryManager.AddDeviceAsync(new Device(deviceName));
                }

                devices.Add(new DeviceItem()
                {
                    Id = deviceName,
                    MessageTemplate = @"
    ""deviceType"": ""Fridge"",
    ""temperature"": ""{number}"",
",
                });

                var twin = new Twin()
                {
                    Tags = { ["IsSimulated"] = "Y" }
                };

                await registryManager.UpdateTwinAsync(device.Id, twin, "*");
            });

            await ForEachAsync(numberOfDevices, 10, async (deviceNumber) =>
            {
                var deviceName = $"SimulatedLightBulbs-{deviceNumber:0000}";

                Console.WriteLine($"Registering device ({deviceName})");
                var device = await registryManager.GetDeviceAsync(deviceName);
                if (device == null)
                {
                    device = await registryManager.AddDeviceAsync(new Device(deviceName));
                }

                devices.Add(new DeviceItem()
                {
                    Id = deviceName,
                    MessageTemplate = @"
    ""deviceType"": ""LightBulb"",
    ""state"": ""{boolean}"",
",
                });

                var twin = new Twin()
                {
                    Tags = { ["IsSimulated"] = "Y" }
                };

                await registryManager.UpdateTwinAsync(device.Id, twin, "*");
            });

            await ForEachAsync(devices, 100, async (deviceItem) =>
            {
                var device = await registryManager.GetDeviceAsync(deviceItem.Id);
                var iotHubConnectionString = IotHubConnectionStringBuilder.Create(connectionString);
                var deviceKeyInfo = new DeviceAuthenticationWithRegistrySymmetricKey(deviceItem.Id, device.Authentication.SymmetricKey.PrimaryKey);
                var deviceConnectionStringBuilder = ClientIotHubConnectionStringBuilder.Create(iotHubConnectionString.HostName, deviceKeyInfo);
                var deviceConnectionString = deviceConnectionStringBuilder.ToString();

                var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString);
                await deviceClient.OpenAsync();

                var messageBody = deviceItem.MessageTemplate
                    .Replace("{number}", new Random().Next(60, 80).ToString())
                    .Replace("{boolean}", new Random().Next(1, 2) % 2 == 0 ? "on" : "off");

                var eventJson = JsonConvert.SerializeObject(messageBody);
                var eventJsonBytes = Encoding.UTF8.GetBytes(eventJson);
                var message = new ClientMessage(eventJsonBytes);

                var messageProperties = message.Properties;
                messageProperties.Add("messageType", "Telemetry");
                messageProperties.Add("correlationId", Guid.NewGuid().ToString());
                messageProperties.Add("parentCorrelationId", Guid.NewGuid().ToString());
                messageProperties.Add("createdDateTime", DateTime.UtcNow.ToString("u", DateTimeFormatInfo.InvariantInfo));
                messageProperties.Add("deviceId", deviceItem.Id);

                var properties = new Dictionary<string, string>();
                if (properties != null)
                {
                    foreach (var property in properties)
                    {
                        messageProperties.Add(property.Key, property.Value);
                    }
                }

                Console.WriteLine($"Sending data ({deviceItem.Id}): {eventJson}");
                await deviceClient.SendEventAsync(message);
                await Task.Delay(interval * 1000);
            });

            await Task.Delay(1000);
            Console.ReadKey();
        }

        public static Task ForEachAsync<T>(this IEnumerable<T> source, int dop, Func<T, Task> body)
        {
            return Task.WhenAll(
                from partition in Partitioner.Create(source).GetPartitions(dop)
                select Task.Run(async delegate
                {
                    using (partition)
                        while (partition.MoveNext())
                            await body(partition.Current);
                }));
        }

        public class DeviceItem
        {
            public string Id { get; set; }

            public string MessageTemplate { get; set; }
        }
    }
}
