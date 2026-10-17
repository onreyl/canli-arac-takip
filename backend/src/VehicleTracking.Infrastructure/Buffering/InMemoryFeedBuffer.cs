using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VehicleTracking.Application.Ingestion;

namespace VehicleTracking.Infrastructure.Buffering;

/// <summary>
/// Bellek tamponu: sınırlı bir Channel. Çekme ve yazma birbirini beklemez;
/// DB kapalıyken partiler burada birikir.
/// </summary>
/// <remarks>
/// Bilinen kayıp durumları:
/// - Uygulama ani çökerse (kill -9) tampondakiler kaybolur. Düzgün kapanışta yazıcı tamponu boşaltmaya çalışır.
/// - DB çok uzun süre kapalı kalıp tampon dolarsa en eski parti atılır (sayılır, loglanır).
/// Disk tamponu kapsam dışıdır; bunlar bilinen sınırlardır. Bkz. docs/known-limitations.md.
/// </remarks>
internal sealed partial class InMemoryFeedBuffer : IFeedBuffer
{
    private readonly Channel<PositionBatch> _channel;

    public InMemoryFeedBuffer(IOptions<IngestionOptions> options, IngestionMetrics metrics, ILogger<InMemoryFeedBuffer> logger)
    {
        // DropOldest: tampon doluyken çekmeyi durdurmak (Wait) canlı haritayı da dondururdu.
        // En eskiyi atıp en yeniyi tutmayı seçiyoruz; atılan her parti kayıp olarak sayılır.
        _channel = Channel.CreateBounded<PositionBatch>(
            new BoundedChannelOptions(options.Value.BufferCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true,
            },
            dropped =>
            {
                metrics.RecordDroppedBatch();
                LogBatchDropped(logger, dropped.Positions.Count);
            });
    }

    public int Count => _channel.Reader.Count;

    public ValueTask EnqueueAsync(PositionBatch batch, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(batch, cancellationToken);

    public ValueTask<PositionBatch> DequeueAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAsync(cancellationToken);

    public bool TryDequeue(out PositionBatch? batch) => _channel.Reader.TryRead(out batch);

    // Bellekte parti alındığı an tampondan çıkmış olur; onaylanacak bir şey yok.
    public ValueTask AcknowledgeAsync(PositionBatch batch, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    [LoggerMessage(Level = LogLevel.Error, Message = "Tampon dolu: en eski parti atıldı, {Count} konum kayboldu.")]
    private static partial void LogBatchDropped(ILogger logger, int count);
}
