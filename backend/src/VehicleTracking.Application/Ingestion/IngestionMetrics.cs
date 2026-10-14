namespace VehicleTracking.Application.Ingestion;

/// <summary>Sistem durumu ekranı için sayaçların anlık kopyası.</summary>
public sealed record IngestionMetricsSnapshot(
    DateTimeOffset? LastSuccessfulFetchAt,
    TimeSpan? LastFetchDuration,
    long FailedFetchCount,
    long FailedWriteCount,
    long WrittenLastHour,
    long DroppedBatchCount);

/// <summary>
/// Çekme ve yazma sayaçları. Çekme ve yazma ayrı iş parçacıklarında çalıştığı (ve API de okuduğu) için kilitli.
/// Sayaçlar bellekte: uygulama yeniden başlayınca sıfırlanır.
/// </summary>
public sealed class IngestionMetrics(TimeProvider timeProvider)
{
    private readonly Lock _lock = new();
    private readonly Queue<(DateTimeOffset At, int Count)> _writes = new();

    private DateTimeOffset? _lastSuccessfulFetchAt;
    private TimeSpan? _lastFetchDuration;
    private long _failedFetchCount;
    private long _failedWriteCount;
    private long _droppedBatchCount;
    private long _writtenLastHour;

    public void RecordFetchSuccess(TimeSpan duration)
    {
        lock (_lock)
        {
            _lastSuccessfulFetchAt = timeProvider.GetUtcNow();
            _lastFetchDuration = duration;
        }
    }

    public void RecordFetchFailure() => Interlocked.Increment(ref _failedFetchCount);

    public void RecordWriteFailure() => Interlocked.Increment(ref _failedWriteCount);

    public void RecordDroppedBatch() => Interlocked.Increment(ref _droppedBatchCount);

    public void RecordWritten(int count)
    {
        lock (_lock)
        {
            _writes.Enqueue((timeProvider.GetUtcNow(), count));
            _writtenLastHour += count;
            PruneWrites();
        }
    }

    public IngestionMetricsSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            PruneWrites();
            return new IngestionMetricsSnapshot(
                _lastSuccessfulFetchAt,
                _lastFetchDuration,
                Interlocked.Read(ref _failedFetchCount),
                Interlocked.Read(ref _failedWriteCount),
                _writtenLastHour,
                Interlocked.Read(ref _droppedBatchCount));
        }
    }

    // Kayan pencere: 1 saatten eski yazma kayıtlarını düş.
    private void PruneWrites()
    {
        DateTimeOffset cutoff = timeProvider.GetUtcNow().AddHours(-1);
        while (_writes.TryPeek(out var oldest) && oldest.At < cutoff)
        {
            _writtenLastHour -= _writes.Dequeue().Count;
        }
    }
}
