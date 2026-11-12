namespace VehicleTracking.Api.Realtime;

/// <summary>Bağlanınca (ve her yeniden bağlanmada) ilk gönderilen tam görüntü. İstemci elindekini bununla değiştirir.</summary>
public sealed record LiveSnapshotMessage(long Version, IReadOnlyList<LiveVehicleDto> Vehicles);
