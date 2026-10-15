# Backend — Zorunlu Maddelere Göre Eksik / Risk Listesi

## Karar durumu (15 Ekim akşamı)

| # | Karar | Durum |
|---|---|---|
| 1 | Docker Compose — yap | ✅ Yapıldı: backend + frontend Dockerfile, `docker compose up --build` tek komutla çalışıyor (.env gerekmez) |
| 2 | Kapanış süresi — yap | ✅ Yapıldı: host 25 sn, compose `stop_grace_period: 30s`, boşaltma 20 sn; `docker compose restart backend` ile düzgün kapanış doğrulandı |
| 3 | README senaryo açıklamaları | README'ye yaz (24-25 Ekim) |
| 4 | Disk tamponu | **Yapılmıyor**; `docs/known-limitations.md` madde 1 |
| 5 | Açılışta DB kapalı | README'ye yaz; `docs/known-limitations.md` madde 2 |
| 6 | Silme gösterimi | ✅ Compose'a ayarlar eklendi (`RETENTION`, `CHUNK_INTERVAL`...); prosedür `docs/resilience-tests.md` Test 7 |
| 7 | Silmenin kilitlememesi ölçümü | Öneri: şimdi yap (küçük) — onay bekliyor |
| 8 | "Son 1 saatte yazılan" bellekte | Öneri: README'ye yaz |
| 9 | Senaryoların denenmesi | Talimatlar `docs/resilience-tests.md`; denemeleri proje sahibi yapacak |
| 10 | Yavaş istemci gerçek ağda | Test 6; README'ye yaz |
| 11 | En fazla bekleme 30 sn | ✅ Yapıldı: 10 sn |
| 12 | HTTPS yönlendirmesi | ✅ Yapıldı: sadece Development'ta |

Tarih: 15 Ekim 2026. Kaynak: case dokümanı 3.1, 3.2, 3.3 ve 5. bölüm. **Bu listede kod değişikliği yapılmadı**; düzeltmeler onayla yapılacak.

Öncelik: **P0** = teslimde eksik kalırsa doğrudan bir zorunlu maddeyi karşılamaz · **P1** = madde karşılanıyor ama bir
dayanıklılık senaryosunda ya da gösterimde zayıf · **P2** = küçük / kozmetik.

## Özet tablo

| # | Madde (doküman) | Durum | Öncelik |
|---|---|---|---|
| 1 | `docker compose up` ile her şey (DB + backend + frontend) tek komutla kalkar | ❌ Sadece DB var; backend/frontend Dockerfile'ları yok | **P0** |
| 2 | Uygulama çalışırken yeniden başlatılırsa veri kaybı yok — Docker'ın kapanış süresi | ⚠️ Docker 10 sn'de öldürür, bizim tampon boşaltma 20 sn | **P0** |
| 3 | Her dayanıklılık senaryosunda verinin akıbeti README'de | ❌ Henüz yazılmadı (README yok) | **P0** |
| 4 | Uygulama ani çökerse / DB kapalıyken yeniden başlarsa tampondaki veri | ⚠️ Aşama 1 (bellek) — kaybolur; disk tamponu (Aşama 2) yapılmadı | **P1** |
| 5 | Açılışta DB kapalıysa veri toplama | ⚠️ DB gelene kadar toplama da, API/SignalR da başlamıyor | **P1** |
| 6 | Silme "3 saat beklemeden" gösterilebilmeli | ⚠️ Ayar var ama gösterim prosedürü denenmedi; bir tuzak var | **P1** |
| 7 | Silme yazmayı/yayını/sorguları kilitlememeli | ⚠️ Tasarım gereği öyle (chunk drop) ama **ölçülmedi** | **P1** |
| 8 | Sistem durumu: "son 1 saatte yazılan kayıt" | ⚠️ Bellekte; uygulama yeniden başlayınca 0'dan başlar | **P1** |
| 9 | Dayanıklılık senaryolarının gerçekten denenmesi | ⚠️ Sadece "DB 20 sn kapalı" ve "uygulama açılırken DB kapalı" denendi | **P1** |
| 10 | Yavaş istemci diğerlerini etkilemez | ⚠️ Mantık birim testli; gerçek yavaş ağda denenmedi | **P2** |
| 11 | DB uzun kapalı kalınca toparlanma gecikmesi | ℹ️ Bekleme 30 sn'ye kadar çıkıyor; DB döndükten sonra ≤30 sn gecikme | **P2** |
| 12 | HTTPS yönlendirmesi | ℹ️ Konteynerde sadece HTTP olacak; her açılışta uyarı logu | **P2** |

