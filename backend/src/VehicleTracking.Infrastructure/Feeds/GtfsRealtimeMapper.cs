using VehicleTracking.Application.Ingestion;
using VehicleTracking.Domain.Vehicles;
// Protobuf'ta da "VehiclePosition" adlı bir tip var. Karışmasın diye protobuf tiplerini "Gtfs" önekiyle kullanıyoruz:
// bu dosyada VehiclePosition = bizim Domain tipimiz, GtfsVehiclePosition = protobuf'unki.
using FeedEntity = TransitRealtime.FeedEntity;
using FeedMessage = TransitRealtime.FeedMessage;
using GtfsOccupancy = TransitRealtime.VehiclePosition.Types.OccupancyStatus;
using GtfsStopStatus = TransitRealtime.VehiclePosition.Types.VehicleStopStatus;
using GtfsVehiclePosition = TransitRealtime.VehiclePosition;

namespace VehicleTracking.Infrastructure.Feeds;

/// <summary>
/// Protobuf (GTFS-RT) → kendi tiplerimiz. Protobuf'u bilen tek yer burası;
/// kaynak formatı değişirse (ör. JSON) değişecek olan bu sınıf ve EnturFeedSource.
/// </summary>
/// <remarks>
/// proto2'de "optional" alanlar: alt nesne (Vehicle, Position, Trip) gönderilmemişse null,
/// sayı/enum gönderilmemişse varsayılan değer (0) gelir; gerçekten gönderilip gönderilmediğini HasX söyler.
/// String gönderilmemişse "" gelir.
/// </remarks>
internal static class GtfsRealtimeMapper
{
    public static FeedSnapshot ToSnapshot(FeedMessage feed)
    {
        var positions = new List<VehiclePosition>(feed.Entity.Count);
        int skipped = 0;

        foreach (FeedEntity entity in feed.Entity)
        {
            if (TryMap(entity, out VehiclePosition? position))
            {
                positions.Add(position);
            }
            else
            {
                skipped++;
            }
        }

        DateTimeOffset? generatedAt = feed.Header.HasTimestamp
            ? DateTimeOffset.FromUnixTimeSeconds((long)feed.Header.Timestamp)
            : null;

        return new FeedSnapshot(generatedAt, positions, skipped);
    }

    private static bool TryMap(FeedEntity entity, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out VehiclePosition? position)
    {
        position = null;
        GtfsVehiclePosition? v = entity.Vehicle;

        // Konumsuz, araç kimliksiz veya ölçüm zamanı olmayan bir kaydı saklamanın anlamı yok:
        // tekil anahtar (entity_id, measured_at) ve harita için bunlar zorunlu.
        if (v?.Position is null || string.IsNullOrEmpty(v.Vehicle?.Id) || !v.HasTimestamp || string.IsNullOrEmpty(entity.Id))
        {
            return false;
        }

        position = new VehiclePosition(
            EntityId: entity.Id,
            VehicleId: v.Vehicle.Id,
            TripId: NullIfEmpty(v.Trip?.TripId),
            RouteId: NullIfEmpty(v.Trip?.RouteId),   // DeadRun seferlerinde boş geliyor
            Latitude: v.Position.Latitude,
            Longitude: v.Position.Longitude,
            Bearing: v.Position.HasBearing ? v.Position.Bearing : null,
            Speed: v.Position.HasSpeed ? v.Position.Speed : null,
            StopId: NullIfEmpty(v.StopId),
            CurrentStatus: v.HasCurrentStatus ? MapStopStatus(v.CurrentStatus) : null,
            Occupancy: v.HasOccupancyStatus ? MapOccupancy(v.OccupancyStatus) : null,
            MeasuredAt: DateTimeOffset.FromUnixTimeSeconds((long)v.Timestamp));

        return true;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    // Sayısal değere güvenip cast etmek yerine açık eşleme: iki enum'un sırası ileride ayrışırsa sessizce yanlış veri yazmayalım.
    private static VehicleStopStatus? MapStopStatus(GtfsStopStatus status) => status switch
    {
        GtfsStopStatus.IncomingAt => VehicleStopStatus.IncomingAt,
        GtfsStopStatus.StoppedAt => VehicleStopStatus.StoppedAt,
        GtfsStopStatus.InTransitTo => VehicleStopStatus.InTransitTo,
        _ => null,
    };

    private static OccupancyStatus? MapOccupancy(GtfsOccupancy occupancy) => occupancy switch
    {
        GtfsOccupancy.Empty => OccupancyStatus.Empty,
        GtfsOccupancy.ManySeatsAvailable => OccupancyStatus.ManySeatsAvailable,
        GtfsOccupancy.FewSeatsAvailable => OccupancyStatus.FewSeatsAvailable,
        GtfsOccupancy.StandingRoomOnly => OccupancyStatus.StandingRoomOnly,
        GtfsOccupancy.CrushedStandingRoomOnly => OccupancyStatus.CrushedStandingRoomOnly,
        GtfsOccupancy.Full => OccupancyStatus.Full,
        GtfsOccupancy.NotAcceptingPassengers => OccupancyStatus.NotAcceptingPassengers,
        GtfsOccupancy.NoDataAvailable => OccupancyStatus.NoDataAvailable,
        GtfsOccupancy.NotBoardable => OccupancyStatus.NotBoardable,
        _ => null,
    };
}
