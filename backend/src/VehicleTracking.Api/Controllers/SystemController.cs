using Microsoft.AspNetCore.Mvc;
using VehicleTracking.Api.Realtime;
using VehicleTracking.Application.Ingestion;
using VehicleTracking.Application.Live;

namespace VehicleTracking.Api.Controllers;

[ApiController]
[Route("api/system")]
[Produces("application/json")]
public sealed class SystemController(
    IngestionMetrics metrics,
    IFeedBuffer buffer,
    LiveVehicleState liveState,
    LiveUpdateDispatcher dispatcher) : ControllerBase
{
    /// <summary>Sistem durumu: veri toplama ve canlı yayının sağlığı.</summary>
    /// <remarks>
    /// Sayaçlar bellekte tutulur, uygulama yeniden başlayınca sıfırlanır (writtenLastHour dahil).
    /// </remarks>
    [HttpGet("status")]
    public ActionResult<SystemStatusResponse> GetStatus()
    {
        IngestionMetricsSnapshot m = metrics.GetSnapshot();
        return new SystemStatusResponse(
            m.LastSuccessfulFetchAt,
            m.LastFetchDuration?.TotalMilliseconds,
            m.WrittenLastHour,
            m.FailedFetchCount,
            m.FailedWriteCount,
            m.DroppedBatchCount,
            dispatcher.ConnectionCount,
            buffer.Count,
            liveState.Count,
            m.LastWriteDuration?.TotalMilliseconds,
            m.MaxWriteDurationLastHour?.TotalMilliseconds);
    }
}

/// <param name="LastSuccessfulFetchAt">Kaynaktan son başarılı okuma zamanı.</param>
/// <param name="LastFetchDurationMs">Son okumanın süresi (çekme + çözme).</param>
/// <param name="WrittenLastHour">Son 1 saatte DB'ye yazılan (yeni) kayıt sayısı.</param>
/// <param name="FailedFetchCount">Başarısız okuma denemesi (kaynak erişilemez / bozuk yanıt).</param>
/// <param name="FailedWriteCount">Başarısız yazma denemesi (DB erişilemez); her tekrar ayrı sayılır.</param>
/// <param name="DroppedBatchCount">Kaybolan parti (tampon taştı ya da kalıcı hata). Sıfır olmalı.</param>
/// <param name="ConnectedClients">Bağlı WebSocket (SignalR) istemcisi.</param>
/// <param name="PendingBatches">Tamponda DB'ye yazılmayı bekleyen parti.</param>
/// <param name="LiveVehicles">Canlı durumdaki araç sayısı.</param>
/// <param name="LastWriteDurationMs">Son başarılı DB yazmasının süresi (milisaniye); henüz yazma yoksa null.</param>
/// <param name="MaxWriteDurationLastHourMs">Son 1 saatteki en uzun başarılı DB yazmasının süresi (milisaniye); pencerede yazma yoksa null.</param>
public sealed record SystemStatusResponse(
    DateTimeOffset? LastSuccessfulFetchAt,
    double? LastFetchDurationMs,
    long WrittenLastHour,
    long FailedFetchCount,
    long FailedWriteCount,
    long DroppedBatchCount,
    int ConnectedClients,
    int PendingBatches,
    int LiveVehicles,
    double? LastWriteDurationMs,
    double? MaxWriteDurationLastHourMs);
