"use client";
import { useTranslations } from "next-intl";
import { Badge } from "@/components/ui/badge";
import { useLiveVehiclesStore } from "../store/liveVehiclesStore";

const statusClasses = {
  connected: "bg-emerald-100 text-emerald-900",
  connecting: "bg-blue-100 text-blue-900",
  reconnecting: "bg-amber-100 text-amber-900",
  disconnected: "bg-red-100 text-red-900",
};

export function ConnectionBadge() {
  const t = useTranslations("Connection");
  const status = useLiveVehiclesStore((state) => state.connectionStatus);
  const count = useLiveVehiclesStore((state) => Object.keys(state.vehicles).length);
  const duration = useLiveVehiclesStore((state) => state.lastUpdateDurationMs);
  return (
    <div className="flex flex-wrap items-center gap-x-3 gap-y-2 rounded-lg border bg-background/95 p-3 shadow-sm">
      <Badge className={statusClasses[status]} role="status">{t(status)}</Badge>
      <span className="text-xs text-muted-foreground">{t("vehicleCount", { count })}</span>
      <span className="text-xs tabular-nums text-muted-foreground">
        {t("duration", { duration })}
      </span>
    </div>
  );
}
