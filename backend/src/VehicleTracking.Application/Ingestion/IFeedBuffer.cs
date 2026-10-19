namespace VehicleTracking.Application.Ingestion;

/// <summary>
/// Çekme ile yazma arasındaki tampon. DB yavaş ya da kapalıyken veri burada bekler.
/// </summary>
/// <remarks>
/// Disk tamponu kapsam dışıdır; mevcut bellek tamponunun bilinen sınırları için bkz. docs/known-limitations.md.
/// </remarks>
public interface IFeedBuffer
{
    ValueTask EnqueueAsync(PositionBatch batch, CancellationToken cancellationToken);

    /// <summary>Sıradaki partiyi bekler ve döndürür.</summary>
    ValueTask<PositionBatch> DequeueAsync(CancellationToken cancellationToken);

    /// <summary>Beklemeden sıradaki partiyi almayı dener (kapanışta tamponu boşaltmak için).</summary>
    bool TryDequeue(out PositionBatch? batch);

    /// <summary>Şu an bekleyen parti sayısı (sistem durumu için).</summary>
    int Count { get; }
}
