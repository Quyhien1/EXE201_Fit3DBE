using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Fit3d.API.Realtime;

namespace Fit3d.API.Hubs;

[Authorize(Roles = "Admin")]
public sealed class AdminAnalyticsHub : Hub
{
    public const string AnalyticsUpdatedEvent = "analyticsUpdated";

    private readonly IAdminAnalyticsSnapshotProvider _snapshotProvider;

    public AdminAnalyticsHub(IAdminAnalyticsSnapshotProvider snapshotProvider)
    {
        _snapshotProvider = snapshotProvider;
    }

    public override async Task OnConnectedAsync()
    {
        var snapshot = await _snapshotProvider.GetSnapshotAsync(Context.ConnectionAborted);
        await Clients.Caller.SendAsync(AnalyticsUpdatedEvent, snapshot, Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public async Task RequestAnalyticsSnapshot()
    {
        var snapshot = await _snapshotProvider.GetSnapshotAsync(Context.ConnectionAborted);
        await Clients.Caller.SendAsync(AnalyticsUpdatedEvent, snapshot, Context.ConnectionAborted);
    }
}
