"use client";
import { useEffect, useId, useState } from "react";
import { useFormatter, useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import type { VehicleFilter } from "../filter/vehicleFilter";
import { useVehicleFilterCount } from "../hooks/useVehicleFilterCount";

const SEARCH_DEBOUNCE_MS = 300;

type Props = {
  filter: VehicleFilter;
  urlKey: string;
  nowMs: number;
  onChange: (filter: VehicleFilter) => void;
};

export function VehicleFilterPanel({ filter, urlKey, nowMs, onChange }: Props) {
  const t = useTranslations("VehicleFilter");
  const format = useFormatter();
  const id = useId();
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState({ urlKey, query: filter.query });
  const { total, shown } = useVehicleFilterCount(filter, nowMs);

  // Geri/ileri gezinme bekleyen yerel aramadan üstündür; eski taslak URL'yi geri yazmamalıdır.
  if (draft.urlKey !== urlKey) setDraft({ urlKey, query: filter.query });

  useEffect(() => {
    if (draft.urlKey !== urlKey || draft.query.trim() === filter.query.trim()) return;
    const timer = setTimeout(() => onChange({ ...filter, query: draft.query }), SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [draft, filter, onChange, urlKey]);

  const active = filter.query.trim().length > 0 || !filter.includeStale;
  return (
    <div className="pointer-events-none max-w-full space-y-2">
      <Button className="pointer-events-auto sm:hidden" variant="outline" size="sm"
        aria-expanded={open} aria-controls={id} onClick={() => setOpen(!open)}>
        {t("title")}
        {active && <span className="size-2 rounded-full bg-primary" role="img" aria-label={t("active")} />}
      </Button>
      <div id={id} className={`pointer-events-auto w-80 max-w-full space-y-3 rounded-lg border bg-background/95 p-3 shadow-sm ${open ? "block" : "hidden"} sm:block`}>
        <label htmlFor={`${id}-query`} className="sr-only">{t("search")}</label>
        <Input id={`${id}-query`} type="search" placeholder={t("placeholder")}
          value={draft.query} onChange={(event) => setDraft({ urlKey, query: event.target.value })} />
        <div className="flex items-center justify-between gap-3">
          <label htmlFor={`${id}-stale`} className="text-sm">{t("includeStale")}</label>
          <Switch id={`${id}-stale`} checked={filter.includeStale}
            onCheckedChange={(includeStale) => onChange({ query: draft.query, includeStale })} />
        </div>
        <p role="status" className="text-xs text-muted-foreground">
          {t("count", { total: format.number(total), shown: format.number(shown) })}
        </p>
      </div>
    </div>
  );
}
