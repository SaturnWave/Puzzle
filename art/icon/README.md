# Uygulama simgesi

Proje sahibinin 2026-10-23'te seçtiği simge: **F · krem zeminde ressam paleti** (önce G, gece mavisi, seçilmişti; "daha sade" diye F'ye geçildi). Paletin boya lekeleri oyunun kendi taşları (uğur böceği, papatya, damla), altta bir fırça. İnsan ya da maskot yok.

| Dosya | Ne için |
|---|---|
| `app-icon-1024.png` | Bütün simge, saydamlıksız: iOS, Android'in eski ve yuvarlak simgeleri, mağazalar |
| `adaptive-background-1024.png` | Android katmanlı simgesinin zemini |
| `adaptive-foreground-1024.png` | Android katmanlı simgesinin ön planı: palet ve fırça, saydam zeminde, ortadaki %66'lık güvenli alanda |
| `entry-logo-1024.png` | Oyunun giriş sayfasının logosu: aynı palet ve fırça, saydam zeminde, bütün simgedeki boyutunda (simge değil; oyun kendisi çizer, Unity'de sıkıştırılmış) |

Kaynak: `tools/final_icon.py` (tasarım `tools/palette.py`, yardımcılar `tools/icons.py`). SVG olarak çizilir ve Chrome'la
1024×1024 piksele çevrilir: `py art/icon/tools/final_icon.py art/icon`. Taş resimleri oyunun kendi çizimleri
(`unity/Assets/Art/Pieces`); başka bir kaynaktan resim yok. Öteki öneriler (A–F) `out/design/icons` klasöründe.
