using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VehicleTracking.Application.Ingestion;
using VehicleTracking.Application.Storage;

namespace VehicleTracking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<IngestionOptions>(configuration.GetSection(IngestionOptions.SectionName));

        // TimeProvider: "şu an" bilgisini enjekte ediyoruz ki testlerde zamanı kontrol edebilelim.
        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<FeedIngestor>();
        services.AddSingleton<IngestionMetrics>();

        return services;
    }
}
