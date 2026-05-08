using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GatewayServer.Services;

public sealed class GatewayTimeoutMonitor : BackgroundService
{
    private readonly ILogger<GatewayTimeoutMonitor> _logger;
    private readonly SqliteRepository _repo;

    private const int GatewayTimeoutSeconds = 90;
    private const int MonitorIntervalSeconds = 30;

    public GatewayTimeoutMonitor(ILogger<GatewayTimeoutMonitor> logger, SqliteRepository repo)
    {
        _logger = logger;
        _repo = repo;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Gateway timeout monitor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var expiredGatewayIds = _repo.GetExpiredGatewayIds(GatewayTimeoutSeconds);

                foreach (var gatewayId in expiredGatewayIds)
                {
                    _repo.MarkGatewayOffline(gatewayId);
                    _repo.SetDevicesAuthStatusByGateway(gatewayId, 0);

                    _logger.LogWarning(
                        "Gateway {GatewayId} expired by timeout. Marked offline and devices auth_status set to 0",
                        gatewayId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gateway timeout monitor failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(MonitorIntervalSeconds), stoppingToken);
        }
    }
}