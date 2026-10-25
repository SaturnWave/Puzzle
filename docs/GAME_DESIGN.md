# Oyun Tasarımı ve Algoritma Planı — v0.2

> Durum (2026-10-15): motor "hazır" (2026-10-12, §21); yayındaki 1000 bölüm bölüm bankasından derlendi (§24); müzenin 20 odası ve 100 tablosu oyunda (§18); ekonomi sayıları simülatörden okundu ve yürürlükte (§25). Unity istemcisinde 15 aşamalı arayüz planının 1–9. aşamaları bitti (§23); sıradaki 10. aşama, bölüm içi booster'lar. Oyunun adı en sona kaldı.
> Projenin kısa özeti: [OVERVIEW.md](OVERVIEW.md) (Genel Belge). Kaynak araştırma: [research.md](../research.md)

## 0. Temel kararlar

| Konu | Karar | Neden |
|---|---|---|
| Tür | Swap match-3 (Royal Match / Candy Crush tarzı) | Araştırmanın tamamı bu mekanik üzerine; pazarı kanıtlanmış |
| Platform | iOS + Android, **Unity + C#** | Mobil match-3'ün standardı |
| Çekirdek | Motordan bağımsız C# simülasyon kütüphanesi (`netstandard2.1`, C# 9) | Unity'ye doğrudan girer; bot, üretici ve oyun **aynı kodu** çalıştırır |
| Determinizm | Kendi PRNG'miz (PCG32) + yalnızca tam sayı aritmetiği | Aynı seed her cihazda aynı oyunu üretir; botun ölçtüğü şey oyuncunun oynadığı şeydir |
| Tahta | Büyük: 10x10, 10x12 ve türevleri; bölüm başına değişken W×H + şekil maskesi | Zor bölümlerde satır/sütun sayısı değişecek |
| İçerik | İlk 1000 bölüm algoritmik üretilecek, hamle sayıları botla kalibre edilecek | Elle 1000 bölüm dengelenemez |
| Tema | Yaşlı bir usta ressam hasar görmüş müzeyi onarıyor; tablolar yapboz parçaları olarak toplanıyor (§18; 2026-10-24'e kadar genç bir ressamdı) | Bölümler yalnızca ilerleme aracı; oyuncuyu geri getiren meta oyun. Kamu malı tablolar içerik maliyetini sıfıra yaklaştırıyor |
| Hedef oyuncu | 30 yaş üstü rahat oyuncu; içerik aileye uygun (§19) | Türün en büyük ve kanıtlanmış kitlesi |
| İş modeli | Ücretsiz oyun + uygulama içi satın alma + isteğe bağlı ödüllü reklam (§19) | Türün standardı |
| Ekip | Kullanıcı ve Claude; görseller en son (Claude Design / 3D araçlar) | Darboğaz çizim; içerik parça parça çoğaltılabilir olmalı |

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
- **Puan fonksiyonu:** hedef ilerlemesi (ana bileşen) + hedef tamamlama bonusu + üretilen özel taşın değeri − harcanan özel taşın değeri (×0,25) + kısmi hasar (kutu canı, jöle katı, zincir) + malzemenin aşağı inmesi + temizlenen taş sayısı. Kazandıran hamle her zaman seçilir.
- **Beceri profilleri:**

| Profil | Dikkat (hamleyi görme %) | Sıcaklık (softmax) | Kullanım |
|---|---|---|---|
| Novice | 50 | 12 | Kolay bölüm kontrolü |
| Casual | 70 | 6 | Gündelik oyuncu |
| **Average** | 85 | 3 | **Kalibrasyonun ana profili** |
| Expert | 100 | 0 (her zaman en iyi) | Hatasız ama tek hamle ileriyi gören oyuncu; "çözülemez bölüm" tespiti |

- Botlar eğitilmedi: kurallar ve puanlar elle yazıldı, oynadıkça hiçbir şey öğrenmiyorlar. CNN taklit modeli için gerçek oyuncu verisi gerekir; bu yüzden ilk sürüm **parametrik greedy**. Yayındaki oyuncu verisiyle profiller yeniden kalibre edilir (§20).
- **Özel taş biriktirme (düzeltildi, 2026-10-03):** harcanan özel taşın değerinin yarısı kayıp sayıldığı için botlar özel taşları biriktiriyordu. 1000 bölüm × 100 oyunluk ölçümde expert, ürettiği özel taşların %19'unu kullanmadan bırakıyordu; zarar neredeyse yalnızca büyük "renk topla" hedefli bölümlerdeydi. Ceza 0,5'ten 0,25'e indi: iki profil için de en iyi değer bu (limitle kazanma expert %84,8 → %86,8, average %64,2 → %65,3). 0 ise eşya indirme bölümlerinde özel taşları erken harcatıyor. Bölümler eski ayarla kalibre edildiği için limitler 6. adımda yeniden üretilecek (§21). (Yapıldı: 1000 bölüm 2026-10-04'te yeni ayarla yeniden üretildi, §13.)
- **Usta bot (2026-10-03, `UstaPolicy`):** adil bir ölçüm botu. Limit belirlemez, raporlarda gerçekçi beceri tavanını gösterir; kalibrasyon greedy profillerde kalır (üretici ustayı kalibrasyon botu olarak kabul etmez). Araştırmadaki uyarı (güçlü arama zorluğu düşük gösterir) bu yüzden geçerli değil.
  - **Nasıl oynar:** her hamleyi expert gibi puanlar ve en iyi 8'ini aday seçer; 2 yer özel taş patlatan hamlelere ayrılır. Adayları kendi rastgeleliğiyle örneklediği olası geleceklerde karşılaştırır: her örnekte aday hamleyi oynar, sonra o tahtadaki en iyi ikinci hamleye bakar (iki hamle ileri). Zayıf adaylar art arda yarıya elenir. Bütçe karar başına 24 örnek (8 aday × 1, 4 × 2, 2 × 4); bütün adaylar aynı örnek geleceklerde denenir.
  - **Adillik:** gerçek gelecek taşları ve oyunun gerçek efekt ve karıştırma rastgeleliğini göremez. Olası gelecekleri `Game.CreateImagined(seed)` üretir: bölümün herkese açık spawn kuralları, botun kendi tohumu. Renk dengesi ağırlıkları kopyalanır; bu, geçmiş taşları kusursuz hatırlayan bir oyuncunun bilebileceği bilgi. Testler, aynı tahtada gerçek gelecek farklı olsa da aynı hamlenin seçildiğini ve kararın kalan hamleye bağlı olmadığını doğrular.
  - **Neden MCTS değil:** ölçülen hızda karar başına yaklaşık 550 deneme oyunluk bütçe var. Gerçek bir ağaç araması bu bütçede derinleşemiyor; üç bağımsız tasarım arasından değerlendiriciler örneklemeli iki hamle aramayı seçti.
  - **Örneklemesiz iki hamle denendi, reddedildi:** 40 bölümlük örneklemde biraz daha iyi görünmüştü. 1000 bölümün tamamında ise limitle kazanmada 1,0 puan geride, ortancada 0,9 hamle fazla çıktı ve rapor 24 dakika sürdü. Örneklem temsil edici değildi (1000 bölümdeki 356 "sadece renk topla" bölümüne karşı 5). Ders: bot kararları 1000 bölümün tamamında doğrulanır.
  - **İsimler:** `usta` (24 örnek) ve `usta-<n>` (n örnek). Web test uygulaması yalnızca `usta`yı ve en fazla 100 simülasyonu kabul eder.

**Üç ajan ve hata payı** (araştırma §3 ile uyumlu):

| Ajan | Ne bilir | Durum | Ne ölçer |
|---|---|---|---|
| (a) **Çözücü** — beam search, mükemmel bilgi | Gelecek taşları bilir (oyunun birebir replikası) | ✅ `BeamSearchSolver` | "Her seferinde doğru hamle" ile bulunan en iyi hamle sayısı (alt sınır tahmini) |
| (b) Geleceği bilmeyen güçlü ajan ("usta" bot) | Yalnızca tahtayı; birçok olası geleceği örnekler | ✅ `UstaPolicy` (örneklemeli iki hamle arama) | Gerçekçi beceri tavanı |
| (c) İnsan benzeri bot | Yalnızca tahtayı; dikkat + sıcaklık | ✅ `GreedyPolicy` profilleri | Hamle limiti kalibrasyonu |

- Mükemmel oyunu bulmak taşlar bilinse bile NP-zor. Çözücünün sonucu "optimal" değil, "bulunan en iyi"dir. Kısa çözümlerde `ExhaustiveSearch` bütün dizileri deneyerek optimumu kanıtlayabilir (§13).
- Hamle limiti yalnızca çözücüye göre ayarlanmaz.
- Faz 2 metrikleri:
  - `marj = limit − P80(b)`
  - `beceri aralığı = geçme(b) − geçme(c)`
  - `şans payı` = seed'ler arası geçme varyansı

**Beceri merdiveni (2026-10-06).** Dört greedy profilin arası 5 puanlık basamaklarla dolduruldu: `skill-30` … `skill-100` (15 bot). Basamak numarası botun fark ettiği hamle yüzdesidir; yargı hatası dört profilden geçen doğrularla belirlenir. `skill-50`, `skill-70`, `skill-85` ve `skill-100` tam olarak novice, casual, average ve expert'tir; adlı profiller değişmedi. Basamak adları bot adı geçen her yerde kullanılabilir (CLI, profil, test paneli).

Bölümler merdivenle ölçülür (`puzzle ladder`; bölüm ve bot başına 60 oyun, her bot aynı oyunlarda; hiçbir bölüm dosyası değişmez; sonuç `reports/ladder.csv`). Bankadan derlenen bugünkü bölümlerde (2026-10-12, §24; paylar §8 madde 9), limitte kazanma oranı:

| Basamak | 1–50 | 51+ Kolay | 51+ Normal | 51+ Zor | 51+ Süper Zor |
|---|---|---|---|---|---|
| 30 | %81 | %76 | %56 | %35 | %23 |
| 40 | %86 | %83 | %64 | %44 | %30 |
| 50 (novice) | %90 | %88 | %73 | %53 | %37 |
| 55 | %92 | %91 | %77 | %58 | %43 |
| 60 | %94 | %94 | %83 | %64 | %50 |
| 65 | %96 | %96 | %88 | %71 | %58 |
| 70 (casual) | %98 | %98 | %92 | %79 | %68 |
| 75 | %99 | %99 | %95 | %85 | %76 |
| 80 | %99 | %99 | %97 | %90 | %83 |
| 85 (average) | %100 | %100 | %99 | %94 | %89 |
| 90 | %100 | %100 | %99 | %97 | %94 |
| 100 (expert) | %100 | %100 | %100 | %99 | %98 |

- Tablo yeni tohumlarda ölçülür; 55. basamak: %91 / %77 / %58 / %43 (hedef 90 / 75 / 55 / 40). Oyuncunun gerçekten oynadığı havuz tohumlarında aynı basamak %91,4 / %76,7 / %56,6 / %40,7 verir (§24); yeni tohumlardaki oran havuzdakinden 1–3 puan yüksektir.
- Tablo, bölümleri yeniden üretmeden "şu basamaktaki oyuncu şu oranı tutsun" sorusunu cevaplar: dosyada her bölüm ve basamak için o botun %70, %80, %90 ve %95 kazandığı en küçük limit de var.
- **Önceki bölümlerde** (51+ average bota göre, yüzde 25 payla) aynı ölçüm 55. basamak için Kolay %72, Normal %46, Zor %17, Süper Zor %8 veriyordu; 50. basamaktaki bir oyuncunun Zor bölümlerde %70 kazanması için ortalama 72 hamle gerekiyordu (limit 39). Yalnızca limiti uzatmak yetmediği için bölümler yeniden üretildi; hedefler de küçüldü.

## 8. Hamle sayısının hesaplanması

**Temel fikir:** bot kalan hamleye bakmadan oynadığı için, **sınırsız hamleyle** N oyun oynatıp her oyunun "kaçıncı hamlede bittiğini" kaydetmek yeterlidir. Herhangi bir hamle limiti `L` için kazanma oranı = `bitiş hamlesi ≤ L` olan oyunların oranı (birikimli dağılım). Tek bir simülasyon grubuyla bütün `L` değerleri için kazanma eğrisi elde edilir.

