"use client";
import dynamic from "next/dynamic";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { ConnectionBadge } from "./ConnectionBadge";
import { useLiveVehicleStream } from "../hooks/useLiveVehicleStream";
import { useLiveVehiclesStore } from "../store/liveVehiclesStore";

const LiveMap = dynamic(() => import("./LiveMap"), { ssr: false });

export default function LiveMapScreen() {
  const t = useTranslations("LiveMap");
  const retry = useLiveVehicleStream();
  const status = useLiveVehiclesStore((state) => state.connectionStatus);
  const hasSnapshot = useLiveVehiclesStore((state) => state.hasSnapshot);
  const isEmpty = useLiveVehiclesStore((state) => Object.keys(state.vehicles).length === 0);
  const isLoading = !hasSnapshot && status !== "disconnected";

  return (
    <main className="relative isolate h-dvh min-h-80 w-full overflow-hidden" aria-label={t("title")}>
      <LiveMap />
      <div className="pointer-events-none absolute inset-x-0 top-0 z-10 flex flex-col items-start gap-3 p-3 sm:p-4">
        {status === "disconnected" && (
          <div role="alert" className="pointer-events-auto flex w-full flex-wrap items-center justify-between gap-2 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-950">
            <span>{t("disconnected")}</span>
            <Button variant="outline" size="sm" onClick={retry}>{t("retry")}</Button>
          </div>
        )}
        <div className="max-w-full"><ConnectionBadge /></div>
      </div>
      {(isLoading || (hasSnapshot && isEmpty && status !== "disconnected")) && (
        <div className="pointer-events-none absolute inset-0 flex items-center justify-center p-4">
          <p role="status" className="max-w-full rounded-lg border bg-background/95 p-4 text-center text-sm shadow-sm">
            {isLoading ? t("loading") : t("empty")}
          </p>
        </div>
      )}
    </main>
  );
}
