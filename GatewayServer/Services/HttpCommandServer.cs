using System.Net;
using System.Text;
using System.Text.Json;
using GatewayServer.Models;
using GatewayServer.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GatewayServer.Services;

public sealed class HttpCommandServer : BackgroundService
{
    private readonly ILogger<HttpCommandServer> _logger;
    private readonly SqliteRepository _repo;
    private readonly MqttHostedService _mqttService;
    private readonly CommandStateStore _stateStore;

    public HttpCommandServer(
        ILogger<HttpCommandServer> logger,
        SqliteRepository repo,
        MqttHostedService mqttService,
        CommandStateStore stateStore)
    {
        _logger = logger;
        _repo = repo;
        _mqttService = mqttService;
        _stateStore = stateStore;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add("http://+:6050/");
        listener.Start();

        _logger.LogInformation("HTTP command server listening at http://+:6050/");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context, stoppingToken), stoppingToken);
            }
        }
        finally
        {
            listener.Stop();
            listener.Close();
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken ct)
    {
        try
        {
            if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(context, 405, new { message = "Only POST allowed" });
                return;
            }

            string body;
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
            {
                body = await reader.ReadToEndAsync();
            }

            _logger.LogInformation("HTTP RX Body={Body}", body);

            HttpCommandRequest? req;
            try
            {
                req = JsonSerializer.Deserialize<HttpCommandRequest>(body);
            }
            catch
            {
                await WriteJsonAsync(context, 400, new { message = "Invalid JSON" });
                return;
            }

            if (req == null ||
                string.IsNullOrWhiteSpace(req.CommandId) ||
                string.IsNullOrWhiteSpace(req.DevEui) ||
                req.Connection == null ||
                string.IsNullOrWhiteSpace(req.Connection.Ip) ||
                string.IsNullOrWhiteSpace(req.Data))
            {
                await WriteJsonAsync(context, 400, new { message = "Missing required fields" });
                return;
            }

            // Lưu metadata profile để dùng lại cho keep_alive / unsolicited event
            _repo.UpsertDeviceProfileInfo(
                req.DevEui,
                req.DeviceProfileName,
                req.DeviceProfileId
            );

            var route = _repo.GetAuthorizedRouteByDeviceId(req.DevEui);
            if (route == null)
            {
                await WriteJsonAsync(context, 403, new
                {
                    command_id = req.CommandId,
                    status = "denied",
                    message = "Device is not authorized or not bound to a valid gateway"
                });
                return;
            }

            var spacedPayload = PayloadHelper.NormalizeHexToSpaced(req.Data);
            if (string.IsNullOrWhiteSpace(spacedPayload))
            {
                await WriteJsonAsync(context, 400, new
                {
                    message = "Invalid data hex",
                    devEui = req.DevEui
                });
                return;
            }

            int port = 502;
            if (!string.IsNullOrWhiteSpace(req.Connection.Port) &&
                int.TryParse(req.Connection.Port, out var parsedPort) &&
                parsedPort > 0)
            {
                port = parsedPort;
            }

            var ctxModel = new FrontendCommandContext
            {
                CommandId = req.CommandId,
                DevEui = req.DevEui,
                DeviceProfileName = req.DeviceProfileName ?? "",
                DeviceProfileId = req.DeviceProfileId ?? "",
                Transport = req.Transport ?? "mqtt",
                Action = req.Action ?? ""
            };
            _stateStore.Save(ctxModel);

            var mqttCmd = new GatewayDownlinkCommand
            {
                CommandId = req.CommandId,
                Address = req.Connection.Ip,
                Port = port,
                Payload = spacedPayload
            };

            var ok = await _mqttService.PublishDownlinkAsync(route.GatewayId, req.DevEui, mqttCmd, ct);
            if (!ok)
            {
                await WriteJsonAsync(context, 503, new { message = "MQTT publish failed" });
                return;
            }

            await WriteJsonAsync(context, 200, new
            {
                command_id = req.CommandId,
                status = "accepted"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP command processing failed");
            await WriteJsonAsync(context, 500, new { message = "Internal server error" });
        }
    }

    private static async Task WriteJsonAsync(HttpListenerContext context, int statusCode, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        context.Response.ContentEncoding = Encoding.UTF8;
        context.Response.ContentLength64 = bytes.Length;

        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.OutputStream.Close();
    }
}