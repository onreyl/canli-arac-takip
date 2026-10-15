"use client";
import { useLiveVehiclesStore } from "./store/liveVehiclesStore";

export { routeLabel, isStale } from "./filter/vehicleFilter";
export function useLiveVehicle(vehicleId: string) {
  return useLiveVehiclesStore((state) => state.vehicles[vehicleId]);
}
export function useLiveSnapshotReady() {
  return useLiveVehiclesStore((state) => state.hasSnapshot);
}
