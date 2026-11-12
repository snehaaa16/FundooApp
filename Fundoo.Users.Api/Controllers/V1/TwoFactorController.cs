using Asp.Versioning;
using Fundoo.User.Application.Services;
using Fundoo.Users.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Claims;

namespace Fundoo.User.Api.Controllers.V1
{
    /// <summary>
    /// Email-based Two-Factor Authentication (OTP sent to registered email).
    ///
    /// Flow:
    ///   1. POST /api/v1/User/login        → returns a partial JWT (password verified)
    ///   2. POST /api/v1/TwoFactor/send    → generates 6-digit OTP, stores in Redis (5 min TTL),
    ///                                        sends email via Notification service
    ///   3. POST /api/v1/TwoFactor/verify  → validates OTP from Redis → deletes it → returns
    ///                                        { verified: true }
    ///   4. POST /api/v1/TwoFactor/enable  → opts the user into Email-2FA
    ///   5. POST /api/v1/TwoFactor/disable → opts the user out of Email-2FA
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class TwoFactorController : ControllerBase
    {
        // Redis key: "2fa:otp:{userId}"  →  "123456"  (TTL 5 min)
        private const string KeyPrefix   = "2fa:otp:";
        private const int    OtpTtlMins  = 5;
        private const int    OtpLength   = 6;

        private readonly IUserBL             _userBL;
        private readonly IJwtService         _jwt;
        private readonly IEmailService       _email;
        private readonly IDistributedCache   _cache;

        public TwoFactorController(
            IUserBL           userBL,
            IJwtService       jwt,
            IEmailService     email,
            IDistributedCache cache)
        {
            _userBL = userBL;
            _jwt    = jwt;
            _email  = email;
            _cache  = cache;
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private int CurrentUserId()
        {
            var claim = User.FindFirst("user_id")
                     ?? User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
        }

        private string? CurrentUserEmail()
            => User.FindFirst(ClaimTypes.Email)?.Value;

        /// <summary>Generates a cryptographically random N-digit numeric OTP.</summary>
        private static string GenerateOtp(int digits = OtpLength)
        {
            // Use Random.Shared (thread-safe) with a modulo to stay numeric
            var bytes = new byte[4];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
            uint value = BitConverter.ToUInt32(bytes, 0);
            int  otp   = (int)(value % (uint)Math.Pow(10, digits));
            return otp.ToString($"D{digits}");   // zero-padded e.g. "007412"
        }

        private string RedisKey(int userId) => $"{KeyPrefix}{userId}";

        // ── SEND OTP ─────────────────────────────────────────────────────────
        /// <summary>
        /// Generates a 6-digit OTP, stores it in Redis for 5 minutes,
        /// and sends it to the user's registered email address.
        /// Call this after a successful /login when 2FA is required.
        /// </summary>
        [Authorize]
        [HttpPost("send")]
        public async Task<IActionResult> SendOtp()
        {
            int    userId = CurrentUserId();
            string? email = CurrentUserEmail();

            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { message = "Email claim missing in token." });

            // Generate OTP and persist in Redis with TTL
            string otp = GenerateOtp();
            await _cache.SetStringAsync(
                RedisKey(userId),
                otp,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(OtpTtlMins)
                });

            // Build a clean HTML email body
            string htmlBody = $@"
                <div style='font-family:Arial,sans-serif;max-width:480px;margin:auto'>
                  <h2 style='color:#4a90e2'>🔐 Fundoo — Login Verification</h2>
                  <p>Your one-time password (OTP) is:</p>
                  <div style='font-size:36px;font-weight:bold;letter-spacing:8px;
                              color:#222;padding:16px;background:#f4f4f4;
                              text-align:center;border-radius:8px'>
                    {otp}
                  </div>
                  <p style='color:#666;margin-top:16px'>
                    This code expires in <strong>{OtpTtlMins} minutes</strong>.<br/>
                    If you didn't request this, please ignore this email.
                  </p>
                </div>";

            var emailResult = await _email.SendEmailAsync(new EmailRequestModel
            {
                To      = email,
                Subject = "Fundoo — Your Login OTP",
                Body    = htmlBody,
                IsHtml  = true
            });

            if (!emailResult.Success)
                return StatusCode(503, new
                {
                    message = $"Could not send OTP email: {emailResult.Message}"
                });

            return Ok(new
            {
                message   = $"OTP sent to {MaskEmail(email)}. Valid for {OtpTtlMins} minutes.",
                expiresIn = $"{OtpTtlMins} minutes"
            });
        }

