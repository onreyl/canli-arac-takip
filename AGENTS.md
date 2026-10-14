# Kodlama ajanları için kurallar (Codex)

Bu repoda iş akışı şöyle: **Claude görevi tarif eder, sen (Codex) yazarsın, Claude kontrol eder, insan commit atar.**
Proje bağlamı ve tüm gereksinimler `CLAUDE.md`'de; işe başlamadan önce oku. Mimari: `docs/architecture.md`.
Gerçek veri gözlemleri (tasarımın gerekçeleri): `docs/observations.md`.

## Kesin kurallar

- **Sadece görevde istenen dosyalara dokun.** Görevin dışında bir değişiklik gerekli görünüyorsa yapma; sonda raporla.
- **Tasarım kararı verme.** Görevde açıkça yazmayan bir seçim (kütüphane, endpoint adı, eşik değeri, veri modeli) gerekiyorsa
  en makul seçeneği uygula ama raporda "KARAR:" başlığıyla açıkça belirt ki gözden geçirilsin.
- **git commit / push / branch yapma.** `git` sadece okumak için (status, diff, log).
- `notes/`, `docs/observations.md`, `CLAUDE.md`, `AGENTS.md` dosyalarını değiştirme.
- Gizli bilgi (şifre, bağlantı cümlesi) dosyaya yazma. Bağlantı cümlesi user-secrets'ta / ortam değişkeninde.
- Bitirmeden önce `dotnet build CanliAracTakip.slnx` uyarısız ve hatasız geçmeli. Test projesi varsa `dotnet test` de geçmeli.

## Mimari kurallar (backend)

- Katmanlar: `Domain` ← `Application` ← `Infrastructure` ← `Api`. Bağımlılık hep içe doğru.
  - Domain hiçbir şeye bağımlı değil.
  - Application, Infrastructure'ı ve ASP.NET'i bilmez; arayüzleri tanımlar.
  - Protobuf (GTFS-RT) tipleri Infrastructure dışına çıkmaz (`Access="Internal"`).
- Infrastructure'daki uygulama sınıfları `internal sealed`; dışarıya `DependencyInjection.cs` içindeki uzantı metotlarıyla açılır.
- Toplu yazma Npgsql ile (EF Core değil); EF Core şema/migration/okuma için.
- Canlı yayın hatası kalıcılık kolunu (tampona/DB'ye yazma) asla engellememeli.

## Kod stili

- .NET 10, C# son sürüm: primary constructor, `sealed`, `record`, collection expression, `Lock`.
- Loglama `[LoggerMessage]` kaynak üreteciyle, mesajlar Türkçe.
- "Şimdi" zamanı için `TimeProvider` enjekte et, `DateTime.UtcNow` kullanma.
- Ayarlar Options pattern ile (`appsettings.json` bölümü + options sınıfı), sabit değer gömme.
- **Yorumlar Türkçe ve sadece "neden"i anlatır** (tasarım gerekçesi, gerçek veri gözlemi, tuzak). "Ne yaptığını" anlatan yorum yazma.
  Public API'lere kısa `/// <summary>` yaz (Swagger'da görünüyor).
- Mevcut dosyaların üslubunu taklit et; yeni bir kalıp getirmeden önce benzeri repoda var mı bak.

## Bitirince rapor

Son mesajında şunları listele:
1. Değiştirdiğin / eklediğin dosyalar ve her birinde ne yaptığın (1 satır)
2. "KARAR:" ile verdiğin her seçim ve gerekçesi
3. Çalıştırdığın doğrulamalar (build/test) ve sonuçları
4. Emin olmadığın ya da gözden geçirilmesini istediğin noktalar
