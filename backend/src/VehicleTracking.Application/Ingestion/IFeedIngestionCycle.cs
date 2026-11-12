namespace VehicleTracking.Application.Ingestion;

/// <summary>Feed toplama iş akışının tek turunu çalıştırır.</summary>
public interface IFeedIngestionCycle
{
    /// <summary>Feed'i bir kez işleyerek yeni konumları yayınlar ve tampona yazar.</summary>
    Task RunOnceAsync(CancellationToken cancellationToken);
}
