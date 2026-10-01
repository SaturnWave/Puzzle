# Oyun Tasarımı ve Algoritma Planı — v0.1

> Durum: Faz 1 (çekirdek algoritma + test botu) tamamlandı; sıradaki Faz 2 (1000 bölüm). UI ve oyunun adı en son.
> Kaynak araştırma: [research.md](../research.md)

## 0. Temel kararlar

| Konu | Karar | Neden |
|---|---|---|
| Tür | Swap match-3 (Royal Match / Candy Crush tarzı) | Araştırmanın tamamı bu mekanik üzerine; pazarı kanıtlanmış |
| Platform | iOS + Android, **Unity + C#** | Mobil match-3'ün standardı |
| Çekirdek | Motordan bağımsız C# simülasyon kütüphanesi (`netstandard2.1`, C# 9) | Unity'ye doğrudan girer; bot, üretici ve oyun **aynı kodu** çalıştırır |
| Determinizm | Kendi PRNG'miz (PCG32) + yalnızca tam sayı aritmetiği | Aynı seed her cihazda aynı oyunu üretir; botun ölçtüğü şey oyuncunun oynadığı şeydir |
| Tahta | Büyük: 10x10, 10x12 ve türevleri; bölüm başına değişken W×H + şekil maskesi | Zor bölümlerde satır/sütun sayısı değişecek |
| İçerik | İlk 1000 bölüm algoritmik üretilecek, hamle sayıları botla kalibre edilecek | Elle 1000 bölüm dengelenemez |

## 1. Beyin fırtınası — büyük tahtanın getirdikleri

- **Büyük tahta = çok kaskad, çok özel taş.** 10x12 tahtada (120 hücre) 5 renkle kendiliğinden eşleşme çok olur. Zorluğu üç kolla dengeleriz: **renk sayısı** (4→6), **engel yoğunluğu**, **hamle bütçesi**.
- **İnsan gözü büyük tahtada hamle kaçırır.** Botun "dikkat" parametresi var: her hamleyi belli bir olasılıkla "görür". Böylece bot, büyük tahtadaki insan görsel arama zorluğunu taklit eder.
- **Boyut değişimi bir zorluk sinyalidir.** Hard / Super Hard bölümlerde tahta uzar (10x12, 11x13), daralır (9x13) veya şekillenir. Oyuncu "bu bölüm farklı" hissini görsel olarak alır.
- **İpucu sistemi bedava gelir.** Oyundaki "ipucu göster" özelliği botun kendisidir (aynı kütüphane).
- **Dinamik zorluk (DDA) için hazırlık.** Spawn sistemi ağırlık tabanlı; art arda kaybeden oyuncuya hedef rengin ağırlığı artırılabilir (araştırma: DDA bölümü).
- **Sonraki fikirler (v2+):** bölüm ortasında genişleyen çok aşamalı tahta, portal, konveyör bant, yayılan balçık, buz, taş duvar.

## 2. Tahta modeli

Her hücre dört katmandan oluşur:

| Katman | Değerler | Not |
|---|---|---|
| Hücre | oynanabilir `#` / boşluk `.` | Boşluklar **geçirgen**: taşlar boşluğun içinden aşağı düşer |
| Zemin | jöle 0–2 kat | Üstündeki hücre vurulunca 1 kat gider |
| Taş | taş (6 renk), özel taş, kutu (1–3 can), malzeme | Tek slot |
| Üst | zincir 0–2 can | Taşı kilitler |

- Koordinat: `(x, y)`, `y = 0` en üst satır (taşların geldiği taraf).
- Her sütunun en üst oynanabilir hücresi **spawner**, en alt oynanabilir hücresi **çıkış** (malzeme toplama).
- Limitler: motor 16x16'ya kadar destekler. Ürün limiti **genişlik ≤ 11** (telefonda hücre ≈ 32–36 dp; daha dar dokunma hedefi yanlış dokunuş üretir), **yükseklik ≤ 13**.

## 3. Eşleşme ve özel taşlar

Geçerli eşleşme: yatay/dikey **3+** aynı renk veya **2x2 kare**. Eşleşen hücreler birleşim-bul (union-find) ile gruplanır; birbirine değen ama ortak hücresi olmayan iki dizi ayrı gruptur.

