using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VehicleTracking.Api.Realtime;
using VehicleTracking.Application.History;
using VehicleTracking.Application.Live;
using VehicleTracking.Application.Storage;
using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Api.Controllers;

[ApiController]
[Route("api/vehicles")]
[Produces("application/json")]
public sealed class VehiclesController(
    LiveVehicleState liveState,
    IVehicleHistoryReader historyReader,
    IOptions<StorageOptions> storageOptions,
    TimeProvider timeProvider) : ControllerBase
{
    private const int DefaultPageSize = 200;
    private const int MaxPageSize = 1000;

    /// <summary>Tüm araçların anlık durumu (bellekten; SignalR'daki ilk görüntünün aynısı).</summary>
    [HttpGet]
    public ActionResult<LiveSnapshotMessage> GetAll()
    {
        LiveSnapshot snapshot = liveState.GetSnapshot();
        return new LiveSnapshotMessage(snapshot.Version, snapshot.Vehicles.Select(LiveVehicleDto.From).ToList());
    }

    /// <summary>Tek aracın son durumu.</summary>
    /// <response code="404">Araç son 3 saatte görülmedi.</response>
    [HttpGet("{vehicleId}")]
    [ProducesResponseType<LiveVehicleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<LiveVehicleDto> GetById(string vehicleId)
    {
        VehiclePosition? position = liveState.Find(vehicleId);
        return position is null ? NotFound() : LiveVehicleDto.From(position);
    }

    /// <summary>
    /// Aracın konum geçmişi, en yeniden eskiye, sayfalı. Yalnızca saklama penceresi (son 3 saat) içinden.
    /// </summary>
    /// <param name="vehicleId">Araç kimliği.</param>
    /// <param name="from">Başlangıç (dahil). Boşsa: to - 1 saat.</param>
    /// <param name="to">Bitiş (hariç). Boşsa: şimdi.</param>
    /// <param name="pageSize">1-1000, varsayılan 200.</param>
    /// <param name="cursor">Bir önceki cevabın nextCursor değeri; ilk sayfa için boş.</param>
    /// <param name="cancellationToken"></param>
    /// <response code="400">Geçersiz aralık, sayfa boyutu veya imleç.</response>
    [HttpGet("{vehicleId}/history")]
    [ProducesResponseType<HistoryPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<HistoryPageResponse>> GetHistory(
        string vehicleId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset windowStart = now - storageOptions.Value.Retention;
        DateTimeOffset end = to ?? now;
        DateTimeOffset start = from ?? end.AddHours(-1);

        // Saklama penceresinin dışı zaten silinmiş (ya da silinmek üzere); oraya sorgu kabul etmiyoruz.
        // 1 dk tolerans: istemcinin saati ile sunucununki arasındaki küçük farklar yüzünden hata vermeyelim.
        if (start < windowStart - TimeSpan.FromMinutes(1))
        {
            ModelState.AddModelError(nameof(from), "Başlangıç saklama penceresinin (son 3 saat) dışında.");
        }

        if (end <= start)
        {
            ModelState.AddModelError(nameof(to), "Bitiş başlangıçtan sonra olmalı.");
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            ModelState.AddModelError(nameof(pageSize), $"Sayfa boyutu 1 ile {MaxPageSize} arasında olmalı.");
        }

        HistoryCursor? after = null;
        if (cursor is not null && !HistoryCursorCodec.TryDecode(cursor, out after))
        {
            ModelState.AddModelError(nameof(cursor), "Geçersiz imleç.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // Pencere başından eskisini istese bile (tolerans içinde) pencereye kırpıyoruz:
        // chunk bütün olarak silindiği için DB'de 3 saatten biraz eski satır kalabilir, kullanıcı görmemeli.
        start = start < windowStart ? windowStart : start;

        HistoryPage page = await historyReader.GetPageAsync(vehicleId, start, end, pageSize, after, cancellationToken);
        return new HistoryPageResponse(
            page.Items.Select(HistoryPointDto.From).ToList(),
            page.Next is null ? null : HistoryCursorCodec.Encode(page.Next));
    }
}

/// <summary>Geçmişteki bir nokta.</summary>
public sealed record HistoryPointDto(
    DateTimeOffset MeasuredAt,
    float Latitude,
    float Longitude,
    float? Bearing,
    float? Speed,
    string? RouteId,
    string? TripId,
    string? StopId,
    VehicleStopStatus? Status,
    OccupancyStatus? Occupancy)
{
    public static HistoryPointDto From(VehiclePosition p) => new(
        p.MeasuredAt, p.Latitude, p.Longitude, p.Bearing, p.Speed,
        p.RouteId, p.TripId, p.StopId, p.CurrentStatus, p.Occupancy);
}

/// <summary>Bir sayfa geçmiş. NextCursor null ise son sayfa.</summary>
public sealed record HistoryPageResponse(IReadOnlyList<HistoryPointDto> Items, string? NextCursor);
