"use client";

import { useEffect, useRef } from "react";
import { useTranslations } from "next-intl";
import { Map as MapLibreMap, setWorkerUrl, type GeoJSONSource } from "maplibre-gl";
import type { FeatureCollection, Point } from "geojson";
import { useLiveVehiclesStore } from "../store/liveVehiclesStore";
import "maplibre-gl/dist/maplibre-gl.css";

// Karar 8: aktif araçların ölçümleri iki dakikadan sık; beş dakika sessizlik soluk gösterilir.
// public/'e kopyalanan worker (bkz. scripts/copy-maplibre-worker.mjs): paketleyici bu dosyayı kendisi servis etmiyor.
setWorkerUrl("/maplibre/maplibre-gl-worker.mjs");

const STALE_AFTER_MS = 5 * 60_000;
const STALE_REFRESH_MS = 30_000;
// Karar 1 ve 8: boş seferler gri, yolculu seferler mavi; eskilik yalnız opaklığı değiştirir.
const ACTIVE_COLOR = "#2563eb";
const DEAD_RUN_COLOR = "#6b7280";
const ACTIVE_OPACITY = 0.9;
const STALE_OPACITY = 0.3;

export default function LiveMap() {
  const container = useRef<HTMLDivElement>(null);
  const t = useTranslations("Map");

  useEffect(() => {
    if (!container.current) return;
    const map = new MapLibreMap({
      container: container.current,
      style: "https://tiles.openfreemap.org/styles/liberty",
      center: [10.75, 59.91],
      zoom: 5,
      locale: {
        "Map.Title": t("title"),
        "Map.Description": t("description"),
        "AttributionControl.ToggleAttribution": t("toggleAttribution"),
      },
    });
    let loaded = false;
    let frame: number | undefined;
    let storeDurationMs = useLiveVehiclesStore.getState().lastUpdateDurationMs;

    function flush() {
      frame = undefined;
      if (!loaded) return;
      const source = map.getSource<GeoJSONSource>("vehicles");
      if (!source) return;
      const state = useLiveVehiclesStore.getState();
      const started = performance.now();
      const now = Date.now();
      const data: FeatureCollection<Point> = {
        type: "FeatureCollection",
        features: Object.values(state.vehicles).map((vehicle) => ({
          type: "Feature",
          geometry: { type: "Point", coordinates: [vehicle.longitude, vehicle.latitude] },
          properties: {
            vehicleId: vehicle.vehicleId,
            routeId: vehicle.routeId,
            isDeadRun: vehicle.isDeadRun,
            isStale: now - Date.parse(vehicle.measuredAt) > STALE_AFTER_MS,
          },
        })),
      };
      source.setData(data);
      // Worker/GPU çizimi asenkrondur; bu ölçüm GeoJSON üretimi ve setData çağrısının CPU süresidir.
      useLiveVehiclesStore.setState({
        lastUpdateDurationMs: storeDurationMs + performance.now() - started,
      });
    }

    function schedule() {
      if (loaded && frame === undefined) frame = requestAnimationFrame(flush);
    }

    const unsubscribe = useLiveVehiclesStore.subscribe((state, previous) => {
      if (state.vehicles !== previous.vehicles || state.version !== previous.version) {
        storeDurationMs = state.lastUpdateDurationMs;
        schedule();
      }
    });

    map.on("load", () => {
      map.addSource("vehicles", { type: "geojson", data: { type: "FeatureCollection", features: [] } });
      map.addLayer({
        id: "vehicles",
        type: "circle",
        source: "vehicles",
        paint: {
          "circle-radius": 5,
          "circle-color": ["case", ["get", "isDeadRun"], DEAD_RUN_COLOR, ACTIVE_COLOR],
          "circle-opacity": ["case", ["get", "isStale"], STALE_OPACITY, ACTIVE_OPACITY],
        },
      });
      loaded = true;
      schedule();
    });
    const interval = setInterval(schedule, STALE_REFRESH_MS);

    return () => {
      loaded = false;
      unsubscribe();
      clearInterval(interval);
      if (frame !== undefined) cancelAnimationFrame(frame);
      map.remove();
    };
  }, [t]);

  // Dış kutu konumlandırır, iç kutu haritayı taşır: MapLibre iç kutuya kendi "position: relative" stilini verir,
  // "absolute" doğrudan harita kutusunda olsaydı ezilir ve harita 0 yükseklikte kalırdı.
  return (
    <div className="absolute inset-0">
      <div ref={container} className="h-full w-full" aria-label={t("title")} />
    </div>
  );
}