| Desen (öncelik sırasıyla) | Özel taş | Etki |
|---|---|---|
| 5'li düz | **Renk Bombası** | Değiştirildiği taşın rengindeki tüm taşları vurur |
| L / T / + (yatay 3 + dikey 3 kesişir) | **Bomba** | 5x5 alan (yarıçap 2) |
| 4'lü düz | **Roket** (eşleşmeye dik yönde) | Tüm satırı veya sütunu vurur |
| 2x2 kare | **Pervane** | Etrafındaki + şeklini vurur, sonra bir hedef hücreye uçar |
| 3'lü | — | — |

- **Konum:** oyuncunun hareket ettirdiği hücre gruptaysa özel taş orada çıkar; kaskadda desenin sabit hücresinde (bomba → kesişim, roket → 2. hücre, renk bombası → orta, pervane → karenin sol üstü).
- **Özel taşlar renksizdir** (Royal Match tarzı): eşleşmeye katılmaz. Patlatma yolları: bir komşuyla yer değiştirmek, **dokunmak** (1 hamle) veya başka bir efektin vurması (zincirleme).
- **Pervane hedefi:** aktif hedefe en çok katkı veren hücre (kutu > zincir > jöle > hedef renk > herhangi taş); eşitlikte deterministik rastgele.

**Kombinasyonlar** (iki özel taş yer değiştirirse):

| Kombinasyon | Etki |
|---|---|
| Roket + Roket | Satır + sütun (artı) |
| Roket + Bomba | 3 satır + 3 sütun |
| Bomba + Bomba | 7x7 alan (yarıçap 3) |
| Pervane + Pervane | + şekli, sonra 3 ayrı hedefe uçuş |
| Pervane + Roket/Bomba | Pervane hedefe uçar, orada roket/bomba patlar |
| Renk Bombası + Roket/Bomba/Pervane | En çok bulunan rengin tüm taşları o özel taşa dönüşür ve patlar |
| Renk Bombası + Renk Bombası | Tüm tahta |

## 4. Engeller ve hedefler (v1)

| Mekanik | Davranış | Bağlı hedef |
|---|---|---|
| **Kutu** (1–3 can) | Düşmez, yerçekimini keser. Yanındaki eşleşme (adım başına 1 kez) veya efekt 1 can götürür | Kutuları kır |
| **Jöle** (1–2 kat) | Zemin katmanı, hareketi etkilemez | Jöleleri temizle |
| **Zincir** (1–2 can) | Taş kaydırılamaz ve düşmez ama eşleşmeye katılır; vuruş önce zinciri kırar | Zincirleri kır |
| **Malzeme** | Düşer, kaydırılabilir, eşleşmez, yok edilemez; çıkış hücresine ulaşınca toplanır | Malzemeleri indir |
| — | — | **X renginden N taş topla** |

Kural: bir bölümde **en fazla 4 farklı mekanik** (araştırmadaki "element constraint"), en fazla 3 hedef.

**v2 mekanik adayları** (300+ bölümde çeşitlilik için): buz, taş duvar, yayılan balçık, portal, konveyör, bal.

## 5. Tur akışı

```
hamle → doğrula (geçersizse reddedilir, hamle düşmez)
  ├─ özel taş içeriyorsa → efekt kuyruğuna ekle (kombinasyon / tek özel)
  └─ normal swap        → taşları değiştir (oyuncunun hücreleri "tercihli konum")
döngü:
  1. efekt kuyruğunu işle (FIFO; her efekt bir hücreyi en fazla 1 kez vurur;
     vurulan özel taş kuyruğa eklenir → zincirleme)
  2. yerçekimi: dikey düşüş (boşluklardan geçerek) + spawn
               → çapraz kayma (yalnızca kutu/zincir altındaki boş hücreler) → tekrar
  3. çıkış hücresindeki malzemeleri topla → tekrar yerçekimi
  4. eşleşmeleri bul → vur, kutulara yan hasar, özel taş üret
  5. eşleşme yoksa döngüden çık
durum: hedefler tamam → KAZANDI · hamle bitti → KAYBETTİ · hamle yok → ücretsiz karıştır
```

**Vuruş kuralı:** zincir varsa önce zincir hasar alır (taş kalır). Yoksa taş yok olur (özel taşsa patlar), kutu 1 can kaybeder, malzeme etkilenmez. Kutu veya zincir koruması yoksa zemindeki jöle 1 kat kaybeder.

## 6. Yukarıdan gelen parçalar — spawn sistemi

