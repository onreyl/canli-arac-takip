# Frontend Kararları

Her karar: **ne seçtik**, **neden**, **C#/backend karşılığı** (anlatırken köprü olsun diye).
Genel ilke: dokümanın istediği her özellik var, ama en sade haliyle. Fazladan özellik, animasyon ya da desen yok.

## 1. Harita: MapLibre GL JS + OpenFreeMap altlık

- **Neden:** MapLibre haritayı WebGL ile (ekran kartında) çizer. 3.000 aracı tek bir "katman" olarak verip tek seferde güncelleyebiliyoruz.
  Doküman "binlerce araçta akıcı" diyor; yoğun saatte ~3.200 araç ölçtük.
- **Neden Leaflet değil:** Leaflet'te her araç ayrı bir HTML elemanı olur; 3.000 elemanı birkaç saniyede bir güncellemek tarayıcıyı yorar.
- **Altlık:** OpenFreeMap. OpenStreetMap verisinden üretilmiş ücretsiz vektör harita; API anahtarı gerektirmez, repoya gizli bilgi girmez.
- **Araçlar nasıl çiziliyor:** tüm araçlar tek bir GeoJSON kaynağı + bir "circle" katmanı. Renk/opaklık veriye göre (aktif / eski / boş sefer) katman stilinde.

## 2. Canlı veri istemcide nerede? (README sorusu)

İki ayrı depo, çünkü iki farklı türde veri var:

