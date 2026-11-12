namespace VehicleTracking.Api.Realtime;

/// <summary>Sonraki değişiklikler. İstemci Upserts'i ekler/günceller, Removals'ı siler.</summary>
public sealed record LiveUpdateMessage(long Version, IReadOnlyList<LiveVehicleDto> Upserts, IReadOnlyList<string> Removals);
