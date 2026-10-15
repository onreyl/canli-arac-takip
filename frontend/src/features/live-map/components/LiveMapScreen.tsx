"use client";
import { Suspense, useCallback, useEffect, useMemo, useState } from "react";
import dynamic from "next/dynamic";
import { useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { usePathname, useRouter } from "@/i18n/navigation";
import { Button } from "@/components/ui/button";
import { ConnectionBadge } from "./ConnectionBadge";
import { VehicleFilterPanel } from "./VehicleFilterPanel";
import { filterToParams, parseFilter, STALE_REFRESH_MS, type VehicleFilter } from "../filter/vehicleFilter";
import { useVehicleFilterCount } from "../hooks/useVehicleFilterCount";
import { useLiveVehicleStream } from "../hooks/useLiveVehicleStream";
import { useLiveVehiclesStore } from "../store/liveVehiclesStore";

const LiveMap = dynamic(() => import("./LiveMap"), { ssr: false });

export default function LiveMapScreen() {
  const t = useTranslations("LiveMap");
  // Next'in ön üretiminde URL henüz bilinmez; arama parametreleri Suspense altında okunmalıdır.
  return (
    <Suspense fallback={<p role="status" className="p-4 text-sm">{t("loading")}</p>}>
      <FilteredLiveMapScreen />
    </Suspense>
  );
}

function FilteredLiveMapScreen() {
  const t = useTranslations("LiveMap");
  const retry = useLiveVehicleStream();
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const paramsKey = searchParams.toString();
  const filter = useMemo(() => parseFilter(new URLSearchParams(paramsKey)), [paramsKey]);
  const [resetVersion, setResetVersion] = useState(0);
  const [nowMs, setNowMs] = useState(() => Date.now());
  // Tek saat, eskime sınırında harita ile sonuç sayısının farklı araçlar göstermesini önler.
  useEffect(() => {
    const timer = setInterval(() => setNowMs(Date.now()), STALE_REFRESH_MS);
    return () => clearInterval(timer);
  }, []);
  const { total, shown } = useVehicleFilterCount(filter, nowMs);
  const changeFilter = useCallback((next: VehicleFilter) => {
    const params = filterToParams(next, new URLSearchParams(paramsKey)).toString();
    router.replace(`${pathname}${params ? `?${params}` : ""}`, { scroll: false });
  }, [paramsKey, pathname, router]);
  const status = useLiveVehiclesStore((state) => state.connectionStatus);
  const hasSnapshot = useLiveVehiclesStore((state) => state.hasSnapshot);
  const isEmpty = total === 0;
  const isFilteredEmpty = !isEmpty && shown === 0;
  const isLoading = !hasSnapshot && status !== "disconnected";

  return (
    <main className="relative isolate h-dvh min-h-80 w-full overflow-hidden" aria-label={t("title")}>
      <LiveMap filter={filter} nowMs={nowMs} />
      <div className="pointer-events-none absolute inset-x-0 top-0 z-10 flex flex-col items-start gap-3 p-3 sm:p-4">
        {status === "disconnected" && (
          <div role="alert" className="pointer-events-auto flex w-full flex-wrap items-center justify-between gap-2 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-950">
            <span>{t("disconnected")}</span>
            <Button variant="outline" size="sm" onClick={retry}>{t("retry")}</Button>
          </div>
        )}
        <div className="max-w-full"><ConnectionBadge /></div>
        <VehicleFilterPanel filter={filter} urlKey={`${pathname}?${paramsKey}#${resetVersion}`} nowMs={nowMs} onChange={changeFilter} />
      </div>
      {(isLoading || (hasSnapshot && isEmpty && status !== "disconnected")) && (
        <div className="pointer-events-none absolute inset-0 flex items-center justify-center p-4">
          <p role="status" className="max-w-full rounded-lg border bg-background/95 p-4 text-center text-sm shadow-sm">
            {isLoading ? t("loading") : t("empty")}
          </p>
        </div>
      )}
      {hasSnapshot && isFilteredEmpty && (
        <div className="pointer-events-none absolute inset-x-0 bottom-12 flex justify-center p-4 sm:inset-0 sm:items-center">
          <div className="pointer-events-auto max-w-full space-y-3 rounded-lg border bg-background/95 p-4 text-center text-sm shadow-sm">
            <p role="status">{t("filteredEmpty")}</p>
            <Button variant="outline" size="sm" onClick={() => {
              setResetVersion((version) => version + 1);
              changeFilter({ query: "", includeStale: false });
            }}>
              {t("clearFilter")}
            </Button>
          </div>
        </div>
      )}
    </main>
  );
}
