"use client";
import { useQuery } from "@tanstack/react-query";
import { useFormatter, useNow, useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Link } from "@/i18n/navigation";
import { LocaleSwitcher } from "@/shared/ui/LocaleSwitcher";
import { getSystemStatus } from "../api/getSystemStatus";

function LastFetch({ value }: { value: string }) {
  const t = useTranslations("SystemStatus");
  const format = useFormatter();
  // Saniyelik saat bütün paneli yeniden çizdirmemelidir.
  const now = useNow({ updateInterval: 1000 });
  const date = new Date(value);
  const stale = now.getTime() - date.getTime() > 30_000;
  return (
    <div className={stale ? "space-y-1 text-amber-800 dark:text-amber-300" : "space-y-1"}>
      <p>{format.relativeTime(date, { now, unit: "second", style: "short" })}</p>
      <time dateTime={value} className="text-sm">
        {format.dateTime(date, { dateStyle: "short", timeStyle: "medium" })}
      </time>
      {stale && <p className="text-sm">{t("stale")}</p>}
    </div>
  );
}

export function SystemStatusLoading() {
  const t = useTranslations("SystemStatus");
  return (
    <div role="status" className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
      <span className="sr-only">{t("loading")}</span>
      {Array.from({ length: 9 }, (_, index) => <Skeleton key={index} className="h-28 w-full" />)}
    </div>
  );
}

export function SystemStatusPanel() {
  const t = useTranslations("SystemStatus");
  const navigation = useTranslations("Navigation");
  const format = useFormatter();
  const query = useQuery({
    queryKey: ["system-status"],
    queryFn: ({ signal }) => getSystemStatus(signal),
    refetchInterval: 5000,
  });
  const data = query.data;
  const hasError = query.isError;
  const counters = [
    "writtenLastHour", "failedFetchCount", "failedWriteCount", "droppedBatchCount",
    "connectedClients", "pendingBatches", "liveVehicles",
  ] as const;

  return (
    <main className="mx-auto max-w-6xl space-y-6 p-4 sm:p-6">
      <header className="space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <Link href="/" className="rounded-md py-2 text-sm underline underline-offset-4">{navigation("backToMap")}</Link>
          <LocaleSwitcher />
        </div>
        <h1 className="text-2xl font-semibold">{t("title")}</h1>
      </header>
      {hasError && <div role="alert" className="space-y-2 rounded-lg border border-red-200 bg-red-50 p-4 text-red-950">
        <p>{data ? t("updateError") : t("error")}</p>
        <Button variant="outline" disabled={query.isFetching} onClick={() => void query.refetch()}>{t("retry")}</Button>
      </div>}
      {query.isPending && !hasError && <SystemStatusLoading />}
      {data && <>
        {data.lastSuccessfulFetchAt === null && <p role="status" className="rounded-lg border p-4">{t("empty")}</p>}
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <Card>
            <CardHeader><CardTitle><h2>{t("lastSuccessfulFetchAt")}</h2></CardTitle></CardHeader>
            <CardContent className="text-lg tabular-nums">
              {data.lastSuccessfulFetchAt === null ? t("unknown") : <LastFetch value={data.lastSuccessfulFetchAt} />}
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle><h2>{t("lastFetchDurationMs")}</h2></CardTitle></CardHeader>
            <CardContent className="text-2xl tabular-nums">
              {data.lastFetchDurationMs === null ? t("unknown") : t("milliseconds", { value: format.number(data.lastFetchDurationMs, { maximumFractionDigits: 1 }) })}
            </CardContent>
          </Card>
          {counters.map((key) => (
            <Card key={key} className={key === "droppedBatchCount" && data[key] > 0 ? "bg-red-50 text-red-950 ring-red-300" : undefined}>
              <CardHeader><CardTitle><h2>{t(key)}</h2></CardTitle></CardHeader>
              <CardContent className="text-2xl tabular-nums">{format.number(data[key])}</CardContent>
            </Card>
          ))}
        </div>
      </>}
    </main>
  );
}
