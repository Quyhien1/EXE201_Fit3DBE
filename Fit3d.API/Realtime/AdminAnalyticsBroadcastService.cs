using System.Text.Json;
using Fit3d.API.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Fit3d.API.Realtime;

public sealed class AdminAnalyticsBroadcastService : BackgroundService
{
    private static readonly TimeSpan BroadcastInterval = TimeSpan.FromSeconds(3);

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IHubContext<AdminAnalyticsHub> _hubContext;
    private readonly ILogger<AdminAnalyticsBroadcastService> _logger;
    private string? _lastSnapshotHash;

    public AdminAnalyticsBroadcastService(
        IServiceScopeFactory serviceScopeFactory,
        IHubContext<AdminAnalyticsHub> hubContext,
        ILogger<AdminAnalyticsBroadcastService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(BroadcastInterval);

        await BroadcastIfChangedAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await BroadcastIfChangedAsync(stoppingToken);
        }
    }

    private async Task BroadcastIfChangedAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var provider = scope.ServiceProvider.GetRequiredService<IAdminAnalyticsSnapshotProvider>();
            var snapshot = await provider.GetSnapshotAsync(cancellationToken);

            var serialized = JsonSerializer.Serialize(new
            {
                snapshot.Summary,
                snapshot.Brands,
                snapshot.Products
            });
            if (serialized == _lastSnapshotHash)
            {
                return;
            }

            _lastSnapshotHash = serialized;
            await _hubContext.Clients.All.SendAsync(AdminAnalyticsHub.AnalyticsUpdatedEvent, snapshot, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Graceful shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast admin analytics snapshot");
        }
    }
}
