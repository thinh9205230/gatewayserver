using System.Net.Http.Json;
using GatewayServer.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GatewayServer.Services;

public sealed class FrontendCallbackService
{
    private readonly ILogger<FrontendCallbackService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _callbackUrl;
    private readonly string? _apiKey;

    public FrontendCallbackService(
        ILogger<FrontendCallbackService> logger,
        IConfiguration configuration)
    {
        _logger = logger;

        _callbackUrl = configuration["Frontend:CallbackUrl"]
            ?? "http://192.168.1.169:8990/webhooks/edge-result";

        _apiKey = configuration["Frontend:ApiKey"];

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
        }

        _logger.LogInformation("Frontend callback URL: {Url}", _callbackUrl);
    }

    public async Task SendResultAsync(FrontendResultCallback payload, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("HTTP CALLBACK RESULT -> {Url}", _callbackUrl);
            _logger.LogInformation("RESULT payload: {@Payload}", payload);

            var response = await _httpClient.PostAsJsonAsync(_callbackUrl, payload, ct);
            _logger.LogInformation("RESULT callback status: {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send RESULT callback");
        }
    }

    public async Task SendKeepAliveAsync(FrontendKeepAliveCallback payload, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("HTTP CALLBACK KEEP_ALIVE -> {Url}", _callbackUrl);
            _logger.LogInformation(
                "KEEP_ALIVE payload -> devEui={DevEui}, deviceProfileName={DeviceProfileName}, deviceProfileId={DeviceProfileId}, event_type={EventType}, status_device={StatusDevice}",
                payload.DevEui,
                payload.DeviceProfileName,
                payload.DeviceProfileId,
                payload.EventType,
                payload.StatusDevice
            );
            _logger.LogInformation(
                "KEEP_ALIVE raw json -> {Json}",
                System.Text.Json.JsonSerializer.Serialize(payload)
            );

            var response = await _httpClient.PostAsJsonAsync(_callbackUrl, payload, ct);
            _logger.LogInformation("KEEP_ALIVE callback status: {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send KEEP_ALIVE callback");
        }
    }
}