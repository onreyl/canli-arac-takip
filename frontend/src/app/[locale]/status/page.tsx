import type { Metadata } from "next";
import { Suspense } from "react";
import { getTranslations } from "next-intl/server";
import { getSystemStatus, SystemStatusLoading, SystemStatusPanel } from "@/features/system-status";
import { apiInternalBaseUrl } from "@/shared/config.server";

export async function generateMetadata({ params }: { params: Promise<{ locale: string }> }): Promise<Metadata> {
  const { locale } = await params;
  const t = await getTranslations({ locale, namespace: "SystemStatus" });
  return { title: t("title") };
}

async function StatusContent() {
  const initialData = await getSystemStatus(apiInternalBaseUrl).catch(() => undefined);
  return <SystemStatusPanel initialData={initialData} initialError={!initialData} />;
}

export default async function StatusPage() {
  // Cache Components açıkken taze veri isteği Suspense altında kalmalıdır.
  return (
    <Suspense fallback={<div className="mx-auto max-w-6xl p-4 sm:p-6"><SystemStatusLoading /></div>}>
      <StatusContent />
    </Suspense>
  );
}
