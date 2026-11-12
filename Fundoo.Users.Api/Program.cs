using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Fundoo.User.Api.Swagger;
using Fundoo.User.Application.Handler;
using Fundoo.User.Application.Services;
using Fundoo.User.Infrastructure.Services.Users;
using Fundoo.Users.Infrastructure.Email;
using Fundoo.Users.Infrastructure.Persistence;
using Fundoo.Users.Infrastructure.Persistence.Repositories;
using Fundoo.Users.Infrastructure.Services.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Polly;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text;
using System.Threading.RateLimiting;

namespace Fundoo.User.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ─────────────────────────────────────────────────────────────────────
        // API VERSIONING — URL path: /api/v1/... and /api/v2/...
        // ─────────────────────────────────────────────────────────────────────
        builder.Services
            .AddApiVersioning(options =>
            {
                // If client sends no version header/segment, default to v1
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;

                // Report supported + deprecated versions in response headers:
                //   api-supported-versions: 1.0, 2.0
                //   api-deprecated-versions: (none yet)
                options.ReportApiVersions = true;

                // Strategy: URL segment  →  /api/v1/...  /api/v2/...
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddApiExplorer(options =>
            {
                // Group name format used by Swagger: "v1", "v2", etc.
                options.GroupNameFormat = "'v'VVV";

                // Automatically substitute {version} in route templates
                options.SubstituteApiVersionInUrl = true;
            });

        // Add services to the container.
        builder.Services.AddControllers();

        // ─────────────────────────────────────────────────────────────────────
        // HttpClient for Email/Notification Service with full Resilience Pipeline
        // ─────────────────────────────────────────────────────────────────────
        builder.Services.AddHttpClient<IEmailService, EmailService>((services, client) =>
        {
            var configuration = services.GetRequiredService<IConfiguration>();
            var baseUrl = configuration["NotificationService:BaseUrl"]
                ?? throw new InvalidOperationException("NotificationService:BaseUrl is not configured.");
            client.BaseAddress = new Uri(baseUrl);
        })
        .AddResilienceHandler("NotificationServicePipeline", pipelineBuilder =>
        {
            // 1. OUTBOUND RATE LIMITER
            //    Token Bucket: max 20 requests per minute (prevents hammering Email API)
            pipelineBuilder.AddRateLimiter(new TokenBucketRateLimiter(
                new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 20,
                    QueueLimit = 0,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    TokensPerPeriod = 20,
                    AutoReplenishment = true
                }));

            // 2. RETRY — Exponential Backoff + Jitter (2s → 4s → 8s)
            //    Handles: 5xx errors, 408 Timeout, network drops
            pipelineBuilder.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(2)
            });

            // 3. CIRCUIT BREAKER
            //    Opens if >= 50% requests fail in a 10s window (min 4 requests needed)
            //    Stays open for 15s, then half-opens to test recovery
            pipelineBuilder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(10),
                MinimumThroughput = 4,
                BreakDuration = TimeSpan.FromSeconds(15)
            });

            // 4. PER-ATTEMPT TIMEOUT — each try cancelled after 30 seconds (allows SMTP handshake)
            pipelineBuilder.AddTimeout(new HttpTimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(30)
            });
        });

        // ─────────────────────────────────────────────────────────────────────
        // INBOUND RATE LIMITING — Per endpoint policies (applied via attributes)
        // ─────────────────────────────────────────────────────────────────────
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Custom 429 response body (JSON)
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    message = "Too many requests. Please try again later."
                }, cancellationToken: token);
            };

            // LOGIN — 5 attempts per minute per IP (prevents brute-force)
            options.AddPolicy("login-policy", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            // REGISTER — 5 attempts per hour per IP (prevents bot account creation)
            options.AddPolicy("register-policy", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0
                    }));

            // FORGOT/RESET PASSWORD — 50 attempts per minute (relaxed for testing)
            options.AddPolicy("password-reset-policy", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 50,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
        });

        builder.Services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(CreateUserHandler).Assembly));

        builder.Services.AddScoped<IUserBL, UserBL>();
        builder.Services.AddScoped<IUserRL, UserRL>();
        builder.Services.AddScoped<IJwtService, JwtService>();

        // ─────────────────────────────────────────────────────────────────────
        // REDIS — OTP store for Email-based 2FA (key: "2fa:otp:{userId}", TTL: 5 min)
        // ─────────────────────────────────────────────────────────────────────
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = builder.Configuration.GetConnectionString("Redis")
                ?? "localhost:6379";
            options.InstanceName  = "FundooUsers:";
        });

        builder.Services.AddDbContext<FundooContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("FundooConnection"))
        );

        builder.Services.AddEndpointsApiExplorer();

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    ValidAudience = builder.Configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
                    ClockSkew = TimeSpan.Zero
                };
            })
            // ── Google OAuth 2.0 ─────────────────────────────────────────────
            .AddGoogle(googleOptions =>
            {
                googleOptions.ClientId     = builder.Configuration["Authentication:Google:ClientId"]!;
                googleOptions.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
                googleOptions.CallbackPath = "/signin-google";
            });

        // ─────────────────────────────────────────────────────────────────────
        // SWAGGER — dynamically generates one doc per API version
        //   ConfigureSwaggerOptions reads IApiVersionDescriptionProvider at runtime
        //   so we never need to hard-code "v1" / "v2" here again.
        // ─────────────────────────────────────────────────────────────────────
        builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();

            // Dynamically build one Swagger UI endpoint per API version
            app.UseSwaggerUI(options =>
            {
                var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

                foreach (var description in provider.ApiVersionDescriptions)
                {
                    options.SwaggerEndpoint(
                        url: $"/swagger/{description.GroupName}/swagger.json",
                        name: description.GroupName.ToUpperInvariant());
                }
            });
        }

        app.UseRateLimiter();       // Rate Limiting middleware (must be before Auth)
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}
