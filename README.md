# Puzzle (çalışma adı)

iOS + Android için swap match-3 oyunu (Unity + C#). Tahtalar büyük ve boyutu bölümden bölüme değişiyor
(10x10, 10x12, 11x13 …). Bölümler algoritmik olarak üretilip test botuyla kalibre ediliyor.

- Tasarım ve algoritma planı: [docs/GAME_DESIGN.md](docs/GAME_DESIGN.md)
- Geliştirme günlüğü: [docs/DEVLOG.md](docs/DEVLOG.md)
- Herkese açık test (tanıdıklara oynatma): [docs/PUBLIC_TEST.md](docs/PUBLIC_TEST.md)
- PDF baskıları: [docs/pdf/](docs/pdf/) — `py tools/docs-pdf/build.py` ile Markdown dosyalarından üretilir
- Araştırmalar:
  - [research.md](research.md)
  - [docs/research/spawn-control-and-solvability.md](docs/research/spawn-control-and-solvability.md)

## Web test laboratuvarı

```bash
dotnet run --project src/Puzzle.Web
```

Tarayıcıda <http://localhost:5180> adresini aç. Laboratuvarda şunlar var:
- Örnek bölümleri sürükleyerek oynama.
- "Yeni deneme" ile eşit zorluk havuzundan farklı desenler; "Aynı seed" ile birebir tekrar; başlangıç tahtasını sabit veya değişken deneme.
- Seed ve DDA yardımı seçme.
- İpucu, tek bot hamlesi ve otomatik oynayan bot.
- Bölümü 100–1000 kez simüle edip kazanma oranı eğrisini görme.
- Gelecek taşları bilen çözücüyle "mükemmel oyun"u bulup canlı oyunda oynatma.

Aynı Wi-Fi'deki telefon ve bilgisayarlardan erişmek için uygulamayı ayrı bir klasöre yayınla, sonra bütün ağ arayüzlerinde başlat. Ayrı klasör, geliştirme derlemeleriyle dosya kilidi çakışmasını önler.

```bash
dotnet publish src/Puzzle.Web -c Release -o out/web
```

```bash
dotnet out/web/Puzzle.Web.dll --urls http://0.0.0.0:5180 --contentRoot .
```

Sonra diğer cihazda `http://<bilgisayarın Wi-Fi IP'si>:5180` adresini aç. Windows Güvenlik Duvarı gelen bağlantıyı engelliyorsa yönetici PowerShell'de şu üç adımı uygula: Wi-Fi'yi "Özel" ağ yap, .NET Host engelleme kurallarını kapat, 5180 portuna izin ver.

```powershell
Set-NetConnectionProfile -InterfaceAlias "Wi-Fi" -NetworkCategory Private
```

```powershell
Get-NetFirewallRule -DisplayName ".NET Host" | Where-Object Action -eq Block | Disable-NetFirewallRule
```

```powershell
New-NetFirewallRule -DisplayName "Puzzle Test Lab (5180)" -Direction Inbound -Protocol TCP -LocalPort 5180 -Action Allow -Profile Private
```

## Yapı

| Proje | Hedef | İçerik |
|---|---|---|
| `src/Puzzle.Core` | netstandard2.1, C# 9 | Simülasyon motoru ve spawn yönetmeni; Unity'ye olduğu gibi girer |
| `src/Puzzle.Bot` | netstandard2.1, C# 9 | Botlar (greedy profilleri, usta arama botu), beam search çözücü, paralel simülasyon ve kalibrasyon raporu |
| `src/Puzzle.Tooling` | net10.0 | Bölüm JSON'u ve bölüm kütüphanesi |
| `src/Puzzle.Generator` | net10.0 | Bölüm üreticisi: profil, planlayıcı, şekil ve engel portföyü, bot kalibrasyonu, rapor |
| `src/Puzzle.Cli` | net10.0 | `generate`, `sim`, `botstats`, `ladder`, `humans`, `demo`, `bench`, `solve`, `seeds`, `validate` komutları |
| `src/Puzzle.Web` | net10.0 | Tarayıcıda test laboratuvarı: oyna, botlar, çözücü, simülasyon, deneme kayıtları ve botlarla karşılaştırma |
| `tests/Puzzle.Core.Tests` | net10.0, xUnit | Motor, bot, çözücü ve dosya formatı testleri |
| `levels/samples` | JSON | El yapımı örnek bölümler |
| `levels/generated` | JSON + CSV | Üretilen bölümler ve `report.csv` |
| `profiles` | JSON | Üretim profilleri (zorluk eğrisi, hamle aralıkları, renkler, mekanik takvimi) |

## Komutlar

```bash
dotnet test
```

Bölüm üretimi (profil `profiles/default.json`; mevcut dosyalar korunur, `--overwrite` ile yeniden üretilir, `--quick` hızlı önizleme):

```bash
dotnet run --project src/Puzzle.Cli -c Release -- generate --from 1 --to 1000 --out levels/generated
```

```bash
dotnet run --project src/Puzzle.Cli -c Release -- sim levels/samples/s03_jelly_shape.json --bot all --runs 300
```

Bir botun 1000 bölümün her birini kaç hamlede bitirdiği (CSV; `--bot usta` ile arama botu, yaklaşık 15 dakika):

```bash
dotnet run --project src/Puzzle.Cli -c Release -- botstats levels/generated --bot expert --runs 30
```

Mevcut bölümleri beceri merdivenine karşı ölçer; hiçbir bölüm yeniden üretilmez. Merdiven `skill-30` … `skill-100` botlarıdır (5'er puan arayla; `skill-50` = novice, `skill-70` = casual, `skill-85` = average, `skill-100` = expert) ve bir bölümde bütün botlar aynı oyunları oynar. Çıktı bölüm ve bot başına bir satırdır (`reports/ladder.csv`: limitte kazanma oranı, gereken hamle, her kazanma oranı için gereken limit). `--from/--to` ile parça parça çalıştırılabilir; her çalıştırma kendi bölümlerinin satırlarını yeniler, diğerlerini korur:

```bash
dotnet run --project src/Puzzle.Cli -c Release -- ladder levels/generated --from 1 --to 100
```

Web test uygulamasında oynanan denemeleri (`recordings/attempts.jsonl`) botlarla karşılaştırır:

```bash
dotnet run --project src/Puzzle.Cli -c Release -- humans
```

```bash
dotnet run --project src/Puzzle.Cli -c Release -- solve levels/samples/s03_jelly_shape.json --seeds 6 --beam 64 --prove 4
```

```bash
dotnet run --project src/Puzzle.Cli -c Release -- seeds levels/samples/s03_jelly_shape.json --write
```

```bash
dotnet run --project src/Puzzle.Cli -c Release -- demo levels/samples/s04_items_chains.json --seed 3
```

```bash
dotnet run --project src/Puzzle.Cli -c Release -- validate levels
```
