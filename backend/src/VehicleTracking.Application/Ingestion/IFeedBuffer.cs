namespace VehicleTracking.Application.Ingestion;

/// <summary>
/// Çekme ile yazma arasındaki tampon. DB yavaş ya da kapalıyken veri burada bekler (Karar 4).
/// </summary>
/// <remarks>
/// "Al → yaz → onayla" sözleşmesi: yazıcı bir partiyi ancak DB'ye başarıyla yazdıktan sonra onaylar.
/// Aşama 1 (bellek) için onay bir şey yapmaz; Aşama 2'de (disk) onaylanan partinin dosyası silinecek,
/// onaylanmadan çökülürse dosya kalacak ve açılışta tekrar yazılacak (yazma tekrar güvenli olduğu için sorun yok).
/// Bu yüzden arayüz şimdiden onay adımını içeriyor; Aşama 2'de diğer kod değişmeyecek.
/// </remarks>
public interface IFeedBuffer
{
    ValueTask EnqueueAsync(PositionBatch batch, CancellationToken cancellationToken);

    /// <summary>Sıradaki partiyi bekler ve döndürür.</summary>
    ValueTask<PositionBatch> DequeueAsync(CancellationToken cancellationToken);

    /// <summary>Beklemeden sıradaki partiyi almayı dener (kapanışta tamponu boşaltmak için).</summary>
    bool TryDequeue(out PositionBatch? batch);

    /// <summary>Parti DB'ye yazıldı, artık tutulmasına gerek yok.</summary>
    ValueTask AcknowledgeAsync(PositionBatch batch, CancellationToken cancellationToken);

    /// <summary>Şu an bekleyen parti sayısı (sistem durumu için).</summary>
    int Count { get; }
}
