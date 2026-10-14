namespace VehicleTracking.Infrastructure.Persistence;

/// <summary>
/// appsettings.json'daki "Storage" bölümü. Doküman saklama süresinin koddan değil yapılandırmadan okunmasını istiyor;
/// demoda Retention'ı 00:05:00, ChunkInterval'ı 00:01:00 yapıp silmeyi birkaç dakikada gösterebiliriz.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Bu süreden eski chunk'lar silinir.</summary>
    public TimeSpan Retention { get; init; } = TimeSpan.FromHours(3);

    /// <summary>
    /// Yeni chunk'ların zaman aralığı. Küçük chunk = silme daha hassas (bir chunk en fazla bu kadar fazla kalır),
    /// ama daha çok chunk. 3 saatlik pencerede 15 dk → ~12 chunk.
    /// </summary>
    public TimeSpan ChunkInterval { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>TimescaleDB'nin silme işinin ne sıklıkla çalışacağı.</summary>
    public TimeSpan RetentionCheckInterval { get; init; } = TimeSpan.FromMinutes(1);
}
