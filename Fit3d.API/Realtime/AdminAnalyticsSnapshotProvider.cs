using FIt3d.DAL.Data;
using FIt3d.DAL.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fit3d.API.Realtime;

public sealed class AdminAnalyticsSnapshotProvider : IAdminAnalyticsSnapshotProvider
{
    private readonly Fit3dDbContext _dbContext;

    public AdminAnalyticsSnapshotProvider(Fit3dDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminAnalyticsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var totalUsersTask = _dbContext.Users.AsNoTracking().CountAsync(cancellationToken);
        var totalOrdersTask = _dbContext.Orders.AsNoTracking().CountAsync(cancellationToken);
        var totalProductsTask = _dbContext.Products.AsNoTracking().CountAsync(cancellationToken);

        var orderStatusCountsTask = _dbContext.Orders
            .AsNoTracking()
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var totalRevenueTask = _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.PaymentStatus == PaymentStatus.Paid)
            .SumAsync(o => (decimal?)o.TotalAmount, cancellationToken);

        var recentOrdersTask = _dbContext.Orders
            .AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .Take(8)
            .Select(o => new AdminRecentOrderItem
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                TotalAmount = o.TotalAmount,
                ReceiverName = o.ReceiverName,
                UserName = o.User != null ? o.User.FullName : null,
                Status = o.Status
            })
            .ToListAsync(cancellationToken);

        var brandsTask = _dbContext.Products
            .AsNoTracking()
            .GroupBy(p => string.IsNullOrWhiteSpace(p.Brand) ? "Unknown" : p.Brand!.Trim())
            .Select(g => new AdminBrandStat
            {
                Brand = g.Key,
                ProductCount = g.Count()
            })
            .OrderByDescending(x => x.ProductCount)
            .ToListAsync(cancellationToken);

        var recentProductsTask = _dbContext.Products
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Take(8)
            .Select(p => new AdminAnalyticsProductItem
            {
                Id = p.Id,
                Name = p.Name,
                Brand = p.Brand,
                CategoryName = p.Category != null ? p.Category.Name : null,
                Price = p.Price,
                SalePrice = p.SalePrice,
                StockQuantity = p.StockQuantity
            })
            .ToListAsync(cancellationToken);

        await Task.WhenAll(
            totalUsersTask,
            totalOrdersTask,
            totalProductsTask,
            orderStatusCountsTask,
            totalRevenueTask,
            recentOrdersTask,
            brandsTask,
            recentProductsTask);

        var orderStatusCounts = orderStatusCountsTask.Result.ToDictionary(x => x.Status, x => x.Count);

        return new AdminAnalyticsSnapshot
        {
            Summary = new AdminDashboardSummary
            {
                TotalUsers = totalUsersTask.Result,
                TotalOrders = totalOrdersTask.Result,
                TotalProducts = totalProductsTask.Result,
                PendingOrders = GetStatusCount(orderStatusCounts, OrderStatus.Pending),
                ProcessingOrders = GetStatusCount(orderStatusCounts, OrderStatus.Processing),
                ShippedOrders = GetStatusCount(orderStatusCounts, OrderStatus.Shipped),
                DeliveredOrders = GetStatusCount(orderStatusCounts, OrderStatus.Delivered),
                CancelledOrders = GetStatusCount(orderStatusCounts, OrderStatus.Cancelled),
                TotalRevenue = totalRevenueTask.Result ?? 0m,
                RecentOrders = recentOrdersTask.Result
            },
            Brands = brandsTask.Result,
            Products = recentProductsTask.Result,
            LastUpdatedUtc = DateTime.UtcNow
        };
    }

    private static int GetStatusCount(IReadOnlyDictionary<OrderStatus, int> counts, OrderStatus status)
    {
        return counts.TryGetValue(status, out var count) ? count : 0;
    }
}
