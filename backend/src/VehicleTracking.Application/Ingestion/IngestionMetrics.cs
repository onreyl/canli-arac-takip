namespace VehicleTracking.Application.Ingestion;

/// <summary>Sistem durumu ekranı için sayaçların anlık kopyası.</summary>
public sealed record IngestionMetricsSnapshot(
    DateTimeOffset? LastSuccessfulFetchAt,
    TimeSpan? LastFetchDuration,
    long FailedFetchCount,
    long FailedWriteCount,
    long WrittenLastHour,
    long DroppedBatchCount,
    TimeSpan? LastWriteDuration,
    TimeSpan? MaxWriteDurationLastHour);

/// <summary>
/// Çekme ve yazma sayaçları. Çekme ve yazma ayrı iş parçacıklarında çalıştığı (ve API de okuduğu) için kilitli.
/// Sayaçlar bellekte: uygulama yeniden başlayınca sıfırlanır.
/// </summary>
public sealed class IngestionMetrics(TimeProvider timeProvider)
{
    private readonly Lock _lock = new();
    private readonly Queue<(DateTimeOffset At, int Count, TimeSpan Duration)> _writes = new();

    private DateTimeOffset? _lastSuccessfulFetchAt;
    private TimeSpan? _lastFetchDuration;
    private long _failedFetchCount;
    private long _failedWriteCount;
    private long _droppedBatchCount;
    private long _writtenLastHour;
    private TimeSpan? _lastWriteDuration;

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

    /// <summary>Başarılı yazmanın kayıt sayısını ve süresini kaydeder.</summary>
    public void RecordWritten(int count, TimeSpan duration)
    {
        lock (_lock)
        {
            _writes.Enqueue((timeProvider.GetUtcNow(), count, duration));
            _lastWriteDuration = duration;
            _writtenLastHour += count;
            PruneWrites();
        }
    }

    public IngestionMetricsSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            PruneWrites();
            // Pencerede yaklaşık en fazla 1.200 kayıt olduğundan en uzun süre için kuyruğu taramak yeterli.
            TimeSpan? maxWriteDuration = null;
            foreach (var write in _writes)
            {
                if (maxWriteDuration is null || write.Duration > maxWriteDuration.Value)
                {
                    maxWriteDuration = write.Duration;
                }
            }

            return new IngestionMetricsSnapshot(
                _lastSuccessfulFetchAt,
                _lastFetchDuration,
                Interlocked.Read(ref _failedFetchCount),
                Interlocked.Read(ref _failedWriteCount),
                _writtenLastHour,
                Interlocked.Read(ref _droppedBatchCount),
                _lastWriteDuration,
                maxWriteDuration);
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
