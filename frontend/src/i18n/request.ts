import { hasLocale } from "next-intl";
import { getRequestConfig } from "next-intl/server";
import { locale as getRootLocale } from "next/root-params";
import { routing } from "./routing";

export default getRequestConfig(async ({ locale: override }) => {
  const requested = override ?? await getRootLocale();
  const locale = hasLocale(routing.locales, requested) ? requested : routing.defaultLocale;
  return {
    locale,
    messages: (await import(`../../messages/${locale}.json`)).default,
    // Tüm saatler tek bir dilimde: verinin ve senaryodaki ulaşım otoritesinin yerel saati (Oslo). Tek yerde tanımlı olunca
    // sunucuda üretilen HTML ile tarayıcıdaki gösterim aynı olur (aksi halde hydration uyuşmazlığı ve farklı ekranlarda farklı saat).
    timeZone: "Europe/Oslo",
  };
});
