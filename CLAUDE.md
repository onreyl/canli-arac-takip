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

- Kodu Claude yazar; satır satır anlatımı ChatGPT'ye yaptırıyorum. Claude'un açıklamaları kısa olsun ve **"neden"e** (projeye özel tasarım gerekçeleri, gerçek veri gözlemleri) odaklansın.
- ChatGPT kodu yorumlarıyla birlikte görüyor. Bu yüzden ChatGPT'nin bilemeyeceği projeye özel "neden"leri (gerçek veri gözlemleri, tasarım kararları) koda yorum olarak yaz. "Ne yaptığını" anlatan yorum yazma. Keşif aracında (tools/) bol olabilir; backend/frontend'de sadece karar noktalarına koy, uzun gözlemler `docs/observations.md`'ye.
- Yeni bir yapıyı anlatırken önce kodda izlenecek yolu ve tipleri göster (ör. `entity.Vehicle.Vehicle.Id` → `VehicleDescriptor`), sadece proto adlarıyla anlatma.
- Yeni kavramları Claude yazar; bildiğim şeylerin uygulamasını (LINQ, basit iş kuralları) küçük görevler olarak ben yazarım, Claude kontrol eder. Bir görevde 10 dakikadan fazla takılırsam yardım eder.
- `notes/chatgpt-context.md` (gitignore'da) ChatGPT'ye yapıştırdığım bağlam dosyası: proje özeti, kodda izlenen yollar/tipler, gerçek veri gözlemleri, kararlar, açık sorular. Yeni bir gözlem ya da karar çıktığında Claude bu dosyayı günceller.
- Frontend'de çoğunu Claude yazar; ben her parçadan sonra küçük bir canlı değişiklik provası yaparım.

## Ben

Makine mühendisi, 3 yıl kendi kendime yazılım. C#/.NET'te rahatım (Clean Architecture, CQRS + MediatR, Result pattern, FluentValidation + pipeline behavior, Repository + UnitOfWork + EF Core, domain event). React'ın temellerine baktım; Next.js, TypeScript ve Docker'da yeniyim.

## Plan

- Gün 1: GTFS-RT ve Protocol Buffers'ı anla, Entur akışını çözen küçük konsol uygulaması, verinin gerçek davranışını gözlemle (README için).
- Gün 2: Tasarım kararları (veritabanı/saklama, **veri kaybı senaryolarının tasarımı**), veri hacmi hesabı, mimari diyagram, Docker ile veritabanı.
- Gün 3-5: Backend (veri çekme, toplu yazma, 3 saat saklama/temizlik, SignalR, REST, Swagger, sistem durumu). Akşamları paralel TS/React temeli.
- Gün 6: Dayanıklılık senaryolarını gerçekten dene, README'ye yaz.
- Gün 7-11: Frontend (harita, canlı bağlantı, araç detayı/geçmişi, sistem durumu, i18n, dört durum, mobil).
- Gün 12: Docker Compose. Gün 13: README, ölçümler, video. Gün 14: Yedek.
- Artı puanlar sadece zaman kalırsa.

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
