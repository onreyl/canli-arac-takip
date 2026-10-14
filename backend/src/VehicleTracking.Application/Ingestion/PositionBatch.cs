using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Application.Ingestion;

/// <summary>Bir feed'den çıkan, DB'ye yazılmayı bekleyen yeni konumlar.</summary>
/// <param name="Id">Partinin kimliği (Aşama 2'de disk dosyasının adı olacak).</param>
public sealed record PositionBatch(Guid Id, DateTimeOffset? FeedGeneratedAt, IReadOnlyList<VehiclePosition> Positions);
