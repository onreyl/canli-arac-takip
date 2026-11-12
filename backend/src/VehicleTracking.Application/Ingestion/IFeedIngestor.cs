namespace VehicleTracking.Application.Ingestion;

/// <summary>Feed içinden yazılacak yeni konumları ayıklar.</summary>
public interface IFeedIngestor
{
    /// <summary>Feed'i işler ve saklama penceresindeki yeni konumları döndürür.</summary>
    IngestResult Process(FeedSnapshot snapshot, DateTimeOffset now);
}
