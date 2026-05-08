using System.Text.Json.Serialization;

namespace GatewayServer.Models;

public sealed class EdgeResultMessage
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("command_id")]
    public string? CommandId { get; set; }

    [JsonPropertyName("gateway_id")]
    public string? GatewayId { get; set; }

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("request_mode")]
    public string? RequestMode { get; set; }

    [JsonPropertyName("request_hex")]
    public string? RequestHex { get; set; }

    [JsonPropertyName("response_hex")]
    public string? ResponseHex { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}