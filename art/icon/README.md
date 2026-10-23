# Uygulama simgesi

Proje sahibinin 2026-10-23'te seçtiği simge: **G · gece mavisi zeminde ressam paleti**. Paletin boya lekeleri oyunun kendi
taşları (uğur böceği, papatya, damla, yeşil kare), altta bir fırça, paletin kenarı yaldızlı. İnsan ya da maskot yok.

| Dosya | Ne için |
|---|---|
| `app-icon-1024.png` | Bütün simge, saydamlıksız: iOS, Android'in eski ve yuvarlak simgeleri, mağazalar |
| `adaptive-background-1024.png` | Android katmanlı simgesinin zemini |
| `adaptive-foreground-1024.png` | Android katmanlı simgesinin ön planı: palet ve fırça, saydam zeminde, ortadaki %66'lık güvenli alanda |

Kaynak: `tools/final_g.py` (tasarım `tools/palette.py`, yardımcılar `tools/icons.py`). SVG olarak çizilir ve Chrome'la
1024×1024 piksele çevrilir: `py art/icon/tools/final_g.py art/icon`. Taş resimleri oyunun kendi çizimleri
(`unity/Assets/Art/Pieces`); başka bir kaynaktan resim yok. Öteki öneriler (A–F) `out/design/icons` klasöründe.
