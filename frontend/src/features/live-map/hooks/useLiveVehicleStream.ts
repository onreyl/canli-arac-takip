"use client";
import { useCallback, useEffect, useRef } from "react";
import { createVehicleHub } from "@/shared/realtime/vehicleHub";
import { useLiveVehiclesStore } from "../store/liveVehiclesStore";

export function useLiveVehicleStream() {
  const hubRef = useRef<ReturnType<typeof createVehicleHub> | null>(null);

  useEffect(() => {
    // StrictMode'un eski bağlantısı yeni mount'un durumuna yazmamalı.
    const hub = createVehicleHub();
    hubRef.current = hub;
    hub.on("snapshot", (message) => useLiveVehiclesStore.getState().applySnapshot(message));
    hub.on("update", (message) => useLiveVehiclesStore.getState().applyUpdate(message));
    hub.on("status", (status) => useLiveVehiclesStore.getState().setConnectionStatus(status));
    // Geliştirmede React StrictMode bileşeni bilerek aç-kapa-aç yapar. Başlatmayı bir sonraki tick'e ertelersek
    // hemen kapatılan ilk kopya bağlantı hiç açmaz; yoksa SignalR "negotiation sırasında durduruldu" hatası basıyordu.
    const startTimer = setTimeout(() => void hub.start(), 0);
    return () => {
      clearTimeout(startTimer);
      hubRef.current = null;
      void hub.stop().catch((error: unknown) => console.warn("Araç bağlantısı kapatılamadı.", error));
    };
  }, []);

  return useCallback(() => { void hubRef.current?.start(); }, []);
}
