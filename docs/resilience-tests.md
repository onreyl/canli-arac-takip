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

**Sonuç:** Çalıştırılmadı (bilgisayarın internetini kesmeyi gerektiriyor). Aynı kod yolu Test 2'deki HTTP hatası adımıyla denendi: `Entur'a ulaşılamadı` → başarısız okuma sayıldı, döngü durmadı.

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

**Sonuç (22 Ekim 08:28-08:30 Oslo):** Protobuf olmayan sayfa → 60 sn'de 21 kez `Entur yanıtı çözülemedi (bozuk protobuf)`, başarısız okuma 21, kayıp parti 0, uygulama ayakta (`/health` HTTP 200, `/health/live` 200). HTTP 404 adresi → 30 sn'de 11 kez `Entur'a ulaşılamadı`. Normal adrese dönünce 20 sn içinde okuma devam etti (okuma 258 ms). Bu sürede veri yok (dakika başına satır 08:28'de 7.415, 08:29'da 3.001; normal ~18-20 bin): kaynaktan okunamayan an, kaçınılmaz.

## Test 3 — Veritabanı birkaç dakika kapalı

**Ne bekliyoruz:** toplama ve **canlı harita** devam eder, yazılamayan veri tamponda bekler, DB dönünce yazılır,
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
4. DB kapalıyken geçmiş sorgusunu kontrol et. Canlı listede araç varsa kimliğini bul:
   ```powershell
   (Invoke-RestMethod http://localhost:5295/api/vehicles).vehicles[0].vehicleId
   ```
   Aşağıdaki `<bir araç id>` yerine bu kimliği yaz (liste boşsa canlı araç gelmesini bekle):
   ```powershell
   Invoke-WebRequest "http://localhost:5295/api/vehicles/<bir araç id>/history" -SkipHttpErrorCheck | Select-Object StatusCode, Content
   ```
   Beklenen: HTTP **503**, ProblemDetails gövdesinde `title`: "Veritabanına şu an ulaşılamıyor" ve
   `detail`: "Geçmiş verisi geçici olarak kullanılamıyor; lütfen biraz sonra tekrar deneyin."
5. Veritabanını başlat:
   ```powershell
   docker compose start db
   ```
6. En fazla ~10 sn içinde `Bekleyen parti` 0'a inmeli.
7. Dakika bazında satır sorgusu: kesinti dakikalarında **delik olmamalı** (sayılar normal seviyede).

**Sonuç (22 Ekim 08:30:55-08:34:04 Oslo, DB 3 dk kapalı):** Okuma her 30 sn'de devam etti (okuma ~185-226 ms), canlı araç sayısı güncellenmeye devam etti (3.198 → 3.223). Bekleyen parti 5 → 31'e çıktı, başarısız yazma 16, kayıp parti 0. `/health` 503, `/health/live` 200, geçmiş ucu 503 + "Veritabanına şu an ulaşılamıyor", anlık durum ucu 200. DB açıldıktan sonra **5 sn içinde bekleyen parti 0**. Dakika başına satır kesinti boyunca normal (08:31: 17.042, 08:32: 17.187, 08:33: 19.355, 08:34: 19.476): **delik yok, kayıp yok.**

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

**Sonuç — Test 4 (22 Ekim 08:35 Oslo):** Log: `Application is shutting down...` → `Kapanışta tampondan 0 parti yazıldı.` → `Veritabanı hazır` → `Now listening`. Yeniden başlatma ~1 sn sürdü, toplama 20 sn içinde normaldi. Dakika başına satır 08:35'te 18.571 (normal): görünür düşüş yok.

**Sonuç — Test 4b (22 Ekim 08:36:25-08:37:47 Oslo):** DB 1 dk kapalıyken bekleyen parti 11 oldu; yeniden başlatmada log: **`Kapanışta tampon boşaltılamadı, 10 parti kayboldu.`** Dakika başına satır 08:36: 6.055, 08:37: 7.561, 08:38: 10.265 (normal ~18-20 bin). Bilinen eksik, beklendiği gibi; `docs/known-limitations.md` madde 1.

## Test 5 — WebSocket istemcisi bağlantıyı koparıyor

**Ne bekliyoruz:** istemci otomatik yeniden bağlanır, yeni tam görüntüyle kayıpsız güncel duruma döner. Sunucu etkilenmez.

