using System.Text.Json.Serialization;

namespace GatewayServer.Models;

public sealed class HeartbeatMessage
{
    [JsonPropertyName("gateway_id")]
    public string? GatewayId { get; set; }

    [JsonPropertyName("heartbeat_type")]
    public string? HeartbeatType { get; set; }

    [JsonPropertyName("device1")]
    public HeartbeatDevice? Device1 { get; set; }

    [JsonPropertyName("device2")]
    public HeartbeatDevice? Device2 { get; set; }
}

public sealed class HeartbeatDevice
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("status_device")]
    public bool? StatusDevice { get; set; }
}