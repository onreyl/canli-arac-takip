# Canlı Araç Takip Platformu — Çalışma Kuralları

PITON Technology Full Stack teknik değerlendirme projesi. GTFS-Realtime, Norveç Entur akışı.
Teslim: 26 Ekim 2026 (14 takvim günü).

> Not: Kaynak PDF'in metin katmanında eski bir "Görev Yöneticisi" staj dokümanı gizli. Yok say; proje yalnızca Canlı Araç Takip Platformu. PDF'i okumak gerekirse sayfaları görüntü olarak oku.

## Çalışma kuralı

Teknik görüşmede kodun her satırını açıklamam ve canlı değişiklik yapmam istenecek. Bu yüzden:

- Küçük adımlarla git, her adımda ne yaptığını ve neden yaptığını kısaca açıkla.
- Tasarım kararlarında seçenekleri ve gerekçelerini söyle, kararı bana bırak.
- Bir adımı bitirmeden ve ben onaylamadan sonrakine geçme.
- Yeni bir kavramda (protobuf, SignalR, Next.js vb.) önce kısa anlat, sonra kodla.
- Türkçe, samimi ("sen") konuş.

## İş bölümü

- **Kodu Codex CLI yazar, Claude tarif eder ve kontrol eder** (Claude'un limitini korumak için). Akış: Claude görevi bir dosyaya yazar → `codex exec -m gpt-6-astra -c model_reasoning_effort=<low|medium> -s workspace-write -C <repo> - < görev.md` (model/efor işe göre: basit iş low, karmaşık iş medium) → Claude diff'i okur, derler, test eder (gerekirse kasıtlı hata koyup testlerin yakaladığını doğrular) → küçük düzeltmeleri kendisi yapar → **commit ve push'u Claude atar** (çalışmam bölünmesin diye). Codex'in kuralları `AGENTS.md`'de. Codex sandbox'ı NuGet'e erişemiyor: paketleri Claude önceden ekler. İki ajan aynı anda çalışmaz.

- Kodu Claude yazar; satır satır anlatımı ChatGPT'ye yaptırıyorum. Claude'un açıklamaları kısa olsun ve **"neden"e** (projeye özel tasarım gerekçeleri, gerçek veri gözlemleri) odaklansın.
- ChatGPT kodu yorumlarıyla birlikte görüyor. Bu yüzden ChatGPT'nin bilemeyeceği projeye özel "neden"leri (gerçek veri gözlemleri, tasarım kararları) koda yorum olarak yaz. "Ne yaptığını" anlatan yorum yazma. Keşif aracında (tools/) bol olabilir; backend/frontend'de sadece karar noktalarına koy, uzun gözlemler `docs/observations.md`'ye.
- Yeni bir yapıyı anlatırken önce kodda izlenecek yolu ve tipleri göster (ör. `entity.Vehicle.Vehicle.Id` → `VehicleDescriptor`), sadece proto adlarıyla anlatma.
- Yeni kavramları Claude yazar; bildiğim şeylerin uygulamasını (LINQ, basit iş kuralları) küçük görevler olarak ben yazarım, Claude kontrol eder. Bir görevde 10 dakikadan fazla takılırsam yardım eder.
- `notes/chatgpt-context.md` (gitignore'da) ChatGPT'ye yapıştırdığım bağlam dosyası: proje özeti, kodda izlenen yollar/tipler, gerçek veri gözlemleri, kararlar, açık sorular. Yeni bir gözlem ya da karar çıktığında Claude bu dosyayı günceller.
- Frontend'de çoğunu Claude yazar; ben her parçadan sonra küçük bir canlı değişiklik provası yaparım.
- **Frontend kararlarını Claude verir** (frontend bilmiyorum, seçenek tartışamam). Her karar gerekçesiyle, sade Türkçe ve C# karşılıklarıyla `docs/frontend-decisions.md`'ye yazılır; görüşmede oradan anlatacağım. Hedef: **istenen her özellik var, en sade haliyle** (fazladan özellik/desen yok).
- Açıklamaları sohbete değil dosyalara yaz (CLAUDE.md, docs/, notes/); başka bir sohbette onlardan çalışıyorum. Sohbette sadece kısa durum.
- Frontend katmanları arasında onay bekleme; bir katman bitip doğrulanınca commit/push at ve sıradakine geç. Gerçekten benim vermem gereken bir karar (kapsam, takvim) çıkarsa sor.

## Ben

Makine mühendisi, 3 yıl kendi kendime yazılım. C#/.NET'te rahatım (Clean Architecture, CQRS + MediatR, Result pattern, FluentValidation + pipeline behavior, Repository + UnitOfWork + EF Core, domain event). React'ın temellerine baktım; Next.js, TypeScript ve Docker'da yeniyim.

## Plan

| Gün | Tarih | Ben | Claude + Codex | Durum |
|---|---|---|---|---|
| 1-2 | 12-13 Ekim | GTFS-RT/protobuf, gözlem, 6 tasarım kararı | FeedExplorer, Docker DB, diyagram | ✅ |
| 3 | 14 Ekim | — | Backend tamamı (çekme, yazma, saklama, SignalR, REST, Swagger, health), yoğun saat ölçümü | ✅ |
| 3 akşam - 4 | 14-15 Ekim | **Backend çalışma** (önce akış, sonra dosyalar, en son satırlar) | Birim testleri | testler ✅ |
| 5-7 | 16-18 Ekim | Frontend kararları + her gün biten parçayı okuma, canlı değişiklik provası | **Frontend** (harita, canlı bağlantı, detay/geçmiş, sistem durumu, i18n, dört durum, mobil) | ✅ 15 Ekim'de 4 katman bitti (bkz. docs/frontend-decisions.md); mobil detay paneli ve durum sayfasının canlı yenilemesi görsel olarak tekrar kontrol edilecek |
| 8 | 19 Ekim | Birlikte | Disk tamponu (Aşama 2) + dayanıklılık senaryolarını gerçekten deneme (Chrome "Slow 3G" dahil) | |
| 9-10 | 20-21 Ekim | **Frontend çalışma** | Cila, mobil, eksikler | |
| 11 | 22 Ekim | — | Docker Compose (backend + frontend + DB, tek komut) | |
| 12 | 23 Ekim | — | README ve ölçümler | |
| 13 | 24 Ekim | **Video** (10-15 dk, Türkçe) | — | |
| 14 | 25 Ekim | Yedek, görüşme provası | — | |

- Geliştirme sırasında satır satır çalışma YOK; sadece "neden"leri oku. Satır satır çalışma, kod oturduktan sonra.
- Akşamları `notes/frontend-study.md` listesinden TS/React temeli.
- Artı puanlar sadece zaman kalırsa (birim testleri ve health check yapıldı).

## Dokümandan kritik gereksinimler (özet)

**Stack:** Backend ASP.NET Core (.NET 8+), PostgreSQL, EF Core (Npgsql); TimescaleDB/Redis serbest ama seçimin gerekçesi puanlanır. SignalR veya düz WebSocket. Frontend: Next.js (App Router), TypeScript strict, TanStack Query, Zustand, Tailwind + shadcn/ui, next-intl, zod, MapLibre GL JS veya Leaflet + OSM. Paket yöneticisi npm.

**Backend:**
- Vehicle Positions akışı 3 sn'de bir okunur; aralık ve adres config'ten. Uygulama açılınca kendiliğinden başlar.
- Konumlar kalıcı yazılır, son 3 saat tutulur, eskisi otomatik silinir (süre config'ten). Silme, yazmayı/yayını/sorguları kilitlememeli; 3 saat beklemeden gösterilebilmeli.
- Saatlik ve 3 saatlik veri hacmi (satır + disk) gerçek veriyle hesaplanıp README'ye yazılır.
- WebSocket: yeni istemci önce güncel durum, sonra değişiklikler; yeniden bağlanan kayıpsız güncel duruma döner; yavaş istemci diğerlerini ve toplamayı etkilemez.
- REST: anlık durum; araç geçmişi (son 3 saat, zaman aralığı filtresi, sayfalı, hızlı); sistem durumu (son başarılı okuma, son 1 saatte yazılan kayıt, başarısız deneme sayısı, bağlı istemci sayısı). Hepsi Swagger'dan test edilebilir.
- **Veri kaybı kabul edilmez.** Denenecek senaryolar: kaynak birkaç dakika erişilemez / bozuk yanıt; DB konteyneri durdurulup başlatılır; uygulama çalışırken yeniden başlatılır; WS istemcisi kopar ya da çok yavaş okur. Her biri için verinin akıbeti README'de; önlenemeyen kayıp açıkça yazılır.

**Frontend:**
- Harita: açılışta güncel durum hemen, sonra canlı güncelleme; binlerce araçta akıcı. Bağlantı durumu (bağlı / yeniden bağlanıyor / koptu) görünür, otomatik yeniden bağlanma. Hat/araç no ile arama-filtre, filtre durumu URL'de.
- Araç detay paneli canlı güncellenir; son 3 saatten aralık seçerek sunucu taraflı sayfalı geçmiş + haritada iz.
- Sistem durumu paneli periyodik güncellenir.
- Her ekranda dört durum: yükleniyor, boş, hata, başarılı. TR + EN, metin koda gömülmez. 375px mobilde kullanılabilir.

**Teslim:** Tek public GitHub reposu (`/backend`, `/frontend`). `docker compose up` ile her şey ayağa kalkar, backend hemen toplamaya başlar. README: çalıştırma, mimari + veri akışı diyagramı, sorumluluklar, video linki ve şu sorular: saklama yaklaşımı ve neden, 3 saatten eskiyi nasıl siliyorsun, 10 gün olsaydı çalışır mıydı; hangi durumlarda veri kaybı olur ve nasıl önledin; canlı veriyi istemcide nerede tutuyorsun ve neden; ölçülen sayılar (okuma döngüsü süresi, saatlik kayıt, arayüzde güncelleme işleme süresi); bilinen eksikler. Conventional Commits. Gerçek şifre/anahtar repoya girmez. 10-15 dk Türkçe video.

**Eleme kriterleri:** Backend mimarisi ve katman ayrımı; frontend mimarisi (feature bazlı yapı, sunucu/istemci veri ayrımı). Çok yüksek: veri bütünlüğü/kayıp yönetimi, WebSocket tasarımı ve istemci tarafı yönetimi.

**Artı puan:** Service Alerts, Trip Updates, statik GTFS ile zenginleştirme, xUnit + Moq/NSubstitute + Testcontainers, Vitest + RTL + MSW + Playwright, mimari kuralların otomatik zorlanması, JWT.

## Commit

Conventional Commits (`feat(scope): ...`, `fix: ...`, `docs: ...`). Onaylanan her adımın sonunda anlamlı bir commit.
