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
    private readonly TimeSpan _retention = storageOptions.Value.Retention;
    private readonly Lock _lock = new();
    private readonly LatestByKey<VehiclePosition> _vehicles = new(position => position.MeasuredAt);
    private long _version;

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

            // Son ölçümü saklama penceresinden çıkan araçları bırak; istemciler de haritadan kaldırsın.
            IReadOnlyList<string> removals = _vehicles.RemoveOlderThan(now - _retention);

            if (upserts.Count == 0 && removals.Count == 0)
            {
                return new LiveUpdate(_version, [], []);
            }

            _version++;
            return new LiveUpdate(_version, upserts.Values.ToList(), removals);
        }
    }

    public LiveSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            return new LiveSnapshot(_version, _vehicles.Values.ToList());
        }
    }
}
