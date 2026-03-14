using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Fit3d.BLL.Interfaces;
using FIt3d.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Configuration;
using OtpNet;
using OtpEntity = FIt3d.DAL.Entities.Otp;

namespace Fit3d.BLL.Services
{
    public class OtpService : IOtpService
    {
        private readonly IGenericRepository<OtpEntity> _otpRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly string _totpSecret;

        public OtpService(
            IGenericRepository<OtpEntity> otpRepository,
            IUnitOfWork unitOfWork,
            IConfiguration configuration)
        {
            _otpRepository = otpRepository;
            _unitOfWork = unitOfWork;

            _totpSecret =
                configuration["OtpSettings:SecretKey"]
                ?? configuration["JwtSettings:SecretKey"]
                ?? throw new InvalidOperationException("OTP secret key is not configured.");
        }

        public async Task<string> GenerateOtpAsync(string email)
        {
            // Generate TOTP code with 60-second time step
            var nowUtc = DateTime.UtcNow;
            var totp = CreateTotp(email);
            var code = totp.ComputeTotp(nowUtc);
            var remainingSeconds = totp.RemainingSeconds(nowUtc);
            if (remainingSeconds <= 0)
            {
                remainingSeconds = 60;
            }

            var expiration = nowUtc.AddSeconds(remainingSeconds);

            var existingOtps = (await _otpRepository.GetListAsync(
                x => x.Email == email && !x.IsDeleted))
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .ToList();

            if (existingOtps.Count == 0)
            {
                var newOtp = new OtpEntity
                {
                    Id = Guid.NewGuid(),
                    Email = email,
                    Code = code,
                    ExpirationTime = expiration,
                    IsUsed = false,
                    CreatedAt = nowUtc
                };

                await _otpRepository.InsertAsync(newOtp);
            }
            else
            {
                var otpToKeep = existingOtps[0];
                otpToKeep.Code = code;
                otpToKeep.ExpirationTime = expiration;
                otpToKeep.IsUsed = false;
                otpToKeep.UpdatedAt = nowUtc;
                _otpRepository.UpdateAsync(otpToKeep);

                foreach (var duplicateOtp in existingOtps.Skip(1))
                {
                    duplicateOtp.IsDeleted = true;
                    duplicateOtp.UpdatedAt = nowUtc;
                    _otpRepository.UpdateAsync(duplicateOtp);
                }
            }

            await _unitOfWork.SaveChangesAsync();

            return code;
        }

        public async Task<bool> VerifyOtpAsync(string email, string code)
        {
            var otp = (await _otpRepository.GetListAsync(
                x => x.Email == email && !x.IsDeleted))
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .FirstOrDefault();

            if (otp == null || otp.IsUsed || otp.ExpirationTime < DateTime.UtcNow)
            {
                return false; // Invalid or expired
            }

            var totp = CreateTotp(email);
            var isValid = totp.VerifyTotp(code, out _, new VerificationWindow(previous: 0, future: 0));

            if (!isValid || !string.Equals(otp.Code, code, StringComparison.Ordinal))
            {
                return false;
            }

            // Mark as used
            otp.IsUsed = true;
            otp.UpdatedAt = DateTime.UtcNow;

            _otpRepository.UpdateAsync(otp);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        private Totp CreateTotp(string email)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();
            var keyMaterial = Encoding.UTF8.GetBytes($"{_totpSecret}:{normalizedEmail}");
            var key = SHA256.HashData(keyMaterial);

            return new Totp(key, step: 60, totpSize: 6, mode: OtpHashMode.Sha256);
        }
    }
}
