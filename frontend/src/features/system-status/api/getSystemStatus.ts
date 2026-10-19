import { fetchJson } from "@/shared/api/http";
import { systemStatusSchema } from "@/shared/contracts/systemStatus";

export function getSystemStatus(signal?: AbortSignal) {
  return fetchJson("/api/system/status", systemStatusSchema, { cache: "no-store", signal });
}
