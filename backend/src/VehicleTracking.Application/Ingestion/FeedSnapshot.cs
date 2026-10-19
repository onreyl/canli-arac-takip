using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Application.Ingestion;

/// <summary>Kaynaktan bir kez çekilen tam liste.</summary>
/// <param name="GeneratedAt">
/// Kaynağın feed'i ürettiği an. Aynıysa içerik de aynı; geri gidebilir (Entur'da turların ~%15'i).
/// Bu yüzden "zaten işledim / eski feed" kararı buna göre verilir.
/// </param>
/// <param name="Positions">Geçerli konumlar (eksik alanlı varlıklar ayıklanmış).</param>
public sealed record FeedSnapshot(DateTimeOffset? GeneratedAt, IReadOnlyList<VehiclePosition> Positions);