1. Average bot ile N = 150 oyun (üst sınır 150 hamle) → bitiş hamlesi dağılımı.
2. `L` = hedef kazanma oranına ulaşan **en küçük** hamle sayısı.
3. **Doğrulama:** yeni seed'lerle 200 oyun; kazanma oranı hedefin ±%7'si içinde değilse `L` ayarlanır.
4. Ek metrikler: **near-miss** oranı (kaybedip hedefin ≥%80'ine ulaşan oyunlar — "bir hamle daha" hissi), ortalama karıştırma sayısı, Expert/Novice kazanma oranı farkı.

İki ek (2026-10-02 kararı, §21):

5. **Casual koruması (karar ve uygulama 2026-10-03):** limit average bota göre belirlenir; Kolay ve Normal bölümlerde casual bot da bir alt sınırı tutturmalıdır. Limit, ikisini birlikte karşılayan en küçük hamle sayısıdır: `max(average'ın hedef limiti, casual'ın alt sınır limiti)`. Zor ve Süper Zor bölümlerde koruma yok.

   | Bölüm | Kolay: hedef / casual alt sınırı | Normal: hedef / casual alt sınırı |
   |---|---|---|
   | 1–50 (2026-10-05'e kadar; şimdi madde 7) | %95 / %80 | %85 / %60 |
   | 51–300 (2026-10-06'ya kadar; şimdi madde 9) | %90 / %75 | %75 / %50 |
   | 301–1000 (2026-10-06'ya kadar; şimdi madde 9) | %85 / %70 | %65 / %40 |

   - **Neden aralığa göre:** sabit 80/60 eşiği 51. bölümden sonra Kolay ve Normal bölümlerin %95'inden fazlasında limiti belirliyor ve zorluk artışını yok ediyordu (Normal'de average her aralıkta %89). Casual ile average arasındaki 19–32 puanlık fark bölümlerden değil botlardan geliyor; hiçbir bölüm tasarımı kapatmıyor. Aralığa göre inen eşik ilk 50 bölümde tam olarak 80/60, sonrasında zorluk artışını koruyor (Normal'de average 88 → 81 → 75) ve çoğu Kolay ve Normal bölüme yaklaşık 2 hamle ekliyor.
   - **Doğrulama:** yeni tohumlarla iki bot da kendi eşiğine tolerans içinde olmalıdır. Kural, korumasız eski kuralın aynısı gibi iki yönlü: daha sıkı olan koşul hedefinin tolerans kadar altında da üstünde de kalamaz; limit çok cömertse yeniden okunur.
   - **Maliyet:** Kolay ve Normal adaylarında casual bot da oynar (aday başına 320 oyun daha); 1000 bölümlük üretime yaklaşık 10 dakika ekler.
   - Alt sınırı tutturmak için limit hamle aralığını aşınca üretici önce hedefleri küçültür; bu yüzden koruma çoğu zaman "daha çok hamle" yerine "biraz daha kolay hedef" olarak görünür.
6. **Limit+5 sütunu (uygulandı, 2026-10-03):** raporlar `L + 5` hamledeki kazanma oranını da gösterir (`botstats`: son sütun `winAtLimitPlus5`; üretici raporu: `win`'in yanında `winPlus5`). 5 değeri `ExtraMovesOffer.Moves` sabitinde. `L + 5` hamle üst sınırını aşarsa hücre boş kalır. "+5 hamle" teklifinin bölümü ne kadar kolaylaştırdığı böylece görünür. Kalibrasyon yine `L`'ye göre yapılır; dağılım elde olduğu için ek simülasyon gerekmez.
7. **İlk oda kuralı (karar ve uygulama 2026-10-05):** birinci müze odasında (bölüm 1–50) limiti average bot değil, zayıf oyuncu modeli belirler; casual koruması bu aralıkta kullanılmaz. Gerekçe: ilk insan verisi (§13, §20).

   | Bölüm | Kademe | Limiti belirleyen bot ve hedef |
   |---|---|---|
   | 1–3 | Kolay | random %90 (yalnızca geçerli hamle yapan biri de kazanmalı) |
   | 4–10 | Kolay | novice %95 |
   | 11–20 | Kolay | novice %90 |
   | 21–30 | döngü | novice: Kolay %90, Normal %80, Zor %65, Süper Zor %55 |
   | 31–50 | döngü | novice: Kolay %80, Normal %60, Zor %40, Süper Zor %30 (geçici; insan verisiyle gözden geçirilecek) |
   | 51–1000 | döngü | 55. basamak botu, onaylanan paylar (madde 9); 2026-10-06'ya kadar average + casual koruması |

   - Tohum havuzu da aynı botla kurulur; ilk denemeler zayıf oyuncu için de dengeli tohumlarla başlar.
   - Profilde her bant kendi kalibrasyon botunu adlandırır (`bands[].calibrationBot`, §9). 51 ve sonrasında kalibrasyon botu 2026-10-06'dan beri `skill-55` (madde 9).
8. **Hamle payı (karar ve uygulama 2026-10-05, "bütün bölümler yüzde 25 yumuşatılsın"):** kalibrasyon ve doğrulama yukarıdaki gibi çalışır ve **kalibre limiti** `L₀` verir. Bölümün limiti `L = ⌈L₀ × 1,25⌉` olur (yukarı yuvarlanır; örnek: 19 → 24). Profilde `calibration.moveMargin` (0,25) varsayılandır; bir bant kendi payını yazabilir (`bands[].moveMargin`, sonraki bantlar miras alır). 2026-10-06'dan beri pay yalnızca ilk odada: 51 ve sonrasının bandı payını 0 yazar (madde 9).
   - Paydan sonra her şey `L`'de ölçülür: tohum havuzu kalibrasyon botunun `L`'deki kazanma oranına göre dengelenir; rapordaki `win`, `winPlus5`, `floorWin`, near-miss, casual ve expert oranları `L`'dedir. `targetWin` ve `moveRange` kalibre limiti anlatır; bu yüzden `moves` hamle aralığının dışına çıkabilir ve `win − targetWin` yumuşatmanın büyüklüğünü gösterir.
   - Rapora `calibratedMoves` (L₀) ve `moveMargin` sütunları eklendi. `generate --repair` payı değişen bölümü yeniden üretir; pay varken limiti yalnızca tek yönlü denetler (bir bot hedefinin toleranstan fazla altına düşmüşse), çünkü hedefin üstünde olmak payın amacıdır.
   - Pay 0 iken üretici eskisiyle bayt bayt aynı bölümleri verir (1–60. bölümlerde sınandı). Pay, hiçbir hamle aralığının tepesini simülasyon üst sınırının (150 hamle) ötesine taşıyamaz; profil yüklenirken reddedilir.
   - **Bedeli:** ilk oda kendi kalibrasyon botu için neredeyse kaybedilmez oldu (novice 1–21'de %98–100) ve o bölümlerde havuz tohumları ayırt etmiyor. Bant başına pay 2026-10-06'da eklendi (madde 9); ilk odanın payı 0,25 kaldı.
9. **Ortalama oyuncu ve onaylanan paylar (karar ve uygulama 2026-10-06):** zorluk bir botun kazanma oranıyla ayarlanır; "ortalama oyuncu" artık average bot değil, ilk insan test oyuncusunun beceri merdivenindeki yeri olan **55. basamak** botudur (`skill-55`; §7, §13). 51. bölümden itibaren bu bot, ilk denemede şu payı kazanır:

   | Bölüm | Kolay | Normal | Zor | Süper Zor |
   |---|---|---|---|---|
   | 51–1000 | %90 | %75 | %55 | %40 |

   - Paylar limitin kendisinde tutulur: bu bölümlerde hamle payı 0'dır ve casual koruması yoktur (55. basamak casual bottan zayıf; koruma daha güçlü bir kalibrasyon botu için vardı). 301. bölümdeki sıkılaşma kaldırıldı.
   - Değişmeyenler: ilk oda (madde 7 ve 8), kademe döngüsü, hamle aralıkları, renk sayıları, tahta boyutları, mekanik takvimi.
   - Hamle aralıkları average bot dönemindekiyle aynı kaldığı için, daha zayıf bir bot aynı aralıkta hedefe ancak daha küçük hedeflerle ulaşır: limitler kısalır, hedefler küçülür (sonuçlar §13).
   - Daha çok insan verisi geldikçe ortalama oyuncunun basamağı yeniden kestirilir (`puzzle humans` ve `reports/ladder.csv`); değişirse profilde yalnızca bandın `calibrationBot` alanı değişir.

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

### Uygulama (Faz 2, `Puzzle.Generator`)

Bütün ayarlar bir **profil dosyasında**: `profiles/default.json`. Farklı kombinasyonları denemek için profilin kopyasını değiştirip ayrı bir klasöre üretmek yeterli:

```
puzzle generate --profile profiles/deneme.json --from 1 --to 100 --out levels/deneme
```

Raporları (`report.csv` ve kademe özeti) karşılaştırarak profiller kıyaslanır.

| Profil alanı | Ne belirler |
|---|---|
| `cycle`, `tutorialUntil` | Zorluk testere dişi; ilk bölümler hep Kolay |
| `bands[]` | Bölüm aralığına göre hedef kazanma oranı, **hamle aralığı** (kademe başına), renk sayısı, en fazla engel türü. Her bant yalnızca değişen alanları yazar, gerisini öncekinden miras alır |
| `sizes` | Kademe başına tahta boyutları. `fixedUntil`'e kadar sabit; bölüm içinde boyut hiç değişmez. `plainShapesUntil` (20): o bölüme kadar tahtalar düz dikdörtgen |
| `palette` | `newColorsFrom`'dan sonra her bölüm grubu (`chapterSize`) 8 renk arasından kendi paletini alır |
| `mechanics[]` | Mekanik açılış takvimi (aşağıdaki tablo). Yeni açılan mekanik `hookLevels` bölüm boyunca öne çıkarılır |
| `calibrationBot`, `bands[].calibrationBot` | Hamle limitini ve tohum havuzunu belirleyen oyuncu modeli. Üst düzeydeki alan yalnızca ilk bandın varsayılanıdır; her bant kendi botunu adlandırabilir, sonraki bantlar miras alır. Merdiven basamakları da yazılabilir (`skill-55`). Bugün: 1–3 `random`, 4–50 `novice`, 51+ `skill-55` (§8 madde 7 ve 9). Ölçüm botu (usta) olamaz |
| `calibration.moveMargin`, `bands[].moveMargin` | Hamle payı (§8 madde 8): kalibre limitin üstüne eklenen oran, 0–1 arası. Üst düzeydeki alan varsayılandır (0,25); bir bant kendi payını yazabilir, sonraki bantlar miras alır. Bugün: 1–50 0,25; 51+ 0 |
| `reportBots` | Raporda ayrıca kazanma oranı gösterilen oyuncular (`casual`, `expert`) |
| `floorBot`, `bands[].casualFloor` | Casual koruması (§8): alt sınırı tutturması gereken bot (`casual`) ve aralık başına alt sınırlar. Koruma botu kalibrasyon botundan zayıf bir oyuncu modeli olmalı (random < novice < casual < average < expert); ölçüm botu olamaz. 2026-10-06'dan beri hiçbir bandın alt sınırı yok (§8 madde 9); düzenek duruyor. Raporda `floor`, `floorWin`, `floorSetLimit`, `floorBot` sütunları; `generate --repair` alt sınırı, koruma botu ya da hedefi değişen bölümleri yeniden üretir |
| `calibration`, `seedPool` | Oyun sayıları, tolerans, aday sayısı |

Profil dosyasında biçimde olmayan bir alan (yazım hatası, yanlış yere konmuş alan) artık yükleme hatasıdır; eskiden sessizce yok sayılıyordu ve bir bandın bütün bölümlerini fark ettirmeden değiştirebiliyordu. İlk oda için 2026-10-05'te kullanılan iki geçici profil (`onboarding-1-3`, `onboarding-4-50`) `default.json` içine katlandı ve silindi; bugünkü profilin katlanmadan önceki hâli test fikstürü olarak duruyor (`tests/fixtures/profile-before-onboarding.json`).

**Bir bölümün üretimi:**
1. **Plan** (`LevelPlanner`).
2. **Aday:** portföyden şekil stili, engel ailesi ve desen seçilir; her deneme farklı bir kombinasyon.
3. **Beslenebilirlik analizi:** kutu ve zincir hiç kırılmasa bile her hücreye yukarıdan veya çaprazdan taş gelebilmeli. Kenara dayalı bir kutu çifti köşedeki alt hücreyi kapattığı için reddedilir.
4. **Kalibrasyon:** bot sınırsız oynar → hedef kazanma oranını veren limit. Limit aralık dışındaysa ölçeklenebilir hedefler (renk toplama, malzeme) büyütülür veya küçültülür. **Engel miktarı tahta başına aranır (2026-10-06):** bir tahta ilk ıskada bırakılmaz; engel miktarı, limitin kaçırdığı yöne doğru en çok dört adımda ayarlanır (planlanan miktarın üçte biri ile üç katı arasında). Tahta yine oturmazsa sıradaki tahtaya geçilir (bölüm başına en çok sekiz tahta).
5. **Doğrulama:** kalibrasyonda hiç kullanılmamış seed'lerle; sapma toleranstan büyükse limit yeniden okunur.
6. **Eşit zorluk havuzu:** seed'ler **bölümün doğrulanmış kazanma oranını** hedefleyerek seçilir. Medyan seed'i hedeflemek yanlıştı: zor bölümlerde seed zorluğu çarpık dağılıyor; ortanca seed ortalamadan çok daha zor çıkabiliyor (#310: kazanma %26, ortanca seed'lerin %0–8'i).
7. **Hamle payı** (§8 madde 8): limit, kalibre limitin bandın payı kadar üstüne çıkarılır (ilk odada yüzde 25, sonrasında 0); havuz (6. adım) ve rapor botları (8. adım) bu son limitte ölçülür.
8. **Rapor botları:** casual ve expert kazanma oranları ölçülür; dosya ve rapor satırı yazılır.

Sekiz tahtanın hiçbiri oturmazsa limiti aralığa en yakın aday tutulur ve rapora `relaxed` diye işaretlenir. Bu limit de yeni tohumlarla denetlenir; hedefi tolerans içinde tutmuyorsa o oyunlardan yeniden okunur. Gevşetilen bölüm hamle aralığından çıkar, kazanma oranından çıkmaz.

**Arama neden değişti (2026-10-06, iki adımda):**
- Eskiden engel miktarı adaydan adaya tek bir çarpanla taşınıyordu: aday uzun gelirse sıradaki aday daha az engelle çiziliyordu. Uzunluğu bu çarpana bağlı olmayan bir aile (indirilecek malzeme, jöle bandı) art arda "uzun" çıkınca çarpan çöküyor, sıra kutu ya da zincir tahtasına geldiğinde tahtada neredeyse hiçbir şey kalmıyordu. 55. basamak botuyla ilk üretimde 31 bölüm bu yüzden aralık dışında kaldı (ör. 969: 4 kutu, 5 hamle; 127: 2 zincir, 6 hamle). İlk düzeltme, yalnızca hiçbir adayı oturmayan bölümde tahtaları tek tek kendi miktarıyla yeniden denemekti.
- Aynı gün ikinci adım (sahibinin onayıyla): her tahta baştan beri kendi miktarını arar; taşınan çarpan kaldırıldı. Gerekçe çeşitlilik: zayıf botla engelli tahta planlanan miktarda çoğu zaman uzun gelir; tahta hemen bırakılınca sıra, her miktarda oturan yalnızca renk toplamalı tahtalara geliyordu (sonuçlar §13).
- Bir tahtanın şekli ve engel aileleri sabittir; engellerin yeri, ikinci ailenin deseni ve eklenen renk hedefi miktarla değişebilir (çizim sırası engel sayısına bağlı). Miktar değişince aynı kalan tahta yeniden oynatılmaz: kutular sütun ve küme hâlinde gelir, malzeme sayısı ve en küçük jöle desenleri miktarı izlemez.
- **İlk oda eski aramayla üretildi** ve dosyaları olduğu gibi duruyor (kayıtları var). Bugünkü kodla yeniden üretilirse değişir; `generate` var olan dosyaya dokunmaz, `--repair` de satırı hâlâ geçerli olan bölümü yeniden üretmez. İlk oda yeniden ayarlanacağı zaman eski dosyalar `levels/archive/` altına alınır.

### Zorluk eğrisi — 10'luk testere dişi

| Döngüdeki sıra | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| Kademe | Kolay | Normal | Normal | Normal | **Zor** | Kolay (nefes) | Normal | Normal | **Zor** | **Süper Zor** |

**İlk oda (2026-10-05):** 20. bölüme kadar her bölüm Kolay (`tutorialUntil` 20); döngü 21'de başlar. İlk Zor bölüm 25, ilk Süper Zor 30; ikisi de sonraki odalardaki adaşlarından çok daha yumuşak (§8 madde 7).

Average bot için hedef kazanma oranı (bölüm ilerledikçe sıkılaşır). Tablo tarihsel: 1–50 sütunu 2026-10-05'ten, öteki iki sütun 2026-10-06'dan beri geçerli değil. İlk odada §8 madde 7, 51–1000'de §8 madde 9 uygulanır:

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

İlk 30 bölümde boyut sabit kalır (öğrenme); boyut değişimi bölüm 30'dan sonra Zor bölümlerde başlar. İlk 20 bölümün tahtaları düz dikdörtgen; şekilli tahtalar ikinci tablonun ilk bölümünde, 21'de başlar. Böylece 11. bölüm yalnızca kutuyu öğretir (karar 2026-10-03).

### Mekanik açılış takvimi

**Karar (2026-10-02):** yeni engeller tablo başlangıçlarına hizalanır. Her yeni tablo (10 bölüm) yeni bir şey öğretir; her yeni müze odası (50 bölüm) büyük bir yenilik getirir. Profil 2026-10-03'te güncellendi; üretilmiş 1000 bölüm 6. adımdaki yeniden üretime kadar eski takvimde (parantez içindeki numaralar). (Yeniden üretim 2026-10-04'te yapıldı; o günden beri bütün bölümler bu takvimde.) Özel taşların tanıtım bölümleri (3, 5, 8, 12, 15) bir **öğretici takvimidir** (karar 2026-10-03): özel taşlar her bölümde eşleşme şekliyle oluşur, oyun kuralı değişmez; oyuncu o özel taşı ilk kez yaptığında arayüz kısa bir anlatım gösterir. Arayüz fazında yapılacak.

| Bölüm | Yeni öğe |
|---|---|
| 1 | Temel eşleşme, renk toplama |
| 3 / 5 / 8 / 12 / 15 | Roket / Bomba / Pervane / Renk Bombası (eski: 11) / Büyük Bomba tanıtımı |
| 11 | Kutu (1 can) (eski: 13) |
| 21 | Jöle (1 kat) |
| 31 | Kutu (2 can) |
| 41 | Zincir |
| 51 | Malzeme indirme; ikinci müze odası bununla açılır (eski: 56) |
| 71 | Jöle (2 kat) |
| 91 | Kutu (3 can) |
| 120+ | 2 engel türü birlikte; 200+ 3 tür |

v2 engelleri ve v2 özel taşları (§16) yayından sonra güncellemelerle gelir; en erken 300. bölümden sonra kullanılır. O zamana kadar 300 sonrası bölümler v1 mekaniklerinin kombinasyonlarıyla üretilir.

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

`contentVersion` (isteğe bağlı): bölümün içerik sürümü. Yazılmamışsa 1'dir; araçlar 1. sürümü dosyaya yazmaz. Yalnızca düzeltme paketleri daha yüksek bir sürüm verir (§20).

| Katman | Karakterler |
|---|---|
| `cells` | `#` oynanabilir, `.` boşluk |
| `tiles` | `.` rastgele taş, `r g b y p o c k` sabit renk (c = camgöbeği, k = pembe), `H`/`V` roket, `X` bomba, `M` büyük bomba, `S` pervane, `C` renk bombası, `1`–`3` kutu, `I` malzeme |
| `jelly` / `chains` | `.` yok, `1`–`2` kat/can |

## 11. Proje yapısı

Güncellendi 2026-10-15 (önceki listede `Puzzle.Generator` yoktu ve CLI'ın komut listesi Faz 1'den kalmıştı). Projelerin birbirine bağımlılıkları ve istemcinin yapısı: [OVERVIEW.md](OVERVIEW.md) bölüm 5.

**Kod**

```
src/Puzzle.Core       netstandard2.1, C# 9 — motor, spawn yönetmeni, deneme kaydı (Unity'ye girer)
src/Puzzle.Bot        netstandard2.1, C# 9 — botlar, merdiven, çözücü, simülasyon (Unity'ye girer)
src/Puzzle.Museum     netstandard2.1, C# 9 — odalar, tablolar, yapboz, ilerleme (Unity'ye girer)
src/Puzzle.Meta       netstandard2.1, C# 9 — can, coin, booster, ödül, +5, mağaza (Unity'ye girer)
src/Puzzle.Tooling    net10.0 — bölüm JSON'u, merdiven ölçümü, insan–bot karşılaştırması
src/Puzzle.Generator  net10.0 — bölüm üreticisi: plan, şekil ve engel portföyü, kalibrasyon (§9)
src/Puzzle.Bank       net10.0 — bölüm bankası ve derleyici, politika, yayına alma (§24)
src/Puzzle.Economy    net10.0 — ekonomi simülatörü (§25)
src/Puzzle.Cli        net10.0 — puzzle: sim, demo, bench, solve, seeds, generate, botstats, ladder,
                      validate, humans, players, bank, assemble, compare, promote, patch, economy
src/Puzzle.Web        net10.0 — web test uygulaması: laboratuvar ve herkese açık test sunucusu
tests/Puzzle.Core.Tests  net10.0, xUnit — bütün C# testleri, istemcinin Unity'siz dosyaları dahil
unity/                Unity 6.6 istemcisi (Android; iOS sonra); motoru derlenmiş hâliyle kullanır
```

**Veri ve içerik**

```
levels/generated      yayındaki 1000 bölüm; assembly.csv, assembly.manifest, report.csv
tests/fixtures        testlerin sabitlenmiş dosyaları (motor izi, kayıt örnekleri, ekonomi paketi)
levels/samples        el yapımı örnek bölümler
levels/archive        değiştirilen bölümlerin eski sürümleri (kayıtlar karşılaştırılabilsin diye)
profiles/             üretim profili (default.json: plan, bantlar, boyutlar, palet, mekanik takvimi)
policies/             zorluk politikası (default.json: oyuncu, paylar, tür hedefleri, sabitlenenler)
economy/              ekonomi ayarları: default.json (oyun ve simülatör ortak) ve değişkenleri
museum/catalogue.json odaların ve tabloların veri dosyası (başlık, ressam, yıl, görüntü, lisans)
art/paintings         lisans listeleri (oda oda); görüntüler unity/Assets/Resources/Paintings
art/fonts             yazı tiplerinin kaynakları ve lisansları (licences.json)
art/unity             istemci çizimlerinin SVG kaynakları (duvar, plaket, madalyon, raf, aplik ...)
reports/              merdiven ve expert ölçümleri (ladder.csv, expert_moves.csv)
recordings/, data/    web testinin oyun kayıtları ve hesapları; git'e girmez
```

**Araçlar ve belgeler**

```
tools/unity           Unity'ye aktarma (motor, bölümler, katalog, ekonomi), tablo ve yazı tipi
                      indirme, çizimleri PNG'ye çevirme
tools/web-theme       taş çizimleri ve taş paleti (web testi ve Unity ortak)
tools/public          herkese açık test sunucusunu başlatan ve durduran betikler
tools/docs-pdf        belgelerin PDF baskısını üreten betik (pandoc + Chrome)
docs/                 belgeler; docs/pdf üretilmiş PDF'ler (kaynak her zaman Markdown dosyasıdır)
```

**Belgeler.** Tasarım belgeleri Markdown olarak yazılır ve gözden geçirilir; her değişiklikten sonra `py tools/docs-pdf/build.py` ile PDF baskıları yeniden üretilir (kapak, belge künyesi, sayfa numaralı içindekiler, üst ve alt bilgi). Kod, `beautiful-code` standardına göre yazılır (kullanıcı kararı, 2026-10-02).

## 12. Yol haritası

| Faz | İçerik |
|---|---|
| 1 ✅ | Çekirdek motor, bot, CLI, testler, örnek bölümler |
| 1.5 ✅ | İki bomba, 8 renk + palet, spawn yönetmeni, mükemmel bilgi çözücüsü, web test laboratuvarı |
| 2 ✅ | Bölüm üretici, 1000 bölüm, zorluk raporu |
| 2.5 ✅ | Motoru "hazır" yapmak: bot düzeltmesi, usta bot, casual koruması, booster'lar, bitiş şöleni, düzeltme paketleri, 1000 bölümün yeniden üretimi (§21). Bitti: 2026-10-12 |
| 3 ✅ | Tema ve meta tasarımı: yapboz kesimi (C#) ✅, ilk iki odanın tabloları ✅; ekonomi simülatörü ✅ 2026-10-13 (§25), 3–20. odaların 90 tablosu ✅ 2026-10-14 (§18). Açık kalan: yan koleksiyonun büyüklüğü ve parça fiyatı (§25) |
| 4 (sürüyor) | Arayüz: tasarım tuvalindeki ekranlar Unity istemcisinde, 15 aşamalı planla (§23); 1–9. aşamalar ✅ 2026-10-14, sıradaki 10. aşama (bölüm içi booster'lar). Oyunun adı en sona bırakıldı (karar 2026-10-05) |
| 5 (sürüyor) | Unity: görsel katman, animasyon event'leri, ipucu, analiz; görseller en son. **Başladı 2026-10-06** (kullanıcı kararıyla öne alındı); ilk oynanabilir sürüm telefonda 2026-10-07 (§23). Durum 2026-10-15: tahta, müze ekranları ve bitiş şöleni istemcide; ipucu, analiz ve ses yok; telefonda en son 2026-10-07'de denendi |
| 6 | Yayın: APK testi → doğrudan dünya geneli (karar 2026-10-05; sınırlı ülke yayını yok, §20) |

Sıra kararı (2026-10-02): tema arayüzden önce gelir, çünkü engellerin görünüşü, renk paleti ve oyunun adı temadan çıkar.

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

**Faz 2 sonuçları — ilk 1000 bölüm** (average bot, doğrulama oyunları; ayrıntı `levels/generated/report.csv`):

| Kademe | Bölüm | Ort. hamle | Hedef | Average | Casual | Expert | Near-miss |
|---|---|---|---|---|---|---|---|
| Kolay | 204 | 22,6 | %87 | %87 | %67 | %96 | %10 |
| Normal | 497 | 26,5 | %68 | %69 | %39 | %90 | %23 |
| Zor | 199 | 31,1 | %43 | %44 | %18 | %74 | %35 |
| Süper Zor | 100 | 35,5 | %28 | %29 | %10 | %57 | %41 |

- Hedeften 7 puandan fazla sapan bölüm: 2. Bütün bölümlerin eşit zorluk havuzu en az 8 seed.
- Gevşetilen veya üretilemeyen bölüm yok.
- Expert bot average'dan belirgin güçlü değil (§7); expert sütunu bilgi amaçlı.

**Expert botun hamle raporu** (`puzzle botstats levels/generated --bot expert --runs 30`, hamle üst sınırı 150; ayrıntı `reports/expert_moves.csv`):

| Kademe | Hamle limiti | Expert en az | Expert medyan | Expert p90 | Limitle kazanma |
|---|---|---|---|---|---|
| Kolay | 22,6 | 6,9 | 14,2 | 19,7 | %96 |
| Normal | 26,5 | 9,8 | 19,0 | 26,3 | %90 |
| Zor | 31,1 | 13,8 | 26,2 | 37,3 | %74 |
| Süper Zor | 35,5 | 17,4 | 34,0 | 49,4 | %59 |

- Expert her bölümü 30 denemenin 30'unda 150 hamle içinde bitirdi. Medyanların toplamı 20.930 hamle; limitlerin toplamı 27.515.
- 26 bölümde expert'in medyanı limiti aşıyor (23'ü Süper Zor, 3'ü Zor). Çoğunda fark 1–3 hamle. En büyük fark #780: medyan 44, limit 33.
- Bu ölçümler özel taş biriktirme düzeltmesinden önce alındı (§7).
- **Bugünkü bölümlerde (2026-10-06, onaylanan paylar; `reports/expert_moves.csv` yenilendi):** ortalama limit / expert medyanı / expert p90 / limitle kazanma: Kolay 25,0 / 8,8 / 12,6 / %100; Normal 29,3 / 12,9 / 17,9 / %100; Zor 31,9 / 16,0 / 22,8 / %99; Süper Zor 36,7 / 19,6 / 29,0 / %97. Hiçbir bölümde expert'in medyanı limiti aşmıyor; medyanların toplamı 13.279 hamle, limitlerin toplamı 29.603. Güçlü bir oyuncu için oyun artık kolay: limitin yarısından azı yetiyor.

**Usta ve expert** (2026-10-03; 1000 bölüm × 30 oyun, üst sınır 150, `botstats` tohumlarıyla eşleştirilmiş; expert 0,25 cezasıyla). Limitler hâlâ eski ayarla kalibre edilmiş limitler; 6. adımda yeniden üretilecek (yeniden üretim 2026-10-04'te yapıldı; aşağıda).

| Kademe | Bölüm | Expert limitle kazanma | Usta limitle kazanma | Expert ortanca hamle | Usta ortanca hamle |
|---|---|---|---|---|---|
| Kolay | 204 | %97,7 | %99,9 | 13,5 | 11,1 |
| Normal | 497 | %91,5 | %99,4 | 18,6 | 14,6 |
| Zor | 199 | %76,5 | %97,5 | 25,8 | 19,4 |
| Süper Zor | 100 | %61,5 | %94,2 | 33,1 | 23,9 |
| **Toplam** | 1000 | **%86,8** | **%98,6** | **20,4** | **15,8** |

- Usta her hedef türünde önde; en büyük kazanç eşya indirme bölümlerinde (+16,5 puan, −6,5 hamle). Ortancada 984 bölümde daha iyi, 1 bölümde daha kötü.
- Rapor süresi 13,4 dakika (12 iş parçacığı; ölçüm sırasında makinede başka iş de çalışıyordu). Karar başına yaklaşık 550 deneme oyunu.
- `botstats` artık bütün çekirdekleri kullanıyor; aynı girdiyle çıktısı birebir aynı, expert raporu yaklaşık iki kat hızlı.

**Yeniden üretim — yeni kurallarla 1000 bölüm** (2026-10-04; düzeltilmiş botlar, casual koruması, hizalanmış mekanik takvimi, 20. bölüme kadar düz tahta; ayrıntı `levels/generated/report.csv`):

| Kademe | Bölüm | Ort. hamle | Hedef | Average | Average, +5 hamleyle | Casual | Expert | Near-miss |
|---|---|---|---|---|---|---|---|---|
| Kolay | 204 | 23,7 | %87 | %92 | %98 | %73 | %98,5 | %6 |
| Normal | 497 | 27,6 | %68 | %76 | %90 | %45 | %94 | %18 |
| Zor | 199 | 30,9 | %43 | %44 | %64 | %18 | %76 | %35 |
| Süper Zor | 100 | 34,9 | %28 | %29 | %45 | %9 | %60 | %40 |

- 1000 bölümün tamamı geçerli; gevşetilen yok; her deneme havuzunda en az 8 tohum var; casual alt sınırını toleranstan fazla kaçıran bölüm yok.
- Casual koruması limiti Kolay'da 172, Normal'de 341 bölümde belirledi. Normal'de average bölüm aralıklarına göre %89 → %81 → %73 (ölçümdeki tahmin 88 → 81 → 75).
- Üretim tekrarlanabilir: 83–164. bölümler yeniden üretildiğinde hiçbir dosya değişmedi.
- Yukarıdaki "Faz 2 sonuçları", "Expert botun hamle raporu" ve "Usta ve expert" tabloları önceki bölüm setiyle ölçüldü; karşılaştırma için duruyor.

**İlk insan verisi ve ilk odanın yeniden üretimi (2026-10-05)**

Herkese açık testte ilk oyuncu (match-3'te deneyimli biri; tek kişi, 37 deneme, 1–10. bölümler) ilk bölümleri çok zor buldu. Botlar aynı tohumlarda ve aynı limitte oynatıldı (`puzzle humans`; karşılaştırılabilir 26 deneme):

| Bölüm (eski) | Kademe | Limit | İnsan | novice | casual | average | expert |
|---|---|---|---|---|---|---|---|
| 1 | Kolay | 16 | 1 / 3 | %78 | %98 | %99 | %100 |
| 2–6 | Kolay | 13–25 | 5 / 5 | %53–100 | %83–100 | %93–100 | %83–100 |
| 7 | Normal | 22 | 1 / 7 | %31 | %63 | %95 | %98 |
| 8 | Normal | 21 | 1 / 2 | %23 | %65 | %93 | %100 |
| 9 | Zor | 27 | 1 / 1 | %0 | %17 | %57 | %67 |
| 10 | Süper Zor | 31 | 0 / 8 (+11 tekrar, hepsi kayıp) | %2 | %20 | %46 | %50 |
| Toplam | | | %35 | %34 | %58 | %80 | %82 |

- **Deneyimli bir insan novice botla aynı oranda kazanıyor.** Bot adları insan becerisini göstermiyor: `average` bot insanüstü. İlk 12 denemenin hamle hamle tekrarında oyuncunun hamle başına topladığı hedef taşı 6,57; novice 7,34; average 10,97.
- Oyuncu özel taşı ihmal etmiyor (kendi eşleşmeleriyle botlardan iki kat özel taş kuruyor); geride kalma nedeni tahtadaki zincirleme düşüşlerden neredeyse hiç taş alamaması ve özel taşları tek tek patlatması.
- Örneklem tek kişi. Yönü destekliyor (ilk bölümler average bota göre ayarlanamaz); hiçbir sayının büyüklüğünü desteklemiyor.

İlk oda §8 madde 7'deki kuralla yeniden üretildi (bölüm ve bot başına 600 oyun, üreticinin görmediği tohumlarla):

| Bölüm | Ort. limit | random | novice | casual | average | Beklenen deneme (novice): yeni / eski |
|---|---|---|---|---|---|---|
| 1–3 | 16,7 | %90 | %100 | %100 | %100 | 1–10 toplam: 10,3 / 93,6 |
| 4–10 | 19,6 | %49 | %95 | %100 | %100 | |
| 11–20 | 17,6 | %29 | %92 | %99 | %100 | 10,8 / 78,8 |
| 21–30 | 23,8 | %23 | %77 | %95 | %100 | 13,2 / 85,1 |
| 31–40 | 22,9 | %11 | %60 | %84 | %96 | 18,0 / 70,9 |
| 41–50 | 22,9 | %9 | %57 | %82 | %96 | 19,5 / 89,9 |

- Örnek: 1. bölüm "110 sarı, 16 hamle" yerine "55 sarı, 19 hamle"; 10. bölüm "144 yeşil, 31 hamle (Süper Zor)" yerine "39 yeşil, 21 hamle (Kolay)".
- 50 bölümün hiçbiri gevşetilmedi; havuzlar en az 8 tohum. 51–1000 dosyaları değişmedi (51–55 yeniden üretilip bayt bayt karşılaştırıldı).
- Eski 50 dosya `levels/archive/before-onboarding-2026-10-05/` altında; eski kayıtlar karşılaştırılabilir kalıyor. Yukarıdaki "Yeniden üretim" tablosunda Kolay ve Normal satırları 1–50'yi eski hâliyle içeriyor.
- Kabul ölçütleri ve prova: `out/onboarding/spec.md`, `out/onboarding/rehearsal-check.js` (depoya girmiyor).

**Yüzde 25 hamle payı — 1000 bölümün yeniden üretimi (2026-10-06)**

Bütün bölümler §8 madde 8'deki payla yeniden üretildi. Tahtalar, hedefler ve kalibre limitler 1000 bölümün 1000'inde öncekiyle aynı; değişen yalnızca limit (kalibre limitin yüzde 25 fazlası) ve tohum havuzları. Gevşetilen bölüm yok; her havuzda en az 8 tohum var.

| Bölüm | Kademe | Adet | Ort. limit (önce → sonra) | Kalibrasyon botu | O botun kazanması | Casual | Expert |
|---|---|---|---|---|---|---|---|
| 1–20 | Kolay | 20 | 18,1 → 23,1 | random (1–3), novice (4–20) | %93 → %99 | %100 → %100 | %100 → %100 |
| 21–50 | Kolay | 6 | 19,7 → 24,7 | novice | %86 → %97 | %97 → %100 | %100 → %100 |
| 21–50 | Normal | 15 | 23,0 → 29,2 | novice | %67 → %86 | %89 → %96 | %99 → %100 |
| 21–50 | Zor | 6 | 25,3 → 32,0 | novice | %51 → %78 | %82 → %96 | %100 → %100 |
| 21–50 | Süper Zor | 3 | 27,0 → 34,0 | novice | %39 → %65 | %73 → %91 | %98 → %99 |
| 51–300 | Kolay | 50 | 23,9 → 30,3 | average | %94 → %99 | %75 → %93 | %99 → %100 |
| 51–300 | Normal | 125 | 26,3 → 33,3 | average | %81 → %95 | %51 → %78 | %96 → %99 |
| 51–300 | Zor | 50 | 29,9 → 37,7 | average | %51 → %82 | %20 → %46 | %79 → %97 |
| 51–300 | Süper Zor | 25 | 32,2 → 40,5 | average | %37 → %67 | %13 → %32 | %68 → %93 |
| 301–1000 | Kolay | 140 | 24,1 → 30,5 | average | %91 → %99 | %71 → %91 | %98 → %100 |
| 301–1000 | Normal | 350 | 28,5 → 35,9 | average | %73 → %92 | %41 → %69 | %93 → %99 |
| 301–1000 | Zor | 140 | 31,6 → 39,8 | average | %40 → %69 | %16 → %37 | %74 → %92 |
| 301–1000 | Süper Zor | 70 | 36,2 → 45,6 | average | %25 → %52 | %7 → %20 | %56 → %82 |

Novice bot (ilk oyuncunun en çok benzediği model) 51. bölümden sonra, yeni limitlerde (bölüm başına 300 oyun, üreticinin görmediği tohumlar):

| Bölüm | Kolay | Normal | Zor | Süper Zor | Hepsi | Bölüm başına beklenen deneme |
|---|---|---|---|---|---|---|
| 51–100 | %66 | %44 | %16 | %5 | %39 | 5,5 |
| 101–150 | %72 | %46 | %17 | %7 | %42 | 5,1 |
| 301–350 | %61 | %35 | %13 | %5 | %33 | 7,4 |
| 601–650 | %74 | %37 | %15 | %9 | %37 | 4,6 |

- **Pay, 51 sonrasındaki duvarı kaldırmıyor.** Casual bot için belirgin bir rahatlama var; novice gibi oynayan biri için ikinci odanın Zor ve Süper Zor bölümleri hâlâ çok zor. Malzeme indirme bölümleri en kötüsü (55. bölüm: novice %10; ortanca oyun 73 hamle istiyor, limit 39).
- İlk oda ise kendi botu için neredeyse kaybedilmez oldu (1–20'de %98–100).
- Asıl çözüm adayı: sonraki odaları da novice bota göre kalibre etmek (profilde bandın `calibrationBot` alanı; hedefler oda oda sıkılaşır). Karar insan verisiyle verilecek.

**Onaylanan paylar — 51–1000'in 55. basamak botuyla yeniden üretimi (2026-10-06)**

İlk test oyuncusunun (Admin) merdivendeki yeri, kayıtlı denemelerinin olabilirliğiyle kestirildi: en iyi uyan basamak 55, 40–60 arası makul, 65 ve üstü verilerle bağdaşmıyor. Sahibinin kararı: bu oyuncu ortalama oyuncu sayılır ve 51. bölümden itibaren ilk denemede Kolay %90, Normal %75, Zor %55, Süper Zor %40 kazanır (§8 madde 9). 51–1000 bu kuralla yeniden üretildi; ilk oda 50 bölümün 50'sinde bayt bayt aynı kaldı.

| 51–1000 | Adet | Ort. limit (önce → sonra) | 55. basamak (hedef) | Casual | Expert |
|---|---|---|---|---|---|
| Kolay | 190 | 30,4 → 25,2 | %90 (%90) | %98 | %100 |
| Normal | 475 | 35,2 → 29,3 | %76 (%75) | %91 | %100 |
| Zor | 190 | 39,2 → 31,9 | %56 (%55) | %77 | %99 |
| Süper Zor | 95 | 44,3 → 36,8 | %41 (%40) | %64 | %98 |

- Her havuzda en az 8 tohum var. Limitler 18 ile 65 hamle arasında; 45'in üstünde 3 bölüm var. Near-miss oranı (kaybedilip hedefin en az %80'ine ulaşılan oyunlar) ortalama %10'dan %19'a çıktı. Tablo ve aşağıdaki sayılar yayındaki son üretime aittir (tahta başına engel miktarı aramasıyla, aynı gün).
- **Hedefler küçüldü, limitler kısaldı.** Örnek: 100. bölüm "88 turuncu, 34 hamle" yerine "60 turuncu, 32 hamle"; 1000. bölüm "36 pembe + 36 sarı, 49 hamle" yerine "23 + 23, 34 hamle"; 55. bölüm "4 malzeme + 21 sarı, 39 hamle" yerine "3 malzeme, 31 hamle".
- **Tahtalar da değişti:** 950 bölümün 401'i average bot dönemindeki tahta stilini korudu, 35'i aynı hedefleri. 51 ve sonrasını henüz kimse oynamadığı için kayıt kaybı yok.
- **Çeşitlilik:** zayıf botla engelli tahtalar hamle aralığına daha zor oturur. İlk üretimde üretici oturmayan tahtayı hemen bıraktığı için bölümler yalnızca renk toplamaya kaymıştı; tahta başına engel miktarı araması (§9) kutu, zincir ve kısmen jöleyi geri getirdi:

  | 51–1000 içinde (950 bölüm) | Average bot dönemi | İlk üretim (55. basamak) | Yayındaki üretim |
  |---|---|---|---|
  | Yalnızca renk toplama | 335 | 528 | 400 |
  | Kutulu | 212 | 158 | 223 |
  | Zincirli | 218 | 185 | 272 |
  | Jöleli | 187 | 78 | 110 |
  | Malzeme indirme | 223 | 70 | 61 |

- **Malzeme bölümleri geri gelmedi.** Nedeni arama değil, mekaniğin kendisi: 55. basamak botu bir malzemeyi indirmek için 15–20 hamle harcıyor; üç malzemeli bir tahta Normal aralığına (20–33 hamle) oturmuyor ve malzeme sayısı engel miktarını izlemiyor (51–250 örneğinde izletmek 200 bölümde yalnızca 4 bölüm kazandırdı, uygulanmadı). Seçenekler: malzemeli bölümlere daha uzun hamle aralığı vermek ya da insanların malzeme bölümlerini nasıl oynadığını görmek (51 ve sonrası oynandıkça). Karar bekliyor. (Karar 2026-10-12: hamle aralığı şimdilik değişmiyor; §24 "Bilinen sınırlar".)
- **Gevşetilen 9 bölüm** (limit hamle aralığının dışında, pay hedefte): 52 (44 hamle), 53 (45), 358 (36), 730 (65), 771 (33), 795 (54), 813 (37), 841 (44), 980 (48). Altısı malzeme indirme ya da jöle bandı bölümü: 55. basamak botu üç malzemeyi bu tahtalarda aralıktaki hamleyle indiremiyor. 730 ve 795 elle gözden geçirilmeye aday.
- Hedeften 7 puandan fazla sapan tek bölüm 899 (%62,5; hedef %55): limit bir hamle azalınca oran hedefin altına düşüyor.
- İlk üretimde 31 bölüm gevşetilmiş, 14 bölüm hedeften 7 puandan fazla sapmıştı (5 ve 6 hamlelik iki bölüm dahil). Nedeni ve düzeltmesi §9'da ("ikinci arama"); düzeltmeden sonra bu 31 bölümün 26'sı değişti. Tahta başına aramaya geçilince gevşetilen 9 bölüm ve 899 aynı kaldı; 950 bölümün 303'ü değişti, 647'si bayt bayt aynı.

## 14. Açık sorular

1. ~~Bomba 5x5 mi 3x3 mü?~~ → İkisi de: Bomba 3x3, Büyük Bomba 5x5 (§3).
2. ~~Dokunarak patlatma?~~ → Evet, 1 hamle.
3. ~~Boyut değişimi bölüm içinde de mi?~~ → Hayır. Bölüm içinde sabit, bölümler ilerledikçe değişir (profil `sizes`).
4. ~~Can sistemi / booster ekonomisi?~~ → 5 can + sınırsız can ödülleri, 7 booster, "+5 hamle" teklifi (§18, §19). Fiyatlar ekonomi simülatörüyle belirlenecek. (Belirlendi 2026-10-13: §25; bölüm içi booster fiyatları hâlâ yer tutucu.)
5. ~~Retry politikası?~~ → Farklı desen, aynı kurallar, aynı zorluk (§17).
6. ~~Başlangıç tahtası?~~ → **Her denemede farklı** (2026-10-01). Sabit tahta desteği motorda duruyor ama kullanılmıyor.
7. ~~Zorluk hedefleri?~~ → Şimdilik §9 tablosu. Farklı kombinasyonlar profil dosyalarıyla denenecek. (Güncel: §9 tablosu tarihsel; ilk odada §8 madde 7, 51–1000'de §8 madde 9 geçerli.)
8. ~~Hedef oyuncu?~~ → 30 yaş üstü rahat oyuncu. Yayından önce limitler `average` bota göre, casual korumasıyla (§8). Gerçek oyuncu verisi geldikçe yeniden ayarlanır (§20). (Güncel: 2026-10-05'ten beri ilk oda zayıf oyuncu modeline göre, 2026-10-06'dan beri 51–1000 ortalama oyuncu sayılan 55. basamağa göre ayarlanır; casual koruması kullanılmıyor: §8 madde 7 ve 9.)
9. ~~Hamle aralığı?~~ → Bölüm aralığına ve zorluk kademesine göre değişken (profil `bands[].moves`).
10. ~~Mekanik takvimi?~~ → Tablo başlangıçlarına hizalandı (§9).
11. ~~Taşların görseli?~~ → Seçildi (2026-10-05, renkler 2026-10-07): her rengin kendi şekli var, renk körlüğüne uygun sabit set (§22).
12. ~~Fiyatlar ve coin miktarları?~~ → Simülatörden okundu, yürürlükte (2026-10-13; hoş geldin hediyesi ve reklamla altın 2026-10-14; §25). Açık: bölüm içi booster fiyatları, yan koleksiyon parçasının fiyatı.
13. ~~20 odanın sanat akımı listesi ve 100 tablo?~~ → Onaylandı ve oyunda (2026-10-14). Odalar akım değil dönemdir; 3–20. odalar zamanda geriye gider (§18).
14. **Oyunun adı:** tema kesinleşti; arayüzle birlikte konuşulacak. (2026-10-05'ten beri: en sona bırakıldı, §12.)
15. **Analiz servisi:** Firebase mi Unity Analytics mi (§20).
16. ~~Sınırlı yayın ülkeleri?~~ → Sınırlı ülke yayını yok; APK testinden sonra doğrudan dünya geneli (karar 2026-10-05, §20).

## 15. Paralel çalışan algoritmalar — mimari

Oyun tek bir algoritma değil, aynı anda çalışan birkaç sistemden oluşuyor:

| Katman | Algoritmalar | Nerede çalışır | Durum |
|---|---|---|---|
| **Spawn yönetmeni** | Denge (dueness) + hedef eğilimi + kaskad kontrolü + DDA yardımı, her taş için birlikte | Oyun içi (Core) | ✅ |
| **Oyuncu botları** | Random, Greedy (4 profil; 2026-10-06'dan beri 15 basamaklı beceri merdiveni, §7), usta (örneklemeli iki hamle arama), çözücü (beam search, mükemmel bilgi) | Analiz, ipucu (Bot) | ✅ |
| **Paralel simülasyon** | Yüzlerce oyun 12 çekirdekte aynı anda; tek seri tüm kazanma eğrisini verir | Kalibrasyon (Bot) | ✅ |
| **Jeneratör portföyü** | 7 şekil stili × engel desenleri, beslenebilirlik analizi, bot kalibrasyonu, eşit zorluk havuzu | Üretim (`Puzzle.Generator`) | ✅ |
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
- **Temalar:** her 50 bölüm bir müze odası, yani bir sanat akımı (§18). (2026-10-13'ten beri oda bir dönemdir, akım değil.) Oda kendi arka planını ve renk paletini getirir. Mekanik değişmez; yalnızca Unity görsel katmanı.

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

## 18. Tema ve meta oyun — ressam ve müze

**Karar (2026-10-02).** Bölümler oyunun yakıtı; oyuncuyu ertesi gün geri getiren şey, bölümlerle kazandığını harcadığı yer. Tema kullanıcının fikri.

**Hikâye:** yaşlı bir usta ressamın ömrünü verdiği müze bir felakette hasar gördü; tablolar parçalanıp dağıldı. Usta, bölüm kazanarak parçaları topluyor, tabloları yapboz gibi birleştirip yerlerine asıyor ve müzeyi oda oda yeniden açıyor.

**Güncelleme (2026-10-24), onay bekliyor:** kahraman artık genç değil, yaşlı bir usta: proje sahibi avatar olarak keçe bereli usta ressamı seçti (§22). Yukarıdaki cümle yeni sözlerdir ve proje sahibinin onayını bekler; eskisi "genç bir ressamın sevdiği müze…" idi. Oyunun hiçbir yazısı ressamdan söz etmiyor (13 dilin tabloları tarandı), bu yüzden değişen yalnız belgeler: bu bölüm, §0, §22, `docs/OVERVIEW.md`, `README.md` ve müzik isteği (`docs/music/MUSIC_BRIEF.md`).

**Yapı.** Üreticideki döngülerle birebir örtüşüyor; mevcut sisteme değişiklik gerektirmiyor:

| Birim | Bölüm | Üreticideki karşılığı |
|---|---|---|
| 1 tablo | 10 | Zorluk testere dişi (`cycle`). Döngünün son ve en zor bölümü tablonun son parçalarını verir |
| 1 müze odası = 1 sanat akımı (2026-10-13'ten beri 1 dönem) | 50 (5 tablo) | Renk paleti grubu (`palette.chapterSize`) |
| Yayındaki oyun | 1000 (100 tablo, 20 oda) | — |

- **Oda sırası:** en sevilen akımlarla başlar (Empresyonizm, Post-empresyonizm), kronolojik değil. İlk 100 bölüm oyuncuyu tutmak için en kritik dönem; en renkli ve en tanınan tablolar orada olmalı.
- **Oda finali:** her odanın 5. tablosu o akımın en ünlü eseri. Her 50 bölümde bir büyük ödül anı olur.
- **Güncelleme (2026-10-13 ve 2026-10-14):** ilk iki oda (Empresyonizm, Post-empresyonizm) yerinde kaldı; 3–20. odalar birer **dönemdir** ve zamanda geriye, 15. yüzyıla kadar gider (aşağıda "Müzenin 100 tablosu"). Her tablo odasının döneminden olur, tür serbesttir; ressamın ünlü olması gerekmez, ünlü portreler aranır (DEVLOG Oturum 20).
- **İlk iki tablo (2026-10-05):** 1–20. bölümler düz ve kolaydır; "tablonun son bölümü en zorudur" kuralı üçüncü tablodan (21–30) itibaren geçerlidir (§8 madde 7).

**Tablolar**
- **Ana içerik:** kamu malı ünlü tablolar, güvenli olmak için 1900 öncesi sanatçılarla sınırlı. Kaynak: müzelerin açık erişim koleksiyonları (Art Institute of Chicago, The Met, Rijksmuseum). Yalnızca Art Institute of Chicago'da yaklaşık 2.000 kamu malı tablo var (2026-10-02 sorgusu).
- **Lisans listesi:** her görüntünün kaynağı ve lisansı kaydedilir, tek tek kontrol edilir. Aynı müzede "American Gothic" ve "Nighthawks" telifli.
- **Güncelleme (2026-10-13):** kural sıkılaştı: görüntü yalnızca eserin sahibi müzenin kendisinin her kullanıma serbest bıraktığı fotoğraf olabilir. Yayındaki 100 tablonun kaynakları: Art Institute of Chicago (1. ve 2. odalar) ile National Gallery of Art, The Met, Rijksmuseum, SMK, Nationalmuseum, Cleveland, Skokloster Şatosu, Getty ve Finlandiya Ulusal Galerisi (aşağıda). Mauritshuis görüntülerini ticari kullanıma serbest bırakmadığı için kullanılmaz (DEVLOG Oturum 20).
- **Yan koleksiyon:** bizim çizdiğimiz özgün tablolar ve etkinlik tabloları. Parçaları coinle alınır. Ana tablolar bölümlere bağlı kalır.

**Yapboz**
- Gerçek mini yapboz: oyuncu parçayı sürükler, parça yerine yaklaşınca oturur. Döndürme yok. İstemeyen için "otomatik tamamla" düğmesi.
- Her kazanılan bölüm eşit sayıda parça verir. Parçalar tepsiye düşer; oyuncu hemen ya da sonra yerleştirir.
- Gerçek parayla rastgele parça paketi satılmaz. Rastgele paket yalnızca ücretsiz etkinlik ödülü olabilir.
- Kesimi algoritma üretir; aynı tohum aynı parçaları verir (motorun determinizm ilkesiyle aynı).

| Bölüm | Parça sayısı | Bölüm başına | Dizilim (yatay tablo) |
|---|---|---|---|
| 1–100 | 10 | 1 | **3-4-3** (sıralar 3, 4 ve 3 parçalı) |
| 101–300 | 20 | 2 | 5×4 |
| 301–600 | 30 | 3 | 6×5 |
| 601–1000 | 40 | 4 | 8×5 |

10 parçayı düz ızgarayla bölmenin tek yolu 5×2; yaygın tablo oranlarında ince uzun parçalar veriyor. 3-4-3 diziliminde parçalar kareye yakın kalıyor (prototipte karşılaştırıldı, kullanıcı 3-4-3'ü seçti). Dikey tablolarda dizilimler 90 derece döner. Prototip atılabilir bir JS denemesi; gerçek kesim kodu C# ile yazılacak (§12, Faz 3). (Yazıldı: `src/Puzzle.Museum`, 2026-10-08; §23.)

**Uygulama durumu (2026-10-08).** Bu bölümdeki yapı, yapboz tablosu ve 3-4-3 dizilimi Unity istemcisinde çalışıyor (§23). İlk odanın tabloları: Sainte-Adresse Plajı (Monet, 1867), Kız Kardeş (Terasta) (Renoir, 1881), Yağmurlu Gün (Caillebotte, 1877), Nilüferler (Monet, 1906), Buğday Yığınları (Yaz Sonu) (Monet, 1890–91); hepsi Art Institute of Chicago'da kamu malı, lisans listesi `art/paintings/room-01/licences.json`. 2. odanın tabloları 2026-10-08'de, 3–20. odalarınki 2026-10-14'te geldi (aşağıda). Yan koleksiyon, coinle parça ve etkinlik tabloları yok.

**Müzenin 100 tablosu (2026-10-14).** Kullanıcı 3–20. odaların önerisini onayladı. Kural: yalnızca üzerinde telif olmayan eserler, her oda bir dönem, tür serbest ama dönemin içinde. Görüntü yalnızca sahibi müzenin kendi serbest bıraktığı fotoğraf olabilir. Odalar 1–2'den (Empresyonizm, Post-empresyonizm) geriye doğru, zamanda geriye gider:

| Oda | Dönem | Final |
|---|---|---|
| 3 | Kuzey Işığı: İsveç, Finlandiya, Danimarka (1875–1899) | Büyük Huş Ağacının Altında Kahvaltı (Larsson) |
| 4 | Amerika: Yaldızlı Çağ (1870–1899) | Kırbaç Oyunu (Homer) |
| 5 | Hollanda: Lahey Okulu ve Amsterdam (1860–1899) | Sahilde Sabah At Gezintisi (Mauve) |
| 6 | Japonya: Edo'nun Son Yılları ve Meiji (1840–1895) | Shin-Ohashi'de Ani Sağanak (Hiroşige) |
| 7 | Fransa: Barbizon, Gerçekçilik ve Salon (1830–1885) | Prenses de Broglie (Ingres) |
| 8 | Amerika: Hudson Nehri ve Sınır Boyları (1825–1870) | Washington Delaware Nehri'ni Geçerken (Leutze) |
| 9 | Danimarka Altın Çağı (1810–1860) | Kolezyum'un Üç Kemeri (Eckersberg) |
| 10 | Romantik Britanya: Constable ve Turner (1800–1850) | Lordlar ve Avam Kamaralarının Yanışı (Turner) |
| 11 | Edo Japonyası: Hokusai ve Hiroşige (1790–1840) | Büyük Dalga (Hokusai) |
| 12 | Devrimler Çağı: Amerika ve Avrupa (1770–1825) | Kırmızılı Çocuk (Goya) |
| 13 | Kral George Çağı Britanyası (1740–1800) | Elizabeth Farren (Lawrence) |
| 14 | Şehir Manzaraları: Venedik, Roma, İstanbul (1720–1790) | San Marco Meydanı (Canaletto) |
| 15 | Rokoko Fransası (1715–1789) | Kitap Okuyan Genç Kız (Fragonard) |
| 16 | Hollanda Altın Çağı: Vermeer Kuşağı (1650–1700) | Küçük Sokak (Vermeer) |
| 17 | Barok Avrupa: Hollanda, Flandre ve Fransa (1600–1650) | Otoportre (Leyster) |
| 18 | İran ve Babür Sarayları: Minyatür Çağı (1520–1650) | At Üstünde Şah Cihan |
| 19 | Rönesans Avrupası: Holbein'den Arcimboldo'ya (1500–1600) | Vertumnus (Arcimboldo) |
| 20 | Flandre ve İtalya: 15. Yüzyıl (1430–1500) | Giuliano de' Medici (Botticelli) |

- **Kaynaklar:** National Gallery of Art (32), The Met (30), Rijksmuseum (13), SMK (6), Nationalmuseum (3), Cleveland (2), Skokloster Şatosu (2), Getty (1), Finlandiya Ulusal Galerisi (1). Her tablonun müzesi, sayfası, kaynağı ve lisans cümlesi `art/paintings/room-NN/licences.json` dosyasında. Kırpılan tablonun kırpma kutusu da orada.
- **Görüntüler:** uzun kenar 1920 piksel, asla büyütülmez. 50 tablonun çerçevesi, paspartusu, renk kartı ya da kâğıt kenarı kırpıldı. 90 yeni tablo toplam 75,5 MB.
- **Getirme:** `node tools/unity/fetch-paintings.js` artık her müzeyi tanır. Kırpma adımı (`tools/unity/prepare-painting.py`, Pillow) aynı indirmeden her zaman aynı baytları üretir. 1. ve 2. odalar birebir aynı kaldı.
- **Kararlar:**
  - K2: Washington tablosu yanlardan kırpılıp 8. odanın finali oldu; Elma Şırası Yapımı yedeğe döndü.
  - K3: üç eserin görüntüsü müzenin kendi sunucusundan indi.
  - K4: notlu üç eser kaldı.
- **Türkçe adlarla ilgili not:** levhalardaki ifadeler ("y. 1460", "muhtemelen 1538", "Genç Hans Holbein", "Abel Grimmer'e atfen") ajanın seçimi. Japonca adlarda uzatma işaretleri düşürüldü (Ōtani → Otani), çünkü oyunun harf kümesinde ō ve ū yok.

**Müze:** hafif. Tamamlanan tablo duvara asılır, oda adım adım onarılır (çatlaklar kapanır, ışıklar yanar). Oyuncu seçim yapmaz. Seçenekli dekorasyon ve müze işletmesi ileride eklenebilir.

**Tema eşlemesi.** Motordaki adlar değişmez; yalnızca görünüşler ve oyuncunun gördüğü adlar değişir.

| Motordaki öğe | Temadaki karşılığı |
|---|---|
| Renkli taşlar | Altı sade şekil: uğur böceği, damla, kare, papatya, koni, hilal (§22) |
| Roket | Fırça darbesi |
| Bomba / Büyük Bomba | Boya sıçraması / büyük sıçrama |
| Pervane | Kelebek (§22) |
| Renk Bombası | Gökkuşağı paleti |
| Jöle | Tablodaki kir ya da eski vernik katmanı |
| Kutu | Nakliye sandığı |
| Zincir | Koruma bandı ya da ip |
| Malzeme | Kayıp çerçeve köşesi ya da fırça |
| v2: yayılan balçık / buz / bal | Dökülen mürekkep / koruyucu cila / tutkal |

Taş renkleri tablonun renklerinden üretilmez; renk körlüğüne uygun sabit set kalır. Tablonun renkleri arka plana ve çerçeveye yansır.

**Booster'lar.** Motora "booster kullanma" diye yeni bir hamle türü eklenecek; hamle sayısını düşürmez. Botlar kalibrasyonda booster kullanmaz: zorluk booster'sız ölçülür, booster oyuncu için gerçek bir avantaj olarak kalır.

| Ne zaman | Booster | Etki |
|---|---|---|
| Bölüm öncesi | Fırça + sıçrama | Tahtada bir roket ve bir bombayla başla |
| Bölüm öncesi | Gökkuşağı paleti | Tahtada bir renk bombasıyla başla |
| Bölüm öncesi | +3 hamle | Hamle limiti 3 artar |
| Bölüm içi | Spatula | Tek bir hücreyi vurur |
| Bölüm içi | Yatay fırça | Bir satırı vurur |
| Bölüm içi | Dikey fırça | Bir sütunu vurur |
| Bölüm içi | Paleti karıştır | Tahtayı karıştırır |

**Motorda (2026-10-12).** Motor kendi adlarını kullanır; tablodaki adlar temanındır.

| Booster | Motordaki adı | Kayıttaki adı |
|---|---|---|
| Fırça + sıçrama | `StartBooster.RocketAndBomb` | `start-rocket-and-bomb` |
| Gökkuşağı paleti | `StartBooster.ColorBomb` | `start-color-bomb` |
| +3 hamle | `StartBooster.ExtraMoves` | `start-extra-moves` |
| Spatula | `BoosterUse.HitCell` | `hit-cell` |
| Yatay fırça | `BoosterUse.HitRow` | `hit-row` |
| Dikey fırça | `BoosterUse.HitColumn` | `hit-column` |
| Paleti karıştır | `BoosterUse.Shuffle` | `shuffle` |

- **Bölüm öncesi** booster'lar oyunun seçeneğidir (`GameOptions.StartBoosters`), her biri denemede en çok bir kez. Özel taşlar başlangıç tahtasındaki rastgele serbest taşların (zincirsiz düz taş) yerine konur. Kendi rastgelelik akışlarını kullanırlar: tahtanın geri kalanı ve sonradan düşen her şey booster'sız oyunla aynıdır. "+3 hamle" yalnızca limiti uzatır (sayı `RuleSet.StartBoosterExtraMoves`).
- **Bölüm içi** booster'lar oyuncunun hamle saymayan eylemidir (`Game.TryUseBooster`). Vuruşlar (hücre, satır, sütun) her hücreye bir kez, bir eşleşmenin vurduğu gibi vurur: zincir bir can kaybeder ve altındaki taşı korur, kutu bir can, jöle bir kat kaybeder, vurulan özel taş patlar, malzeme zarar görmez. Sonra tahta bir hamleden sonraki gibi oturur (düşme, zincirleme eşleşmeler, gerekirse karıştırma). Booster bölümü kazandırabilir.
- **Boşa giden booster olmaz:** hiçbir şeyi değiştirmeyecek kullanım reddedilir ve tahta olduğu gibi kalır (vurulacak bir şey olmayan hücre, tahtanın dışı ya da boşluğu, yeniden dizilemeyen tahta). İstemci booster'ı ancak kabul edilen kullanımda harcar.
- **Karıştırma** yalnızca gerçek bir yeniden dizilişi kabul eder: serbest taşlar yer değiştirir, hiçbiri yeniden boyanmaz, en az bir hücrede başka bir taş durur, tahtada eşleşme oluşmaz ve hamle kalır. Kutu, zincirli taş, özel taş ve malzeme yerinde kalır. Böyle bir diziliş bulunamazsa (örneğin tek serbest taş) kullanım reddedilir. Hamle kalmadığında oyunun kendi yaptığı karıştırma bundan farklıdır: oyun sürsün diye son çare olarak taşları yeniden boyayabilir.
- Kaç booster'ın bulunduğu ve nasıl kazanıldığı motorun değil meta katmanın işidir. Kullanılan booster'lar deneme kaydına sırasıyla yazılır.
- **Bilinen sınır (2026-10-14 kapandı):** kayıt, kullanılan booster'ların adlarını tutar; hangi hücreye ve hangi hamleden sonra kullanıldıklarını tutmaz. Booster'lı bir deneme kayıttan birebir yeniden oynatılamaz. İstemciye booster gelmeden önce hamle, booster ve "+5 hamle"yi aynı sırada tutan tek bir eylem kaydı eklenmeli (kayıt biçimine eklemeli değişiklik). Eklendi: kaydın isteğe bağlı `actions` alanı hamleyi, booster'ı nişanıyla, "+5"i ve reddedilen istekleri sırasıyla tutar; `AttemptReplay` denemeyi birebir yeniden oynatır (§23).
- İstemcide (Unity) booster düğmeleri ve animasyonları yok; sıradaki istemci işi. **Güncelleme (2026-10-14):** bölüm öncesi booster'lar istemcide (bölüm başı kartı, arayüz planının 8. aşaması, §23). Bölüm içi dört booster 10. aşamanın işi ve sıradaki iş; raftaki dört yerleri ayrıldı (9. aşama).

## 19. Ürün kararları

**Karar (2026-10-02).**

- **Hedef oyuncu:** 30 yaş üstü, kısa oturumlarla oynayan rahat oyuncu. İçerik aileye uygun.
- **İş modeli:** ücretsiz oyun + uygulama içi satın alma (booster, ekstra hamle, coin) + isteğe bağlı ödüllü reklam.
- **Can sistemi:** 5 can, yarım saatte bir can dolar, yalnızca kaybedince can gider. 2026-10-13'te yeniden onaylandı: bu kuralla ortalama oyuncu 100. bölüme 8. günde gelir ve bu kabul edildi (§25). Etkinlik ödülü olarak sınırsız can süreleri. Yalnızca meta katmanı; motoru etkilemez.
- **"+5 hamle" teklifi:** hamle bitince coinle ya da reklam izleyerek alınır. Zorluk ayarı teklifsiz limite göre yapılır; raporlar limit+5'teki kazanma oranını da gösterir (§8). Motorda (2026-10-12): `Game.TryTakeExtraMoves`; hamlesi biten oyun 5 hamleyle sürer, **bir denemede bir kez** (deneme kaydındaki tek alanla uyumlu; birden çok alım istenirse ayrıca karar gerekir). Tahtada hamle kalmamışsa önce karıştırılır; karıştırmayla da hamle çıkmayan tahtaya teklif satılmaz (istek reddedilir, hiçbir şey değişmez). İstemci deneme kaydını ancak oyuncu teklifi geri çevirdikten ya da deneme kesin bittikten sonra yazar.
- **Bitiş şöleni:** bölüm kazanılınca kalan her hamle tahtada bir rokete dönüşüp patlar ve coin verir. Kazanma kesinleştikten sonra çalıştığı için zorluk ölçümünü etkilemez. Motorda (2026-10-12): `Game.PlayFinale` yalnızca istemci isteyince çalışır; kalan her hamle rastgele bir serbest taşı rokete çevirir, roketler patlar, tahta oturur; taş yetmezse dalga dalga sürer. Sonucu, kullanılan hamleyi, oyunun toplamlarını ve hedef sayaçlarını değiştirmez (şölenden sonra yazılan kayıt da oyunu anlatır); aynı oyunun şöleni her zaman aynıdır. Tahtada kalmış başka özel taşlar patlatılmaz. Coin miktarı motorun değil ekonominin kararıdır: kalan hamle başına 4 coin (§25).
- **Coin:** booster, ekstra hamle ve yan koleksiyon parçaları için. Ana tabloların parçaları coinle alınmaz.
- **Fiyatlar:** elle tahmin edilmeyecek (simülatör kuruldu: §25; ilk sayılar 2026-10-13'te ondan okundu ve yürürlükte). Bir **ekonomi simülatörü** botların 1000 bölümdeki kazanma oranlarını ve hamle dağılımlarını kullanarak can, "+5" teklifi ve coin akışını ölçecek; "ücretsiz oynayan biri 100. bölüme kaç günde gelir, nerede tıkanır" sorusunu cevaplayacak.
- **Etik sınırlar** (§17'ye ek): oyuncunun para harcama eğilimi zorluğu etkilemez, zorluk gizlice ayarlanmaz, gerçek parayla rastgele kutu satılmaz.
- **Ekip ve görseller:** kullanıcı ve Claude. Görseller en son, Claude Design ya da 3D araçlarla.

**Kararlar (2026-10-18 ve 19).** Prototip denendikten sonra GitHub kayıtlarından yürünüyor; sıra **#6 ipucu → #5 öğretici → #7 booster tanıtımı → #8 dil desteğinin kalanı**. Kaydırarak geçiş (#3) 18 Ekim'de yapıldı.

- **İpucu (#6):**
  - Düğme yok. Oyuncu tahta dururken bir süre hamle yapmazsa ipucu kendiliğinden çıkar; süre bir ayar (başlangıç 5 sn).
  - Bir denemede en çok 2–3 ipucu (ayar; başlangıç 3).
  - Gösterilen hamleyi oyunun bot ailesinden bir bot seçer; uzman bot değil. Amaç oyuncuya yardım etmek, "ama o kadar da değil". Hangi botun seçileceği, ipucunun ortalama oyuncunun kazanma oranını ne kadar artırdığı ölçülerek belirlenir; ölçüm gelene kadar "casual" profili.
  - Görünüş: hamlenin iki taşı parlak bir halkayla çevrilir ve birbirine doğru kıpırdar; titreşim anahtarı açıksa kısa bir titreşim olabilir. Görünüş proje sahibinin gözüyle telefonda denenecek.
  - Kart açıkken, booster seçiliyken, şölende, zincirleme sürerken ve hamleler bittikten sonra ipucu çıkmaz; tahtaya dokununca kaybolur.
  - Hesap motor tarafında, tahtanın bir kopyasında yapılır; oyunun rastgele akışına dokunmaz. Deneme kaydı gösterilen ipucu sayısını tutar.
  - **Ölçüm ve karar (2026-10-19):** 500 bölümde bot başına 10.000 denemeyle ölçüldü. Oyuncu her ipucuna uyarsa (en iyimser durum) kazanma oranı şöyle artıyor: casual ipucuyla ortalama oyuncu +3,3–4,4 puan, zorlanan oyuncu +4,8–7,1 puan. Skill-60 ile +1,2–3,3 ve +2,2–3,7; novice ile −0,6…+1,0 ve +0,6–3,3; average ile +6,3–7,5 ve +8,6–10,7 puan. Proje sahibi **casual** botu seçti. Bekleme **8 saniye**: kayıtlı oyuncular hamle başına ortalama 5,3 saniye düşünüyor; 5 saniyede üç ipucu ilk birkaç hamlede harcanıyordu. Ölçüm `puzzle hints` komutuyla tekrarlanır.
  - **Kayıtlar (karar 2026-10-19):** ipucu görülen denemeler bot–insan karşılaştırmasında **ayrı bir grup** olur ve ipucuna uyan botlarla karşılaştırılır. Neredeyse her denemede ipucu çıktığı için telefon verisi böylece kalibrasyonda kullanılabilir kalır.
- **Öğretici (#5):** dört bölümün hepsi kabul edildi; denemede gerekirse bazıları çıkarılabilir.
  - İlk 3 bölüm: 1. bölümde taş değiştirme ve eşleşme, 2. bölümde dörtlü eşleşme ve fırça, 3. bölümde beşli eşleşme ve palet. Her biri elle gösterilen tek bir hamle, öteki taşlar soluk.
  - Hedef ve hamle sayısı: 1. bölümde plaketlerin kısa tanıtımı.
  - Engeller: her engel ilk görüldüğü bölümde kısaca gösterilir.
  - Müze ve yapboz: ilk parçanın yapboza yerleştirilmesi ve ilk tablonun tamamlanması rehberli.
  - Öğreticinin gösterdiği hamleyi ipucu seçer.
- **Booster tanıtımı (#7):** öğreticiyle aynı dili kullanır; öğreticiden sonra. Kararlar (2026-10-19):
  - Bölüm içi booster'lar (spatula 7, satır fırçası 12, sütun fırçası 16, karıştırma 20) açıldıkları bölümde bir kez elle kullandırılır: gerisi kararır, disk ve hedef aydınlık. Bugünkü ışık halkası ve "Yeni: …" yazısı bu adıma dönüşür.
  - Rehberli kullanım bedavadır: hiçbir şey harcanmaz, hoş geldin hediyesindeki 3 adet olduğu gibi kalır.
  - Sonradan bir booster'a uzun basınca kısa, döngülü bir canlandırma kartı açılır: booster'ın etkisi gerçek tahta çizimiyle gösterilir, video dosyası yok. Kart kapatılabilir.
  - Bölüm öncesi booster'lar (fırça + sıçrama, gökkuşağı paleti, +3 hamle) oyuncu bir bölümü ilk kez kaybedip yeniden denerken başlangıç kartında tek tek aydınlanır, her biri tek cümleyle.
- **Dil desteği (#8):** ilk sürümde 13 dil: İngilizce, Türkçe, Korece, Japonca, Almanca, Fransızca, İspanyolca, Portekizce (Brezilya), İtalyanca, Rusça, Çince (basitleştirilmiş ve geleneksel), Endonezce. İlk dil telefonun dili, desteklenmiyorsa İngilizce; ayarlardan değiştirilir. Tablo adları seçilen dilde, ressam adları değişmez. Çeviriler yayından önce ana dili olan biri tarafından okunur. Arapça ve Tayca sonra.
- **Müzik (#9):** proje sahibinin Suno Premier hesabıyla sözsüz müzik üretilecek: müze teması, tahta müziği, kazanma, kaybetme, tablo tamamlandı, oda onarıldı, ödül. İstekler ve Suno'ya yazılacaklar: `docs/music/MUSIC_BRIEF.md` ("Müzik İstekleri" PDF'i).
- **Harita (#4, kararlar 2026-10-22):** harita gerçek bir dünya haritası olur; müzenin kat planı yerine.
  - Her tablonun iğnesi tablonun **yapıldığı ya da gösterdiği yerde** durur (Monet'nin plajı Normandiya'da, Hokusai'nin Dalgası Kanagawa'da). Tablonun bugün hangi müzede olduğu kartında yazar.
  - Görünüş **eski bir müze haritası**: gerçek kıyılar, oyunun renklerinde (krem kara, camgöbeği deniz, yaldızlı çerçeve, pusula).
  - Oyuncu ileri dönemlere bakabilir: dönemin adı, tablonun yeri, adı, ressamı ve açılacağı bölümler görünür; **resim örtülü** kalır.
  - Etkileşimli tasarım tasarım tuvalinde ("Dünya haritası · dönemler"): açılınca dünyadan oyuncunun dönemine kayar; alttaki şeritte 20 dönem; iğneye dokununca tablo kartı; şu anki tabloda "Oyna". Konum listesi bir ilk derlemedir; 10 tablonun yeri yalnızca bölge düzeyinde, oyuna girmeden doğrulanacak. Proje sahibi tasarımı onayladı (2026-10-22: "eskisine göre daha iyi").
  - **Karar (2026-10-22):** oyunda tek bir genel müzik var: M1 Müze teması, her yerde çalar. Kısa parçalar (kazanma, kaybetme, tablo tamamlandı, oda onarıldı, ödül) ve ayrı tahta müziği yok. Ayarlar kartına "Müzik" anahtarı gelir.

**Karar (2026-10-24): v1'de gerçek parayla satılanlar.** v1, bugünkü oyun ve gerçek satın almalardır. Satılanlar `economy/default.json`'un `store` bölümündedir; tutarlar simülatörden geçti (§25).

- **Dört altın paketi:** kese $1.99 = 1.000, torba $4.99 = 2.750, sandık $9.99 = 6.000, hazine $19.99 = 13.000 altın.
- **Çırak Çantası:** $1.99; 1.500 altın ve üç bölüm öncesi booster (fırça + sıçrama, renk bombası, +3 hamle, birer). Oyuncu başına bir kez. 20. bölümden itibaren mağazanın başında durur; geri sayım yok.
- **Fiyat kuralları** (araştırma §3.2, denge testlerinde): paket büyüdükçe dolar başına altın düşmez, en büyük paket en küçükten en çok %35 fazla verir; her paket tam sayıda "+5" eder ve büyüdükçe daha çok; başlangıç paketinin değeri aynı paradaki altının en çok 2,5 katıdır; fiyatlar .99 ile biter, hiçbiri $19.99'u geçmez.
- Hiçbir üründe rastgele içerik yok; içerik satın almadan önce görünür. Hiçbir satın alma bölümün hamle limitine, tohumlarına ya da yardımına dokunmaz (§17). Her satın almada onay adımı var; kaybetme kartından tek dokunuşla ücret alınmaz.
- Ürün kimlikleri oyunun uygulama kimliğine bağlıdır: `com.saturnwave.museumpuzzle.coins.pouch` gibi. Uygulama kimliği kalıcıdır (karar 2026-10-25): Android'in paket adı ve iOS'un bundle kimliği `com.saturnwave.museumpuzzle`; oyunun adı sonra kesinleşse de değişmez. Ürünleri mağazalarda proje sahibi açar.

**Karar ve uygulama notu (2026-10-24): satın almanın kuralları oyunda.** Satış açıldı (§23, "Gerçek satın almalar").

- **Onay:** her ürün önce bir kartla sorulur (ürün, içerik, mağazanın yerel fiyatı); ödeme yalnız kartın yeşil "Satın al"ıyla, mağazanın kendi ödeme sayfasında yapılır. Mağazadaki hiçbir dokunuş tek başına ödeme yapmaz. Kaybetme kartında ve bölümde gerçek parayla satın alma yolu yok.
- **Çırak Çantası'nın yeri:** teklif sürdükçe altın panelinin ilk sütununda, aynı fiyattaki kesenin yerinde durur; çerçevesinde "BİR KEZ" yazar. Alınınca kese geri gelir. Mağazanın başına ayrı bir panel denendi ve ölçüldü: en kısa telefonlarda (1080×1920, sistem çubuklarıyla) ve dik iPad'lerde düğmeler 38 noktanın altına iniyordu. "Başında" kararı böyle uygulandı; sahip ayrı panel isterse bu ekranlar için başka bir yer seçilmeli.
- **Sunucu yok (gelir raporu §7.3, seçenek A sürüyor):** ödeme telefonda, mağazanın verdiği haliyle kabul edilir; fiş sunucuda doğrulanmaz. Bir ödeme bir kez verilir: altını ve ödemenin kimliği aynı kayıtta saklanır, mağazaya onay ancak kayıttan sonra gider.
  - **İade:** mağaza parayı geri verse de verilen altın ve booster'lar oyuncuda kalır; onları geri alacak bir sunucu yok.
  - **Silip kurma, yeni telefon:** ilerleme yalnız telefonda; alınmış altın paketleri geri gelmez. Çırak Çantası mağazada oyuncunun malı olarak kaldığı için bir kez geri gelir: Google Play'de oyun açılınca kendiliğinden, App Store'da "Satın alımları geri yükle" ile. Bu iki cümle mağaza sayfasında ve destek metninde açıkça yazılmalı.
- **Test modu:** v1 derlemesi parayla sattığı için test modu yok (2026-10-17 kuralı değişmedi). Telefon testleri için test modu gerekirse `build.realMoneyPurchases: "notAvailable"` ile ayrı bir test derlemesi yapılır.
- **Ürünler mağazada yokken** (bugünkü test APK'sı: Play Console'da ürün yok) mağaza "Mağaza şu anda açılamıyor" der; oyun altınla ve reklamla aynen sürer.

**Karar (2026-10-24): v1'de oyun istatistikleri yalnız oyuncunun izniyle; telefondan neyin çıktığı açıkça yazılır.** Sahibin v1 kararları; metinlerin kaynağı `docs/research/privacy-v1.md` (taslak). Uygulama: §23, "Oyun istatistikleri ve rıza".

- **Hizmet:** Aptabase, Avrupa Birliği bölgesi (Almanya). Başka izleyici, reklam kimliği, hesap yok. Oyunun rastgele cihaz kimliği notlara girmez.
- **Soru:** ilk açılışta, giriş sayfasından sonra salonun üstünde bir kez. "İzin ver" ve "İzin verme" aynı boyda ve aynı renkte; geri tuşu "İzin verme" demektir. Yanıt `device.json`'da durur; Ayarlar'daki "Oyun istatistikleri" anahtarıyla her zaman değişir. Soru yanıtlanmadan, "İzin verme"den sonra ve anahtar kapandıktan sonra hiçbir not gönderilmez.
- **Notlar, yalnız bunlar:** oyunun açılması; bölüm başladı, kazanıldı, kaybedildi (bölüm numarası ve kalan hamle); tablo ve oda tamamlandı; mağaza açıldı; satın alma verildi (yalnız ürün; fiyat, işlem kimliği ve fiş yok).
- **Telefondan üç şey çıkar:** ödemeyi mağaza alır; Unity'nin satın alma kütüphanesi (IAP 5.4.4) her satın alma denemesini Unity'ye bildirir, bu kapatılamaz ve oyunda açıkça yazılır; istatistikler yalnız izinle Aptabase'e gider. "Oyun hiçbir şey göndermez" sözü kalktı. Android paketi INTERNET iznini yeniden ister.
- **Sunucu yok:** ilerleme ve altın yalnız telefonda. Oyun silinince ya da telefon değişince kaybolur (telefonun yedeği geri getirebilir); iadeyi Google Play ya da Apple yönetir. Gizlilik kartı bunu söyler.
- **Metinler taslak:** gizlilik kartı, soru, ayar satırları ve onay kartının ikinci satırı taslağın §3'ünden; sahibin onayını bekler (13 dil, makine çevirisi). Sitedeki politika (taslak §4) v1'den önce sahibin yayımlayacağı iş; oyunun içinden politikaya bağlantı, sitenin adresi gelince eklenir.

**Karar (2026-10-24): v1'in avatarı usta ressam; künye oyunda.**

- **Avatar:** proje sahibinin seçtiği A adayı, keçe bereli yaşlı usta ressam; Blender'de işlenmiş resimler (§22). Hikâyenin kahramanı onunla birlikte usta oldu (§18; yeni hikâye cümlesi sahibin onayını bekliyor).
- **Yerleri:** salon, öğretici ve booster tanıtımı yazıları, kazanma kartı, tamamlanan tablo, onarılan oda (§22, §23). Mağazada, kaybetme kartında ve hiçbir satın alma adımında yer almaz; oyunun akışına ve zorluğuna dokunmaz (§17).
- **Künye:** Ayarlar'daki "Künye" satırı bir kart açar: ressamın kaynakları (Einar, CC BY 4.0, "modified" notuyla; Microsoft Rocketbox, MIT metni bütün), tablolar ve fotoğraflarının müzeleri, yazı tiplerinin telif satırları (SIL OFL 1.1), sesler (Kenney, CC0). Lisans satırları her dilde İngilizce, lisans kayıtlarındaki gibi; yalnız başlıklar 13 dile çevrilir. Testler oyunun satırlarını kayıtlarla karşılaştırır. Müzik sahibin kendi üretimi olduğu için listede yok.

## 20. Kalibrasyon, veri ve yayın

**Karar (2026-10-02).**

- **Yayın öncesi kalibrasyon:** `average` bot + casual koruması (§8). İnsan verisi yayından önce az olacağı için ilk sürüm botla ayarlanır. **Güncelleme:** ilk insan verisinden sonra ilk oda (1–50) 2026-10-05'ten beri zayıf oyuncu modeline (random ve novice), 51–1000 2026-10-06'dan beri ortalama oyuncu sayılan 55. basamak botuna göre ayarlanır; casual koruması artık kullanılmıyor (§8 madde 7 ve 9).
- **Deneme kaydı (uygulandı, 2026-10-03):** her oyun denemesi tek bir kayıt. Kayıt tipi `Puzzle.Core/Recording` içinde tanımlı, yani mobil oyun aynı biçimi kullanacak: bölüm, deneme tohumları, hamle limiti, kullanılan hamle, sonuç (kazandı / hamle bitti / hamle kalmadı / yarıda bırakıldı), her hedefin son durumu, "+5" alındı mı, booster'lar, rastgele cihaz kimliği, başlangıç ve bitiş zamanı. Web test uygulamasında ayrıca oynanan hamleler de saklanır; deneme birebir tekrar oynatılabilir.
- **Web test uygulaması:** oynanan her deneme `recordings/attempts.jsonl` dosyasına bir satır olarak yazılır (git'e girmez). Deneme bitince, yarıda bırakılınca, oturum zaman aşımına uğrayınca ve sunucu kapanırken kaydedilir; yazılamayan kayıt bekletilip yeniden denenir. Deneme numarasını sunucu verir. İpucu, çözücü, bot hamlesi ya da DDA yardımıyla oynanan denemeler ve elle seçilen tohumlar kaydedilir ama karşılaştırmaya girmez.
- **İnsan ve bot karşılaştırması:** `puzzle humans` komutu ve web'deki "Kayıtlar" paneli. Her insan denemesinin oyununu (aynı tohumlar, aynı hamle limiti) dört greedy profile oynatır, limitte kazanma oranlarını yan yana koyar ve en yakın profili söyler. Yarıda bırakılan denemeler kazanma oranını yükseltmesin diye iki oran verilir: biten denemeler üzerinden ve yarıda bırakılanlar kayıp sayılarak (alt sınır).
- **Herkese açık web testi (2026-10-04, APK'dan önce):** web test uygulamasının "herkese açık" modu. Tanıdıklar internet adresinden giriş yapıp bölümleri gerçek oyundaki gibi sırayla oynar; her deneme kaydedilir. Test paneli yalnızca sahibine, yönetici anahtarıyla açıktır. Yayın Tailscale Funnel ile yapılır. Kullanım: [PUBLIC_TEST.md](PUBLIC_TEST.md). **Durum (2026-10-15):** sunucu yaklaşık 12 Ekim'den beri kapalı (en son 12 Ekim akşamı veri yazdı); sahibinin kararıyla (2026-10-14) arayüz işleri bitince yeniden açılacak.
- **İlk insan verisi (2026-10-04/05):** deneyimli tek bir oyuncunun 37 denemesi novice botla aynı kazanma oranını verdi (§13). Sonuç: **yayın öncesi kalibrasyonda ilk oda novice bota göre ayarlanır** (§8 madde 7); 51 ve sonrası için karar daha çok veriyle verilecek. Tek bölüm için karar eşiği: 35 ilk deneme bir oranı ancak ±10 puana sabitler; daha azıyla bölüm bazında sayı değiştirilmez.
- **Canlı testte bölüm değiştirme:** sunucu bölümleri yalnızca açılışta okur. Dosyalar diskte değişir, eski sürümler `levels/archive/` altına alınır, sunucu yeniden başlatılır. Yarım kalmış denemesi olan oyuncunun o denemesi düşer; oyuncu bulunduğu bölümün yeni sürümünden devam eder.
- **APK testi:** Play Store'dan önce kullanıcı ve birkaç kişi APK ile oynar. Hataları bulur ve insanların hangi bota benzediğini kabaca gösterir. 1000 bölümün limitlerini tek tek ayarlamaya yetmez; bunun için bölüm başına yüzlerce deneme gerekir.
- **Toplanan veri** (her bölüm denemesi için): bölüm numarası, seed numarası, kullanılan hamle, sonuç, kalan hedefler, "+5" alındı mı, kullanılan booster'lar. Kimlik yalnızca rastgele bir cihaz kimliği. İsim, e-posta ve reklam takibi yok. KVKK ve GDPR için rıza ekranı ve gizlilik politikası. **(2026-10-24)** Deneme kayıtları telefondan çıkmıyor. v1'de rıza sorusu ve yeni gizlilik kartı geldi; telefondan çıkan tek analiz Aptabase'in izinli notları (§19). Politika sitede yayımlanacak (taslak `privacy-v1.md` §4).
- **İstemcide gizlilik ve veri (2026-10-15, arayüz aşaması 13):** ayarlar kartındaki "Gizlilik ve veri" kartı neyin telefonda tutulduğunu söyler (ilerleme, cüzdan, ayarlar, deneme kayıtları). Deneme kayıtlarını sayar ve bir soru sorduktan sonra siler; ilerleme, cüzdan ve ayarlar yerinde kalır.
  - **İlk metin, proje sahibince onaylandı (2026-10-16)** (`PrivacyWords.cs`): bu sürüm telefondan dışarı bir şey göndermez; hesap, reklam ağı ve satın alma yoktur; Unity'nin istatistikleri kapalıdır; kayıtlarda ad ya da telefon bilgisi yoktur, yalnızca rastgele bir kimlik vardır; Android yedeklemesi açıksa dosyalar telefonun yedeğine katılabilir. **v1'de geçerli değil (2026-10-24):** yerini aşağıdaki taslak aldı.
  - **Dayanağı (2026-10-16'da):** oyunun kodu ağa hiç çıkmıyordu; paket INTERNET iznini istemiyordu; reklam, satın alma ya da analiz paketi yoktu; Unity servisleri kapalı; `ProjectSetup` Unity'nin istatistik bayrağını kapatıyor (bu sonuncusu sürüyor).
  - **Rıza ekranı yoktu,** çünkü telefondan bir şey çıkmıyordu. **2026-10-24:** analiz geldi, rıza sorusu da (§19, §23).
  - **Karar (2026-10-16):** INTERNET izni paketten çıkarıldı (A28 kapandı); Android yedeklemesi açık kalıyor (A29 kabul edildi). **2026-10-24:** v1 INTERNET'i yeniden istiyor: Aptabase'in izinli notları ve IAP 5.4.4'ün satın alma olayları için.
  - **v1 metni (taslak, 2026-10-24; sahibin onayını bekliyor):** gizlilik kartı sekiz paragraf. Oyun ilerlemeyi, cüzdanı (satın alınan altın dahil), ayarları ve deneme kayıtlarını yalnız telefonda tutar; sunucu yok. Satın almaları Google Play ya da App Store yapar; kart bilgisi oyuna gelmez; hesap ve reklam ağı yok. Unity'nin satın alma olayları (ürün, fiyat, fiş, telefonun modeli ve dili, iOS'ta IDFV) kapatılamaz. İstatistikler yalnız izinle Aptabase'e (AB) gider; notların listesi; IP saklanmaz; kimlik yok; izin vermeyen için oyun aynı, karar ayarlardan değişir. Kayıtların rastgele kimliği hiçbir yere gitmez. Android yedeği. Sunucu yok: silip kurunca ya da telefon değişince altın ve ilerleme kaybolur, iadeyi mağaza yönetir.
- **Veri hattı:** **Karar (2026-10-24): Aptabase**, yalnız oyuncunun izniyle (§19); Firebase ve Unity Analytics yok, reklam kimliği yok. Deneme kayıtları yine telefonda kalır; APK testinde test eden kişi dosyayı iletir.
- **Mağaza gizlilik beyanları (2026-10-24, taslak `privacy-v1.md` §5).** Beyan, kütüphanelerin yapabildiğine değil v1'in gerçekte gönderdiğine göre. §19'daki notlar ya da Unity'nin davranışı değişirse beyan da değişir. Formu proje sahibi doldurur. App Store'un "App Privacy" cevapları iOS dalında (`IOS_BUILD.md`).
  - **Google Play > Data safety > Data collection and security:** kullanıcı verisi toplanıyor: **Yes**. Aktarımda şifreli: **Yes** (Aptabase ve Unity adresleri `https://`). Hesap açılmıyor. Silme isteği yolu: **No** (öneri; Aptabase notlarında kimlik yok, Unity'nin silme yolu yok). Reklam kimliği (Advertising ID) kullanılıyor mu: **No**; paketin izinlerinde `AD_ID` yok (derleme denetler, §23).
  - **Veri türleri:**

    | Kategori > tür | Toplanıyor | Paylaşılıyor | Zorunlu / isteğe bağlı | Amaçlar | Kimden |
    |---|---|---|---|---|---|
    | Location > Approximate location | Evet | Hayır | Kullanıcı seçebilir | Analytics | Aptabase, IP'den ülke ve bölge (IP saklanmaz) |
    | Financial info > Purchase history | Evet | **Evet** (öneri: Unity satış ve fatura verisinde bağımsız veri sorumlusu) | Zorunlu | App functionality; Analytics; Fraud prevention, security, and compliance | Unity'nin satın alma olayları; izinle Aptabase'in `purchase_complete` notu |
    | App activity > App interactions | Evet | Hayır | Kullanıcı seçebilir | Analytics | Aptabase'in notları |
    | App info and performance > Diagnostics | Evet | Purchase history ile aynı | Zorunlu | App functionality; Analytics | Unity olayındaki cihaz bilgisi (model, işletim sistemi, dil, disk) |
    | Device or other IDs | Evet | Purchase history ile aynı | Zorunlu | App functionality; Analytics | Unity olayındaki Unity oturum kimliği (Android'de IDFV yok) |
    | Crash logs, Other app performance data, User IDs, Email ve öteki türler | Hayır | | | | Aptabase'in hata raporu kapalı; UGS başlatılmıyor |

  - **Açık soru:** Unity kendi Data safety tablosunda "Shared: No" diyor, sözleşmesinde (2026-06-30) satış ve fatura verisi için kendini bağımsız veri sorumlusu sayıyor. Öneri "paylaşılıyor" beyanı; karar ve uzman okuması sahipte (taslak §6).
- **Yayın sırası (karar 2026-10-05):** APK testi → doğrudan dünya geneli. Önceki plandaki birkaç küçük ülkede sınırlı yayın adımı kaldırıldı. Sonucu: limitleri gerçek oyuncu verisiyle ayarlama fırsatı yayından önce yalnızca web testi ve APK testidir; yayından sonraki ayar düzeltme paketleriyle yapılır (aşağıda). Bu yüzden yayın öncesi kalibrasyon bilerek yumuşak tutulur (§8 madde 7 ve 8). Yayında 1000 bölümün tamamı bulunur.
- **Düzeltme paketleri:** bölümler uygulamanın içinde gelir; sunucudan küçük düzeltme paketleri inebilir (örneğin bir bölümün hamle limiti ya da seed havuzu). Oyun internetsiz de oynanır. Bölüm dosyasındaki `formatVersion` dosya biçimini sürümler; bölümün içeriğini `contentVersion` sürümler.
- **İçerik sürümü ve paket (uygulandı, 2026-10-12):**
  - Yayınlanan her bölüm 1. içerik sürümündedir. 1. sürüm dosyaya yazılmaz: dokunulmamış bölümün dosyası ve parmak izi sürümlerden önceki hâliyle aynıdır, eski kayıtlar ve banka kayıtları bölümlerini tanımaya devam eder.
  - Paket, düzeltilen bölümlerin **tamamını** taşır (bölüm başına yaklaşık 0,7 KB; alan alan yama değil). Böylece paketteki her bölüm, uygulamadaki bölümlerle aynı denetimden geçer ve tahta düzeltmesi de limit düzeltmesi de aynı yoldan gider.
  - **Tek paket her uygulama sürümüne hizmet eder:** paket, bugüne kadar 1. sürümün üstüne çıkmış her bölümü en son sürümüyle içerir. Cihaz paketi uygulamanın içindeki bölümlerin üstüne serer: paketteki sürüm daha yüksekse o oynanır, değilse uygulamadaki (yeni uygulama sürümü düzeltmeyi zaten içerir). Eski ve yeni uygulama aynı paketi okur; en son paket tek başına yeter. Paket bölüm eklemez; uygulamada olmayan numara atlanır.
  - **Paketin sıra numarası** vardır (`sequence`, 1'den başlar, her yayınlanan pakette bir artar). Cihaz gördüğü en yüksek numaralı paketi tutar; geç gelen eski bir kopya hiçbir bölümü geri götürmez. Sıra numarası olmayan dosya paket sayılmaz.
  - **Bir sürüm numarası bir içeriği sonsuza dek adlandırır.** En son yayınlanan içeriğinden farklı olan bölüm bir sonraki sürümü alır; yayındaki içeriğe geri döndürülen bölüm de. Yayınlanmış düzeltme, yenisi gelene kadar sonraki her pakette kalır (ondan önceki uygulamaların ona ihtiyacı var).
  - Neyin yayınlandığı iki yerden okunur: önceki paket ve yayın klasöründe 1. sürümün üstünde duran bölümler. İkisi aynı sürüm için farklı içerik gösterirse paket üretilmez. `patch make` önceki paketi ister (`--previous`) ya da hiç paket yayınlanmadığının açıkça söylenmesini (`--first`).
  - **Yeni uygulama sürümünün bölüm klasörü** `patch apply` ile üretilir (önceki yayın klasörü + son paket): dosyalar sürümlerini taşır. Çalışma klasörü `levels/generated` hiç sürüm taşımaz.
  - Deneme kaydı oynanan içerik sürümünü taşır (`contentVersion`; 1. sürümde yazılmaz). Yayınlanan oyunun kayıtlarında bölüm parmak izi yoktur: bir bölümün düzeltmeden önceki ve sonraki denemelerini bu sürüm ayırır; özet ve insan–bot karşılaştırması kayıtları buna göre gruplar ve eşler.
  - Komutlar: `puzzle patch make --shipped <yayın klasörü> --current levels/generated (--previous <önceki paket> | --first) --out pack.json` ve `puzzle patch apply pack.json --shipped <yayın klasörü> --out <klasör>`.
  - Yapılmadı (istemci ve sunucu işi): paketin sunucudan indirilmesi, cihazda saklanması, oyun açılırken uygulanması; ilk yayında `levels/generated` klasörünün "yayın klasörü" olarak dondurulması ve Unity'nin bölüm paketinin o klasörden yapılması.

## 21. Motorun "hazır" tanımı ve iş sırası

**Karar (2026-10-02).** Motor aşağıdaki liste tamamlanınca "hazır" sayılır ve tema fazına geçilir. v2 engelleri bu tanıma girmez; yayından sonra güncellemelerle gelir.

| # | İş | Durum |
|---|---|---|
| 1 | Kararları bu belgeye ve geliştirme günlüğüne yaz | ✅ |
| 2 | Botların özel taş biriktirme sorununu düzelt; usta botu yaz | ✅ 2026-10-03 |
| 3 | Üreticiye casual korumasını, raporlara limit+5 sütununu ekle; mekanik takvimini tablo başlangıçlarına hizala | ✅ 2026-10-03 |
| 4 | Motora booster'ları, bitiş şölenini, bölüm içerik sürümünü ve düzeltme paketini ekle | ✅ 2026-10-12 (§18, §19, §20; "+5 hamle" teklifi de motorda) |
| 5 | Web test uygulamasına oyun kaydını ekle | ✅ 2026-10-03 (kayıt, insan–bot karşılaştırması, yenilenen arayüz) |
| 6 | 1000 bölümü yeniden üret, raporları çıkar. Motor "hazır" | Bölümler yeniden üretildi ✅ 2026-10-04 (herkese açık test bu bölümlerle yapılsın diye öne alındı); 51–1000 bankadan derlendi ✅ 2026-10-12 (§24). **Motor "hazır": 2026-10-12** |

Sıranın gerekçesi: yeniden üretim (6) bot düzeltmesine, casual korumasına ve takvime bağlı; bölümler iki kez üretilmesin diye onlar önce gelir.

## 22. Görsel tasarım — sanat yönü ve çizim planı

**Karar (2026-10-05).** Sanat yönü, kullanıcının Claude Design'da çizdirdiği ve beğendiği tuvaldir (ana ekran ve 47. bölüm tahtası). Bu oturumda denenen diğer yönler rafa kalktı. Görseller yol haritasında hâlâ en sonda; aşağıdaki işler çizim dosyaları üzerindedir, oyun koduna girmez. Çizimlerin kaynağı `out/design/next/` altında (taş parçaları `sym/final`, üreteç betikleri `tools`); depoya girmiyor.

**Dil**
- Camgöbeği damask galeri duvarı, pirinç aplikler, tele asılı krem plaketler, basamaklı ve boncuklu yaldız çerçeve içinde açık camgöbeği dokulu tepsi, altta ahşap booster rafı.
- Yazı tipleri: Gabarito (arayüz), Paytone One (büyük düğmeler), Young Serif (oda ve tablo adları).
- **Taş tarifi:** her taş tek, sade ve iri bir siluettir; hepsi aynı ışığı paylaşır (sol üstten ışık, alt kenarda kalınlık, içte hafif kabartma, iki kenar parıltısı, tek beyaz yansıma). Ayrıntılı nesne çizimi bu dile yabancı kalır.

**Renk kararı (2026-10-07).** Kullanıcı telefonda oynadıktan sonra taşları daha canlı ve uyumlu istedi; altı panelli karşılaştırmadan doygun taşlar ve **koyu camgöbeği tepsi** seçildi (açık camgöbeği tepsi bırakıldı: yeşil, mavi ve elmas ona karışıyordu). Kural tek yerde: `tools/web-theme/gem-palette.js` (OKLCH'de doygunluk 1,3 katı, açıklık artmaz, yeşil 140° ve elmas 220° yönüne kayar, beyazlar değişmez). Tepsi #124652 / #0f3e49 dama. Web testi ve Unity aynı derlemeden beslenir.

**Taş seti**

| Motordaki | Görsel | Durum |
|---|---|---|
| Kırmızı | Uğur böceği (kullanıcı isteği, 2026-10-05; girdaplı diskin yerine) | Çizildi 2026-10-05 |
| Mavi | Damla | Var |
| Yeşil | Yuvarlak köşeli kare | Var |
| Sarı | Papatya (kullanıcı isteği, 2026-10-05; sarı çiçeğin yerine). **Beyaz yapraklı, altın sarısı göbekli** (karar 2026-10-05; ilk çizim açık sarı yapraklıydı) | Çizildi 2026-10-05 |
| Mor | Koni | Var |
| Turuncu | Hilal | Var |
| Camgöbeği (7. renk, 301. bölümden sonra) | Elmas | Çizildi 2026-10-05 |
| Pembe (8. renk, 601. bölümden sonra) | Lale | Çizildi 2026-10-05 |
| Roket (yatay / dikey) | Fırça kovanı | Var |
| Bomba / Büyük Bomba | Mürekkep sıçraması / yaldız kuşaklı büyük sıçrama | Var / çizildi |
| Pervane | Kelebek | Var |
| Renk Bombası | Gökkuşağı paleti (altı boya damlalı sedef kap) | Çizildi |
| Jöle (1 / 2 kat) | Eski vernik / kalın (koyu) vernik | Var / çizildi |
| Kutu (1 / 2 / 3 can) | Çatlak sandık / nakliye sandığı / demirli sandık | Çizildi |
| Zincir (1 / 2 can) | Koruma bandı / çift bant | Çizildi |
| Malzeme | Kayıp çerçeve köşesi | Çizildi |

**Çizim planı**

| # | İş | Durum |
|---|---|---|
| 1 | Uğur böceği ve papatya taşları; eksik engeller, özel taşlar ve hedef simgeleri | ✅ 2026-10-05 |
| 2 | Oyunun gerçek genişliğinde (10 sütun, 36 px hücre) Türkçe tahta ekranı; taş ve engel föyü | ✅ 2026-10-05 (tuvalde iki yeni çizim) |
| 3 | Türkçe ana ekran | ✅ 2026-10-05 |
| 4 | Yapboz yerleştirme ekranı (3-4-3 kesim; tepsideki parça yerinin gerçek kesimiyle çizili) | ✅ 2026-10-05 |
| 5 | Bölüm başı (hedefler, bölüm öncesi booster'lar), kazanma (bitiş şöleni, parça ödülü), kaybetme ("+5 hamle" teklifi) | ✅ 2026-10-05 |
| 6 | Müze haritası (kat planı), koleksiyon (salon duvarı), mağaza ve ayarlar | ✅ 2026-10-05 (fiyatlar `[fiyat]`; altın paket miktarları, set içerikleri ve oda planı örnek) |
| 7 | Herkese açık web testindeki oyuncu sayfasına bu görünümün uygulanması | ✅ 2026-10-06, yayında; tasarımın yazı tipleriyle (Google Fonts üzerinden) |
| 8 | Unity'ye aktarım (SVG'den sprite) | Sürüyor: yazı tipleri, 51 yeni çizim ve ortak parçalar ✅ 2026-10-13 (§23); ekranlar sırada. 2026-10-14: altın ve can satırı, bölüm başı kartı, tahta plaketleri, raf ve kazanma kartı ✅ (arayüz planının 7–9. aşamaları); kalan ekranlar 10–15. aşamalarda |

**Avatar: ressamın çizimi (2026-10-15).** Avatar hikâyenin genç ressamıdır. Çizimini proje sahibi yapacak; ajanın çizdiği dört taslak reddedildi. Oyunun istediği çizimler:

| Çizim | Nerede görünür | Boyut ve biçim |
|---|---|---|
| Ayakta tam boy, el sallayan ya da paletli | Salonda tezgâhın yanında (telefonda yaklaşık 450 piksel boyunda) | Saydam arka planlı PNG ya da SVG; en az 900×2100 piksel |
| Büst (baş ve omuzlar), üç ifade: yol gösteren, düşünen, sevinen | Rehber ve booster tanıtım yazılarının yanında, sonuç kartlarında (telefonda yaklaşık 100 piksel) | Kare, saydam; en az 512×512 piksel; yüz 100 pikselde de okunmalı |
| Sevinen tam boy (isteğe bağlı) | Oda onarılınca ve tablo tamamlanınca | Tam boyla aynı |

- **Renk:** duvar koyu camgöbeği. Giysinin ağırlıklı rengi camgöbeği ya da yeşil olmamalı; sıcak renkler (hardal, krem, mercan) öne çıkar.
- **Işık ve çizgi:** taşlarla aynı ışık yönü (sol üst) ve sıcak koyu kahve dış çizgi uyumlu durur; şart değil.
- **Yer:** tasarım tuvalindeki "Hediye" çerçevesi salonda figürün duracağı yerde asılı. Çizim gelince ikisinin yeri birlikte ayarlanacak. **Ayarlandı (2026-10-24):** ressam tezgâhın sağında duruyor, Hediye çerçevesi tezgâhın soluna, aynı yüksekliğe alındı.

**Güncelleme (2026-10-24): avatar seçildi ve oyunda.** Proje sahibi üçüncü turun **A** adayını seçti: keçe bereli, hardal gömlekli, kanvas önlüklü yaşlı usta ressam. Çizim değil, Blender'de pozlanıp işlenmiş resimler: başı Blender Studio'nun "Charge" filmindeki Einar (CC BY 4.0, değiştirildi), gövdesi Microsoft Rocketbox (MIT); dokular Poly Haven (CC0). Hiçbir yüz elle çizilmedi ya da yontulmadı; ifadeler modelin kendi yüz kontrolleriyle. Kaynaklar ve lisans kaydı `art/avatar/` (`licences.json`, `credits.md`), oyundaki kopyalar `unity/Assets/Art/Avatar/Resources/Painter/`.

| Resim | Oyunda nerede |
|---|---|
| Ayakta, palet ve fırçayla | Salonda tezgâhın sağında; 1080×2424 telefonda 450 piksel |
| Ayakta, el sallayan | Aynı yerde, henüz bölüm kazanmamış yeni oyuncunun salonunda |
| Ayakta, sevinen (iki kol havada) | Tablo tamamlanınca yapbozun tepsisinin sağ ucunun önünde; oda onarılınca salonda pankartın sağında |
| Büst, yol gösteren | Öğretici ve booster tanıtımı yazılarının solunda, küçük yaldız çerçevede |
| Büst, düşünen | Yeni bir engelin (sandık, vernik, bant, çerçeve köşesi) ve uzun basmanın yazısının yanında |
| Büst, sevinen | Kazanma kartının nişinde, sol üstte; ilk tablo tamamlanınca yazının yanında |

Bilinen üslup farkı: salon düz renkli çizim, ressam gerçekçi 3B işleme. Telefondaki boyutta resmin içindeki yumuşak gölge onu zemine bağlıyor; sıcak koyu kahve bir dış çizgi ileride denenebilir. Kaybetme kartı için üzgün ya da şaşıran büst ve bekleme canlandırması (nefes, göz kırpma) yapılmadı.

Çizimlerdeki yer tutucular (karar değil): "+5 hamle" fiyatı `[fiyat]` olarak duruyor; coin ödülü (+45), booster adetleri ve "Güçlendirici seç" gibi başlıklar örnek metin. Yıldız puanı yok (oyunda böyle bir sistem tanımlı değil); bölüm ödülü yapboz parçası ve coin.

Açık nokta: 10 sütunlu tahtada hücre telefonda yaklaşık 36 pt; dokunma hedefi için önerilen 44 pt'nin altında. Tahta genişliği arayüz fazında ayrıca konuşulacak (§2).

## 23. Unity istemcisi

**Durum (2026-10-15).** İstemcide: müzenin salonu, harita, koleksiyon, yapboz; tahta ve hamle animasyonları; bitiş şöleni; salon, harita ve koleksiyonda altın ve can satırı, can kartı; bölüm başı kartı ve bölüm öncesi booster'lar (karttan satın alma dahil); tahta plaketleri, duraklatmalı raf, çıkma kartı ve kazanma kartı; cüzdanı da tutan kayıt dosyası (katman 3); telefonda deneme kaydı ve cihaz kimliği. Müzenin 20 odası ve 100 tablosu var. Yok: bölüm içi booster'lar (10. aşama, sıradaki), "+5 hamle" teklifi ve yeni kaybetme kartı (11), mağaza ve ödüllerin gösterilmesi (12), ayarlar (13), ses ve titreşim (14), hesap. Bu işler 15 aşamalı arayüz planının parçasıdır (aşağıda; 1–9. aşamalar bitti). Telefonda en son 2026-10-07'de oynandı, son APK 2026-10-08'de derlendi; sonraki işler yalnızca Editor'da ve .NET'te denendi (`docs/PHONE_TESTS.md`).

*Önceki durum (2026-10-08):* Android'de çalışan sürümde müzenin salonu, harita, koleksiyon, yapboz, tahta, hamle animasyonları, sonuç kartı, bölümden çıkış, ilerlemenin kaydı vardı; ilk odanın beş tablosu; mağaza, altın, can, booster ve hesap yoktu.

**Kararlar**
- **Unity 6.6 (6000.6.4f1), URP 2B.** Unity Personal lisansı. LTS olmayan "Supported" koluyla başlandı, çünkü bilgisayarın diski iki Editor'ı kaldırmıyor.
- **Motor derlenmiş girer.** `Puzzle.Core` ve `Puzzle.Bot` derlemeleri ile 1000 bölümün tek dosyalık paketi `node tools/unity/sync-engine.js` ile projeye konur (`unity/Assets/Generated`, depoya girmez). Oyun, bölümlerin ayarlandığı kodun aynısını çalıştırır.
- **Çalışma ortamları arası eşitlik sabitlenmiştir.** El yapımı dört örnek bölüm üçer tohumla sonuna kadar oynatılır; araçlardaki .NET ile Unity aynı satırları vermek zorundadır (`tests/fixtures/engine-trace.txt`, `EngineTraceTests`, `EngineSmokeTest`).
- **Editor penceresi kullanılmaz.** Sahne, çizim hattı ayarları, atlas ve varlık kataloğu Editor betikleriyle komut satırından üretilir (`ProjectSetup.Run`). Sahne dosyası tek nesnedir (`GameRoot`); görünen her şey çalışırken koddan kurulur.
- **Açık saat.** Sahnenin bütün animasyonları `GameStage.Advance(saniye)` ile ilerler; oyun buna kare süresini verir. Aynı saat sayesinde Editor, oynatma moduna girmeden herhangi bir anın görüntüsünü çıkarabilir (`RenderHarness`, `Storyboard`).
- **Ekran motoru izler.** Hamle önce motorda oynanır; olay günlüğü zamanlı bir senaryoya çevrilir (`TurnChoreographer`); senaryo bitince ekrandaki tahta motorun tahtasıyla karşılaştırılır. Uyuşmazlıkta tahta motorun durumuna döndürülür.
- **Saf kod testlenir.** Düzen hesabı, çerçeve hattı, zaman çizelgesi, hamle senaryosu, dokunma yorumu ve akış Unity'ye bağlı değildir; depo testleri bu dosyaları doğrudan derler.
- **Deneme tohumu web testiyle aynı kuraldır:** k. deneme bölümün ayarlanmış havuzundaki k. tohumu kullanır; sayaç yalnızca oyuncunun bulunduğu bölüm için tutulur ve kazanınca sıfırlanır.

**Komutlar** (depo kökünden; Unity yolu `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe`)

| İş | Komut |
|---|---|
| Motoru ve bölümleri projeye al | `node tools/unity/sync-engine.js` |
| Projeyi kur (çizim hattı, atlas, katalog, sahne) | `Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.ProjectSetup.Run` |
| Motor denetimi | `... -executeMethod Puzzle.Client.EditorTools.EngineSmokeTest.Run` |
| Ekonomi ayarını paketle ve sabitle | `node tools/unity/economy-pack.js --pin` (ayar dosyası değişince; sonra `sync-engine.js`) |
| Ekonomi denetimi | `... -executeMethod Puzzle.Client.EditorTools.EconomyCheck.Run` (Unity paketi okuyabiliyor mu, kurallar ondan kuruluyor mu) |
| Görüntü al | `... -executeMethod Puzzle.Client.EditorTools.RenderHarness.Run -shotLevels 40 -shotSizes 1080x2340 -shotTimes settled` |
| Botla oynat, kareleri çıkar | `... -executeMethod Puzzle.Client.EditorTools.Storyboard.Run -storyLevels 40 -storyBot expert -storyTurns 6` |
| Müze ekranlarının görüntüsü | `... -executeMethod Puzzle.Client.EditorTools.ScreenShots.Run` (salon, harita, koleksiyon, yapboz; oyuncu durumu ve ekran boyu seçilir) |
| Yan tutulan ekranların görüntüsü | `... -executeMethod Puzzle.Client.EditorTools.ScreenShots.Run -screenSizes 2732x2048+0+40,2560x1600,2152x2076+0+0+90+0,2152x2076+0+0+0+90` (12,9" iPad, 16:10 tablet, Fold iki yana: kamera solda ve sağda) |
| Döndürme denetimi | `... -executeMethod Puzzle.Client.EditorTools.RotationCheck.Run` (iPad dik↔yan, Fold iç ekran iki yana, Fold dıştan içe; görüntüler `out/unity/shots/rotation`) |
| Satın alma denetimi | `... -executeMethod Puzzle.Client.EditorTools.PurchaseCheck.Run` (`-quit` olmadan; betikli mağazayla yürüyüş ve oyun kipinde sahte mağaza; `-purchaseCheckBreaksTheSave` ile düşmeli) |
| Baştan sona bir yürüyüş | `... -executeMethod Puzzle.Client.EditorTools.GameWalk.Run` (bölüm kazan, parçayı gör, yerleştir, salona dön) |
| Müzenin durumu ve kesimler | `... -executeMethod Puzzle.Client.EditorTools.MuseumReport.Run` |
| Bitiş şöleninin kareleri | `... -executeMethod Puzzle.Client.EditorTools.FinaleStory.Run -finaleLevels 40 -finaleLeft 3,12,30 -finaleSize 1080x2340` (kalan hamle sayısı, ekran boyu, atlama anı seçilir; görüntüler `out/unity/shots/finale`) |
| Bitiş şöleninin denetimi | `... -executeMethod Puzzle.Client.EditorTools.FinaleCheck.Run` (sonuna kadar izleme, dokunuşla ve geri tuşuyla atlama, şölensiz kazanma ve kaybetme, hata durumunda kartın yine gelmesi) |
| Koleksiyonda bellek denetimi | `... -executeMethod Puzzle.Client.EditorTools.CollectionMemoryCheck.Run` (20 oda beş kez gezilir; bellekte aynı anda 25'ten fazla resim olmaz, gezintiden gezintiye hiçbir şey artmaz; sayılar `out/unity/collection-memory.json`) |
| Yazı örnek föyü | `... -executeMethod Puzzle.Client.EditorTools.LetteringSheet.Run` (on bir yazı stili kendi zemininde, Türkçe harflerle; `out/unity/shots/lettering-sheet-2x.png`) |
| Çizim föyü | `... -executeMethod Puzzle.Client.EditorTools.ArtSheet.Run` (kataloğun bütün çizimleri duvar, krem ve ahşap üstünde; `art-sheet-2x.png`) |
| Ortak parçaların föyü | `... -executeMethod Puzzle.Client.EditorTools.ScreenShots.Run -screens controls -screenSizes 1080x2340` (düğmeler ve diskler; duvar, krem kart ve ahşap üstünde) |
| Tasarımla yan yana | `... -executeMethod Puzzle.Client.EditorTools.DesignPairs.Run` (yedi ekran, solda tasarım çizimi, sağda oyun; `pair-<ekran>-1080x2340.png`) |
| Yazı tiplerini indir | `node tools/unity/fetch-fonts.js` (kaynaklar ve lisans metinleri `art/fonts/licences.json`) |
| Tasarım tuvalinden simge çıkar | `node tools/unity/extract-design-symbols.js` (tuvaldeki simgeleri tek başına SVG olarak `art/unity` altına yazar) |
| Tabloları indir | `node tools/unity/fetch-paintings.js` (yeni oda eklemek için betikteki listeye yazılır) |
| Önbellekleri başka sürücüye taşı | `.\tools\unity\Move-CachesToDrive.ps1 -Drive D` |
| APK derle | `Unity.exe -batchmode -quit -projectPath unity -buildTarget Android -executeMethod Puzzle.Client.EditorTools.AndroidBuild.Run` → `unity/Build/Android/puzzle.apk` |

**Müze tarafı (2026-10-08).** Kurallar `src/Puzzle.Museum` projesinde, Unity'ye bağlı değil ve testli; `sync-engine.js` bu derlemeyi ve kataloğu da projeye koyar. Ekranlar tahtayla aynı ilkeleri izler: açık saat, her şey oyun kamerasında, komut satırından görüntü. Salon, harita ve koleksiyon `MuseumStage`'in sayfalarıdır; yapboz ve tahta kendi sahneleridir (`JigsawStage`, `GameStage`); üçü de `IStage` sözleşmesini uygular. İlerleme dosyası sürümlüdür: eski dosya okunur, kazanılmış bölümlerin parçaları bekler.

**Bitiş şöleni (2026-10-13).** Kazandıran hamle sonuna kadar gösterilip tahta durduktan sonra, hamle kaldıysa şölen oynar (§19); kaybedilen oyunda ve son hamlede kazanılan bölümde şölen yoktur.

- **Oyuncunun gördüğü:** HAMLE madalyonunda ışık açılır; kalan her hamle için madalyondan bir yaldız kıvılcımı çıkıp bir taşa uçar, sayı birer birer düşer. Kıvılcımın konduğu taş fırça kovanına döner; kovanlar kısa bir an durur, sırayla patlar; tahta çöker ve dolar; sonra sonuç kartı bugünkü gibi gelir. Hedef sayaçları kıpırdamaz. Coin gösterilmez (ekonomi kararı bekliyor). (Güncelleme: kalan hamle başına 4 altın 2026-10-13'te kararlaştırıldı, §25; ödenen altın 2026-10-14'ten beri şölenden sonra kazanma kartında sayarak gösterilir, aşağıda arayüz planının 9. aşaması.)
- **Süre:** 1 hamle için yaklaşık 1,3–1,7 saniye, 3 hamle için 2–3, 5 hamle için 2,5–3,5, 12 hamle için 4–4,6, 30 hamle için yaklaşık 4 saniye; kaç hamle kalırsa kalsın 5,5 saniyeyi geçmez (uzun şölenin dolma kısmı hızlandırılır, motorun yaptığı hiçbir şey atlanmaz). Tahtada taş hamleden azsa şölen dalga dalga sürer.
- **Atlama:** ekrana dokunmak (ikinci bir parmak dahil) ya da geri tuşu şöleni o anda bitirir; tahta motorun son hâlini gösterir ve kart gelir. Şölen sürerken raf yazı göstermez.
- **Kazanım güvende:** ilerleme kazandıran hamle oynandığı anda kaydedilir; şölen izlenmese ya da uygulama o sırada kapansa da kazanım durur.
- **Nasıl kurulu:** motor şöleni tek seferde oynar (`Game.PlayFinale`); olayları `TurnChoreographer.ComposeFinale` ile zamanlı bir senaryoya çevrilir ve hamlelerle aynı zaman çizelgesinde oynatılır. Motorun bir dalgası, renk bombasının taşları rokete çevirmesiyle aynı olay biçimindedir; o yol yeniden kullanıldı. Şölen bitince ekran yine motorla karşılaştırılır; gösterimde hata olursa tahta motorun durumuna döner ve kart yine gelir.
- **Doğrulama:** senaryo için düz C# testleri (gerçek bölümlerin gerçek motor olaylarıyla), Unity'de `FinaleCheck` ve `FinaleStory`; üç ekran boyunda (1080×2340, 1080×1920, 2076×2152) karelerine bakıldı.
- **Ölçülmedi:** telefonda kare hızı ve gerçek parmakla atlama. Ses ve titreşim yok.

**Yere konma esnemesi (2026-10-13).** Yere konan bir parçanın kısa esnemesi, parça esneme bitmeden yeniden hareket ettiğinde artık o anda biter. Önceden esneme sonraki düşüşün içine taşıyordu (hamle senaryolarının yaklaşık yüzde 11'inde, şölenlerin yüzde 36'sında); art arda çıkan malzemelerde parçanın alınışını bile geçebiliyordu. Hiçbir olayın başlangıcı ya da bitişi değişmedi; yalnızca taşan esnemelerin süresi kısaldı. Çok kısa kalan esneme atılmaz, kaldığı kadar oynar.

**Arayüzün araçları: yazı, çizimler, ortak parçalar (2026-10-13).** Tasarımdaki bütün ekranların istemciye gelmesi 15 aşamalı bir plandır (`out/unity/ui-plan.json`, depoya girmez). İlk yapılan, ekranların ortak malzemesidir (planın 4–6. aşamaları); yeni ekran henüz yoktur.

- **Yazı tipleri.** Tasarımın üç yazı tipi oyundadır: Gabarito (etiketler ve arayüz), Paytone One (rakamlar ve düğme yazıları), Young Serif (başlıklar, oda ve tablo adları). Üçü de SIL Open Font License ile dağıtılır; lisans metinleri ve kaynak adresleri `art/fonts/licences.json` dosyasındadır. Roboto Bold kaldırıldı. Yazı on bir stille yazılır (`LetteringStyle`): stil yazı tipini, zemine göre rengi ve beyaz yazının koyu çevre çizgisini seçer. Young Serif'in rakamları satır boyundadır ("10. tablo", "Bölüm 51'de açılır"). Harf kümesi Türkçenin bütün harflerini ve arayüzde geçen işaretleri kapsar; küme ya da yazı tipi dosyası değişince `ProjectSetup` yazı varlığını kendiliğinden yeniden üretir.
- **Çizimler.** Tasarım tuvalinden 51 yeni çizim geldi (altın, can, sandık, mağaza ve takvim simgeleri, booster diskleri, küçük düğmeler, rozetler, ahşap raflar, pirinç levhalar): 20'si tuvalden betikle çıkarıldı, 31'i tuvalin ölçülerine göre elle yazıldı. Katalog artık 125 çizimi iki atlasta tutar. Bir çizimin dört yeri (kaynak dosya, `GalleryArt` üyesi, katalog listesi, içe aktarma ölçüleri) birbirini tutmak zorundadır; bunu Unity'siz çalışan testler denetler.
- **Ortak parçalar.** Asılı kart (`HangingCard`: her boyda gövde, pirinç isim levhası, teller, isteğe bağlı kapatma düğmesi; sonuç kartı artık bunun üstünde durur), yuvarlak düğme (`DiscButton`: geri, ileri, duraklat, kapat, ayarlar, artı; eski `RoundButton` silindi), booster diski (`BoosterDisc`: adetli, bitmiş ve artılı, seçili, kurulu), sayı rozeti (`CountBadge`), küçük düğme (`SmallButton`: yeşil ya da mavi, altın ya da oynat işaretli), fiyat haplı büyük düğme (`PushButton`), bölüm başlığı (`SectionHeader`), ilerleme sırası (`PipRow`), tek tip soluk görünüm (`Dimming`) ve sayıların yazımı (`NumberWords`: "1.250", "12:34", "1 sa 42 dk").
- **Doğrulama.** `ProjectSetup`, `EngineSmokeTest`, `FinaleCheck`, ekran yürüyüşü, üç föy ve `DesignPairs` Unity'de hatasız çalıştı; `ProjectSetup` ikinci çalıştırmada hiçbir dosyayı değiştirmiyor. Yedi ekranın tasarım çizimiyle yan yana görüntüsüne, föylere ve koleksiyonun 2. odasına bakıldı: yazılar ve var olan parçalar çizimle örtüşüyor. Çizimlerde olup oyunda henüz olmayanlar sonraki aşamaların işidir: altın ve can satırı, mağaza ve takvim sekmeleri, hediye ve etkinlik çerçeveleri, tahta rafındaki booster diskleri, kazanma kartındaki altın satırı, kaybetme kartındaki "+5 hamle" teklifi.
- **Bilinen küçük farklar.** Beyaz yazının çevre çizgisi her yanda eşit kalınlıkta; çizimlerde alt kenarı biraz daha kalın. Bölüm başlığının mürekkebi krem zemin içindir; ahşap üstünde soluk kalır (tasarımda ahşap üstünde başlık yok).

**Kurallar istemcide: can, coin, booster (2026-10-14).** Arayüz planının 1. aşaması. Ekranda görünen bir şey değişmedi; ekranların dayanacağı kurallar yazıldı.

- **`src/Puzzle.Meta`** (düz C#, Unity'ye derlenmiş girer; yalnızca `Puzzle.Core`'a bağlı): cüzdan (coin, can, booster rafı, günün reklam sayıları, ödül sayaçları; değişmez, değerce eşit), can sayacı, kazanma ödülü, "+5 hamle" teklifinin koşulları (coin'le ya da günün reklam hakkıyla), bölüm öncesi booster seçimi, tablo ve gün ödülleri (n. ödül sıradaki n. booster'dır; rastgelelik yok), mağaza (tek booster, set, reklamla coin; gerçek para paketleri "satışta değil"). Her kuralın sonucu kapalı bir türdür: alındı, coin yetmiyor, satışta değil gibi.
- **Saat dışarıdan verilir.** Kurallar saati bilmez; her soruya zaman parametre olarak gelir. Geri alınmış bir saat can getirmez ve yeni gün hakkı açmaz. İleri alınmış saat canları doldurur: bilinen açık, sunucusuz bir oyunda kapanmaz.
- **Tek ayar dosyası.** Oyun fiyatları ve miktarları `economy/default.json` dosyasından okur (§25). `tools/unity/economy-pack.js` dosyanın oyuna ait bölümlerini paketler (`unity/Assets/Generated/Resources/Economy.json`); paketin sabitlenmiş bir kopyası testlerdedir (`tests/fixtures/economy-pack.json`). **Ayar dosyası her değiştiğinde:** `node tools/unity/economy-pack.js --pin`, ardından `node tools/unity/sync-engine.js`; yoksa betiğin testi, `EconomyFileTests` ve `EconomyCheck` kırmızıya döner.
- **Yeni alanlar.** Yeni oyuncunun hoş geldin hediyesi: 200 coin ve her booster'dan 3. Kullanıcının kararıdır (2026-10-14) ve simüle edilir (§25). Oyuna özel alanları simülatör yalnızca biçim olarak denetler, simüle etmez:
  - bir denemeye alınabilecek bölüm öncesi booster sayısı: 3;
  - mağazada coin paketleri: 500 / 1.200 / 2.500, fiyatsız;
  - booster setleri: `starter` 900, `master` 6.300 coin; ikisi de içindekilerin tek tek fiyatlarının toplamı;
  - reklamla coin: 4 coin (kullanıcı kararı), günde 3 kez;
  - derlemenin işaretleri: reklam "canlandırma", gerçek para "yok".
- **Simülatörle aynı cevap.** Simülatörün yürüttüğü oyuncu yolculukları oyunun kurallarından geçirilir; can, coin ve booster her adımda aynı çıkmak zorundadır (`MetaAgainstTheSimulatorTests`). Yeni alanlar simülasyonun tek bir sayısını değiştirmedi.
- **Doğrulama:** 3.160 test (99'u yeni), paket betiğinin 8 testi; Unity'de `ProjectSetup`, `EngineSmokeTest`, `EconomyCheck` (Unity'nin okuyucusu paketi yazıldığı gibi okuyor ve oyun kuralları ondan kuruyor) ve ekran yürüyüşü hatasız.
- **Kanıtlanmadı:** telefonda ve IL2CPP derlemesinde çalışma; bölüm içi booster kullanımı ve aynı denemede iki bölüm öncesi booster simülatör karşılaştırmasında yok.
**Cüzdan kayıt dosyasında (2026-10-14).** Arayüz planının 2. aşaması. Ekranda yine bir şey değişmedi.

- **Kayıt dosyası katman 3:** ilerlemeyle birlikte cüzdanı da yazar: altın, eksik can sayısı ve sonraki canın anı, booster'lar, günün reklam sayıları, ödül sayaçları.
- **Eski dosyalar:** katman 1 ve 2 dosyaları kuralların başlangıç cüzdanıyla okunur, yani hoş geldin hediyesi ve dolu canla. Cüzdanın dosyada olup olmadığına katman numarası karar verir, asla bir sıfır değil.
- **Bozuk ve yeni dosyalar:** cüzdanı olmayan ya da olamayacak bir cüzdanı olan katman 3 dosyası bozuk sayılır. Daha yeni bir katman anlaşıldığı kadar okunur, üzerine yazılmaz.
- **Denemenin hesabı (`AttemptLedger`):** tahta sahnesi denemenin olaylarını bildirir, kurallar her birini işler:
  - ilk hamle denemeyi saydırır;
  - kazandıran hamle oyuncuyu ilerletir ve altınını aynı kayıtta öder, şölen sırasında kapanan oyun hiçbirini kaybetmez;
  - kesin kayıp ya da hamleden sonra bölümden çıkmak bir can götürür;
  - hamlesiz çıkmak ve bölüm ortasında oyunu kapatmak ücretsizdir.
- **Saat:** dışarıdan, tam saniyeyle gelir. Canlar tam dakikalarla döndüğü için sonraki canın anı dosyadan birebir okunur.
- **Henüz yok:** can denetimi (canı biten oyuncu yine başlayabilir; 7–8. aşamalar), cüzdanın ekranda görünmesi.
- **Doğrulama:** 3.194 test. Unity'de `ProgressFileCheck` (gerçek bir katman 2 dosyası hoş geldin hediyesi ve dolu canla okunuyor, katman 3 olarak yazılıp yeniden başlatmadan sonra aynı okunuyor), ekran yürüyüşü, kazanma ve kaybetme akışları üç ekran boyunda.
- **Telefon testleri:** `docs/PHONE_TESTS.md`, T23–T28 (kayıt dosyası `adb` ile okunarak).

**Denemenin sıralı kaydı (2026-10-14).** Arayüz planının 3. aşaması. Ekranda yine bir şey değişmedi.

- **Motor:** `GameOptions.RecordActions` açıkken oyuncunun yaptıklarını sırasıyla tutar. Tutulanlar: hamleler, nişanıyla booster'lar, alınan "+5", karıştırma akışından çekip reddedilen karıştırma ve "+5" istekleri. Kapalıyken hiçbir şey tutulmaz ve oyun eskisi gibi oynar: motor izi aynı; ekonomi simülatörünün denetimi 48 saklı oyunu bu derlemeyle yeniden oynayıp kabul etti.
- **Kayıt ve yeniden oynatma:** deneme kaydının son alanı `actions` isteğe bağlıdır; şema 1 kalır, eski satırlar okunur. `AttemptReplay` bir denemeyi kaydından yeniden oynatır, her eylemi ve sonu denetler. §18'deki "booster'lı deneme yeniden oynatılamaz" sınırı böylece kapandı.
- **Telefonda:** biten her deneme (kazanılan, kesin kaybedilen ya da hamleden sonra bırakılan) için bir satır `attempts.jsonl` dosyasına eklenir. Araçların okuduğu satırın aynısıdır. Yazılamayan satır bekletilir ve yeniden denenir (en çok 64). `device.json` rastgele 128 bitlik bir cihaz kimliği tutar. Teklif açıkken kayıt yazılmaz.
- **Bilinen:** ekranlar henüz booster ya da "+5" sunmadığı için oyunun yazdığı kayıtlarda yalnızca hamle var. (Güncelleme: 8. aşamadan beri bölüm öncesi booster'lar da kayda girer; bölüm içi booster'lar ve "+5" 10. ve 11. aşamalarda gelecek.) Bölüm ortasında kapatılan oyun kayıt bırakmaz. Dosya sınırsız büyür (deneme başına yaklaşık 1,1 KB) ve henüz "Kayıtları sil" yok.
- **Sonraki aşamalara notlar:**
  - Karıştırma booster'ı eşleşme içeren her dizilimi reddediyor; büyük tahtalarda oyuncu sık sık "reddedildi" görebilir.
  - Bugün deneme ancak bir hamleden sonra sayılıyor; yalnızca booster kullanıp çıkmak kayıt bırakmıyor ve can götürmüyor. Bu karar 10. aşamada verilecek.
- **Doğrulama:** 3.250 test. Unity'de `AttemptLogCheck`: kazanılan, kaybedilen, bırakılan ve booster'lı denemeler yazıldı, Unity'nin okuyucusuyla okundu ve aynı sona yeniden oynatıldı. Ben de çalıştırdım: 5 kayıt, geçti.

**Müze sayfalarında cüzdan ve canlar (2026-10-14).** Arayüz planının 7. aşaması; planın görünür ilk aşaması.

- **Cüzdan satırı:** salon, harita ve koleksiyonda, tasarım çizimindeki yerinde, korniş ile plaket arasında durur:
  - solda pulunun üstünde altın ("200", "1.250", "1.234.567"; uzun sayı hapı genişletir);
  - yanında üzerinde can sayısı yazan kalp ve "Dolu" ya da bir sonraki cana kalan süreyi ("12:34") gösteren hap.

  Sayfalar değişirken satır yerinde kalır. Canlar gerçek saatle döner. Tahtada kazanılan altın, salona dönülünce eski tutardan yenisine sayarak artar.
- **Can kartı:** tasarımda çizimi yok; ekrana koyduğumuz yeni bir parça. Kalbin hapına dokununca, can bitmişken de "Bölüm N"ye basınca açılır; can bitmişse bölüm açılmaz. Kartta "n / 5", sonraki can, canların tamamen dolacağı süre ve "Tamam" düğmesi var.
- **Salon:** "Tablo" düğmesinin altında çizimdeki küçük ilerleme noktaları var; bekleyen parça ışıltılarla tezgâha yaslanıyor. Satırın yüksekliği tezgâhtan alındı; tablo eski boyunda kaldı.
- **Sonraki aşamalara kalanlar:** artı düğmeleri ve ayarlar dişlisi (yerleri ayrıldı), etkinlik ve hediye çerçeveleri, mağaza ve takvim sekmeleri, koleksiyonun oda şeritleri.
- **Oda adları (düzeltme):** koleksiyon plaketi bütün oda adlarını en uzun adın sığdığı boyda yazıyordu; dönem adlarıyla bu boy yaklaşık 6 punto oldu. Artık her ad kendi boyunda yazılıyor (en çok 13,5 punto). Tek satırda 11 puntodan küçük kalacak bir ad, iki dengeli satıra bölünüp en çok 10 puntoyla yazılıyor.
- **Doğrulama:** 3.276 test. Unity'de bütün denetimler ve ekran yürüyüşü (canı bitmiş oyuncu için bir adım eklendi) hatasız. 96 sayfa görüntüsüne, tasarım çizimiyle yan yana karşılaştırmalara ve oda adı düzeltmesinde altı odaya bakıldı.
- **Telefon testleri:** `docs/PHONE_TESTS.md`, T90–T97.

**Bölüm başı kartı ve bölüm öncesi booster'lar (2026-10-14).** Arayüz planının 8. aşaması.

- **Kart:** "Bölüm N" düğmesi tasarımdaki kartı indirir: bölüm, verdiği parça ("ÖDÜL · PARÇA k / m"), bir ile üç hedef, hamle madalyonu ve üç bölüm öncesi booster. Sahip olunan booster'a dokunmak onu seçer ya da geri bırakır (en çok 3, ayar dosyasındaki sınır); "+3 hamle" madalyona eklenir.
- **Satın alma kartı:** hiç kalmamış booster artıyla görünür. Artıya dokununca önde ikinci bir kart açılır ve booster'ı tek fiyatına satar; altın yetmezse düğme soluk ve "Altın yetmiyor" der. Satın alma kurallardan geçer, hemen kaydedilir, altın satırı geriye sayar. Bu kartın tasarımda çizimi yok.
- **Tek kapı:** "Oyna" tahtayı seçilen booster'larla açar; özel taşlar öteki taşlardan sonra altın bir ışıkla belirir. Kayıptan sonra "Tekrar dene" aynı bölümün kartına götürür; can yoksa can kartı gelir. Kazanınca "Devam" salona döner.
- **Harcama:** seçilen booster'lar ancak ilk hamlede harcanır. Kartı kapatmak ya da hamle yapmadan çıkmak hiçbir şey götürmez. Deneme kaydı booster'ları adıyla tutar; kayıt aynı sona yeniden oynatılır (gerçek bölümlerle test edildi).
- **Değişmeyenler:** motor, kurallar ve ekonomi ayarları.
- **Doğrulama:** 3.322 test. Unity'de bütün denetimler, kartın içinden geçen ekran yürüyüşü, yeterli ve yetersiz altınla satın alma, tasarımla yan yana karşılaştırma (kart çizime çok yakın).
- **Telefon testleri:** `docs/PHONE_TESTS.md`, T100–T109.

**Tahta plaketleri, raf ve kazanma kartı (2026-10-14).** Arayüz planının 9. aşaması.

- **Plaketler:** parça veren bölümde sol plaket hedeflerin üstünde "BÖLÜM N" der. Sağ plaket "PARÇA k / m" der; altında tablonun örtülü küçük bir kopyası durur, kazanılacak parça yaldızla aydınlanır. Parça vermeyen bölüm eski düzende kalır.
- **Raf:** tasarımdaki ahşap tahta, daha yüksek, beş yerli: duraklatma ve 10. aşamanın dört booster'ı için yer.
- **Duraklatma:** bölümden çıkma kartını açar. Kartın notu çıkmanın neye mal olacağını söyler: hamleden sonra bir can, önce hiçbir şey. Basıldığı andan sonra tahta dokunuş almaz; sürmekte olan hamle önce sonuna kadar gösterilir.
- **Kazanma kartı:** WinTR çizimindeki gibi:
  - geniş bir oyukta kazanılan parça (yeni çizim `alcove-wide`);
  - yenisi halkalı ilerleme noktaları;
  - sayarak artan "+N" altın hapı ve "Kalan N hamle fırçaya dönüştü" satırı.

  Altın, deneme hesabının ödediği kuraldan gelir; salonun altın satırı tam o kadar artar. Son hamlede kazanılan bölüm "+0" göstermez. Şölen 5,5 saniye sınırında kalır. Kart, şölen izlense de atlansa da oynatacak bir şeyi olmasa da gelir.
- **Değişmeyenler:** motor. Kaybetme ve çıkma kartlarının yeni görünümü 11. aşamada.
- **Doğrulama:** 3.349 test. Unity'de bütün denetimler, yeni durumlarıyla şölen ve dokunma denetimleri, oynatma kipi, ekran yürüyüşü ve tasarımla karşılaştırma hatasız. Tahta ve kazanma kartı çizimle neredeyse aynı.
- **Telefon testleri:** `docs/PHONE_TESTS.md`, T110–T119.

**Bölüm içi booster kararları (2026-10-15).** Kullanıcı, araştırma notunun (`docs/research/in-level-boosters.md`) üç önerisini onayladı:

1. **Karıştırma booster'ı hep karıştırır.** İlk dizilimde eşleşme varsa reddetmez; yeniden çeker ya da eşleşmesiz bir dizilim kurar. Yalnızca hiçbir karıştırmanın bir şey değiştiremeyeceği tahtada reddeder. Motorun hamle kalmayınca yaptığı karıştırma değişmez; bölümlerin ayarı ve motor izi aynı kalır. Kural sürümlenir: değişiklikten önce yazılmış deneme kayıtları da birebir yeniden oynatılır.
2. **Booster da denemeyi başlatır.** Motorun kabul ettiği bölüm içi booster ilk hamle gibi sayılır: deneme sayılır, kartta seçilen bölüm öncesi booster'lar harcanır, bundan sonra çıkmak can götürür, kayıt yazılır. Hiç dokunulmamış tahtadan çıkmak bedava kalır.
3. **Bölüm içi booster'lar birer birer açılır.**
   - Spatula 7., satır fırçası 12., sütun fırçası 16., karıştırma 20. bölümde açılır.
   - O bölüme kadar disk kilitlidir: sönük, üstünde "Bölüm N".
   - Açıldığı bölümde bir kez halkayla ve tek cümlelik bir yazıyla gösterilir.
   - Hoş geldin hediyesindeki 3'er adet bölüm içi booster açıldıkları bölümde verilir. Bölüm öncesi booster'lar baştan açık kalır, 3'er adetleri baştan verilir.
   - İlk tanıtımda "Her bölüm booster'sız geçilebilir" cümlesi yer alır; bölümler booster'sız ayarlandığı için bu ölçümle de doğru.

Bu kararlarla 10. aşama başladı.

**Bölüm içi booster'lar oyunda (2026-10-15).** Arayüz planının 10. aşaması; yukarıdaki üç kararla.

- **Kullanım:**
  - Disk tek dokunuşla seçilir; parmak altında yanar, kaydırılabilir.
  - Hap aracın adını, işini ve neyi seçmek gerektiğini söyler ("Spatula · bir taşı ya da bir katı kaldırır / Bir taş seç").
  - Hedef, satır ya da sütun boyunca soluk bir ışık bandıyla ya da taşın altında bir halkayla görünür.
  - Booster parmak kalkınca, tahta durunca uygulanır. Taşlar düşerken de seçilebilir; kullanım tahtanın oturmasını bekler.
  - Seçili booster aynı diske yeniden dokununca, geri hareketiyle, parmağı tahtadan çıkarınca ya da duraklatınca bırakılır; hiçbir şey harcanmaz.
- **Harcama:** booster yalnızca motor kabul edince harcanır. Hiçbir şey değiştirmeyecek kullanımda disk sallanır, hap nedenini söyler ("Burada vurulacak bir şey yok"), sayı aynı kalır. Kabul edilen ilk booster denemeyi başlatır.
- **Satın alma:** boş diskteki artı, booster'ı tek fiyatına satar. Satın alınan spatula ya da fırça hemen seçili gelir; karıştırma tek dokunuşla çalıştığı için seçili gelmez.
- **Karıştırma:** serbest taşlar eşleşme bırakmadan dizilir.
  - 1000 bölümde 2000 tahtada (her bölümün ilk tahtası ve üç hamle sonrası) eski kural 449 kez reddediyordu; yenisi hiç reddetmedi.
  - Motorun hamle kalmayınca yaptığı karıştırma değişmedi; motor izi aynı.
  - Yeni kayıtlar `"shuffleRule":2` taşır. Bu alanı olmayan kayıt eski kuralla yeniden oynatılır.
- **Açılış:** spatula 7., satır fırçası 12., sütun fırçası 16., karıştırma 20. bölümde açılır. O bölüme kadar disk sönüktür ve üstünde "Bölüm N" yazar. Açıldığı bölümde ışık halkası ve "Yeni: Spatula! Diske dokun, sonra bir taşa." yazısı çıkar; spatulanın altında "Her bölüm booster'sız geçilebilir" de yazar.
- **Hediye:** 3'er adet her booster açıldığında bir kez verilir.
  - Cüzdandan önceki kayıtlar (katman 1–2) her hediyeyi bir kez alır.
  - 2.–9. aşama derlemelerinin kayıtları hediyeyi zaten başta almış sayılır, iki kez verilmez.
  - Ekonomi paketi biçim 2, kayıt dosyası katman 4 oldu; ikisi de eklemeli değişiklik.
- **Simülatör:** bölüm içi hediyeyi açılış bölümünde verir. Ortalama oyuncunun sayıları değişmedi: 8., 25. ve 90. gün.
- **Bilinen:** 9:16 telefonda uzun tahtalarda ve katlanır telefonun yatay iç ekranında hap en alttaki sıranın bir kısmını örtüyor. Dokunuşlar geçiyor, ama ilk denemedeki yazı ilk hamleye kadar o taşları gizliyor. Seçili booster varken başka diske geçilemiyor; önce bırakmak gerekiyor.
- **Doğrulama:** 3.424 test. Ekonomi denetimi oyunları bu derlemeyle kabul etti. Unity'de bütün denetimler, iki yeni araç (`BoosterCheck`, `BoosterShots`), oynatma kipi, ekran yürüyüşü ve tasarımla karşılaştırma hatasız.
- **Telefon testleri:** `docs/PHONE_TESTS.md`, T120–T134.

**Hamle bitince: "+5" teklifi, reklam canlandırması, vazgeçme (2026-10-15).** Arayüz planının 11. aşaması.

- **Teklif kartı:** hedefler kalmışken hamle bitince LoseTR çizimindeki kart iner:
  - "Hamle bitti!" ve yaldızlı +5;
  - hedeflerin yüzde 80'i yapıldıysa "Az kaldı!", yapılmadıysa "Kalan hedefler", altında kalan hedefler;
  - fiyatıyla "+5 hamle" (900), "Reklam izle" ve "Vazgeç ♥ 1 can gider".

  Kart açıkken deneme bitmez: can gitmez, kayıt yazılmaz, dokunuş, duraklatma ve geri tuşu bir şey yapmaz.
- **Ödeme:** önce motora sorulur. Altınla tam fiyat düşer. Reklam seçilirse üç saniyelik, "TEST SÜRÜMÜ" yazılı bir canlandırma kartı gelir; ağa bağlanmaz, günün 3 ücretsiz reklamından birini harcar. Aynı deneme beş hamle daha sürer. Ödenemeyen yol nedeniyle sönük durur ("Altın yetmiyor", "Bugünlük bitti").
- **Ret:** hiçbir karıştırmanın canlandıramayacağı tahtada teklif reddedilir, hiçbir şey ödenmez, kart "Oynanacak hamle kalmadı!" der.
- **Vazgeç:** bir can götürür, kaydı yazar, son kaybetme kartını gösterir. Oradan "Tekrar dene" bölüm başı kartına, can yoksa can kartına götürür.
- **Kartlar:** son kaybetme kartı ve duraklatmadaki çıkma kartı tasarımdaki görünüme geçti ("–1" can hapı).
- **Adalet (§17):** teklifin fiyatı tekrarlandıkça artmaz; "+5" bir denemede bir kez alınır; ödemeden sonra kazandıracak bir yardım konmaz.
- **Bilinen:** teklif ya da reklam açıkken uygulama kapatılırsa can gitmez ve kayıt yazılmaz (planın bildiği açık). Gerçek reklam eklendiğinde "izlenmedi" sonu da gerekecek.
- **Doğrulama:** 3.443 test. Unity'de bütün denetimler, iki yeni araç (`OfferCheck`, `OfferShots`), "+5"i bir kez reklamla alıp bir kez vazgeçen ekran yürüyüşü ve tasarımla karşılaştırma hatasız. Kart çizimle birebir; fiyat artık gerçek değer.
- **Telefon testleri:** `docs/PHONE_TESTS.md`, T140–T151.

**Mağaza, artı düğmeleri, günün hediyesi ve tablo ödülü (2026-10-15).** Arayüz planının 12. aşaması.

- **Mağaza:** ShopTR çizimindeki sayfa, çubuğun en solunda. Çubukta artık dört yer var: Mağaza, Koleksiyon, Müze, Harita.
  - Lambalar arasında tabela.
  - ALTIN bölümünde üç paket. Altlarında "Yakında" yazar: bu test sürümü parayla bir şey satmaz, basınca bir kart bunu söyler.
  - GÜÇLENDİRİCİLER bölümünde iki set: Başlangıç seti 900, Usta seti 6.300 altın (içindekilerin tek tek fiyatlarının toplamı). Altın yetmezse fiyat düğmesi sönük durur; basınca eksik tutarı söyler.
  - ÜCRETSİZ şeridinde "İzle" reklam canlandırmasını oynatır, bitince 4 altın verir; günde 3 kez, sonra "Yarın yine".

  Telefonda tek sütun, katlanırın iç ekranında iki sütun; kaydırma yok.
- **Artı düğmeleri:** cüzdan satırında altının artısı mağazaya, kalbin artısı can kartına götürür (can satılmaz). Altını yetmeyen "daha fazla al" kartında "Mağazaya git" yolu var.
- **Günün hediyesi:** salondaki "Hediye" çerçevesi (kırmızı nokta) takvim gününde bir kez sıradaki booster'ı verir. Sonra hap oyuncunun gece yarısına kalan saati sayar. Geri alınan saat yeni gün açmaz.
- **Tablo ödülü:** bir tablonun son parçası yerine oturunca 80 altın ve sıradaki booster gelir. Ödül parçayla aynı kayıtta yazıldığı için bir kez verilir. Bu sürümden önce tamamlanmış tablolar geriye dönük ödenmez.
- **Tek defter:** her satın alma, reklam altını ve hediye tek bir defterden (`WalletLedger`) kurallara gider ve hemen kaydedilir. Ret hiçbir şeyi değiştirmez; müze nedenini söyler. Motor ve ekonomi değişmedi.
- **Bilinen:** telefonun saati ileri alınırsa ertesi günün reklamları ve hediyesi açılır (sunucu yok; A01). Simülatör tablo ödülünü onuncu kazanmada veriyor, oyun son parçada (A26). Satın alma ve ödülün sesi ve uçan altın canlandırması yok.
- **Doğrulama:** 3.560 test. Unity'de bütün denetimler; mağaza, reklam, hediye ve tamamlanan tablo yürüyüşü; mağazalı tasarım karşılaştırması hatasız. Mağaza çizimle neredeyse birebir.
- **Telefon testleri:** `docs/PHONE_TESTS.md`, T160–T172.

**Ayarlar, gizlilik ve veri, destek (2026-10-15).** Arayüz planının 13. aşaması.

- **Çark ve kart:** cüzdan satırının sağındaki çark, SettingsTR çizimindeki "Ayarlar" kartını salonda, haritada, koleksiyonda ve mağazada açar. Geri hareketi ve × kartı kapatır; kart açıkken arkası tepki vermez.
- **Satırlar:** yalnızca bu sürümün yapabildiği şeyler için satır çizilir (`DeviceAbilities`).
  - "Dil" "Türkçe" gösterir, basılmaz.
  - "Gizlilik ve veri" ile "Destek" kendi kartlarını açar.
  - Ses ve titreşim anahtarları 14. aşamada gelir. Müzik satırı bir müzik seçilene kadar yok (2026-10-22 düzeltmesi: müzik ve "Müzik" anahtarı geldi, aşağıda "Müzik").

  Kartın altında oyuncu kimliği (cihaz kimliğinin ilk sekiz karakteri, iki grup), sürüm ve kuralların sürüm işaretlerinden gelen satır durur: "Test sürümü · reklamlar canlandırma, satın alma kapalı".
- **Gizlilik ve veri:** taslak metin (§20), deneme kayıtlarının sayısı ve "Kayıtları sil". Silme bir kez sorar. "Sil" kayıt dosyasını boşaltır; ilerleme, cüzdan ve ayarlar yerinde kalır.
- **Destek:** oyuncu kimliği, sürüm, bölüm içeriği sürümü ve kayıt dosyasının yeri. Destek adresi yok, çünkü henüz verilmedi.
- **Ayarların kaydı:** `device.json` 2. düzende ayarları da tutar. 1. düzendeki dosya bütün ayarlar açık okunur; bozuk dosya ilerlemeyi ve cüzdanı götürmez. Yeni anahtar denetimi (`Toggle`) hazır.
- **Unity istatistikleri:** `ProjectSetup` Unity'nin kendi istatistik bayrağını kapatır (`submitAnalytics: 0`).
- **Bilinen:** APK INTERNET iznini taşıyor (A28); Android yedeklemesi açık (A29); gizlilik metni onay bekliyor. 2026-10-16: metin onaylandı, izin kaldırıldı, yedekleme açık kalıyor. Silme sorusunda "Sil" yeşil, "Vazgeç" mavi, çünkü sanatta kırmızı düğme yok.
- **Doğrulama:** 3.648 test. Unity'de bütün denetimler; çark, kartlar ve silme yürüyüşü; ayarlar kartlı tasarım karşılaştırması hatasız. Kart çizimdekinden kısa, çünkü anahtar satırları yok. Gizlilik satırı çizimdeki "Yalnızca anonim oyun verisi toplanır" yerine "Oyun hiçbir şey göndermez" diyor; çizimdeki cümle bu sürüm için doğru olmazdı.
- **Telefon testleri:** `docs/PHONE_TESTS.md`, T180–T187.

**Ses ve titreşim, yer tutucular (2026-10-16).** Arayüz planının 14. aşaması.

- **Sesler:** 15 kapalı ses işareti var: basış, ret, değiştirme, eşleşme, özel taş, patlama, booster, şölen atışı, kart inişi, kazanma, kaybetme, altın, parça yerleşmesi, tablo tamamlanması, anahtar.
  - Hangi sesin ne zaman çalacağını düz bir kural söyler (`SoundScore`).
  - Sesler tahtanın kendi zaman çizelgesinde çalar; hiçbir tur ses yüzünden uzamaz.
  - Aynı ses bir hamle ya da şölen içinde en erken 0,12 saniye sonra yinelenir. 12 hamlelik şölen yaklaşık iki atışta bir duyulur.
- **Titreşim:** dört anda kısa bir tık: özel taş ve booster hafif, kazanma ve tablo tamamlanması güçlü. Paket VIBRATE iznini ister.
- **Ayarlar:** "Ses" ve "Titreşim" anahtarları ayarlar kartında. Ayar her sesin çaldığı anda okunur; değişiklik hemen geçerli olur ve `device.json`'da saklanır. İlk açılışta ikisi de açık. Müzik satırı yok, çünkü müzik yok (2026-10-22 düzeltmesi: "Müzik" anahtarı Ses ile Titreşim'in arasında).
- **Ses dosyaları:** Kenney'nin Interface ve Impact paketlerinden 15 kısa CC0 dosyası (toplam 114 KB). `tools/unity/fetch-sounds.js` indirir; kaynaklar, lisanslar ve özetler `art/sounds/licences.json`'da. Dosyalar ad ve ölçüyle (uzunluk, tepe seviyesi) seçildi, kulakla değil.
- **Bilinmeyen:** seslerin nasıl duyulduğu ve tıkların nasıl hissedildiği (A30). Proje sahibinin kulağı ve eli telefonda karar verecek (T191–T197).
- **Doğrulama:** 3.666 test. Unity'de bütün denetimler, anahtarı çevirip kartı yeniden açınca ayarın korunduğunu gösteren yürüyüş ve her sesi çalan oyun denetimi hatasız.
- **Telefon testleri:** `docs/PHONE_TESTS.md`, T190–T201.

**Gizlilik kararları ve ilk prototip APK'sı (2026-10-16).**

- Proje sahibi gizlilik metnini onayladı.
- INTERNET izni paketten çıkarıldı (`AndroidBuild.Permissions`). 16 Ekim APK'sında `aapt` ile doğrulandı: paket yalnızca VIBRATE iznini istiyor.
- Android yedeklemesi açık kalıyor; telefon değişince ilerlemeyi kurtarabilir.
- 15. aşama APK'dan sonraya bırakıldı.
- APK 16 Ekim 01.23'te proje sahibinin telefonuna USB ile kuruldu (eskisinin üstüne) ve açıldı. Eski kayıt okundu: ilk tablonun 5/10 parçası yerinde; cüzdan eski kayıt düzeninden hoş geldin hediyesiyle başladı (200 altın, 5 can).
- Bir indirme sayfası açıldı; önce yalnızca Tailscale hesabındaki cihazlara, aynı gece proje sahibinin isteğiyle herkese (Tailscale Funnel, 10000 numaralı kapı): https://arc.tail0b9256.ts.net:10000/ (bu bilgisayar açıkken ve D: takılıyken çalışır; sunucu `tools/apk-share/serve.mjs`, yalnızca 127.0.0.1'de)

**Gizli test modu (2026-10-17, #10).** Proje sahibi oyunu durmadan deneyebilsin diye istedi.

- **Açma:** çark > Destek > "Sürüm" satırına art arda 7 dokunuş. "Test modu açılsın mı?" sorusuna "Aç". Ayarlar kartında "Test modu" anahtarı görünür; oradan kapatılır.
- **Açıkken:** cüzdan satırında altın ve kalpte "∞", can hapında "Test". Satın almak altın düşürmez (setler, başlangıç booster'ları, raftaki artı, altınla "+5"). Kaybetmek ya da hamleden sonra çıkmak can götürmez. Kaybetme ve çıkma kartları "Test modu · can gitmez" der, teklifte "Vazgeç" yalnız durur. Kazanç, reklam hakları, günün hediyesi ve parçalar olağan kurallarla çalışır.
- **Kurallar değişmedi:** `Puzzle.Meta`, `Puzzle.Economy` ve ekonomi ayarları aynı. Test modu istemcide cüzdanın bir türü (`WalletHandling`: `Normal | Unlimited`). Yalnızca kuralların sürüm işaretleri test sürümü dediği sürece var (reklam canlandırma, parayla satış kapalı); mağaza sürümünde yok.
- **Kayıt:** `device.json` 3. düzende `testerMode` tutar; eski dosya kapalı okunur. Deneme kaydına isteğe bağlı `"testerMode": true` alanı eklendi (yalnızca doğruyken yazılır, şema 1, motor izi aynı). `puzzle humans` ve özet bu denemeleri karşılaştırmaya katmaz, ayrı sayar.
- **Yazı tipleri:** dört yazı tipi "∞" işaretini içerecek biçimde yeniden üretildi.
- **Doğrulama:** 3.685 test. Unity'de bütün denetimler ve test modu yürüyüşü hatasız. Telefon testleri: `docs/PHONE_TESTS.md`, T210–T223.
- **Prototip APK'sı (2026-10-17):** Test modlu APK 17 Ekim'de derlendi (9,7 dakika, 283 MB; `aapt`: yalnızca VIBRATE izni) ve indirme sayfasında 16 Ekim APK'sının yerini aldı. İndirme sayfasındaki eski APK'nın yerini aldı.

**Sayfalar arasında kaydırma (2026-10-18, #3).**

- **Sayfalar:** müzenin dört sayfasında (Mağaza, Koleksiyon, Müze, Harita) sola ya da sağa kaydırmak, alt çubuğun sırasıyla komşu sayfaya geçer. Sayfa parmağı izler, komşusu yanında belirir. Bırakınca genişliğin üçte birini geçtiyse ya da hızlı bir fiske (en az 24 pt, saniyede 500 pt) varsa geçer, yoksa geri yerine oturur. Çubuktaki vurgu bırakınca kayar; düğmeler eskisi gibi çalışır.
- **Uçlar:** mağazadan sağa, haritadan sola çekince sayfa en çok genişliğin %15'i kadar esner ve geri oturur.
- **Koleksiyon:** oda plaketinin üstünde başlayan kaydırma odayı değiştirir (sonraki odanın tabloları kayarak gelir); başka her yerde sayfayı değiştirir. Oklar aynen duruyor.
- **Kaymayan yerler:** tahta, yapboz, öne alınmış tablo ve açık bir kart varken hiçbir şey kaymaz.
- **Dokunuş ve kaydırma ayrımı:** dokunuş 10 pt'ye kadar dokunuş kalır. Kaydırma sayılmak için yanlara, yukarı-aşağıya gittiğinin en az iki katı gitmelidir. Ekranın sol ya da sağ kenarına 32 pt'den yakın başlayan dokunuş hiç kaydırmaz; Android'in geri hareketi kendi bölgesini korur.
- **Yapı:** karar düz bir türde (`PageSwipe`, adlı eşikler, 36 test); sürükleme sahnenin saatinde çizilir (`Tempo`, sayfa genişliği başına 0,28 sn).
- **Bilinen:** uçta esnerken sayfanın ötesinde duvar deseni olmayan düz bir şerit görünüyor (A34). Komşu sayfa ilk sürüklemede kuruluyor, telefonda takılabilir (T240).
- **Doğrulama:** 3.721 test. Unity'de bütün denetimler ve kaydırma yürüyüşü hatasız. Telefon testleri: `docs/PHONE_TESTS.md`, T230–T241.

**Dil desteği, 1. adım: altyapı, Türkçe, İngilizce, Korece (2026-10-18, #8).**

- **Çeviri tabloları:** oyuncunun gördüğü her yazı dil başına bir tablodan gelir; anahtarlar kapalı bir türdür (`WordId`, 200 anahtar). Kaynak `words/tr.json`'dur ve her girişte çevirmen için bir not vardır; `words/en.json` ve `words/ko.json` onu çevirir. Kod içinde tablo dışında oyuncu yazısı kalırsa bir test kırılır.
- **Biçimler:** binlik işareti (1.250 / 1,250), kısa süreler ("2 sa 59 dk", "2h 59m", "2시간 59분"), çoğullar (Rusçanın bir/birkaç/çok biçimi hazır) ve sıra sayıları dile göre.
- **Dil seçimi:** proje sahibinin seçtiği 13 dil kapalı bir tür. İlk dil telefonun dili: her Portekizce Brezilya'nınki, Tayvan, Hong Kong ve Makao geleneksel Çince, başka her şey İngilizce. Ayarlardaki "Dil" satırı dil kartını açar; her dil kendi adıyla yazılır; seçim bütün sayfaları ve kartları hemen değiştirir, sonraki tahta yeni dilde kurulur. Seçim `device.json`'da (4. düzen); eski dosya telefonun dilini izler. Bir dil, tablosu tamamsa kartta görünür; bu adımda Türkçe, İngilizce ve Korece.
- **Yazı tipleri:** Noto Sans KR ExtraBold ve Noto Serif KR Bold (OFL) tasarım yazı tiplerinin arkasında, tabloların kullandığı 416 Korece hecelik sabit atlaslar olarak durur; lisansları `art/fonts/licences.json`'da. Depoya yaklaşık 29 MB ekledi; APK'ya tahminen 1–2 MB.
- **Tablo ve oda adları:** `museum/catalogue.json` değişmedi; dil başına adlar `museum/titles/<dil>.json`'da. Korece 22 tablo adı Wikidata'daki yerleşik adlardır (her biri tablosuyla karşılaştırıldı), 78'i çeviridir ve öyle işaretlidir. Ressam adları değişmez.
- **Sığdırma:** uzun tablo adları plaketlerde iki dengeli satıra bölünür (Türkçede de); harita etiketleri oda duvarının içine sığacak kadar küçülür; Korece sözcük aralarından kırılır.
- **Bilinen:** çeviriler makine çevirisidir, yayından önce ana dili olan birinin gözden geçirmesi gerekir. Dar dikey tabloların ressam satırı eskisi gibi küçülüyor (yaklaşık %46).
- **Doğrulama:** 3.882 test; ekonomi denetimi aynı. Unity'de bütün denetimler; Türkçe, İngilizce ve Korece yürüyüşler; dil kartında dil değiştiren yürüyüş hatasız. Telefon testleri: `docs/PHONE_TESTS.md`, T250–T265.

**İpucu (2026-10-19, #6).** Kararlar §19'da.

- **Görünüş:** tahta dururken oyuncu bir süre dokunmazsa hamlenin iki taşı sıcak bir hale ve parlak bir halkayla çevrilir, hafifçe büyür ve 1,5 saniyede bir birbirine doğru kıpırdar. Yerinde patlatılan bir özel taşa verilen ipucu parlayıp şişer. Titreşim açıksa hafif bir tık gelir; ses yoktur.
- **Kurallar:** herhangi bir dokunuş, geri hareketi, hamle ya da booster ipucunu hemen kaldırır. Kart açıkken, booster seçiliyken, şölende, zincirleme sürerken, bir taş seçiliyken ve hamle kalmamışken çıkmaz. Bir denemede en çok 3.
- **Hesap:** `HintAdvisor.MoveFor` (`Puzzle.Bot`) hamleyi, tahtanın deneme tohumundan kurulmuş bir kopyasında merdivendeki bir bota sordurur; oyunun rastgele akışına dokunmaz. Motor izi değişmedi; sabitlenmiş bir ipucu izi Unity'de .NET'teki ipuçlarını verir.
- **Ayarlar:** ekonomi ayarlarında `hints` bölümü (bot, bekleme, deneme başına sayı; paket biçimi 3). Seçilen bot casual, bekleme 8 sn, denemede en çok 3; üçü de karar (2026-10-19, 9ad22f0).
- **Ölçüm:** `puzzle hints` komutu, botların ipucuna uyarak oynadığı denemelerle kazanma oranındaki artışı ölçer (§19'daki tablo).
- **Kayıt:** deneme kaydında isteğe bağlı `hintsShown` (şema 1). İpucu görülen denemeler ayrı bir grupta, casual'ın ipuçlarına uyan botların yanında karşılaştırılır (`puzzle humans`; CSV'de `group` sütunu). Bir botun hangi hamlelerde ipucu göreceği bir varsayıma dayanır: düşünme süreleri belleksiz dağılır, ortalama hamle başına 5,3 sn; buna göre 8 sn'de hamlelerin %22'sinde. Telefon kayıtlarındaki gerçek düşünme süreleri gelince bu varsayım onlarla değiştirilecek.
- **Bilinen:** halenin tahtanın kenarında yaldız çerçeveye taşması kabul edildi (T297). Ölçüm, oyuncunun her ipucuna uyduğunu varsayar (üst sınır). İpucunun aynı çıkması .NET ve Mono'da gösterildi, telefonun IL2CPP derlemesinde değil.
- **Doğrulama:** 3.920 test. Unity'de bütün denetimler ve ipucu yürüyüşü hatasız. Telefon testleri: `docs/PHONE_TESTS.md`, T290–T299.

**Öğretici (2026-10-19, #5).** Kararlar §19'da. Her adım oyuncuya bir kez gösterilir.

| Adım | Ne zaman | Ne gösterir | Ne der |
|---|---|---|---|
| Hedef | 1. bölüm, ilk sakin an | Hedef plaketi aydınlık | "Hedefin bu plakette / Bu taşları topla, bölümü kazan" |
| Eşleşme | 1. bölüm | İki taş ve oluşacak üçlü aydınlık; yalnızca o hamle kabul edilir | "Bu iki taşın yerini değiştir / Aynı üç taş yan yana gelince toplanır" |
| Hamleler | 1. bölüm, ilk hamleden sonra | Hamle madalyonu | "Her hamle bu sayıyı bir azaltır / Hamlelerin bitmeden hedefi tamamla" |
| Dörtlü | 2.–5. bölüm, tahta bir dörtlü sunduğu ilk an | Hamle ve dörtlü sıra | "Dört aynı taşı yan yana getir / Fırça olur: dokununca bir sırayı boyar" |
| Beşli | 3.–6. bölüm, tahta bir beşli sunduğu ilk an | Hamle ve beşli sıra | Beşlinin paleti yaptığını söyler |
| Engeller | Sandık 11, vernik 21, bant 41, çerçeve köşesi 51 | Tahtanın ortasına en yakın engel | Engelin ne olduğunu ve nasıl temizlendiğini söyler |
| İlk parça | İlk yapboz | Tepsideki parça ve tablodaki yeri | "Parçayı tablodaki yerine sürükle" |
| İlk tablo | İlk tablo tamamlanınca, kutlamadan sonra | Tablo çerçevesinde | Müze hakkında bir satır |

- **Gösterim:** gerisi kararır, aydınlık yerlerin kenarı yumuşak bir yaldızla çizilir, yazı raftaki yazı gibi bir hapta durur. El ya da insan çizimi yok.
- **Hamle seçimi:** gösterilecek hamle, tahtanın bir kopyasında yalnızca gereken özel taşı yapan (ve başka eşleşme doğurmayan) düz taş değiş tokuşları arasından seçilir; aralarından uzman bot seçer. Tahta böyle bir hamle sunmazsa adım bekler; kendi bölümünde ve sonraki üç bölümde sunulmazsa bırakılır. Taramada 2. bölüm 16 tahtanın 14'ünde başta bir dörtlü sunuyor; 3. bölüm 16'nın 8'inde başta bir beşli, bölüm içinde ise daha sık.
- **Kayıt:** ilerleme dosyası 5. sürüm, görülen adımları tutar; eski dosyada bölümü kazanılmış adım görülmüş sayılır. Rehberli hamleler deneme kaydında sayılır (`guidedMoves`) ve denemeyi yardımlı yapar.
- **İpucuyla ilişki:** adım sürerken ipucu çıkmaz; adımlar bitince ipucu olağan kuralla döner.
- **Bilinen:** öteki özel taşlar (sıçrama, kelebek, büyük sıçrama) için adım yok. Bir köşe parçasının yerini gösteren pencere çerçevenin üstüne taşıyor. Telefonda bakılmadı.
- **Doğrulama:** 3.953 test. Unity'de bütün denetimler ve öğretici yürüyüşleri (Türkçe, İngilizce, Korece) hatasız. Telefon testleri: `docs/PHONE_TESTS.md`, T310–T321.

**Booster tanıtımı (2026-10-19, #7).** Kararlar §19'da.

| Ne | Ne zaman | Nasıl |
|---|---|---|
| Rehberli kullanım | Spatula 7., satır fırçası 12., sütun fırçası 16., karıştırma 20. bölümde, bir kez | Gerisi kararır; disk ışık halkasıyla, hedef aydınlık. Diske dokununca hedefte booster'ın kendi nişanı çıkar; yalnızca o kullanım kabul edilir. Karıştırma dokununca çalışır. Bedavadır, stok azalmaz. |
| Uzun basma öğretimi | İlk rehberli kullanımdan hemen sonra, bir kez | Disk yeniden aydınlanır: "Bir booster'ı basılı tutarsan ne yaptığını yeniden izlersin". |
| Canlandırma kartı | Herhangi bir booster'a 0,45 sn basılı tutunca (raftaki dört, bölüm kartındaki üç) | Kart iner, 5×5'lik küçük bir tahtada booster'ın etkisi oyunun kendi tahta çizimiyle ve motoruyla tekrar tekrar oynar. Hiçbir şey seçilmez ya da harcanmaz; dokunuş ya da geri hareketi kapatır. |
| Başlangıç booster'ları | Bir bölüm ilk kez kazanılamadan bırakıldıktan sonra (kayıp ya da hamleden sonra çıkış), o bölümün kartı inince, bir kez | Fırça + sıçrama, gökkuşağı paleti ve +3 hamle sırayla aydınlanır, her biri tek cümleyle; dokunuş ilerletir. |

- **Hedef seçimi:** her aday kullanım tahtanın bir kopyasında denenir (gerçek oyuna dokunulmaz); hedeflere en çok ilerleten kazanır. Spatulada adaylar engeller ve hedef rengindeki taşlar (eşitlikte önce engel, sonra ortaya en yakın); fırçada her satır ya da sütun (eşitlikte ortaya en yakın). Fırça aydınlık satırın her hücresinde kabul edilir. Gönderilen bölümlerde 7. bölüm mor bir taşı, 12. bölüm iki sandıklı alt satırı gösteriyor.
- **Bedava ve deneme:** rehberli kullanım cüzdandan bir şey düşmez. Tek başına denemeyi başlatmaz: hemen ardından çıkmak can götürmez, kayıt yazılmaz, adım görülmüş kalır. Deneme oyuncunun kendi ilk hamlesi ya da booster'ıyla başlar.
- **Kayıt:** rehberli kullanım `boostersUsed` içinde durur, deneme "yardımlı" sayılır. İsteğe bağlı yeni `guidedBoosters` alanı stoğun ödemediği kullanımları adlandırır (şema 1).
- **Eskiden olan:** açılıştaki ışık halkası ve "Yeni: …" yazısı bu adıma dönüştü. Rehberli adım ve kart açıkken ipucu çıkmaz.
- **Bilinen:** küçük ekranda (2076×2152) bazı haplar tahtanın kenar hücrelerinin üstüne biniyor. Kartın küçük tahtasında düşen taşlar bazen yeni özel taş yapıyor. +3 hamle kartında yalnızca madalyon sayıyor. Uzun basma yazısını kaçıran oyuncu sonraki açılış bölümünde görür, 20. bölümü geçmişse hiç görmez. Telefonda bakılmadı.
- **Doğrulama:** 3.994 test. Ekonomi denetimi temiz. Unity'de bütün denetimler, öğretici ve booster yürüyüşleri (Türkçe, İngilizce, Korece) hatasız. Telefon testleri: `docs/PHONE_TESTS.md`, T330–T345.

**Dil desteği, 2. adım (2026-10-19, #8).** Kararlar §19'da. Oyun artık 13 dil konuşuyor: İngilizce, Türkçe, Korece, Almanca, Fransızca, İspanyolca, Portekizce (Brezilya), İtalyanca, Endonezce, Rusça, Japonca, Çince (basitleştirilmiş ve geleneksel).

- **Tablolar:** on dilin sözcük tabloları ve tablo adları bekleme klasöründen oyuna geçti (232 kimlik). Booster tanıtımının 13 yazısı da çevrildi. Yerleşik tablo adları Wikidata'dan, katalogdaki tablo ve müzeyle eşleşince alındı; kalanı çeviri.
- **İlk dil:** telefonun dili. Portekizcenin her bölgesi Brezilya Portekizcesine, Çin Çincesi basitleştirilmişe, Tayvan ve Hong Kong gelenekseline gider. Desteklenmeyen dilde İngilizce.
- **Dil kartı:** ayarlardan açılır, sayfada tek başına asılı durur; 13 dil iki sütunda, her biri kendi yazısıyla.
- **Yazı tipleri:** Kiril, Japonca ve Çince için Noto yedekleri (OFL; kaynak notofonts GitHub sürümleri; `art/fonts/licences.json`). Her dil kendi Han biçimini önce sorar: Japonca JP, basitleştirilmiş SC, geleneksel TC, Korece KR. Atlaslar yalnızca tablolarda geçen harflerden kurulur; APK'ya tahminen 4,5 MB ekler (tam yazı tipleri 45 MB olurdu). Yazı tipi dosyaları, Korecedeki gibi depoda tutulur.
- **Satır kırma:** Japonca ve Çince boşluksuz satırlar TextMesh Pro'nun baş ve son karakter kurallarıyla kırılır. Fransızcada ! ? : ; önünde bölünmez boşluk var. Harf setine ¡ ¿ œ Œ « » eklendi.
- **Sığma:** bütün dillerde iki ekranda da durgun sayfalarda kenardan taşan satır yok. 9 puntonun altına düşen yedi satır kısaltıldı (2 Fransızca, 1 Almanca, 1 Japonca, 3 Rusça).
- **Bilinen:**
  - Çevirilerin hepsi makine çevirisi; yayından önce her dili ana dili olan biri okumalı.
  - Bütün yedek atlaslar her dilde belleğe yüklenir (16,5 MiB doku).
  - İspanyolca "MOVIMIENTOS" madalyon halkasına çok yakın (kısa seçenek "JUGADAS").
  - "Português (Brasil)" ve "Bahasa Indonesia" dil kartında yaklaşık %80'e küçülüyor.
  - Japonca ve Çince yedeklerin kalınlığı (700) Koreceninkinden (800) biraz ince.
  - Telefonda bakılmadı.
- **Doğrulama:** 4.175 test. Ekonomi denetimi temiz. Unity'de bütün denetimler; ekran, öğretici ve booster yürüyüşleri on bir dilde iki ekranda hatasız. Telefon testleri: `docs/PHONE_TESTS.md`, T350–T363.

**Müzik (2026-10-22, #9).** Kararlar §19'da: tek bir genel müzik, M1 Müze teması, her yerde.

- **Parça:** proje sahibinin Suno Premier hesabıyla üretildi (2026-10-20). Oyun açılınca girişi (33,6 sn) bir kez çalar, sonra 36 ölçülük döngüye (103,4 sn) boşluksuz geçer. Döngünün sonu, başından önceki çeyrek saniyeyle yumuşak geçişle birleşir.
- **Kesim:** `tools/music/cut-loop.py`, `art/sounds/music.json`taki kayda göre iki parçayı asıl dosyadan keser; iki çalıştırma aynı baytları verir. Döngü M1-B; M1-A ya da M1-C'ye geçmek kayıtta tek değer. Kayıt lisansı da tutar (hesap, tarih, istem, asıl dosyanın parmak izi).
- **Çalar:** oyunun kökünde tek bir çalar; ekran değişince müzik baştan başlamaz; efektlerin yaklaşık 5 dB altında (`SoundLevels.Music` = 0,4); uygulama arka plana geçince durur, dönünce sürer.
- **Ayarlar:** "Müzik" anahtarı Ses ile Titreşim'in arasında; açık başlar, kapatınca 0,3 saniyede susar, `device.json` 5. düzende saklanır (eski dosya "açık" sayılır). Sözcük 13 dilde.
- **Boyut:** Android'de Vorbis: giriş 0,70 MB, döngü 2,23 MB (akışla); APK'ya tahminen 2,9 MB.
- **Bilinen:** müzik henüz kulakla dinlenmedi; seviye ölçüyle seçildi. Başka bir uygulama müzik çalarken oyunun müziği susar, efektler sürer (proje sahibinin kararı, 2026-10-23; a05c580): oyun açılışta, öne gelince ve 2 saniyede bir Android'in çalan medya oynatıcılarına bakar, kendi oynatıcısını etiketinden ayırır; öteki müzik durunca 2–4 saniye içinde kaldığı yerden geri gelir. iOS'ta aynı kural Apple'ın "ikincil ses susmalı" ipucuyla, iOS dalında. Suno bağlantısı kayıtta yok.
- **Doğrulama:** 4.223 test. Ekonomi denetimi temiz. Unity'de bütün denetimler ve yeni müzik denetimi hatasız. Telefon testleri: `docs/PHONE_TESTS.md`, T370–T379.

**Dünya haritası (2026-10-22, #4).** Kararlar §19'da; tasarım tuvalindeki "Dünya haritası · dönemler" çizimi birebir oyuna geçti, müzenin kat planı kalktı.

- **Yerler:** `museum/places.json`: her tablonun yapıldığı ya da gösterdiği yer; 59'u Wikidata ya da tabloyu tutan müzenin sayfasıyla doğrulandı (24 tam, 31 şehir, 4 bölge), kalanı ressamın o yıllarda çalıştığı yer. Yer, not ve müze adları 13 dilde `museum/places/<dil>.json`. Proje sahibi açık kalan beş seçimi olduğu gibi bıraktı (kediler Edo, Vertumnus Prag, Babür tabloları Agra, Stubbs Knole, Oudry Versay).
- **Harita verisi:** `puzzle worldmap build`, Natural Earth 50m kara ve göllerini (kamu malı; `art/map/sources.json`) oyunun çizdiği `WorldMap.bytes` dosyasına çevirir (351 KB, 32 bin kıyı noktası; iki çalıştırma aynı dosyayı verir).
- **Çizim:** kara üçgen ağlarla, kıyı çizgileri bir gölgelendiricide her yakınlıkta aynı incelikte; kayışta hiçbir şey yeniden kurulmaz. Bu bilgisayarda bir kayış karesi grafik kartında yaklaşık 8 ms (salon 21 ms).
- **Davranış:** açılışta bütün dünya, sonra 0,85 sn'de oyuncunun dönemine kayış; 20 dönemlik şerit; aynı yerdeki tablolar bir sıra hâlinde, ince çizgiyle yerlerine bağlı; tablo kartında durum, "Oyna" (bölüm kartı) ve "Koleksiyonda gör". Şeritte yatay sürükleme şeridi kaydırır, haritanın geri kalanında sayfalar eskisi gibi kayar; kart açıkken hiçbir şey kaymaz.
- **Sözcükler:** 26 yeni satır ve 20 kısa dönem adı 13 dilde (`museum/titles`).
- **Bilinen:** oyunda ekran okuyucu bağlantısı yok (düğmelerin etiketleri var, okuyan yok). Uzun dönem adları plakada iki satıra bölünüyor (proje sahibinin seçimi, 2026-10-22); 14. dönem karşılaştırma için bilerek tek satırda, küçük kalıyor ve seçimden sonra kaldırılacak. 6. dönemin beş yeri Tokyo çevresinde, kamera en yakından açılıyor. Geleneksel Çince serif atlası büyüdü (+2 MB). Telefonda ölçülmedi.
- **Doğrulama:** 4.068 test (kat planının yaklaşık 200 testi onunla gitti, 49 harita testi geldi). Ekonomi denetimi temiz. Unity'de bütün denetimler ve beş dilde harita yürüyüşleri hatasız. Telefon testleri: `docs/PHONE_TESTS.md`, T390–T403.

**Geçici olanlar:** paket kimliği `com.saturnwave.puzzle` ve ürün adı (oyunun adı en sona kaldı), Unity'nin deneme imzası. **Güncelleme (2026-10-25):** paket kimliği artık kalıcı: `com.saturnwave.museumpuzzle` (§19); ürün adı ve imza hâlâ geçici.

**Salonda geri tuşu ve dokunuş eşiği (2026-10-23).**

- **Çıkış kartı:** 23 Ekim telefon turunda salonda Android geri tuşu hiçbir şey yapmıyordu (T43). Artık salonda, kart açık değilken geri tuşu ya da kenardan geri hareketi "Oyundan çıkılsın mı?" kartını indirir.
  - "Vazgeç" ya da yeniden geri kartı kaldırır; kartın dışına dokunmak bir şey yapmaz.
  - "Çık" önce ilerlemeyi kaydeder ve bekleyen deneme kayıtlarını yazar, sonra oyunu kapatır. Oyun yalnızca Android'de kapanır; Editör'de günlüğe yazar ve açık kalır.
  - iOS'ta bu tür bir geri tuşu yoktur; kart orada hiç çıkmaz.
  - Yapı: `QuitCard`, `MuseumStage.TakeTheWishToQuit`; yazılar `HallQuitQuestion`, `HallQuitLeave`, `HallQuitStay` (13 dil).
- **Dokunuş eşiği:** ayarlar kartında bir satırın üstünde yatay kaydırma satıra basılmış sayılıyordu (dil kartı açılıyor, Titreşim kapanıyordu).
  - Artık her düğme yalnızca dokunuşla basılır. Parmak indiği yerden 10 pt'den uzağa giderse sürükleme olur ve nasıl kalkarsa kalksın hiçbir şeye basmaz.
  - Eşik sayfa kaydırmasınınkiyle aynıdır (Android'in kendi eşiği kadar); kartlarda kartın kendi ölçüsünde ölçülür.
  - Bütün kartlar, sayfalar, tahta ve yapboz düğmeleri için geçerli. Yapı: `TapSlop` (Unity'siz, testli), `PressSet`, `PageSwipe`.
- **Doğrulama:** 4.111 test. Unity'de bütün denetimler ve yeni "wayback" yürüyüşü (tr, en, ko) hatasız; yürüyüş eski kodda kırılıyor. Telefon testleri T410–T414; T43'ün beklenen sonucu güncellendi.

**Koleksiyonda bellek (2026-10-24, A42/4).**

- **Bulgu:** 23 Ekim telefon turunda koleksiyonun 20 odası gezilince oyunun belleği 401 MB'tan 649 MB'a çıktı. Neden: her odanın resimleri `Resources` ile yükleniyor, oda kaldırılınca bırakılmıyordu. Yalnızca sahne değişince (tahtaya ya da yapboza geçişte) boşaltılıyordu. Bir gezintide müzenin 100 resminin hepsi bellekte kalıyordu; telefonda resim başına yaklaşık 2,5 MB (ASTC 6x6, 2048 piksel, mipmap'li). Ağlar, malzemeler ve sahne nesneleri zaten temizleniyordu.
- **Düzeltme:** `PictureShelf` (Unity'siz, testli) her resmi bir kez yükler. Resmi gösteren her şey onu tutar ve kaldırılınca bırakır (`PaintingLook` artık `IDisposable`). Hiçbir şeyin tutmadığı resimlerden yalnızca son bırakılan 10'u (iki oda) bellekte kalır; fazlası, en eskisinden başlayarak `Resources.UnloadAsset` ile boşaltılır. Tutulan bir resim hiç boşaltılmaz; sahne değişince kalan 10 da boşaltılır.
  - Resmi tutanlar: koleksiyonun odaları ve yakından bakılan tablo, salon ve plaketteki küçük resimler, haritanın iğneleri ve kartı, yapboz, tahtanın ödül resmi ve sonuç kartı.
- **Oyuncunun gördüğü:** değişiklik yok. Son iki odaya dönmek hiçbir şey yüklemez; daha uzaktaki bir oda, ilk açılışındaki gibi yüklenir.
- **Denetim:** `CollectionMemoryCheck`. Son odadaki bir oyuncu için koleksiyonu 20. odadan 1.'ye ve geri gezer: üç kez düğmelerle, iki kez plakette kaydırarak. Her dönüşte ve parmak inikken bellekteki resimleri sayar. 25'ten fazla resim ya da ilk gezintiden sonra artan bir sayı (resim, doku, ağ, malzeme, sahne nesnesi) hatadır.
  - Eski kodda: aynı anda 100 resim, denetim kırılıyor. Yeni kodda: en çok 20, her gezinti sonunda 15 resim; ağ, malzeme ve nesne sayıları gezintiden gezintiye aynı.
- **Doğrulama:** 4.122 test (11'i yeni). Ekonomi denetimi temiz. Unity'de bütün denetimler hatasız; koleksiyonun görüntüleri değişmedi. Telefon testleri T420–T425.

**Giriş sayfası (2026-10-24).**

- **Ne:** oyun her soğuk açılışta salondan önce giriş sayfasıyla açılır. Tür proje sahibinin seçimi ("logo ve dokunarak başla"); çizim tasarım tuvalindeki EntryTR, gece mavisi yerine F simgesine uyarlandı. Oyun arka plandan dönünce sayfa çıkmaz, oyun kaldığı yerden sürer.
- **Görünüm:** simgenin krem zemini (#FFF8E6 → #F6E4BC → #D9B77A) ve üstünde galerinin damaskı kahverengi baskıyla; yukarıdan iki ışık huzmesi, paletin arkasında beyaz ışıma ve altında sıcak bir gölge.
  - Ortada simgenin paleti ve fırçası (`art/icon/entry-logo-1024.png`: simgenin çizimi tam boyunda, saydam zeminde). Yanında paletteki üç taş dışındaki dört taş (mor, turkuaz, pembe, yeşil) yavaşça süzülür.
  - Altında yaldızlı "Müze Yapbozu" koyu kahve çerçeve ve gölgeyle, "Tabloları onar, müzeyi yeniden aç" ve "ÇALIŞMA ADI" rozeti: başlık hâlâ çalışma adı.
  - Altta yaldızlı çubuk (koyu kuyu, yeşil dolgu, üstünden geçen parıltı) ve "Yükleniyor… %45"; en altta "SaturnWave" ile "Sürüm 1.0 · test sürümü". Müzik bu sayfada başlar.
- **Çubuk gerçek işi izler:** bölümler, müze kataloğu, ekonomi ve oturum (efektleri çizer, salonu ve odanın beş resmini hazırlar). Her karede bir adım yapılır; ilk adım sayfanın ilk karesi çizildikten sonra.
  - Adımların payı Editör'de ölçülen sürelerdir (200, 10, 50, 650 ms). Telefonda her adım süresiyle günlüğe yazılır ("Start-up: … took … ms").
  - Çubuk işin önüne geçmez, geri gitmez ve 1,2 saniyeden kısa sürede dolmaz: hızlı telefonda da dolduğu görülür, yanıp sönmez. Dolunca 0,15 sn durur ve 0,3 sn'de yerini nefes alan "Başlamak için dokun"a bırakır.
- **Dokunuş:** hazır sayfada herhangi bir yere dokunmak sayfayı 0,22 sn'de karartır; salon, bir bölümden dönüşteki gibi o karanlıktan aydınlanır. Salon sayfa gösterilirken arkada, karanlıkta açılmıştır: öğretici, hediyeler ve bölüm düğmesi bugünkü gibi. Çubuk dolmadan dokunuş bir şey yapmaz; kaydırma dokunuş sayılmaz.
- **Geri tuşu:** salondaki "Oyundan çıkılsın mı?" kartının aynısını indirir, çubuk dolarken de. "Vazgeç" ya da yeniden geri kartı kaldırır; "Çık" oyunu kapatır (oturum açıldıysa önce kaydeder).
  - Gerekçe: giriş sayfasının da salon gibi daha geri bir yeri yok; aynı kural, kenar hareketiyle yanlışlıkla çıkılmasın diye. iOS'ta kart hiç çıkmaz.
- **Ekranlar:** sayfa tasarımın ölçeğinde çizilir: dar dış ekranda genişliğe göre (%92), geniş iç ekranda tasarımın kendi boyunda; logo resmindeki pikselden fazlasıyla çizilmez. Kamera çentiğinin ve sistem çubuklarının altına bir şey konmaz; artan yükseklik paletin üstüne, rozet ile çubuk arasına ve çubuk ile alt satır arasına paylaşılır.
- **Yapı:** `EntryStage` (sayfa; salonu görmesin diye sahnenin uzağında kurulur), `EntryLayout`, `LoadingBar`, `StartUpWork` (son üçü Unity'siz, testli) ve `GameRoot` (adımlar, geçiş, ses). Yeni yazı stilleri `GiltTitle` ve `OnCream`; çizimler `entry-bar` ve `entry-bar-fill`. 7 yeni yazı 13 dilde; "SaturnWave" her dilde aynı.
- **Bilinen:** oturumu açan adım bir kareyi tutar (Editör'de yaklaşık 0,5 sn); o arada taşlar ve parıltı durur. Telefondaki süre günlükten okunacak, gerekirse resimler kare kare yüklenir. Alt satırdaki "test sürümü" mağaza sürümünde değişmeli.
- **Doğrulama:** 4.153 test (31'i yeni). Ekonomi denetimi temiz. Unity'de `PlayModeCheck` (giriş sayfası → salon) ve `MusicCheck` (giriş → salon → bölüm → salon) hatasız; sayfanın görüntüleri iki ekranda, güvenli alanlarla ve dört dilde (`ScreenShots -screens entry`). Telefon testleri T430–T436.

**Yan çevirme: tabletler ve açık Fold (2026-10-24).**

- **Kural (sahibinin kararı):** iPad her zaman döner. Android'de ekranın en büyük penceresinin kısa kenarı 600 dp ve üstüyse (tabletler, açık Fold) döner; o anki pencereye bakılmaz, bölünmüş ekrandaki tablet telefon sanılmaz. Telefonlar ve Fold'un dış ekranı dik kalır.
  - Dönen ekran dört yöne döner, ters dik de: Apple başka uygulamanın yanında çalışan iPad uygulamasından dört yönü ister, Android tablet de iki yönde tutulur (planın önerisi).
  - Bölüm oynanırken de döner: iOS kilidi garanti etmiyor, pencere zaten boyut değiştirebiliyor.
  - Android paketi dik açılır, telefon hiç yan görünmez; oyun ilk karede kuralı uygular. Sistemin döndürme kilidine uyulur. Yapı: `ScreenTurns` (Unity'siz, testli), `DeviceScreen`, `GameRoot`.
  - Manifestte `appCategory="game"` yoksa Android derlemesi durur (`ManifestGameCategory`): Android 16 ve sonrası büyük ekranda oyun olmayan uygulamanın yön isteğine uymuyor. Hedef API 36'ya sabitlendi; bir Unity güncellemesi onu sessizce 37'ye çekemez.
- **Görünüm (seçenek A):** dik düzen ekranın ortasında tam boy kalır, yanlarda galerinin duvarı: desen, köşelerde koyulaşma, lambalar. Siyah bant yok.
  - Yatay tablette (genişlik yüksekliğin en az 1,25 katı) salonda lamba çerçeveye yaklaşır, ötesinde odanın onarılmış tabloları asılır: en çok dört, her yanda ikişer, koleksiyonun ince çerçevesi ve plakasıyla; üsttekiler raydan tellerle. Tabloların resimleri plaketin küçük resimlerinin zaten tuttuğu resimlerdir; bellek artmaz. Telefonda hiç yüklenmezler.
  - Harita: çerçeve dünyanın biçimine doğru genişler (haritanın yüksekliğinin en çok 2,1 katı, yanlarda 28 nokta duvar); şeritte 9–10 dönem birden görünür. Tablo kartı ortada, en çok 420 nokta.
  - Yapboz: tablo solda ekranın yüksekliğince (12,9" iPad'de dik tablo, dik iPad'dekinin 1,3 katından büyük); sağda bir sütun: raydan asılı pip çubukları, ipucu, tepsi ve "Otomatik tamamla". Tepside altı yer, 2×3 ya da 3×2; parçaları büyük gösteren seçilir.
  - Fold yan tutulunca ekran neredeyse kare (663×640 nokta): salon, harita ve yapboz bugünkü gibi kalır.
  - Tahtanın gerçek yatay düzeni (B) bu adımda yok; iPad'de A denendikten sonra konuşulacak. Booster yazısının en alt sırayı örtmesi (A24) sürüyor.
- **Döndürmeye dayanıklılık:** ekran değişince (dönme, katlanma, pencere boyutu) basılı parmak bırakılır (`GameRoot`, `GameSession.Fit`). Dönmeden önce başlamış bir sürükleme başka bir çifti takas etmez, taşınan parça tepsiye döner. Giriş sayfasında kartın düğmesine basılı parmak da bırakılır.
- **Yan paylar:** yan tutulunca kamera deliği yana geçer. `ScreenShape` sol ve sağ payı taşır (varsayılan 0, dik ekranlar aynı), `ScreenView.UsableLeft/UsableRight`. Geri düğmesi, hediye çerçevesi, giriş sayfasının alt satırı, salonun kanatları, harita ve yapboz paylara uyar. Editör araçları ekranı `2152x2076+0+0+90+0` biçiminde de alır.
- **iPad dalından ana dala:** koleksiyon 410 noktadan geniş ekranda üçlü sıra (dik iPad'ler), titreşim motoru olmayan cihazda ayarlarda titreşim anahtarı yok (birçok Android tablet), `MusicCheck` sayfanın yerleşmesini bekler.
- **Denetim:** `RotationCheck` oyunu oturum üzerinden sürer ve ekranı riskli anlarda değiştirir: 12,9" iPad dik↔yan, Fold iç ekran dik↔yan (kamera yanda), Fold dış ekrandan iç ekrana. Taşlar düşerken, sürüklemenin ortasında, tahtanın ve müzenin bütün kartları açıkken, finalde, yapboz parçası taşınırken ve giriş sayfasında; her değişiklikten sonra tahta motorunkiyle aynı, düğmeler kullanılabilir ekranda, günlükte hata yok. Eski kodda sürüklemenin ortasında dönünce bir hamle oynanıyordu; denetim kırılıyor.
- **Telefonlar değişmedi:** Pixel dış ve iç ekranın 262 görüntüsü (19 sayfa ve kart, üç oyuncu) önce ve sonra piksel piksel aynı.
- **iOS** (`feature/ios`): Info.plist'te iPad dört yön, iPhone yalnız dik; "tam ekran zorunlu" kalktı, oyun Pencereli Uygulamalar'da ve Stage Manager'da da çalışır (`docs/IOS_BUILD.md`).
- **Doğrulama:** 5.367 test. Ekonomi denetimi temiz. Unity'de `PlayModeCheck`, `MusicCheck`, `RotationCheck` ve öteki denetimler hatasız; yatay görüntüler üç ekranda (12,9" iPad, 16:10 tablet, Fold yan) bakıldı. Telefon testleri T450–T465.

**İnceleme düzeltmeleri: paketin yönü, açılış hatası, yan paylar (2026-10-24).**

- **Paket dik açılır ve bu denetlenir:** Android derlemesinin manifest adımı izinlerden ve oyun kategorisinden sonra Unity etkinliğinin `android:screenOrientation="portrait"` olduğunu da ister (`ManifestOrientation`, Unity'siz, testli). Başka bir değer (iOS'un `AutoRotation` varsayılanıyla gelen `fullUser` gibi) ya da yönsüz etkinlik derlemeyi durdurur: telefon oyunu ilk karede bile yan göstermez, tablet ve açık Fold oyun çalışınca kurala göre döner.
  - Kayıtlı proje ayarları iki dalda da dik (`Portrait`) ve hedef API 36. iOS derlemesi `AutoRotation`'ı ve "tam ekran istemez"i yalnız kendi dışa aktarması için koyar, bitince geri alır. Hiçbir derleme `ProjectSettings.asset`'i değiştirmez.
- **Açılış hatası:** açılış adımlarından biri hata verirse sayfa "Yükleniyor… %N"de kalmaz; bir sonraki karede çubuğun satırı "Oyun açılamadı; yeniden başlatın" olur (13 dilde, uzun dilde küçülerek). Çubuk kaldığı yerde durur, dokunuş bir şey yapmaz; geri tuşu çıkış kartını indirir, "Çık" oyunu kapatır. Hatanın kendisi günlükte. Açılış işi bitince ya da durunca bırakılır, adımların tuttukları da.
- **Ekran boyu okunamazsa:** Android ekranın boyunu veremezse (etkinlik yok, yoğunluk 0, boş pencere) oyun telefon sayılır ve dik kalır; uyarı bir kez yazılır. Ekran değişince dönme kuralı, sayfa yeni ekrana yerleştikten sonra uygulanır: bir hata sayfayı dondurmaz.
- **Yan paylar:** `RotationCheck` Fold'u iki yana da çevirir (kamera solda `2152x2076+0+0+90+0`, sağda `+0+0+0+90`). Oyun açılışta ve her ekran değişiminde ekranı ve güvenli alanı günlüğe yazar ("The screen is …"); telefon testinde yan paylar oradan okunur (T460, T465).
- **Doğrulama:** 5.376 test. Ekonomi denetimi temiz. Unity'de `RotationCheck` (84 değişiklik), `PlayModeCheck`, `MusicCheck`, `EngineSmokeTest` hatasız; giriş sayfası (açılamayan hâli de) ve salon 1080×2424, 2732×2048 ve yan Fold'da bakıldı. APK'da etkinlik dik, kategori oyun, hedef API 36.

**v1'in ürünleri istemci için (2026-10-24).** Gerçek parayla satılacak dört altın paketi ve Çırak Çantası `economy/default.json`'un `store` bölümünde (§19, §25): her ürünün ABD fiyatı (sent), altını, booster'ları ve ürün kimliğinin uygulama kimliğinden sonraki kısmı (`coins.pouch` → `com.saturnwave.puzzle.coins.pouch`). İstemci bu bölümü henüz okumuyor; `tools/unity/economy-pack.js` onu pakete koymuyor, `Economy.json` değişmedi. Mağaza sayfası üç paket sütunu için çizildi (`ShopLayout`: panel 362, dört sütun 449 nokta); satışı açan aşama dört paketi ve Çırak Çantası'nı yerleştirmeli, fiyatı mağazanın yerel yazısıyla göstermeli.

**Gerçek satın almalar (2026-10-24).**

- **Paket:** Unity In-App Purchasing **5.4.4** (`com.unity.purchasing`), Unity'nin kendi paket kaydından, sürümü sabit; lisans Unity Companion Package License v1.0. Yanında `com.unity.services.core` 1.18.0, `com.unity.services.deployment` 1.7.2 ve `deployment.api` 1.1.3 gelir. Unity Gaming Services başlatılmaz, bulut projesi yok; ürünler kodda tanımlı (dört paket tüketilir, Çırak Çantası tüketilmez). IAP `Assets/Resources/BillingMode.json`'u yazdı (`GooglePlay`).
- **Ne gönderir:** 5.4.4 her satın alma adımında fişi, ürünü, fiyatı, cihaz modelini, dili ve iOS'ta IDFV'yi Unity'nin analiz adresine yollar; kapatma ayarı yok ve oyun bunu yamamaz (araştırma `iap-and-analytics.md` §3.4). Android'de INTERNET bu aşamada hâlâ silinmiş (`AndroidBuild.Permissions` değişmedi), olaylar telefondan çıkamaz; iOS'ta çıkar. Gizlilik metni sonraki aşamada (taslak `docs/research/privacy-v1.md`); bugünkü onaylı metin "satın alma yoktur" diyor.
- **Manifeste gelenler** (denemenin ölçümü, aynı belge §3.1): `com.android.vending.BILLING`, `ACCESS_NETWORK_STATE`; Google Play ödeme hizmetinin `queries` kaydı; `ProxyBillingActivity`, `ProxyBillingActivityV2`, `GoogleApiActivity`; `datatransport` servisi ve alıcısı. Konum izni yok. APK yaklaşık 8 MB büyür.
- **Ekonomi paketi biçim 4:** `tools/unity/economy-pack.js` `store` bölümünü pakete koyar (`EconomyFile.store`, `Puzzle.Meta.StoreCatalogue`); `shop.coinPacks` örnek paketleri ayarlardan, simülatörden ve oyundan kalktı. `build.realMoneyPurchases` artık `"store"`; `"notAvailable"` test derlemesi için durur. Yukarıdaki "v1'in ürünleri istemci için" paragrafındaki "henüz okumuyor" bu aşamayla bitti.
- **Satın almanın yolu (`Checkout`, Unity'siz, testli):** boşta → soruyor (onay kartı) → bekliyor (mağazanın ödeme sayfası) → verildi, vazgeçildi, olmadı ya da onay bekliyor; kart kapanınca boşta. Kapalı küme (`PurchaseState`); yola uymayan istek (basış ile mağazanın cevabı çakışırsa) bırakılır. Mağazanın haberleri her adımda alınır, bölümdeyken de: onaylanan bekleyen ödeme o an verilir, kartı müzeye dönünce iner.
- **Bir kez teslim:** `WalletLedger.Deliver` altını ve booster'ları cüzdana, ödemenin kimliğini ilerlemeye tek değişiklikte koyar; oturum kaydeder, mağazaya onay (`ConfirmPurchase`) ancak kayıt tutunca gider. Arada oyun kapanırsa mağaza ödemeyi yeniden bildirir: kayıt tutmuşsa oyun onu verilmiş bulur ve yalnız onaylar, tutmamışsa bir kez verir. `PurchasesMade` her ürünün kaç kez alındığını ve son 64 ödemenin kimliğini tutar; Çırak Çantası'nı oyuncu başına bir keze bağlayan sayı da bu. İlerleme dosyası 6. düzende (`purchases` bloğu); eski düzenler "hiç alınmamış" okunur.
- **Mağaza arabirimi (`IStore`):** oyunda `UnityStore` (IAP 5.4.4 `StoreController`: bağlan, ürünleri al, mağazanın yerel fiyat yazısı, satın al, bekleyen ödemeyi onayla, iOS'ta geri yükle; oyuncunun sahip olduğu Çırak Çantası mağaza açılınca bildirilir), araçlarda ve testlerde `ScriptedStore` (ABD fiyatları "$1.99"; öde, vazgeç, reddet, onay bekle ve yeniden bildir senaryoları). Fiyat mağazanın yazısıdır ve `decimal` tutarıyla gelir; oyun fiyat yazmaz, ABD fiyatından da hesaplamaz.
- **Mağaza sayfası:** dört paket bir sırada, her sütun dörtte üç boyda (düğmenin yüksekliği aynı); düğmede mağazanın yerel fiyatı (tasarımdaki "[fiyat]"). Mağaza cevap vermeden "…" (sönük), satmadığı paket "Satışta değil" (sönük); ulaşılamıyorsa düğmelerin yerinde sakin satır "Mağaza şu anda açılamıyor", oyuncu mağazadayken 15 saniyede bir yeniden sorulur. Test derlemesinde düğmeler "Yakında" der (eski kart). Çırak Çantası 20. bölümden alınana kadar ilk paketin (kese) yerinde: çerçevede altın yığını ve üç booster, üst kenarda "BİR KEZ", altında 1.500, düğmesinde fiyat (§19). iOS'ta altın panelinin altında "Satın alımları geri yükle".
- **Kartlar** (`NoticeCard`'ın yeni cevap türleri `BuyOrDecline` ve `None`): onay kartı (ürün adı, içerik, "₺… karşılığında alınsın mı?", "Ödemeyi mağaza alır; ödeme bilgin oyuna gelmez.", mavi "Vazgeç", yeşil "Satın al"); "Ödeme sürüyor" (düğmesiz, mağaza cevap verince kalkar); "Satın alındı" (gelenler, "1.000 altın cüzdanında.", "Teşekkür ederiz." ya da booster'ların nerede kullanıldığı); "Satın alma olmadı" (neden ve "Ödeme alınmadı."); "Onay bekleniyor"; "Geri yükleme" (ne geldi; "Altın paketleri geri gelmez; yalnız Çırak Çantası gelir."). Mağazanın sayfasında vazgeçilen ödemeye kart yok. Tasarım tuvali bu kartları çizmiyor; bildirim kartının dilinde yapıldılar.
- **Test modu:** kural aynı (yalnız parayla satmayan derlemede). Araçlar test modunu `StoryStudio.TestBuildEconomy` ile (paketin `notAvailable` hâli) yürür.
- **Yazılar:** 35 yeni yazı 13 dilde (makine çevirisi, `tr` kaynak ve çevirmen notlu). Çince serif yedek yazı tipinin resmi 2048×2048 oldu; yeni harfler 2048×1024'e sığmadı.
- **Denetim (`PurchaseCheck`, `-quit` olmadan):** önce oyunun oturumu üzerinde betikli mağazayla yürüyüş (`GameWalk.Purchases.cs`, resimler `walk-purchase-*`): Çırak Çantası ve dört paket birer kez verilip kayıttan sonra onaylanıyor, vazgeçme kartsız ve boş, ret kartlı, onay bekleyen ödeme sonra geliyor, ikinci Çırak Çantası mağazaya gitmeden reddediliyor, ödeme ile teslim arasında ve kayıt ile onay arasında kapanan oyun ödemeyi bir kez veriyor, iOS'ta geri yükleme Çırak Çantası'nı bir kez getiriyor, ulaşılamayan ve cevap vermeyen mağaza. Sonra oyun kipinde IAP 5.4.4 ve Unity'nin sahte mağazası: UGS'siz bağlanma, beş fiyat, beş ürün birer kez verilip kayıttan sonra onaylandı, iptal boş, ikinci Çırak Çantası reddedildi, yeni kurulumda Çırak Çantası açılışta bir kez geri geldi. Sahte mağazanın kendi ödeme penceresine "Buy" ya da "Cancel" ile cevap verilir; Windows Editor'da IAP geri yükleme düğmesini reddeder, denetim bunu söyler. `-purchaseCheckBreaksTheSave` ile (kaydı tutmayan depo) denetim düşüyor: "the saved progress holds 200 coins and nothing bought".
- **Doğrulama:** 5.457 test (49'u yeni), betik testleri ve ekonomi denetimi temiz (100. bölüm 8. gün). Unity'de `PurchaseCheck`, `RotationCheck` (84 değişiklik), `PlayModeCheck` (oyun açılışta sahte mağazayı beş fiyatla açtı, hata yok), `MusicCheck` hatasız; `ScreenShots -screens shop -screenWalk` bütün kart yürüyüşleriyle (test modu ve satın almalar dahil) 1080×2424, 2076×2152 ve 2732×2048'de geçti, resimlere bakıldı. Telefon testleri T470–T489; cihazda denenmedi.

**Oyun istatistikleri ve rıza (2026-10-24).** §19'daki kararın uygulaması; metinler `docs/research/privacy-v1.md` taslağından.

- **Paket:** Aptabase'in Unity paketi **0.3.1** (`com.aptabase`), resmî GitHub deposundan (`github.com/aptabase/aptabase-unity`), `0.3.1` etiketinin işlemesine sabit (`138318625c09…`); lisans MIT; bağımlılığı yok. Ayar dosyası `Assets/Resources/AptabaseSettings.asset` **boş anahtarla** gelir, çökme raporu kapalı. Anahtarı sahip koyar (`A-EU-…`).
  - Paket açılışta bu dosyayı kendisi okur. Anahtar yoksa hiçbir şey kurmaz, hiçbir şey göndermez ve günlüğe bir hata satırı yazar ("…App Key  is invalid. Tracking will be disabled"). Paket bu satırsız açılmanın yolunu vermiyor. `PlayModeCheck` ve `MusicCheck` bu satırı, ayar dosyasında anahtar yokken oyunun hatası saymaz (`KeylessStatisticsNotice`).
  - Paketin içe aktarıcısı projede ayar dosyası bulamazsa yer tutucu anahtarlı (`A-EU-0000000000`) ikinci bir dosya yazar. Bizim dosyamız orada olduğu için yazmaz; `ConsentCheck` tek dosya olduğunu denetler.
- **Kapı (`PlayStatistics`, Unity'siz, testli):** not yalnız oyuncunun yanıtı "İzin ver" iken pakete verilir. Yanıt yokken, "hayır"dan sonra ve anahtar kapanınca paket kapatılır (`SetEnabled(false)`) ve ona hiçbir şey verilmez. Kapı paketin değil oyunun kodunda: 0.3.1 kapalıyken de kuyruğa alıyor ve açılınca gönderiyor. Her çalıştırmada izin olunca önce oyunun açılışı bir kez not edilir.
- **Anahtar (`StatisticsKey`, Unity'siz, testli):** yalnız AB bölgesinin anahtarı (`A-EU-…`) gönderir. Boş dosya ve paketin yer tutucusu "anahtar yok" sayılır. Başka bölgenin (ABD, kendi sunucusu, geliştirme) ya da biçimsiz anahtar reddedilir, günlükte hata olur ve hiçbir şey gönderilmez. "Notlar AB'de sayılır" sözü böyle korunur.
- **Nereye:** anahtarlı derlemede `AptabaseNotes` (paket, dakikada bir toplu gönderim, `https://eu.aptabase.com`). Anahtarsız derlemede ve Editor'da her zaman `NotesToTheLog`: not yalnız günlüğe yazılır ("Statistics: level_start (level=12), not sent (…)"). Soru anahtarsız derlemede de sorulur, telefon testleri akışı görsün diye.
- **Notlar (`PlayNote`, kapalı küme):** `app_start`; `level_start` (`level`); `level_win`/`level_lose` (`level`, `moves_left`: deneme kesin bitince; hamle yapmadan çıkılan bölüm deneme sayılmadığı için not almaz); `painting_complete` (`painting`, `room`); `room_complete` (`room`); `shop_open` (mağazaya her girişte bir kez); `purchase_complete` (`product`: ürün kimliğinin uygulama kimliğinden sonraki kısmı, `coins.bag`). Cihaz kimliği, oyuncu kimliği, işlem kimliği, fiş, fiyat ve serbest metin yok.
- **Aptabase'in kendi ekledikleri** (paketin kaynağı, 0.3.1): zaman; yalnız bellekte duran, yeniden açılışta ya da 60 dakika boşta yenilenen oturum numarası; oyun sürümü ve derleme numarası (boş); işletim sistemi ve sürümü; yerel ayar (`CultureInfo.CurrentCulture`); geliştirme derlemesi mi; istek başlığında "işletim sistemi/sürüm yerel ayar". Sunucu IP'den ülke ve bölge çıkarır, IP'yi saklamaz.
- **Soru kartı (`ConsentCard`):** salonun üstünde, oturum müzede açılırken ve yanıt yokken, karanlık salonda iner. Plakada "Oyun istatistikleri", altında soru ve üç paragraf. "İzin verme" ve "İzin ver" aynı boyda ve aynı mavide; yuvarlak düğme yok; geri tuşu "İzin verme". Kart yanıtlanmadan arkasındaki hiçbir şey basılmaz. Tasarım tuvali bu kartı çizmiyor; ayarlar kartının dilinde yapıldı, sahibin bakıp yeniden çizeceği iş.
- **Ayarlar:** dördüncü anahtar "Oyun istatistikleri" (`DeviceSetting.Statistics`, iPad'de üçüncü). İşareti yükselen üç çubuk; tuvalde yok, tuvalin ayar işaretlerinin elle çizilmiş benzeri (`art/unity/icon-statistics.svg`). Gizlilik satırı anahtara göre değişir: "Telefondan yalnızca satın almalar çıkar" ya da "Satın almalar ve kimliksiz istatistikler çıkar". Uzun dilde satır küçülür, okun önünde durur.
- **`device.json` 6. düzen:** `settings.statistics` ("allowed", "declined"; boş: sorulmadı). Eski düzen ve bilinmeyen sözcük "sorulmadı" okunur; soru yeniden gelir, hiçbir şey gitmez.
- **Gizlilik kartı:** taslağın sekiz paragrafı (§20). Metin bir karta sığmadığı için sayfalara bölünür (`TextPages`, Unity'siz, testli): paragraf bölünmez, her sayfa en yüksek sayfa kadardır. Altta oklar ve "1 / 3"; kayıtların sayısı, dosyası ve "Kayıtları sil" yerinde kalır. Onay kartının ikinci satırı "Ödemeyi mağaza alır; satın alma bilgisi Unity'ye de gider." oldu. Sitedeki politikaya bağlantı yok; sitenin adresi bekleniyor.
- **Android izinleri:** `AndroidBuild.Permissions` INTERNET'i istiyor. Derleme, Unity'nin bir önceki derlemeden tuttuğu kütüphane manifestini siler (denemenin tuzağı: silinmiş izin eski dosyada kalıyordu). Derlemeden sonra APK'nın izinleri `aapt dump permissions` ile okunur (`PackagePermissions`, Unity'siz, testli). Tam olarak `VIBRATE`, `INTERNET`, `com.android.vending.BILLING`, `ACCESS_NETWORK_STATE` ve Unity'nin alıcı izni değilse derleme durur; `AD_ID` ya da konum gelirse de durur.
- **Yazılar:** 13 yeni, 5 değişen yazı, 13 dilde (makine çevirisi, `tr` kaynak, notlar "taslak, sahibin onayını bekliyor"). Rusça, Japonca ve Çincede Latin harfiyle yalnız adlar kalır (Aptabase, Unity, Google Play, App Store, Apple, iPhone, iPad, ID, IP). Çince sans yedek yazı tiplerinin resmi 2048×2048 oldu; yeni harfler 2048×1024'e sığmadı.
- **Denetim (`ConsentCheck`, `-quit` ile):** oyunun oturumunda, kaydedici paketin yerinde. İlk açılışta soru iniyor ve kapalıdan başka bir şey duyulmuyor. Geri tuşu "hayır" kaydediyor; sonra mağaza ve sonuna kadar oynanan bölüm hiçbir şey göndermiyor. "İzin ver" sonrası sıra açık, `app_start`, `shop_open`, `purchase_complete` (ürünüyle), `level_start`, bölüm sonu (numarası ve kalan hamlesiyle); hiçbir notta kimlik yok. İzin vermiş oyuncunun sonraki açılışında soru yok, açılış not ediliyor. Anahtar kapanınca hemen kapalı ve sonra hiçbir şey; gizlilik satırı geri dönüyor. Gizlilik kartının sayfaları sona ve geri dönüyor. Ayar dosyası tek, anahtar boş ya da AB, çökme raporu kapalı.
- **Doğrulama:** 5.505 test (48'i yeni), ekonomi denetimi temiz (100. bölüm 8. gün). Unity'de `ConsentCheck` geçti. Türkçe metin gizlilik kartında 3 sayfa (Almancada 4, Japoncada 5); kart artık en çok 470 nokta (eskiden 382). Bu yükseklik, test edilen her ekranda üç ve daha çok anahtarlı ayar kartının önüne sığıyor; artık her derleme en az üç anahtar çiziyor. `RotationCheck` (84 değişiklik), `PlayModeCheck` (giriş sayfasından sonra salonda soru kartı; anahtarsız paketin satırı sayılmadı, oyun hatası yok) ve `MusicCheck` (soruyu "İzin verme" ile cevapladı) hatasız. `ScreenShots -screens consent,settings,privacy` 1080×2424, 2076×2152 ve 2732×2048'de, Almanca ve Japonca telefonda; `-screenWalk` üç ekranda geçti. Resimlere bakıldı. APK derlendi: eski kütüphane manifesti silindi, `aapt` APK'nın tam olarak INTERNET, VIBRATE, BILLING, ACCESS_NETWORK_STATE ve alıcı iznini istediğini okudu (290 MB). Telefon testleri T490–T499; cihazda denenmedi.

**Ressam oyunda (2026-10-24).** §22'deki seçimin uygulaması; kararlar §19'da, telefon testleri T500–T509 (F26), açık A49.

- **Resimler:** `art/avatar/` işleyicinin adlarıyla kaynaklar ve lisans kaydı (`licences.json`: her resmin özeti, kaynaklar, kredi satırları, MIT metni; `credits.md` işleyicinin kendi notu, olduğu gibi). Oyun aynı baytları İngilizce adlarla taşır: `unity/Assets/Art/Avatar/Resources/Painter/` (`painter-with-palette`, `painter-waving`, `painter-cheering`, `painter-bust-guiding`, `painter-bust-thinking`, `painter-bust-joyful`). Eşleme kaydın `inGame` alanında; oyunun kodu Türkçe sözcük taşımaz (`WordsOutsideTheTablesTests`), testler iki kopyanın özetini kayıtla karşılaştırır.
- **İçe aktarma (`AvatarArtImport`):** sprite, mipmap yok, saydam piksel komşusunun renginde (saç ve sakal kenarında koyu hale yok). Ayakta resimler metreyle ölçülür (resmin 1100 pikseli bir metre) ve ayaklarından döner: üç poz aynı ölçekte, ayaklar aynı çizgide. En çok 1024 piksel, telefonda ASTC 6x6 (yaklaşık 0,3 MB). Büstler 100 birim genişlikte, ortadan döner; en çok 256 piksel, telefonda ASTC 4x4 (64 KB).
- **Yükleme (`PainterGallery`, `PictureShelf` kurallarıyla):** bir resim onu gösteren ekran isteyince yüklenir, ekran kalkınca hemen bırakılır; bırakılan tutulmaz. Tablolarla aynı yoldan her ekrana gider (`PaintingGallery.Painter`).
- **Salon (`HallLayout`, Unity'siz, testli):** ressam tezgâhın sağında, ayakları tezgâhın ayak çizgisinde. Boyu tezgâhın çiziminin 0,92'si (1080×2424 telefonda 150 nokta, 450 piksel), ama oda onarıldığında kalkan kolları pirinç levhaya değmeyecek kadar; ekranın kenarından 4 nokta içerde, sığmazsa masanın ucunun biraz önünde durur. Hediye çerçevesi aynı yükseklikte tezgâhın soluna aynalandı. Henüz bölüm kazanmamış oyuncuya el sallar. Yatay tabletlerde ortadaki sütunda kalır: kanatlar odanın tablolarının.
- **Onarılan oda:** ressam iki kolu havada pankartın sağında. Pankart ortada kalır; ressam yer bırakmazsa yalnız gerektiği kadar sola kayar, soldaki duvar yetmezse küçülür (1080×2424'te tam boyunun %84'ü). Geniş ekranlarda ortada kaldığını test denetler (ilk denemede yatay iPad'de kanadın tablosunun üstüne kaymıştı).
- **Rehber yazıları (`ShelfPill`):** adımın yazısının solunda küçük yaldız çerçevede büst (`PainterPortrait`: hediye çerçevesinin kadifesi ve çerçevesi). Çerçeve yazı kartı kadar yüksek, en az 44 nokta; ikisi birlikte ortalanır, yazının yeri çerçeve kadar daralır. Bakış adıma göre (`PainterPictures.MoodOf`, testli): hedef, hamleler, dörtlü, beşli, ilk parça, booster kullanımları ve başlangıç booster'ları yol gösteren; sandık, vernik, bant, çerçeve köşesi ve uzun basma düşünen; ilk tablonun tamamlanması sevinen. Tahtada, yapbozda ve bölüm başı kartında aynı; adım bitince büst bırakılır.
- **Kazanma kartı:** nişin duvarında sol üstte 60 noktalık çerçevede sevinen büst, parçadan sonra belirir; o köşedeki büyük kıvılcım çıkarıldı. Kaybetme kartında büst yok.
- **Tablo tamamlandı (`JigsawLayout.CheeringPainter`, testli):** ressam tepsinin sağ ucunun önünde, ayakları alt kenarında; boyu ellerinin tepsinin üstündeki yazıya değmeyeceği kadar, en çok 166 nokta (1080×2424'te yaklaşık 105). Işıkla birlikte aşağıdan yükselip belirir; ödül kuyunun onun solundaki kısmında ortalanır. Yalnız tablo bu ekranda tamamlanınca; tamamlanmış açılan tabloda yok.
- **Künye:** Ayarlar'ın yönlendiren satırlarının sonuncusu "Künye" (koleksiyon işareti). Kart (`CreditsCard`) gizlilik kartı gibi sayfalı (Türkçede 4 sayfa); başlık ilk satırıyla aynı sayfada kalır, satır bölünmez. Oklar artık iki kartın ortak parçası (`PageArrow`), altta "Tamam". Lisans satırları `CreditWords`'te İngilizce, yazı tablolarının dışında; testler `art/avatar`, `art/fonts`, `art/paintings` ve `art/sounds` kayıtlarıyla karşılaştırır, oyunun harfleri dışında harf yok ("©" yerine "(c)"). Ayar kartı bir satır (53,5 nokta) uzadı; test edilen en kısa ekranlarda da satırlar 44 noktanın üstünde kalıyor.
- **Yazılar:** 6 yeni yazı 13 dilde (makine çevirisi, `tr` kaynak ve notlu): "Künye", ilk satır ve dört başlık. Çince, Japonca ve Korece yedek yazı tiplerine yeni harfler eklendi.
- **Çizim sırası:** salondaki ressam tezgâhın ve parçasının üstünde (`HallPainter`), tepsideki ödülün üstünde ve yazısının altında (`TrayPainter`); çerçeveli büstün üç parçası (kadife, büst, çerçeve) rehber kartının, bölüm başı kartındaki rehber kartının ve kazanma kartındaki rozetlerin üstünde kendi yerlerinde (`PortraitPart`, testli).
- **Denetim (`AvatarCheck`, `-quit` ile):** 1080×2424'te yeni oyuncunun ve 47. bölümdeki oyuncunun salonu (mağazaya geçip dönerek), onarılan oda, üç bakışıyla rehber kartı, kazanma kartı ve düğmeyle tamamlanan ilk tablo; her adımdan sonra bellekteki ressam resimleri sayılır: ekranın gösterdiği tam olarak o, ekran kalkınca hiçbiri. 22 sayımın hepsi geçti.
- **İlk içe aktarma:** yeni bir doku işleyicisi Unity'nin bütün dokuları bir kez yeniden içe aktarmasına yol açtı (yaklaşık 25 dakika). iOS modülü kurulu olduğu için Unity bu sırada 254 dokunun `.meta` dosyasına varsayılan (geçersiz kılınmamış) bir iOS bölümü yazdı; ayrı bir işlemede.
- **Doğrulama:** 5.839 test (334 yeni), ekonomi denetimi temiz (100. bölüm 8. gün). Unity'de `AvatarCheck`, `RotationCheck` (84 değişiklik), `PlayModeCheck` ve `MusicCheck` (`-quit` olmadan) hatasız; telefonda bütün kart yürüyüşleri (künye kartının sayfaları dahil) geçti. Resimler 1080×2424, 2076×2152, 2152×2076, 2732×2048, 2048×2732 ve 2560×1600'de bakıldı.

**v1 incelemesinin düzeltmeleri (2026-10-25).** Satın alma, izin ve ressam işinin incelemesi çift teslim ve izinsiz giden veri bulmadı; bulduğu on açığı kapattık. Her biri bir birim testiyle ya da Editor denetimiyle tutuluyor. Telefon testleri T516–T519 eklendi; T470, T471, T482, T490, T496 ve T508 değişti (F24, F25, F26). Açık A51.

- **Kaydı tutamayan açılış satmaz:** ilerleme deposu artık oyun kapandıktan sonra da tutup tutmadığını söylüyor (`IProgressStore.Keeping`: `AfterClosing` ya da `ForThisRunOnly`). İlerleme dosyası okunamazsa ya da onu daha yeni bir sürüm yazmışsa açılış ilerlemeyi yalnız bellekte tutar (`ProgressInMemory.ForThisRunOnly`). Böyle bir açılışta `Checkout` mağazaya hiç sormaz (`StoreStatus.ClosedForThisRun`): satmaz, geri yükleme satırını göstermez, önceki açılıştan bekleyen bir ödemeyi ne verir ne onaylar. Ödeme mağazada onaysız kalır; kaydedebilen sonraki açılış onu bir kez verir ve onaylar. Mağazada düğmelerin yerinde sakin bir satır var: "Mağaza bu oturumda kapalı". Araçların bellekteki deposu ilerleme dosyasının yerine geçtiği için "tutar" der.
- **Onayı almayan mağaza:** `UnityStore` bekleyen siparişi, IAP onayın bittiğini söyleyene (`ConfirmedOrder`) kadar elinde tutar. Onay alınmazsa (`FailedOrder`) oyuna `StoreNews.NotConfirmed` bildirir; sebep "bağlı değil" ise mağazayı ulaşılamaz da sayar. `Checkout` ödemeyi yeniden onaylanacaklara koyar ve mağaza bir sonraki açılışında onayı yeniden gönderir. Mağaza kopmuşsa ve onay bekleyen bir ödeme varsa, oyuncu nerede olursa olsun mağaza 15 saniyede bir yeniden sorulur. Açık bir mağazanın reddettiği onay bir sonraki açılışa kalır; sonsuz deneme yok. IAP bekleyen bir siparişi aynı oturumda yalnız bir kez yeniden bildirdiği için siparişi oyun elinde tutuyor.
- **"Ödeme sürüyor" kartının çıkışı:** mağaza ödeme sayfasına 30 saniye cevap vermezse satın alma `PurchaseState.Unanswered` olur. Süre oyunun kendi zamanıyla sayılır; oyun mağazanın sayfası arkasındayken bu süre işlemez. Sayfa açıkken bağlantı koparsa da aynı şey olur. Kartta aynı plaka, "Mağazadan henüz yanıt gelmedi.", "Ödeme bitince aldıkların gelir." ve "Tamam" var. Kart kapanınca satın alma yolu boşa döner. Ödeme sonradan gelirse yine bir kez verilir ve kartı iner. Mağazanın, oyunun satmadığı (ya da adını bozduğu) bir ürün için verdiği vazgeçme ya da ret sayfanın cevabı sayılır. Oyunun başka bir ürününe ait cevap sayılmaz: o, oyun kapanmadan önce başlamış bir satın almadır.
- **Bağlantı koparken "Ödeme alınmadı" yok:** kart eskiden "Satın alma olmadı / Mağaza şu anda açılamıyor. / Ödeme alınmadı." diyordu. Ödemenin alınıp alınmadığını oyun bilmediği için artık yukarıdaki kart iniyor.
- **Kendiliğinden geri gelen Çırak Çantası:** mağazanın açılışta verdiği ürün (Google Play'de yeniden kurulumdan sonra) bir ödeme değil, geri yüklemedir (`DeliveryReason.Restore`). Kart "Geri yükleme / Çırak Çantası geri geldi. / 1.500 altın cüzdanında." der. İstatistiklere `purchase_complete` notu gitmez; ayrı bir not da yok, gizlilik metnindeki liste aynı kaldı.
- **Ulaşılamayan mağazada yanıp sönme yok:** mağaza yeniden sorulurken durum "ulaşılamaz" kalır, düğmeler "…"ya dönmez. Cevap gelince fiyatlar görünür ya da sakin satır yerinde kalır.
- **Gizlilik metni ve soru:** istatistik paragrafı ve sorunun ilk paragrafı, Aptabase'in bağlantıdan yaklaşık konumu (ülke ve bölge) çıkardığını söylüyor. Bu, §20'deki Data safety tablosunun ve iOS dalındaki App Store etiketinin beyanıyla aynı. 13 dilde; taslak, sahibin onayını bekliyor.
- **Künye'de yazılım:** yeni "Yazılım" başlığı ve ikinci bir MIT metni geldi. Aptabase SDK 0.3.1 (Copyright (c) 2023 Aptabase) ve Json.NET (`com.unity.nuget.newtonsoft-json` 3.2.2; James Newton-King, SaladLab, Kalle Jillheden ve Mike Wuetherick) MIT ile yazılı. IAP 5.4.4'ün bildirimlerinde Apache 2.0 olarak geçen Google Play Billing Library 9.0.0 ve Apple StoreKit 2 adıyla, telif satırıyla ve lisansın adresiyle yazılı. Kayıt `unity/Packages/licences.json`'da. Testler kartı kayda, kaydın sürümlerini de `packages-lock.json`'a bağlıyor. Yalnız Editor'da kullanılan paketler listede yok. Açık iki soru var: Apache metninin bütünü gösterilmeli mi, URP'nin FXAA bildirimi (BSD benzeri) derlemeye giriyor mu.
- **"Test sürümü" yalnız vekil derlemede:** mağazadan satan derlemede giriş sayfasının altı "Sürüm 1.0", ayarların satırı "Bu sürüm · reklamlar canlandırma, satın alma mağazadan" der. Satış yapmayan derleme eskisi gibi "test sürümü" der. Giriş sayfası bunu ekonomiyi okuduğu açılış adımında öğrenir; o ana kadar yalnız sürümü yazar. Son ifade sahibin.
- **Yorumlar:** `UnityStore`'un açıklaması INTERNET izninin kaldırıldığını artık söylemiyor. `economy/default.json` Çırak Çantası'nı "mağazanın başında" değil, kesenin yerinde (§19) anlatıyor.
- **Yazılar:** 5 yeni yazı (sakin satır, cevap gelmedi, sürüm, mağaza derlemesinin satırı, "Yazılım") ve 2 değişen yazı, 13 dilde. Çince, Japonca ve Korece yedek yazı tiplerine yeni harfler eklendi.
- **Yürüyüşler:** `GameWalk.Purchases` dört yürüyüş kazandı: cevapsız sayfa ve kopan bağlantı (`walk-purchase-silent-*`), kaydı tutamayan açılış (`walk-purchase-unsaved-*`) ve Google Play'in kendiliğinden geri verişi (`walk-purchase-givenback-*`). `ScreenShots -screens purchases` bunları her ekranda resmeder.
- **Doğrulama:** 5.861 test (22'si yeni), ekonomi denetimi temiz (100. bölüm 8. gün). Unity'de `PurchaseCheck` (`-quit` olmadan, dört yeni yürüyüşüyle), `ConsentCheck` (gizlilik kartı Türkçede artık 4 sayfa), `AvatarCheck` (22 sayım), `RotationCheck` (84 değişiklik), `EngineSmokeTest`, `PlayModeCheck` ve `MusicCheck` (`-quit` olmadan) hatasız. `ScreenShots -screens purchases,consent,privacy,credits,entry,settings` 1080×2424 ve 2732×2048'de 88 resim verdi, ekran kenarını aşan satır yok. Resimlere bakıldı: kapalı mağaza, düğmesiz ve "Tamam"lı ödeme kartı, geri yükleme kartı, soru kartı, gizlilik kartının istatistik sayfası, künyenin 6 sayfası, ayarların satırı. APK derlendi: 290 MB, versionCode 1, minSdk 26, targetSdk 36, dik, kategori oyun. `aapt` tam olarak INTERNET, VIBRATE, BILLING, ACCESS_NETWORK_STATE ve Unity'nin alıcı iznini okudu; `AD_ID` ve konum izni yok. Cihazda denenmedi.

## 24. Bölüm bankası ve derleyici

**Karar (2026-10-08).** Zorluk ayarının kalıcı çözümü: üretilen tahtalar atılmaz, ölçümleriyle birlikte saklanır; oyunun 1000 bölümü bu bankadan bir **politikayla** seçilir. Kullanıcının isteği: "kalıcı bir çözüm; üretilen bölümlerin çoğunu harddiske kaydedebiliriz". §8 ve §9'daki üretici (`generate`) duruyor; derleyici kendini kanıtladıkça onun yerini alır.

**Neden.** Üretici her bölüm numarası için aday çizer, tek bir botla oynatır ve limiti o botun hedefine göre koyup gerisini atardı. Oyuncuyu temsil eden bot, pay, hamle payı ya da çeşitlilik değişince 1000 bölüm baştan üretiliyordu (40 dakika; tahtalar değişiyor; eski tahtalardaki kayıtlar boşta kalıyor).

**İki parça**

| | Banka | Derleyici |
|---|---|---|
| Ne yapar | Bölüm numarasına bağlı olmayan aday tahtaları çizer, merdivenin 15 botuyla limitsiz oynatır, oyunlarıyla saklar | Politika ve her numaranın planıyla bankadan tahta seçer, limiti ve tohum havuzunu saklı oyunlardan okur, yeni oyunlarla denetler, bölümleri yazar |
| Maliyet | Pahalı, rastgele, bir kez (aday başına yaklaşık 2 saniye) | Ucuz, bilinçli, istenildiği kadar |
| Ne zaman geçersiz olur | Yalnızca motor ya da bot kuralı değişince (yeniden ölçülür) | Hiç; politika değişince yeniden çalışır |

**Kavramlar**
- **Aday:** numarası, kademesi, limiti, havuzu ve renk adları olmayan bölüm içeriği. Kimliği içeriğinin özetidir; herhangi bir bölüm dosyasının arkasındaki aday aynı yolla bulunur.
- **Tahta ailesi:** aynı şekil ve engel aileleri, farklı miktarlar. Bir aile bir derlemede en çok bir kez kullanılır. Miktarlar 1,25 oranlı bir ızgaranın adımlarıdır; her miktar ayrı ölçülür, çünkü bot hedefe göre oynar.
- **Plan ve politika:** bir bölüm numarasının ne olduğu (kademe, boyut, renk sayısı, mekanikler, hamle aralığı) plandır ve `profiles/default.json` içinde kalır. Kimin için ve ne kadar zor olduğu politikadır (`policies/default.json`): oyuncu (bir basamak ya da basamakların karışımı), kademe başına ilk deneme payı, hamle payı, tür hedefleri, sabitlenen numaralar, kayıtlı bölümlere davranış, denetim boyutları.
- **Seçim:** plana uymak şarttır (boyut, renk sayısı, açık mekanikler, öne çıkarılan mekanik gerçekten tahtada). Uyanlar arasında maliyet: limitin hamle aralığının ortasına uzaklığı, tür hedeflerinden açık, komşu bölümle aile tekrarı. Yerinde duran ve hâlâ uyan bölüm değişmez.
- **Sabitleme ve kayıtlar:** `pinned` numaralar diskteki hâliyle, bayt bayt kalır (ilk oda). Üzerinde oyuncu kaydı olan bölüm varsayılan olarak tahtasını korur (`recorded: keepBoard`); yalnızca limiti ve havuzu politikayı izler.
- **Denetim:** çok adayın en iyi görüneni şanslı ölçümdür; seçilen bölüm politika oyuncusuyla, kendi havuz tohumlarında yeniden oynatılır (oyuncu yalnızca havuz tohumlarını oynar; herkesin ilk denemesi havuzun ilk tohumudur). Rapordaki pay bu denetimden sonraki oyunlardan okunur.
- **Damga ve dönem:** her kayıt hangi motor ve bot sürümüyle ölçüldüğünü taşır (dönem). Her derlenen bölüm hangi kurallarla karar verildiğinin damgasını taşır; damga değişmedikçe yerinde kalır. Kural değişince `bank remeasure` eksik oyunları oynatır.
- **Açıklık:** aralığa oturmayan, eskimiş ya da hedefinden sapan bölümler özet ve dökümde adıyla yazılır; `promote` bunları ancak açıkça kabul edilirse yayına alır.

**Depolama.** Varsayılan `D:\puzzle-bank` (seçenek ya da ortam değişkeniyle verilir; depoya girmez). Kayıtlar yalnızca eklenir; her kayıt sağlama toplamı taşır; yarım kalmış son kayıt atlanır; aynı anda tek yazıcı. Aday başına yaklaşık 37 KB (oyunlarıyla). `bank verify` saklı oyunları yeniden oynatıp bayt bayt karşılaştırır.

**Deneme (2026-10-08; 2.465 aday, 8,85 milyon oyun, 87 MB).** 55. basamak, 51–1000: hedef 90 / 75 / 55 / 40; bağımsız ölçüm bölümün kendi havuzunda %91,6 / %76,5 / %57,1 / %40,7; yeni tohumlarda %91,6 / %77,5 / %58,1 / %43,6. Tür dağılımı hedeflerde (yalnızca toplama %35, kutu %25, zincir %26, jöle %18), malzeme %4 (hedef %6). İlk oda her derlemede bayt bayt aynı. Sıfırdan derleme 6,6 dakika, tekrarı 14–21 saniye ve oyunsuz. Ayrıntı: DEVLOG Oturum 18.

**Yayın (2026-10-12; 5.420 aday, 14,5 milyon oyun, 151 MB).** Yayındaki 51–1000. bölümler büyüyen bankadan derlendi; ilk oda (1–50) bayt bayt aynı kaldı. 55. basamak, 51–1000: hedef 90 / 75 / 55 / 40; bağımsız ölçüm bölümün kendi havuzunda %91,4 / %76,7 / %56,6 / %40,7. Tür dağılımı: yalnızca toplama %34,8, kutu %25,1, zincir %24,1, jöle %18,3, malzeme %3,6 (hedef %6). 52 ve 53. bölümler aralığa oturan aday olmadığı için eski hâlleriyle ve "eskimiş" işaretiyle yayında. `levels/generated` her bölümün bankadaki kaydını ve karar damgasını `assembly.csv` ile `assembly.manifest` içinde taşır; sonraki derlemeler `--base levels/generated` ile yapılır, hâlâ uyan bölüm yerinde kalır. Ayrıntı: DEVLOG Oturum 19.

**Bilinen sınırlar**
- Yeni bir oyuncu basamağı ilk seferde bir yeniden üretim kadar oyun ister (tahtalar yerinde kalır); pay değişikliği dakikalar; hamle aralığı, hamle payı, tür hedefi ve ağırlık değişikliği oyunsuzdur.
- Malzeme indirme bölümleri planın hamle aralıklarına nadiren oturur; politika türe özel aralık kabul eder (`kindMoves`). Karar (2026-10-12): aralık şimdilik değişmiyor.
- Derlenen bölümler kendi havuzlarında hedefin 1–2 puan üstündedir.
- Bir dönem geri açılamaz; yeni dönem açmak adıyla onay ister.

**Komutlar** (`puzzle` = `src/Puzzle.Cli/bin/Release/net10.0/puzzle.exe`; uzun işlerde `DOTNET_PROCESSOR_COUNT=10`)

| İş | Komut |
|---|---|
| Bankanın durumu ve bütünlüğü | `puzzle bank status --bank D:\puzzle-bank`, `puzzle bank verify --bank D:\puzzle-bank` |
| Bankayı büyüt (kaldığı yerden sürer) | `puzzle bank grow --bank D:\puzzle-bank --policy policies/default.json --levels 51-1000 --also-aim skill-45,skill-60,skill-65 --spare 3.0 --minutes 480` |
| Var olan bölümleri bankaya al | `puzzle bank import ...` |
| Kural değişince yeniden ölç | `puzzle bank remeasure --bank D:\puzzle-bank --in-use levels/generated --new-epoch` |
| Derle (deneme klasörüne) | `puzzle assemble --policy policies/default.json --base levels/generated --bank D:\puzzle-bank --out out/assemble/<ad>` |
| İki bölüm kümesini karşılaştır | `puzzle compare levels/generated out/assemble/<ad> --from 51` |
| Yayına al (sunucu durmuşken) | `puzzle promote out/assemble/<ad> [--accept <numaralar>] [--dry-run]` |

## 25. Ekonomi simülatörü

**Karar (2026-10-12).** Fiyatlar tahmin edilmez (§19); simülatörden okunur. Kullanıcı ilk varsayımları onayladı: ücretsiz ortalama oyuncu 100. bölüme 10–14 günde gelsin; günde 3 oturum; reklamla "+5 hamle" günde 3 kez ücretsiz; booster hem coin'le alınır hem ödül olarak gelir. Hepsi ayar dosyasındadır, hiçbiri kodda sabit değildir.

**Üç parça**

| Parça | Ne yapar |
|---|---|
| Ölçüm (`puzzle economy measure`) | Beceri karışımındaki botlar her bölümü kendi tohum havuzunda limitin ötesine kadar oynar: düz, ve tahtaya özel taş koyan iki bölüm öncesi booster'la. Her oyunun kaç hamlede bittiği saklanır. 862.512 oyun, dört iş parçacığında yaklaşık 13 dakika, 11 MB (`out/economy/measurement.jsonl`, depoya girmez). "+3 hamle" ve "+5 hamle" ayrıca oynanmaz: saklı oyunun üstünde aritmetiktir |
| Ayar dosyası (`economy/default.json` ve yanında tek alanı değiştiren yedi değişken: `sessions-6-minutes`, `sessions-20-minutes`, `sessions-60-minutes`, `one-life`, `life-every-16-hours`, `no-offers`, `extra-moves-1800`) | Oyuncu alışkanlığı ve ekonomi: can, ödüller, şölende hamle başına coin, fiyatlar, oyuncunun "+5"i ve booster'ı ne zaman istediği. Her alanın birimi ve kaynağı yazılıdır: karar, onaylanan varsayım, öneri, yer tutucu, kayıt |
| Simülasyon (`puzzle economy run`, `compare`) | Binlerce sanal oyuncuyu gün, oturum, can, coin ve booster üzerinden yürütür; oyun oynamaz, saklı oyunlara bakar. İki ayar dosyasını yan yana koyar |

**İlk sonuçlar (2026-10-13; ilk varsayımlarla: 20 dakikalık oturum, kalan hamle başına 10 coin; 4.000 ücretsiz oyuncu).** Bölüme varılan gün, ortalama oyuncu (55. basamak), günde 3 oturum:

| Oturum tavanı | 100. bölüm | 300. bölüm | 1000. bölüm |
|---|---|---|---|
| 6 dakika | 12. gün | 39. gün | 138. gün |
| 10 dakika | 8. gün | 25. gün | 90. gün |
| 20 dakika (ilk varsayım) | 4. gün | 14. gün | 48. gün |
| 60 dakika | 2. gün | 6. gün | 22. gün |

- **Hızı oturum süresi belirliyor**, can ya da fiyat değil. 20 dakikalık oturumda canlar (5 can, 30 dakikada bir) hiç devreye girmiyor: her oturum 5 canla başlıyor, oturumların yüzde 98,6'sı süre tavanında bitiyor. 60 dakikalık oturumda oturumların yüzde 55'i canla bitiyor.
- **Hiçbir bölüm duvar değil.** Bölüm başına ortalama deneme: ilk oda 1,05, Kolay 1,05, Normal 1,15, Zor 1,43, Süper Zor 1,80.
- **Tekliflerin etkisi** (ortalama oyuncu, bütün bölümler): limitte kazanma yüzde 73,1; "+5 hamle" ile yüzde 85,7; renk bombası +16,6 puan, "+3 hamle" +8,5, roket ve bomba +6,2.
- **Bölümlerin ayarı doğrulandı:** herkesin ilk denemesi olan ilk tohumda 55. basamak Kolay yüzde 90,5, Normal 75,8, Zor 56,2, Süper Zor 41,6 kazanıyor (hedef 90 / 75 / 55 / 40).
- **Coin tablosu oturum süresiyle tersine dönüyor.** Yer tutucu fiyatla ("+5" = 900 coin) 20 dakikalık oturumda oyuncu günde 5,1 kez "+5" istiyor (3'ü reklamla) ve coin yetmiyor; 6 dakikalıkta günde 1,7 kez istiyor ve coin birikiyor. Tek bir varsayımdan fiyat okunmaz.
- İki gerçek oyuncunun kaydı: oturum başına 5 deneme, deneme başına yaklaşık 2 dakika; bu 10 dakikalık oturuma yakındır.

**Karar (2026-10-13).** Kullanıcı önerilen ayarlarla ilerlemeyi seçti; `economy/default.json` artık oyunun ayarıdır (istemci de fiyatları buradan okuyacak).

- **Can:** 5 can ve 30 dakika kalır. Ortalama oyuncunun 100. bölüme **8. günde** gelmesi kabul edildi (ilk dilek 10–14 gündü).
- **Alışkanlık:** hesaplar günde 3 oturum ve **10 dakikalık oturuma** dayanır; iki gerçek oyuncunun kaydına en yakın alışkanlık budur.
- **Coin sayıları** (simülatörden okundu, yürürlükte; her biri ayar dosyasında "öneri" olarak işaretli ve değiştirilebilir): kalan hamle başına **4**, tamamlanan tablo başına **80**, "+5 hamle" **900**, bölüm öncesi booster'lar: renk bombası **500**, roket ve bomba **200**, "+3 hamle" **200**. Bölüm kazanmanın sabit ödülü ve günlük coin ödülü yok.
- **Yer tutucu kalanlar:** bölüm içi dört booster'ın fiyatı (ölçülmedi), yan koleksiyon parçasının fiyatı, mağazanın paketleri ve setleri, günlük reklam sayısı (§23).

**Neden 5 can.** 10 dakikalık oturumda 5 can hiçbir oturumu bitirmez; hızı yavaşlatmanın yolu oyuncuyu durdurmaktır. İlk dileği (10–14 gün) yalnızca iki ayar tutturuyor, ikisi de rahat oyuncu hedefiyle (§19) çelişiyor:

| Ayar | 100. bölüm | 300. bölüm | 1000. bölüm | Oturumlar |
|---|---|---|---|---|
| 5 can, 30 dakikada bir (seçilen) | 8. gün | 25. gün | 90. gün | Can hiçbir oturumu bitirmiyor |
| 1 can (`one-life.json`) | 10. gün | 34. gün | 121. gün | Yüzde 49'u ilk kayıpta bitiyor |
| 5 can, 16 saatte bir (`life-every-16-hours.json`) | 11. gün | 40. gün | 147. gün | Yüzde 26'sı canla bitiyor, yüzde 25'i başlayamıyor; 45. basamak 100. bölüme 17. günde geliyor |

2 can ya da 8 saatlik dolum 8. günü değiştirmiyor; denemeler arasına 50 saniyelik bekleme koymak ancak 9. güne çıkarıyor.

**Seçilen ayarlarla sonuç** (4.000 ücretsiz oyuncu; ölçüm bu derlemeyle denetlendi):

| Oyuncu | 100. bölüm | 300. bölüm | 1000. bölüm | Kese: 100. / 300. / 1000. bölümde (ortanca coin) |
|---|---|---|---|---|
| 45. basamak | 9. gün | 30. gün | 108. gün | 1.624 / 816 / 1.056 |
| 55. basamak (ortalama) | 8. gün | 25. gün | 90. gün | 3.720 / 3.756 / 6.504 |
| 65. basamak | 6. gün | 21. gün | 75. gün | 5.288 / 11.860 / 34.496 |

- Ortalama oyuncu günde yaklaşık 34 dakika oynar; 1000 bölüm yaklaşık 13 hafta sürer.
- Ortalama oyuncu "+5 hamle"yi günde 2,7 kez ister; günde 3'ü reklamla ücretsizdir. 1000 bölümde yaklaşık 205'ini reklamla, 34'ünü coin'le alır; 3'ünü alamaz. Kesesi her zaman 4–7 "+5" değerindedir.
- 1000 bölümde ortalama oyuncu şölenden yaklaşık 30.000, tablolardan 8.000 coin kazanır; yaklaşık 30.700'ünü "+5"e harcar. Bölüm öncesi booster'ı neredeyse hiç satın almaz: ödül olarak gelenler yetiyor.
- Zayıf oyuncunun (45. basamak) kesesi dar kalır ve 1000 bölümde yaklaşık 46 "+5" isteği karşılanmaz. İyi oyuncunun (65. basamak) coin'i birikir, çünkü harcayacağı yer yoktur.

**Alışkanlık yanlışsa** (aynı sayılar, ortalama oyuncu):

| Oturum tavanı | 100. bölüm | 300. bölüm | 1000. bölüm | Kese, 1000. bölümde | Ne olur |
|---|---|---|---|---|---|
| 6 dakika (`sessions-6-minutes.json`) | 12. gün | 39. gün | 138. gün | 28.884 | Reklamlar bütün "+5" isteklerini karşılar; coin birikir |
| 10 dakika (seçilen) | 8. gün | 25. gün | 90. gün | 6.504 | |
| 20 dakika (`sessions-20-minutes.json`) | 4. gün | 14. gün | 50. gün | 900 | Coin dar: 1000 bölümde yaklaşık 73 "+5" isteği karşılanmaz |
| 60 dakika (`sessions-60-minutes.json`) | 2. gün | 7. gün | 23. gün | 664 | Oturumların yüzde 63'ü canla biter; yaklaşık 163 "+5" isteği karşılanmaz |

Oyuncular kayıttakinden uzun oynarsa hız artar ve coin daralır; kısa oynarsa coin birikir. İlk gerçek veride bakılacak ilk sayı oturum süresidir.
**Hoş geldin hediyesi ve denge politikası (2026-10-14).** Kullanıcı yeni oyunculara başta biraz yardım istedi: **200 altın ve her booster'dan 3** (yedi tür, 21 booster). Reklamla altın **4** oldu (günde 3 kez). Kullanıcı sayıları kabaca yazdığını söyleyip bir denge politikası istedi; sayılar aşağıdaki kurallara bağlandı ve hepsi kurallardan geçiyor.

Hediye artık simülasyona da giriyor: simüle edilen oyuncu ilk bölümden önce hediyeyi alır, raporlarda ayrı bir gelir sütunu olarak görünür. Karşılaştırma için `economy/no-welcome-gift.json` hediyeyi çıkarır.

*Sabit ilişkiler.* Ayar dosyasına bakan testler bunları tutar (`EconomyBalancePolicyTests`). Bir sayı değişip ona bağlı olan değişmezse test kırmızıya döner.

1. Hoş geldin altını en ucuz bölüm öncesi booster'ı alabilir, "+5 hamle"yi alamaz: 200 altın, roket ve bomba ya da "+3 hamle"nin fiyatı; "+5" 900. İlk mağaza ziyaretinde bir şey alınabilir, ilk "+5" oynayarak kazanılır.
2. Hoş geldin booster'ları yedi türün hepsinden ve eşit sayıdadır: her booster parasız tanınır, hiçbiri öne çıkarılmaz.
3. Bir booster seti, içindekilerin tek tek fiyatlarının toplamına satılır; indirim icat edilmez.
4. Bir günün reklamla altını "+5"in onda birinden azdır (4 × 3 = 12 < 90): reklamla altın küçük bir ek olarak kalır.
5. Ayar okuyucusu ayrıca şunları denetler: şölen "+5"i geri ödemez ("+5" 4 hamlelik altından pahalıdır), "+3 hamle" booster'ı 3 hamlelik altından pahalıdır.

*Simülatörle bakılanlar.* Her ayar değişikliğinde `puzzle economy compare` ile bakılır:

6. Ortalama oyuncu 100. bölüme hedef aralıkta, 7–9. günde gelir.
7. Hoş geldin hediyesi bu günü en çok bir gün değiştirir.
8. Ortalama oyuncunun kesesi 100., 300. ve 1000. bölümde 2–8 "+5" değerinde kalır.

Ölçüm (4.000 oyuncu, aynı oyuncular iki ayarla):

| | Hediyeyle | Hediyesiz |
|---|---|---|
| Ortalama oyuncu: 100. / 300. / 1000. bölüm | 8. / 25. / 90. gün | 8. / 25. / 90. gün |
| Ortalama oyuncunun kesesi: 100. / 300. / 1000. bölümde | 4.004 / 4.192 / 6.744 | 3.720 / 3.756 / 6.504 |
| Zayıf oyuncunun kesesi, 100. bölümde | 2.024 | 1.624 |
| Zayıf oyuncunun karşılanmayan "+5" isteği, 100. bölüme kadar | 0,57 | 0,89 |

Hediye hızı değiştirmiyor (kural 7). Keseye 200–400 altın ekliyor; ortalama oyuncunun kesesi 4,4–7,5 "+5" değerinde (kural 8). En çok zayıf oyuncuya yarıyor: ilk 100 bölümde karşılanmayan "+5" isteği üçte bir azalıyor.

Simülasyonda hediyenin 12 bölüm içi booster'ı etkisiz sayılır, çünkü bölüm içi booster ölçülmedi. Oyunda ilk bölümleri bundan biraz daha kolaylaştırırlar.

**Sınırlar**
- Bölüm içi booster'lar ölçülmedi (hiçbir bot ne zaman kullanacağını bilmiyor). Gerçek para 2026-10-24'ten beri yalnız `player.purchases`'ın üç kuralıyla simüle ediliyor (aşağıda); varsayılan oyuncu ücretsizdir ve eksiği raporlanır.
- Tek tek bölüm sıralaması gürültülüdür; yalnızca kademe bazındaki tablo sağlamdır.
- Hedefi 2–4 adımda ilerleyen 71 bölümde "+5"in istenme kuralı ilerlemeyi göremiyor; bu bölümler raporda ayrı gösterilir.
- Oyuncu can beklemez, erken dönmez: "bekleme" yerine canın kestiği oyun süresi raporlanır.

**Karar (2026-10-14):** yan koleksiyon parçaları oyunla kazanılan coin'le alınır. İyi oyuncunun biriken coin'inin gideceği yer burasıdır; parça fiyatı, koleksiyonun büyüklüğü belli olunca simülatörden okunur.

**Açık kararlar:** coin sayılarının tek tek onayı; bölüm içi booster fiyatları (ölçüm yok); yan koleksiyonun büyüklüğü ve parça fiyatı.

**Ödeyen oyuncular ve v1'in ürünleri (2026-10-24).** v1'in satışları (§19) `economy/default.json`'un yeni `store` bölümüne yazıldı: dört altın paketi (kese $1.99 = 1.000, torba $4.99 = 2.750, sandık $9.99 = 6.000, hazine $19.99 = 13.000) ve Çırak Çantası ($1.99: 1.500 altın ve üç bölüm öncesi booster, oyuncu başına bir kez, 20. bölümden). Fiyat bir ABD sentidir (199 = $1.99); oyuncu fiyatı mağazanın kendi ülkesindeki yazısıyla görür. Ürün kimliği uygulama kimliği, bir nokta ve `productIdSuffix`'tir (`com.saturnwave.puzzle.coins.pouch`); uygulama kimliği kesinleşince kimlikler onu izler. Oyun bu bölümü henüz okumuyor: bu derleme parayla bir şey satmıyor ve mağazası `shop.coinPacks`'in örnek paketlerini gösteriyor; oyunun paketi (`Economy.json`) değişmedi.

Simülatöre üç ödeyen oyuncu eklendi. Kuralları ayar dosyasında (`player.purchases`), her biri ayrı bir değişken dosyada: `pays-when-short.json` (reklam kalmamışken "+5" istenir ve altın yetmezse en küçük paketi alır), `buys-the-starter-pack.json` (Çırak Çantası'nı 20. bölüme gelince alır), `pays-monthly.json` (1. gün ve her 30 günde bir $9.99'luk sandığı alır). Satın alma hiçbir oyunu değiştirmez; bir oyuncu dört ayarda da aynı saklı oyunlarla karşılaşır (test). Raporlarda yeni `purchases.csv` ve özetteki "PURCHASES" tablosu var; coin tablosunda satın alınan altın ayrı sütundur.

Ölçüm (4.000 oyuncu, aynı oyuncular, ölçüm bu derlemeyle denetlendi), ortalama oyuncu (55. basamak):

| Oyuncu | 100. / 300. / 1000. bölüm | Kese, ortanca: 100. / 300. / 1000. bölümde | "+5", 1000. bölüme kadar: reklamla / altınla / karşılanmayan | Ödenen, 1000. bölüme kadar |
|---|---|---|---|---|
| Ücretsiz (`default`) | 8. / 25. / 90. gün | 4.004 / 4.192 / 6.744 | 204,4 / 34,2 / 2,8 | — |
| Altın yetmeyince kese | 8. / 25. / 89. gün | 4.004 / 4.196 / 6.840 | 204,2 / 36,9 / 0 | 2,4 alım, ortalama $4,84 (ortanca $1,99; oyuncuların yarısı hiç almıyor) |
| Çırak Çantası | 8. / 25. / 89. gün | 5.516 / 5.612 / 7.836 | 204,3 / 35,0 / 1,9 | $1,99 |
| Ayda bir sandık | 8. / 25. / 89. gün | 10.004 / 10.124 / 25.180 | 204,2 / 36,8 / 0,1 | 3,3 alım, ortalama $32,79 |

- **Zayıf oyuncu (45. basamak)** en çok alan: altın yetmeyince 1000 bölümde 36 kese alır (ortalama $71,57; onda biri $95,52 ya da daha fazla öder), karşılanmayan 44,6 "+5" isteği sıfıra iner ve 1000. bölüme 108 yerine 105. günde gelir. Ayda bir sandıkla yine 18 isteği karşılanmaz: para, istediği günlere denk gelmiyor.
- **İyi oyuncu (65. basamak)** altın yetmeyince neredeyse hiç almaz (binde bir oyuncu, bir kese).
- **Booster:** bölüm öncesi booster kullanımı dört ayarda da aynı (ortalama oyuncu 1000 bölümde yaklaşık 57); hiçbiri coin'le booster almaz. Çırak Çantası'nın üç booster'ı ödül olarak gelenlerin yanında küçük kalır.
- **Denge politikası:** ayar ilişkileri tutuyor (`EconomyBalancePolicyTests`); politikaya araştırmanın fiyat kuralları 1, 2, 3 ve 6 eklendi: dolar başına altın 503 / 551 / 601 / 650 (en büyük paket %29 fazla, sınır %35); paketler 1 / 3 / 6 / 14 kez "+5" eder; Çırak Çantası'nın değeri 2.400 altın, aynı paradaki kesenin 2,4 katı (sınır 2,5) ve bir kez; fiyatlar .99 ile bitiyor, en yükseği $19.99. Fiyat kuralı 4 (rastgele içerik yok) biçimden gelir: dosya yalnız sabit içerik yazabilir. Simülatörle bakılan denge kurallarından 6. kural (100. bölüm 7–9. günde) dört oyuncuda da tutuyor (8. gün). 8. kuralın bandı (kese 2–8 "+5") ücretsiz oyuncu için yazıldı ve tutuyor (4,4–7,5); ödeyende kese satın aldığı kadar yükseliyor: Çırak Çantası 1000. bölümde 8,7, ayda bir sandık 11–28 "+5" değerinde.
- **Tutarlar değişmedi.** Simülatör araştırmanın önerisini değiştirmek için bir neden göstermedi. Gösterdiği şu: günde üç ücretsiz reklam ortalama oyuncunun "+5" isteklerinin çoğunu karşılıyor; ödeyen ortalama oyuncu az alıyor, düzenli ödeyenin altını ise birikiyor. v1'de altının gidecek yeri "+5" ve bölüm öncesi booster'dır (simüle edilen oyuncu mağazadan set almaz). Biriken altının yeri yan koleksiyondur (2026-10-14 kararı); parça fiyatı açık.
- Simüle edilmeyenler: iade, yeniden kurulumda kaybolan satın alma, ödemeyi bırakan ya da başka türlü ödeyen oyuncu.

**Komutlar**

| İş | Komut |
|---|---|
| Ölç (bir kez; kaldığı yerden sürer) | `puzzle economy measure economy/default.json` |
| Simüle et | `puzzle economy run economy/default.json` (raporlar `out/economy/<ad>`: `summary.txt`, hız, takılma, can, coin, harcama, booster, teklif CSV'leri) |
| İki ayarı karşılaştır | `puzzle economy compare economy/default.json economy/sessions-20-minutes.json` |
| Ödeyen oyuncuyu ücretsizle karşılaştır | `puzzle economy compare economy/default.json economy/pays-when-short.json` (ya da `buys-the-starter-pack.json`, `pays-monthly.json`) |
