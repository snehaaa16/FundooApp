using Fundoo.Notification.Infrastructure.Email.Template;
using Fundoo.Notification.Application.Feature.Notification.Services;
using Fundoo.Notification.Infrastructure.Email;
using Microsoft.OpenApi.Models;

namespace Fundoo.Notification.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();

        builder.Services.Configure<SmtpSettings>(
            builder.Configuration.GetSection("SmtpSettings"));

        builder.Services.AddScoped<IEmailService, EmailService>();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Fundoo Notification API",
                Version = "v1",
                Description = "Email & Notification Service for Fundoo App"
            });
        });

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "Fundoo Notification API v1");
            });
        }

        app.UseAuthorization();
        app.MapControllers();

        app.Run();
    }
}
