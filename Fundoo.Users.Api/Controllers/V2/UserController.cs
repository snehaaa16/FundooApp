using Asp.Versioning;
using Fundoo.User.Application.Command;
using Fundoo.User.Domain.Models;
using Fundoo.Users.Domain.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ModelLayer.Models;
using System.Security.Claims;

namespace Fundoo.User.Api.Controllers.V2
{
    // ─────────────────────────────────────────────────────────────────────────
    // V2 Changes over V1:
    //  1. Register  → returns enriched ApiResponse<T> with apiVersion + timestamp
    //  2. Login     → response includes tokenExpiresAt (UTC) field
    //  3. Profile   → returns structured UserProfile object (not anonymous type)
    //  4. ForgotPassword → returns 202 Accepted (not 200 OK) for async semantic
    //  5. ResetPassword  → same as V1 (no breaking change needed)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Enriched wrapper returned by all V2 endpoints.</summary>
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public string ApiVersion { get; set; } = "2.0";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Structured user profile returned by GET /profile in V2.</summary>
    public class UserProfileV2
    {
        public string? UserId { get; set; }
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public string? IssuedAt { get; set; }   // JWT iat claim (if present)
    }

    /// <summary>Login response enriched with token expiry info (V2).</summary>
    public class LoginResponseV2
    {
        public UserResponseModel? User { get; set; }
        public string? Token { get; set; }
        public DateTime TokenExpiresAt { get; set; }
    }

    [ApiController]
    [ApiVersion("2.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly ISender _sender;

        public UserController(ISender sender)
        {
            _sender = sender;
        }

        // ── REGISTER ──────────────────────────────────────────────────────────
        // V2: Same logic, but response wrapped in ApiResponse<T> with timestamp
        [EnableRateLimiting("register-policy")]
        [HttpPost("register")]
        public async Task<IActionResult> RegisterUser(RegistrationModel registrationModel)
        {
            CreateUserCommand command = new CreateUserCommand(registrationModel);
            UserResponseModel result = await _sender.Send(command);

            var response = new ApiResponse<UserResponseModel>
            {
                Success = true,
                Message = "User Registered Successfully",
                Data = result
            };
            return CreatedAtAction(nameof(Profile), response);
        }

        // ── LOGIN ─────────────────────────────────────────────────────────────
        // V2: Response includes tokenExpiresAt (UTC) so clients can schedule refresh
        [EnableRateLimiting("login-policy")]
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginModel model)
        {
            LoginCommand command = new LoginCommand(model);
            UserResponseModel? result = await _sender.Send(command);

            if (result == null)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Invalid email or password"
                });
            }

            // Parse expiry from the JWT token so the client knows when to refresh
            DateTime tokenExpiresAt = DateTime.UtcNow.AddMinutes(60); // fallback
            if (!string.IsNullOrWhiteSpace(result.Token))
            {
                try
                {
                    var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                    var jwt = handler.ReadJwtToken(result.Token);
                    tokenExpiresAt = jwt.ValidTo;
                }
                catch { /* keep fallback */ }
            }

            var loginResponse = new LoginResponseV2
            {
                User = result,
                Token = result.Token,
                TokenExpiresAt = tokenExpiresAt
            };

            return Ok(new ApiResponse<LoginResponseV2>
            {
                Success = true,
                Message = "Login successful",
                Data = loginResponse
            });
        }

        // ── PROFILE ───────────────────────────────────────────────────────────
        // V2: Returns strongly-typed UserProfileV2 (no anonymous object)
        [Authorize]
        [HttpGet("profile")]
        public IActionResult Profile()
        {
            var profile = new UserProfileV2
            {
                UserId   = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                Email    = User.FindFirst(ClaimTypes.Email)?.Value,
                FullName = User.FindFirst(ClaimTypes.Name)?.Value,
                IssuedAt = User.FindFirst("iat") is { } iatClaim
                    ? DateTimeOffset.FromUnixTimeSeconds(long.Parse(iatClaim.Value))
                                   .UtcDateTime.ToString("o")
                    : null
            };

            return Ok(new ApiResponse<UserProfileV2>
            {
                Success = true,
                Message = "Profile fetched successfully",
                Data = profile
            });
        }

        // ── FORGOT PASSWORD ───────────────────────────────────────────────────
        // V2: Returns 202 Accepted (async semantic — email is being sent)
        [EnableRateLimiting("password-reset-policy")]
        [HttpPost("ForgotPassword")]
        public async Task<IActionResult> ForgotPasswordAsync(ForgotPasswordRequestModel req)
        {
            var res = await _sender.Send(new ForgotPasswordCommand(req));

            var response = new ApiResponse<ForgotPasswordResponseModel>
            {
                Success = res.Success,
                Message = res.Success
                    ? "Password reset email dispatched. Check your inbox."
                    : "No account found with that email address.",
                Data = res
            };

            // 202 Accepted: request is valid, email sending is async
            return res.Success ? Accepted(response) : NotFound(response);
        }

        // ── RESET PASSWORD ────────────────────────────────────────────────────
        // V2: Same logic as V1, wrapped in ApiResponse<T>
        [EnableRateLimiting("password-reset-policy")]
        [HttpPost("ResetPassword")]
        public async Task<IActionResult> ResetPasswordAsync(ResetPasswordRequestModel req)
        {
            var res = await _sender.Send(new ResetPasswordCommand(req));

            var response = new ApiResponse<ResetPasswordResponseModel>
            {
                Success = res.Success,
                Message = res.Message,
                Data = res
            };

            return res.Success ? Ok(response) : BadRequest(response);
        }
    }
}
