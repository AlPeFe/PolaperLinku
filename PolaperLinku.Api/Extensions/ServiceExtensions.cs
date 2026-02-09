using Microsoft.EntityFrameworkCore;
using PolaperLinku.Api.Models;
using PolaperLinku.Api.Services;

namespace PolaperLinku.Api.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration configuration)
    {
        var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "data", "polaperlinku.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        services.AddScoped<DbContext, AppDbContext>();
        services.AddSingleton<MetadataCache>();
        services.AddHttpClient<MetadataExtractor>();
        services.AddLogging();

        return services;
    }
}
