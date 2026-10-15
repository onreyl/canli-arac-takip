import { create } from "zustand";
import type { LiveSnapshotMessage, LiveUpdateMessage, LiveVehicle } from "@/shared/contracts/live";
import type { ConnectionStatus } from "@/shared/realtime/vehicleHub";

type LiveVehiclesState = {
  vehicles: Record<string, LiveVehicle>;
  version: number;
  connectionStatus: ConnectionStatus;
  hasSnapshot: boolean;
  lastUpdateDurationMs: number;
  applySnapshot: (message: LiveSnapshotMessage) => void;
  applyUpdate: (message: LiveUpdateMessage) => void;
  setConnectionStatus: (status: ConnectionStatus) => void;
};

export const useLiveVehiclesStore = create<LiveVehiclesState>((set) => ({
  vehicles: {},
  version: -1,
  connectionStatus: "connecting",
  hasSnapshot: false,
  lastUpdateDurationMs: 0,
  setConnectionStatus: (connectionStatus) => set({ connectionStatus }),
  applySnapshot: (message) => {
    const started = performance.now();
    const vehicles = Object.fromEntries(message.vehicles.map((vehicle) => [vehicle.vehicleId, vehicle]));
    // Sunucu yeniden başladığında sürüm sıfırlanabilir; snapshot her zaman otoritedir.
    set({
      vehicles,
      version: message.version,
      hasSnapshot: true,
      lastUpdateDurationMs: performance.now() - started,
    });
  },
  applyUpdate: (message) => {
    const started = performance.now();
    set((state) => {
      if (!state.hasSnapshot || message.version <= state.version) return state;
      const vehicles = message.upserts.length || message.removals.length
        ? { ...state.vehicles }
        : state.vehicles;
      for (const vehicle of message.upserts) vehicles[vehicle.vehicleId] = vehicle;
      for (const vehicleId of message.removals) delete vehicles[vehicleId];
      return { vehicles, version: message.version, lastUpdateDurationMs: performance.now() - started };
    });
  },
}));
