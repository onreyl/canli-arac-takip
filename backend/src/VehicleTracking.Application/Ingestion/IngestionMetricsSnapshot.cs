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
