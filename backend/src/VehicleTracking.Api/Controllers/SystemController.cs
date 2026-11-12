using Microsoft.AspNetCore.Mvc;
using VehicleTracking.Api.Contracts;
using VehicleTracking.Api.Realtime;
using VehicleTracking.Application.Ingestion;
using VehicleTracking.Application.Live;

namespace VehicleTracking.Api.Controllers;

[ApiController]
[Route("api/system")]
[Produces("application/json")]
public sealed class SystemController(
    IIngestionMetrics metrics,
    IFeedBuffer buffer,
    ILiveVehicleStore liveState,
    ILiveConnectionRegistry dispatcher) : ControllerBase
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