        // ── VERIFY OTP ────────────────────────────────────────────────────────
        /// <summary>
        /// Validates the OTP submitted by the user against the value stored in Redis.
        /// On success the OTP is deleted (single-use) and { verified: true } is returned.
        /// </summary>
        [Authorize]
        [HttpPost("verify")]
        public async Task<IActionResult> VerifyOtp([FromBody] EmailOtpVerifyRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Code) || req.Code.Length != OtpLength)
                return BadRequest(new { message = $"A {OtpLength}-digit OTP code is required." });

            int    userId  = CurrentUserId();
            string redisKey = RedisKey(userId);

            // Fetch stored OTP from Redis
            string? stored = await _cache.GetStringAsync(redisKey);

            if (stored is null)
                return BadRequest(new
                {
                    message = "OTP has expired or was never sent. Please request a new one."
                });

            // Constant-time comparison to prevent timing attacks
            bool valid = CryptographicEquals(stored, req.Code.Trim());

            if (!valid)
                return Unauthorized(new { message = "Invalid OTP. Please try again." });

            // Single-use: delete immediately after successful verification
            await _cache.RemoveAsync(redisKey);

            return Ok(new { message = "OTP verified successfully.", verified = true });
        }

        // ── ENABLE Email-2FA ─────────────────────────────────────────────────
        /// <summary>
        /// Enables Email-2FA after verifying the OTP code sent to user's email.
        /// </summary>
        [Authorize]
        [HttpPost("enable")]
        public async Task<IActionResult> Enable([FromBody] EmailOtpVerifyRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Code) || req.Code.Length != OtpLength)
                return BadRequest(new { message = $"A {OtpLength}-digit OTP code is required to enable 2FA." });

            int    userId   = CurrentUserId();
            string redisKey = RedisKey(userId);

            var user = await _userBL.GetUserByIdAsync(userId);
            if (user is null) return NotFound(new { message = "User not found." });
            if (user.IsTwoFactorEnabled)
                return BadRequest(new { message = "Email 2FA is already enabled." });

            // Verify OTP from Redis first
            string? stored = await _cache.GetStringAsync(redisKey);
            if (stored is null)
                return BadRequest(new { message = "OTP has expired or was never sent. Please call /send first." });

            if (!CryptographicEquals(stored, req.Code.Trim()))
                return Unauthorized(new { message = "Invalid OTP. Please try again." });

            // Remove OTP from Redis so it cannot be reused
            await _cache.RemoveAsync(redisKey);

            // Save 2FA enabled status in database
            bool saved = await _userBL.Enable2FAAsync(userId, "EMAIL_2FA");
            return saved
                ? Ok(new { message = "Email 2FA enabled successfully. Your account is now secured." })
                : StatusCode(500, new { message = "Failed to enable 2FA in database." });
        }

        // ── DISABLE Email-2FA ─────────────────────────────────────────────────
        /// <summary>
        /// Disables Email-2FA. Requires a valid OTP to prevent unauthorised disable.
        /// </summary>
        [Authorize]
        [HttpPost("disable")]
        public async Task<IActionResult> Disable([FromBody] EmailOtpVerifyRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Code))
                return BadRequest(new { message = "OTP code is required to disable 2FA." });

            int    userId   = CurrentUserId();
            string redisKey = RedisKey(userId);

            var user = await _userBL.GetUserByIdAsync(userId);
            if (user is null) return NotFound();
            if (!user.IsTwoFactorEnabled)
                return BadRequest(new { message = "Email 2FA is already disabled." });

            // Verify OTP before allowing disable
            string? stored = await _cache.GetStringAsync(redisKey);
            if (stored is null)
                return BadRequest(new { message = "No active OTP found. Send a new OTP first." });

            if (!CryptographicEquals(stored, req.Code.Trim()))
                return Unauthorized(new { message = "Invalid OTP." });

            await _cache.RemoveAsync(redisKey);
            await _userBL.Enable2FAAsync(userId, string.Empty);   // clears the flag

            return Ok(new { message = "Email 2FA disabled successfully." });
        }

        // ── STEP: LOGIN VERIFY (Issue Final Access Token) ───────────────────
        /// <summary>
        /// Called when Login returns isTwoFactorRequired=true.
        /// Verifies the OTP sent to email and issues the final JWT Access Token.
        /// </summary>
        [HttpPost("login-verify")]
        public async Task<IActionResult> LoginVerify([FromBody] LoginTwoFactorVerifyRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Code))
                return BadRequest(new { message = "Email and OTP code are required." });

            var user = await _userBL.LoginUserBL(new LoginModel { Email = req.Email.Trim(), Password = "" });
            if (user is null)
                return NotFound(new { message = "User not found." });

            string redisKey = RedisKey(user.UserId);
            string? stored = await _cache.GetStringAsync(redisKey);

            if (stored is null)
                return BadRequest(new { message = "OTP has expired or was never sent. Please login again." });

            if (!CryptographicEquals(stored, req.Code.Trim()))
                return Unauthorized(new { message = "Invalid OTP code. Please try again." });

            // Remove OTP from Redis so it cannot be used again
            await _cache.RemoveAsync(redisKey);

            // Generate the final access token
            var userResponse = new UserResponseModel
            {
                UserId = user.UserId,
                Email = user.Email,
                Fname = user.FirstName,
                Lname = user.LastName,
                ContactNo = user.PhoneNumber,
                IsTwoFactorRequired = false
            };
            userResponse.Token = _jwt.GenerateToken(userResponse);

            return Ok(new
            {
                success = true,
                message = "2FA verified. Login completed successfully.",
                data = userResponse
            });
        }

        // ── Utility ───────────────────────────────────────────────────────────

        /// <summary>Masks email for safe display: a***@gmail.com</summary>
        private static string MaskEmail(string email)
        {
            var parts = email.Split('@');
            if (parts.Length != 2) return "***";
            string local  = parts[0];
            string domain = parts[1];
            string masked = local.Length <= 2
                ? new string('*', local.Length)
                : local[0] + new string('*', local.Length - 1);
            return $"{masked}@{domain}";
        }

        /// <summary>
        /// Constant-time string comparison — prevents timing-based OTP brute-force.
        /// </summary>
        private static bool CryptographicEquals(string a, string b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }

    // ── Request / Response models ─────────────────────────────────────────────
    public record EmailOtpVerifyRequest(string Code);
    public record LoginTwoFactorVerifyRequest(string Email, string Code);
}
