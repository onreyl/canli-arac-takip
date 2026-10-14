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

7. **`position.bearing` sık sık tam 0.** Gerçek kuzey yönü mü, yoksa gönderilmemiş alan mı, belirsiz.

## Hacim ölçümü

"Yazılacak satır": Karar 1'in kuralına göre yazılacak satırlar, yani daha önce görülmemiş `(entity_id, measured_at)` çiftleri ve ölçüm zamanı 3 saatten yeni olanlar. İlk tur başlangıç yüklemesi olduğu için hesaba katılmadı.

| Ölçüm | Saat (TR / Oslo) | Süre | Yazılacak satır / saat | 3 saat |
|---|---|---|---|---|
| Öğleden sonra (kısa) | 15:53 / 14:53 | 40 sn | ~1,15 milyon | ~3,5 milyon |
| Akşam | 21:03-21:23 / 20:03-20:23 | 20 dk | **~358 bin** | **~1,07 milyon** |
| Sabah yoğun saati | — | — | *ölçülecek* | |

- Hacim günün saatine göre ~3 kat değişiyor. Tasarım yoğun saate göre yapılmalı.
- Öğleden sonra ölçümü sadece 40 sn sürdü; sabah yoğun saatinde 20 dk'lık ölçümle kesinleşecek.
- Disk boyutu Gün 3'te gerçek tabloya yazılınca `pg_total_relation_size` ile ölçülecek.

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

## Tasarıma etkileri (Gün 2'de karara bağlanacak)

- Turlar arası takip ve tekrar kontrolü `entity.id` + ölçüm zamanı üzerinden yapılmalı.
- Feed zamanı değişmemişse veya geri gitmişse tur atlanabilir.
- 3 saatten eski ölçümlerin yazılıp yazılmayacağı, haritada gösterilip gösterilmeyeceği kararı gerekiyor.
- Haritadaki "araç" kavramı varlık mı, araç kimliği mi (aracın en yeni varlığı) olacak, karar gerekiyor.
