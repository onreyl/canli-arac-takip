import type { Metadata } from "next";
import { Suspense } from "react";
import { getTranslations } from "next-intl/server";
import { SystemStatusLoading, SystemStatusPanel } from "@/features/system-status";

export async function generateMetadata({ params }: { params: Promise<{ locale: string }> }): Promise<Metadata> {
  const { locale } = await params;
  const t = await getTranslations({ locale, namespace: "SystemStatus" });
  return { title: t("title") };
}

export default function StatusPage() {
  // Cache Components açıkken URL'yi okuyan bileşen (dil değiştirici, useSearchParams) Suspense altında olmalıdır.
  return (
    <Suspense fallback={<div className="mx-auto max-w-6xl p-4 sm:p-6"><SystemStatusLoading /></div>}>
      <SystemStatusPanel />
    </Suspense>
  );
}
