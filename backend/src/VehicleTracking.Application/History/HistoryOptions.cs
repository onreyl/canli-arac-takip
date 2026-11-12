namespace VehicleTracking.Application.History;

/// <summary>Geçmiş sorgusunun varsayılanları ve sınırları.</summary>
public sealed class HistoryOptions
{
    public const string SectionName = "History";
    // Action parametresinin varsayılanı derleme zamanı sabiti olmalı; options da aynı kaynağı kullanır.
    public const int DefaultPageSizeValue = 200;
    public const int MinPageSize = 1;

    /// <summary>Başlangıç belirtilmediğinde bitişten çıkarılacak süre.</summary>
    public TimeSpan DefaultRange { get; init; } = TimeSpan.FromHours(1);
    /// <summary>Saklama sınırındaki saat farkı toleransı.</summary>
    // İstemci ile sunucu saati arasındaki küçük farklar için tolerans tanınır.
    public TimeSpan ClockSkewTolerance { get; init; } = TimeSpan.FromMinutes(1);
    /// <summary>Varsayılan sayfa boyutu.</summary>
    public int DefaultPageSize { get; init; } = DefaultPageSizeValue;
    /// <summary>İzin verilen en büyük sayfa boyutu.</summary>
    public int MaxPageSize { get; init; } = 1000;
}
