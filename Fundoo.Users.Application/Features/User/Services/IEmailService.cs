using Fundoo.Users.Domain.Models;

namespace Fundoo.User.Application.Services
{
	public interface IEmailService
	{
        public Task<EmailResponseModel> SendEmailAsync(EmailRequestModel request);

    }
}

