using Microsoft.Extensions.Options;
using VehicleTracking.Application.Storage;
using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.UnitTests;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    public static IOptions<StorageOptions> StorageOptions =>
        Options.Create(new StorageOptions { Retention = TimeSpan.FromHours(3) });

    public static VehiclePosition Position(
        string entityId,
        string vehicleId,
        DateTimeOffset measuredAt,
        string? tripId = "BRA:ServiceJourney:6660_1022",
        string? routeId = "BRA:Line:6660") =>
        new(entityId, vehicleId, tripId, routeId, 59.91f, 10.75f,
            null, null, null, null, null, measuredAt);
}
