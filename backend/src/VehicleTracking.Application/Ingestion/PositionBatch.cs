using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Application.Ingestion;

/// <summary>Bir feed'den çıkan, DB'ye yazılmayı bekleyen yeni konumlar.</summary>
public sealed record PositionBatch(IReadOnlyList<VehiclePosition> Positions);
