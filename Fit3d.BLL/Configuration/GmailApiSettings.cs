namespace Fit3d.BLL.Configuration
{
    public class GmailApiSettings
    {
        public string ApplicationName { get; set; } = "Fit3d.API";
        public string UserId { get; set; } = "me";
        public string SenderName { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}
