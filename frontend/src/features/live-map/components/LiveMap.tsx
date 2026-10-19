"use client";

import { useEffect, useRef } from "react";
import { useTranslations } from "next-intl";
import { Map as MapLibreMap, setWorkerUrl, type GeoJSONSource } from "maplibre-gl";
import type { FeatureCollection, LineString, Point } from "geojson";
import { useLiveVehiclesStore } from "../store/liveVehiclesStore";
import { isStale, matchesFilter, type VehicleFilter } from "../filter/vehicleFilter";
import "maplibre-gl/dist/maplibre-gl.css";

// public/'e kopyalanan worker (bkz. scripts/copy-maplibre-worker.mjs): paketleyici bu dosyayı kendisi servis etmiyor.
setWorkerUrl("/maplibre/maplibre-gl-worker.mjs");

// Karar 1 ve 8: boş seferler gri, yolculu seferler mavi; eskilik yalnız opaklığı değiştirir.
const ACTIVE_COLOR = "#2563eb";
const DEAD_RUN_COLOR = "#6b7280";
const ACTIVE_OPACITY = 0.9;
const STALE_OPACITY = 0.3;

type Props = {
  filter: VehicleFilter;
  nowMs: number;
  selectedVehicleId: string | null;
  onSelectVehicle: (vehicleId: string) => void;
  trail: [number, number][];
};

export default function LiveMap({ filter, nowMs, selectedVehicleId, onSelectVehicle, trail }: Props) {
  const scheduleRef = useRef<(() => void) | null>(null);
  const selectedRef = useRef(selectedVehicleId);
  const selectRef = useRef(onSelectVehicle);
  const trailRef = useRef(trail);
  const updateTrailRef = useRef<(() => void) | null>(null);
  useEffect(() => {
    selectedRef.current = selectedVehicleId;
    selectRef.current = onSelectVehicle;
    scheduleRef.current?.();
  }, [selectedVehicleId, onSelectVehicle]);
  useEffect(() => {
    trailRef.current = trail;
    updateTrailRef.current?.();
  }, [trail]);
  const filterRef = useRef(filter);
  const nowRef = useRef(nowMs);

  // Filtre değişince WebGL haritasını yeniden kurmak pahalıdır ve görünümü sıçratır.
  useEffect(() => {
    filterRef.current = filter;
    nowRef.current = nowMs;
    scheduleRef.current?.();
  }, [filter, nowMs]);
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
      const now = nowRef.current;
      const data: FeatureCollection<Point> = {
        type: "FeatureCollection",
        features: Object.values(state.vehicles)
          .filter((vehicle) => matchesFilter(vehicle, filterRef.current, now))
          .map((vehicle) => ({
            type: "Feature",
            geometry: { type: "Point", coordinates: [vehicle.longitude, vehicle.latitude] },
            properties: {
              vehicleId: vehicle.vehicleId,
              routeId: vehicle.routeId,
              isDeadRun: vehicle.isDeadRun,
              isStale: isStale(vehicle, now),
              selected: vehicle.vehicleId === selectedRef.current,
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

    scheduleRef.current = schedule;
    function updateTrail() {
      if (!loaded) return;
      const coordinates = trailRef.current;
      const data: FeatureCollection<LineString> = {
        type: "FeatureCollection",
        features: coordinates.length < 2 ? [] : [{
          type: "Feature", properties: {},
          geometry: { type: "LineString", coordinates },
        }],
      };
      map.getSource<GeoJSONSource>("trail")?.setData(data);
    }
    updateTrailRef.current = updateTrail;

    const unsubscribe = useLiveVehiclesStore.subscribe((state, previous) => {
      if (state.vehicles !== previous.vehicles || state.version !== previous.version) {
        storeDurationMs = state.lastUpdateDurationMs;
        schedule();
      }
    });

    map.on("load", () => {
      map.addSource("trail", { type: "geojson", data: { type: "FeatureCollection", features: [] } });
      map.addLayer({
        id: "trail", type: "line", source: "trail",
        paint: { "line-color": ACTIVE_COLOR, "line-width": 3 },
      });
      map.addSource("vehicles", { type: "geojson", data: { type: "FeatureCollection", features: [] } });
      map.addLayer({
        id: "vehicles",
        type: "circle",
        source: "vehicles",
        paint: {
          "circle-radius": ["case", ["get", "selected"], 9, 5],
          "circle-stroke-width": ["case", ["get", "selected"], 3, 0],
          "circle-stroke-color": "#ffffff",
          "circle-color": ["case", ["get", "isDeadRun"], DEAD_RUN_COLOR, ACTIVE_COLOR],
          "circle-opacity": ["case", ["get", "isStale"], STALE_OPACITY, ACTIVE_OPACITY],
        },
      });
      loaded = true;
      updateTrail();
      schedule();
    });

    map.on("click", "vehicles", (event) => {
      const id = event.features?.[0]?.properties?.vehicleId;
      if (typeof id === "string") selectRef.current(id);
    });
    map.on("mouseenter", "vehicles", () => { map.getCanvas().style.cursor = "pointer"; });
    map.on("mouseleave", "vehicles", () => { map.getCanvas().style.cursor = ""; });

    return () => {
      loaded = false;
      updateTrailRef.current = null;
      unsubscribe();
      scheduleRef.current = null;
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
