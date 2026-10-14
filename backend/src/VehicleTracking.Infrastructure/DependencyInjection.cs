using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VehicleTracking.Application.Ingestion;
using VehicleTracking.Infrastructure.Feeds;
using VehicleTracking.Infrastructure.Persistence;

namespace VehicleTracking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddPersistence(services, configuration);
        AddFeeds(services, configuration);
        return services;
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        // Şifre repoda değil: geliştirmede user-secrets'tan, Docker'da ortam değişkeninden gelir.
        string connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres ayarı eksik.");

        services.AddDbContext<VehicleTrackingDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddHostedService<DatabaseInitializer>();
    }

    private static void AddFeeds(IServiceCollection services, IConfiguration configuration)
    {
        // Ayar eksikse uygulama ilk istekte değil, açılırken hata versin.
        services.AddOptions<EnturOptions>()
            .Bind(configuration.GetSection(EnturOptions.SectionName))
            .Validate(o => o.VehiclePositionsUrl is not null && !string.IsNullOrWhiteSpace(o.ClientName),
                "Entur:VehiclePositionsUrl ve Entur:ClientName zorunlu.")
            .ValidateOnStart();

        // IHttpClientFactory: HttpClient'ı her seferinde new'lemek soket tükenmesine, tek örneği sonsuza dek
        // tutmak DNS değişikliklerini kaçırmaya yol açar; fabrika ikisini de yönetir.
        services.AddHttpClient<IFeedSource, EnturFeedSource>((sp, client) =>
            {
                EnturOptions entur = sp.GetRequiredService<IOptions<EnturOptions>>().Value;
                client.Timeout = entur.RequestTimeout;
                client.DefaultRequestHeaders.Add("ET-Client-Name", entur.ClientName);
            })
            // gzip: yanıt ~1,7 MB → ~450 KB (gözlem), okuma belirgin şekilde hızlanıyor.
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip,
            });
    }
}
