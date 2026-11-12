using Fundoo.User.Application.Command;
using Fundoo.User.Application.Services;
using Fundoo.User.Domain.Models;
using Fundoo.Users.Domain.Models;
using MediatR;
using ModelLayer.Models;

namespace Fundoo.User.Application.Handler
{
    public class ForgotPasswordHandler: IRequestHandler<ForgotPasswordCommand, ForgotPasswordResponseModel>
    {
        private readonly IUserRL _userRL;
        private readonly IEmailService _emailService;
        private readonly IJwtService _jwtService;

        public ForgotPasswordHandler(IUserRL userRL,IEmailService emailService,IJwtService jwtService)
        {
            _userRL = userRL;
            _emailService = emailService;
            _jwtService = jwtService;
        }

        public async Task<ForgotPasswordResponseModel> Handle(ForgotPasswordCommand command,CancellationToken cancellationToken)
        {
            var request = command.Request;

            var result = await _userRL.ForgotPasswordAsyncRL(request);

            if (!result.Success)
                return result;

            var cleanEmail = request.Email.Trim().ToLower();
            var token = _jwtService.GeneratePasswordResetToken(cleanEmail);

            var emailRequest = new EmailRequestModel
            {
                To = request.Email.Trim(),
                Subject = "Fundoo Notes - Password Reset Token",
                Body = $"Hello,\n\nYour password reset token is:\n\n{token}\n\nThis token will expire in 10 minutes.\nUse this token in the ResetPassword API.",
                IsHtml = false
            };

            var emailResponse = await _emailService.SendEmailAsync(emailRequest);

            if (!emailResponse.Success)
            {
                return new ForgotPasswordResponseModel
                {
                    Success = false,
                    Message = $"Email could not be sent: {emailResponse.Message}",
                    Token = token
                };
            }

            return new ForgotPasswordResponseModel
            {
                Success = true,
                Message = "Password reset email sent successfully. (Please check your Inbox and Spam/Junk folder)",
                Token = token
            };
        }
    }
}

