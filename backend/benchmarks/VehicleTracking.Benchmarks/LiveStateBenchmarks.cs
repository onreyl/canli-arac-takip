using BenchmarkDotNet.Attributes;
using VehicleTracking.Application.Live;
using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Benchmarks;

/// <summary>Canlı durumdaki tur taramasını ve yeni istemcilerin görüntü alma maliyetini ölçer.</summary>
[MemoryDiagnoser]
public class LiveStateBenchmarks
{
    private VehiclePosition[] _vehicles = null!;
    private VehiclePosition[] _round = null!;
    private LiveVehicleState _state = null!;

    /// <summary>Ardışık görüntü isteği sayısı.</summary>
    [Params(1, 10)]
    public int SnapshotCount { get; set; }

    /// <summary>Gerçek feed'den sabit büyüklükte iş yükü hazırlar.</summary>
    [GlobalSetup]
    public async Task Setup()
    {
        _vehicles = await BenchmarkData.LoadVehiclesAsync();
        _round = BenchmarkData.CreateRound(_vehicles);
        _state = BenchmarkData.CreateState(_vehicles);
    }

    // Aynı ölçümler tekrar uygulanırsa üretim kodu onları atlar; her iterasyon gerçek bir güncelleme olmalı.
    // IterationSetup, BenchmarkDotNet'in bu hedefi iterasyon başına tek çağrı çalıştırmasını sağlar.
    /// <summary>Uygulama ölçümünden önce başlangıç durumunu yeniler.</summary>
    [IterationSetup(Target = nameof(Apply))]
    public void ResetState() => _state = BenchmarkData.CreateState(_vehicles);

    /// <summary>1.500 yeni konumun uygulanmasını ölçer.</summary>
    [Benchmark]
    public LiveUpdate Apply() => _state.Apply(_round, _round[0].MeasuredAt);

    /// <summary>Ardışık tam görüntü isteklerini ölçer.</summary>
    [Benchmark]
    public long GetSnapshot()
    {
        long count = 0;
        for (int i = 0; i < SnapshotCount; i++)
        {
            count += _state.GetSnapshot().Vehicles.Count;
        }

        return count;
    }
}
