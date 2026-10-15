"use client";
import { useMemo } from "react";
import { matchesFilter, type VehicleFilter } from "../filter/vehicleFilter";
import { useLiveVehiclesStore } from "../store/liveVehiclesStore";

export function useVehicleFilterCount(filter: VehicleFilter, nowMs: number) {
  const vehicles = useLiveVehiclesStore((state) => state.vehicles);
  return useMemo(() => {
    const values = Object.values(vehicles);
    return {
      total: values.length,
      shown: values.filter((vehicle) => matchesFilter(vehicle, filter, nowMs)).length,
    };
  }, [vehicles, filter, nowMs]);
}
