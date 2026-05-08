using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using GatewayServer.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;

namespace GatewayServer.Services;

public sealed class MqttHostedService : BackgroundService
{
    private readonly ILogger<MqttHostedService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HeartbeatProcessor _heartbeatProcessor;
    private readonly CommandStateStore _stateStore;
    private readonly FrontendCallbackService _callbackService;
    private readonly SqliteRepository _repo;

    private IMqttClient? _client;

    private readonly string _host;
    private readonly int _port;
    private readonly string? _username;
    private readonly string? _password;
    private readonly bool _useTls;
    private readonly string? _caCertPath;
    private readonly bool _allowUntrusted;

    public MqttHostedService(
        ILogger<MqttHostedService> logger,
        IConfiguration configuration,
        HeartbeatProcessor heartbeatProcessor,
        CommandStateStore stateStore,
        FrontendCallbackService callbackService,
        SqliteRepository repo)
    {
        _logger = logger;
        _configuration = configuration;
        _heartbeatProcessor = heartbeatProcessor;
        _stateStore = stateStore;
        _callbackService = callbackService;
        _repo = repo;

        _host = _configuration["Mqtt:Host"] ?? throw new InvalidOperationException("Missing Mqtt:Host");
        _port = int.TryParse(_configuration["Mqtt:Port"], out var p) ? p : 1883;
        _username = _configuration["Mqtt:Username"];
        _password = _configuration["Mqtt:Password"];
        _useTls = bool.TryParse(_configuration["Mqtt:UseTls"], out var tls) && tls;
        _caCertPath = _configuration["Mqtt:CaCertPath"];
        _allowUntrusted = bool.TryParse(_configuration["Mqtt:AllowUntrusted"], out var allow) && allow;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();

        _client.ApplicationMessageReceivedAsync += async args =>
        {
            var topic = args.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);

            _logger.LogInformation("MQTT RX Topic={Topic}", topic);
            _logger.LogInformation("MQTT PAYLOAD={Payload}", payload);

            if (topic.EndsWith("/heartbeat", StringComparison.OrdinalIgnoreCase))
            {
                await HandleHeartbeatAsync(topic, payload, CancellationToken.None);
                return;
            }

            if (topic.EndsWith("/result", StringComparison.OrdinalIgnoreCase))
            {
                await HandleResultAsync(payload, CancellationToken.None);
                return;
            }

            if (topic.EndsWith("/event", StringComparison.OrdinalIgnoreCase))
            {
                await HandleDeviceEventAsync(payload, CancellationToken.None);
                return;
            }
        };

