import { apiBaseUrl } from "@/shared/config";
import { systemStatusSchema } from "@/shared/contracts/systemStatus";

export async function getSystemStatus(baseUrl = apiBaseUrl, signal?: AbortSignal) {
  const response = await fetch(baseUrl + "/api/system/status", { cache: "no-store", signal });
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  return systemStatusSchema.parse(await response.json());
}
