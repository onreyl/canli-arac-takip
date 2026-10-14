namespace VehicleTracking.Domain.Vehicles;

/// <summary>
/// Doluluk durumu. GTFS-RT'deki karşılığıyla aynı anlamda, kendi enum'umuz.
/// </summary>
public enum OccupancyStatus
{
    Empty,
    ManySeatsAvailable,
    FewSeatsAvailable,
    StandingRoomOnly,
    CrushedStandingRoomOnly,
    Full,
    NotAcceptingPassengers,
    NoDataAvailable,
    NotBoardable,
}
