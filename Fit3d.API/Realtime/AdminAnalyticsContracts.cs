using FIt3d.DAL.Enums;

namespace Fit3d.API.Realtime;

public sealed class AdminAnalyticsSnapshot
{
    public required AdminDashboardSummary Summary { get; init; }
    public required IReadOnlyList<AdminBrandStat> Brands { get; init; }
    public required IReadOnlyList<AdminAnalyticsProductItem> Products { get; init; }
    public DateTime LastUpdatedUtc { get; init; }
}

public sealed class AdminDashboardSummary
{
    public int TotalUsers { get; init; }
    public int TotalOrders { get; init; }
    public int TotalProducts { get; init; }
    public int PendingOrders { get; init; }
    public int ProcessingOrders { get; init; }
    public int ShippedOrders { get; init; }
    public int DeliveredOrders { get; init; }
    public int CancelledOrders { get; init; }
    public decimal TotalRevenue { get; init; }
    public required IReadOnlyList<AdminRecentOrderItem> RecentOrders { get; init; }
}

public sealed class AdminRecentOrderItem
{
    public Guid Id { get; init; }
    public required string OrderNumber { get; init; }
    public decimal TotalAmount { get; init; }
    public required string ReceiverName { get; init; }
    public string? UserName { get; init; }
    public OrderStatus Status { get; init; }
}

public sealed class AdminBrandStat
{
    public required string Brand { get; init; }
    public int ProductCount { get; init; }
}

public sealed class AdminAnalyticsProductItem
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Brand { get; init; }
    public string? CategoryName { get; init; }
    public decimal Price { get; init; }
    public decimal? SalePrice { get; init; }
    public int StockQuantity { get; init; }
}
