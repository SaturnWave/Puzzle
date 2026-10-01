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
| 6+ taşlı kesişim (ör. 4'lü × 3'lü, L + kare) | **Büyük Bomba** | 5x5 alan (yarıçap 2) |
| L / T / + — tam 5 taş (yatay 3 + dikey 3 kesişir) | **Bomba** | 3x3 alan (yarıçap 1) |
| 4'lü düz | **Roket** (eşleşmeye dik yönde) | Tüm satırı veya sütunu vurur |
| 2x2 kare | **Pervane** | Etrafındaki + şeklini vurur, sonra bir hedef hücreye uçar |
| 3'lü | — | — |

- **İki bomba (2026-10-01 kararı):** "Daha büyük şekil, daha büyük bomba." Küçük L/T 3x3, daha büyük kesişimler 5x5 patlar. Yarıçaplar `RuleSet.BombRadius` ve `RuleSet.BigBombRadius` içinde.
- **Konum:** oyuncunun hareket ettirdiği hücre gruptaysa özel taş orada çıkar; kaskadda desenin sabit hücresinde (bomba → kesişim, roket → 2. hücre, renk bombası → orta, pervane → karenin sol üstü).
- **Özel taşlar renksizdir** (Royal Match tarzı): eşleşmeye katılmaz. Patlatma yolları: bir komşuyla yer değiştirmek, **dokunmak** (1 hamle) veya başka bir efektin vurması (zincirleme).
- **Pervane hedefi:** aktif hedefe en çok katkı veren hücre (kutu > zincir > jöle > hedef renk > herhangi taş); eşitlikte deterministik rastgele.

**Kombinasyonlar** (iki özel taş yer değiştirirse):

| Kombinasyon | Etki |
|---|---|
| Roket + Roket | Satır + sütun (artı) |
| Roket + Bomba / Büyük Bomba | 3 satır + 3 sütun / 5 satır + 5 sütun |
| Bomba + Bomba | Yarıçaplar toplanır: 3x3+3x3 → 5x5, 3x3+5x5 → 7x7, 5x5+5x5 → 9x9 |
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
- **Renk paleti:** oyun 8 renk tanır (kırmızı, yeşil, mavi, sarı, mor, turuncu, camgöbeği, pembe). Her bölüm
  3–8 renklik kendi paletini seçer (varsayılan: ilk N renk). Böylece ilerledikçe yeni renkler tanıtılabilir ve
  bölüm grupları farklı renk setleriyle oynanabilir. Arayüzde her rengin ayrıca kendi şekli var (daire, kare,
  elmas, altıgen, üçgen, sekizgen, beşgen, yıldız); renk körü oyuncular için kimlik yalnızca renkte değil.
