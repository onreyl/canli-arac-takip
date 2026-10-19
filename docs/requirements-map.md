# Gereksinim → Kod Haritası

Case dokümanının (fullstack_case_study.pdf) her zorunlu maddesi için: hangi dosya/sınıf karşılıyor, nasıl karşılıyor.
Mülakat çalışması için. Yollar `backend/src/VehicleTracking.*` ve `frontend/src` altına göredir.

Kısaltmalar: **Api** = `VehicleTracking.Api`, **App** = `VehicleTracking.Application`, **Infra** = `VehicleTracking.Infrastructure`,
**Domain** = `VehicleTracking.Domain`.

---

## 0. Genel stack (sayfa 1)

| Madde | Nerede | Nasıl |
|---|---|---|
| ASP.NET Core (.NET 8+) | `Api/VehicleTracking.Api.csproj` | .NET 10, Web SDK. |
| PostgreSQL + EF Core (Npgsql) | `Infra/Persistence/VehicleTrackingDbContext.cs`, `Infra/DependencyInjection.cs` | EF Core + Npgsql provider; tek `NpgsqlDataSource` (tek connection pool) EF ve toplu yazıcı arasında paylaşılır. |
| Zaman serisi eklentisi + "neden seçtin" | `docker-compose.yml` (timescaledb imajı), `Infra/Persistence/DatabaseInitializer.cs` | TimescaleDB hypertable + retention policy. Neden: eski veriyi satır satır `DELETE` yerine chunk bütün olarak düşürülür (bkz. 3.1). |
| SignalR veya WebSocket | `Api/Realtime/VehiclesHub.cs` | SignalR hub, `/hubs/vehicles`. |
| Next.js App Router | `frontend/src/app/[locale]/...` | `app/` klasörü, `[locale]` segmenti ile. |
| TypeScript strict | `frontend/tsconfig.json` | `"strict": true`. |
| TanStack Query | `shared/providers.tsx`, `features/vehicle-detail/hooks/useVehicleHistory.ts`, `features/system-status/components/SystemStatusPanel.tsx` | Sunucudan gelen REST verisi (geçmiş, sistem durumu). |
| Zustand | `features/live-map/store/liveVehiclesStore.ts` | Canlı araç listesi + bağlantı durumu (istemci tarafı canlı state). |
| Tailwind + shadcn/ui | `app/globals.css`, `components/ui/*`, `components.json` | Tailwind 4; shadcn bileşenleri (button, badge, card, input, skeleton, switch). |
| next-intl | `i18n/*`, `messages/tr.json`, `messages/en.json`, `proxy.ts` | Bkz. 4.4. |
| zod | `shared/contracts/*.ts`, `shared/api/http.ts`, `shared/realtime/vehicleHub.ts` | Sunucudan gelen her mesaj (REST ve SignalR) zod şemasıyla doğrulanır. |
| MapLibre GL + OSM tabanlı altlık | `features/live-map/components/LiveMap.tsx` | MapLibre, OpenFreeMap "liberty" stili (OSM verisi). |
| npm | `frontend/package-lock.json` | — |

## Mimari (8. bölüm, eleme kriterleri)

