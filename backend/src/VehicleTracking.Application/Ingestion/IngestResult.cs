using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Application.Ingestion;

/// <summary>Bir feed'in işlenme sonucu.</summary>
/// <param name="IsStale">Feed bir öncekinden yeni değil (aynı ya da geri gitmiş); hiç işlenmedi.</param>
/// <param name="NewPositions">Daha önce görülmemiş ve saklama penceresi içindeki konumlar.</param>
public sealed record IngestResult(bool IsStale, IReadOnlyList<VehiclePosition> NewPositions)
{
    public static readonly IngestResult Stale = new(true, []);
}
