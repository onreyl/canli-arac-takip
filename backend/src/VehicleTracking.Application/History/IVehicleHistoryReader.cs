namespace VehicleTracking.Application.History;

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
