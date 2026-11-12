# Performans ölçümleri

Repo kökünden çalıştırın:

```sh
dotnet run -c Release --project backend/benchmarks/VehicleTracking.Benchmarks -- --filter '*' --job short
```

Yalnızca listelemek için son argümanları `--list flat` yapın. Sonuçlar BenchmarkDotNet.Artifacts altında oluşur; Allocated ve Gen2 sütunları özellikle feed çekmedeki büyük nesne tahsislerini karşılaştırmak içindir.

Veri, Entur'un gerçek GTFS-RT vehicle-positions yanıtıdır. Repo kökünde indirin (PowerShell'de gerekirse curl.exe kullanın):

```sh
curl --compressed -H "ET-Client-Name: sirket-canli-arac-takip-benchmarks" https://api.entur.io/realtime/v1/gtfs-rt/vehicle-positions -o backend/benchmarks/VehicleTracking.Benchmarks/data/vehicle-positions.pb
```

Dosya gzip'siz protobuf olmalıdır; derlemede çıktı dizinine kopyalanır. Önce/sonra karşılaştırmalarında aynı veri dosyasını kullanın. Ölçüm sırasında ağ veya disk okuması yapılmaz.

- FeedFetch: gerçek yanıtın tamamı, her istekte yeni MemoryStream ve StreamContent üzerinden FetchAsync'e verilir.
- LiveBroadcast: snapshot'ını almış 1/10/50 istemci için 1.500 araçlık Merge → TakePending → UTF-8 JSON. SignalR ile aynı camelCase ve metin enum ayarları kullanılır; ağ, dispatcher ve SignalR mesaj zarfı ölçülmez.
- LiveState: 3.300 araç üzerinde 1.500 yeni konumun Apply maliyeti ve 1/10 ardışık GetSnapshot çağrısı. Apply öncesinde IterationSetup durumu yeniler; böylece her iterasyonda gerçekten yeni konumlar uygulanır (iterasyon başına tek çağrı). SnapshotCount yalnızca GetSnapshot iş yükünü değiştirir; Apply iki parametre satırında aynı işi ölçer.

Canlı durum hazırlığında araç başına en yeni gerçek kayıt alınır ve araç kimliğine göre sıralanır. İş yükünü sabitlemek için ilk 3.300 araç kullanılır; veri daha küçükse gerçek kayıtlar benzersiz benchmark kimlikleriyle çoğaltılır. Başlangıç ölçüm zamanları dosyadaki en yeni ölçüme eşitlenir, turun ilk 1.500 konumu 3 saniye ileri alınır. Böylece dosya yaşlandıkça araçlar silinmez ve ölçüm boyunca durum boyutu sabit kalır; bu senaryo retention nedeniyle silme maliyetini içermez.
