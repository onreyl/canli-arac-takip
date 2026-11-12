using VehicleTracking.Application.Live;

namespace VehicleTracking.Api.Realtime;

/// <summary>Bağlanınca (ve her yeniden bağlanmada) ilk gönderilen tam görüntü. İstemci elindekini bununla değiştirir.</summary>
public sealed record LiveSnapshotMessage(long Version, IReadOnlyList<LiveVehicleDto> Vehicles)
{
    /// <summary>Canlı durum görüntüsünü istemci mesajına dönüştürür.</summary>
    public static LiveSnapshotMessage From(LiveSnapshot snapshot) => new(
        snapshot.Version, snapshot.Vehicles.Select(LiveVehicleDto.From).ToList());
}
