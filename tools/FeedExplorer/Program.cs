using System.Diagnostics;
using System.Net;
using TransitRealtime;

// Entur'un gerçek zamanlı araç konumu GTFS-RT feed URL'si
const string FeedUrl = "https://api.entur.io/realtime/v1/gtfs-rt/vehicle-positions";

// Feed'i ne sıklıkla çekeceğimiz (case dokümanı 3 sn istiyor)
var interval = TimeSpan.FromSeconds(3);

// Sunucu gzip ile sıkıştırılmış gönderebilsin; HttpClient açmayı otomatik yapar.
// Neden: ölçümde yanıt sıkıştırmasız ~1,7 MB, gzip ile ~450 KB (4 kat küçük) ve belirgin şekilde daha hızlı geldi.
var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip };

// HTTP istemcisi oluşturulur ve Entur'un istediği kimlik başlığı eklenir
// Not: başlıksız istek de 200 dönüyor; ama Entur istemcilerin kendini tanıtmasını istiyor,
// tanıtmayanlara daha sıkı limit uygulayabilir (doğrulanmadı). Backend'de bu değer config'ten gelecek.
using var http = new HttpClient(handler);
http.DefaultRequestHeaders.Add("ET-Client-Name", "onure-canliaractakip");

// Ctrl+C'ye basınca program aniden kapanmasın, döngüden düzgünce çıkıp özet yazsın
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// İsteğe bağlı süre (dakika): "dotnet run -- 20" → 20 dk ölçüp kendiliğinden durur (uzun hacim ölçümü için)
if (args.Length > 0 && int.TryParse(args[0], out int minutes))
{
    cts.CancelAfter(TimeSpan.FromMinutes(minutes));
}

// Turlar arası hafıza: her varlığın en son gördüğümüz ölçüm zamanı (varlık id → timestamp)
// GERÇEK VERİ GÖZLEMİ: araç id (entity.Vehicle.Vehicle.Id) tekil DEĞİL.
//   Feed'de ~8.600 varlık var ama sadece ~3.300 farklı araç. Aynı araç günde yaptığı her sefer için
//   ayrı bir varlık olarak geliyor (bir araç 42 kez görüldü); bitmiş seferler son konumlarıyla listede kalıyor.
//   Varlık id (entity.Id) ise "sefer-tarih-araç" biçiminde ve tekil, ör:
//   "BRA:ServiceJourney:6660_..._1022-20261012-258010350"
//   İlk denemede araç id'ye göre tuttuk; aynı id'li varlıklar birbirinin üstüne yazıp sayıları şişirdi.
var lastSeen = new Dictionary<string, ulong>();
ulong lastFeedTimestamp = 0;
int poll = 0;

// Hacim ölçümü için sayaçlar
long retentionSeconds = (long)TimeSpan.FromHours(3).TotalSeconds;
long toWriteAfterFirstPoll = 0;   // ilk tur hariç: ilk tur "başlangıç yüklemesi", saatlik hızı şişirir
var sinceFirstPoll = new Stopwatch();
var feedStateCounts = new Dictionary<string, int> { ["YENİ"] = 0, ["aynı"] = 0, ["GERİ"] = 0, ["HATA"] = 0 };
long fetchMsTotal = 0, fetchMsMax = 0;
FeedMessage? lastFeed = null;

Console.WriteLine($"Her {interval.TotalSeconds} sn'de bir çekiliyor. Durdurmak için Ctrl+C.");

