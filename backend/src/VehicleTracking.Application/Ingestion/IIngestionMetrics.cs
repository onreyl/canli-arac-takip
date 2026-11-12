namespace VehicleTracking.Application.Ingestion;

/// <summary>Veri toplama ve kalıcı yazma sayaçları.</summary>
public interface IIngestionMetrics
{
    /// <summary>Başarılı okumanın zamanını ve süresini kaydeder.</summary>
    void RecordFetchSuccess(TimeSpan duration);

    /// <summary>Başarısız okuma sayısını artırır.</summary>
    void RecordFetchFailure();

    /// <summary>Başarısız yazma sayısını artırır.</summary>
    void RecordWriteFailure();

    /// <summary>Kaybolan parti sayısını artırır.</summary>
    void RecordDroppedBatch();

    /// <summary>Başarılı yazmanın kayıt sayısını ve süresini kaydeder.</summary>
    void RecordWritten(int count, TimeSpan duration);

    /// <summary>Sayaçların anlık kopyasını döndürür.</summary>
    IngestionMetricsSnapshot GetSnapshot();
}
