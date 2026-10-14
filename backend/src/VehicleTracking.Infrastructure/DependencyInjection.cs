using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VehicleTracking.Infrastructure.Persistence;

namespace VehicleTracking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Şifre repoda değil: geliştirmede user-secrets'tan, Docker'da ortam değişkeninden gelir.
        string connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres ayarı eksik.");

        services.AddDbContext<VehicleTrackingDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddHostedService<DatabaseInitializer>();

        return services;
    }
}