// do-while: ilk tur hemen çalışsın, sonrakiler timer'ı beklesin
// Neden PeriodicTimer: bir tur 3 sn'den uzun sürerse kaçan turlar birikmez, aynı anda iki istek asla çalışmaz.
using var timer = new PeriodicTimer(interval);
try
{
    do
    {
        poll++;

        // Çekme + çözme süresini ölçüyoruz (README'deki "okuma döngüsü süresi" için)
        var sw = Stopwatch.StartNew();
        FeedMessage feed;
        int size;
        try
        {
            byte[] bytes = await http.GetByteArrayAsync(FeedUrl, cts.Token);
            feed = FeedMessage.Parser.ParseFrom(bytes);
            size = bytes.Length;
        }
        catch (Exception ex) when (ex is HttpRequestException or Google.Protobuf.InvalidProtocolBufferException
                                   || (ex is TaskCanceledException && !cts.IsCancellationRequested))
        {
            // Ağ hatası, bozuk yanıt veya HttpClient zaman aşımı: bu turu atla, programı çökertme.
            // Dikkat: HttpClient zaman aşımı TaskCanceledException fırlatır; ayırt etmezsek
            // dıştaki "Ctrl+C" catch'i onu yakalar ve döngü sessizce biterdi.
            feedStateCounts["HATA"]++;
            Console.WriteLine($"#{poll} HATA: {ex.Message}");
            continue;
        }
        sw.Stop();
        fetchMsTotal += sw.ElapsedMilliseconds;
        fetchMsMax = Math.Max(fetchMsMax, sw.ElapsedMilliseconds);

        // Feed bir önceki turdan daha yeni mi, aynı mı, yoksa geriye mi gitti?
        // GERÇEK VERİ GÖZLEMLERİ:
        //   - Header zamanı aynıysa içerik byte byte aynı → "bu feed'i zaten işledim" için güvenilir işaret.
        //   - Feed her 3 sn'de değişmiyor; turların kabaca yarısı "aynı" geliyor.
        //   - Zaman damgası GERİ gidebiliyor (ör. ...551'den sonra ...549 geldi). Muhtemelen Entur'un arkasında
        //     yük dengeleyici + birden fazla sunucu var. Bu yüzden "en büyük" zamanı tutuyoruz (Math.Max),
        //     eski bir feed'i yeni sanmıyoruz.
        ulong feedTs = feed.Header.Timestamp;
        string feedState = feedTs > lastFeedTimestamp ? "YENİ"
                         : feedTs == lastFeedTimestamp ? "aynı"
                         : "GERİ";
        lastFeedTimestamp = Math.Max(lastFeedTimestamp, feedTs);
        feedStateCounts[feedState]++;
        lastFeed = feed;
        long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Kaç varlık bu turda yeni bir konumla geldi? (ölçüm zamanı öncekinden büyükse yeni sayıyoruz)
        // Neden önemli: veritabanına sadece yeni konumları yazarsak satır sayısı ~9 kat azalıyor
        //   (ilk ölçüm: "YENİ" turda ~1.400-2.150 yeni konum; saatte ~1,15 milyon vs. hepsini yazarsak ~10 milyon).
        // Not: C#'ta gönderilmemiş alt nesne (Vehicle, Position, Trip) null gelir, string alan "" gelir.
        //   Bu feed'de her varlıkta Vehicle dolu olduğu için burada kontrol etmiyoruz; backend'de edeceğiz.
        int newPositions = 0;
        // "Yazılacak" = yeni konum VE ölçüm zamanı saklama penceresinin (3 saat) içinde
        int toWrite = 0;
        foreach (FeedEntity entity in feed.Entity)
        {
            ulong measuredAt = entity.Vehicle.Timestamp;
            if (!lastSeen.TryGetValue(entity.Id, out ulong previous) || measuredAt > previous)
            {
                newPositions++;
                lastSeen[entity.Id] = measuredAt;
                if ((long)measuredAt >= nowUnix - retentionSeconds)
                {
                    toWrite++;
                }
            }
        }

        if (poll == 1)
        {
            sinceFirstPoll.Start();
        }
        else
        {
            toWriteAfterFirstPoll += toWrite;
        }

        Console.WriteLine(
            $"#{poll} {DateTime.Now:HH:mm:ss} | {sw.ElapsedMilliseconds} ms | {size / 1024} KB | " +
            $"feed {feedState} | varlık {feed.Entity.Count} | yeni konum {newPositions} | yazılacak {toWrite}");

        // Anlık görüntü istatistiklerini sadece ilk turda yazdır (her turda ekranı doldurmasın)
        if (poll == 1)
        {
            PrintSnapshotStats(feed);
        }
    }
    while (await timer.WaitForNextTickAsync(cts.Token));
}
catch (OperationCanceledException)
{
    // Ctrl+C'ye basıldı, normal çıkış
}

Console.WriteLine();
Console.WriteLine($"Toplam tur: {poll}, görülen farklı varlık: {lastSeen.Count}");
PrintVolumeSummary();
if (lastFeed is not null)
{
    PrintFreshness(lastFeed);
}

