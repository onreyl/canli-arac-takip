namespace VehicleTracking.Api.Contracts;

/// <summary>Bir sayfa geçmiş. NextCursor null ise son sayfa.</summary>
public sealed record HistoryPageResponse(IReadOnlyList<HistoryPointDto> Items, string? NextCursor);