| Veri | Nerede | Neden | C# karşılığı |
|---|---|---|---|
| Canlı araçlar (SignalR'dan akan) | **Zustand** store: `aracId → araç`, sürüm, bağlantı durumu | Sürekli akan, uygulama boyunca yaşayan tek bir durum | Uygulama boyunca yaşayan **singleton** bir servis (bizdeki `LiveVehicleState` gibi) |
| REST'ten istenince çekilen (geçmiş, sistem durumu, araç detayı) | **TanStack Query** önbelleği | Yükleniyor/hata/başarılı durumlarını, yeniden denemeyi, önbelleği hazır yönetir | `HttpClient` + bir önbellek + yeniden deneme politikası (Polly) |

- **Neden Zustand:** dokümanın stack listesinde var, çok küçük, öğrenmesi kolay. Redux gibi tören yok.
- **Performans:** SignalR'dan gelen her güncelleme önce store'a yazılır; harita store'u **React'ın yeniden çizimi olmadan** okur ve
  MapLibre kaynağını saniyede en fazla birkaç kez toplu günceller. Böylece 3.000 aracın her değişikliği React bileşenlerini yeniden çizdirmez.

## 3. SignalR bağlantısı

- `@microsoft/signalr` istemcisi, `withAutomaticReconnect()` ile.
- Bağlantı durumları: **bağlı / yeniden bağlanıyor / koptu** → store'da tutulur, ekranda rozet olarak görünür.
- Sunucu bağlanınca ve her yeniden bağlanmada önce `snapshot` (tam görüntü) gönderir; istemci elindeki listeyi bununla **değiştirir**.
  Sonra gelen `update` mesajlarında `upserts` eklenir/güncellenir, `removals` silinir. Böylece yeniden bağlanan istemci kayıpsız güncel duruma döner.
- Otomatik yeniden bağlanma vazgeçerse (uzun kesinti), "koptu" gösterilir ve birkaç saniyede bir yeniden denenir.

## 4. Sayfa düzeni

- **Masaüstü:** tam ekran harita; solda arama/filtre; araç seçilince sağda detay paneli; üstte bağlantı rozeti.
- **Mobil (375 px):** tam ekran harita; filtre açılır-kapanır; detay ekranın alt yarısında (modal değil, harita kullanılabilir kalır).
- **Sistem durumu:** ayrı sayfa (`/[dil]/status`).

## 5. Adresler, dil, URL'de durum

- Dil ön eki: `/tr/...`, `/en/...` (varsayılan `tr`). Kütüphane: **next-intl**. Tüm metinler `messages/tr.json` ve `messages/en.json`'da; kodda metin yok.
- Filtre ve seçili araç URL'de: `?q=...&route=...&active=1&vehicle=...`.
  - **Neden:** sayfa yenilendiğinde ya da bağlantı paylaşıldığında aynı ekran gelsin (doküman istiyor).
  - URL tek doğru kaynak: filtre değişince URL güncellenir, ekran URL'i okur.

## 6. Sunucu / istemci ayrımı (Next.js)

Next.js'te bir bileşen ya **sunucuda** çalışır (HTML üretip gönderir) ya da `"use client"` ile **tarayıcıda** çalışır.

- **Harita: tamamen istemcide.** WebGL ve WebSocket sadece tarayıcıda var. İlk görüntü SignalR `snapshot`'ından gelir;
  sunucuda üretseydik 3.000 araçlık veri sayfa HTML'ine gömülür, sayfa ~400 KB şişerdi.
- **Sistem durumu sayfası:** ilk veri **sunucuda** çekilir (sayfa boş açılmaz), sonra **istemcide** TanStack Query 5 sn'de bir yeniler.
  Ayrımın küçük ve anlaşılır bir örneği.
- **Sayfa dosyaları (`app/...`) ince:** sadece feature bileşenlerini yerleştirir; mantık feature klasörlerinde.

## 7. Klasör yapısı (feature bazlı — eleme kriteri)

```
frontend/src/
  app/[locale]/             → sayfalar ve layout (ince)
  features/live-map/        → harita, filtre, canlı araç store'u, SignalR'ı store'a bağlayan hook
  features/vehicle-detail/  → detay paneli, geçmiş listesi, haritada iz
  features/system-status/   → sistem durumu sayfasının bileşenleri
  shared/                   → API istemcisi, SignalR bağlantısı, zod şemaları, ortak UI parçaları
  i18n/                     → next-intl ayarları
messages/tr.json, en.json
```

- **Kural:** bir feature başka bir feature'ın iç dosyasını import etmez; ortak şey `shared/`'a gider.
  (Backend'deki katman kuralının frontend karşılığı.)

## 8. Eski araçlar ve boş seferler (gerçek veri gözlemi)

- Araç tazeliği iki tepeli: aktif araçlar 2 dk'dan sık konum gönderiyor, gerisi saatlerdir sessiz (bkz. `docs/observations.md`).
- **Son ölçümü 5 dk'dan eski araç soluk** çizilir; "sadece aktif araçlar" anahtarı (varsayılan açık) bunları gizler.
- **DeadRun (boş sefer)** gri, detayda "boş sefer" etiketi.

## 9. Gelen veriyi doğrulama: zod

- REST ve SignalR'dan gelen JSON, kullanılmadan önce zod şemasıyla doğrulanır.
- **Neden:** TypeScript tipleri sadece derleme zamanında var; sunucudan gelen veri çalışma zamanında beklenenden farklı olabilir.
- **C# karşılığı:** FluentValidation + gelen DTO'yu doğrulamak.

## 10. Dört durum (her ekranda)

Her veri gösteren yerde: **yükleniyor** (iskelet/spinner), **boş** (ör. "bu aralıkta kayıt yok"), **hata** (mesaj + yeniden dene), **başarılı**.
TanStack Query'nin `isPending / isError / data` alanlarından türetilir; canlı haritada ise bağlantı durumundan.

## 11. UI: Tailwind + shadcn/ui

- shadcn/ui bir kütüphane değil, projeye kopyalanan hazır bileşenler (`src/components/ui`). Sadece gerekenler eklenir
  (button, input, badge, card, switch, skeleton).
- Stil Tailwind sınıflarıyla (`className="flex gap-2"`).

## 11b. Arama ve filtre (2. katman)

- **Tek arama kutusu:** araç numarası, kısa hat adı (`RUT:Line:31` → `31`) ya da tam hat kimliği içinde arar; harf büyüklüğü ve
  baştaki/sondaki boşluk önemsiz. Boş seferlerin hattı olmadığından sadece araç numarasıyla bulunurlar.
- **"Eski araçları da göster" anahtarı:** varsayılan kapalı (sadece aktif araçlar), açıkken URL'de `all=1`.
- **URL tek doğru kaynak:** `?q=...&all=1`. Yazarken her tuşta URL değişmesin diye kutu kendi yerel durumunu tutar ve 300 ms sonra
  URL'e yazar (debounce); geri/ileri ya da paylaşılan link gelince kutu URL'i yansıtır. `router.replace` kullanılır: her harf
  tarayıcı geçmişine girmesin.
- **Eşleştirme saf fonksiyon** (`features/live-map/filter/vehicleFilter.ts`): React bilmez; harita da sonuç sayısı da aynı
  fonksiyonu kullanır, ikisi asla tutarsız olmaz. C# karşılığı: static, yan etkisiz bir yardımcı sınıf.
- **Filtre değişince harita yeniden kurulmaz:** filtre bir `ref`'te tutulur, sadece veri yeniden üretilir. Haritayı yeniden
  oluşturmak pahalı olurdu ve görünüm sıçrardı.
- **Yeni boş durum:** araç var ama filtreye uyan yok → "Filtreye uyan araç yok" + "Filtreyi temizle".
- **Mobil:** sadece "Filtre" butonu; basınca panel açılır. Aktif bir filtre varsa butonda nokta.

## 11c. Araç detayı, geçmiş ve iz (3. katman)

- **Seçim URL'de:** haritada araca tıklayınca `?vehicle=<id>` eklenir (diğer parametreler korunur), panel kapanınca kalkar.
  Paylaşılan link aynı aracı açık getirir; açılışta o araca bir kez yaklaşılır.
- **Canlı bilgiler Zustand store'dan:** hat, sefer, durum, doluluk, son güncelleme ("12 sn önce"), boş sefer / eski etiketi.
  Panel açıkken araç güncellendikçe kendiliğinden değişir (doküman istiyor). Araç canlı durumda yoksa "şu an canlı takipte değil".
- **Geçmiş REST'ten, TanStack Query ile:** `GET /api/vehicles/{id}/history`, imleçli sayfalama → `useInfiniteQuery`,
  "daha fazla yükle" butonu. C# karşılığı: sayfalı bir HTTP çağrısı + önbellek.
- **Zaman aralığı:** hazır seçenekler — son 15 dk, 30 dk, 1 saat, 3 saat (URL'de `range=15m|30m|1h|3h`, varsayılan 1h).
  Bilinçli sadeleştirme: serbest başlangıç/bitiş seçici yerine "şimdiden geriye" aralıklar; saklama penceresi zaten 3 saat.
- **İz haritada:** yüklenen geçmiş noktaları zaman sırasına göre çizgi olarak çizilir (sunucu en yeniden eskiye verir, biz çeviririz).
  Daha fazla sayfa yüklendikçe iz uzar. Seçili araç haritada belirgin (büyük, kenarlıklı) gösterilir.
- **Aynı veri, tek sorgu:** geçmişi hem panel (liste) hem harita (iz) kullanır. İkisi de aynı hook'u aynı anahtarla çağırır;
  TanStack Query isteği bir kez atar, ikisine aynı önbelleği verir. Aralık URL'de olduğu için ikisi aynı aralığı görür.
- **Feature'lar arası kural (güncelleme):** bir feature başka bir feature'ı sadece onun `index.ts`'i (dışa açık API) üzerinden
  kullanır, iç dosyalarına uzanmaz. C# karşılığı: başka bir projenin sadece `public` arayüzünü kullanmak, `internal`'ına değil.
- **Masaüstü:** sağda sabit genişlikte panel. **Mobil:** ekranın alt yarısı. Tek bileşen, sadece Tailwind'in ekran genişliğine
  göre değişen sınıfları (`sm:`). Modal değil: harita üstte kullanılabilir kalır, başka bir araca dokunup seçilebilir.
  (Codex ilk sürümde mobilde modal `Sheet` + JS ile ekran ölçümü kullanmıştı; haritayı kilitlediği ve gereksiz karmaşık olduğu için sadeleştirildi.)
- **Geçmiş penceresi nasıl sabit kalıyor:** "şimdi" İLK sayfa çekilirken belirlenir ve sonraki sayfalara imleçle birlikte taşınır
  (`pageParam = { cursor, windowEnd }`). Render sırasında `Date.now()` kullanılmaz (React kuralı; ayrıca her render yeni bir istek
  demek olurdu). "Yenile" sorguyu sıfırlar → yeni bir "şimdi".

## 11d. Sistem durumu sayfası (4. katman)

- **Adres:** `/[dil]/status`. Haritadan ve haritaya bağlantı; her iki sayfada TR/EN dil değiştirici.
- **Sunucu/istemci ayrımının örneği:** sayfa bir **Server Component**. İlk veriyi sunucuda `GET /api/system/status`'tan çeker,
  böylece sayfa boş açılmaz. Sonra **Client Component** (`SystemStatusPanel`) TanStack Query ile 5 sn'de bir yeniler; ilk veri
  `initialData` olarak verilir. C# karşılığı: sayfayı sunucuda dolu render etmek (Razor) + sonra tarayıcıda periyodik AJAX.
- **İki API adresi:** tarayıcı backend'e `NEXT_PUBLIC_API_BASE_URL` (ör. `http://localhost:5295`) ile gider. Sunucu tarafı ise
  Docker'da backend'e konteyner ağı üzerinden ulaşır (ör. `http://backend:8080`): `API_INTERNAL_BASE_URL`. Tanımlı değilse
  tarayıcı adresi kullanılır (geliştirmede ikisi aynı).
- **Gösterilenler:** son başarılı okuma (göreli + saat), son okuma süresi, son 1 saatte yazılan kayıt, başarısız okuma,
  başarısız yazma, kayıp parti, bağlı istemci, bekleyen parti, canlı araç sayısı. Son okuma 30 sn'den eskiyse uyarı rengi
  (backend health check'teki eşikle aynı); kayıp parti > 0 ise kırmızı.
- **Dört durum:** yükleniyor (ilk veri yoksa iskelet), boş (henüz hiç başarılı okuma yok), hata (backend'e ulaşılamadı + yeniden
  dene; elde son veri varsa o gösterilir ve "güncellenemedi" notu düşülür), başarılı.

- **Saat dilimi: tüm uygulamada Oslo** (`i18n/request.ts` → `timeZone: "Europe/Oslo"`, etiketlerde "(Oslo)").
  Neden: veri Norveç'in ve senaryodaki ulaşım otoritesi Oslo saatiyle çalışır; tek yerde tanımlı olunca sunucuda üretilen HTML ile
  tarayıcının gösterdiği saat aynı olur. (Codex ilk sürümde sadece bu sayfada İstanbul saatini gömmüştü; geçmiş listesi ise
  tarayıcı saatini kullanıyordu → iki ekranda farklı saat. Tek dilime çekildi.)
- **Doğrulandı:** `/tr/status`'un düz HTML'inde (JavaScript çalışmadan) veriler var → sunucu tarafı ilk yükleme çalışıyor.

## 12. Kurulumda karşılaşılan sorunlar ve çözümleri (görüşmede sorulabilir)

- **Harita boş görünüyordu: MapLibre worker dosyası yüklenemiyordu.** MapLibre 6 haritayı ayrı bir web worker'da işler ve
  `maplibre-gl-worker.mjs` dosyasını kendi paketinin yanından yüklemeye çalışır. Next.js'in paketleyicisi (Turbopack) bu dosyayı
  çıktıya koymuyordu (404); üstelik dil yönlendirmemiz isteği `/tr/...`'ye çevirip HTML döndürüyordu.
  Çözüm: `scripts/copy-maplibre-worker.mjs` dosyayı kurulumda ve her `dev`/`build` öncesinde `public/maplibre/`'e kopyalar,
  kodda `setWorkerUrl("/maplibre/maplibre-gl-worker.mjs")` ile adres verilir. (`public/` altındaki noktalı dosya adları proxy'ye girmez.)
- **Harita 0 yükseklikteydi.** MapLibre harita kutusuna kendi CSS'inde `position: relative` veriyor, bizim `absolute inset-0`
  ayarımızı eziyordu. Çözüm: konumlandırma dış kutuda, harita iç kutuda.
- **Geliştirmede "negotiation sırasında durduruldu" hatası.** React StrictMode geliştirmede bileşeni bilerek aç-kapa-aç yapar;
  ilk SignalR bağlantısı kurulurken kapatılıyordu. Çözüm: bağlantıyı bir sonraki tick'e ertele, hemen kapatılan kopya hiç başlamaz.
  (Üretimde StrictMode bu çift çalıştırmayı yapmaz.)
- **"Koptu" ile "bağlanıyor" arasında gidip gelme.** SignalR'ın otomatik yeniden bağlanması 0/2/10/30 sn denemeden sonra vazgeçer;
  biz de 5 sn'de bir yeniden deneriz. Bu arka plan denemeleri durumu "bağlanıyor"a çekmez, bağlantı gerçekten kurulana kadar
  "koptu" kalır. Kullanıcının "yeniden dene" butonu ise "bağlanıyor" gösterir.

## 13. Doğrulanan davranış (15 Ekim)

- Açılışta snapshot ile ~2.300 araç; güncelleme işleme süresi (store + GeoJSON + setData) ~1-4 ms.
- Backend durdurulunca: "yeniden bağlanıyor" → (~40 sn sonra) "koptu" şeridi; harita son bilinen konumlarla kalır.
- Backend yeniden açılınca: dokunmadan "bağlı" ve yeni tam görüntü.
- TR/EN ve 375 px mobilde rozet ve şerit düzgün.
