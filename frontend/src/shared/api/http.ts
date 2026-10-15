import { z } from "zod";
import { apiBaseUrl } from "@/shared/config";

export class ApiError extends Error {
  constructor(public readonly status: number, message: string) {
    super(message);
    this.name = "ApiError";
  }
}

export async function fetchJson<T>(path: string, schema: z.ZodType<T>, init?: RequestInit): Promise<T> {
  const response = await fetch(apiBaseUrl + path, init);
  if (!response.ok) {
    const problem = z.object({ title: z.string().optional(), detail: z.string().optional() })
      .safeParse(await response.json().catch(() => null));
    throw new ApiError(response.status, problem.success
      ? problem.data.detail ?? problem.data.title ?? response.statusText
      : response.statusText || `HTTP ${response.status}`);
  }
  return schema.parse(await response.json());
}