1. Haritayı aç, **F12** → **Network** sekmesi → üstteki hız menüsünden (**No throttling** yazan) **Offline** seç.
2. 20-30 sn bekle: rozet "yeniden bağlanıyor", sonra (≈40 sn) "koptu" + kırmızı şerit. Harita son bilinen konumlarla kalır.
3. Tekrar **No throttling** seç: rozet kendiliğinden "bağlı" olur, araç sayısı güncellenir.
4. (Alternatif) Sunucu tarafından koparma: `docker compose stop backend`, 30 sn, `docker compose start backend`.

**Sonuç (22 Ekim 08:38-08:40 Oslo):** Tarayıcı yerine aynı SignalR protokolünü konuşan bir .NET test istemcisiyle (`withAutomaticReconnect`). Bağlanınca snapshot (3.009 araç). Backend 08:38:33'te durduruldu → istemci "yeniden bağlanıyor". Backend 08:39:04'te açıldı → istemci kendiliğinden yeniden bağlandı ve **yeni snapshot** aldı (2.977 araç), sonra update'ler devam etti. (Tarayıcıdaki rozet ve şerit davranışı 15 Ekim'de doğrulanmıştı: `docs/frontend-decisions.md` madde 13.)

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

**Sonuç (22 Ekim 08:40:44-08:43:44 Oslo):** İki .NET test istemcisi 3 dk: HIZLI (mesajı hemen işler) ve yavaş (her mesajda 15 sn bekler). 90. sn'de sistem durumu normal: okuma 201 ms, yazma 49 ms, bağlı istemci 2, kayıp parti 0. HIZLI 32 update aldı ve sonuna kadar güncel kaldı; yavaş istemci 11 update işleyebildi ve geride kaldı (son aldığı v34, o sırada HIZLI v54'teydi). **Diğer istemci ve veri toplama etkilenmedi.** Not: bu yerel (loopback) testte yavaş istemcinin mesajları işletim sisteminin TCP tamponlarında birikti; sunucu tarafı birleştirme ancak bu tamponlar dolunca devreye girer, o yüzden yavaş istemci önce eski mesajları sırayla aldı. Birleştirmenin kendisi `LiveClientFeedTests` ile doğrulanıyor.

## Test 7 — Silmenin 3 saat beklemeden gösterilmesi

> ⚠️ Bu test **veritabanındaki verileri siler** (temiz başlangıç gerekiyor: chunk aralığı değişikliği sadece yeni chunk'ları etkiler, eski 15 dk'lık chunk'lar varken silme ancak ~20 dk sonra görülür).

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

**Sonuç:** Çalıştırılmadı (veritabanını siler). Silmenin normal ayarla çalıştığının kanıtı `docs/observations.md` "Saklama / silme gerçekte çalışıyor" (14 Ekim: silme işi 71 kez çalıştı, 0 hata).

---

## Sonuç tablosu (README'ye gidecek)

| Senaryo | Ne oldu | Veri kaybı | Not |
|---|---|---|---|
| 1. Kaynak erişilemez | Çalıştırılmadı; aynı kod yolu Test 2'nin HTTP hatası adımında denendi | Kesinti süresi kadar (kaçınılmaz) | |
| 2. Bozuk yanıt | Başarısız okuma sayıldı, uygulama ayakta, adres düzelince 20 sn içinde devam | Okunamayan süre (kaçınılmaz); bozuk veri DB'ye girmedi | 22 Ekim |
| 3. DB 3 dk kapalı | Okuma ve canlı durum devam etti, bekleyen parti 31'e çıktı, DB dönünce 5 sn'de 0; geçmiş ucu 503 | **Yok** (dakika başına satırda delik yok) | 22 Ekim |
| 4. Uygulama yeniden başlatma | Düzgün kapanış, ~1 sn'de geri geldi | Görünür kayıp yok | 22 Ekim |
| 4b. DB kapalıyken yeniden başlatma | `Kapanışta tampon boşaltılamadı, 10 parti kayboldu.` | **10 parti** (bilinen eksik) | 22 Ekim |
| 5. İstemci kopar | Otomatik yeniden bağlandı, yeni snapshot aldı | Yok | 22 Ekim, .NET test istemcisi |
| 6. Yavaş istemci | Diğer istemci ve toplama etkilenmedi; yavaş istemci geride kaldı | Yok | 22 Ekim, .NET test istemcisi |
| 7. Silme gösterimi | Çalıştırılmadı (DB'yi siler); normal ayarla silme 14 Ekim'de doğrulandı | — | |