Karşılanan zorunlu maddeler (kısaca): 3 sn'lik okuma ve adres config'ten ✓ · açılışta kendiliğinden başlama ✓ ·
kalıcı yazma + son 3 saat + otomatik silme, süre config'ten ✓ · saatlik / 3 saatlik hacim gerçek veriyle ölçüldü ✓
(`docs/observations.md`) · WebSocket'te önce tam görüntü sonra değişiklikler ✓ · yeniden bağlananın tam görüntüyle dönmesi ✓ ·
yavaş istemci için istemci başına ayrı döngü + birleştirme ✓ · REST: anlık durum, sayfalı/hızlı geçmiş (~45 ms), sistem durumu ✓ ·
Swagger ✓ · gizli bilgi repoda yok ✓ · Conventional Commits ✓.

## Ayrıntılar

### 1. Docker Compose eksik (P0)
- Doküman: "docker-compose.yml ile veritabanı, backend ve frontend tek komutla ayağa kalkmalı; backend hemen veri toplamaya başlamalı."
- Şu an: compose'da sadece `db` servisi var. Backend ve frontend için Dockerfile yok.
- Gerekenler: backend Dockerfile (çok aşamalı .NET build), frontend Dockerfile (Next.js production build), compose'da
  `depends_on: db: condition: service_healthy`, bağlantı cümlesi ortam değişkeniyle (`ConnectionStrings__Postgres`),
  CORS'a frontend adresi, frontend için iki API adresi (tarayıcı: `NEXT_PUBLIC_API_BASE_URL`, sunucu: `API_INTERNAL_BASE_URL`).
- Not: `NEXT_PUBLIC_*` Next.js'te **build anında** koda gömülür; Dockerfile'da build argümanı olarak verilmeli. Tuzak.

### 2. Kapanış süresi uyuşmazlığı (P0)
- Docker `docker stop` / `docker compose stop` önce SIGTERM gönderir, **varsayılan 10 sn** sonra süreci öldürür.
- Bizim `PositionWriterWorker` kapanışta tamponu boşaltmak için 20 sn ayırıyor; .NET host'un kapanış süresi de varsayılan 30 sn.
- Sonuç: DB yavaş/kapalıyken uygulama durdurulursa Docker 10. saniyede öldürür, tampondaki partiler kaybolur — "uygulama
  çalışırken yeniden başlatılır" senaryosunda **önlenebilir bir kayıp**.
- Çözüm önerisi: compose'da backend için `stop_grace_period: 30s` ve kodda boşaltma süresini bunun altında tutmak
  (ör. host kapanış süresi 25 sn, boşaltma 20 sn). Küçük değişiklik.

### 3. README'de senaryo açıklamaları (P0)
- Doküman her senaryo için "verinin akıbeti (ne kaybolur, ne korunur, neden)" istiyor; tamamen önlenemeyen kayıp açıkça yazılmalı.
- Malzeme hazır: `docs/observations.md`, `notes/chatgpt-context.md` "Uçtan uca akış", Karar 4. Takvimde 24-25 Ekim.

### 4. Bellek tamponu ve ani çökme (P1)
- Aşama 1 (bellek `Channel`): düzgün kapanışta boşaltılıyor, **ani çökmede (kill -9) ya da DB kapalıyken yeniden başlatmada
  tampondakiler kayboluyor**. Tampon dolarsa (DB ~50 dk kapalı) en eski parti atılıyor (sayılıyor, loglanıyor).
- Aşama 2 (disk tamponu) planlıydı (Karar 4), yapılmadı. Arayüz (`IFeedBuffer`, al → yaz → onayla) buna hazır; değişecek tek
  yer `InfrastructureDependencyInjection` kaydı + yeni sınıf. Tahmini iş: yarım gün + Docker volume.
- Karar senin: (a) Aşama 2'yi yap, (b) Aşama 1'de kal ve README'de bu kaybı açıkça yaz. Doküman (b)'yi de kabul ediyor
  ("tamamen önleyemediğin bir kayıp varsa açıkça yazman gizlemenden değerlidir"), ama "veri kaybı kabul edilmez" çok yüksek önemde.

### 5. Açılışta DB kapalıysa (P1)
- `DatabaseInitializer` DB gelene kadar 5 sn'de bir dener ve **bu sürede diğer servisler başlamaz**: veri toplama, REST ve
  SignalR (Kestrel de bekliyor — 14 Ekim testinde "Now listening" ancak DB gelince yazıldı).
- Etkisi: compose'da healthcheck + `depends_on` sayesinde ilk açılışta sorun olmaz. Ama "DB kapalıyken uygulama yeniden
  başlatılırsa" o süre boyunca **hiç veri toplanmaz** (kaçınılmaz kayıp gibi görünür ama aslında önlenebilir; ancak Aşama 2
  disk tamponu olmadan önlenemez, çünkü toplanan veri bellekte bekler ve zaten kaybolabilir).
- Öneri: Aşama 2 yapılırsa başlatmayı DB'den bağımsız hale getirmek (toplama hemen başlasın, diske yazsın; DB gelince yazıcı
  boşaltsın). Aşama 2 yapılmazsa README'de yazmak yeterli.

