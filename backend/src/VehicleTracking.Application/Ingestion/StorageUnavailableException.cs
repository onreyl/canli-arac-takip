namespace VehicleTracking.Application.Ingestion;

/// <summary>
/// Depo geçici olarak kullanılamıyor (DB kapalı, bağlantı koptu, zaman aşımı). Aynı parti beklenip
/// tekrar denenmeli; yazma tekrar güvenli olduğu için yarım kalmış bir denemeyi tekrarlamak çift kayıt üretmez.
/// </summary>
public sealed class StorageUnavailableException(string message, Exception innerException)
    : Exception(message, innerException);
