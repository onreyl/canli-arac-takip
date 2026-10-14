using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Application.Ingestion;

/// <summary>Konumları kalıcı depoya toplu yazar.</summary>
public interface IPositionWriter
{
    /// <summary>
    /// Konumları yazar. Tekrar güvenlidir (idempotent): daha önce yazılmış (EntityId, MeasuredAt) çiftleri sessizce
    /// atlanır. Bu sayede bir parti hata sonrası tekrar denenebilir ya da uygulama yeniden başlayıp aynı feed'i
    /// tekrar okuyabilir; çift kayıt oluşmaz.
    /// </summary>
    /// <returns>Gerçekten eklenen (yeni) satır sayısı.</returns>
    Task<int> WriteAsync(IReadOnlyCollection<VehiclePosition> positions, CancellationToken cancellationToken);
}