### 6. "3 saat beklemeden" silme gösterimi (P1)
- `Storage:Retention` ve `Storage:ChunkInterval` config'ten okunuyor; açılışta retention policy yeniden kuruluyor ✓.
- **Tuzak:** `set_chunk_time_interval` sadece **yeni** chunk'ları etkiler. Mevcut 15 dk'lık chunk'lar olan bir DB'de saklamayı
  5 dk'ya çekersek ilk silinme ancak en eski 15 dk'lık chunk tamamen 5 dk'yı aşınca olur (~20 dk). Gösterim prosedürü:
  temiz bir DB (ayrı compose profili ya da volume silme) + `Retention=00:05:00`, `ChunkInterval=00:01:00`,
  `RetentionCheckInterval=00:00:30` → birkaç dakikada silindiği `timescaledb_information.chunks` ve `job_stats` ile görülür.
- Henüz denenmedi. README'de ve videoda gösterilecek.

### 7. Silmenin kilitlememesi ölçülmedi (P1)
- Tasarım: retention policy chunk'ı bütün olarak atar; yazma hep en yeni chunk'a gider.
- Eksik: bunun kanıtı. Öneri: silme işi çalışırken yazma süresini (`PositionWriter` süresi) ve geçmiş sorgusu süresini ölçüp
  README'ye koymak (ör. 5 dk saklama + 1 dk chunk ile 10 dk çalıştırıp yazma süresi dağılımına bakmak). Kod değişikliği:
  yazma süresini sayaçlara eklemek (küçük).

### 8. "Son 1 saatte yazılan kayıt" bellekte (P1)
- `IngestionMetrics` kayan pencereyle tutuyor; uygulama yeniden başlayınca 0'dan başlar. Swagger açıklamasında yazıyor.
- Alternatif: DB'den `count(*) where measured_at > now() - 1 saat` (yeniden başlatmaya dayanıklı, ama "ölçülme zamanı"na göre
  sayar, "yazılma zamanı"na göre değil; iki sayı çok yakın). Ya da ikisini birden göstermek.
- Karar senin; README'de hangisinin ne anlama geldiği yazılmalı.

### 9. Senaryoların gerçekten denenmesi (P1)
Denenen: DB 20 sn kapalı (delik yok), uygulama açılırken DB kapalı (bekleyip devam), istemci kopup geri gelme (frontend'de).
Denenmeyen:
- **Kaynak birkaç dakika erişilemez:** config'te Entur adresini geçersiz bir adrese çevirip birkaç dakika çalıştırmak; başarısız
  deneme sayacı ve health check "Degraded" görülmeli, adres düzelince toplama devam etmeli.
- **Bozuk yanıt:** adresi protobuf olmayan bir uca çevirmek (ör. `https://example.com`) → `InvalidProtocolBufferException` yolu.
- **DB birkaç dakika kapalı:** 20 sn yerine 3-5 dk; bekleyen parti sayısı, sonra boşalma, dakika bazında delik kontrolü.
- **Uygulama çalışırken yeniden başlatma:** `docker compose restart backend` (madde 2 düzeltildikten sonra).
- **Yavaş istemci:** Chrome DevTools "Slow 3G" (madde 10).
Bunlar için kod değişikliği gerekmez; sonuçlar README'ye.

### 10. Yavaş istemci gerçek ağda (P2)
- Birleştirme mantığı birim testli (`LiveClientFeedTests`); yerel loopback testinde TCP tamponları büyük olduğu için tetiklenmedi.
- Öneri: frontend açıkken Chrome "Slow 3G" ile ikinci bir sekme; diğer sekme ve `/api/system/status` etkilenmemeli.

### 11. Uzun DB kesintisinden sonra gecikme (P2)
- Yazıcı bekleme süresi 1 → 2 → 4 … → 30 sn. DB döndükten sonra yazmaya başlaması en fazla 30 sn gecikebilir. Kayıp değil.
- İstenirse en fazla bekleme 10 sn'ye indirilebilir (tek sabit). README'de yazmak yeterli.

### 12. HTTPS yönlendirmesi (P2)
- `app.UseHttpsRedirection()` var; konteynerde sadece HTTP dinleneceği için her açılışta "https portu bulunamadı" uyarısı çıkar.
  Konteyner içinde kaldırmak ya da sadece Development'ta açmak yeterli. Tek satır.

## Önerilen sıra (onayınla)

1. **P0 — madde 2** (kapanış süresi): küçük, hemen.
2. **P0 — madde 1** (Docker Compose): backend + frontend Dockerfile, compose. Frontend Dockerfile frontend koduna dokunmaz;
   sadece `frontend/Dockerfile` + `.dockerignore` ekler (yine de frontend klasörü olduğu için senin onayın lazım).
3. **P1 — madde 4/5 kararı:** disk tamponu mu, README'de açıklama mı?
4. **P1 — madde 6, 7, 9:** senaryoları dene + ölçümleri al (kod değişikliği yok ya da çok küçük).
5. **P1 — madde 8 kararı.**
6. P0 — madde 3: README (24-25 Ekim).
