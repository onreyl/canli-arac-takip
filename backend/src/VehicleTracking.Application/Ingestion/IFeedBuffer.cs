namespace VehicleTracking.Application.Ingestion;

/// <summary>
/// Çekme ile yazma arasındaki tampon. DB yavaş ya da kapalıyken veri burada bekler.
/// </summary>
/// <remarks>
/// "Al → yaz → onayla" sözleşmesi: yazıcı bir partiyi ancak DB'ye başarıyla yazdıktan sonra onaylar.
/// Bellek tamponunda onay bir şey yapmaz. Olası disk tamponunda onaylanan partinin dosyası silinebilir;
/// onaylanmadan çökülürse dosya açılışta tekrar yazılabilir (yazma tekrar güvenli olduğu için sorun yok).
/// Onay adımı, disk tamponuna veya kalıcı bir kuyruğa (RabbitMQ vb.) geçilirse yazıcı işçisinin değişmemesini sağlar.
/// Disk tamponu kapsam dışıdır; mevcut bellek tamponunun bilinen sınırları için bkz. docs/known-limitations.md.
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
