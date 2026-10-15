import { z } from "zod";
import { liveVehicleSchema } from "./live";

export const historyPointSchema = liveVehicleSchema.omit({ vehicleId: true, isDeadRun: true }).extend({
  speed: z.number().nullable(),
  stopId: z.string().nullable(),
});
export const historyPageSchema = z.object({
  items: z.array(historyPointSchema),
  nextCursor: z.string().nullable(),
});
export type HistoryPoint = z.infer<typeof historyPointSchema>;
export type HistoryPage = z.infer<typeof historyPageSchema>;
