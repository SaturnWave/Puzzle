# Oyun Tasarımı ve Algoritma Planı — v0.2

> Durum: Faz 1 (motor + bot) ve Faz 2 (ilk 1000 bölüm) tamamlandı. Tema, ürün ve yayın kararları 2026-10-02'de verildi (§18–§21). Sıradaki iş motoru "hazır" yapmak (§21); arayüz, isim ve görseller en son.
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
| Tema | Genç bir ressam hasar görmüş müzeyi onarıyor; tablolar yapboz parçaları olarak toplanıyor (§18) | Bölümler yalnızca ilerleme aracı; oyuncuyu geri getiren meta oyun. Kamu malı tablolar içerik maliyetini sıfıra yaklaştırıyor |
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
- **Özel taş biriktirme (düzeltildi, 2026-10-03):** harcanan özel taşın değerinin yarısı kayıp sayıldığı için botlar özel taşları biriktiriyordu. 1000 bölüm × 100 oyunluk ölçümde expert, ürettiği özel taşların %19'unu kullanmadan bırakıyordu; zarar neredeyse yalnızca büyük "renk topla" hedefli bölümlerdeydi. Ceza 0,5'ten 0,25'e indi: iki profil için de en iyi değer bu (limitle kazanma expert %84,8 → %86,8, average %64,2 → %65,3). 0 ise eşya indirme bölümlerinde özel taşları erken harcatıyor. Bölümler eski ayarla kalibre edildiği için limitler 6. adımda yeniden üretilecek (§21).
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
   | 1–50 | %95 / %80 | %85 / %60 |
   | 51–300 | %90 / %75 | %75 / %50 |
   | 301–1000 | %85 / %70 | %65 / %40 |

   - **Neden aralığa göre:** sabit 80/60 eşiği 51. bölümden sonra Kolay ve Normal bölümlerin %95'inden fazlasında limiti belirliyor ve zorluk artışını yok ediyordu (Normal'de average her aralıkta %89). Casual ile average arasındaki 19–32 puanlık fark bölümlerden değil botlardan geliyor; hiçbir bölüm tasarımı kapatmıyor. Aralığa göre inen eşik ilk 50 bölümde tam olarak 80/60, sonrasında zorluk artışını koruyor (Normal'de average 88 → 81 → 75) ve çoğu Kolay ve Normal bölüme yaklaşık 2 hamle ekliyor.
   - **Doğrulama:** yeni tohumlarla iki bot da kendi eşiğine tolerans içinde olmalıdır. Kural, korumasız eski kuralın aynısı gibi iki yönlü: daha sıkı olan koşul hedefinin tolerans kadar altında da üstünde de kalamaz; limit çok cömertse yeniden okunur.
   - **Maliyet:** Kolay ve Normal adaylarında casual bot da oynar (aday başına 320 oyun daha); 1000 bölümlük üretime yaklaşık 10 dakika ekler.
   - Alt sınırı tutturmak için limit hamle aralığını aşınca üretici önce hedefleri küçültür; bu yüzden koruma çoğu zaman "daha çok hamle" yerine "biraz daha kolay hedef" olarak görünür.
6. **Limit+5 sütunu (uygulandı, 2026-10-03):** raporlar `L + 5` hamledeki kazanma oranını da gösterir (`botstats`: son sütun `winAtLimitPlus5`; üretici raporu: `win`'in yanında `winPlus5`). 5 değeri `ExtraMovesOffer.Moves` sabitinde. `L + 5` hamle üst sınırını aşarsa hücre boş kalır. "+5 hamle" teklifinin bölümü ne kadar kolaylaştırdığı böylece görünür. Kalibrasyon yine `L`'ye göre yapılır; dağılım elde olduğu için ek simülasyon gerekmez.

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
| `calibrationBot` | Hamle limitinin göre ayarlandığı oyuncu: `average` (karar 2026-10-02) |
| `reportBots` | Raporda ayrıca kazanma oranı gösterilen oyuncular (`casual`, `expert`) |
| `floorBot`, `bands[].casualFloor` | Casual koruması (§8): alt sınırı tutturması gereken bot (`casual`) ve aralık başına alt sınırlar. Koruma botu kalibrasyon botundan zayıf bir oyuncu modeli olmalı (random < novice < casual < average < expert); ölçüm botu olamaz. Raporda `floor`, `floorWin`, `floorSetLimit`, `floorBot` sütunları; `generate --repair` alt sınırı, koruma botu ya da hedefi değişen bölümleri yeniden üretir |
| `calibration`, `seedPool` | Oyun sayıları, tolerans, aday sayısı |

**Bir bölümün üretimi:**
1. **Plan** (`LevelPlanner`).
2. **Aday:** portföyden şekil stili, engel ailesi ve desen seçilir; her deneme farklı bir kombinasyon.
3. **Beslenebilirlik analizi:** kutu ve zincir hiç kırılmasa bile her hücreye yukarıdan veya çaprazdan taş gelebilmeli. Kenara dayalı bir kutu çifti köşedeki alt hücreyi kapattığı için reddedilir.
4. **Kalibrasyon:** bot sınırsız oynar → hedef kazanma oranını veren limit. Limit aralık dışındaysa ölçeklenebilir hedefler (renk toplama, malzeme) büyütülür veya küçültülür. Engel yoğunluğu adaydan adaya uyarlanır: uzun gelirse azaltılır, kısa gelirse artırılır.
5. **Doğrulama:** kalibrasyonda hiç kullanılmamış seed'lerle; sapma toleranstan büyükse limit yeniden okunur.
6. **Eşit zorluk havuzu:** seed'ler **bölümün doğrulanmış kazanma oranını** hedefleyerek seçilir. Medyan seed'i hedeflemek yanlıştı: zor bölümlerde seed zorluğu çarpık dağılıyor; ortanca seed ortalamadan çok daha zor çıkabiliyor (#310: kazanma %26, ortanca seed'lerin %0–8'i).
7. **Rapor botları:** casual ve expert kazanma oranları ölçülür; dosya ve rapor satırı yazılır.

Hiçbir aday aralığa oturmazsa en yakın olanı tutulur. Limiti yine hedef kazanma oranına göredir ve rapora `relaxed` diye işaretlenir.

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

İlk 30 bölümde boyut sabit kalır (öğrenme); boyut değişimi bölüm 30'dan sonra Zor bölümlerde başlar. İlk 20 bölümün tahtaları düz dikdörtgen; şekilli tahtalar ikinci tablonun ilk bölümünde, 21'de başlar. Böylece 11. bölüm yalnızca kutuyu öğretir (karar 2026-10-03).

### Mekanik açılış takvimi

**Karar (2026-10-02):** yeni engeller tablo başlangıçlarına hizalanır. Her yeni tablo (10 bölüm) yeni bir şey öğretir; her yeni müze odası (50 bölüm) büyük bir yenilik getirir. Profil 2026-10-03'te güncellendi; üretilmiş 1000 bölüm 6. adımdaki yeniden üretime kadar eski takvimde (parantez içindeki numaralar). Özel taşların tanıtım bölümleri (3, 5, 8, 12, 15) bir **öğretici takvimidir** (karar 2026-10-03): özel taşlar her bölümde eşleşme şekliyle oluşur, oyun kuralı değişmez; oyuncu o özel taşı ilk kez yaptığında arayüz kısa bir anlatım gösterir. Arayüz fazında yapılacak.

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
tools/docs-pdf      belgelerin PDF baskısını üreten betik (pandoc + Chrome)
docs/pdf            üretilmiş PDF'ler; kaynak her zaman Markdown dosyasıdır
```

**Belgeler.** Tasarım belgeleri Markdown olarak yazılır ve gözden geçirilir; her değişiklikten sonra `py tools/docs-pdf/build.py` ile PDF baskıları yeniden üretilir (kapak, belge künyesi, sayfa numaralı içindekiler, üst ve alt bilgi). Kod, `beautiful-code` standardına göre yazılır (kullanıcı kararı, 2026-10-02).

## 12. Yol haritası

| Faz | İçerik |
|---|---|
| 1 ✅ | Çekirdek motor, bot, CLI, testler, örnek bölümler |
| 1.5 ✅ | İki bomba, 8 renk + palet, spawn yönetmeni, mükemmel bilgi çözücüsü, web test laboratuvarı |
| 2 ✅ | Bölüm üretici, 1000 bölüm, zorluk raporu |
| 2.5 (sıradaki) | Motoru "hazır" yapmak: bot düzeltmesi, usta bot, casual koruması, booster'lar, bitiş şöleni, düzeltme paketleri, 1000 bölümün yeniden üretimi (§21) |
| 3 | Tema ve meta tasarımı: yapboz kesimi (C#), ekonomi simülatörü, 20 odanın ve 100 tablonun listesi (§18, §19) |
| 4 | Arayüz ve oyunun adı |
| 5 | Unity: görsel katman, animasyon event'leri, ipucu, analiz; görseller en son |
| 6 | Yayın: APK testi → sınırlı ülke yayını → dünya geneli (§20) |

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

**Usta ve expert** (2026-10-03; 1000 bölüm × 30 oyun, üst sınır 150, `botstats` tohumlarıyla eşleştirilmiş; expert 0,25 cezasıyla). Limitler hâlâ eski ayarla kalibre edilmiş limitler; 6. adımda yeniden üretilecek.

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

## 14. Açık sorular

1. ~~Bomba 5x5 mi 3x3 mü?~~ → İkisi de: Bomba 3x3, Büyük Bomba 5x5 (§3).
2. ~~Dokunarak patlatma?~~ → Evet, 1 hamle.
3. ~~Boyut değişimi bölüm içinde de mi?~~ → Hayır. Bölüm içinde sabit, bölümler ilerledikçe değişir (profil `sizes`).
4. ~~Can sistemi / booster ekonomisi?~~ → 5 can + sınırsız can ödülleri, 7 booster, "+5 hamle" teklifi (§18, §19). Fiyatlar ekonomi simülatörüyle belirlenecek.
5. ~~Retry politikası?~~ → Farklı desen, aynı kurallar, aynı zorluk (§17).
6. ~~Başlangıç tahtası?~~ → **Her denemede farklı** (2026-10-01). Sabit tahta desteği motorda duruyor ama kullanılmıyor.
7. ~~Zorluk hedefleri?~~ → Şimdilik §9 tablosu. Farklı kombinasyonlar profil dosyalarıyla denenecek.
8. ~~Hedef oyuncu?~~ → 30 yaş üstü rahat oyuncu. Yayından önce limitler `average` bota göre, casual korumasıyla (§8). Gerçek oyuncu verisi geldikçe yeniden ayarlanır (§20).
9. ~~Hamle aralığı?~~ → Bölüm aralığına ve zorluk kademesine göre değişken (profil `bands[].moves`).
10. ~~Mekanik takvimi?~~ → Tablo başlangıçlarına hizalandı (§9).
11. **Taşların görseli:** sanat aşamasında seçilecek. Kural: her rengin kendi şekli olur, renkler renk körlüğüne uygun sabit bir set kalır.
12. **Fiyatlar ve coin miktarları:** ekonomi simülatörüyle (§19).
13. **20 odanın sanat akımı listesi ve 100 tablo:** tema fazında hazırlanıp onaya sunulacak.
14. **Oyunun adı:** tema kesinleşti; arayüzle birlikte konuşulacak.
15. **Analiz servisi:** Firebase mi Unity Analytics mi (§20).
16. **Sınırlı yayın ülkeleri** (§20).

## 15. Paralel çalışan algoritmalar — mimari

Oyun tek bir algoritma değil, aynı anda çalışan birkaç sistemden oluşuyor:

| Katman | Algoritmalar | Nerede çalışır | Durum |
|---|---|---|---|
| **Spawn yönetmeni** | Denge (dueness) + hedef eğilimi + kaskad kontrolü + DDA yardımı, her taş için birlikte | Oyun içi (Core) | ✅ |
| **Oyuncu botları** | Random, Greedy (4 profil), usta (örneklemeli iki hamle arama), çözücü (beam search, mükemmel bilgi) | Analiz, ipucu (Bot) | ✅ |
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
- **Temalar:** her 50 bölüm bir müze odası, yani bir sanat akımı (§18). Oda kendi arka planını ve renk paletini getirir. Mekanik değişmez; yalnızca Unity görsel katmanı.

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

**Hikâye:** genç bir ressamın sevdiği müze bir felakette hasar gördü; tablolar parçalanıp dağıldı. Ressam bölüm kazanarak parçaları topluyor, tabloları yapboz gibi birleştirip yerlerine asıyor ve müzeyi oda oda yeniden açıyor.

**Yapı.** Üreticideki döngülerle birebir örtüşüyor; mevcut sisteme değişiklik gerektirmiyor:

| Birim | Bölüm | Üreticideki karşılığı |
|---|---|---|
| 1 tablo | 10 | Zorluk testere dişi (`cycle`). Döngünün son ve en zor bölümü tablonun son parçalarını verir |
| 1 müze odası = 1 sanat akımı | 50 (5 tablo) | Renk paleti grubu (`palette.chapterSize`) |
| Yayındaki oyun | 1000 (100 tablo, 20 oda) | — |

- **Oda sırası:** en sevilen akımlarla başlar (Empresyonizm, Post-empresyonizm), kronolojik değil. İlk 100 bölüm oyuncuyu tutmak için en kritik dönem; en renkli ve en tanınan tablolar orada olmalı.
- **Oda finali:** her odanın 5. tablosu o akımın en ünlü eseri. Her 50 bölümde bir büyük ödül anı olur.

**Tablolar**
- **Ana içerik:** kamu malı ünlü tablolar, güvenli olmak için 1900 öncesi sanatçılarla sınırlı. Kaynak: müzelerin açık erişim koleksiyonları (Art Institute of Chicago, The Met, Rijksmuseum). Yalnızca Art Institute of Chicago'da yaklaşık 2.000 kamu malı tablo var (2026-10-02 sorgusu).
- **Lisans listesi:** her görüntünün kaynağı ve lisansı kaydedilir, tek tek kontrol edilir. Aynı müzede "American Gothic" ve "Nighthawks" telifli.
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

10 parçayı düz ızgarayla bölmenin tek yolu 5×2; yaygın tablo oranlarında ince uzun parçalar veriyor. 3-4-3 diziliminde parçalar kareye yakın kalıyor (prototipte karşılaştırıldı, kullanıcı 3-4-3'ü seçti). Dikey tablolarda dizilimler 90 derece döner. Prototip atılabilir bir JS denemesi; gerçek kesim kodu C# ile yazılacak (§12, Faz 3).

**Müze:** hafif. Tamamlanan tablo duvara asılır, oda adım adım onarılır (çatlaklar kapanır, ışıklar yanar). Oyuncu seçim yapmaz. Seçenekli dekorasyon ve müze işletmesi ileride eklenebilir.

**Tema eşlemesi.** Motordaki adlar değişmez; yalnızca görünüşler ve oyuncunun gördüğü adlar değişir.

| Motordaki öğe | Temadaki karşılığı |
|---|---|
| Renkli taşlar | Boya (görseli sanat aşamasında seçilecek) |
| Roket | Fırça darbesi |
| Bomba / Büyük Bomba | Boya sıçraması / büyük sıçrama |
| Pervane | Tablodan fırlayan kuş ya da kelebek |
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

## 19. Ürün kararları

**Karar (2026-10-02).**

- **Hedef oyuncu:** 30 yaş üstü, kısa oturumlarla oynayan rahat oyuncu. İçerik aileye uygun.
- **İş modeli:** ücretsiz oyun + uygulama içi satın alma (booster, ekstra hamle, coin) + isteğe bağlı ödüllü reklam.
- **Can sistemi:** 5 can, yarım saatte bir can dolar, yalnızca kaybedince can gider. Etkinlik ödülü olarak sınırsız can süreleri. Yalnızca meta katmanı; motoru etkilemez.
- **"+5 hamle" teklifi:** hamle bitince coinle ya da reklam izleyerek alınır. Zorluk ayarı teklifsiz limite göre yapılır; raporlar limit+5'teki kazanma oranını da gösterir (§8).
- **Bitiş şöleni:** bölüm kazanılınca kalan her hamle tahtada bir rokete dönüşüp patlar ve coin verir. Kazanma kesinleştikten sonra çalıştığı için zorluk ölçümünü etkilemez.
- **Coin:** booster, ekstra hamle ve yan koleksiyon parçaları için. Ana tabloların parçaları coinle alınmaz.
- **Fiyatlar:** elle tahmin edilmeyecek. Bir **ekonomi simülatörü** botların 1000 bölümdeki kazanma oranlarını ve hamle dağılımlarını kullanarak can, "+5" teklifi ve coin akışını ölçecek; "ücretsiz oynayan biri 100. bölüme kaç günde gelir, nerede tıkanır" sorusunu cevaplayacak.
- **Etik sınırlar** (§17'ye ek): oyuncunun para harcama eğilimi zorluğu etkilemez, zorluk gizlice ayarlanmaz, gerçek parayla rastgele kutu satılmaz.
- **Ekip ve görseller:** kullanıcı ve Claude. Görseller en son, Claude Design ya da 3D araçlarla.

## 20. Kalibrasyon, veri ve yayın

**Karar (2026-10-02).**

- **Yayın öncesi kalibrasyon:** `average` bot + casual koruması (§8). İnsan verisi yayından önce az olacağı için ilk sürüm botla ayarlanır.
- **Web test uygulaması:** oynanan her oyun otomatik kaydedilir. Kayıt biçimi yayındaki oyununkiyle aynı; ilk "insan mı, bot mu" karşılaştırması APK beklenmeden görülür.
- **APK testi:** Play Store'dan önce kullanıcı ve birkaç kişi APK ile oynar. Hataları bulur ve insanların hangi bota benzediğini kabaca gösterir. 1000 bölümün limitlerini tek tek ayarlamaya yetmez; bunun için bölüm başına yüzlerce deneme gerekir.
- **Toplanan veri** (her bölüm denemesi için): bölüm numarası, seed numarası, kullanılan hamle, sonuç, kalan hedefler, "+5" alındı mı, kullanılan booster'lar. Kimlik yalnızca rastgele bir cihaz kimliği. İsim, e-posta ve reklam takibi yok. KVKK ve GDPR için rıza ekranı ve gizlilik politikası.
- **Veri hattı:** hazır bir analiz servisi (Firebase ya da Unity Analytics; seçim açık). Servisin reklam kimliği toplaması kapatılır. APK testinde yedek: kayıtlar telefonda dosyaya yazılır, test eden kişi dosyayı iletir.
- **Yayın sırası:** APK testi → birkaç küçük ülkede birkaç haftalık sınırlı yayın → gerçek kazanma oranlarına göre limit ayarı → dünya geneli. Yayında 1000 bölümün tamamı bulunur.
- **Düzeltme paketleri:** bölümler uygulamanın içinde gelir; sunucudan küçük düzeltme paketleri inebilir (örneğin bir bölümün hamle limiti ya da seed havuzu). Oyun internetsiz de oynanır. Bölüm dosyasındaki `formatVersion` dosya biçimini sürümler; düzeltme paketleri için bölüm başına bir içerik sürümü alanı eklenecek (eklemeli değişiklik).

## 21. Motorun "hazır" tanımı ve iş sırası

**Karar (2026-10-02).** Motor aşağıdaki liste tamamlanınca "hazır" sayılır ve tema fazına geçilir. v2 engelleri bu tanıma girmez; yayından sonra güncellemelerle gelir.

| # | İş | Durum |
|---|---|---|
| 1 | Kararları bu belgeye ve geliştirme günlüğüne yaz | ✅ |
| 2 | Botların özel taş biriktirme sorununu düzelt; usta botu yaz | ✅ 2026-10-03 |
| 3 | Üreticiye casual korumasını, raporlara limit+5 sütununu ekle; mekanik takvimini tablo başlangıçlarına hizala | ✅ 2026-10-03 |
| 4 | Motora booster'ları, bitiş şölenini, bölüm içerik sürümünü ve düzeltme paketini ekle | |
| 5 | Web test uygulamasına oyun kaydını ekle | |
| 6 | 1000 bölümü yeniden üret, raporları çıkar. Motor "hazır" | |

Sıranın gerekçesi: yeniden üretim (6) bot düzeltmesine, casual korumasına ve takvime bağlı; bölümler iki kez üretilmesin diye onlar önce gelir.
