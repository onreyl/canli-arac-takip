using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using VehicleTracking.Application.History;
using VehicleTracking.Application.Ingestion;
using VehicleTracking.Infrastructure.Buffering;
using VehicleTracking.Infrastructure.Feeds;
using VehicleTracking.Infrastructure.Persistence;

namespace VehicleTracking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddPersistence(services, configuration);
        AddFeeds(services, configuration);

        // Bellek tamponu ani çökmede veriyi korumaz; bilinen sınır için bkz. docs/known-limitations.md.
        services.AddSingleton<IFeedBuffer, InMemoryFeedBuffer>();

        return services;
    }

    /// <summary>Veritabanı hazırlık kontrolünü kaydeder.</summary>
    public static IHealthChecksBuilder AddInfrastructureHealthChecks(this IHealthChecksBuilder builder)
    {
        // 3 sn sınır: DB kapalıyken Npgsql bağlantı denemesi varsayılan 15 sn bekler, /health isteği de o kadar takılırdı.
        // Süre aşılırsa kontrol Unhealthy sayılır.
        return builder.AddCheck<DatabaseHealthCheck>("database", tags: ["ready"], timeout: TimeSpan.FromSeconds(3));
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        // Şifre repoda değil: geliştirmede user-secrets'tan, Docker'da ortam değişkeninden gelir.
        string connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres ayarı eksik.");

        // Tek bir NpgsqlDataSource = tek bağlantı havuzu. EF Core (migration, okuma sorguları) ve
        // toplu yazıcı (doğrudan Npgsql) aynı havuzu paylaşır.
        NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString);
        services.AddSingleton(dataSource);

        services.AddDbContext<VehicleTrackingDbContext>(options => options
            .UseNpgsql(dataSource)
            .UseSnakeCaseNamingConvention());

        services.AddSingleton<IPositionWriter, NpgsqlPositionWriter>();
        services.AddScoped<IVehicleHistoryReader, EfVehicleHistoryReader>();   // DbContext scoped olduğu için

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
