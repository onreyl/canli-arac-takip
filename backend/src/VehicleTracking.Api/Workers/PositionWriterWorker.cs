using VehicleTracking.Application.Ingestion;

namespace VehicleTracking.Api.Workers;

/// <summary>
/// Tampondan partileri alıp DB'ye yazar. DB kapalıysa aynı partiyi bekleyerek tekrar dener (veri kaybolmaz,
/// arkadakiler tamponda bekler). Çekmeden ayrı çalıştığı için DB yavaşlığı çekmeyi ve canlı yayını durdurmaz.
/// </summary>
internal sealed partial class PositionWriterWorker(
    IFeedBuffer buffer,
    IPositionWriter writer,
    IngestionMetrics metrics,
    ILogger<PositionWriterWorker> logger) : BackgroundService
{
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);
    // 10 sn: DB döndükten sonra yazmaya başlama gecikmesi en fazla bu kadar olur (30 sn'de uzun bir kesintiden
    // sonra veri yarım dakika bekleyebiliyordu). Kapalı bir DB'yi 10 sn'de bir denemek yük oluşturmaz.
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(10);

    // Kapanışta tamponu boşaltmak için ayrılan süre (host'un varsayılan kapanış süresi 30 sn).
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(20);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                PositionBatch batch = await buffer.DequeueAsync(stoppingToken);
                await WriteUntilDoneAsync(batch, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Kapanıyoruz: aşağıda tamponu boşaltacağız.
        }

        await DrainAsync();
    }

    // Düzgün kapanışta (docker stop, Ctrl+C) bellekte bekleyenleri yazmaya çalış.
    // Çekme işçisi bundan önce durur (Program.cs'teki kayıt sırası), yani tampona yeni parti gelmez.
    private async Task DrainAsync()
    {
        using var drainCts = new CancellationTokenSource(DrainTimeout);
        int drained = 0;
        try
        {
            while (buffer.TryDequeue(out PositionBatch? batch) && batch is not null)
            {
                await WriteUntilDoneAsync(batch, drainCts.Token);
                drained++;
            }
        }
        catch (OperationCanceledException)
        {
            LogDrainTimedOut(buffer.Count);
        }

        LogDrained(drained);
    }

    private async Task WriteUntilDoneAsync(PositionBatch batch, CancellationToken cancellationToken)
    {
        TimeSpan delay = InitialRetryDelay;
        while (true)
        {
            try
            {
                int inserted = await writer.WriteAsync(batch.Positions, cancellationToken);
                metrics.RecordWritten(inserted);
                await buffer.AcknowledgeAsync(batch, cancellationToken);
                return;
            }
            catch (StorageUnavailableException ex)
            {
                // Geçici: aynı partiyi bekleyip tekrar dene. Bekleme 1 sn'den 10 sn'ye kadar katlanarak artar,
                // kapalı bir DB'yi saniyede bir dövmeyelim.
                metrics.RecordWriteFailure();
                LogWriteRetry(ex.InnerException?.Message ?? ex.Message, delay, buffer.Count);
                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxRetryDelay.Ticks));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Kalıcı hata (bozuk veri ya da bug): tekrar denemek işe yaramaz ve arkadakileri bekletir.
                // Partiyi atıp kayıp olarak sayıyoruz; yüksek seviyede logluyoruz ki gözden kaçmasın.
                metrics.RecordWriteFailure();
                metrics.RecordDroppedBatch();
                LogBatchDiscarded(ex, batch.Positions.Count);
                await buffer.AcknowledgeAsync(batch, cancellationToken);
                return;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "DB'ye yazılamadı ({Error}). {Delay} sonra tekrar denenecek; tamponda {Pending} parti bekliyor.")]
    private partial void LogWriteRetry(string error, TimeSpan delay, int pending);

    [LoggerMessage(Level = LogLevel.Error, Message = "Parti kalıcı hata nedeniyle atıldı, {Count} konum kayboldu.")]
    private partial void LogBatchDiscarded(Exception ex, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Kapanışta tampondan {Count} parti yazıldı.")]
    private partial void LogDrained(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Kapanışta tampon boşaltılamadı, {Pending} parti kayboldu.")]
    private partial void LogDrainTimedOut(int pending);
}
