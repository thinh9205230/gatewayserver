using System.Text.Json.Serialization;

namespace GatewayServer.Models;

public sealed class FrontendAckCallback
{
    [JsonPropertyName("command_id")]
    public string? CommandId { get; set; }

    [JsonPropertyName("devEui")]
    public string? DevEui { get; set; }

    [JsonPropertyName("deviceProfileName")]
    public string? DeviceProfileName { get; set; }

    [JsonPropertyName("deviceProfileId")]
    public string? DeviceProfileId { get; set; }

    [JsonPropertyName("transport")]
    public string? Transport { get; set; }

    [JsonPropertyName("ack")]
    public string? Ack { get; set; }
}