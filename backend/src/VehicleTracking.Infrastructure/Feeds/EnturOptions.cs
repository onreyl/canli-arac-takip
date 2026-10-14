namespace VehicleTracking.Infrastructure.Feeds;

/// <summary>appsettings.json'daki "Entur" bölümü. Doküman akış adresinin koddan değil yapılandırmadan okunmasını istiyor.</summary>
public sealed class EnturOptions
{
    public const string SectionName = "Entur";

    public required Uri VehiclePositionsUrl { get; init; }

    /// <summary>
    /// Entur istemcilerin "ET-Client-Name" başlığıyla kendini tanıtmasını istiyor ("şirket-uygulama" biçiminde).
    /// Başlıksız istek de 200 dönüyor (gözlem), ama tanıtmayan istemcilere daha sıkı limit uygulanabilir.
    /// </summary>
    public required string ClientName { get; init; }

    /// <summary>
    /// Tek istek için üst sınır. Gözlemde okuma en fazla ~1,7 sn sürdü; 10 sn takılı kalan bir isteği
    /// bırakıp sonraki tura geçmek için yeterli pay.
    /// </summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(10);
}
