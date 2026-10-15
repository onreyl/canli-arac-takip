"use client";
import { useFormatter, useNow, useTranslations } from "next-intl";
import { XIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { isStale, routeLabel, useLiveSnapshotReady, useLiveVehicle } from "@/features/live-map";
import { ApiError } from "@/shared/api/http";
import type { LiveVehicle } from "@/shared/contracts/live";
import { historyRanges, type HistoryRange } from "../range";
import type { useVehicleHistory } from "../hooks/useVehicleHistory";

type Props = {
  vehicleId: string;
  range: HistoryRange;
  history: ReturnType<typeof useVehicleHistory>;
  onRangeChange: (range: HistoryRange) => void;
  onClose: () => void;
};

function MeasurementAge({ vehicle }: { vehicle: LiveVehicle }) {
  const format = useFormatter();
  // Saniyelik saat listeyi ve haritayı yeniden çizdirmemelidir.
  const now = useNow({ updateInterval: 1000 });
  return (
    <span className="flex flex-wrap items-center gap-2">
      {format.relativeTime(new Date(vehicle.measuredAt), { now, unit: "second", style: "short" })}

    </span>
  );
}

function StaleBadge({ vehicle }: { vehicle: LiveVehicle }) {
  const t = useTranslations("VehicleDetail");
  const now = useNow({ updateInterval: 30_000 });
  return isStale(vehicle, now.getTime()) ? <Badge variant="secondary">{t("stale")}</Badge> : null;
}

function LiveDetails({ vehicleId }: { vehicleId: string }) {
  const t = useTranslations("VehicleDetail");
  const format = useFormatter();
  const vehicle = useLiveVehicle(vehicleId);
  const ready = useLiveSnapshotReady();
  if (!ready) return <p role="status">{t("loadingLive")}</p>;
  if (!vehicle) return <p role="status">{t("notLive")}</p>;
  const fields = [
    [t("route"), vehicle.routeId ?? t("unknown")],
    [t("trip"), vehicle.tripId ?? t("unknown")],
    [t("statusLabel"), vehicle.status ? t(`status.${vehicle.status}`) : t("unknown")],
    [t("occupancyLabel"), vehicle.occupancy ? t(`occupancy.${vehicle.occupancy}`) : t("unknown")],
    [t("bearing"), vehicle.bearing === null ? t("unknown") : t("degrees", { value: format.number(vehicle.bearing, { maximumFractionDigits: 1 }) })],
    [t("position"), t("coordinates", {
      latitude: format.number(vehicle.latitude, { minimumFractionDigits: 5, maximumFractionDigits: 5 }),
      longitude: format.number(vehicle.longitude, { minimumFractionDigits: 5, maximumFractionDigits: 5 }),
    })],
  ];
  return (
    <dl className="space-y-2 text-sm">
      {fields.map(([label, value]) => <div key={label}><dt className="text-muted-foreground">{label}</dt><dd className="break-words">{value}</dd></div>)}
      <div><dt className="text-muted-foreground">{t("updated")}</dt><dd><MeasurementAge vehicle={vehicle} /></dd></div>
    </dl>
  );
}

function HistoryDetails({ range, history, onRangeChange }: Pick<Props, "range" | "history" | "onRangeChange">) {
  const t = useTranslations("VehicleDetail");
  const format = useFormatter();
  const points = history.data?.pages.flatMap((page) => page.items) ?? [];
  return (
    <section className="space-y-3 border-t pt-4" aria-label={t("history")}>
      <div className="flex items-center justify-between gap-2">
        <h3 className="font-medium">{t("history")}</h3>
        <Button variant="outline" size="sm" disabled={history.isFetching} onClick={() => void history.refresh()}>{t("refresh")}</Button>
      </div>
      <div className="flex flex-wrap gap-1" role="group" aria-label={t("range")}>
        {historyRanges.map((value) => <Button key={value} size="sm" variant={range === value ? "default" : "outline"}
          aria-pressed={range === value} onClick={() => onRangeChange(value)}>{t(`ranges.${value}`)}</Button>)}
      </div>
      {history.isPending && <div role="status" className="space-y-2">
        <span className="sr-only">{t("loadingHistory")}</span>
        {[0, 1, 2, 3].map((row) => <Skeleton key={row} className="h-8 w-full" />)}
      </div>}
      {history.isError && <div role="alert" className="space-y-2">
        <p>{history.error instanceof ApiError && history.error.status === 400 ? t("invalidRange") : t("historyError")}</p>
        <Button variant="outline" size="sm" disabled={history.isFetching}
          onClick={() => void (history.isFetchNextPageError ? history.fetchNextPage() : history.refetch())}>{t("retry")}</Button>
      </div>}
      {history.isSuccess && points.length === 0 && <p role="status">{t("emptyHistory")}</p>}
      {points.length > 0 && <table className="w-full text-left text-xs tabular-nums">
        <thead><tr><th scope="col" className="py-2">{t("time")}</th><th scope="col">{t("statusLabel")}</th><th scope="col" className="text-right">{t("speed")}</th></tr></thead>
        <tbody>{points.map((point, index) => <tr key={index} className="border-t">
          <td className="py-2 pr-2">{format.dateTime(new Date(point.measuredAt), { hour: "2-digit", minute: "2-digit", second: "2-digit", hourCycle: "h23" })}</td>
          <td className="pr-2">{point.status ? t(`status.${point.status}`) : t("unknown")}</td>
          <td className="text-right">{point.speed === null ? t("missingSpeed") : format.number(point.speed * 3.6, { maximumFractionDigits: 1 })}</td>
        </tr>)}</tbody>
      </table>}
      {history.data && <p role="status" className="text-xs text-muted-foreground">{t("loadedCount", { count: format.number(points.length) })}</p>}
      {history.hasNextPage && <Button variant="outline" className="w-full" disabled={history.isFetching}
        onClick={() => void history.fetchNextPage()}>{history.isFetchingNextPage ? t("loadingHistory") : t("loadMore")}</Button>}
    </section>
  );
}

export function VehicleDetailPanel(props: Props) {
  const t = useTranslations("VehicleDetail");
  const vehicle = useLiveVehicle(props.vehicleId);
  const title = t("title", { vehicle: props.vehicleId });
  const heading = <span className="flex flex-wrap items-center gap-2">
    <span>{title}</span>
    {vehicle && routeLabel(vehicle.routeId) && <Badge variant="outline">{routeLabel(vehicle.routeId)}</Badge>}
    {vehicle?.isDeadRun && <Badge variant="secondary">{t("deadRun")}</Badge>}
    {vehicle && <StaleBadge vehicle={vehicle} />}
  </span>;
  const close = <Button variant="ghost" size="icon-sm" aria-label={t("close")} onClick={props.onClose}><XIcon /></Button>;
  const content = <div className="space-y-4 p-4">
    <section aria-label={t("live")} className="space-y-3">
      <h3 className="font-medium">{t("live")}</h3>
      <LiveDetails vehicleId={props.vehicleId} />
    </section>
    <HistoryDetails range={props.range} history={props.history} onRangeChange={props.onRangeChange} />
  </div>;
  // Tek panel, iki düzen: mobilde ekranın alt yarısı, sm (640px) ve üstünde sağda sabit genişlik. Modal değil:
  // harita üstte görünür ve kullanılabilir kalır (başka araca dokunup seçmek mümkün).
  return (
    <aside aria-label={title}
      className="absolute inset-x-0 bottom-0 z-20 h-[55dvh] overflow-y-auto rounded-t-lg border-t bg-background shadow-lg sm:inset-x-auto sm:inset-y-4 sm:right-4 sm:h-auto sm:w-[360px] sm:rounded-lg sm:border">
      <div className="sticky top-0 z-10 flex items-start justify-between gap-2 border-b bg-background p-4">
        <h2 className="min-w-0 break-words font-medium">{heading}</h2>{close}
      </div>
      {content}
    </aside>
  );
}
