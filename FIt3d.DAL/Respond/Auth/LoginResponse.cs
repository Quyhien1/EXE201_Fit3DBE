using FIt3d.DAL.Entities;

namespace FIt3d.DAL.Respond.Auth
{
    public class LoginResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? AccessTokenExpiration { get; set; }
        public DateTime? RefreshTokenExpiration { get; set; }
        public UserInfo? User { get; set; }
    }

    public class UserInfo
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public UserRole Role { get; set; }
        public bool IsVerified { get; set; }
    }

    public class RefreshTokenResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? AccessTokenExpiration { get; set; }
        public DateTime? RefreshTokenExpiration { get; set; }
    }
}
