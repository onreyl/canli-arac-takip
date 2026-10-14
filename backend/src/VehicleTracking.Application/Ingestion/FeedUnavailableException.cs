namespace VehicleTracking.Application.Ingestion;

/// <summary>
/// Kaynağa ulaşılamadı, zaman aşımı oldu veya yanıt çözülemedi. Çekme döngüsü bunu yakalayıp turu atlar
/// ve "başarısız deneme" sayacını artırır; döngü durmaz.
/// </summary>
public sealed class FeedUnavailableException(string message, Exception innerException)
    : Exception(message, innerException);
