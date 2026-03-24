namespace Fit3d.API.Realtime;

public interface IAdminAnalyticsSnapshotProvider
{
    Task<AdminAnalyticsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
