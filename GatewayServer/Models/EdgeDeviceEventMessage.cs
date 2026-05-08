using System.Text.Json.Serialization;

namespace GatewayServer.Models;

public sealed class EdgeDeviceEventMessage
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("gateway_id")]
    public string? GatewayId { get; set; }

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("data_hex")]
    public string? DataHex { get; set; }
}