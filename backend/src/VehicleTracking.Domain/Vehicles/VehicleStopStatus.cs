namespace VehicleTracking.Domain.Vehicles;

/// <summary>
/// Aracın durağa göre durumu. GTFS-RT'deki karşılığıyla aynı anlamda, ama protobuf tipine
/// bağımlı olmamak için kendi enum'umuz (protobuf tipleri Infrastructure dışına çıkmaz).
/// </summary>
public enum VehicleStopStatus
{
    IncomingAt,
    StoppedAt,
    InTransitTo,
}
