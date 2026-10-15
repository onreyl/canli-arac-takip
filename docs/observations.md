# Entur Vehicle Positions — Gerçek Veri Gözlemleri

Kaynak: `https://api.entur.io/realtime/v1/gtfs-rt/vehicle-positions`
Araç: [`tools/FeedExplorer`](../tools/FeedExplorer) (3 sn'de bir çekip istatistik toplayan konsol uygulaması)
Tarih: 12-13 Ekim 2026. Ölçümler kısa sürelidir (dakikalar); uzun ölçümle güncellenecek.

## Yanıt

| Ölçüm | Değer |
|---|---|
| Boyut (sıkıştırmasız) | ~1,6-1,7 MB |
| Boyut (gzip) | ~450 KB |
| Okuma süresi (çekme + protobuf çözme) | 220-500 ms (ilk istek ~800 ms) |
| `header.incrementality` | `FULL_DATASET` (her yanıt tam liste) |
| `ET-Client-Name` başlığı olmadan | 200 OK (zorunlu değil, yine de gönderiyoruz) |

## Spesifikasyondan farklı davranışlar

1. **Bir varlık bir araç değildir.**
   - ~8.200-8.600 varlığa karşılık ~3.300 farklı araç kimliği (`vehicle.vehicle.id`) var.
   - Varlık kimliği `sefer-tarih-araç` biçiminde, ör. `BRA:ServiceJourney:6660_..._1022-20261012-258010350`. Aynı araç gün içinde yaptığı her sefer için ayrı varlık olarak geliyor; bir araç 42 kez görüldü.
   - Varlık kimliği tekil, araç kimliği tekil değil.

2. **Bitmiş seferler son konumlarıyla feed'de kalıyor.**
   - 13 Ekim 20:20 (TR saati) ölçümünde 8.244 varlığın **4.655'inin (%56)** ölçüm zamanı 3 saatten eski.
   - Yani feed'in yarısından fazlası 3 saatlik saklama penceresinin dışında.

3. **Feed her 3 sn'de değişmiyor.**
   - Turların kabaca yarısında `header.timestamp` bir öncekiyle aynı.
   - Zaman aynıysa içerik byte byte aynı.

4. **Feed zamanı geri gidebiliyor.**
   - Ör. `...551` geldikten sonra `...549` geldi.
   - Muhtemel sebep: Entur'un arkasında yük dengeleyici ve birden fazla, hafif farklı durumda sunucu.

5. **Yeni konum sayısı düşük.**
   - Zamanı ilerlemiş bir feed'de, bir önceki tura göre ölçüm zamanı ilerleyen ~700-2.150 varlık var.
   - Yalnızca bunları yazmak kabaca **saatte ~1,15 milyon satır** eder. Her turda tüm varlıkları yazmak ise ~10 milyon satır olurdu (~9 kat fark).
   - Bu tahmin 40 sn'lik ölçüme dayanıyor; kesinleştirilecek.

6. **DeadRun (yolcusuz boş sefer).**
   - Örnek ölçümde 988 varlık (%12).
   - `trip.route_id` boş, `trip.trip_id` içinde `DeadRun` geçiyor, ör. `BOR:DeadRun:260907141040052_0`.

7. **`position.bearing` çoğu zaman gönderilmiyor; "0" görünmesi yanıltıcı.**
   - proto2'de gönderilmemiş sayı alanı varsayılan değer (0) olarak okunur. Gerçekten gönderilip gönderilmediğini `HasBearing` söyler.
   - 14 Ekim 12:19 (TR) ölçümü, 8.049 konum: **4.082'sinde yön hiç gönderilmemiş**, sadece 35'i gerçekten 0 (tam kuzey).
   - `HasX` kontrolü yapılmazsa haritada ~4.000 araç kuzeyi gösteren okla çizilirdi. Gönderilmeyen alanlar `null` saklanıyor.

8. **Diğer opsiyonel alanlar da çoğunlukla eksik** (aynı ölçüm, 8.049 konum):
   - `speed`: 7.857'sinde yok.
   - `occupancy_status`: 4.455'inde yok.
   - `current_status`: 193'ünde yok.
   - `route_id`: 1.020'sinde yok, ve bunların **hepsi** DeadRun (`route_id` yalnızca boş seferlerde eksik).
   - Konumu, araç kimliği ya da ölçüm zamanı eksik varlık: 0.

## Hacim ölçümü

"Yazılacak satır": Karar 1'in kuralına göre yazılacak satırlar, yani daha önce görülmemiş `(entity_id, measured_at)` çiftleri ve ölçüm zamanı 3 saatten yeni olanlar. İlk tur başlangıç yüklemesi olduğu için hesaba katılmadı.

| Ölçüm | Saat (TR / Oslo) | Süre | Yazılacak satır / saat | 3 saat |
|---|---|---|---|---|
| Öğleden sonra (kısa) | 15:53 / 14:53 | 40 sn | ~1,15 milyon | ~3,5 milyon |
| Akşam | 21:03-21:23 / 20:03-20:23 | 20 dk | **~358 bin** | **~1,07 milyon** |
| **Akşam yoğun saati (gerçek backend)** | 16:05-17:05 / 15:05-16:05 | 1 saat | **~949 bin** | **~2,85 milyon** |

- Hacim günün saatine göre ~3 kat değişiyor (akşam ~358 bin, yoğun saat ~949 bin satır/saat). Tasarım yoğun saate göre yapıldı.
- Yoğun saat ölçümü gerçek backend'le yapıldı (14 Ekim, 70 dk kesintisiz çalışma). Satır sayısı, ölçüm zamanı 16:05-17:05 (TR) aralığındaki satırların DB'deki sayımıdır. Bu sırada canlı araç sayısı ~3.200'dü (öğlen ~2.150).
- Aynı 70 dakikada: hata 0, kayıp parti 0, başarısız okuma 0, başarısız yazma 0.

## Disk boyutu (gerçek veri, 14 Ekim)

1.120.117 satır için `hypertable_detailed_size`:

| Bölüm | Boyut |
|---|---|
| Tablo (satırlar) | 227 MB |
| Index'ler (3 adet) | 208 MB |
| **Toplam** | **435 MB → satır başına ~407 byte** |

| | Satır | Disk |
|---|---|---|
| Yoğun saatte 1 saat | ~949 bin | **~390 MB** |
| Yoğun saatte 3 saat (saklama penceresi) | ~2,85 milyon | **~1,15 GB** |
| Akşam 3 saat | ~1,07 milyon | ~430 MB |

**Neden satır başına 407 byte?** Diskin yarıya yakını index'ler. En büyük sütun `entity_id`: ortalama 60 byte, hem tabloda hem birincil anahtar index'inde tutuluyor. Diğer metin sütunları: `trip_id` 41, `stop_id` 15, `route_id` 14, `vehicle_id` 10 byte.

Zaman olsaydı yapılabilecek iyileştirmeler (README "bilinen eksikler"):
- **TimescaleDB sıkıştırması:** ör. 30 dk'dan eski chunk'lar sütun bazlı sıkıştırılır. Bu tür tekrarlı veride genelde çok büyük kazanç sağlar. Saklama 10 güne çıksaydı (~76 milyon satır, ~30 GB sıkıştırmasız) gerekli olurdu.
- **`entity_id` gereksiz tekrar:** `trip_id + tarih + vehicle_id`'den oluşuyor. Ayrı saklamak yerine `(trip_id, service_date, vehicle_id)` ya da kısa bir hash kullanmak satırı ~60 byte küçültürdü.

## Saklama / silme gerçekte çalışıyor

- 14 Ekim 17:10 (TR) itibarıyla en eski chunk 14:00'te başlıyor; sabahki test verisi (12:xx) otomatik silinmiş.
- Silme işi 71 kez çalışmış, 0 hata.
- O anda 3 saatten eski 160 satır vardı: hepsi en eski chunk'ın (14:00-14:15) içinde, chunk tamamen süresini doldurunca silinecek. Beklenen davranış: chunk bütün olarak silindiği için bir chunk süresi (15 dk) kadar fazla kalabilir. API sorguları zaten 3 saate kırpıyor.

**Akşam ölçümünün ayrıntıları (401 tur, 20 dk):**
- Feed durumları: YENİ 196 (%49), aynı 146 (%36), **GERİ 58 (%15)**, hata 0.
- Okuma süresi (çekme + çözme): ortalama 251 ms, en fazla 1.651 ms. En kötü durumda bile 3 sn'lik aralığın yarısının altında.
- "YENİ" bir feed'de ortalama ~600 yazılacak satır var.

## Araçların tazeliği

Her araç kimliğinin en yeni ölçümünün yaşı (akşam ölçümü sonu, 2.127 farklı araç):

| Son ölçümün yaşı | Araç |
|---|---|
| < 2 dk | 907 |
| 2-5 dk | 30 |
| 5-15 dk | 59 |
| 15-60 dk | 50 |
| 1-3 saat | 224 |
| > 3 saat | 857 |

Dağılım iki tepeli. Aktif araçlar 2 dk'dan sık konum gönderiyor; geri kalanların büyük kısmı saatlerdir sessiz (seferi bitmiş, garajda). 2-15 dk aralığı çok az.
Haritada "canlı" eşiği için doğal bir sınır var gibi: ~5 dk. Karar frontend tasarımında verilecek.

## Haritada görülen: İsveç'teki araçlar

Canlı haritada Stockholm, Göteborg ve Kopenhag civarında da araçlar görünüyor. Entur Norveç verisi ama sınır ötesine giden
tren/otobüs hatlarının araçları seyir boyunca raporlanmaya devam ediyor (15 Ekim gözlemi).

## Hangi operatörler var? (15 Ekim)

Canlı durumdaki 2.309 aracın hat kimliği önekine göre dağılımı: TRO 315, AKT 299, NOR 293, BRA 219, ATB 217, KOL 185,
MOR 125, OST 120, (boş sefer) 107, FIN 105, VKT 90, VYG 68, VYX 52, TEL 52, VYB 30, FLT 13, GOA 10, NBU 9.

**Oslo'nun Ruter'i (RUT) ve Bergen'in Skyss'i (SKY) bu akışta yok.** Bu yüzden Oslo ve Bergen şehir içi seyrek; oradaki noktalar
çevre operatörlerden (Brakar, Østfold vb.) ve trenlerden geliyor. Bizim tarafımızda bir filtreleme yok, kaynak bunları göndermiyor.

## Tasarıma etkileri (Gün 2'de karara bağlanacak)

- Turlar arası takip ve tekrar kontrolü `entity.id` + ölçüm zamanı üzerinden yapılmalı.
- Feed zamanı değişmemişse veya geri gitmişse tur atlanabilir.
- 3 saatten eski ölçümlerin yazılıp yazılmayacağı, haritada gösterilip gösterilmeyeceği kararı gerekiyor.
- Haritadaki "araç" kavramı varlık mı, araç kimliği mi (aracın en yeni varlığı) olacak, karar gerekiyor.
