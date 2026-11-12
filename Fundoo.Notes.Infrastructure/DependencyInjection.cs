using Fundoo.Notes.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fundoo.Notes.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotesInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<NotesRepository>();
        services.AddScoped<INoteRepository>(provider => provider.GetRequiredService<NotesRepository>());
        return services;
    }
}
