namespace VehicleTracking.Application.History;

/// <summary>Varsayılanları ve imleci çözülmüş geçmiş sorgusu.</summary>
public sealed record ResolvedHistoryQuery(
    string VehicleId,
    DateTimeOffset Start,
    DateTimeOffset End,
    DateTimeOffset WindowStart,
    int PageSize,
    string? Cursor,
    HistoryCursor? After)
{
    /// <summary>Geçmiş sorgusunu verilen ana göre çözer.</summary>
    public static ResolvedHistoryQuery Resolve(HistoryQuery query, DateTimeOffset now, TimeSpan retention, HistoryOptions options)
    {
        DateTimeOffset end = query.To ?? now;
        HistoryCursor? after = null;
        if (query.Cursor is not null)
            HistoryCursorCodec.TryDecode(query.Cursor, out after);

        return new(query.VehicleId, query.From ?? end - options.DefaultRange, end, now - retention,
            query.PageSize, query.Cursor, after);
    }
}
