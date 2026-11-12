using Asp.Versioning;
using Fundoo.User.Application.Command;
using Fundoo.User.Domain.Models;
using Fundoo.Users.Domain.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace Fundoo.User.Api.Controllers.V1
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly ISender _sender;

        public UserController(ISender sender)
        {
            _sender = sender;
        }

        // REGISTER: 5 requests per hour per IP (bot prevention)
        [EnableRateLimiting("register-policy")]
        [HttpPost("register")]
        public async Task<ResponseModel<UserResponseModel>> RegisterUser(RegistrationModel registrationModel)
        {
            CreateUserCommand command = new CreateUserCommand(registrationModel);
            UserResponseModel response = await _sender.Send(command);

            return new ResponseModel<UserResponseModel>
            {
                Data = response,
                Success = true,
                Message = "User Registered Successfully"
            };
        }

        // LOGIN: 5 requests per minute per IP (brute-force prevention)
        [EnableRateLimiting("login-policy")]
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginModel model)
        {
            LoginCommand command = new LoginCommand(model);
            var result = await _sender.Send(command);

            var response = new ResponseModel<UserResponseModel>();

            if (result == null)
            {
                response.Success = false;
                response.Message = "Invalid email or password";
                return Unauthorized(response);
            }

            if (result.IsTwoFactorRequired)
            {
                response.Success = true;
                response.Message = "2FA is enabled. An OTP has been sent to your email. Please verify via POST /api/v1/TwoFactor/login-verify with the code to get your access token.";
                response.Data = result;
                return Ok(response);
            }

            response.Success = true;
            response.Message = "Login successful";
            response.Data = result;
            return Ok(response);
        }

        // PROFILE: No rate limit — read-only, authenticated endpoint
        [Authorize]
        [HttpGet("profile")]
        public IActionResult Profile()
        {
            string? userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string? email = User.FindFirst(ClaimTypes.Email)?.Value;
            string? name = User.FindFirst(ClaimTypes.Name)?.Value;

            return Ok(new { userId, email, name });
        }

        // FORGOT PASSWORD: 3 requests per 15 min per IP (email bombing prevention)
        [EnableRateLimiting("password-reset-policy")]
        [HttpPost("ForgotPassword")]
        public async Task<IActionResult> ForgotPasswordAsync(ForgotPasswordRequestModel req)
        {
            var res = await _sender.Send(new ForgotPasswordCommand(req));
            return Ok(res);
        }

        // RESET PASSWORD: 3 requests per 15 min per IP (same as forgot)
        [EnableRateLimiting("password-reset-policy")]
        [HttpPost("ResetPassword")]
        public async Task<IActionResult> ResetPasswordAsync(ResetPasswordRequestModel req)
        {
            var res = await _sender.Send(new ResetPasswordCommand(req));
            if (res.Success)
            {
                return Ok(res);
            }
            return BadRequest(res);
        }
    }
}
