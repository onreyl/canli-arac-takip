using Microsoft.Extensions.Options;
using VehicleTracking.Application.Ingestion;

namespace VehicleTracking.Api.Workers;

/// <summary>
/// Uygulama açılınca kendiliğinden başlar, her PollInterval'da (3 sn) feed'i çeker ve yeni konumları tampona koyar.
/// Hiçbir hata döngüyü durdurmaz.
/// </summary>
internal sealed partial class FeedPollingWorker(
    IFeedIngestionCycle cycle,
    IOptions<IngestionOptions> options,
    ILogger<FeedPollingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // PeriodicTimer: bir tur aralıktan uzun sürerse turlar birikmez, aynı anda iki istek çalışmaz.
        // Gözlemde en yavaş okuma ~1,7 sn; 3 sn'lik aralığa sığıyor.
        using var timer = new PeriodicTimer(options.Value.PollInterval);
        try
        {
            do
            {
                try
                {
                    await cycle.RunOnceAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Beklenmeyen bir hata (bug) bile döngüyü öldürmesin: .NET'te BackgroundService'ten kaçan hata
                    // varsayılan olarak tüm uygulamayı durdurur.
                    LogUnexpectedError(ex);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Uygulama kapanıyor.
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Çekme turunda beklenmeyen hata")]
    private partial void LogUnexpectedError(Exception ex);
}
