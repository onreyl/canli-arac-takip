"use client";
import { useInfiniteQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError, fetchJson } from "@/shared/api/http";
import { historyPageSchema, type HistoryPage } from "@/shared/contracts/history";
import { rangeDurationMs, type HistoryRange } from "../range";

// Sonraki sayfayı istemek için gereken her şey: sunucunun imleci + pencerenin sabitlenmiş bitişi.
type NextPage = { cursor: string; windowEnd: number };
type WindowPage = HistoryPage & { windowEnd: number };

export function historyCoordinates(pages: HistoryPage[]): [number, number][] {
  // Sunucu yeniden eskiye sayfalar; çizgi bütün sayfalarda ölçüm sırasını izlemelidir.
  return pages.flatMap((page) => page.items).sort((a, b) => Date.parse(a.measuredAt) - Date.parse(b.measuredAt))
    .map((point) => [point.longitude, point.latitude]);
}

export function useVehicleHistory(vehicleId: string | null, range: HistoryRange) {
  const client = useQueryClient();
  const queryKey = ["vehicle-history", vehicleId, range] as const;

  const query = useInfiniteQuery({
    queryKey,
    enabled: !!vehicleId,
    initialPageParam: null as NextPage | null,
    // Sabit bir geçmiş penceresinin verisi değişmez; arka planda yeniden çekmeye gerek yok. Yeni pencere = "yenile".
    staleTime: Infinity,
    // 400 (geçersiz aralık) tekrar denense de düzelmez.
    retry: (count, error) => !(error instanceof ApiError && error.status === 400) && count < 3,
    queryFn: async ({ pageParam, signal }): Promise<WindowPage> => {
      // Pencerenin bitişi ("şimdi") İLK sayfa çekilirken sabitlenir, sonraki sayfalar imleçle birlikte devralır.
      // Her sayfada yeniden Date.now() alsaydık imleçli sayfalar farklı pencerelere ait olurdu.
      const windowEnd = pageParam?.windowEnd ?? Date.now();
      const params = new URLSearchParams({
        from: new Date(windowEnd - rangeDurationMs[range]).toISOString(),
        to: new Date(windowEnd).toISOString(),
        pageSize: "100",
      });
      if (pageParam) params.set("cursor", pageParam.cursor);
      const page = await fetchJson(
        `/api/vehicles/${encodeURIComponent(vehicleId!)}/history?${params}`, historyPageSchema, { signal });
      return { ...page, windowEnd };
    },
    getNextPageParam: (page): NextPage | null =>
      page.nextCursor ? { cursor: page.nextCursor, windowEnd: page.windowEnd } : null,
  });

  // Yenile: sorguyu sıfırla → ilk sayfa yeni bir "şimdi" ile çekilir.
  const refresh = () => client.resetQueries({ queryKey, exact: true });
  return { ...query, refresh };
}
