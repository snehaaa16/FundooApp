using Asp.Versioning;
using Fundoo.User.Application.Services;
using Fundoo.Users.Domain.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Fundoo.User.Api.Controllers.V1
{
    /// <summary>
    /// Google OAuth 2.0 login flow:
    ///   1. GET /api/v1/OAuth/google-login   → redirects to Google consent screen
    ///   2. Google redirects to /api/v1/OAuth/google-callback
    ///   3. We extract the user's Google profile, upsert in DB, return JWT
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class OAuthController : ControllerBase
    {
        private readonly IJwtService _jwt;
        private readonly IUserBL    _userBL;

        public OAuthController(IJwtService jwt, IUserBL userBL)
        {
            _jwt    = jwt;
            _userBL = userBL;
        }

        /// <summary>
        /// Step 1: Redirect browser to Google's OAuth consent screen.
        /// </summary>
        [HttpGet("google-login")]
        public IActionResult GoogleLogin()
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = Url.Action(nameof(GoogleCallback), "OAuth",
                    new { version = "1" }, Request.Scheme)
            };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        /// <summary>
        /// Step 2: Google redirects here after the user grants permission.
        /// Extracts email/name from Google claims, issues a Fundoo JWT.
        /// </summary>
        [HttpGet("google-callback")]
        public async Task<IActionResult> GoogleCallback()
        {
            // Authenticate using the Google cookie set by the middleware
            var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);

            if (!result.Succeeded || result.Principal is null)
                return Unauthorized(new { message = "Google authentication failed." });

            // Extract Google profile claims
            var email     = result.Principal.FindFirstValue(ClaimTypes.Email);
            var firstName = result.Principal.FindFirstValue(ClaimTypes.GivenName)
                         ?? result.Principal.FindFirstValue(ClaimTypes.Name)
                         ?? "Google";
            var lastName  = result.Principal.FindFirstValue(ClaimTypes.Surname) ?? "User";
            var googleId  = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { message = "Email not provided by Google." });

            // Try to find existing user OR auto-register them
            var existingUser = await _userBL.LoginUserBL(new Fundoo.Users.Domain.Models.LoginModel
            {
                Email    = email,
                Password = string.Empty   // Google users have no local password
            });

            UserResponseModel userResponse;

            if (existingUser is not null)
            {
                // Existing user → build response model
                userResponse = new UserResponseModel
                {
                    UserId = existingUser.UserId,
                    Email  = existingUser.Email,
                    Fname  = existingUser.FirstName
                };
            }
            else
            {
                // New user → auto-register with a random password (Google manages auth)
                var newUser = new Fundoo.Users.Domain.Entity.UserEntity
                {
                    FirstName = firstName,
                    LastName  = lastName,
                    Email     = email,
                    Password  = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString())
                };

                var registered = await _userBL.RegisterUserBL(newUser);

                userResponse = new UserResponseModel
                {
                    UserId = registered.UserId,
                    Email  = registered.Email,
                    Fname  = registered.FirstName
                };
            }

            // Issue Fundoo JWT
            var token = _jwt.GenerateToken(userResponse);
            userResponse.Token = token;

            return Ok(new
            {
                success    = true,
                message    = "Google login successful",
                data       = userResponse,
                loginType  = "google"
            });
        }
    }
}
