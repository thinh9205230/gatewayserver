using System.Text.Json;
using GatewayServer.Models;
using GatewayServer.Utils;
using Microsoft.Extensions.Logging;

namespace GatewayServer.Services;

public sealed class HeartbeatProcessor
{
    private readonly ILogger<HeartbeatProcessor> _logger;
    private readonly SqliteRepository _repo;
    private readonly FrontendCallbackService _callbackService;

    public HeartbeatProcessor(
        ILogger<HeartbeatProcessor> logger,
        SqliteRepository repo,
        FrontendCallbackService callbackService)
    {
        _logger = logger;
        _repo = repo;
        _callbackService = callbackService;
    }

    public void Process(string topic, string payload)
    {
        var gatewayIdFromTopic = ExtractGatewayIdFromTopic(topic);
        if (string.IsNullOrWhiteSpace(gatewayIdFromTopic))
        {
            _logger.LogWarning("Cannot extract gateway_id from topic {Topic}", topic);
            return;
        }

        if (!_repo.GatewayExists(gatewayIdFromTopic))
        {
            _logger.LogWarning("Ignored heartbeat from unknown gateway_id {GatewayId}", gatewayIdFromTopic);
            return;
        }

        HeartbeatMessage? hb;

        try
        {
            hb = JsonSerializer.Deserialize<HeartbeatMessage>(payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid heartbeat JSON. Topic={Topic}", topic);
            return;
        }

        if (hb == null)
        {
            _logger.LogWarning("Heartbeat payload is null. Topic={Topic}", topic);
            return;
        }

        if (string.IsNullOrWhiteSpace(hb.GatewayId))
        {
            _logger.LogWarning("Heartbeat missing gateway_id in payload. Topic={Topic}", topic);
            return;
        }

        if (!string.Equals(hb.GatewayId, gatewayIdFromTopic, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Gateway ID mismatch. Topic={GatewayFromTopic}, Payload={GatewayFromPayload}",
                gatewayIdFromTopic, hb.GatewayId);
            return;
        }

        _repo.MarkGatewayHeartbeat(hb.GatewayId);
        _logger.LogInformation("Processing heartbeat from gateway: {GatewayId}", hb.GatewayId);

        if (string.Equals(hb.HeartbeatType, "keep_alive", StringComparison.OrdinalIgnoreCase))
        {
            ProcessKeepAliveDevice(hb.Device1);
            ProcessKeepAliveDevice(hb.Device2);
        }

        ProcessDevice(hb.GatewayId, hb.Device1);
        ProcessDevice(hb.GatewayId, hb.Device2);
    }

    private void ProcessKeepAliveDevice(HeartbeatDevice? device)
    {
        if (device == null) return;
        if (string.IsNullOrWhiteSpace(device.Id)) return;
        if (!device.StatusDevice.HasValue) return;

        // BỎ QUA nếu status_device = false
        if (!device.StatusDevice.Value)
        {
            _logger.LogDebug("Skip keep_alive false for device {DeviceId}", device.Id);
            return;
        }

        var info = _repo.GetDeviceProfileInfo(device.Id);
        if (info == null) return;

        var payload = new FrontendKeepAliveCallback
        {
            DevEui = info.DeviceId,
            DeviceProfileName = info.DeviceProfileName,
            DeviceProfileId = info.DeviceProfileId,
            EventType = "keep_alive",
            StatusDevice = true
        };

        _callbackService.SendKeepAliveAsync(payload).GetAwaiter().GetResult();
    }

    private void ProcessDevice(string gatewayId, HeartbeatDevice? device)
    {
        if (device == null) return;
        if (string.IsNullOrWhiteSpace(device.Id)) return;
        if (string.IsNullOrWhiteSpace(device.Key)) return;

        _logger.LogInformation("Checking device {DeviceId}", device.Id);

        var state = _repo.GetDeviceState(device.Id);
        if (state == null)
        {
            _logger.LogInformation("Device {DeviceId} not found in table devices", device.Id);
            return;
        }

        if (string.IsNullOrWhiteSpace(state.OtpCode))
        {
            _logger.LogWarning("Device {DeviceId} has empty otpCode in DB", device.Id);
            _repo.UpdateAuthStatus(device.Id, 0);
            return;
        }

        var expectedHash = HashHelper.Sha256Hex(state.OtpCode);

        if (!string.Equals(expectedHash, device.Key, StringComparison.OrdinalIgnoreCase))
        {
            _repo.UpdateAuthStatus(device.Id, 0);
            _logger.LogWarning(
                "Key mismatch for device {DeviceId} from gateway {GatewayId}. auth_status set to 0",
                device.Id, gatewayId);
            return;
        }

        if (string.IsNullOrWhiteSpace(state.GatewayId) || state.AuthStatus == 0)
        {
            _repo.UpdateGatewayAndAuthStatus(device.Id, gatewayId, 1);
            _logger.LogInformation(
                "Device {DeviceId} authenticated and bound to gateway {GatewayId}",
                device.Id, gatewayId);
        }
        else
        {
            _repo.UpdateAuthStatus(device.Id, 1);
            _logger.LogInformation(
                "Device {DeviceId} authenticated with existing gateway {ExistingGateway}. Incoming gateway {IncomingGateway} ignored because auth_status is already 1",
                device.Id, state.GatewayId, gatewayId);
        }
    }

    private static string? ExtractGatewayIdFromTopic(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return null;

        var parts = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
            return null;

        if (!string.Equals(parts[0], "gateway", StringComparison.OrdinalIgnoreCase))
            return null;

        if (!string.Equals(parts[2], "heartbeat", StringComparison.OrdinalIgnoreCase))
            return null;

        return parts[1];
    }
}