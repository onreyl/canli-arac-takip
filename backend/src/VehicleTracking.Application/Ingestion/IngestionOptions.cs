namespace VehicleTracking.Application.Ingestion;

/// <summary>appsettings.json'daki "Ingestion" bölümü.</summary>
public sealed class IngestionOptions
{
    public const string SectionName = "Ingestion";

    /// <summary>Doküman 3 sn istiyor; yapılandırmadan okunmalı.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Bellek tamponunda en fazla kaç parti bekleyebilir (Aşama 1). Bir parti ≈ bir yeni feed'in yeni konumları
    /// (~600-2.000 konum, kabaca 0,2-0,6 MB). Yeni feed ~6 sn'de bir geldiği için 500 parti ≈ DB'siz ~50 dk.
    /// Dolarsa en eski parti atılır ve kayıp olarak sayılır (Aşama 2'de disk tamponu bu sınırı kaldıracak).
    /// </summary>
    public int BufferCapacity { get; init; } = 500;
}
