using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Api.Contracts;

/// <summary>Geçmişteki bir nokta.</summary>
public sealed record HistoryPointDto(
    DateTimeOffset MeasuredAt,
    float Latitude,
    float Longitude,
    float? Bearing,
    float? Speed,
    string? RouteId,
    string? TripId,
    string? StopId,
    VehicleStopStatus? Status,
    OccupancyStatus? Occupancy)
{
    public static HistoryPointDto From(VehiclePosition p) => new(
        p.MeasuredAt, p.Latitude, p.Longitude, p.Bearing, p.Speed,
        p.RouteId, p.TripId, p.StopId, p.CurrentStatus, p.Occupancy);
}
