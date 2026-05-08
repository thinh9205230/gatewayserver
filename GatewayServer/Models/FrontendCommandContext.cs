namespace GatewayServer.Models;

public sealed class FrontendCommandContext
{
    public string CommandId { get; set; } = "";
    public string DevEui { get; set; } = "";
    public string DeviceProfileName { get; set; } = "";
    public string DeviceProfileId { get; set; } = "";
    public string Transport { get; set; } = "";
    public string Action { get; set; } = "";
}