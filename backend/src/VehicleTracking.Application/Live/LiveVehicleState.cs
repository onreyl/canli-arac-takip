using Microsoft.Extensions.Options;
using VehicleTracking.Application.Storage;
using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Application.Live;

/// <summary>Tüm araçların o anki görüntüsü. Version: bu görüntünün hangi değişiklikten sonra alındığı.</summary>
public sealed record LiveSnapshot(long Version, IReadOnlyList<VehiclePosition> Vehicles);

/// <summary>Bir turdaki değişiklik: güncellenen araçlar ve artık izlenmeyenler.</summary>
public sealed record LiveUpdate(long Version, IReadOnlyList<VehiclePosition> Upserts, IReadOnlyList<string> Removals)
{
    public bool IsEmpty => Upserts.Count == 0 && Removals.Count == 0;
}

/// <summary>
/// Bellekte her aracın son konumu (Karar 6: canlı yayın DB'den bağımsız; DB kapalıyken de harita akar).
/// Anahtar araç kimliği: feed'de aynı araç birden çok varlık olarak geliyor, haritada ise tek işaret olmalı (Karar 2).
/// </summary>
/// <remarks>
/// Çekme döngüsü yazar, SignalR ve REST okur (farklı iş parçacıkları) → kilitli.
/// Uygulama açılınca boştur; ilk turda feed'deki son 3 saatin tüm konumlarıyla dolar.
/// </remarks>
public sealed class LiveVehicleState(IOptions<StorageOptions> storageOptions)
{
    private readonly TimeSpan _retention = storageOptions.Value.Retention;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, VehiclePosition> _vehicles = new();
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
                if (_vehicles.TryGetValue(position.VehicleId, out VehiclePosition? current)
                    && position.MeasuredAt <= current.MeasuredAt)
                {
                    continue;
                }

                _vehicles[position.VehicleId] = position;
                upserts[position.VehicleId] = position;
            }

            // Son ölçümü saklama penceresinden çıkan araçları bırak; istemciler de haritadan kaldırsın.
            List<string> removals = _vehicles
                .Where(kv => !kv.Value.IsWithinRetention(now, _retention))
                .Select(kv => kv.Key)
                .ToList();
            foreach (string vehicleId in removals)
            {
                _vehicles.Remove(vehicleId);
            }

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

    public VehiclePosition? Find(string vehicleId)
    {
        lock (_lock)
        {
            return _vehicles.GetValueOrDefault(vehicleId);
        }
    }
}
