using System;
using Fundoo.Notification.Domain.Models;

namespace Fundoo.Notification.Application.Feature.Notification.Services
{
	public interface IEmailService
	{
        public Task<EmailResponseModel> SendEmailAsync(EmailRequestModel request);

    }
}

