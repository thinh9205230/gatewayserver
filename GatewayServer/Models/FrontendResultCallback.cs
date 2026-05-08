using System.Text.Json.Serialization;

namespace GatewayServer.Models;

public sealed class FrontendResultCallback
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

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("event_type")]
    public string? Event_type { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("data")]
    public string? Data { get; set; }
}