- **Spawn yönetmeni ("karma algoritma"):** her yeni taşı aynı anda çalışan birkaç algoritma birlikte belirler:
  1. **Denge (dueness):** yukarıdaki sönümlü ağırlıklar taban ağırlıkları önerir.
  2. **Hedef eğilimi** (`goalBias`, −90..+200 %): açık "renk topla" hedefinin renklerini daha sık veya seyrek getirir.
  3. **Kaskad kontrolü** (`cascadeBias`, −100..+200 %): düştüğü yerde hemen eşleşecek rengin şansını değiştirir.
     −100 hiç bedava eşleşme vermez (her renk eşleşiyorsa denge ağırlıklarına döner).
  4. **DDA yardımı** (`GameOptions.AssistPercent`, 0..+100): oyun anında, oyuncuya göre; kaskad ve hedef eğilimine eklenir.
     **Yalnızca kolaylaştırır.** Bir bölümü zorlaştırmak tasarım zamanı kararıdır (bölüm dosyasındaki bias'lar).
     Gerekçe: [araştırma §5](research/spawn-control-and-solvability.md).
  Stratejiler çarpımsal birleşir, sonra tek bir ağırlıklı çekiliş yapılır. Yeni bir strateji eklemek tek bir sınıf işi.
- **Bölüm başına spawn tablosu:** palet, yumuşatma, hedef/kaskad eğilimi, malzeme spawn'ı (toplam adet, tahtada en fazla, % şans).
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

**Üç ajan ve hata payı** (araştırma §3 ile uyumlu):

| Ajan | Ne bilir | Durum | Ne ölçer |
|---|---|---|---|
| (a) **Çözücü** — beam search, mükemmel bilgi | Gelecek taşları bilir (oyunun birebir replikası) | ✅ `BeamSearchSolver` | "Her seferinde doğru hamle" ile bulunan en iyi hamle sayısı (alt sınır tahmini) |
| (b) Geleceği bilmeyen güçlü ajan | Yalnızca tahtayı; birçok olası geleceği örnekler | Faz 2 (expectimax / MCTS) | Gerçekçi beceri tavanı |
| (c) İnsan benzeri bot | Yalnızca tahtayı; dikkat + sıcaklık | ✅ `GreedyPolicy` profilleri | Hamle limiti kalibrasyonu |

- Mükemmel oyunu bulmak taşlar bilinse bile NP-zor. Çözücünün sonucu "optimal" değil, "bulunan en iyi"dir. Kısa çözümlerde `ExhaustiveSearch` bütün dizileri deneyerek optimumu kanıtlayabilir (§13).
- Hamle limiti yalnızca çözücüye göre ayarlanmaz.
- Faz 2 metrikleri:
  - `marj = limit − P80(b)`
  - `beceri aralığı = geçme(b) − geçme(c)`
  - `şans payı` = seed'ler arası geçme varyansı

## 8. Hamle sayısının hesaplanması

**Temel fikir:** bot kalan hamleye bakmadan oynadığı için, **sınırsız hamleyle** N oyun oynatıp her oyunun "kaçıncı hamlede bittiğini" kaydetmek yeterlidir. Herhangi bir hamle limiti `L` için kazanma oranı = `bitiş hamlesi ≤ L` olan oyunların oranı (birikimli dağılım). Tek bir simülasyon grubuyla bütün `L` değerleri için kazanma eğrisi elde edilir.

1. Average bot ile N = 150 oyun (üst sınır 150 hamle) → bitiş hamlesi dağılımı.
2. `L` = hedef kazanma oranına ulaşan **en küçük** hamle sayısı.
3. **Doğrulama:** yeni seed'lerle 200 oyun; kazanma oranı hedefin ±%7'si içinde değilse `L` ayarlanır.
4. Ek metrikler: **near-miss** oranı (kaybedip hedefin ≥%80'ine ulaşan oyunlar — "bir hamle daha" hissi), ortalama karıştırma sayısı, Expert/Novice kazanma oranı farkı.

## 9. 1000 bölüm üretim hattı (Faz 2)

Her bölüm `i` için deterministik seed (`ana seed + i + deneme no`):

1. **Plan:** zorluk kademesi, tahta boyutu, açık mekanikler, renk sayısı, simetri türü.
2. **Şekil — jeneratör portföyü:** tek jeneratör yerine paralel çalışan birkaç algoritma aday üretir: simetrik maske şablonları (köşe kesme, orta delik, elmas, kum saati, sütun boşlukları), önceki bölümlerin mutasyonu, kısıt yayılımlı (WFC benzeri) yerleşim. Bot her adayı ölçer; hedef zorluğa ve çeşitliliğe en uygun olanı seçilir. Kısıtlar: oynanabilir hücre ≥ %70, bağlı, her hücre beslenebilir.
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
| 3 / 5 / 8 / 11 / 15 | Roket / Bomba / Pervane / Renk Bombası / Büyük Bomba tanıtımı |
| 13 | Kutu (1 can) |
| 21 | Jöle (1 kat) |
| 31 | Kutu (2 can) |
| 41 | Zincir |
| 56 | Malzeme indirme |
| 71 | Jöle (2 kat) |
| 91 | Kutu (3 can) |
| 120+ | 2 engel türü birlikte; 200+ 3 tür |
| 300+ | v2 engelleri ve v2 özel taşları (§16) |

Renk sayısı (Faz 1 ölçümüne göre güncellendi, bkz. §13). Bölümler ilerledikçe yeni renkler gelir:

| Bölüm | Renk | Not |
|---|---|---|
| 1–5 | 4 | Öğretici |
| 6–30 | 5 | |
| 31–300 | 6 (standart) | 5 renk yalnızca Kolay / nefes bölümlerinde |
| 301+ | 6–7 | 7. renk Zor / Süper Zor bölümlerde |
| 600+ | 6–8 | 8 renk yalnızca seyrek "renk fırtınası" kancası olarak |

Büyük tahtada 4–5 renk, hedef sayıları ne olursa olsun bölümü "kendi kendine oynayan" bir tahtaya çevirir.

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
  "spawn":  { "colors": 6, "palette": ["Red", "Blue", "Cyan", "Pink", "Yellow", "Green"],
              "smoothingPermille": 60, "goalBias": 0, "cascadeBias": -30,
              "itemsTotal": 0, "maxItemsOnBoard": 0, "itemChancePercent": 0 }
}
```

`palette` verilirse `colors` yok sayılır; verilmezse palet ilk `colors` renktir.

| Katman | Karakterler |
|---|---|
| `cells` | `#` oynanabilir, `.` boşluk |
| `tiles` | `.` rastgele taş, `r g b y p o c k` sabit renk (c = camgöbeği, k = pembe), `H`/`V` roket, `X` bomba, `M` büyük bomba, `S` pervane, `C` renk bombası, `1`–`3` kutu, `I` malzeme |
| `jelly` / `chains` | `.` yok, `1`–`2` kat/can |

## 11. Proje yapısı

```
src/Puzzle.Core     netstandard2.1, C# 9 — simülasyon + spawn yönetmeni (Unity'ye girer)
src/Puzzle.Bot      netstandard2.1, C# 9 — botlar, çözücü, paralel simülasyon (oyunda ipucu için de)
src/Puzzle.Tooling  net10.0 — bölüm JSON'u ve bölüm kütüphanesi (CLI, web ve üretici ortak)
src/Puzzle.Cli      net10.0 — sim / demo / bench / solve / validate (Faz 2'de generate)
src/Puzzle.Web      net10.0 — tarayıcıda test laboratuvarı (oyna, bot, ipucu, çözücü, simülasyon)
tests/Puzzle.Core.Tests  net10.0, xUnit
levels/samples      el yapımı örnek bölümler
```

## 12. Yol haritası

| Faz | İçerik |
|---|---|
| 1 ✅ | Çekirdek motor, bot, CLI, testler, örnek bölümler |
| 1.5 ✅ | İki bomba, 8 renk + palet, spawn yönetmeni, mükemmel bilgi çözücüsü, web test laboratuvarı |
| **2 (sıradaki)** | Bölüm üretici, 1000 bölüm, zorluk raporu |
| 3 | Unity entegrasyonu: görsel katman, animasyon event'leri, ipucu, DDA |
| 4 | UI, oyunun adı, meta oyun (harita, ödüller), ekonomi |

## 13. Faz 1 sonuçları — ölçümler (2026-10-01)

Bot: `greedy-average`, 300 oyun / ölçüm, hamle üst sınırı 150. Komut: `puzzle sim <bölüm> --bot all`.
Bu bölümdeki ilk iki tablo Faz 1 kuralıyla (tek bomba, 5x5) ölçüldü; Faz 1.5 ölçümleri aşağıda.

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

**Faz 1.5 ölçümleri** (iki bomba kuralıyla, s03 jöle bölümü, average bot, 300 oyun):

| Renk | Kaskad eğilimi | Medyan hamle | p90 | Kaskad / hamle |
|---|---|---|---|---|
| 5 | 0 / −50 / −100 | 12 / 12 / 13 | 18 / 20 / 20 | 2,00 / 1,80 / 1,51 |
| 6 | 0 / −50 / −100 | 22 / 22 / 22 | 35 / 35 / 35 | 1,03 / 0,91 / 0,81 |

- Kaskad kontrolü kaskadları ~%25 azaltıyor ama hamle ihtiyacını yalnızca ~%5–8 artırıyor. Neden: kaskadların çoğu yeni düşen taştan değil, mevcut taşların kaymasından doğuyor. Bu yüzden orta güçte bir ince ayar kolu; ana kol renk sayısı.

**Mükemmel bilgi çözücüsü** (`puzzle solve`, beam 8, aynı başlangıç tahtası):

| Bölüm | Çözücü (geleceği bilir) | Average bot | Random bot | Oran |
|---|---|---|---|---|
| s03 jöle, 6 renk | 7,7 | 18,2 | ~90 | 2,4x |
| s02 kutular, 5 renk | 3,7 | 6,0 | ~17 | 1,6x |

Senin gözlemin doğrulanıyor: her seferinde doğru hamleyle (ve gelecek taşları bilerek) bölüm çok daha kısa bitiyor.
Yanlış hamleler hamle ihtiyacını 1,6–2,4 katına çıkarıyor. Çözücü 6 seed'i 0,5 saniyede çözüyor.

**"Kural belli, en iyi yolu bulmak zor olmaz" mı?** (s03, 6 renk, 6 seed; `puzzle solve --beam B --prove 4`)

| Beam genişliği | 1 | 2 | 4 | 8 | 16 | 32 | 64 |
|---|---|---|---|---|---|---|---|
| Bulunan en iyi (ort. hamle) | 15,8 | 11,0 | 9,3 | 7,7 | 6,8 | 5,8 | 5,5 |

Tam arama sonuçları (daha kısa bütün hamle dizileri denendi):
- **Seed 2 ve 3:** beam 64'ün yolu gerçekten en iyisi (kanıtlandı).
- **Seed 0 ve 5:** gelecek taşları bilen beam 64 beş hamle buldu; tam arama **dört hamlelik daha kısa bir kazanç** buldu.
- **Seed 1 ve 4:** kanıt 6–7 hamle derinliği istiyor; hesaplanamadı.

Sonuç: kuralı izlemek (simülasyon) kolay, iyi bir yol bulmak kolay, **en iyi yolu kanıtlamak** zor. Pozisyon başına ortalama ~37–40 geçerli hamle var:
- 4 hamle derinlik ≈ 1 milyon pozisyon, 1,5 sn.
- 6 hamle derinlik ≈ 2,5 milyar pozisyon, ~yarım saat.
- 20 hamlelik bir oyun astronomik.

Oyuncu açısından ise senin dediğin gibi kuralı (spawn yönetmeni + seed) tersine mühendislikle çözmek zor.

**Seed'lerin zorluk farkı** (`puzzle seeds`, 64 aday seed × 64 bot oyunu):

| Bölüm | Seed başına medyan hamle (en kolay … en zor) | Limitte kazanma, rastgele seed'ler | Havuz (16 seed, ±5 puan) |
|---|---|---|---|
| s03 jöle, 6 renk | 8 … 29 | %55 … %100 | %83 … %91 |
| s04 malzeme + zincir, 6 renk | 13 … 28 | %72 … %100 | %89 … %92 |

Sadece "her denemede yeni seed" vermek eşit zorluk sağlamıyor; kalibre edilmiş havuz gerekiyor (§17).

## 14. Açık sorular

1. ~~Bomba 5x5 mi 3x3 mü?~~ → İkisi de: Bomba 3x3, Büyük Bomba 5x5 (§3).
2. ~~Dokunarak patlatma?~~ → Evet, 1 hamle.
3. Zor bölümlerde boyut değişimi bölüm başına mı, yoksa ileride bölüm içinde (çok aşamalı tahta) de olsun mu?
4. Can sistemi / booster ekonomisi Faz 4'te mi konuşulacak?
5. ~~Retry politikası?~~ → Farklı desen, aynı kurallar, aynı zorluk (§17).
6. **Başlangıç tahtası:** sabit mi değişken mi? İkisi de destekleniyor; zorluk her iki modda havuzla eşitleniyor. Web laboratuvarında "Başlangıç tahtası" seçicisiyle denenip karar verilecek.

## 15. Paralel çalışan algoritmalar — mimari

Oyun tek bir algoritma değil, aynı anda çalışan birkaç sistemden oluşuyor:

| Katman | Algoritmalar | Nerede çalışır | Durum |
|---|---|---|---|
| **Spawn yönetmeni** | Denge (dueness) + hedef eğilimi + kaskad kontrolü + DDA yardımı, her taş için birlikte | Oyun içi (Core) | ✅ |
| **Oyuncu botları** | Random, Greedy (4 profil), çözücü (beam search, mükemmel bilgi) | Analiz, ipucu (Bot) | ✅ |
| **Paralel simülasyon** | Yüzlerce oyun 12 çekirdekte aynı anda; tek seri tüm kazanma eğrisini verir | Kalibrasyon (Bot) | ✅ |
| **Jeneratör portföyü** | Şablon/simetri, mutasyon, WFC benzeri yerleşim, paralel aday + bot seçimi | Üretim (Faz 2) | Plan |
| **Oyun içi eşzamanlı sistemler** | İpucu botu, beceri takibi (EWMA), DDA denetleyicisi (yalnızca yardım), near-miss tespiti (yalnızca ölçüm), telemetri | Unity (Faz 3) | Plan |

## 16. İlerleme — yeni renkler, engeller, özel taşlar, temalar

Bölümler ilerledikçe dört eksende yenilik gelir (her yeni öğe bir "kanca" bölümüyle tanıtılır):

- **Renkler:** §9 tablosu (4 → 8 renk) + bölüm grubuna özel palet.
- **Yeni engeller (v2, 300+):**

  | Engel | Davranış |
  |---|---|
  | Buz | Taşın üstünde kaplama; vurulunca kırılır, taş düşebilir |
  | Taş duvar | Yok edilemez; şekil gibi davranır ama kaskadı keser |
  | Yayılan balçık | Her tur temizlenmezse komşuya yayılır |
  | Portal | Sütunlar arası düşüş |
  | Konveyör | Her hamlede satırı kaydırır |
  | Bal | Komşu eşleşmeyle temizlenir, üstündeki taşı yapıştırır |

- **Yeni özel taşlar (v2 adayları):**

  | Özel taş | Etki |
  |---|---|
  | Şimşek | İki çapraz |
  | Boya kovası | 3x3 alandaki taşları tek renge boyar |
  | Mıknatıs | Bir hedef türünü kendine çeker |
  | Kum saati | +2 hamle, nadir ödül |

  Her yeni özel taş yeni bir `TileKind`. Kapalı küme testleri hangi switch'lerin güncelleneceğini gösterir (Büyük Bomba eklenirken bu sistem eksik switch'i anında yakaladı).
