namespace VehicleTracking.Domain.Vehicles;

/// <summary>
/// Bir aracın belirli bir anda ölçülmüş konumu. Bir kez ölçülür, sonra değişmez.
/// </summary>
/// <remarks>
/// Kimlik (tekil anahtar) <see cref="EntityId"/> + <see cref="MeasuredAt"/> çiftidir.
/// <see cref="VehicleId"/> tekil değildir: Entur aynı aracı gün içindeki her seferi için ayrı bir
/// varlık olarak gönderir (gözlem: bir araç 42 kez görüldü). Bkz. docs/observations.md.
/// </remarks>
public sealed record VehiclePosition(
    string EntityId,
    string VehicleId,
    string? TripId,
    string? RouteId,
    // float: kaynak (GTFS-RT) koordinatları float gönderiyor; double'a çevirmek bilgi eklemez, sadece
    // dönüşüm gürültüsü (68.73694610595703) ve satır başına fazladan 8 byte ekler. Hassasiyet ~1 m.
    float Latitude,
    float Longitude,
    float? Bearing,
    float? Speed,
    string? StopId,
    VehicleStopStatus? CurrentStatus,
    OccupancyStatus? Occupancy,
    DateTimeOffset MeasuredAt)
{
    /// <summary>
    /// Yolcu taşımayan boş sefer (garaja dönüş vb.). Entur'da hat bilgisi gelmez,
    /// sefer kimliğinde ":DeadRun:" geçer, ör. "BOR:DeadRun:260907141040052_0".
    /// </summary>
    public bool IsDeadRun => TripId?.Contains(":DeadRun:", StringComparison.Ordinal) == true;

    /// <summary>
    /// Ölçüm saklama penceresinin içinde mi? Feed'deki varlıkların yarısından fazlası bitmiş seferlerin
    /// saatler önceki son konumu; bunları yazmıyoruz.
    /// </summary>
    public bool IsWithinRetention(DateTimeOffset now, TimeSpan retention) => MeasuredAt >= now - retention;
}