        _client.DisconnectedAsync += async args =>
        {
            _logger.LogWarning("MQTT disconnected. Reason={Reason}", args.Reason);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(3000, stoppingToken);
                    await ConnectAndSubscribeAsync(stoppingToken);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Reconnect failed");
                }
            }
        };

        await ConnectAndSubscribeAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(10000, stoppingToken);
        }
    }

    private async Task ConnectAndSubscribeAsync(CancellationToken ct)
    {
        if (_client == null) return;
        if (_client.IsConnected) return;

        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(_host, _port)
            .WithCredentials(_username, _password);

        if (_useTls)
        {
            optionsBuilder = optionsBuilder.WithTlsOptions(tls =>
            {
                tls.UseTls();
                tls.WithSslProtocols(SslProtocols.Tls12 | SslProtocols.Tls13);

                tls.WithCertificateValidationHandler(context =>
                {
                    try
                    {
                        if (_allowUntrusted)
                            return true;

                        if (string.IsNullOrWhiteSpace(_caCertPath))
                        {
                            _logger.LogError("TLS enabled but CaCertPath is empty");
                            return false;
                        }

                        var fullPath = Path.IsPathRooted(_caCertPath)
                            ? _caCertPath
                            : Path.Combine(AppContext.BaseDirectory, _caCertPath);

                        if (!File.Exists(fullPath))
                        {
                            _logger.LogError("CA cert file not found: {Path}", fullPath);
                            return false;
                        }

                        if (context.Certificate == null)
                        {
                            _logger.LogError("Server certificate is null");
                            return false;
                        }

                        using var caCert = new X509Certificate2(fullPath);
                        using var serverCert = new X509Certificate2(context.Certificate);

                        using var chain = new X509Chain();
                        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
                        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                        chain.ChainPolicy.CustomTrustStore.Add(caCert);

                        var ok = chain.Build(serverCert);

                        if (!ok)
                        {
                            foreach (var status in chain.ChainStatus)
                            {
                                _logger.LogWarning("TLS chain status: {Status} - {Info}", status.Status, status.StatusInformation);
                            }
                        }

                        return ok;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "TLS certificate validation failed");
                        return false;
                    }
                });
            });
        }

        var options = optionsBuilder.Build();

        await _client.ConnectAsync(options, ct);
        _logger.LogInformation("MQTT connected to {Host}:{Port} TLS={UseTls}", _host, _port, _useTls);

        await _client.SubscribeAsync("gateway/+/heartbeat", MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce, ct);
        await _client.SubscribeAsync("gateway/+/+/result", MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce, ct);
        await _client.SubscribeAsync("gateway/+/+/event", MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce, ct);

        _logger.LogInformation("SUBSCRIBED >>> gateway/+/heartbeat");
        _logger.LogInformation("SUBSCRIBED >>> gateway/+/+/result");
        _logger.LogInformation("SUBSCRIBED >>> gateway/+/+/event");
    }

    public async Task<bool> PublishDownlinkAsync(string gatewayId, string deviceId, GatewayDownlinkCommand command, CancellationToken ct)
    {
        if (_client == null || !_client.IsConnected)
        {
            _logger.LogWarning("MQTT client is not connected");
            return false;
        }

        var topic = $"gateway/{gatewayId}/{deviceId}";
        var payload = JsonSerializer.Serialize(command);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _client.PublishAsync(message, ct);

        _logger.LogInformation("MQTT TX Topic={Topic}", topic);
        _logger.LogInformation("MQTT TX Payload={Payload}", payload);

        return true;
    }

    private async Task HandleHeartbeatAsync(string topic, string payload, CancellationToken ct)
    {
        HeartbeatMessage? hb;
        try
        {
            hb = JsonSerializer.Deserialize<HeartbeatMessage>(payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid heartbeat JSON");
            return;
        }

        if (hb == null || string.IsNullOrWhiteSpace(hb.GatewayId))
            return;

        if (string.Equals(hb.HeartbeatType, "bootstrap_request", StringComparison.OrdinalIgnoreCase))
        {
            await PublishBootstrapResponseAsync(hb.GatewayId, ct);
            return;
        }

        _heartbeatProcessor.Process(topic, payload);
    }

    private async Task PublishBootstrapResponseAsync(string gatewayId, CancellationToken ct)
    {
        if (_client == null || !_client.IsConnected)
        {
            _logger.LogWarning("Cannot publish bootstrap_response because MQTT is not connected");
            return;
        }

        var devices = _repo.GetBootstrapDevicesByGatewayId(gatewayId);

        var payloadObj = new
        {
            gateway_id = gatewayId,
            devices = devices.Select(d => new
            {
                device_id = d.DeviceId,
                mac_address = d.MacAddress,
                port = d.Port
            }).ToArray()
        };

        var payload = JsonSerializer.Serialize(payloadObj);
        var topic = $"gateway/{gatewayId}/bootstrap_response";

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _client.PublishAsync(message, ct);

        _logger.LogInformation("MQTT TX Topic={Topic}", topic);
        _logger.LogInformation("MQTT TX BootstrapResponse={Payload}", payload);
    }

    private async Task HandleResultAsync(string payload, CancellationToken ct)
    {
        EdgeResultMessage? result;
        try
        {
            result = JsonSerializer.Deserialize<EdgeResultMessage>(payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid RESULT JSON");
            return;
        }

        if (result == null || string.IsNullOrWhiteSpace(result.CommandId))
            return;

        if (!_stateStore.TryGet(result.CommandId, out var ctx) || ctx == null)
        {
            _logger.LogWarning("RESULT command_id not found in state store: {CommandId}", result.CommandId);
            return;
        }

        var callback = new FrontendResultCallback
        {
            CommandId = ctx.CommandId,
            DevEui = ctx.DevEui,
            DeviceProfileName = ctx.DeviceProfileName,
            DeviceProfileId = ctx.DeviceProfileId,
            Transport = ctx.Transport,
            Action = ctx.Action,
            Status = result.Success ? "success" : "error",
            Data = CompactHex(result.ResponseHex ?? result.Error ?? "")
        };

        await _callbackService.SendResultAsync(callback, ct);
        _stateStore.Remove(result.CommandId);
    }

    private async Task HandleDeviceEventAsync(string payload, CancellationToken ct)
    {
        EdgeDeviceEventMessage? msg;
        try
        {
            msg = JsonSerializer.Deserialize<EdgeDeviceEventMessage>(payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid EVENT JSON");
            return;
        }

        if (msg == null || string.IsNullOrWhiteSpace(msg.DeviceId))
            return;

        var info = _repo.GetDeviceProfileInfo(msg.DeviceId);
        if (info == null)
        {
            _logger.LogWarning("EVENT device not found in DB: {DeviceId}", msg.DeviceId);
            return;
        }

        var callback = new FrontendResultCallback
        {
            DevEui = info.DeviceId,
            DeviceProfileName = info.DeviceProfileName,
            DeviceProfileId = info.DeviceProfileId,
            Transport = "mqtt",
            Action = "do.change.status",
            Status = "success",
            Data = CompactHex(msg.DataHex ?? "")
        };

        await _callbackService.SendResultAsync(callback, ct);
    }

    private static string CompactHex(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        return string.Concat(input.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
    }
}