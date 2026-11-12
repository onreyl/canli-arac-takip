using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Application.Live;

/// <summary>Tüm araçların o anki görüntüsü. Version: bu görüntünün hangi değişiklikten sonra alındığı.</summary>
public sealed record LiveSnapshot(long Version, IReadOnlyList<VehiclePosition> Vehicles);
