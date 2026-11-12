using Microsoft.AspNetCore.Mvc;
using VehicleTracking.Api.Realtime;
using VehicleTracking.Application.History;
using VehicleTracking.Application.Live;

namespace VehicleTracking.Api.Controllers;

[ApiController]
[Route("api/vehicles")]
[Produces("application/json")]
public sealed class VehiclesController(
    ILiveVehicleStore liveState,
    IHistoryQueryService historyService) : ControllerBase
{
    /// <summary>Tüm araçların anlık durumu (bellekten; SignalR'daki ilk görüntünün aynısı).</summary>
    [HttpGet]
    public ActionResult<LiveSnapshotMessage> GetAll()
    {
        LiveSnapshot snapshot = liveState.GetSnapshot();
        return LiveSnapshotMessage.From(snapshot);
    }

    /// <summary>
    /// Aracın konum geçmişi, en yeniden eskiye, sayfalı. Yalnızca saklama penceresi (Storage:Retention, varsayılan 3 saat) içinden.
    /// </summary>
    /// <param name="vehicleId">Araç kimliği.</param>
    /// <param name="from">Başlangıç (dahil). Boşsa: to - 1 saat.</param>
    /// <param name="to">Bitiş (hariç). Boşsa: şimdi.</param>
    /// <param name="pageSize">1-1000, varsayılan 200.</param>
    /// <param name="cursor">Bir önceki cevabın nextCursor değeri; ilk sayfa için boş.</param>
    /// <param name="cancellationToken"></param>
    /// <response code="400">Geçersiz aralık, sayfa boyutu veya imleç.</response>
    /// <response code="503">Veritabanına şu an ulaşılamıyor; geçmiş verisi geçici olarak kullanılamıyor.</response>
    [HttpGet("{vehicleId}/history")]
    [ProducesResponseType<HistoryPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HistoryPageResponse>> GetHistory(
        string vehicleId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int pageSize = HistoryOptions.DefaultPageSizeValue,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        HistoryQueryResult result = await historyService.QueryAsync(
            new HistoryQuery(vehicleId, from, to, pageSize, cursor), cancellationToken);
        if (result.Errors is { } errors)
            return ValidationProblem(new ValidationProblemDetails(errors));

        return result.Page!;
    }
}
