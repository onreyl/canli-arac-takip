using Microsoft.Extensions.Diagnostics.HealthChecks;
using VehicleTracking.Application.Ingestion;

namespace VehicleTracking.Api.Health;

internal sealed class IngestionHealthCheck(
    IngestionMetrics metrics,
    IFeedBuffer buffer,
    TimeProvider timeProvider) : IHealthCheck
{
    // 3 saniyelik aralıkta yaklaşık 10 turun kaçması kaynak erişiminde sorun olduğunu gösterir.
    private const int FetchTimeoutSeconds = 30;
    // Yaklaşık 10 dakikalık birikme, DB yazımının yetişemediğine işaret eder.
    private const int MaxPendingBatches = 100;

    private readonly DateTimeOffset _startedAt = timeProvider.GetUtcNow();

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IngestionMetricsSnapshot snapshot = metrics.GetSnapshot();
        int pendingBatches = buffer.Count;
        DateTimeOffset now = timeProvider.GetUtcNow();
        TimeSpan fetchTimeout = TimeSpan.FromSeconds(FetchTimeoutSeconds);
        Dictionary<string, object> data = new()
        {
            ["pendingBatches"] = pendingBatches,
            ["droppedBatchCount"] = snapshot.DroppedBatchCount,
        };
        if (snapshot.LastSuccessfulFetchAt is { } fetchedAt)
        {
            data["lastSuccessfulFetchAt"] = fetchedAt;
        }
        List<string> reasons = [];

        if (snapshot.LastSuccessfulFetchAt is not { } lastSuccessfulFetchAt)
        {
            if (now - _startedAt > fetchTimeout)
            {
                reasons.Add($"Uygulama {FetchTimeoutSeconds} saniyeden uzun süredir çalışıyor, henüz başarılı okuma yok.");
            }
        }
        else if (now - lastSuccessfulFetchAt > fetchTimeout)
        {
            reasons.Add($"Son başarılı okuma {FetchTimeoutSeconds} saniyeden eski.");
        }

        if (pendingBatches > MaxPendingBatches)
        {
            reasons.Add($"Tamponda {MaxPendingBatches} partiden fazla bekliyor.");
        }

        if (snapshot.DroppedBatchCount > 0)
        {
            reasons.Add("Parti kaybı yaşandı.");
        }

        // Kaynak veya DB sorunu sürecin öldüğü anlamına gelmez; çekme döngüsü kendini toparlar.
        // Unhealthy dönmek Docker'ın konteyneri gereksiz yere yeniden başlatmasına yol açabilir.
        return Task.FromResult(reasons.Count > 0
            ? HealthCheckResult.Degraded(string.Join(" ", reasons), data: data)
            : HealthCheckResult.Healthy(data: data));
    }
}
