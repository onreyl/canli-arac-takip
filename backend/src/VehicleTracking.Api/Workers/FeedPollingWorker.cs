using System.Diagnostics;
using Microsoft.Extensions.Options;
using VehicleTracking.Application.Ingestion;

namespace VehicleTracking.Api.Workers;

/// <summary>
/// Uygulama açılınca kendiliğinden başlar, her PollInterval'da (3 sn) feed'i çeker ve yeni konumları tampona koyar.
/// Hiçbir hata döngüyü durdurmaz.
/// </summary>
internal sealed partial class FeedPollingWorker(
    IFeedSource feedSource,
    FeedIngestor ingestor,
    IFeedBuffer buffer,
    IngestionMetrics metrics,
    TimeProvider timeProvider,
    IOptions<IngestionOptions> options,
    ILogger<FeedPollingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // PeriodicTimer: bir tur aralıktan uzun sürerse turlar birikmez, aynı anda iki istek çalışmaz.
        // Gözlemde en yavaş okuma ~1,7 sn; 3 sn'lik aralığa sığıyor.
        using var timer = new PeriodicTimer(options.Value.PollInterval);
        try
        {
            do
            {
                await PollOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Uygulama kapanıyor.
        }
    }

    private async Task PollOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            long start = Stopwatch.GetTimestamp();
            FeedSnapshot snapshot;
            try
            {
                snapshot = await feedSource.FetchAsync(stoppingToken);
            }
            catch (FeedUnavailableException ex)
            {
                // Kaçınılmaz kayıp: GTFS-RT geçmiş tutmaz, okuyamadığımız an geri gelmez. Sayıp devam ediyoruz.
                metrics.RecordFetchFailure();
                LogFetchFailed(ex.Message);
                return;
            }

            metrics.RecordFetchSuccess(Stopwatch.GetElapsedTime(start));

            IngestResult result = ingestor.Process(snapshot, timeProvider.GetUtcNow());
            if (result.IsStale || result.NewPositions.Count == 0)
            {
                return;
            }

            await buffer.EnqueueAsync(
                new PositionBatch(Guid.NewGuid(), snapshot.GeneratedAt, result.NewPositions),
                stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Beklenmeyen bir hata (bug) bile döngüyü öldürmesin: .NET'te BackgroundService'ten kaçan hata
            // varsayılan olarak tüm uygulamayı durdurur.
            LogUnexpectedError(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Feed okunamadı: {Error}")]
    private partial void LogFetchFailed(string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Çekme turunda beklenmeyen hata")]
    private partial void LogUnexpectedError(Exception ex);
}
