"use client";
import { useLocale, useTranslations } from "next-intl";
import { useSearchParams } from "next/navigation";
import { Link, usePathname } from "@/i18n/navigation";
import { routing } from "@/i18n/routing";

export function LocaleSwitcher() {
  const t = useTranslations("Navigation");
  const locale = useLocale();
  const pathname = usePathname();
  const params = useSearchParams().toString();
  return (
    <nav aria-label={t("language")} className="flex items-center gap-1">
      {routing.locales.map((value) => (
        <Link key={value} href={`${pathname}${params ? `?${params}` : ""}`} locale={value}
          aria-current={locale === value ? "page" : undefined}
          className={`rounded-md px-2 py-2 text-sm focus-visible:outline-2 ${locale === value ? "bg-primary text-primary-foreground" : "hover:bg-muted"}`}>
          {t(value)}
        </Link>
      ))}
    </nav>
  );
}
