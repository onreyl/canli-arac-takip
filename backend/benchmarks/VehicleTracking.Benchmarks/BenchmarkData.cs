using Microsoft.Extensions.Options;
using VehicleTracking.Application.Live;
using VehicleTracking.Application.Storage;
using VehicleTracking.Domain.Vehicles;
using VehicleTracking.Infrastructure.Feeds;

namespace VehicleTracking.Benchmarks;

// Aynı örnek ve sabit iş yükü, sonraki iyileştirmelerle karşılaştırmayı tekrarlanabilir tutar.
internal static class BenchmarkData
{
    internal const int VehicleCount = 3_300;
    internal const int UpdateCount = 1_500;
    internal const string FeedUrl = "https://api.entur.io/realtime/v1/gtfs-rt/vehicle-positions";
    internal static string DataPath => Path.Combine(AppContext.BaseDirectory, "data", "vehicle-positions.pb");
    internal static string MissingDataMessage =>
        "Entur örnek verisi bulunamadı: " + DataPath + Environment.NewLine +
        "Repo kökünde indirin ve yeniden derleyin: curl --compressed -H \"ET-Client-Name: sirket-canli-arac-takip-benchmarks\" " +
        FeedUrl + " -o backend/benchmarks/VehicleTracking.Benchmarks/data/vehicle-positions.pb";

    internal static byte[] ReadBytes()
    {
        if (!File.Exists(DataPath))
        {
            throw new FileNotFoundException(MissingDataMessage, DataPath);
        }

        return File.ReadAllBytes(DataPath);
    }

    internal static EnturFeedSource CreateSource(HttpClient http) => new(http, Options.Create(new EnturOptions
    {
        VehiclePositionsUrl = new Uri(FeedUrl),
        ClientName = "canli-arac-takip-benchmarks"
    }));

    internal static async Task<VehiclePosition[]> LoadVehiclesAsync()
    {
        using var http = new HttpClient(new FeedMessageHandler(ReadBytes()));
        var snapshot = await CreateSource(http).FetchAsync(CancellationToken.None);
        var latest = snapshot.Positions
            .GroupBy(position => position.VehicleId)
            .Select(group => group.MaxBy(position => position.MeasuredAt)!)
            .OrderBy(position => position.VehicleId, StringComparer.Ordinal)
            .ToArray();
        if (latest.Length == 0)
        {
            throw new InvalidDataException("Entur örnek verisinde geçerli araç konumu yok.");
        }

        var now = latest.Max(position => position.MeasuredAt);
        return Enumerable.Range(0, VehicleCount).Select(index =>
        {
            var position = latest[index % latest.Length];
            return position with
            {
                VehicleId = index < latest.Length ? position.VehicleId : $"benchmark-vehicle-{index}",
                EntityId = index < latest.Length ? position.EntityId : $"benchmark-entity-{index}",
                MeasuredAt = now
            };
        }).ToArray();
    }

    internal static LiveVehicleState CreateState(VehiclePosition[] vehicles)
    {
        var state = new LiveVehicleState(Options.Create(new StorageOptions()));
        state.Apply(vehicles, vehicles[0].MeasuredAt);
        return state;
    }

    internal static VehiclePosition[] CreateRound(VehiclePosition[] vehicles) => vehicles
        .Take(UpdateCount)
        .Select(position => position with { MeasuredAt = position.MeasuredAt.AddSeconds(3) })
        .ToArray();
}
