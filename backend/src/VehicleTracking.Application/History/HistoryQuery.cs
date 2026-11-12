namespace VehicleTracking.Application.History;

/// <summary>Geçmiş sorgusunun istemciden gelen parametreleri.</summary>
public sealed record HistoryQuery(string VehicleId, DateTimeOffset? From, DateTimeOffset? To, int PageSize, string? Cursor);
