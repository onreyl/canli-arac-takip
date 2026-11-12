using System.Diagnostics;
using Microsoft.Extensions.Logging;
using VehicleTracking.Application.Live;

namespace VehicleTracking.Application.Ingestion;

/// <summary>Bir feed turunda yeni konumları canlı yayına ve kalıcılık tamponuna iletir.</summary>
public sealed partial class FeedIngestionCycle(
    IFeedSource feedSource,
    IFeedIngestor ingestor,
    IFeedBuffer buffer,
    ILiveVehicleStore liveState,
    ILiveUpdatePublisher publisher,
    IIngestionMetrics metrics,
    TimeProvider timeProvider,
    ILogger<FeedIngestionCycle> logger) : IFeedIngestionCycle
{
    /// <summary>Feed'i bir kez çeker, ayıklar, yayınlar ve tampona yazar.</summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        long start = Stopwatch.GetTimestamp();
        FeedSnapshot snapshot;
        try
        {
            snapshot = await feedSource.FetchAsync(cancellationToken);
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
        await buffer.EnqueueAsync(new PositionBatch(result.NewPositions), cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Feed okunamadı: {Error}")]
    private partial void LogFetchFailed(string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Canlı yayın güncellemesi başarısız (kalıcılık etkilenmedi)")]
    private partial void LogLivePublishFailed(Exception ex);
}
