using Fit3d.BLL.Interfaces;
using FIt3d.DAL.Data;
using Microsoft.EntityFrameworkCore;

namespace Fit3d.API.Jobs
{
    public class OtpRefreshJob
    {
        private readonly Fit3dDbContext _context;
        private readonly IOtpService _otpService;
        private readonly ILogger<OtpRefreshJob> _logger;

        public OtpRefreshJob(
            Fit3dDbContext context,
            IOtpService otpService,
            ILogger<OtpRefreshJob> logger)
        {
            _context = context;
            _otpService = otpService;
            _logger = logger;
        }

        public async Task RefreshOtpsAsync()
        {
            var emails = await _context.Users
                .AsNoTracking()
                .Where(x => x.IsActive && !x.IsDeleted && !string.IsNullOrWhiteSpace(x.Email))
                .Select(x => x.Email)
                .Distinct()
                .ToListAsync();

            foreach (var email in emails)
            {
                try
                {
                    await _otpService.GenerateOtpAsync(email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to refresh OTP for account {Email}", email);
                }
            }
        }
    }
}
