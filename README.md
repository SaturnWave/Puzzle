# Puzzle (çalışma adı)

iOS + Android için swap match-3 oyunu (Unity + C#). Büyük ve boyutu bölümden bölüme değişen tahtalar
(10x10, 10x12, 11x13 …), algoritmik üretilen ve test botuyla kalibre edilen bölümler.

- Tasarım ve algoritma planı: [docs/GAME_DESIGN.md](docs/GAME_DESIGN.md)
- Araştırma özeti: [research.md](research.md)

## Yapı

| Proje | Hedef | İçerik |
|---|---|---|
| `src/Puzzle.Core` | netstandard2.1, C# 9 | Simülasyon motoru — Unity'ye olduğu gibi girer |
| `src/Puzzle.Bot` | netstandard2.1, C# 9 | Bot politikaları, simülasyon ve kalibrasyon raporu |
| `src/Puzzle.Cli` | net10.0 | `sim`, `demo`, `bench`, `validate` komutları |
| `tests/Puzzle.Core.Tests` | net10.0, xUnit | Motor, bot ve dosya formatı testleri |
| `levels/samples` | JSON | El yapımı örnek bölümler |

## Komutlar

```bash
dotnet test
```

```bash
dotnet run --project src/Puzzle.Cli -c Release -- sim levels/samples/s03_jelly_shape.json --bot all --runs 300
```

```bash
dotnet run --project src/Puzzle.Cli -c Release -- demo levels/samples/s04_items_chains.json --seed 3
```

```bash
dotnet run --project src/Puzzle.Cli -c Release -- validate levels
```
