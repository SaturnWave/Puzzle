# Puzzle (çalışma adı)

iOS ve Android için swap match-3 oyunu (Unity + C#). Genç bir ressam hasar görmüş bir müzeyi onarıyor: bölüm kazandıkça telifsiz gerçek tabloların yapboz parçalarını topluyor. 20 oda, 100 tablo, 1000 bölüm. Tahtalar büyük ve boyutu bölümden bölüme değişiyor (9x10 … 11x13); bölümler algoritmayla üretilip botlarla ayarlanıyor.

**Durum (2026-10-15):** motor hazır, müze tamam, ekonomi sayıları kararlaştırıldı; Unity istemcisinde 15 aşamalı arayüz planının 1–9. aşamaları bitti, sıradaki bölüm içi booster'lar. Herkese açık web testi arayüz işleri bitene kadar kapalı.

## Belgeler

| Belge | Ne için |
|---|---|
| [docs/OVERVIEW.md](docs/OVERVIEW.md) | **Buradan başla.** Oyundan yazılıma genel belge: oynanış, içerik, ekonomi, mimari, test, durum, yol haritası |
| [docs/GAME_DESIGN.md](docs/GAME_DESIGN.md) | Tasarım belgesi: her kararın ayrıntısı ve gerekçesi, ölçümler, komutlar |
| [docs/DEVLOG.md](docs/DEVLOG.md) | Proje günlüğü, en yeni oturum en üstte |
| [docs/PUBLIC_TEST.md](docs/PUBLIC_TEST.md) | Web test sunucusu: çalıştırma, internete ve yerel ağa açma, hesaplar |
| [docs/PHONE_TESTS.md](docs/PHONE_TESTS.md) | Bilinen açıklar ve telefon testleri |
| [research.md](research.md), [docs/research/](docs/research/) | Araştırmalar |
| [docs/pdf/](docs/pdf/) | Belgelerin PDF baskıları (`py tools/docs-pdf/build.py` ile Markdown'dan üretilir) |

## Depo

| Yer | İçerik |
|---|---|
| `src/` | On C# projesi: motor (`Puzzle.Core`), botlar, müze ve meta kuralları (Unity'ye derlenmiş girer); bölüm aracı, üretici, banka, ekonomi simülatörü, komut satırı, web test uygulaması |
| `tests/` | Bütün C# testleri |
| `unity/` | Unity 6.6 istemcisi (Android) |
| `levels/` | Yayındaki 1000 bölüm (`generated`), örnekler, arşiv |
| `profiles/`, `policies/`, `economy/`, `museum/` | Bölüm planı, zorluk politikası, ekonomi ayarları, müze kataloğu |
| `art/`, `tools/`, `reports/` | Lisanslar ve çizim kaynakları, betikler, ölçüm raporları |

Ayrıntılı harita: [docs/OVERVIEW.md](docs/OVERVIEW.md) bölüm 5 ve 8.

## Hızlı başlangıç

Gerekenler: .NET 10 SDK; Node.js (Unity'ye aktarma betikleri); Unity 6.6 (istemci); PDF'ler için Python, pandoc ve Chrome ya da Edge.

```bash
dotnet test
```

Tarayıcıda test laboratuvarı (oyna, botlar, çözücü, simülasyon, kayıtlar), <http://localhost:5180>:

```bash
dotnet run --project src/Puzzle.Web
```

Komut satırı aracı (`sim`, `ladder`, `humans`, `validate`, `bank`, `assemble`, `promote`, `patch`, `economy` ve diğerleri; kullanımları GAME_DESIGN §9, §20, §24, §25):

```bash
dotnet run --project src/Puzzle.Cli -c Release -- validate levels
```

Motoru, bölümleri, kataloğu ve ekonomi ayarını Unity projesine almak (Unity komutları GAME_DESIGN §23):

```bash
node tools/unity/sync-engine.js
```
