using System.Diagnostics;
using Microsoft.Extensions.Options;
using VehicleTracking.Application.Ingestion;
using VehicleTracking.Application.Live;

namespace VehicleTracking.Api.Workers;

/// <summary>
/// Uygulama açılınca kendiliğinden başlar, her PollInterval'da (3 sn) feed'i çeker ve yeni konumları tampona koyar.
/// Hiçbir hata döngüyü durdurmaz.
/// </summary>
internal sealed partial class FeedPollingWorker(
    IFeedSource feedSource,
    FeedIngestor ingestor,
    IFeedBuffer buffer,
    LiveVehicleState liveState,
    ILiveUpdatePublisher publisher,
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

            DateTimeOffset now = timeProvider.GetUtcNow();
            IngestResult result = ingestor.Process(snapshot, now);
            if (result.IsStale || result.NewPositions.Count == 0)
            {
                return;
            }

            // Canlı kol: DB'den bağımsız, önce o. Publish beklemez; yavaş istemci bu döngüyü yavaşlatamaz.
            // Ayrı try: canlı yayındaki bir hata asla aşağıdaki kalıcılık kolunu (tampona yazma) engellememeli.
            try
            {
                LiveUpdate update = liveState.Apply(result.NewPositions, now);
                if (!update.IsEmpty)
                {
                    publisher.Publish(update);
                }
            }
            catch (Exception ex)
            {
                LogLivePublishFailed(ex);
            }

            // Kalıcılık kolu: tampona; yazıcı işçisi DB'ye yazar.
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

    [LoggerMessage(Level = LogLevel.Error, Message = "Canlı yayın güncellemesi başarısız (kalıcılık etkilenmedi)")]
    private partial void LogLivePublishFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Çekme turunda beklenmeyen hata")]
    private partial void LogUnexpectedError(Exception ex);
}
