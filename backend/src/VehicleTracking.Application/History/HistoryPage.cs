using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Application.History;

/// <summary>Bir sayfa geçmiş. Next null ise son sayfa.</summary>
public sealed record HistoryPage(IReadOnlyList<VehiclePosition> Items, HistoryCursor? Next);
