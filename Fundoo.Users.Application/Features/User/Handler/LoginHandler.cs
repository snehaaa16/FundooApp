using Fundoo.User.Application.Command;
using Fundoo.User.Application.Services;
using Fundoo.Users.Domain.Models;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Cryptography;

namespace Fundoo.User.Application.Handler
{
	public class LoginHandler : IRequestHandler<LoginCommand, UserResponseModel?>
    {
        private readonly IJwtService _jwtService;
        private readonly IUserBL _userBL;
        private readonly IEmailService _emailService;
        private readonly IDistributedCache _cache;

        public LoginHandler(
            IJwtService jwtService,
            IUserBL userBL,
            IEmailService emailService,
            IDistributedCache cache)
		{
            _jwtService = jwtService;
            _userBL = userBL;
            _emailService = emailService;
            _cache = cache;
		}

		public async Task<UserResponseModel?> Handle(LoginCommand command, CancellationToken cancellationToken)
		{
            var user = await _userBL.LoginUserBL(command._loginModel);
            if (user == null) return null;

            bool isValid = BCrypt.Net.BCrypt.Verify(command._loginModel.Password, user.Password);
            if (!isValid) return null;

            UserResponseModel response = new UserResponseModel();
            response.UserId = user.UserId;
            response.ContactNo = user.PhoneNumber;
            response.Email = user.Email;
            response.Fname = user.FirstName;
            response.Lname = user.LastName;

            // ── CHECK 2FA ────────────────────────────────────────────────────────
            if (user.IsTwoFactorEnabled)
            {
                // 2FA is active: DO NOT issue full access token yet!
                response.IsTwoFactorRequired = true;
                response.Token = string.Empty;

                // 1. Generate 6-digit OTP
                var bytes = new byte[4];
                RandomNumberGenerator.Fill(bytes);
                uint val = BitConverter.ToUInt32(bytes, 0);
                string otp = (val % 1000000).ToString("D6");

                // 2. Store in Redis with key: "2fa:otp:{userId}" (TTL: 5 min)
                await _cache.SetStringAsync(
                    $"2fa:otp:{user.UserId}",
                    otp,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                    },
                    cancellationToken);

                // 3. Send OTP to User's Email
                string html = $@"
                    <div style='font-family:Arial,sans-serif;max-width:480px;margin:auto'>
                      <h2 style='color:#4a90e2'>🔐 Fundoo — Login OTP Verification</h2>
                      <p>Your one-time login verification code is:</p>
                      <div style='font-size:36px;font-weight:bold;letter-spacing:8px;
                                  color:#222;padding:16px;background:#f4f4f4;
                                  text-align:center;border-radius:8px'>
                        {otp}
                      </div>
                      <p style='color:#666;margin-top:16px'>
                        This code is valid for 5 minutes. Enter this code to complete login.
                      </p>
                    </div>";

                await _emailService.SendEmailAsync(new EmailRequestModel
                {
                    To = user.Email,
                    Subject = "Fundoo — Login OTP Code",
                    Body = html,
                    IsHtml = true
                });

                return response;
            }

            // Normal login (2FA disabled): issue full access token
            response.Token = _jwtService.GenerateToken(response);
            return response;
        }
	}
}
