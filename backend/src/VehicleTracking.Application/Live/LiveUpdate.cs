using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Application.Live;

/// <summary>Bir turdaki değişiklik: güncellenen araçlar ve artık izlenmeyenler.</summary>
public sealed record LiveUpdate(long Version, IReadOnlyList<VehiclePosition> Upserts, IReadOnlyList<string> Removals)
{
    public bool IsEmpty => Upserts.Count == 0 && Removals.Count == 0;
}
