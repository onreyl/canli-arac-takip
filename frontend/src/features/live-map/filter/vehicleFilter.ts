import type { LiveVehicle } from "@/shared/contracts/live";

export type VehicleFilter = { query: string; includeStale: boolean };

type SearchParams = Pick<URLSearchParams, "get" | "toString">;

// Gözlemlerde tazelik iki tepeli; Karar 8 beş dakika sessizliği eski kabul eder.
const STALE_AFTER_MS = 5 * 60_000;
// Akış durduğunda da araçlar eskir; harita ve sayım aynı aralıkta yenilenmelidir.
export const STALE_REFRESH_MS = 30_000;

export function parseFilter(searchParams: SearchParams): VehicleFilter {
  // Eski seferler feed'de kaldığından varsayılan yalnız aktif araçlardır (gözlem 2).
  return { query: searchParams.get("q") ?? "", includeStale: searchParams.get("all") === "1" };
}

export function filterToParams(filter: VehicleFilter, searchParams: SearchParams): URLSearchParams {
  // Paylaşılan bağlantıda seçili araç ve diğer katmanların parametreleri kaybolmamalıdır (Karar 5).
  const params = new URLSearchParams(searchParams.toString());
  const query = filter.query.trim();
  // Boş arama ve varsayılan tazelik için parametre gerekmemesi temiz bağlantıyı korur.
  if (query) params.set("q", query);
  else params.delete("q");
  if (filter.includeStale) params.set("all", "1");
  else params.delete("all");
  return params;
}

export function isStale(vehicle: LiveVehicle, nowMs: number): boolean {
  // Feed'de bulunmak canlı olmak değildir; son ölçümün zamanı esas alınır (gözlem 2).
  return nowMs - Date.parse(vehicle.measuredAt) > STALE_AFTER_MS;
}

export function routeLabel(routeId: string | null): string | null {
  // Entur'un operatör önekleri hat numarası değildir; bilinmeyen kimlik biçimlerini kaybetmeyiz.
  if (routeId === null) return null;
  const marker = ":Line:";
  const index = routeId.indexOf(marker);
  return index === -1 ? routeId : routeId.slice(index + marker.length);
}

export function matchesFilter(vehicle: LiveVehicle, filter: VehicleFilter, nowMs: number): boolean {
  // Saatlerdir sessiz araçlar arama sonucu olsa da aktif filtresini geçmemelidir (tazelik gözlemi).
  if (!filter.includeStale && isStale(vehicle, nowMs)) return false;
  // Kimliklerin öneklerini ezberlemeden, boşluk ve harf büyüklüğünden bağımsız arama yapılabilmelidir.
  const query = filter.query.trim().toLowerCase();
  if (!query) return true;
  // DeadRun araçlarında routeId null olduğundan yalnız vehicleId ile bulunurlar (gözlem 6 ve 8).
  return [vehicle.vehicleId, routeLabel(vehicle.routeId), vehicle.routeId]
    .some((value) => value?.toLowerCase().includes(query) ?? false);
}
