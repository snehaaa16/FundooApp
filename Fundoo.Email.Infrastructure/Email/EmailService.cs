using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Fundoo.Notification.Domain.Models;
using Fundoo.Notification.Infrastructure.Email.Template;
using Fundoo.Notification.Application.Feature.Notification.Services;

namespace Fundoo.Notification.Infrastructure.Email
{
	public class EmailService: IEmailService
    {
        private readonly SmtpSettings _smtpSettings;

        public EmailService(IOptions<SmtpSettings> _smtpSettings)
        {
            this._smtpSettings = _smtpSettings.Value;   
        }

        public async Task<EmailResponseModel> SendEmailAsync(EmailRequestModel request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_smtpSettings.Host) || string.IsNullOrWhiteSpace(_smtpSettings.Username))
                {
                    return new EmailResponseModel
                    {
                        Success = false,
                        Message = "SMTP settings are not configured properly (Host or Username is missing)."
                    };
                }

                using var client = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port);

                client.Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password);

                client.EnableSsl = _smtpSettings.EnableSsl;

                var mail = new MailMessage(
                    _smtpSettings.Username,
                    request.To,
                    request.Subject,
                    request.Body);

                mail.IsBodyHtml = request.IsHtml;

                await client.SendMailAsync(mail);

                return new EmailResponseModel
                {
                    Success = true,
                    Message = "Email sent successfully"
                };
            }
            catch (Exception ex)
            {
                return new EmailResponseModel
                {
                    Success = false,
                    Message = $"Failed to send email: {ex.Message}"
                };
            }
        }
    }
}

