using BenchmarkDotNet.Attributes;
using VehicleTracking.Application.Ingestion;
using VehicleTracking.Infrastructure.Feeds;

namespace VehicleTracking.Benchmarks;

/// <summary>Ağ olmadan çekme, protobuf çözme ve eşleme maliyetini; özellikle LOH tahsislerini ölçer.</summary>
[MemoryDiagnoser]
public class FeedFetchBenchmarks
{
    private HttpClient _http = null!;
    private EnturFeedSource _source = null!;

    /// <summary>Örnek baytları ölçüm dışında yükler.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _http = new HttpClient(new FeedMessageHandler(BenchmarkData.ReadBytes()));
        _source = BenchmarkData.CreateSource(_http);
    }

    /// <summary>Üretimdeki uçtan uca çekme yolunu çalıştırır.</summary>
    [Benchmark]
    public Task<FeedSnapshot> FetchAsync() => _source.FetchAsync(CancellationToken.None);

    /// <summary>HTTP kaynaklarını bırakır.</summary>
    [GlobalCleanup]
    public void Cleanup() => _http.Dispose();
}
