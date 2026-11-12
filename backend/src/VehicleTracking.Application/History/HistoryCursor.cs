namespace VehicleTracking.Application.History;

/// <summary>
/// Keyset sayfalamada "kaldığımız yer": bir önceki sayfanın son satırı.
/// Zaman tek başına yetmez (aynı araç aynı saniyede iki varlıkla gelebilir), varlık kimliği eşitliği bozar.
/// </summary>
public sealed record HistoryCursor(DateTimeOffset MeasuredAt, string EntityId);
