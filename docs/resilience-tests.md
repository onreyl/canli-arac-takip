# Dayanıklılık Testleri — Adım Adım

Dokümanın 3.3 bölümündeki senaryolar. Her testin sonunda "Sonuç" satırını doldur; README'ye birlikte yazacağız.
Komutlar **PowerShell**, proje kök klasöründe (`canli-arac-takip`) çalıştırılır.

## 0. Hazırlık (bir kez)

1. Docker Desktop açık olsun.
2. Her şeyi başlat:
   ```powershell
   docker compose up -d --build
   ```
3. Üç pencere aç:
   - Harita: http://localhost:3000/tr
   - Sistem durumu: http://localhost:3000/tr/status (5 sn'de bir kendini yeniler)
   - Ayrı bir PowerShell (loglar için): `docker compose logs -f backend`

### Yardımcı komutlar (testlerde kullanılacak)

Sistem durumu (sayılar):
```powershell
Invoke-RestMethod http://localhost:5295/api/system/status
```

Sağlık (Healthy / Degraded / Unhealthy):
```powershell
Invoke-RestMethod http://localhost:5295/health -SkipHttpErrorCheck | ConvertTo-Json -Depth 5
```

**Dakika bazında yazılan satır** (deliğin olup olmadığını görmek için; son 15 dakika, Oslo saati):
```powershell
docker compose exec db sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "select to_char(date_trunc(''minute'', measured_at) at time zone ''Europe/Oslo'', ''HH24:MI'') as dakika, count(*) as satir from vehicle_positions where measured_at > now() - interval ''15 minutes'' group by 1 order by 1;"'
```
> Not: bu sayım konumun **ölçüldüğü** dakikaya göredir. Normal akışta her dakika ~5-15 bin satır (saate göre değişir).

---

## Test 1 — Kaynak (Entur) birkaç dakika erişilemez

**Ne bekliyoruz:** uygulama çökmez, başarısız okuma sayılır, kaynak dönünce toplama kendiliğinden devam eder.
O dakikalarda veri yoktur — **kaçınılmaz kayıp** (GTFS-RT geçmiş tutmaz).

