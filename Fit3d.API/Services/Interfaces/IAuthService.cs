using FIt3d.DAL.Entities;
using FIt3d.DAL.Request.Auth;
using FIt3d.DAL.Respond.Auth;

namespace Fit3d.API.Services.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request, string? ipAddress = null);
        Task<LoginResponse> RegisterAsync(RegisterRequest request, string? ipAddress = null);
        Task<RefreshTokenResponse> RefreshTokenAsync(string token, string? ipAddress = null);
        Task<bool> RevokeTokenAsync(string token, string? ipAddress = null);
        Task<User?> GetUserByIdAsync(Guid userId);
    }
}
