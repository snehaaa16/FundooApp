using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Fundoo.User.Api.Swagger
{
    /// <summary>
    /// Dynamically generates one SwaggerDoc per discovered API version
    /// so Swagger UI shows a separate dropdown entry for v1 and v2.
    /// </summary>
    public class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
    {
        private readonly IApiVersionDescriptionProvider _provider;

        public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
        {
            _provider = provider;
        }

        public void Configure(SwaggerGenOptions options)
        {
            foreach (var description in _provider.ApiVersionDescriptions)
            {
                options.SwaggerDoc(
                    description.GroupName,
                    CreateInfoForApiVersion(description));
            }

            // JWT Bearer security definition (shared across all versions)
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name        = "Authorization",
                Type        = SecuritySchemeType.Http,
                Scheme      = "bearer",
                BearerFormat = "JWT",
                In          = ParameterLocation.Header,
                Description = "Enter your JWT token (without the 'Bearer' prefix)"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id   = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        }

        private static OpenApiInfo CreateInfoForApiVersion(ApiVersionDescription description)
        {
            var info = new OpenApiInfo
            {
                Title   = "Fundoo User Service API",
                Version = description.ApiVersion.ToString()
            };

            if (description.ApiVersion.MajorVersion == 1)
            {
                info.Description = "v1 — Stable baseline API for user authentication and account management.";
            }
            else if (description.ApiVersion.MajorVersion == 2)
            {
                info.Description =
                    "v2 — Enhanced API with enriched responses (ApiVersion, Timestamp), " +
                    "tokenExpiresAt on login, structured profile object, and 202 Accepted for ForgotPassword.";
            }

            if (description.IsDeprecated)
            {
                info.Description += " ⚠️ This API version has been deprecated.";
            }

            return info;
        }
    }
}
