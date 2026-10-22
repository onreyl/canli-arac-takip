# Bilinen Eksikler (README'nin "Bilinen eksikler" bölümünün ham maddesi)

> README'nin "Bilinen eksikler" bölümü bu dosyanın özetidir. Senaryoların nasıl deneneceği: [resilience-tests.md](resilience-tests.md).

## 1. Disk tamponu yok: ani çökmede bellekteki birkaç saniyelik veri kaybolur

**Ne kaybolur:**
- Çekilen ama henüz veritabanına yazılmamış konumlar bellekteki bir kuyrukta (`InMemoryFeedBuffer`) bekler.
- Veritabanı sağlıklıyken bu kuyruk neredeyse hep boştur: yeni bir feed ~6 sn'de bir gelir, yazma ~0,1-0,5 sn sürer.
- Uygulama **ani** çökerse (süreç öldürülürse, makine kapanırsa) kuyruktakiler kaybolur. Bu, normalde son birkaç saniyelik
  konumdur. Veritabanı o sırada kapalıysa kuyrukta biriken (dakikalarca) veri de kaybolur.
- **Düzgün kapanışta** (`docker compose stop/restart`, Ctrl+C) kayıp yoktur: önce çekme durur, sonra yazıcı kuyruğu 20 sn
  içinde boşaltır. Kapanış süreleri buna göre ayarlı (Docker 30 sn ≥ host 25 sn ≥ boşaltma 20 sn).

**Neden kabul edilebilir:**
- GTFS-RT akışı her okumada **tam güncel durumu** gönderir (`FULL_DATASET`). Uygulama yeniden açıldığında ilk okumada her
  aracın son konumu geri gelir; **canlı harita hemen doğru duruma döner.**
- Kaybolan sadece o birkaç saniyelik **ara konumlar**dır (geçmişte küçük bir boşluk). Uygulamanın kapalı kaldığı süre zaten
  kaçınılmaz bir boşluktur (kaynak geçmiş tutmaz); ani çökmede kaybolan, bu boşluğa birkaç saniye ekler.

**Nasıl önlenirdi:**
- **Disk tamponu:** her yeni feed önce diske yazılır, veritabanına yazılınca silinir, açılışta diskte kalanlar yazılır.
  Tampon `IFeedBuffer` arkasında olduğu için yazıcı ve çekici değişmez; yeni bir uygulama sınıfı, DI kaydı ve arayüze bir "onayla"
  adımı (parti DB'ye yazılınca dosyası silinsin) eklenir. Yazma tekrar güvenli olduğu için (`ON CONFLICT DO NOTHING`) aynı dosyayı
  iki kez yazmak sorun olmaz.
- Ya da **RabbitMQ / Kafka gibi kalıcı bir kuyruk:** çekme servisi kuyruğa yazar, yazıcı oradan onaylı okur.
- Neden yapılmadı: 14 günlük süre içinde öncelik zorunlu maddelere (frontend dahil) verildi; risk küçük ve açıkça belgelendi.

## 2. Uygulama açılırken veritabanı kapalıysa toplama da başlamaz

- `DatabaseInitializer` veritabanı gelene kadar 5 sn'de bir dener; bu sürede diğer servisler (toplama, REST, SignalR) başlamaz.
- `docker compose up`'ta sorun olmaz: backend, veritabanı sağlıklı olmadan başlatılmaz (healthcheck + depends_on).
- Sadece "veritabanı kapalıyken uygulamayı yeniden başlatmak" durumunda o süre boyunca veri toplanmaz. Disk tamponu olsaydı
  toplama veritabanından bağımsız başlayabilirdi (madde 1 ile aynı çözüm).

## 3. Kapsam dışı bırakılan artı puan maddeleri

Ekip liderinin "önceliklendirme senin kararın" cevabı üzerine zorunlu maddelere odaklanıldı. Yapılmayanlar:
- Uyarılar (Service Alerts) akışı ve arayüzde uyarı listesi
- Sefer takibi (Trip Updates): gecikme ve sonraki durak tahminleri
- Statik GTFS ile hat/durak adlarıyla zenginleştirme (şu an kimlikler gösteriliyor, ör. `BRA:Line:4_6104` → `4_6104`)
- Testcontainers ile entegrasyon testleri; frontend testleri (Vitest, RTL, MSW, Playwright)
- Mimari kuralların otomatik zorlanması (ör. katman bağımlılık testleri)
- JWT ile uç noktaların korunması

Yapılan artı puan maddeleri: backend birim testleri (xUnit, `backend/tests/VehicleTracking.UnitTests`: ayıklama, canlı durum,
yavaş istemci birleştirmesi, imleç kodlama, sayaçlar, DB yokken 503), health check uçları (`/health/live`, `/health`).
