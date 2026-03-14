namespace Fit3d.BLL.Configuration
{
    public class EmailSettings
    {
        public bool UseHttpApi { get; set; } = true;
        public string ApiUrl { get; set; } = "https://api.resend.com/emails";
        public string ApiKey { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 20;
    }
}
