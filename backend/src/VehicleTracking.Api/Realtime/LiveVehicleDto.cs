using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Api.Realtime;

/// <summary>İstemciye giden araç bilgisi. Domain tipini doğrudan göndermiyoruz: dış sözleşme ayrı durmalı.</summary>
public sealed record LiveVehicleDto(
    string VehicleId,
    string? RouteId,
    string? TripId,
    float Latitude,
    float Longitude,
    float? Bearing,
    VehicleStopStatus? Status,
    OccupancyStatus? Occupancy,
    DateTimeOffset MeasuredAt,
    bool IsDeadRun)
{
    public static LiveVehicleDto From(VehiclePosition p) => new(
        p.VehicleId, p.RouteId, p.TripId, p.Latitude, p.Longitude, p.Bearing,
        p.CurrentStatus, p.Occupancy, p.MeasuredAt, p.IsDeadRun);
}