// Uzun ölçümün özeti: README'deki "saatlik ve 3 saatlik veri hacmi" ve "okuma döngüsü süresi" için
void PrintVolumeSummary()
{
    double hours = sinceFirstPoll.Elapsed.TotalHours;
    int successfulPolls = poll - feedStateCounts["HATA"];
    Console.WriteLine();
    Console.WriteLine($"Süre: {sinceFirstPoll.Elapsed:hh\\:mm\\:ss} (ilk turdan sonra)");
    Console.WriteLine($"Feed durumları: YENİ {feedStateCounts["YENİ"]}, aynı {feedStateCounts["aynı"]}, " +
                      $"GERİ {feedStateCounts["GERİ"]}, HATA {feedStateCounts["HATA"]}");
    if (successfulPolls > 0)
    {
        Console.WriteLine($"Okuma süresi: ortalama {fetchMsTotal / successfulPolls} ms, en fazla {fetchMsMax} ms");
    }
    if (hours > 0)
    {
        double perHour = toWriteAfterFirstPoll / hours;
        Console.WriteLine($"Yazılacak satır (ilk tur hariç): {toWriteAfterFirstPoll:N0}");
        Console.WriteLine($"  → saatte ~{perHour:N0}, 3 saatte ~{perHour * 3:N0}");
    }
}

// Son ölçümü çok eski araçları haritada "canlı" gibi göstermemek için
// her araç kimliğinin en yeni ölçümünün yaş dağılımına bakıyoruz.
static void PrintFreshness(FeedMessage feed)
{
    long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    // Araç kimliği tekil değil (bkz. lastSeen yorumu): her araç için en yeni ölçümü alıyoruz
    List<long> agesInMinutes = feed.Entity
        .Where(e => e.Vehicle?.Vehicle is not null)
        .GroupBy(e => e.Vehicle.Vehicle.Id)
        .Select(g => (nowUnix - (long)g.Max(e => e.Vehicle.Timestamp)) / 60)
        .ToList();

    Console.WriteLine();
    Console.WriteLine($"Farklı araç: {agesInMinutes.Count:N0}. Son ölçümün yaşı:");
    Console.WriteLine($"  < 2 dk     : {agesInMinutes.Count(a => a < 2):N0}");
    Console.WriteLine($"  2-5 dk     : {agesInMinutes.Count(a => a >= 2 && a < 5):N0}");
    Console.WriteLine($"  5-15 dk    : {agesInMinutes.Count(a => a >= 5 && a < 15):N0}");
    Console.WriteLine($"  15-60 dk   : {agesInMinutes.Count(a => a >= 15 && a < 60):N0}");
    Console.WriteLine($"  1-3 saat   : {agesInMinutes.Count(a => a >= 60 && a < 180):N0}");
    Console.WriteLine($"  > 3 saat   : {agesInMinutes.Count(a => a >= 180):N0}");
}

// Tek bir feed üzerinden istatistikler: burayı sen dolduracaksın
static void PrintSnapshotStats(FeedMessage feed)
{
    // Varlıkları araç konumu listesine çeviriyoruz, böylece her görevde entity.Vehicle yazmana gerek kalmaz
    List<VehiclePosition> vehicles = feed.Entity.Select(e => e.Vehicle).ToList();

    // Şu anki zaman, Unix saniye cinsinden (v.Timestamp ile aynı birimde)
    long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    // GÖREV 1: Seferi DeadRun olan araç sayısı
    // DeadRun: yolcu taşımayan "boş sefer" (garaja dönüş, başlangıç noktasına gidiş). Hat (RouteId) boş geliyor,
    //   TripId'de "DeadRun" geçiyor, ör: "BOR:DeadRun:260907141040052_0". Hat filtresinde sorun çıkarabilir.


    int deadRunCount = vehicles.Count(x => 
        x.Trip is not null && x.Trip.TripId.Contains("DeadRun"));

    // GÖREV 2: Ölçüm zamanı 3 saatten eski olan araç sayısı
    // Neden: bitmiş seferler feed'de son konumlarıyla kalıyor; 4 saat önceki ölçümler gördük.
    //   Bizim saklama penceremiz 3 saat, bu varlıklarla ne yapacağımıza karar vermek için sayısını bilmemiz lazım.
    long threeHoursInSeconds = (long)TimeSpan.FromHours(3).TotalSeconds;
    int olderThan3hCount = vehicles.Count(x => (long)x.Timestamp < nowUnix - threeHoursInSeconds);

    Console.WriteLine();
    Console.WriteLine($"  DeadRun: {deadRunCount}");
    Console.WriteLine($"  3 saatten eski: {olderThan3hCount}");
    Console.WriteLine();
}