1. Saati not et. Sistem durumunda `Başarısız okuma` değerini not et.
2. **Bilgisayarın internetini kes** (Wi-Fi'yi kapat ya da kabloyu çıkar). Veritabanı ve uygulama bilgisayarında çalıştığı için
   onlar etkilenmez; sadece Entur'a ulaşılamaz. (Harita altlığı da internetten geldiği için harita karoları yüklenmeyebilir, normal.)
3. 3 dakika bekle. Bu sırada:
   - Sistem durumu: `Başarısız okuma` artıyor mu? `Son başarılı okuma` eskiyor mu, 30 sn'den sonra uyarı rengi çıkıyor mu?
   - `/health` → `ingestion` "Degraded" mı?
   - Backend logu: `Feed okunamadı: Entur'a ulaşılamadı.` satırları.
4. İnterneti geri aç. 10-20 sn içinde `Son başarılı okuma` güncelleniyor mu?
5. Dakika bazında satır sorgusunu çalıştır: kesinti dakikalarında satır sayısı ~0 olmalı, sonra normale dönmeli.

**Sonuç:** ……

## Test 2 — Kaynak bozuk yanıt dönüyor

**Ne bekliyoruz:** bozuk yanıt veritabanına girmez, başarısız okuma sayılır, döngü durmaz.

1. Kaynak adresini protobuf olmayan bir sayfaya çevir ve backend'i bu ayarla yeniden oluştur:
   ```powershell
   $env:ENTUR_URL = "https://www.google.com"
   docker compose up -d backend
   ```
2. 1 dakika bekle. Kontrol:
   - Backend logu: `Feed okunamadı: Entur yanıtı çözülemedi (bozuk protobuf).`
   - `Başarısız okuma` artıyor, `Kayıp parti` 0 kalıyor, uygulama ayakta.
3. (İsteğe bağlı) HTTP hatası: `$env:ENTUR_URL = "https://api.entur.io/realtime/v1/gtfs-rt/olmayan-adres"` ve tekrar
   `docker compose up -d backend` → log: `Entur'a ulaşılamadı.`
4. Normale dön:
   ```powershell
   Remove-Item Env:ENTUR_URL
   docker compose up -d backend
   ```

**Sonuç:** ……

## Test 3 — Veritabanı birkaç dakika kapalı

**Ne bekliyoruz:** toplama ve **canlı harita** devam eder (Karar 6), yazılamayan veri tamponda bekler, DB dönünce yazılır,
**veri kaybı yok**.

1. Saati not et.
2. Veritabanını durdur:
   ```powershell
   docker compose stop db
   ```
3. 3 dakika bekle. Bu sırada:
   - Harita akmaya devam ediyor mu? (Canlı yayın DB'den bağımsız.)
   - Sistem durumu: `Bekleyen parti` artıyor, `Başarısız yazma` artıyor, `Kayıp parti` 0.
   - `/health` → `database` Unhealthy, HTTP 503; ama `http://localhost:5295/health/live` 200 (süreç ayakta).
   - Backend logu: `DB'ye yazılamadı (...). X sonra tekrar denenecek; tamponda N parti bekliyor.` (bekleme 1→2→4→8→10 sn)
4. Veritabanını başlat:
   ```powershell
   docker compose start db
   ```
5. En fazla ~10 sn içinde `Bekleyen parti` 0'a inmeli.
6. Dakika bazında satır sorgusu: kesinti dakikalarında **delik olmamalı** (sayılar normal seviyede).

**Sonuç:** ……

## Test 4 — Uygulama çalışırken yeniden başlatılıyor

**Ne bekliyoruz:** düzgün kapanış: önce çekme durur, sonra tampon boşaltılır. Uygulamanın kapalı olduğu birkaç saniye
kaçınılmaz boşluk; açılınca feed her aracın son konumunu verir.

1. Saati not et.
2. Yeniden başlat:
   ```powershell
   docker compose restart backend
   ```
3. Kontrol:
   - Backend logu: `Application is shutting down...` → `Kapanışta tampondan N parti yazıldı.` → `Veritabanı hazır` → `Now listening`.
   - Harita: rozet "yeniden bağlanıyor" → "bağlı"; araç sayısı normale döner (yeni tam görüntü).
   - Dakika bazında satır: yeniden başlatma dakikasında hafif düşüş olabilir, delik olmamalı.

### Test 4b — (bilinen eksiği göstermek için) Veritabanı kapalıyken yeniden başlatma

**Ne bekliyoruz:** tampondaki partiler veritabanına yazılamadığı için kaybolur — README'de "bilinen eksikler 1".

1. `docker compose stop db`, 1 dakika bekle (`Bekleyen parti` > 0 olsun).
2. `docker compose restart backend`
3. Log: `Kapanışta tampon boşaltılamadı, N parti kayboldu.` Bu, disk tamponu olmadığı için önlenemeyen kayıptır.
4. `docker compose start db` (backend DB gelene kadar bekler, sonra devam eder).

**Sonuç:** ……

## Test 5 — WebSocket istemcisi bağlantıyı koparıyor

**Ne bekliyoruz:** istemci otomatik yeniden bağlanır, yeni tam görüntüyle kayıpsız güncel duruma döner. Sunucu etkilenmez.

1. Haritayı aç, **F12** → **Network** sekmesi → üstteki hız menüsünden (**No throttling** yazan) **Offline** seç.
2. 20-30 sn bekle: rozet "yeniden bağlanıyor", sonra (≈40 sn) "koptu" + kırmızı şerit. Harita son bilinen konumlarla kalır.
3. Tekrar **No throttling** seç: rozet kendiliğinden "bağlı" olur, araç sayısı güncellenir.
4. (Alternatif) Sunucu tarafından koparma: `docker compose stop backend`, 30 sn, `docker compose start backend`.

**Sonuç:** ……

## Test 6 — Yavaş istemci

**Ne bekliyoruz:** yavaş istemci diğer istemcileri ve veri toplamayı etkilemez; aynı aracın ara konumları yerine son konumunu alır.

1. Haritayı **iki sekmede** aç (A ve B).
2. B sekmesinde **F12** → **Network** → hız menüsünden **Slow 3G** (ya da "Add custom..." ile çok düşük bir hız) seç.
3. 3-5 dakika bekle. Kontrol:
   - A sekmesi normal akıyor mu (işleme süresi, araç hareketleri)?
   - Sistem durumu: `Bağlı istemci` 2 (sistem durumu sayfası sayılmaz, sadece haritalar), `Son okuma süresi` normal, `Kayıp parti` 0.
   - B sekmesi geç ama doğru güncelleniyor mu? (F12 → Network → `vehicles` WebSocket satırı → **Messages**: B'de mesajlar
     seyrekleşip büyüyebilir.)
4. B'de tekrar **No throttling**.

**Sonuç:** ……

## Test 7 — Silmenin 3 saat beklemeden gösterilmesi

> ⚠️ Bu test **veritabanındaki verileri siler** (temiz başlangıç gerekiyor; bkz. backend-review madde 6).

**Ne bekliyoruz:** saklama 5 dk'ya çekilince 5 dk'dan eski chunk'lar otomatik silinir; yazma ve sorgular etkilenmez.

1. Temiz başlat, kısa saklama ayarıyla:
   ```powershell
   docker compose down -v
   $env:RETENTION = "00:05:00"
   $env:CHUNK_INTERVAL = "00:01:00"
   $env:RETENTION_CHECK_INTERVAL = "00:00:30"
   docker compose up -d
   ```
2. Backend logunda: `Veritabanı hazır. Saklama: 00:05:00, chunk: 00:01:00, silme kontrolü: 00:00:30`.
3. 10 dakika bekle, sonra:
   ```powershell
   docker compose exec db sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "select min(measured_at) at time zone ''Europe/Oslo'' as en_eski, now() at time zone ''Europe/Oslo'' as simdi, count(*) from vehicle_positions;" -c "select count(*) as chunk_sayisi, min(range_start) at time zone ''Europe/Oslo'' as en_eski_chunk from timescaledb_information.chunks where hypertable_name = ''vehicle_positions'';" -c "select total_runs, total_failures, last_run_status from timescaledb_information.job_stats where hypertable_name = ''vehicle_positions'';"'
   ```
   - `en_eski` ile `simdi` arası ~5-6 dk olmalı (bir chunk süresi kadar fazlası normal).
   - `total_runs` artıyor, `total_failures` 0.
   - Bu sırada sistem durumu ve harita normal (silme yazmayı kilitlemiyor).
   - **Kanıt (yazma süresi):** `Invoke-RestMethod http://localhost:5295/api/system/status` → `lastWriteDurationMs` (son yazma)
     ve `maxWriteDurationLastHourMs` (son 1 saatteki en uzun). Normalde son yazma ~10-50 ms. Silme işi her 30 sn'de çalışırken
     birkaç kez bak: son yazma süresi normal seviyede kalmalı. (En uzun süre genelde açılıştaki ilk büyük partidir, ~100-500 ms.)
4. Normale dön:
   ```powershell
   Remove-Item Env:RETENTION, Env:CHUNK_INTERVAL, Env:RETENTION_CHECK_INTERVAL
   docker compose up -d backend
   ```
   (Saklama 3 saate döner. Yeni chunk'lar yine 15 dk olur; 1 dk'lık eski chunk'lar zamanla silinir.)

**Sonuç:** ……

---

## Sonuç tablosu (README'ye gidecek)

| Senaryo | Ne oldu | Veri kaybı | Not |
|---|---|---|---|
| 1. Kaynak erişilemez | | Kesinti süresi kadar (kaçınılmaz) | |
| 2. Bozuk yanıt | | Yok (bozuk veri DB'ye girmez) | |
| 3. DB birkaç dk kapalı | | Yok | |
| 4. Uygulama yeniden başlatma | | Birkaç sn (kapalı kalınan süre) | |
| 4b. DB kapalıyken yeniden başlatma | | Tampondaki partiler (bilinen eksik) | |
| 5. İstemci kopar | | Yok | |
| 6. Yavaş istemci | | Yok | |
| 7. Silme gösterimi | | — | |
