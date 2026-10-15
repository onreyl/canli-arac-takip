import { z } from "zod";

export const systemStatusSchema = z.object({
  lastSuccessfulFetchAt: z.iso.datetime({ offset: true }).nullable(),
  lastFetchDurationMs: z.number().nonnegative().nullable(),
  writtenLastHour: z.int().nonnegative(),
  failedFetchCount: z.int().nonnegative(),
  failedWriteCount: z.int().nonnegative(),
  droppedBatchCount: z.int().nonnegative(),
  connectedClients: z.int().nonnegative(),
  pendingBatches: z.int().nonnegative(),
  liveVehicles: z.int().nonnegative(),
});
export type SystemStatus = z.infer<typeof systemStatusSchema>;