| Madde | Nerede | Nasıl |
|---|---|---|
| Backend katman ayrımı | `Domain` → `Application` → `Infrastructure` → `Api` (csproj referansları) | Domain hiçbir şeye bağlı değil. Application sadece Domain'e bağlı ve interface tanımlar (`IFeedSource`, `IPositionWriter`, `IFeedBuffer`, `IVehicleHistoryReader`, `ILiveUpdatePublisher`). Infrastructure bunları uygular (Entur, Npgsql, EF). Api hepsini bir araya getirir (DI, controller, hub, worker). |
| Protobuf dışarı sızmaz | `Infra/VehicleTracking.Infrastructure.csproj` (`Access="Internal"`), `Infra/Feeds/GtfsRealtimeMapper.cs` | Üretilen protobuf sınıfları `internal`; dışarıya sadece Domain'in `VehiclePosition` tipi çıkar. Kuralı derleyici zorlar. |
| Entity dışarı açılmaz (DTO) | `Api/Realtime/LiveMessages.cs` (`LiveVehicleDto`, `LiveSnapshotMessage`, `LiveUpdateMessage`), `Api/Controllers/VehiclesController.cs` (`HistoryPointDto`, `HistoryPageResponse`), `Api/Controllers/SystemController.cs` (`SystemStatusResponse`) | `VehiclePosition` hiçbir zaman doğrudan JSON olmaz; her uç nokta kendi record DTO'sunu döner. |
| İş mantığı controller'da değil | `App/Ingestion/FeedIngestor.cs`, `App/Live/LiveVehicleState.cs`, `App/Ingestion/IngestionMetrics.cs`, `Infra/Persistence/EfVehicleHistoryReader.cs` | Controller'lar sadece parametre doğrular ve DTO'ya çevirir. "Hangi konum yeni?", "canlı durum nasıl değişti?", "sayfa nasıl çekilir?" kararları Application/Infrastructure'da. |
| Frontend feature bazlı yapı | `features/live-map`, `features/vehicle-detail`, `features/system-status`, ortaklar `shared/` | Her feature kendi bileşen/hook/store'unu taşır, dışarıya `index.ts` üzerinden açar. |
| Sunucu / istemci veri ayrımı | TanStack Query (REST: geçmiş, sistem durumu) ↔ Zustand (SignalR: canlı araçlar) | Bir kez çekilip önbelleğe alınan veri Query'de; sürekli akan, push edilen veri Zustand'da. |

---

## 3.1 Veri toplama, saklama ve silme

| Madde | Nerede | Nasıl |
|---|---|---|
| Akış 3 sn'de bir okunur; aralık ve adres config'ten | `Api/appsettings.json` (`Ingestion:PollInterval`, `Entur:VehiclePositionsUrl`), `App/Ingestion/IngestionOptions.cs`, `Infra/Feeds/EnturOptions.cs`, `Api/Workers/FeedPollingWorker.cs` | `PeriodicTimer(PollInterval)`; bir tur uzun sürerse turlar üst üste binmez. Adres `EnturOptions`'tan, açılışta doğrulanır (`ValidateOnStart`). |
| Uygulama açılınca kendiliğinden başlar, kesintisiz çalışır | `FeedPollingWorker` (`BackgroundService`), `Program.cs` (`AddHostedService`) | Host açılınca başlar. Kaynak hatası (`FeedUnavailableException`) sayılır, tur atlanır; beklenmeyen hata da yakalanır, döngü ölmez. |
| Konumlar kalıcı yazılır | `Api/Workers/PositionWriterWorker.cs`, `Infra/Persistence/NpgsqlPositionWriter.cs` | Toplu yazma, `unnest` + `ON CONFLICT DO NOTHING` (tekrar güvenli). EF `SaveChanges` yerine doğrudan Npgsql: ON CONFLICT desteği ve hız için. |
| Son 3 saat tutulur, eskisi otomatik silinir; süre config'ten | `Infra/Persistence/DatabaseInitializer.cs`, `App/Storage/StorageOptions.cs`, `appsettings.json` (`Storage:Retention`) | Açılışta `add_retention_policy(drop_after => Retention)` kurulur; TimescaleDB her `RetentionCheckInterval`'da eski chunk'ları düşürür. Süre değişirse yeniden açılış yeter. |
| Silme yazmayı/yayını/sorguları kilitlemez | Aynı + `Migrations/*InitialCreate.cs` (hypertable) | Silme = chunk (alt tablo) düşürmek, satır satır `DELETE` değil; tablo kilidi ve VACUUM yükü yok. Canlı yayın zaten DB'den bağımsız (bellekten). Kanıt: sistem durumundaki yazma süreleri (`lastWriteDurationMs`, `maxWriteDurationLastHourMs`) silme anında sıçramıyor (`docs/resilience-tests.md`). |
| 3 saat beklemeden gösterilebilir | `docker-compose.yml` (`RETENTION`, `CHUNK_INTERVAL` ortam değişkenleri) | Örn. `RETENTION=00:05:00 CHUNK_INTERVAL=00:01:00` ile birkaç dakikada silme görülür. |
| Saatlik / 3 saatlik veri hacmi README'de | `docs/observations.md` (ölçümler), README (yazılacak) | Gerçek veriyle ölçülen satır ve disk boyutu. |

