using System.Text.Json.Serialization;

namespace GatewayServer.Models;

public sealed class FrontendKeepAliveCallback
{
    [JsonPropertyName("devEui")]
    public string? DevEui { get; set; }

    [JsonPropertyName("deviceProfileName")]
    public string? DeviceProfileName { get; set; }

    [JsonPropertyName("deviceProfileId")]
    public string? DeviceProfileId { get; set; }

    [JsonPropertyName("event_type")]
    public string? EventType { get; set; }

    [JsonPropertyName("status_device")]
    public bool StatusDevice { get; set; }
}