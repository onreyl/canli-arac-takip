using VehicleTracking.Application.Live;

namespace VehicleTracking.Api.Realtime;

/// <summary>İstemciler arasında paylaşılan, DTO'ya dönüştürülmüş değişiklikler.</summary>
internal sealed record LiveDtoUpdate(
    long Version,
    IReadOnlyList<LiveVehicleDto> Upserts,
    IReadOnlyList<string> Removals)
{
    public static LiveDtoUpdate From(LiveUpdate update) => new(
        update.Version, update.Upserts.Select(LiveVehicleDto.From).ToArray(), update.Removals);
}