- **Renk seçimi: sönümlü ağırlıklı rastgele** (araştırmadaki "dueness" algoritması). Her rengin bir olasılığı var; çıkan rengin olasılığı düşer, çıkmayanlarınki artar:
  - çıkan renk: `p ← p − (1 − p_taban) · s`
  - diğerleri: `p ← p + p_taban · s`
  - `s` = yumuşatma (‰). `s = 0` saf rastgele; büyüdükçe "renk kuraklığı" kaybolur ama doğal kümelenme de azalır (bölüm zorlaşır). Varsayılan 60‰.
  - Sabit-nokta tam sayı ile hesaplanır (platformlar arası birebir aynı sonuç).
- **Bölüm başına spawn tablosu:** renk sayısı (3–6), yumuşatma, malzeme spawn'ı (toplam adet, tahtada en fazla, % şans).
- **Başlangıç tahtası:** hazır eşleşme yok, en az 3 geçerli hamle; sağlanamazsa yeniden üretilir. Bölüm dosyasındaki sabit taşlar (kutu, özel taş, malzeme) korunur.
- **Karıştırma:** geçerli hamle kalmazsa ücretsiz; eşleşmesiz ve hamleli bir dizilim bulunana kadar.
- **Bağımsız rastgele akışlar:** başlangıç, spawn, efekt (pervane hedefi vb.) ve karıştırma ayrı akışlardan beslenir; birindeki değişiklik diğerlerini kaydırmaz.

## 7. Test botu

- Bot, oyunun **aynı kodunu** kullanır ama geleceği göremez: bir hamleyi denerken yeni düşen taşlar **gizli** taş olarak gelir (eşleşmez). Oyuncunun gördüğünden fazlasını görmez.
- **Politikalar:** `Random` (alt sınır), `Greedy` (her geçerli hamleyi 1 adım simüle et, puanla, seç).
- **Puan fonksiyonu:** hedef ilerlemesi (ana bileşen) + hedef tamamlama bonusu + üretilen özel taşın değeri − harcanan özel taşın değeri (×0,5) + kısmi hasar (kutu canı, jöle katı, zincir) + malzemenin aşağı inmesi + temizlenen taş sayısı. Kazandıran hamle her zaman seçilir.
- **Beceri profilleri:**

| Profil | Dikkat (hamleyi görme %) | Sıcaklık (softmax) | Kullanım |
|---|---|---|---|
| Novice | 50 | 12 | Kolay bölüm kontrolü |
| Casual | 70 | 6 | Gündelik oyuncu |
| **Average** | 85 | 3 | **Kalibrasyonun ana profili** |
| Expert | 100 | 0 (her zaman en iyi) | Üst sınır, "çözülemez bölüm" tespiti |

- Araştırmaya göre MCTS fazla güçlü oynar ve zorluğu olduğundan düşük gösterir; CNN taklit modeli için gerçek oyuncu verisi gerekir. Bu yüzden ilk sürüm **parametrik greedy**; lansmandan sonra oyuncu telemetrisiyle profiller yeniden kalibre edilir.

## 8. Hamle sayısının hesaplanması

**Temel fikir:** bot kalan hamleye bakmadan oynadığı için, **sınırsız hamleyle** N oyun oynatıp her oyunun "kaçıncı hamlede bittiğini" kaydetmek yeterlidir. Herhangi bir hamle limiti `L` için kazanma oranı = `bitiş hamlesi ≤ L` olan oyunların oranı (birikimli dağılım). Tek bir simülasyon grubuyla bütün `L` değerleri için kazanma eğrisi elde edilir.

