using Newtonsoft.Json;

namespace SimpleDeviceSimulator
{
    public class BooleanDevice
    {
        [JsonProperty("deviceType")]
        public string DeviceType { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }
    }
}
