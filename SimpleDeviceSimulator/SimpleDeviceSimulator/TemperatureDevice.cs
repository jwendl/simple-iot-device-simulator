using Newtonsoft.Json;

namespace SimpleDeviceSimulator
{
    public class TemperatureDevice
    {
        [JsonProperty("deviceType")]
        public string DeviceType { get; set; }

        [JsonProperty("temperature")]
        public int Temperature { get; set; }
    }
}
