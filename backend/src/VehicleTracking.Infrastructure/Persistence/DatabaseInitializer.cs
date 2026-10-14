using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using VehicleTracking.Application.Storage;

namespace VehicleTracking.Infrastructure.Persistence;

/// <summary>
/// Uygulama açılırken veritabanını hazırlar: migration'ları uygular, chunk aralığını ve saklama süresini
/// yapılandırmaya göre ayarlar. "docker compose up" tek komutla çalışsın diye elle migration gerekmez.
/// </summary>
/// <remarks>
/// IHostedService.StartAsync tamamlanmadan sonraki hosted service'ler (ör. feed çekici) başlamaz,
/// böylece tablo hazır olmadan yazma denenmez.
/// Bilinen sınırlama: DB açılışta kapalıysa uygulama DB gelene kadar burada bekler (çekme de başlamaz).
/// Gün 6'da disk tamponu gelince bunu yeniden değerlendireceğiz.
/// </remarks>
internal sealed partial class DatabaseInitializer(
    IServiceScopeFactory scopeFactory,
    IOptions<StorageOptions> options,
    ILogger<DatabaseInitializer> logger) : IHostedService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        StorageOptions storage = options.Value;

        // DB henüz hazır değilse (ör. konteyner yeni açılıyor) çökmek yerine bekleyip tekrar dene.
        while (true)
        {
            try
            {
                await InitializeAsync(storage, cancellationToken);
                LogInitialized(storage.Retention, storage.ChunkInterval, storage.RetentionCheckInterval);
                return;
            }
            catch (NpgsqlException ex) when (!cancellationToken.IsCancellationRequested)
            {
                LogDatabaseUnavailable(ex.Message, RetryDelay);
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task InitializeAsync(StorageOptions storage, CancellationToken cancellationToken)
    {
        // DbContext scoped bir servis; hosted service ise singleton. Bu yüzden kendi scope'umuzu açıyoruz.
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VehicleTrackingDbContext>();

        await db.Database.MigrateAsync(cancellationToken);

        // Sadece bundan sonra açılacak chunk'ları etkiler; mevcutlar kendi aralığıyla kalır.
        await db.Database.ExecuteSqlAsync(
            $"SELECT set_chunk_time_interval('vehicle_positions', {storage.ChunkInterval})",
            cancellationToken);

        // Policy'yi her açılışta config'teki süreyle yeniden kur: süre değiştiyse yeni migration gerekmez.
        await db.Database.ExecuteSqlAsync(
            $"SELECT remove_retention_policy('vehicle_positions', if_exists => true)",
            cancellationToken);
        await db.Database.ExecuteSqlAsync(
            $"SELECT add_retention_policy('vehicle_positions', drop_after => {storage.Retention}, schedule_interval => {storage.RetentionCheckInterval})",
            cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Veritabanı hazır. Saklama: {Retention}, chunk: {ChunkInterval}, silme kontrolü: {CheckInterval}")]
    private partial void LogInitialized(TimeSpan retention, TimeSpan chunkInterval, TimeSpan checkInterval);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Veritabanına ulaşılamadı ({Error}). {Delay} sonra tekrar denenecek.")]
    private partial void LogDatabaseUnavailable(string error, TimeSpan delay);
}