1. Average bot ile N = 150 oyun (üst sınır 150 hamle) → bitiş hamlesi dağılımı.
2. `L` = hedef kazanma oranına ulaşan **en küçük** hamle sayısı.
3. **Doğrulama:** yeni seed'lerle 200 oyun; kazanma oranı hedefin ±%7'si içinde değilse `L` ayarlanır.
4. Ek metrikler: **near-miss** oranı (kaybedip hedefin ≥%80'ine ulaşan oyunlar — "bir hamle daha" hissi), ortalama karıştırma sayısı, Expert/Novice kazanma oranı farkı.

## 9. 1000 bölüm üretim hattı (Faz 2)

Her bölüm `i` için deterministik seed (`ana seed + i + deneme no`):

1. **Plan:** zorluk kademesi, tahta boyutu, açık mekanikler, renk sayısı, simetri türü.
2. **Şekil:** simetrik maske şablonları (köşe kesme, orta delik, elmas, kum saati, sütun boşlukları). Kısıtlar: oynanabilir hücre ≥ %70, bağlı, her hücre beslenebilir.
3. **Engel yerleşimi:** simetrik desenler (sıra, küme, çerçeve).
4. **Hedefler:** yerleştirilen engellerden türetilir + renk toplama / malzeme sayısı.
5. **Yapısal doğrulama:** birkaç seed ile başlangıç tahtası kurulabiliyor mu.
6. **Kalibrasyon** (§8) → hamle sayısı. Hamle 15–45 dışındaysa hedef sayıları ayarlanıp tekrar.
7. **Red kriterleri:** tamamlanamama > %5, çok karıştırma, hedef kazanma oranından sapma.
8. **Yenilik kontrolü:** son 20 bölümle şekil/mekanik benzerliği yüksekse yeniden üret.
9. JSON + metrik satırı (CSV raporu).

### Zorluk eğrisi — 10'luk testere dişi

| Döngüdeki sıra | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| Kademe | Kolay | Normal | Normal | Normal | **Zor** | Kolay (nefes) | Normal | Normal | **Zor** | **Süper Zor** |

Average bot için hedef kazanma oranı (bölüm ilerledikçe sıkılaşır):

| Kademe | Bölüm 1–50 | 51–300 | 301–1000 |
|---|---|---|---|
| Kolay | %95 | %90 | %85 |
| Normal | %85 | %75 | %65 |
| Zor | %60 | %50 | %40 |
| Süper Zor | %45 | %35 | %25 |

### Tahta boyutu planı

| Kademe | Boyutlar |
|---|---|
| Kolay / Normal | 9x10, 10x10, 10x11 (+ şekil maskeleri) |
| Zor | 10x12, 11x11, 9x12 |
| Süper Zor | 11x12, 11x13, 10x13 |

İlk 30 bölümde boyut sabit kalır (öğrenme); boyut değişimi bölüm 30'dan sonra Zor bölümlerde başlar.

### Mekanik açılış takvimi (taslak)

| Bölüm | Yeni öğe |
|---|---|
| 1 | Temel eşleşme, renk toplama |
| 3 / 5 / 8 / 11 | Roket / Bomba / Pervane / Renk Bombası tanıtımı |
| 13 | Kutu (1 can) |
| 21 | Jöle (1 kat) |
| 31 | Kutu (2 can) |
| 41 | Zincir |
| 56 | Malzeme indirme |
| 71 | Jöle (2 kat) |
| 91 | Kutu (3 can) |
| 120+ | 2 engel türü birlikte; 200+ 3 tür |
| 300+ | v2 mekanikleri |

Renk sayısı (Faz 1 ölçümüne göre güncellendi, bkz. §13): bölüm 1–5 → 4 renk (öğretici), 6–30 → 5 renk,
sonrasında **standart 6 renk**. 5 renk yalnızca Kolay / nefes bölümlerinde. Büyük tahtada 4–5 renk, hedef
sayıları ne olursa olsun bölümü "kendi kendine oynayan" bir tahtaya çevirir.

**Kapalı hücre kısıtı (üretici için):** yan yana 3+ kutudan oluşan bir sıranın altındaki hücrelere ne
yukarıdan ne çaprazdan taş gelebilir; kutu kırılana kadar boş kalırlar (Candy Crush'taki davranışla aynı).
Üretici kutu sıralarını en fazla 2'li gruplar hâlinde ya da aralarında boşluk bırakarak yerleştirir;
kasıtlı "kutu tavanı" ancak bir bölümün kancasıysa kullanılır.

## 10. Bölüm dosyası formatı

Katmanlar karakter ızgarası olarak tutulur (elle de düzenlenebilir):

```json
{
  "formatVersion": 1, "id": 12, "difficulty": "Hard",
  "width": 10, "height": 10, "moves": 24,
  "cells":  ["##########", "...", "##..##..##"],
  "tiles":  ["..........", "...", "..2222...."],
  "jelly":  ["1111111111", "..."],
  "chains": ["..........", "..."],
  "goals":  [{ "type": "BreakBoxes", "count": 8 }, { "type": "CollectColor", "color": "Red", "count": 30 }],
  "spawn":  { "colors": 5, "smoothingPermille": 60, "itemsTotal": 0, "maxItemsOnBoard": 0, "itemChancePercent": 0 }
}
```

| Katman | Karakterler |
|---|---|
| `cells` | `#` oynanabilir, `.` boşluk |
| `tiles` | `.` rastgele taş, `r g b y p o` sabit renk, `H`/`V` roket, `X` bomba, `S` pervane, `C` renk bombası, `1`–`3` kutu, `I` malzeme |
| `jelly` / `chains` | `.` yok, `1`–`2` kat/can |

## 11. Proje yapısı

```
src/Puzzle.Core     netstandard2.1, C# 9 — simülasyon (Unity'ye girer)
src/Puzzle.Bot      netstandard2.1, C# 9 — bot politikaları + simülasyon koşucusu (oyunda ipucu için de)
src/Puzzle.Cli      net10.0 — sim / demo / bench komutları (Faz 2'de generate)
tests/Puzzle.Core.Tests  net10.0, xUnit
levels/samples      el yapımı örnek bölümler
```

## 12. Yol haritası

| Faz | İçerik |
|---|---|
| 1 ✅ | Çekirdek motor, bot, CLI, testler, örnek bölümler |
| **2 (sıradaki)** | Bölüm üretici, 1000 bölüm, zorluk raporu |
| 3 | Unity entegrasyonu: görsel katman, animasyon event'leri, ipucu, DDA |
| 4 | UI, oyunun adı, meta oyun (harita, ödüller), ekonomi |

## 13. Faz 1 sonuçları — ölçümler (2026-10-01)

Bot: `greedy-average`, 300 oyun / ölçüm, hamle üst sınırı 150. Komut: `puzzle sim <bölüm> --bot all`.

**Beceri farkı ölçülebiliyor** (örnek bölümler, medyan bitiriş hamlesi):

| Bölüm | Random | Novice | Average | Expert |
|---|---|---|---|---|
| s01 temel 10x10 (40 kırmızı + 40 mavi, 5 renk) | 23 | 13 | 8 | 7 |
| s03 şekilli jöle 10x12 (44 jöle, 5 renk) | 52 | — | 11 | — |
| s04 malzeme + zincir 11x12 (5 renk) | 57 | — | 11 | — |

**Renk sayısı ana zorluk kolu** (s03 jöle bölümü, average bot):

| Renk | Yumuşatma ‰ | Medyan hamle | p90 | Kaskad / hamle | Özel taş / oyun |
|---|---|---|---|---|---|
| 4 | 0 / 60 / 250 | 5 / 5 / 6 | 8–9 | ~4,0 | ~19 |
| 5 | 0 / 60 / 250 | 10 / 11 / 12 | 15–20 | ~2,0 | ~14 |
| 6 | 0 / 60 / 250 | 18 / 20 / 22 | 30–35 | ~1,0 | ~12 |

Sonuçlar:
- 10x10+ tahtada 5 renk çok cömert (hamle başına 2 kaskad, tahta kendi kendine oynuyor). **Standart 6 renk.**
- Yumuşatma ince ayar kolu: 0 → 250‰ arası hamle ihtiyacını ~%10–20 artırıyor.
- Hamle sayısını 20–35 bandına oturtmak için hedef sayıları tahta büyüklüğüyle birlikte ölçeklenmeli
  (ör. 10x10'da renk toplama hedefi 40 değil 80–120). Üretici bunu kalibrasyon döngüsünde yapacak (§9 adım 6).

**Hız** (tek çekirdek, Release): motor ~10–16 bin hamle/sn; average bot ~1–2 bin hamle/sn. 12 çekirdekte
300 oyunluk kalibrasyon bölüm başına ~0,5–1 sn → 1000 bölüm, yeniden denemelerle birlikte dakikalar mertebesinde.

## 14. Açık sorular

1. Bomba yarıçapı 2 (5x5) mi, 1 (3x3) mü? (Şu an 2; `RuleSet` içinde tek sabit.)
2. Özel taşa dokunarak patlatma serbest kalsın mı? (Şu an evet, 1 hamle.)
3. Zor bölümlerde boyut değişimi bölüm başına mı, yoksa ileride bölüm içinde (çok aşamalı tahta) de olsun mu?
4. Can sistemi / booster ekonomisi Faz 4'te mi konuşulacak?
5. Büyük tahtada standart renk sayısını 6 yapıyoruz (§13). Daha da zorlaştırmak için ileride 7. renk eklensin mi?
