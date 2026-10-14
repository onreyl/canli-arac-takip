using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Application.History;

/// <summary>
/// Keyset sayfalamada "kaldığımız yer": bir önceki sayfanın son satırı.
/// Zaman tek başına yetmez (aynı araç aynı saniyede iki varlıkla gelebilir), varlık kimliği eşitliği bozar.
/// </summary>
public sealed record HistoryCursor(DateTimeOffset MeasuredAt, string EntityId);

/// <summary>Bir sayfa geçmiş. Next null ise son sayfa.</summary>
public sealed record HistoryPage(IReadOnlyList<VehiclePosition> Items, HistoryCursor? Next);

/// <summary>Bir aracın kalıcı depodaki konum geçmişi.</summary>
public interface IVehicleHistoryReader
{
    /// <summary>
    /// [from, to) aralığındaki konumlar, en yeniden eskiye. Keyset sayfalama: offset yerine "şu satırdan daha eski
    /// olanlar". Her sayfa (vehicle_id, measured_at DESC) index'i üzerinden doğrudan bulunur; 1. ve 50. sayfa aynı
    /// hızda. Veri sürekli aktığı için offset'te olan sayfa kayması da yaşanmaz.
    /// </summary>
    Task<HistoryPage> GetPageAsync(
        string vehicleId,
        DateTimeOffset from,
        DateTimeOffset to,
        int pageSize,
        HistoryCursor? after,
        CancellationToken cancellationToken);
}
