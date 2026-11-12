using Microsoft.Extensions.Options;
using VehicleTracking.Application.Common;
using VehicleTracking.Application.Storage;
using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Application.Live;

/// <summary>
/// Bellekte her aracın son konumu (canlı yayın DB'den bağımsız; DB kapalıyken de harita akar).
/// Anahtar araç kimliği: feed'de aynı araç birden çok varlık olarak geliyor, haritada ise tek işaret olmalı.
/// </summary>
/// <remarks>
/// Çekme döngüsü yazar, SignalR ve REST okur (farklı iş parçacıkları) → kilitli.
/// Uygulama açılınca boştur; ilk turda feed'deki son 3 saatin tüm konumlarıyla dolar.
/// </remarks>
public sealed class LiveVehicleState(IOptions<StorageOptions> storageOptions) : ILiveVehicleStore
{
    private static readonly TimeSpan PruneInterval = TimeSpan.FromMinutes(1);

    private readonly TimeSpan _retention = storageOptions.Value.Retention;
    private readonly Lock _lock = new();
    private readonly LatestByKey<VehiclePosition> _vehicles = new(position => position.MeasuredAt);
    private long _version;
    private LiveSnapshot? _cachedSnapshot;
    private DateTimeOffset _lastPrunedAt = DateTimeOffset.MinValue;

    public int Count
    {
        get { lock (_lock) { return _vehicles.Count; } }
    }

    /// <summary>Yeni konumları uygular, değişen araçları döndürür.</summary>
    public LiveUpdate Apply(IReadOnlyList<VehiclePosition> positions, DateTimeOffset now)
    {
        lock (_lock)
        {
            // Aynı turda bir aracın birden çok varlığı gelebilir (bitmiş seferleri); en yeni ölçüm kazanır.
            var upserts = new Dictionary<string, VehiclePosition>();
            foreach (VehiclePosition position in positions)
            {
                if (!_vehicles.TryUpdate(position.VehicleId, position))
                {
                    continue;
                }

                upserts[position.VehicleId] = position;
            }

            IReadOnlyList<string> removals = PruneIfDue(now);

            if (upserts.Count == 0 && removals.Count == 0)
            {
                return new LiveUpdate(_version, [], []);
            }

            _version++;
            return new LiveUpdate(_version, upserts.Values.ToList(), removals);
        }
    }

    // Araç en fazla 1 dk fazla kalır; karşılığında her 3 sn'deki tam tarama gider.
    private IReadOnlyList<string> PruneIfDue(DateTimeOffset now)
    {
        if (now - _lastPrunedAt < PruneInterval)
        {
            return [];
        }

        IReadOnlyList<string> removals = _vehicles.RemoveOlderThan(now - _retention);
        _lastPrunedAt = now;
        return removals;
    }

    public LiveSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            // Sürüm yalnızca gerçek değişiklikte artar; aynı sürümde önbellekteki görüntü geçerlidir.
            if (_cachedSnapshot is not null && _cachedSnapshot.Version == _version)
            {
                return _cachedSnapshot;
            }

            _cachedSnapshot = new LiveSnapshot(_version, Array.AsReadOnly(_vehicles.Values.ToArray()));
            return _cachedSnapshot;
        }
    }
}
