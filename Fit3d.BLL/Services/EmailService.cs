using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Fit3d.BLL.Configuration;
using Fit3d.BLL.Interfaces;
using Microsoft.Extensions.Options;

namespace Fit3d.BLL.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly IHttpClientFactory _httpClientFactory;

        public EmailService(IOptions<EmailSettings> emailSettings, IHttpClientFactory httpClientFactory)
        {
            _emailSettings = emailSettings.Value;
            _httpClientFactory = httpClientFactory;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            if (!_emailSettings.UseHttpApi)
            {
                throw new InvalidOperationException("HTTP email provider is disabled.");
            }

            if (string.IsNullOrWhiteSpace(_emailSettings.ApiUrl) || string.IsNullOrWhiteSpace(_emailSettings.ApiKey))
            {
                throw new InvalidOperationException("Email HTTP API settings are missing.");
            }

            var client = _httpClientFactory.CreateClient(nameof(EmailService));

            using var request = new HttpRequestMessage(HttpMethod.Post, _emailSettings.ApiUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _emailSettings.ApiKey);

            var payload = new
            {
                from = string.IsNullOrWhiteSpace(_emailSettings.SenderName)
                    ? _emailSettings.SenderEmail
                    : $"{_emailSettings.SenderName} <{_emailSettings.SenderEmail}>",
                to = new[] { toEmail },
                subject,
                html = htmlBody
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_emailSettings.TimeoutSeconds));
            var response = await client.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cts.Token);
                throw new InvalidOperationException($"Email API error {(int)response.StatusCode}: {body}");
            }
        }
    }
}
