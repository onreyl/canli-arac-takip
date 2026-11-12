using System.Text.Json;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;
using VehicleTracking.Api.Realtime;
using VehicleTracking.Application.Live;

namespace VehicleTracking.Benchmarks;

/// <summary>Bir turun istemci sayısıyla büyüyen birleştirme ve JSON tahsis maliyetini ölçer.</summary>
[MemoryDiagnoser]
public class LiveBroadcastBenchmarks
{
    private LiveVehicleState _state = null!;
    private LiveUpdate _update = null!;
    private LiveClientFeed[] _clients = null!;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Güncellemeyi alacak istemci sayısı.</summary>
    [Params(1, 10, 50)]
    public int ClientCount { get; set; }

    /// <summary>İlk görüntüleri ölçüm dışında hazırlar.</summary>
    [GlobalSetup]
    public async Task Setup()
    {
        var vehicles = await BenchmarkData.LoadVehiclesAsync();
        _state = BenchmarkData.CreateState(vehicles);
        _clients = Enumerable.Range(0, ClientCount).Select(_ => new LiveClientFeed()).ToArray();
        foreach (var client in _clients)
        {
            client.TakePending(_state, out _);
        }

        var round = BenchmarkData.CreateRound(vehicles);
        _update = _state.Apply(round, round[0].MeasuredAt);
    }

    /// <summary>Güncellemeyi her istemci için UTF-8 JSON'a dönüştürür.</summary>
    [Benchmark]
    public int Broadcast()
    {
        int byteCount = 0;
        foreach (var client in _clients)
        {
            client.Merge(_update);
            object message = client.TakePending(_state, out _);
            byteCount += JsonSerializer.SerializeToUtf8Bytes(message, message.GetType(), _json).Length;
        }

        return byteCount;
    }

    /// <summary>İstemci kaynaklarını bırakır.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }
    }
}
