import { z } from "zod";

export const liveVehicleSchema = z.object({
  vehicleId: z.string(),
  routeId: z.string().nullable(),
  tripId: z.string().nullable(),
  latitude: z.number(),
  longitude: z.number(),
  bearing: z.number().nullable(),
  status: z.enum(["IncomingAt", "StoppedAt", "InTransitTo"]).nullable(),
  occupancy: z.enum([
    "Empty", "ManySeatsAvailable", "FewSeatsAvailable", "StandingRoomOnly",
    "CrushedStandingRoomOnly", "Full", "NotAcceptingPassengers",
    "NoDataAvailable", "NotBoardable",
  ]).nullable(),
  measuredAt: z.iso.datetime({ offset: true }),
  isDeadRun: z.boolean(),
});

export const liveSnapshotMessageSchema = z.object({
  version: z.int().nonnegative(),
  vehicles: z.array(liveVehicleSchema),
});

export const liveUpdateMessageSchema = z.object({
  version: z.int().nonnegative(),
  upserts: z.array(liveVehicleSchema),
  removals: z.array(z.string()),
});

export type LiveVehicle = z.infer<typeof liveVehicleSchema>;
export type LiveSnapshotMessage = z.infer<typeof liveSnapshotMessageSchema>;
export type LiveUpdateMessage = z.infer<typeof liveUpdateMessageSchema>;
