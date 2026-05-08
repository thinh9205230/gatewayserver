using System.Text.Json.Serialization;

namespace GatewayServer.Models;

public sealed class GatewayDownlinkCommand
{
    [JsonPropertyName("command_id")]
    public string? CommandId { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("port")]
    public int Port { get; set; } = 502;

    [JsonPropertyName("payload")]
    public string? Payload { get; set; }
}