- **Temalar:** her 50–100 bölüm bir tema (görsel set: taş şekilleri, arka plan, müzik). Mekanik değişmez; yalnızca Unity görsel katmanı.

## 17. Retry politikası ve etik korkuluklar

**Karar (2026-10-01): tekrar denemede taşlar farklı desende gelir, ama algoritmik matematik ve zorluk denemeden öncekiyle aynıdır.**

- **Aynı kurallar:** palet, spawn yönetmeni ayarları ve hamle sayısı bölüm dosyasında sabit; denemeden denemeye değişmez.
- **Gizli zorluk ayarı yok:** denemeler arasında DDA ile kural değişikliği yapılmaz (`AssistPercent` = 0). İleride DDA istenirse ayrı bir ürün kararı olur.
- **Farklı desen:** her deneme farklı bir seed alır. Motor deterministik: aynı seed ve aynı hamleler aynı oyunu üretir.
- **Aynı zorluk:** rastgele seed'ler zorluk bakımından çok farklı (§13 tablosu). Bu yüzden her bölümün bir **eşit zorluk havuzu** var:
  - `puzzle seeds` 64 aday seed'i 64'er bot oyunuyla ölçer.
  - Limitteki kazanma oranı bölümün tipik değerine ±5 puan yakın olan 16 seed'i seçer.
  - Deneme k havuzdaki (k mod 16). seed'i kullanır.
- **Başlangıç tahtası:** bölüm dosyasında `attempts.board` alanıyla seçilir:
  - `varied`: her denemede farklı başlangıç tahtası.
  - `fixed`: tipik zorlukta seçilmiş sabit bir tahta; yalnızca gelen taşlar değişir.

  Havuz, bölümün kendi moduna göre kalibre edilir.
- **Motor tarafı:** `AttemptSeeds` (tahta seed'i + oyun seed'i ayrı), `AttemptPolicy`, `SeedPoolCalibrator`.
- **Web laboratuvarı:** "Yeni deneme" havuzdaki sıradaki seed'i, "Aynı seed" birebir tekrarı verir. İkincisi yalnızca test için.
- Korkuluklar ([araştırma §5](research/spawn-control-and-solvability.md)):
  - DDA yalnızca yardım yönünde (motor zorlar: 0..100).
  - Harcama veya ödeme eğilimi DDA'ya girdi olmaz.
  - Near-miss hedeflenmez, yalnızca ölçülür.
  - Ekstra hamle teklifinden önceki hamlelerde yönetmen sabit kalır (Faz 3).
  - Seed ve bias kayıtları saklanır.
