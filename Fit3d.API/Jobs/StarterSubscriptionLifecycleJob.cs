using System.Globalization;
using Fit3d.BLL.Interfaces;
using FIt3d.DAL.Data;
using FIt3d.DAL.Entities;
using FIt3d.DAL.Enums;
using FIt3d.DAL.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Fit3d.API.Jobs
{
    public class StarterSubscriptionLifecycleJob
    {
        private readonly Fit3dDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<StarterSubscriptionLifecycleJob> _logger;

        public StarterSubscriptionLifecycleJob(
            Fit3dDbContext context,
            IEmailService emailService,
            ILogger<StarterSubscriptionLifecycleJob> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task ProcessStarterSubscriptionLifecycleAsync()
        {
            var nowUtc = DateTime.UtcNow;
            var starterPlanIds = await GetStarterPlanIdsAsync();

            if (starterPlanIds.Count == 0)
            {
                _logger.LogWarning("No active Starter plan found. Skipping starter lifecycle job.");
                return;
            }

            await SendExpiryWarningsAsync(starterPlanIds, nowUtc);
            await ExpireStarterSubscriptionsAsync(starterPlanIds, nowUtc);
            await DowngradeShopsWithoutActiveStarterAsync(starterPlanIds, nowUtc);
        }

        private async Task SendExpiryWarningsAsync(IReadOnlyCollection<Guid> starterPlanIds, DateTime nowUtc)
        {
            var warningTargetDate = nowUtc.Date.AddDays(3);
            var warningWindowStart = warningTargetDate;
            var warningWindowEnd = warningTargetDate.AddDays(1);

            var subscriptionsToWarn = await QueryStarterSubscriptions(starterPlanIds)
                .Include(s => s.User)
                .Where(s =>
                    s.Status == SubscriptionStatus.Active &&
                    s.EndDate >= warningWindowStart &&
                    s.EndDate < warningWindowEnd &&
                    s.ExpiryWarningSentAt == null &&
                    s.User != null &&
                    !s.User.IsDeleted &&
                    s.User.IsActive &&
                    s.User.Email != null &&
                    s.User.Email != string.Empty)
                .ToListAsync();

            if (subscriptionsToWarn.Count == 0)
            {
                return;
            }

            var hasChanges = false;
            foreach (var subscription in subscriptionsToWarn)
            {
                try
                {
                    var fullName = string.IsNullOrWhiteSpace(subscription.User.FullName)
                        ? "Shop owner"
                        : subscription.User.FullName;

                    await _emailService.SendEmailAsync(
                        subscription.User.Email,
                        "Starter subscription expiring in 3 days",
                        BuildExpiryWarningEmailBody(fullName, subscription.EndDate));

                    subscription.ExpiryWarningSentAt = nowUtc;
                    subscription.UpdatedAt = nowUtc;
                    hasChanges = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send Starter expiry warning for SubscriptionId: {SubscriptionId}", subscription.Id);
                }
            }

            if (hasChanges)
            {
                await _context.SaveChangesAsync();
            }
        }

        private async Task ExpireStarterSubscriptionsAsync(IReadOnlyCollection<Guid> starterPlanIds, DateTime nowUtc)
        {
            var expiredSubscriptions = await QueryStarterSubscriptions(starterPlanIds)
                .Where(s =>
                    s.Status == SubscriptionStatus.Active &&
                    s.EndDate <= nowUtc)
                .ToListAsync();

            if (expiredSubscriptions.Count == 0)
            {
                return;
            }

            foreach (var subscription in expiredSubscriptions)
            {
                subscription.Status = SubscriptionStatus.Expired;
                subscription.UpdatedAt = nowUtc;
            }

            await _context.SaveChangesAsync();
        }

        private async Task DowngradeShopsWithoutActiveStarterAsync(IReadOnlyCollection<Guid> starterPlanIds, DateTime nowUtc)
        {
            var usersWithActiveStarter = await GetUsersWithActiveStarterAsync(starterPlanIds, nowUtc);

            var shopUsersToDowngrade = await _context.Users
                .Where(u =>
                    !u.IsDeleted &&
                    u.Role == UserRole.Shop &&
                    !usersWithActiveStarter.Contains(u.Id))
                .ToListAsync();

            if (shopUsersToDowngrade.Count == 0)
            {
                return;
            }

            foreach (var user in shopUsersToDowngrade)
            {
                user.Role = UserRole.User;
                user.UpdatedAt = nowUtc;
            }

            await _context.SaveChangesAsync();
        }

        private async Task<List<Guid>> GetStarterPlanIdsAsync()
        {
            var plans = await _context.SubscriptionPlans
                .AsNoTracking()
                .Where(p =>
                    !p.IsDeleted &&
                    p.IsActive &&
                    p.PlanType == PlanType.B2B_Shop)
                .Select(p => new { p.Id, p.PlanType, p.Name })
                .ToListAsync();

            return plans
                .Where(p => SubscriptionPlanRule.IsStarterShopPlan(p.PlanType, p.Name))
                .Select(p => p.Id)
                .ToList();
        }

        private IQueryable<Subscription> QueryStarterSubscriptions(IReadOnlyCollection<Guid> starterPlanIds)
        {
            return _context.Subscriptions
                .Where(s => !s.IsDeleted && starterPlanIds.Contains(s.SubscriptionPlanId));
        }

        private Task<List<Guid>> GetUsersWithActiveStarterAsync(IReadOnlyCollection<Guid> starterPlanIds, DateTime nowUtc)
        {
            return QueryStarterSubscriptions(starterPlanIds)
                .AsNoTracking()
                .Where(s => s.Status == SubscriptionStatus.Active && s.EndDate > nowUtc)
                .Select(s => s.UserId)
                .Distinct()
                .ToListAsync();
        }

        private static string BuildExpiryWarningEmailBody(string fullName, DateTime endDateUtc)
        {
            var endDateText = endDateUtc.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

            return $@"
                <p>Hello {System.Net.WebUtility.HtmlEncode(fullName)},</p>
                <p>Your Starter subscription will expire on <strong>{endDateText} (UTC)</strong>.</p>
                <p>Please renew your Starter plan before expiry to keep your Shop role active.</p>
                <p>Best regards,<br/>Fit3D Team</p>";
        }
    }
}
