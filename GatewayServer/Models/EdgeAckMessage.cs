using System.Text.Json.Serialization;

namespace GatewayServer.Models;

public sealed class EdgeAckMessage
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("command_id")]
    public string? CommandId { get; set; }

    [JsonPropertyName("gateway_id")]
    public string? GatewayId { get; set; }

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }
}