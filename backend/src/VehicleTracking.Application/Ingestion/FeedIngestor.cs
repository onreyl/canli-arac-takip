using Microsoft.Extensions.Options;
using VehicleTracking.Application.Storage;
using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Application.Ingestion;

/// <summary>Bir feed'in işlenme sonucu.</summary>
/// <param name="IsStale">Feed bir öncekinden yeni değil (aynı ya da geri gitmiş); hiç işlenmedi.</param>
/// <param name="NewPositions">Daha önce görülmemiş ve saklama penceresi içindeki konumlar.</param>
public sealed record IngestResult(bool IsStale, IReadOnlyList<VehiclePosition> NewPositions)
{
    public static readonly IngestResult Stale = new(true, []);
}

/// <summary>
/// "Bu feed'den neyi yazmalıyız?" kararını verir. Durum tutar, bu yüzden singleton.
/// Sadece çekme döngüsü çağırır (tek iş parçacığı), kilit gerekmez.
/// </summary>
public sealed class FeedIngestor(IOptions<StorageOptions> storageOptions)
{
    private readonly TimeSpan _retention = storageOptions.Value.Retention;

    // Her varlığın en son gördüğümüz ölçüm zamanı (varlık id → zaman).
    // Bu sadece bir ön elemedir: uygulama yeniden başlayınca boşalır, o zaman DB'nin birincil anahtarı
    // (ON CONFLICT DO NOTHING) tekrarları yine engeller. Asıl "yeni mi?" otoritesi DB.
    private readonly Dictionary<string, DateTimeOffset> _lastSeen = new();
    private DateTimeOffset? _lastGeneratedAt;
    private DateTimeOffset _lastPrunedAt = DateTimeOffset.MinValue;

    public IngestResult Process(FeedSnapshot snapshot, DateTimeOffset now)
    {
        // Gözlem: feed zamanı aynıysa içerik byte byte aynı; turların ~%15'inde zaman GERİ gidiyor
        // (Entur'un arkasında birden fazla sunucu). İkisinde de işlenecek yeni bir şey yok.
        if (snapshot.GeneratedAt is { } generatedAt && _lastGeneratedAt is { } last && generatedAt <= last)
        {
            return IngestResult.Stale;
        }

        _lastGeneratedAt = snapshot.GeneratedAt ?? _lastGeneratedAt;

        var newPositions = new List<VehiclePosition>();
        foreach (VehiclePosition position in snapshot.Positions)
        {
            // Gözlem: feed'in yarısından fazlası bitmiş seferlerin saatler önceki son konumu.
            if (!position.IsWithinRetention(now, _retention))
            {
                continue;
            }

            if (_lastSeen.TryGetValue(position.EntityId, out DateTimeOffset seen) && position.MeasuredAt <= seen)
            {
                continue;
            }

            _lastSeen[position.EntityId] = position.MeasuredAt;
            newPositions.Add(position);
        }

        PruneIfDue(now);
        return new IngestResult(false, newPositions);
    }

    // Varlık kimliği "sefer-tarih-araç"; her gün yenileri eklenir. Pencere dışına çıkanları atmazsak sözlük sonsuza dek büyür.
    private void PruneIfDue(DateTimeOffset now)
    {
        if (now - _lastPrunedAt < TimeSpan.FromMinutes(1))
        {
            return;
        }

        DateTimeOffset cutoff = now - _retention;
        foreach (string entityId in _lastSeen.Where(kv => kv.Value < cutoff).Select(kv => kv.Key).ToList())
        {
            _lastSeen.Remove(entityId);
        }

        _lastPrunedAt = now;
    }
}
