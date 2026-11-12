namespace VehicleTracking.Application.History;

/// <summary>Geçmiş sorgusunu doğrular ve yanıt sayfasını üretir.</summary>
public interface IHistoryQueryService
{
    /// <summary>Geçmiş sayfasını veya doğrulama hatalarını döndürür.</summary>
    Task<HistoryQueryResult> QueryAsync(HistoryQuery query, CancellationToken cancellationToken);
}
