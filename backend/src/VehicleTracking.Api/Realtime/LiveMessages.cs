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

/// <summary>Bağlanınca (ve her yeniden bağlanmada) ilk gönderilen tam görüntü. İstemci elindekini bununla değiştirir.</summary>
public sealed record LiveSnapshotMessage(long Version, IReadOnlyList<LiveVehicleDto> Vehicles);

/// <summary>Sonraki değişiklikler. İstemci Upserts'i ekler/günceller, Removals'ı siler.</summary>
public sealed record LiveUpdateMessage(long Version, IReadOnlyList<LiveVehicleDto> Upserts, IReadOnlyList<string> Removals);

/// <summary>SignalR'da istemci tarafındaki metot adları (frontend'de connection.on(...) ile dinlenecek).</summary>
public static class LiveMethods
{
    public const string Snapshot = "snapshot";
    public const string Update = "update";
}