## 3.2 Canlı yayın ve REST

| Madde | Nerede | Nasıl |
|---|---|---|
| İstemci WebSocket ile abone olur | `Api/Realtime/VehiclesHub.cs` | Bağlanan her bağlantı `LiveUpdateDispatcher.AddConnection` ile kaydolur. |
| Önce güncel durum, sonra değişiklikler | `Api/Realtime/LiveClientFeed.cs` (`_needsSnapshot`), `App/Live/LiveVehicleState.cs` | Her istemcinin ilk mesajı `snapshot` (tam liste), sonrakiler `update` (upserts + removals). Önce kayıt, sonra snapshot: arada gelen değişiklik kaybolmaz (en kötü ihtimal iki kez gelir, zararsız). |
| Yeniden bağlanan kayıpsız güncel duruma döner | Aynı + `frontend/src/shared/realtime/vehicleHub.ts` (`hasSnapshot`) | Yeniden bağlanma = yeni bağlantı = yeni snapshot. İstemci, yeni snapshot gelene kadar update'leri uygulamaz; snapshot elindekinin yerine geçer. |
| Yavaş istemci diğerlerini etkilemez | `Api/Realtime/LiveUpdateDispatcher.cs` | Her istemcinin kendi gönderim döngüsü var (`RunSendLoopAsync`); biri yavaşsa sadece kendi döngüsü bekler. |
| Yavaş istemci veri toplamayı etkilemez | `LiveUpdateDispatcher.Publish`, `LiveClientFeed.Merge` | `Publish` hiç beklemez: sadece her istemcinin bekleyen sözlüğüne yazar. Sözlük araç kimliğine göre birleştirir (coalescing), yani yavaş istemcinin bekleyeni araç sayısını (~3.000) aşamaz; bellek şişmez. |
| REST: araçların anlık durumu | `Api/Controllers/VehiclesController.cs` → `GET /api/vehicles` | `LiveVehicleState` snapshot'ı (bellekten). |
| REST: araç geçmişi, son 3 saat, zaman aralığı, sayfalı, hızlı | `VehiclesController.GetHistory` → `GET /api/vehicles/{id}/history`, `Infra/Persistence/EfVehicleHistoryReader.cs`, `Api/Controllers/HistoryCursorCodec.cs` | `from`/`to` filtresi, pencere dışı reddedilir. Keyset sayfalama: `(vehicle_id, measured_at DESC)` index'i üzerinden, her sayfa aynı hızda; imleç opak base64 metin. |
| REST: sistem durumu (son okuma, son 1 saatte yazılan, başarısız deneme, bağlı istemci) | `Api/Controllers/SystemController.cs` → `GET /api/system/status`, `App/Ingestion/IngestionMetrics.cs` | Dört zorunlu alan + teşhis için ek sayaçlar (tampon, kayıp parti, yazma süresi). Son 1 saat = kayan pencere kuyruğu. |
| Swagger'dan test edilebilir | `Program.cs` (`AddOpenApi`, `MapOpenApi`, `UseSwaggerUI`) | `/swagger`, her ortamda açık; XML yorumları Swagger açıklaması olur. |

## 3.3 Veri bütünlüğü ve dayanıklılık

Ayrıntılı senaryolar ve deney sonuçları: `docs/resilience-tests.md`, bilinen sınırlar: `docs/known-limitations.md`.

