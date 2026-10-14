namespace VehicleTracking.Application.Ingestion;

/// <summary>
/// Canlı konum verisinin kaynağı. Application, verinin Entur'dan mı, protobuf mu JSON mu geldiğini bilmez.
/// </summary>
public interface IFeedSource
{
    /// <summary>Kaynaktan o anki tam listeyi çeker.</summary>
    /// <exception cref="FeedUnavailableException">Kaynağa ulaşılamadı veya yanıt bozuk.</exception>
    Task<FeedSnapshot> FetchAsync(CancellationToken cancellationToken);
}
