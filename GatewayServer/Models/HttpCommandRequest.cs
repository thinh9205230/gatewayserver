using System.Text.Json.Serialization;

namespace GatewayServer.Models;

public sealed class HttpCommandRequest
{
    [JsonPropertyName("command_id")]
    public string? CommandId { get; set; }

    [JsonPropertyName("devEui")]
    public string? DevEui { get; set; }

    [JsonPropertyName("device_profile_name")]
    public string? DeviceProfileName { get; set; }

    [JsonPropertyName("device_profile_id")]
    public string? DeviceProfileId { get; set; }

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("transport")]
    public string? Transport { get; set; }

    [JsonPropertyName("control_mode")]
    public string? ControlMode { get; set; }

    [JsonPropertyName("connection")]
    public HttpCommandConnection? Connection { get; set; }

    [JsonPropertyName("data")]
    public string? Data { get; set; }
}

public sealed class HttpCommandConnection
{
    [JsonPropertyName("ip")]
    public string? Ip { get; set; }

    [JsonPropertyName("port")]
    public string? Port { get; set; }

    [JsonPropertyName("unit_id")]
    public string? UnitId { get; set; }
}