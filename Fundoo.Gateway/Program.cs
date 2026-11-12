using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Load ocelot.json configuration
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

// Configure CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Built-in .NET IP + Group based Rate Limiter
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"status\":429,\"message\":\"Rate limit exceeded. Too many requests for this IP and Group. Please try again later.\"}",
            token);
    };

    // Partitioned Global Limiter with Key = (Client IP + Group)
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        // 1. Header se IP Address nikalna
        string clientIp = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                          ?? httpContext.Connection.RemoteIpAddress?.ToString()
                          ?? "unknown_ip";

        // 2. Path se Group nikalna (e.g. /api/User/... -> 'User', /api/notes -> 'notes', /api/notification -> 'notification')
        var segments = httpContext.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string group = segments != null && segments.Length > 1 ? segments[1] : (segments != null && segments.Length > 0 ? segments[0] : "general");

        // 3. Composite Key: IP + Group (e.g. "192.168.1.10_User")
        string partitionKey = $"{clientIp}_{group}";

        // 4. Group ke basis par limit:
        // User group: 5 requests / 1 min (Auth security)
        // Notification group: 3 requests / 1 min (Email spam protection)
        // Notes / Others: 30 requests / 1 min (Normal business operations)
        int limit = group.Equals("User", StringComparison.OrdinalIgnoreCase) ? 5 :
                    group.Equals("notification", StringComparison.OrdinalIgnoreCase) ? 3 : 30;

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: partitionKey,
            factory: key => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
});

// Register Ocelot services
builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();

app.UseCors("AllowAll");

// Apply .NET built-in Rate Limiting middleware
app.UseRateLimiter();

// Use Ocelot middleware as reverse proxy to route requests
await app.UseOcelot();

app.Run();
