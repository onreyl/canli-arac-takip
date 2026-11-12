namespace VehicleTracking.Application.History;

/// <summary>Geçmiş sayfası veya doğrulama hataları.</summary>
public sealed record HistoryQueryResult
{
    private HistoryQueryResult(HistoryPageResponse? page, IDictionary<string, string[]>? errors)
        => (Page, Errors) = (page, errors);

    /// <summary>Başarılı sorgunun sayfası.</summary>
    public HistoryPageResponse? Page { get; }
    /// <summary>İstek parametrelerine göre doğrulama hataları.</summary>
    public IDictionary<string, string[]>? Errors { get; }

    /// <summary>Başarılı sorgu sonucunu oluşturur.</summary>
    public static HistoryQueryResult Success(HistoryPageResponse page) => new(page, null);

    /// <summary>Geçersiz sorgu sonucunu oluşturur.</summary>
    public static HistoryQueryResult Invalid(IDictionary<string, string[]> errors) => new(null, errors);
}
