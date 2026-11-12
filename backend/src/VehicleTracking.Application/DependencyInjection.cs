using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VehicleTracking.Application.History;
using VehicleTracking.Application.Ingestion;
using VehicleTracking.Application.Live;
using VehicleTracking.Application.Storage;

namespace VehicleTracking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<IngestionOptions>(configuration.GetSection(IngestionOptions.SectionName));

        services.Configure<HistoryOptions>(configuration.GetSection(HistoryOptions.SectionName));
        services.AddSingleton<IValidator<ResolvedHistoryQuery>, HistoryQueryValidator>();
        services.AddScoped<IHistoryQueryService, HistoryQueryService>();

        // TimeProvider: "şu an" bilgisini enjekte ediyoruz ki testlerde zamanı kontrol edebilelim.
        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<IFeedIngestor, FeedIngestor>();
        services.AddSingleton<IFeedIngestionCycle, FeedIngestionCycle>();
        services.AddSingleton<IIngestionMetrics, IngestionMetrics>();
        services.AddSingleton<ILiveVehicleStore, LiveVehicleState>();

        return services;
    }
}
