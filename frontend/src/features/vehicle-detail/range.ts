export const historyRanges = ["15m", "30m", "1h", "3h"] as const;
export type HistoryRange = typeof historyRanges[number];
export const rangeDurationMs: Record<HistoryRange, number> = {
  "15m": 15 * 60_000, "30m": 30 * 60_000, "1h": 60 * 60_000, "3h": 180 * 60_000,
};

export function parseRange(value: string | null): HistoryRange {
  // Paylaşılan bağlantıdaki bilinmeyen aralık da varsayılan pencereye dönmelidir.
  return historyRanges.find((range) => range === value) ?? "1h";
}

export function detailToParams(vehicleId: string | null, range: HistoryRange, searchParams: Pick<URLSearchParams, "toString">) {
  // Seçim değişirken arama ve eski araç filtresi kaybolmamalıdır (Karar 11c).
  const params = new URLSearchParams(searchParams.toString());
  if (vehicleId) params.set("vehicle", vehicleId);
  else params.delete("vehicle");
  // Varsayılan aralık URL'yi uzatmaz; kapalı panelin aralığı bağlantıda kalmaz.
  if (vehicleId && range !== "1h") params.set("range", range);
  else params.delete("range");
  return params;
}