| Senaryo | Nerede | Verinin akıbeti |
|---|---|---|
| Kaynak birkaç dk erişilemez / bozuk yanıt | `Infra/Feeds/EnturFeedSource.cs`, `FeedPollingWorker` | Hata `FeedUnavailableException`'a çevrilir, sayılır, tur atlanır. O aradaki ara konumlar kaynakta zaten tutulmadığı için **kaybolur** (önlenemez, yazılı). Kaynak dönünce feed son 3 saatin son konumlarını getirdiği için her aracın son durumu geri gelir. |
| DB birkaç dk erişilemez | `App/Ingestion/IFeedBuffer.cs`, `Infra/Buffering/InMemoryFeedBuffer.cs`, `PositionWriterWorker.WriteUntilDoneAsync` | Çekme ve yazma ayrı; partiler bellek tamponunda bekler, yazıcı aynı partiyi 1→10 sn artan beklemeyle tekrar dener. Harita etkilenmez (canlı yayın bellekten). Tampon ~50 dk'lık; dolarsa en eski parti atılır ve sayılır. |
| Tekrar denenen yazmada çift kayıt | `NpgsqlPositionWriter` (`ON CONFLICT DO NOTHING`), birincil anahtar `(entity_id, measured_at)` | Yazma idempotent; yarım kalan deneme tekrarlanabilir. |
| Uygulama yeniden başlatılır | `PositionWriterWorker.DrainAsync`, `Program.cs` (kayıt sırası, `ShutdownTimeout` 25 sn), `docker-compose.yml` (`stop_grace_period: 30s`) | Düzgün kapanışta önce çekici durur, yazıcı tamponu boşaltır. Ani ölümde (kill -9) tampondakiler kaybolur (bilinen sınır, yazılı). |
| WebSocket istemcisi kopar | `vehicleHub.ts` (`withAutomaticReconnect` + kendi 5 sn'lik tekrar denemesi), `LiveClientFeed` | Geri gelince yeni snapshot; arada kaçan hiçbir şey önemli değil çünkü snapshot güncel durumun tamamı. |
| WebSocket istemcisi çok yavaş okur | `LiveUpdateDispatcher`, `LiveClientFeed` | Bkz. 3.2: birleştirme sayesinde sadece o istemci ara adımları atlar, son durumu yine alır. |

---

## 4.1 Canlı harita

| Madde | Nerede | Nasıl |
|---|---|---|
| Araçlar haritada, WebSocket ile anlık güncellenir | `features/live-map/hooks/useLiveVehicleStream.ts`, `store/liveVehiclesStore.ts`, `components/LiveMap.tsx` | Hub mesajları store'a (`applySnapshot` / `applyUpdate`), harita store'a abone. |
| Açılışta güncel durum hemen, sonra canlı | Backend'in ilk mesajı snapshot (3.2) | İlk mesajla bütün araçlar çizilir. |
| Binlerce araçta akıcı | `LiveMap.tsx` | Araçlar tek bir GeoJSON source + `circle` layer (WebGL), DOM marker değil. Güncellemeler `requestAnimationFrame` ile kare başına bire indirilir. Harita React render döngüsünün dışında, store'a doğrudan abone. |
| Bağlantı durumu görünür (bağlı / yeniden bağlanıyor / koptu) | `components/ConnectionBadge.tsx`, `vehicleHub.ts` (`status` olayı) | Dört durum: connecting / connected / reconnecting / disconnected. Koptu ise uyarı + "yeniden dene" butonu (`LiveMapScreen.tsx`). |
| Otomatik yeniden bağlanma, ekran güncel duruma gelir | `vehicleHub.ts` | SignalR `withAutomaticReconnect` + o da pes ederse 5 sn'de bir kendi denemesi; yeni bağlantıda snapshot store'u tamamen değiştirir. |
| Hat / araç no ile arama-filtre | `filter/vehicleFilter.ts` (`matchesFilter`, `routeLabel`), `components/VehicleFilterPanel.tsx` | Araç kimliği, hat kimliği ve sadeleştirilmiş hat numarasında (ör. `RUT:Line:31` → `31`) arar. |
| Filtre URL'de | `vehicleFilter.ts` (`parseFilter`, `filterToParams`), `LiveMapScreen.tsx` (`router.replace`) | `?q=31&all=1`. Sayfa yenilenince veya link paylaşılınca aynı filtre. |

## 4.2 Araç detayı ve geçmişi

| Madde | Nerede | Nasıl |
|---|---|---|
| Tıklayınca panel: hat, sefer, durum, doluluk, son güncelleme | `features/vehicle-detail/components/VehicleDetailPanel.tsx` (`LiveDetails`) | Seçili araç URL'de (`?vehicle=...`). |
| Panel açıkken canlı güncellenir | `features/live-map/index.ts` (`useLiveVehicle`) | Panel Zustand store'daki aracı okur; her update'te yeniden çizilir. |
| Son 3 saatten aralık seçilir, sunucu taraflı sayfalı liste | `features/vehicle-detail/hooks/useVehicleHistory.ts`, `range.ts` | `useInfiniteQuery` + sunucunun `nextCursor`'u; "daha fazla yükle". Pencerenin bitişi ilk sayfada sabitlenir, sonraki sayfalar aynı pencereden gelir. |
| Haritada iz | `historyCoordinates` → `LiveMap.tsx` (`trail` line layer) | Yüklenen sayfalar zamana göre sıralanıp çizgi olarak çizilir. |

## 4.3 Sistem durumu

| Madde | Nerede | Nasıl |
|---|---|---|
| Son okuma, yazılan kayıt, hata sayısı, bağlı istemci; periyodik güncelleme | `features/system-status/components/SystemStatusPanel.tsx`, `api/getSystemStatus.ts`, `app/[locale]/status/page.tsx` | `useQuery` + `refetchInterval: 5000`. |

## 4.4 Genel

| Madde | Nerede | Nasıl |
|---|---|---|
| Her ekranda yükleniyor / boş / hata / başarılı | Harita: `LiveMapScreen.tsx` (loading, empty, filteredEmpty, disconnected). Detay: `VehicleDetailPanel.tsx` (canlı ve geçmiş için ayrı ayrı). Durum: `SystemStatusPanel.tsx` (skeleton, empty, error, kartlar). | Her ekran dört durumu da açıkça çizer. |
| TR + EN, metin koda gömülü değil | `messages/tr.json`, `messages/en.json`, `i18n/routing.ts`, `i18n/request.ts`, `proxy.ts`, `shared/ui/LocaleSwitcher.tsx` | `/tr/...`, `/en/...`; bütün metinler `useTranslations` ile. |
| 375px mobil | `VehicleDetailPanel.tsx` (mobilde alt yarı panel), `VehicleFilterPanel.tsx` (mobilde açılır filtre), Tailwind `sm:` kırılımları | — |

---

## 5. Teslim

| Madde | Nerede | Durum |
|---|---|---|
| Tek public repo, `/backend`, `/frontend` | Repo kökü | ✅ |
| `docker compose up` ile DB + backend + frontend; backend hemen toplar | `docker-compose.yml`, `backend/Dockerfile`, `frontend/Dockerfile` | ✅ DB healthcheck → backend; `DatabaseInitializer` migration'ı kendisi uygular. |
| README: çalıştırma, mimari + veri akışı diyagramı, sorumluluklar, video linki, 5 soru | `docs/architecture.md`, `docs/observations.md`, `docs/resilience-tests.md`, `docs/known-limitations.md` (kaynak) | ⏳ README henüz yok (24-25 Ekim). |
| Ölçülen sayılar: okuma döngüsü süresi, saatlik kayıt, arayüzde güncelleme işleme süresi | `lastFetchDurationMs` + `writtenLastHour` (sistem durumu), `lastUpdateDurationMs` (`liveVehiclesStore.ts` + `LiveMap.tsx`, `ConnectionBadge`'de gösterilir) | Ölçüm araçları hazır; sayılar README'ye yazılacak. |
| Conventional Commits, repoda gerçek sır yok | git geçmişi, `.env.example`, user-secrets | ✅ |
