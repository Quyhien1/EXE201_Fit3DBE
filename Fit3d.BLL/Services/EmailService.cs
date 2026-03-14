using Fit3d.BLL.Configuration;
using Fit3d.BLL.Interfaces;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Threading;

namespace Fit3d.BLL.Services
{
    public class EmailService : IEmailService
    {
        private readonly GmailApiSettings _gmailSettings;

        public EmailService(IOptions<GmailApiSettings> gmailSettings)
        {
            _gmailSettings = gmailSettings.Value;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_gmailSettings.SenderName, _gmailSettings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;

            message.Body = new BodyBuilder
            {
                HtmlBody = htmlBody
            }.ToMessageBody();

            var credential = await CreateCredentialAsync();

            using var gmailService = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = _gmailSettings.ApplicationName
            });

            using var stream = new MemoryStream();
            await message.WriteToAsync(stream);

            var rawMessage = Convert.ToBase64String(stream.ToArray())
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');

            var gmailMessage = new Message
            {
                Raw = rawMessage
            };

            await gmailService.Users.Messages.Send(gmailMessage, _gmailSettings.UserId).ExecuteAsync();
        }

        private async Task<UserCredential> CreateCredentialAsync()
        {
            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = _gmailSettings.ClientId,
                    ClientSecret = _gmailSettings.ClientSecret
                },
                Scopes = new[] { GmailService.Scope.GmailSend },
                DataStore = new NullDataStore()
            });

            var token = new Google.Apis.Auth.OAuth2.Responses.TokenResponse
            {
                RefreshToken = _gmailSettings.RefreshToken
            };

            var credential = new UserCredential(flow, _gmailSettings.SenderEmail, token);
            await credential.RefreshTokenAsync(CancellationToken.None);

            return credential;
        }
    }
}
