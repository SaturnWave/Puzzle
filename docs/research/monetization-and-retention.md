# Gelir ve Oyuncu Tutma: Fiyat Çizelgesi, Sezon Pasosu, Mini Oyunlar, Meta ve Etkinlikler

**Tarih:** 2026-10-23. **Durum:** öneri. Bu belgedeki hiçbir fiyat ya da ödül henüz `economy/default.json` dosyasında ya da oyunda değil. Kararlar verilince `GAME_DESIGN.md` §19 ve §25 ile ayar dosyası güncellenir.

**Dayandığı araştırma:** `docs/research/monetization-2026-10/` klasörü. Fiyatlar `prices-a.md` ve `prices-b.md`, pass ve XP `pass-level.md`, mini oyunlar `minigames.md`, meta `meta.md`, etkinlikler `events-social.md`, doğrulama `verification.md`. Fiyat çizelgesinin makinece okunan kopyası: `docs/research/monetization-2026-10/price-chart.json`.

**Etiketler**
- **FACT**: kaynağın kendisinde gördük (ABD App Store sayfası, oyunun resmî yardım sayfası, bu depo).
- **REPORTED**: ikincil kaynak (hayran wikisi, analist, forum). Büyük olasılıkla doğru ama kimse doğrulamadı.
- **ESTIMATE**: bizim çıkarımımız, hesabımız ya da önerimiz. Bu belgedeki bütün öneriler ESTIMATE'tir.

[K12] gibi numaralar 10. bölümdeki kaynaklardır. Bütün kaynaklar **2026-10-23**'te açıldı. Fiyatlar mağazadaki gibi yazıldı: $4.99 = 4 dolar 99 sent. "Altın" oyundaki coin'dir.

**Doğrulamadan gelen düzeltmeler uygulandı.** Toy Blast'ın $49.99 ve $99.99 paketleri ile 750 altınlık $19.99 paketi bugün görülemedi; kullanılmadı. Toon Blast pasosunun $9.99 / $19.99 fiyatı hiçbir kaynakta yok; kullanılmadı. AppMagic raporundaki "en çok satan teklif payları" ve ARPPU sayıları doğrulanamadı (grafik ABD Google Play'i gösteriyor); kullanılmadı.

**Kısa sözlük**
- **Sezon pasosu (battle pass):** aylık bir ödül merdiveni. Herkes ücretsiz yolu doldurur; para verilince ikinci, ücretli yol açılır.
- **Paket / set (bundle):** altın ve booster'ı bir arada satan ürün.
- **Kumbara (piggy bank):** oyun oynadıkça dolan, parayla açılan altın kutusu.
- **Altın/$:** bir dolara düşen altın. Paket büyüdükçe artar.
- **D1 / D7 / D30:** kurulumdan 1, 7 ve 30 gün sonra oyunu hâlâ açan oyuncuların oranı.
- **Etkinlik (LiveOps):** belli günlerde açılan, süreli içerik.
- **Simüle rakip:** gerçek bir oyuncu değil, kayıtlı ya da hesaplanmış bir rakip.
- **Pay-to-progress:** parayla ilerleme satmak.
- **Efor:** S yaklaşık 2–4 iş günü, M 1–2 hafta, L 3–6 hafta, XL 6 haftadan fazla (proje sahibi ve Claude; ESTIMATE).

---

## 1. Özet

**Sorun.** Oyunun çekirdeği sağlam ama her bölüm aynı döngü: oyna, parça al, tabloyu tamamla. Araştırma ve kendi ekonomi sayılarımız, ileride oyuncu kaybettirecek yedi boşluk gösteriyor. Bunlar hata değil, tasarım boşluğu:

1. **Tekdüzelik.** Bölümler arasında kısa bir mola, ara oyun ya da etkinlik yok. Homescapes ilk 100 bölümde her 5–10 bölümde bir ara oyun veriyor [K33] (REPORTED). Royal Match ve Toon Blast da bölüm yoluna kısa, can harcatmayan ara oyunlar koyuyor; Fishdom'da da ara oyunlar var [K32, minigames.md].
2. **İçerik bitişi.** Ortalama ücretsiz oyuncu 1000. bölüme 90. günde, iyi oyuncu 75. günde varıyor (FACT, §25). Royal Match iki haftada bir 100, Toy Blast iki haftada bir 50 yeni bölüm ekliyor [K1, K6] (FACT). Bizim 1000'den sonrası için planımız yok.
3. **Ölçüm yok.** Oyun telefondan bir şey göndermiyor (FACT, §20). Hangi özelliğin oyuncu tuttuğunu bilemeyiz.
4. **Sosyal yok.** Royal Match, Candy Crush ve Toon Blast'ta takımlar var ve hepsi internet istiyor [K23, K24] (FACT). Bizde sunucu yok.
5. **Zor bölüm daha az altın veriyor.** Altın kalan hamleden geliyor (hamle başına 4); zor bölümde az hamle kalır. Homescapes'te tersi: Normal 10, Zor 100, Süper Zor 150 altın [K34] (REPORTED).
6. **Can neredeyse hiç bitmiyor.** 10 dakikalık oturumda 5 can hiçbir oturumu bitirmiyor (FACT, §25). Rakiplerin en çok kullandığı "sınırsız can" ve "8 can" ödülleri bizde az değerli.
7. **İyi oyuncunun altını birikiyor.** 65. basamak oyuncunun kesesi 1000. bölümde yaklaşık 34.500 altın (FACT, §25). Yan koleksiyon kararı var ama yapılmadı.

**Liderler ne yapıyor**
- İncelenen büyük oyunların çoğunda aylık bir sezon pasosu var: yaklaşık 30 basamak, bölüm kazanınca dolan bir sayaç, ücretsiz ve ücretli iki yol. Ücretli yol $3.99 ile $9.99 arasında [K1, K2, K8, K12, K17] (FACT). Toy Blast'ta pass bulunamadı.
- Etkinliklerin neredeyse hepsi aynı fikir: zaten oynadığın bölümü kazan, jeton topla, jetonu küçük bir oyunda ya da yarışta harca [K60] (REPORTED).
- Altın merdiveni $0.99 ya da $1.99'la başlıyor. Doğrulanmış listelerde görünen en büyük altın paketi $19.99. Büyük paket dolar başına %24–33 fazla veriyor (FACT; oranlar ESTIMATE; §2).
- Ölçülmüş etki zayıf ama yön açık: Royal Match'te pass ilk 30 günde geliri %31 artırdı, ara oyun (King's Nightmare) %1 artırdı [K46] (REPORTED, analist tahmini).

**Önerimiz (hepsi ESTIMATE)**
- **Fiyat çizelgesi:** dört altın paketi ($1.99 / $4.99 / $9.99 / $19.99), bir başlangıç paketi ($1.99, oyuncu başına bir kez), sezon pasosu ($4.99), sonra iki set ($6.99 / $12.99). Rastgele paket, parayla can, $0.99 paket ve $19.99 üstü yok.
- **Sergi Sezonu (pass):** her ay bir "geçici sergi". Kazanılan her bölüm bilet verir, 30 basamak. Ücretsiz yolun sonunda sezonun tablosu herkese. Ücretli "Hami Yolu" daha fazla altın ve booster, yaldızlı çerçeve ve sezon kasası verir.
- **Restoratör rütbesi (XP):** bölüm kazanma, zor bölüm, ilk denemede kazanma, tablo ve oda bitirme XP verir. Kalan hamle ve booster kullanımı XP vermez; gerekçe §4.6.
- **Çeşitlilik:** ilk 100 bölümde her tablodan sonra kısa bir "Restoratör Masası" (vernik temizleme, sahteyi bul, ayrıntı avı). Oda finalinden sonra "Gala gecesi" altın bölümü. Zor bölüme ek ödül. Art arda kazanana ücretsiz booster.
- **Meta:** tamamlanan tabloyu parmakla temizleme, tablo hikâye kartları, gelecek odayı önceden gösterme; sonra müze kataloğu albümü, geçici sergi kanadı ve oda görev listesi.
- **İçerik:** yayından sonra düzenli yeni oda (50 bölüm ve 5 tablo). Bölüm üreticisi ve bot kalibrasyonu bunu ucuz kılıyor.
- **Sunucu:** yayında sunucusuz, ama analiz servisiyle (rıza ekranıyla). Sonra statik bir ayar dosyası ve bulut kayıt. Takımlar için kendi sunucumuz en son ve yalnız veri gerekçe gösterirse.

**Fiyat çizelgesi kısaca** (ayrıntı §3):

| Ürün | Fiyat | İçerik |
|---|---|---|
| Bir kese altın | $1.99 | 1.000 altın (yaklaşık 1 kez "+5 hamle") |
| Bir torba altın | $4.99 | 2.750 altın (3 kez +5) |
| Bir sandık altın | $9.99 | 6.000 altın (6 kez +5) |
| Bir hazine altın | $19.99 | 13.000 altın (14 kez +5) |
| Çırak Çantası (oyuncu başına bir kez) | $1.99 | 1.500 altın + 3 bölüm öncesi booster |
| Sergi Sezonu: Hami Yolu (sezonda bir) | $4.99 | 2.500 altın + 15 booster + sezon kasası (en çok 3.000) + yaldızlı çerçeve |
| Atölye Seti (sonra) | $6.99 | 2.750 altın + her booster'dan 1 |
| Usta Seti (sonra) | $12.99 | 6.000 altın + her booster'dan 2 |

**İlk adım.** Önce altyapı: satın alma paketi, INTERNET izni sorusunun cevabı, analiz ve rıza ekranı kararı (§8, Aşama 0). Yayına altın paketleri, başlangıç paketi ve ilk 100 bölümün çeşitliliği. Pass yayından sonraki ilk ay. Yeni odalar yayından itibaren paralel.

**Karar bekleyenler:** 16 soru, §9'da.

---

## 2. Rakip fiyatları (ABD App Store, USD, 2026-10-23)

Apple bir oyunun yalnız en çok satan 10 satın almasını gösterir. Daha pahalı paketler var olabilir ama dışarıdan görünmez (FACT, katalog listesi "kesik" işaretli; prices-a.md).

### 2.1 Altın merdiveni (içeriği bilinen oyunlar)

| Oyun | Paketler (fiyat → miktar) | Dolar başına: en küçük → en büyük | Durum |
|---|---|---|---|
| Lily's Garden [K11] | $0.99 → 1.000 · $4.99 → 5.500 · $9.99 → 12.000 · $19.99 → 25.000 | 1.010 → 1.251 (+%24) | FACT |
| Matchington Mansion [K10] | $1.99 → 1.100 · $4.99 → 3.000 · $9.99 → 7.000 | 553 → 701 (+%27) | FACT |
| Project Makeover (elmas) [K12] | $1.99 → 500 · $4.99 → 1.500 · $9.99 → 3.300 · $19.99 → 6.700 | 251 → 335 (+%33) | FACT |
| Toy Blast [K6] | $0.99 → 25 · $4.99 → 140 · $9.99 → 300 · $19.99 → 625 | 25,3 → 31,3 (+%24) | FACT |
| Jigsaw Puzzles (Easybrain) [K13] | $0.99 → 1.200 · $2.99 → 4.500 · $6.99 → 12.000 · $9.99 → 20.000 | 1.212 → 2.002 (+%65) | FACT |
| Magic Jigsaw Puzzles [K14] | $0.99 → 1.000 · $2.99 → 3.000 · $4.99 → 5.000 | 1.010 → 1.002 (artış yok) | FACT |
| Royal Match [K1, K45] | $1.99 → 1.000 · $7.99 → 5.000 | 503 → 626 (+%25) | fiyat FACT, miktar REPORTED |
| Candy Crush Saga [K3, K39] | $1.99 → 10 külçe · $7.99 → 50 külçe | 5,0 → 6,3 | fiyat FACT, miktar REPORTED |

- Gardenscapes "1000 Coins"u hem $0.99 hem $1.99'a satıyor; hangi oyuncunun hangisini gördüğü dışarıdan belli değil [K8] (FACT).
- Homescapes, Royal Kingdom, Toon Blast ve Fishdom'un fiyatları FACT, altın miktarları yayımlanmamış.
- Dolar başına sayılar ve yüzdeler bizim hesabımız (ESTIMATE).

**Okuma (ESTIMATE).** Giriş $0.99 ya da $1.99. Görünen en üst altın paketi $9.99 ya da $19.99. Büyük paket dolar başına %24–33 fazla veriyor (Easybrain'in %65'i istisna). $49.99 ve $99.99 bugün doğrulanmış hiçbir ABD listesinde görünmüyor.

### 2.2 Altın ne satın alıyor: "+5 hamle"nin fiyatı

| Oyun | +5 hamle | En küçük paketle kaç kez | Durum |
|---|---|---|---|
| Royal Match [K1] | 900 altın, sonraki 1.900 | $1.99 = 1.000 altın → 1 kez | REPORTED (mağazadaki kullanıcı yorumu; doğrulamada yeniden görüldü) |
| Candy Crush Saga [K39] | 10 külçe | $1.99 = 10 külçe → 1 kez | REPORTED |
| Homescapes [K35] | 900–3.800 altın | — | REPORTED |
| Toon Blast [K41] | 100, sonra 200, sonra 300 altın | — | REPORTED |
| **Bizim oyun** | **900 altın** (günde 3 kez reklamla ücretsiz) | — | FACT (`economy/default.json`) |

**Önemli (ESTIMATE).** Bizim altın ölçeğimiz Royal Match'inkiyle neredeyse aynı (+5 = 900). Bu yüzden Royal Match'in altın miktarlarını (paket, pass ödülleri, kasa tavanı) kendi ölçeğimize doğrudan taşıyabiliyoruz. Liderlerde en küçük paket yaklaşık bir "+5" eder.

### 2.3 Başlangıç paketleri ve setler

| Oyun | Ürün | Fiyat | İçerik | Durum |
|---|---|---|---|---|
| Gardenscapes [K8] | Hot Offer | $1.99 | 1.500 altın + 3 tür güçlendirici (aynı fiyata düz paket 1.000 altın) | FACT |
| Gardenscapes [K8] | Botanist Kit / Florist Kit | $5.99 / $9.99 | 2.000 altın + 4 tür / 5.000 altın + 4 tür | FACT |
| Homescapes [K7] | Starter Pack / Starter Kit | $1.99 / $4.99 | gösterilmiyor | FACT |
| Toy Blast [K6] | Beginner's Bundle | $1.99 | yayımlanmamış | FACT |
| Art Puzzle [K15] | Starter Package | $4.99 | koleksiyona hızlı başlangıç | FACT |
| Matchington [K10] | Novice Furnisher / Aspiring Decorator / Talented Designer | $2.99 / $5.99 / $12.99 | 1.600 / 3.000 / 7.000 altın + booster'lar | FACT |
| Royal Match [K1] | Royal Favor; Duke's / Prince's / Queen's Treasure | $6.99; $5.99 / $9.99 / $19.99 | yayımlanmamış | FACT |
| Homescapes [K7] | Apprentice Pack / Pro Pack | $6.99 / $15.99 | gösterilmiyor | FACT |

- **Matchington'ın kalıbı (ESTIMATE):** bir set, bir alttaki altın paketinin altınını ve booster'ları taşır, biraz daha pahalıdır ($4.99'luk 3.000 altın → $5.99'luk set 3.000 altın + booster).
- **AppMagic:** bulmaca oyunlarında gelirin çoğu $6–15 arası süreli tekliflerden geliyor; $50 üstü ürün nadiren en çok satanlar arasında. İlk 10 bulmaca oyununun ABD teklif fiyatları (2025 ilk yarı): ortanca $1.99, ortalama $6.05, en yüksek $99.99 [K53] (REPORTED; metin doğrulamada görüldü).

### 2.4 Sezon pasoları

| Oyun | Ücretli yol fiyatı | Süre / basamak | Ücretli yolun ek hakları | Durum |
|---|---|---|---|---|
| Royal Match: Royal Pass [K1, K17] | $9.99 | takvim ayı / 30 | can 5→8, altın isim ve çerçeve, takım arkadaşına hediye, Bonus Bank (her 10 anahtara 100 altın, en çok 5.000) | FACT |
| Royal Kingdom: Kingdom Pass [K2, K18] | $9.99 | takvim ayı / "birkaç" | aynı haklar, Bonus Bank en çok 5.000 | FACT |
| Gardenscapes: Golden ticket [K8, K42] | $4.99 | sezon | 8 can, altın çerçeve; 2019 sezonunda ücretli yol yaklaşık 3.500 altın | fiyat FACT, ödüller REPORTED |
| Project Makeover: Camping Expedition [K12] | $3.99 | yaklaşık ayda bir yeni sezon | booster ve elmas | FACT |
| Homescapes: Home Pass [K20] | ilk 10'da görünmüyor | sezon / 30 | 8 can, altın çerçeve, takıma hediye | kurallar FACT |
| Toon Blast: Toon Pass [K19] | doğrulanmış fiyat yok | — / 30 | Golden: 8 can; Diamond: 10 can; ikisi de alınabilir | kurallar FACT |
| Candy Crush Saga: Gold Pass [K21, K44] | oyuncuya göre değişiyor | — | geç alınca önceki ücretli ödüller de verilir | kurallar FACT, fiyat REPORTED |
| Candy Crush Soda [K52] | $4.99–$12.99 (7 / 14 / 21 basamak) | ay | — | REPORTED (2023-08-28) |

- **Royal Pass ödül toplamları:** ücretsiz yol 500 altın + 1 sa 45 dk sınırsız can; ücretli yol 2.500 altın + 3 sa 45 dk sınırsız can + daha büyük booster yığınları [K31] (REPORTED, wiki'nin 2022 tabloları; toplamlar ESTIMATE).
- **Analist:** mobil pasolar genelde $5–15 ve ödülleri fiyatın 10–20 katı değerinde [K49] (REPORTED, 2022).
- **Yaygınlık:** 2023'te en iyi %20'lik oyunların %66'sında pass vardı [K59] (REPORTED).

### 2.5 Kumbara, reklamsız, abonelik, resim paketleri

| Tür | Örnekler | Durum |
|---|---|---|
| Kumbara | Gardenscapes ve Homescapes "Gold Reserve" $2.99; Fishdom "Diamond Fishy Bank" $2.99; Lily's "Roxy's Reserve" $2.99 | FACT [K7, K8, K9, K11] |
| Kumbaranın satışı | Gold Reserve, Gardenscapes'in ABD iOS'ta en çok satan 3. ürünü (Sensor Tower) | REPORTED [K54] |
| Reklamsız | Easybrain Jigsaw ve Art Puzzle $14.99; Magic Jigsaw "Gold Pack" $4.99 (reklamsız + içerik) | FACT [K13, K14, K15] |
| Abonelik | Yalnız Magic Jigsaw: haftalık $7.99 (7 gün ücretsiz deneme), aylık $19.99 | FACT [K14] |
| Resim paketleri | Easybrain: tek resim $0.99, temalı paket $3.99; Magic Jigsaw: temalı paketler $2.99–$4.99 | FACT [K13, K14] |
| Altı büyük match-3'te reklamsız ya da abonelik | ilk 10 listesinde yok | FACT [K1–K6] |

### 2.6 Mağaza ve hukuk kuralları

- **Komisyon:** Apple'ın küçük işletme programında %15 (yılda 1 milyon dolara kadar, kayıt gerekir) [K69]. Google Play ABD'de 30 Haziran 2026'dan beri: ilk 1 milyon dolara %10 + Play Billing için %5 [K71]. İkisi de yaklaşık %15 (FACT; toplam ESTIMATE).
- **Fiyat noktaları:** Apple'da 900 fiyat noktası var [K70] (FACT). Örneklemdeki bütün fiyatlar .99 ile bitiyor (FACT).
- **Satın alınan altın süresiz olmalı:** Apple 3.1.1 [K68] (FACT). Pass bir para birimi değil; sezonla bitebilir.
- **Rastgele ödül:** parayla satılırsa olasılıklar satın almadan önce gösterilmeli: Apple 3.1.1 [K68], Google Ödeme politikası [K72] (FACT). Bizim kuralımız zaten parayla rastgele ürünü yasaklıyor (§17, §19).
- **ABD FTC:** Epic (Fortnite) tek dokunuşla ücret alan düğmeler ve çocukların onaysız alımları yüzünden 245 milyon dolar iade ödedi (2023-03-14) [K73]. HoYoverse için önerilen kararda 20 milyon dolar ceza, 16 yaş altına ebeveyn onayı olmadan rastgele paket satışı yasağı ve kafa karıştıran sanal para uyarısı var (2025-01-17) [K74] (FACT). **Bizim için (ESTIMATE):** her satın almada onay adımı; altın fiyatları yuvarlak ve her kullanımda görünür.

---

## 3. Önerilen fiyat çizelgesi (ABD, USD)

### 3.1 Bugünkü ekonomimiz

| Ne | Değer | Kaynak |
|---|---|---|
| +5 hamle | 900 altın; günde 3 kez reklamla ücretsiz | FACT, `economy/default.json` |
| Kazanma ödülü | kalan hamle başına 4 altın; tablo başına 80 altın + 1 booster | FACT |
| Ortalama ücretsiz oyuncunun günlük altını | yaklaşık 420 | FACT (ayar dosyasındaki not) |
| Bölüm öncesi booster | renk bombası 500, fırça + sıçrama 200, +3 hamle 200 | FACT (simülatörden) |
| Bölüm içi booster | spatula 400, satır 700, sütun 700, karıştırma 300 | FACT ama **yer tutucu** (ölçülmedi) |
| Hoş geldin hediyesi | 200 altın + her booster'dan 3 | FACT |
| Mağazadaki altın paketleri | 500 / 1.200 / 2.500, fiyatsız | FACT, yer tutucu |
| Hız (ortalama oyuncu) | 100. bölüm 8. gün, 300. bölüm 25. gün, 1000. bölüm 90. gün | FACT (§25) |

### 3.2 Fiyatlandırma kuralları (öneri)

1. Paket büyüdükçe dolar başına altın hiç düşmez. En üst paket en küçükten en çok %35 fazla verir (rakipler $19.99'da %24–33).
2. Her altın paketi yaklaşık tam sayıda "+5 hamle" eder; birden az +5'lik artık kalır. Mağaza "yaklaşık N kez +5" yazar.
3. Paranın aldığı bir setin ya da kumbaranın altın değeri (altın + booster'ların bugünkü altın fiyatı) aynı fiyattaki altın merdiveninin en çok 1,6 katıdır. Başlangıç paketi en çok 2,5 katı ve oyuncu başına bir kez. Sezon pasosu bu kuralın dışında: değeri oynayarak kazanılır.
4. Hiçbir üründe rastgele içerik yok. İçerik satın almadan önce listelenir.
5. Hiçbir satın alma bölümün hamle limitine, tohumlarına ya da yardımına dokunmaz (§17).
6. Yalnız .99 ile biten mağaza fiyatları. Yayında $19.99'un üstünde ürün yok.
7. Her satın almada onay adımı var. Kaybetme kartından tek dokunuşla ücret alınmaz.

1, 2, 3 ve 6 numaralı kurallar bugünkü denge testlerine (`EconomyBalancePolicyTests`) eklenebilir.

### 3.3 Çizelge

"Altın değeri" = altın + booster'ların bugünkü altın fiyatı. Yüzdeler 1 numaralı pakete göre. Bütün içerik ve fiyatlar ESTIMATE (önerimiz); dayanak sütunundaki rakip bilgileri FACT ya da REPORTED.

| # | Ürün | Fiyat | İçerik | Altın değeri | Değer / $ | Dayanak | Simülatör |
|---|---|---|---|---|---|---|---|
| 1 | Bir kese altın | $1.99 | 1.000 altın | 1.000 | 503 | Royal Match Mini Coin Package $1.99 = 1.000 (fiyat FACT, miktar REPORTED) [K1, K45]; Matchington $1.99 = 1.100 (FACT) [K10]; Candy Crush $1.99 = 1 kez +5 (REPORTED) [K39] | Evet |
| 2 | Bir torba altın | $4.99 | 2.750 altın | 2.750 | 551 (+%10) | Matchington $4.99 = 3.000 (+%9), Lily's +%9, Toy Blast +%11, Project Makeover +%20 (FACT) [K10, K11, K6, K12] | Evet |
| 3 | Bir sandık altın | $9.99 | 6.000 altın | 6.000 | 601 (+%20) | Lily's +%19, Toy Blast +%19, Matchington +%27, Project Makeover +%31 (FACT) | Evet |
| 4 | Bir hazine altın | $19.99 | 13.000 altın | 13.000 | 650 (+%29) | Lily's +%24, Toy Blast +%24, Project Makeover +%33 (FACT); görünen en üst altın paketi $19.99 | Evet |
| 5 | Çırak Çantası (bir kez, 20. bölümden sonra) | $1.99 | 1.500 altın + fırça+sıçrama, renk bombası, +3 hamle (birer) | 2.400 | 1.206 | Gardenscapes Hot Offer $1.99 = 1.500 altın + 3 güçlendirici; Homescapes Starter Pack $1.99; Toy Blast Beginner's Bundle $1.99 (FACT) [K8, K7, K6] | Evet |
| 6 | Sergi Sezonu: Hami Yolu (sezonda bir) | $4.99 | 2.500 altın + 15 booster + sezon kasası (en çok 3.000) + yaldızlı çerçeve + Hami plaketi | en çok 11.700 | en çok 2.345 | Gardenscapes $4.99, Project Makeover $3.99, Royal Match ve Royal Kingdom $9.99 (FACT); altın miktarları Royal Pass ölçüsünde (REPORTED) [K31] | Evet |
| 7 | Atölye Seti | $6.99 | 2.750 altın + her booster'dan 1 (7 booster) | 5.750 (2.100'ü ölçülmemiş fiyattan) | 823 | Matchington'ın set kalıbı; $6.99'luk teklifler: Royal Favor, King's Favor, Apprentice Pack (FACT) [K10, K1, K2, K7] | Evet; önce bölüm içi booster ölçümü |
| 8 | Usta Seti | $12.99 | 6.000 altın + her booster'dan 2 (14 booster) | 12.000 (4.200'ü ölçülmemiş fiyattan) | 924 | Matchington Talented Designer $12.99 = 7.000 altın + booster; Toon Blast Super Bundle $12.99 (FACT) [K10, K5] | Evet; aynı |
| 9 | Bağış Kutusu (kumbara; isteğe bağlı) | $2.99 | 1.800–2.500 altın | 1.800–2.500 | 602–836 | Gold Reserve $2.99 (Homescapes, Gardenscapes), Fishdom ve Lily's $2.99 (FACT); Gardenscapes'in 3. en çok satanı (REPORTED) [K54] | Evet (dolma hızı) |
| 10 | Müze Dostu Kartı (yalnız gerçek reklam gelirse) | $9.99, bir kez | günün reklam ödülleri reklam izlemeden, aynı günlük sınırlarla | — | — | Easybrain reklamsız $14.99; Magic Jigsaw Gold Pack $4.99 (FACT) [K13, K14] | Hayır |
| 11 | Geçici Sergi paketi (isteğe bağlı) | $2.99, bir kez | 5 ek kamu malı tablo, yalnız yapboz, kalıcı | — | — | Easybrain temalı paket $3.99, Magic Jigsaw temalı paketler $2.99–$4.99 (FACT) [K13, K14] | Hayır |

**Ürünlerin gerekçesi (ESTIMATE)**
- **1–4:** her paket yaklaşık tam sayıda +5 eder (1, 3, 6, 14). Kese, Royal Match'teki gibi tam bir +5 alır ve 100 altın artar. Bir paket ortalama ücretsiz oyuncunun 2,4 / 6,5 / 14,3 / 31 günlük altını kadardır.
- **5:** Gardenscapes'in Hot Offer'ıyla aynı biçim. Yalnız ölçülmüş bölüm öncesi booster'ları içerir, değeri bu yüzden bilinir. 20. bölümden sonra çıkar, çünkü bütün booster'lar 20. bölümde açılmış olur. Geri sayım yok; alınana kadar mağazanın başında durur.
- **6:** ayrıntı §4.
- **7–8:** bölüm içi booster'lar ölçülmeden satılmaz. Değerlerinin üçte biri yer tutucu fiyattan geliyor.
- **9:** oyuncunun kesesinden alınmaz. Şölendeki her kalan hamle için kutuya 2 altınlık bir kopya düşer; dolma hızını simülatör belirler. Hedef: ortalama oyuncu 1.800'e yaklaşık 12 günde varır.
- **10:** oyunumuzda zorunlu reklam yok, yalnız isteğe bağlı ödüllü reklam var. Kaldırılacak bir şey olmadığı için kart yalnız zaman kazandırır.
- **11:** rastgele değil, içerik önceden görünür. Yan koleksiyonun "oyunla kazanılan altınla alınır" kararının yanına ikinci bir yol olur; sahibin kararı gerekir.

**Sınırsız can saati hiçbir satılık üründe yok.** Bugün "can satılmaz" (§23). 10 dakikalık oturumda can neredeyse hiç bitmiyor; satarsak oyuncuya değer vermeyen bir şeyi değerli gibi göstermiş oluruz. Sınırsız can yalnız ücretsiz ödüllerde kalır (pass'in ücretsiz yolu, etkinlikler; §19).

### 3.4 Satmadıklarımız

| Ne | Neden |
|---|---|
| $0.99 altın paketi | +5 hamleyi (900) alamaz. Artık altın ikinci bir alımı kışkırtır (ESTIMATE). |
| $49.99 / $99.99 paketler | Bugün doğrulanmış hiçbir ABD listesinde görünmüyor. AppMagic: $50 üstü nadiren en çok satanlarda [K53]. Aile oyunu. Veri isterse sonra. |
| Parayla rastgele paket, sandık, kart | §17 ve §19 kararı. Satılsaydı olasılık gösterimi ve FTC'nin yaş kuralı gerekirdi [K68, K72, K74]. |
| Parayla can | Bugünkü kural; can neredeyse hiç bitmiyor. |
| Kaybetme kartında parayla +5 | +5 altınla ya da reklamla kalır; kart onaylı bir mağaza yoluna gider. Candy Crush bunu $0.99'a satıyor [K3] (FACT); biz satmıyoruz. |
| Abonelik | Örneklemde yalnız bir oyun satıyor. 30 yaş üstü rahat oyuncuya haftalık yenilenen ödeme uygun değil (ESTIMATE). |

### 3.5 Bize kalan

Mağaza payı yaklaşık %15 (ESTIMATE, §2.6). Satış vergisinden önce: $1.99 → $1.69, $2.99 → $2.54, $4.99 → $4.24, $6.99 → $5.94, $9.99 → $8.49, $12.99 → $11.04, $19.99 → $16.99.

### 3.6 Yayından önce simülatörle bakılacaklar

- **Gerçek para simüle edilmiyor (§25).** "Ödeyen oyuncu" değişkenleri eklenmeli: altın yetmeyince 1 numaralı paketi alan; 20. bölümde Çırak Çantası'nı alan; her ay Hami Yolu'nu alan. Bakılacak: 100. / 300. / 1000. bölüme varış günü ve kese.
- **Her yeni ücretsiz ödül** (pass'in ücretsiz yolu, ara oyun altını, gala bölümü, rütbe hediyeleri, seri booster'ları, zor bölüm ödülü) ücretsiz oyuncunun simülasyonuna girer ve bugünkü kurallara uyar: 100. bölüm 7–9. günde; kese 2–8 kez +5 değerinde.
- **Setler (7, 8)** bölüm içi booster'lar ölçülmeden yayına çıkmaz.
- **Denge bedeli:** Royal Match koleksiyon eklerken bölüm altınını yaklaşık %30 kısmış [K47] (REPORTED). Yeni bir ödül kaynağı, şölen ya da tablo altınını kısmayı gerektirebilir.

---

## 4. Sezon pasosu (Battle Pass) ve oyuncu seviyesi (XP)

### 4.1 Liderlerden alınan kalıp

- **Ortak biçim:** takvim ayı, yaklaşık 30 basamak, bölüm kazanınca dolan bir sayaç (Royal Match anahtar, Toon Blast rozet, Playrix sezon puanı), zor bölüme ek sayaç [K17, K18, K19, K20] (FACT).
- **Ücretli yolun hakları:** 8 can, altın isim ve çerçeve, takım arkadaşına hediye, son basamaktan sonra dolan kasa [K17, K18] (FACT).
- **Adil kurallar:** alınmamış ödüller sezon sonunda kendiliğinden verilir [K17] (FACT). Ücretli yolu geç alan, geçmiş basamakların ücretli ödüllerini de alır [K21] (FACT).
- **Açılış:** 26. ile 37. bölüm arası [K17, K18, K19, K20] (FACT).
- **Call of Duty ve Fortnite farkı:** orada pass oyun parasıyla alınır, 100 basamaklıdır, görev listeleri ve kozmetik ödüller vardır; Fortnite'ta pass'i bitiren bir sonrakinin parasını geri kazanır [K65] (FACT), [K66] (REPORTED). Casual oyunlar iki yolu, aylık sezonu, statü süsünü ve kasayı aldı; uzun görev listesini, basamak satın almayı ve 100 basamağı almadı (pass-level.md §3). Analist: casual oyuncu karmaşık görevleri bırakır, pass sade kalmalı [K49] (REPORTED). Biz casual kalıbı izliyoruz.

### 4.2 Öneri: Sergi Sezonu

| Konu | Öneri (ESTIMATE) | Neden |
|---|---|---|
| Tema | Her sezon bir geçici sergi: bir dönem ya da konu ("Kış Manzaraları", "Japon Baskıları", "Portreler"), yedek havuzdaki kamu malı tablolarla | Müzenin dilinde; oyuncu her ay yeni bir şey görür |
| Süre | Takvim ayı; ayın 1'inde başlar | Royal Match ve Royal Kingdom (FACT) |
| Açılış | 25. bölüm (ortalama oyuncu için yaklaşık 2. gün) | Rakipler 26–37 (FACT); ilk 100 bölüm en kritik dönem |
| Sayaç | "Ziyaretçi bileti": kazanılan bölüm 1; Zor bölüm +1; Süper Zor +2; Restoratör Masası +1; günün Küratör Sınavı +3 | Zorluk etiketi bölüm dosyasında sabit; gizli ayar yok |
| Basamak | 30. Basamak başına bilet: 1–10 arası 3, 11–20 arası 5, 21–30 arası 7. Toplam 150 | Günde 3 oturum oynayan ortalama oyuncu (günde yaklaşık 11 kazanma, 12–15 bilet) 10–13 günde bitirir. Günde 1 oturum oynayan (4–5 bilet) ay sonuna doğru bitirir |
| Ücretsiz yol | 500 altın, 10 booster, 2 × 30 dk sınırsız can; 30. basamakta sezonun tablosu, herkese | Royal Pass'in ücretsiz yolu da 500 altın (REPORTED) [K31] |
| Hami Yolu ($4.99) | 2.500 altın, 15 booster, Hami plaketi (15. basamak), yaldızlı çerçeve (30. basamak), sezon kasası | Royal Pass'in ücretli yolu da 2.500 altın (REPORTED) [K31] |
| Sezon kasası | 30. basamaktan sonra her 10 bilet kasaya 100 altın ekler; en çok 3.000; sezon sonunda yalnız Hami alanlara verilir | Royal Match Bonus Bank: her 10 anahtara 100, en çok 5.000 (FACT) [K17] |
| Geç alma | Hami sonradan alınırsa ulaşılmış basamakların ücretli ödülleri hemen verilir | Candy Crush (FACT) [K21]; "kaçırdım" baskısı olmaz |
| Sezon sonu | Alınmamış ödüller kendiliğinden verilir | Royal Match (FACT) [K17] |
| Booster sırası | Rastgele değil; yedi booster motorun sırasıyla döner | Tablo ödülüyle aynı kural (§25) |

Günde 11 kazanma tahmini §25'ten: ortalama oyuncu 300. bölümden 1000. bölüme 65 günde gidiyor (ESTIMATE).

### 4.3 Ödül tablosu

| Basamaklar | Bilet | Ücretsiz yol | Hami Yolu |
|---|---|---|---|
| 1–5 | 15 | 2 booster, 100 altın | 3 booster, 250 altın |
| 6–10 | 15 | 2 booster, 30 dk sınırsız can, 100 altın | 3 booster, 500 altın |
| 11–15 | 25 | 2 booster, 100 altın | 3 booster, 250 altın, Hami plaketi |
| 16–20 | 25 | 2 booster, 30 dk sınırsız can, 100 altın | 3 booster, 500 altın |
| 21–25 | 35 | 1 booster, 100 altın | 2 booster, 500 altın |
| 26–30 | 35 | 1 booster, **sezonun tablosu** | 1 booster, 500 altın, **yaldızlı çerçeve** |
| 30'dan sonra | her 10 bilet | — | kasaya 100 altın (en çok 3.000) |
| **Toplam** | **150** | **500 altın, 10 booster, 1 saat can, tablo** | **2.500 altın, 15 booster, plaket, çerçeve, kasa** |

**Değer (ESTIMATE, bugünkü fiyatlarla).** Ücretsiz yol yaklaşık 4.400 altın. Hami Yolu en çok 11.700 altın: aynı fiyattaki altın paketinin (2.750) yaklaşık 4 katı. Rakiplerin "10–20 katı"ndan [K49] düşük, çünkü ödülleri kendi altın fiyatımızla sayıyoruz ve can gibi bize maliyeti olmayan şeyleri değer diye eklemiyoruz.

**Ücretsiz oyuncuya etkisi (ESTIMATE).** Ortalama oyuncu ayda yaklaşık 12.600 altın ve 63 booster kazanıyor (günlük hediye ve tablo ödülleri). Ücretsiz yol bunlara %4 altın ve %16 booster ekler. 100. bölüme varış günü 7–9. gün aralığında kalmalı; simülatörle bakılır.

**Neden sezonun tablosu herkese (ESTIMATE).** Para tabloyu değil, çerçeveyi ve tüketilen ödülleri alır. Bu, "ana tabloların parçaları satılmaz" ilkesinin ruhuna uyar. Her ay herkes müzeye bir tablo ekler.

### 4.4 Neden $4.99, $9.99 değil

- Royal Match ve Royal Kingdom $9.99 istiyor (FACT). Ama ücretli yollarının değerinin bir kısmı takım hakları (altın isim takımda görünür, takıma hediye) ve 8 can. Bizde takım yok ve can neredeyse hiç bitmiyor.
- Gardenscapes $4.99, Project Makeover $3.99 (FACT). 2022'de Homescapes $4.99, Lily's Garden $5.99, Candy Crush $6.99'du [K46] (REPORTED).
- Yeni ve küçük bir oyun için satın alma eşiği düşük olmalı (ESTIMATE). Ölçüm gelince $6.99 ya da ikinci bir kademe (Toon Blast'ın Golden/Diamond kalıbı) denenebilir. Analist uyarısı: çok cömert ücretsiz yol ödeme nedenini siler; ilk satın alma oranı ölçülmeli [K49] (REPORTED).
- Pass diğer satışları da yiyebilir; Clash Royale'de gelir pass döneminde düşmüş [K49] (REPORTED). Bu yüzden önce altın paketleri, sonra pass ve ölçüm.

### 4.5 Çevrimdışı çalışma ve harita sonu

- **Tarihler:** sezon takvimi uygulamanın içinde (örneğin 24 aylık) ve telefonun saatinden okunur. Yeni tema uygulama güncellemesiyle ya da ileride statik ayar dosyasıyla gelir. Saati ileri alan oyuncu yalnız kendine sezon atlatır; oyuncular arası adalet sorunu yok (ESTIMATE).
- **Ödeme:** ücretli yol mağazanın ödeme sistemiyle alınır; satın alma anında bağlantı gerekir (ESTIMATE). Bu derlemenin INTERNET izni isteyip istemeyeceği açık soru (verification.md §4.2).
- **Harita sonu:** 1000. bölümü bitiren oyuncu bileti günün Küratör Sınavı'ndan, Restoratör Masası'ndan ve etkinliklerden alır. Candy Crush'ın 2026 pass denemesinde harita sonundaki oyuncular bitiremediklerinden şikâyet etti; aynı başlıkta bir oyuncu Star Tournament'la bitirilebildiğini yazdı [K43] (REPORTED).

### 4.6 Oyuncu seviyesi: Restoratör rütbesi (XP)

- İncelenen büyük match-3 oyunlarında ayrı bir XP yok; görünen ilerleme harita numarası (FACT, okunan sayfalarda yok). XP; Empires & Puzzles, Bingo Blitz ve Clash Royale gibi bulmaca-RPG, bingo ve strateji oyunlarında var (pass-level.md §2).
- Bizde XP'nin işi (ESTIMATE): harita numarasının anlatmadığını anlatmak. Ara oyunlar, günlük sınav ve harita sonrası da ilerlesin; oyuncunun bir unvanı olsun.

**Proje sahibinin üç kaynağı**

| Kaynak | Öneri | Neden |
|---|---|---|
| Geçilen bölüm | **Evet**, ana kaynak | Liderlerin pass sayacı da bu (FACT) [K17, K19]. Basit, adil ve önceden bilinir. |
| Hamle sayısı (kalan hamle) | **Hayır** | 1) Kalan hamleyi şölen zaten altınla ödüyor (hamle başına 4). 2) Zor bölümde az hamle kalır, şans payı büyük. 3) Satın alınan "+5" ve "+3 hamle" booster'ı kalan hamleyi artırır; XP parayla alınmış olur. Candy Crush puana göre yıldızı bıraktı, Royal Match bölüm başına tek yıldız veriyor (REPORTED; pass-level.md §2). |
| Kullanılan booster | **Hayır** (ceza da yok) | Booster parayla da alınır; booster'a XP vermek XP satmak demek. Bingo Blitz "Double XP" satıyor, Monopoly GO'nun Tycoon Pass'i yalnız harcamayla ilerliyor (REPORTED; pass-level.md): bunlar bizim etik sınırımızın dışında. Booster'sız kazanana fazla XP vermek de hoş geldin booster'larını kullanmayı cezalandırır. |

**XP tablosu (öneri)**

| Olay | XP |
|---|---|
| Bölüm kazanmak | 10 |
| Zor / Süper Zor bölüm | +5 / +10 |
| İlk denemede kazanmak | +5 |
| Tablo tamamlamak | +30 |
| Oda açmak | +100 |
| Restoratör Masası ya da ara oyun | +10 |
| Günün Küratör Sınavı | +20 |
| Kalan hamle, booster, satın alma | 0 |

- **İlk deneme notu:** booster'la kazanılan ilk deneme de +5 alır. Bu küçük bir harcama bağı. XP yalnız unvan ve küçük hediye verdiği için kabul edilebilir (ESTIMATE).
- **Rütbe eğrisi:** 1'den 2'ye 100 XP; her sonraki rütbe bir öncekinden 50 XP fazla ister. Ortalama bölüm yaklaşık 20 XP getirir (ESTIMATE). Buna göre 2. rütbe yaklaşık 5. bölümde, 5. rütbe yaklaşık 35. bölümde, 10. rütbe yaklaşık 130. bölümde, 20. rütbe yaklaşık 500. bölümde gelir; 1000. bölümde oyuncu yaklaşık 27. rütbededir. Rütbe sınırsız ve kalıcıdır.
- **Unvanlar (her 5 rütbede):** Gönüllü (1–5), Çırak (6–10), Restoratör (11–15), Konservatör (16–20), Küratör (21–25), Müze Müdürü (26 ve sonrası).
- **Rütbe hediyesi:** rütbe atlayınca sıradaki booster (rastgele değil). Unvan değişince profil çerçevesi. 1000 bölümde yaklaşık 27 booster eder; bugünkü booster gelirine yaklaşık %14 ekler (ESTIMATE). Simülatörle bakılır.
- **Pass ile karışmasın:** tek eylem, iki sayaç. Kazanma kartında "+1 bilet · +15 XP" yazar. Bilet sezonla sıfırlanır, XP kalıcıdır. Ayrı görev listesi yok.

---

## 5. Mini oyunlar ve bölüm çeşitliliği

**Kanıt**
- Homescapes'te oyuncuların yaklaşık %45'i 20. bölüme, %18'i 100. bölüme ulaşıyordu (2020) [K55] (REPORTED, şirketin düzenleyiciye beyanı). Bugün ara oyunu 1–100 arasında her 5–10 bölümde, sonra her 50 bölümde veriyor [K33] (REPORTED, wiki). **Çeşitlilik ilk 100 bölüme konmalı.**
- Royal Match'in King's Nightmare'i atlanabilir, can harcatmaz, kazanınca 50 altın verir [K32] (REPORTED, wiki). İlk 30 günde gelir %1, 14. gün tutma yaklaşık %3 arttı [K46] (REPORTED, zayıf). Ayrı bir düğmeden bölüm yolunun içine taşınınca daha iyi çalıştı [K46] (REPORTED).
- King: aşırı zor bölüm satış getirir ama oyuncu kaçırır; en az eğlenceli 100 bölümü düzeltmek etkileşimi "çok belirgin" artırdı [K56] (REPORTED, sayı yok).
- Candy Crush'ın 665 bölümlük paralel haritası Dreamworld 2016'da kaldırıldı [K40] (REPORTED). İkinci bir harita pahalı.

**Öneriler**

| Ad | Nasıl oynanır | Ne sıklıkla | Ödül | Oyuncuya maliyeti | Yapım | Dayanak |
|---|---|---|---|---|---|---|
| Restorasyon dokunuşu | Tamamlanan tablonun üstündeki kiri parmakla silersin, temiz tablo ortaya çıkar | Her tablo (10 bölümde bir) | Görsel an, +10 XP | Yok | S | Project Makeover'ın "Reveal" anı (REPORTED; meta.md) |
| Restoratör Masası: Vernik temizleme | Pamukla kiri kazı, altındaki gizli "restorasyon işaretini" bul | 1–100: her tablodan sonra (10 kez); sonra odada bir | 50 altın, +1 bilet, +10 XP | Yok; atlanabilir; tekrar oynanır | M (ilk tür), sonrakiler S | King's Nightmare 50 altın [K32]; Homescapes [K33] |
| Restoratör Masası: Sahteyi bul | Asıl tablo ile değiştirilmiş kopya arasında 3–5 fark | Aynı dönüşümde | Aynı | Yok | S–M; değiştirilmiş kopya için her müzenin şartı `licences.json`'da kontrol edilir | Fishdom ve Homescapes ara oyunları (REPORTED) |
| Restoratör Masası: Ayrıntı avı | Tabloda 4–6 küçük ayrıntıyı bul (köpek, gemi, imza) | Aynı dönüşümde | Aynı | Yok | S (ayrıntı koordinatları) | Gardenscapes'in gizli nesne kökü (REPORTED; minigames.md) |
| Gala gecesi | Oda açılınca kaybı olmayan bir altın tahtası; arkada odanın 5 tablosu | Her oda finalinden sonra (50 bölümde bir) | Altın (öneri 300; simülatör belirler) | Yok | S–M | Royal Match bonus bölümleri 2021'de 300–700 altın veriyordu, +5 = 900 [K51] (REPORTED) |
| Hasar patronu | Tablonun 10. (en zor) bölümünde tehdit tahtanın üstünde görünür (küf, güve, su lekesi, çatlak); hedefler bitince tehdit söner | Her 10. bölüm | Değişmez | Normal | S–M (yalnız görünüş; zorluk değişmez) | Royal Kingdom'ın Kara Kral bölümleri (REPORTED; minigames.md) |
| Zor bölüme ek ödül | Zor ve Süper Zor bölüm sabit ek altın ve iki kat bilet verir; etiketi bölüm başlamadan görünür | Var olan Zor / Süper Zor bölümler | Simülatör belirler | Normal | S | Homescapes Normal 10 / Zor 100 / Süper Zor 150 [K34] (REPORTED); Royal Match etkinliklerinde ek jeton [K38] (REPORTED) |
| Sabit El serisi | Art arda 1 / 2 / 3 kazanınca sonraki bölüm fırça+sıçrama / renk bombası / renk bombası + 3 hamle ile başlar; kayıp seriyi sıfırlar | Hep açık | Ücretsiz bölüm öncesi booster | Yok | S–M | Toon Blast Crown Rush [K25] (FACT); Royal Match Butler's Gift (REPORTED); serinin öğretici yanı [K50] (REPORTED) |
| Gece Nöbeti | Her hamlede ilerleyen bir tehdit (su, duman) tabloya varmadan hedefi bitir; saat değil hamle sınırı | Odada bir | 50–100 altın | Can yok; atlanabilir | M–L (yeni hedef türü ve bot ölçümü) | King's Nightmare [K32] |
| Günün Küratör Sınavı | Geçmiş bir bölüm, tek deneme; tarihten tohum, herkese aynı | Günde bir | Altın (simülatör), +3 bilet, +20 XP | Can yok | S (bölüm bankası hazır) | Homescapes "One Shot" bölümleri (REPORTED; minigames.md) |
| Günün Tablosu | Yedek havuzdan bir ek tablonun 12 parçalık küçük yapbozu; "Eskiz Defteri"ne girer | Günde bir | 1 booster ya da 1 bilet | Yok | M (görsel paketi, uygulama boyutu, lisans) | June's Journey yapbozları (REPORTED; minigames.md) |
| Arşiv Kazısı (etkinlik) | Kazanınca mala; bodrumdaki ızgarada eski bir eskizin parçalarını kaz; 5 aşama | Ayda bir hafta | Booster, altın, yan koleksiyon parçası | Yok | M | Royal Match Hidden Temple [K38] (REPORTED), Toon Blast Space Miner [K28] (FACT) |
| Katalog Eşleştirme (etkinlik) | Restore edilmiş tabloların ayrıntılarıyla eşini bul; çevirme hakkı kazanmaktan | Dönüşümlü, 3–7 gün | Booster, çerçeve | Yok | S–M | Royal Kingdom Spooky Carnival [K2] (FACT) |
| Kimin eseri? | İki şıklı soru ("Hangisi daha eski?", "Nerede yapıldı?"), katalog verisinden | Restoratör Masası dönüşümünde ara sıra | Küçük altın | Yok | S kod, M çeviri (13 dil) | Özgün fikir |
| Pigment Atölyesi (birleştirme) | Pigmentleri birleştirip boya ve araç yap, süslü bir çerçeveyi onar | Sonra | Çerçeve, booster | Yok | L | Gardenscapes Secret Ingredient (REPORTED; minigames.md) |

**Kurallar (ESTIMATE)**
- Hiçbir ara oyun can harcatmaz; hepsi atlanabilir. Zaman baskısı yok: Candy Crush zamanlı bölümleri kaldırdı (REPORTED; minigames.md).
- Gece Nöbeti dışında hiçbiri bölümlerin zorluğuna dokunmaz. Sabit El ve zor bölüm ödülü hızı değiştirir; bu yüzden simülatörden geçer. Sabit El ölçülmüş booster'ları (fırça+sıçrama, renk bombası, +3 hamle) kullanır; simülatör etkisini hesaplayabilir.
- Sabit El görünür ve herkese aynı kuraldır; gizli zorluk ayarı değildir (§17).
- Her yeni altın kaynağı simülatörden geçer; gerekirse başka bir kaynak kısılır [K47].

---

## 6. Meta katmanı

**Royal Match örneği.** Kale alanları var. Her kazanılan bölüm 1 yıldız verir; yıldız satılmaz. Yıldızla 4–8 yıldızlık görevler yapılır. "Museum" alanı 9 görev ve 50 yıldız [K36, K37] (REPORTED, wiki). Bu yaklaşık 50 bölüm, yani bizim bir odamız. Dream Games metasız ve başka varyantları denemiş, bugünkü alan + görev metasında kalmış; sonuçlar yayımlanmadı [K48] (REPORTED).

**Ağır mı hafif mi.** Hikâye, karakter ve dekor seçimi olan ağır meta (Playrix, Lily's Garden) üst gelir listesinde yaygın: inşaat metası olan oyunların payı 2016'da %7, 2022'de %49 [K58] (REPORTED). Ama 2021'de D30 tutmada en iyi oyun hafif metalı Royal Match'ti (%16,5); Homescapes %11,4, Gardenscapes %12,1 [K57] (REPORTED). Kaynak, yüksek tutmayı etkinliklere ve önceden gösterilen içeriğe bağlıyor. "Ağır meta tutmayı artırmıyor" ise bizim çıkarımımız (ESTIMATE). Playrix'in 4.000'den fazla çalışanı vardı [K67] (REPORTED). İki kişilik ekip için doğru biçim: hafif meta + ucuz derinlik (ESTIMATE).

**Bizde zaten var:** her kazanmada parça, 10 bölümde tablo, 50 bölümde oda; gerçek dünya haritası, ileri dönemler örtülü (§18, §19). **Eksik:** iki tablo arasında oyuncunun "harcadığı" bir şey ve oda düzeyinde görünür ilerleme.

| Fikir | Ne | Efor | Karar gerekir mi | Dayanak |
|---|---|---|---|---|
| Restorasyon dokunuşu | §5 | S | Hayır | Project Makeover "Reveal" |
| Tablo hikâye kartı | Tablo bitince kısa levha: ressam, yıl, yer, bugünkü müzesi, bir sade bilgi; haritadaki iğneye bağlı | S kod + M çeviri (100 tablo × 13 dil) | Hayır | Lily's Garden'ın küçük hikâye parçaları (REPORTED; meta.md) |
| Gelecek odayı göster | Örtülü çerçeveler, iskele, "Edo Japonyası odasına 3 tablo" sayacı | S (harita bir kısmını zaten yapıyor) | Hayır | Önceden gösterilen içerik ve D30 [K57] |
| Müze kataloğu albümü | Oda = set, tablo = kart; set ödülü; booster'sız kazanma için ek damga; rastgelelik ve tekrar kart yok | M | Hayır | Candy Crush pinleri ve "Trophy Room" [K30] (FACT); pinler yalnız oyunla kazanılır (REPORTED) |
| Geçici Sergi kanadı | Sezon tabloları ve yan koleksiyon için kalıcı kanat; geçen sezonlar orada kalır | M (kanat), sonra her sergi S | Hayır | Toon Blast Renovation (alan etkinlikten sonra kalır) [K29] (FACT); Lily's Garden mevsim alanları (REPORTED) |
| Ziyaretçi defteri | Açılan odalarla artan ziyaretçi sayacı ve kısa defter notları; insan çizimi yok | S kod + çeviri | Hayır | Project Makeover teşekkür mektupları (REPORTED; meta.md) |
| Ressamın atölyesi | Proje sahibinin çizdiği ressam; şövale, palet, araçlar oda finallerinde açılır; yan koleksiyonun yeri | M + sahibin çizimleri | Hayır (avatarı sahibi çizer) | King Robert, Austin gibi sabit karakterler (REPORTED; meta.md) |
| Restoratör rütbesi | §4.6 | S–M | Kaynaklar onaylanmalı | — |
| Oda görev listesi | Her kazanma 1 "fırça" verir; odada 8–12 görev (vernik, çerçeve onarımı, ışıklar, levha, kapılar), görev başına 1–6 fırça; oda yaklaşık 50 fırça; her görev odayı görünür biçimde değiştirir | M–L (oda durum çizimleri; insan yok) | **Evet:** görev sırasını oyuncu seçer; 2026-10-02 "oyuncu seçim yapmaz" kararına dokunur | Royal Match alan + görev [K36, K37] |
| Dekor seçimi | Odada birkaç yerde 3 seçenekten biri (çerçeve, duvar rengi, bank, lamba); sonra değiştirilebilir | L (her yere 3 çizim) | **Evet:** "seçim yok" kararını değiştirir | Homescapes (REPORTED; meta.md) |
| Yeni odalar (1000'den sonra) | 50 bölüm + 5 tablo; üretici + bot kalibrasyonu; levha çevirileri | Her oda S–M | **Evet:** tempo ve konu | Royal Match 2 haftada 100 bölüm [K1], Toy Blast 2 haftada 50 [K6] (FACT) |
| Arkadaş müzesi, hediye, takas | Arkadaşın müzesini gezmek, kart takası | XL (sunucu) | Evet | Monopoly GO (REPORTED; meta.md) |

**Önerilen sıra (ESTIMATE):** dokunuş → hikâye kartı → gelecek oda → albüm → sergi kanadı → (karar çıkarsa) oda görev listesi. Yeni odaların planı yayından hemen sonra başlar.

---

## 7. Etkinlikler ve sosyal

**Liderler.** Royal Match aynı anda 11–20 etkinlik açık tutuyor, 14 günde 27 biçimi döndürüyor [K60] (REPORTED). Hafta sonu geliri etkinliklerdeki artan satıştan geliyor, daha çok oyuncudan değil [K61] (REPORTED). Etkinlik aileleri beş: seri, yarış ve sıralama, takım, koleksiyon, mini oyun [K60] (REPORTED). Tek tek etkinliklerin etkisi birkaç yüzde: ilk 30 günde Sky Race %12, Lightning Rush %9, Team Treasure %2 gelir artışı [K46] (REPORTED, analist tahmini).

### 7.1 Çevrimdışı çalışır (tek telefonda, telefonun saatiyle)

| Etkinlik | Nasıl | Ne sıklıkla | Not | Dayanak |
|---|---|---|---|---|
| Sabit El serisi | §5 | Hep açık | Ölçülen booster'larla; simülatöre girer | Crown Rush [K25] (FACT) |
| Galeri Ziyaret kartı | Bugünkü "günün hediyesi" 7 günlük damga kartına dönüşür: her gün bir bölüm kazan, damga al; 7. damga büyük kutu (içeriği yazılı); gün kaçırmak damgaları silmez | Günlük | Yeni bir ödül kaynağı değil, bugünkünün yeni düzeni | Candy Crush Daily Stamps [K26] (FACT); günlük görevler bazı oyunculara angarya gelir [K64] (FACT, hakemli makale); Duolingo'da 7 günlük seriye varan 3,6 kat daha çok bitiriyor (ilişki, neden değil) [K63] (FACT) |
| Küratörün haftalık görevleri | 3 görev ("40 kir karesi temizle", "5 bölümü ilk denemede kazan"); her biri 1–2 kez değiştirilebilir | Haftalık | Bilet ve XP | Candy Crush Chocolate Box [K27] (FACT) |
| Sergi Sezonu | §4 | Aylık | — | — |
| Sergi takvimi | 2–4 haftalık temalar ("Hollanda Altın Çağı haftaları"); aynı etkinlik motorları yeni adla | Uygulamaya gömülü takvim | Takvim biterse döngüye girer | Playrix'in temalı etkinlikleri [K7] (FACT) |
| Arşiv Kazısı, Katalog Eşleştirme | §5 | Dönüşümlü, 3–7 gün | — | — |
| Gece Mesaisi (yumuşak seri) | 24 saatte art arda 5 bölümü ilk denemede kazan; rakip yok, kişisel ödül | Haftada bir | 7 bölümlük ve 100 kişilik ortak ödüllü sürümü sunucu ya da simüle rakip ister | Royal Match Lava Quest [K22] (FACT); zorluk + seri sinir bozar (REPORTED; events-social.md) |
| Harita sonu atölyesi | 1000'i bitirenlere tekrar turları, rütbe ve bilet | Yeni oda gelene kadar | — | Royal Match Royal League (FACT; events-social.md) |

### 7.2 Sunucu gerekir

| Özellik | Neden sunucu | Dayanak |
|---|---|---|
| Takım ("Müze Dostları Derneği"): can isteme, ortak fon, takım sıralaması | Hesap, üyelik, sohbet, moderasyon | Royal Match takımları: en çok 50 kişi, 21. bölüm, internet şart [K23] (FACT) |
| Ortak restorasyon (iki oyuncu bir sunak tablosu) | Eşleştirme, ortak ilerleme | Royal Match Train Journey, Toon Blast Temple Guardians (FACT; events-social.md) |
| Gerçek rakipli yarış ve sıralama | Eşleştirme, hile koruması | King's Cup, Star Tournament (FACT; events-social.md) |
| Kart takası, arkadaşa hediye | Hesaplar | Candy Crush Pin Swap [K30] (FACT) |
| Etkinliği güncellemesiz değiştirmek, A/B testi | Uzaktan ayar | — |
| Bulut kayıt, telefon değiştirme | Platform hizmeti ya da kendi sunucumuz | — |
| Satın alma makbuzunu sunucuda doğrulamak | Sahte satın almaya karşı daha güvenli | — |
| Ölçüm (D1 / D7 / D30, özelliklerin etkisi) | Analiz servisi, INTERNET izni, rıza ekranı | — |

### 7.3 Sunucu seçenekleri

| Seviye | Ne | Ne açar | Bedeli (ESTIMATE) |
|---|---|---|---|
| A. Sunucusuz (bugün) | Her şey telefonda; içerik uygulama güncellemesiyle | §7.1'in hepsi | Yok. Ama ölçüm yok, takvim güncellemesiz değişmez |
| B. Statik dosya + platform hizmetleri | Etkinlik takvimi ve düzeltme paketleri oyunların sitesinde bir JSON dosyası (planlanan düzeltme paketleriyle aynı yol, §20); bulut kayıt Google Play Games ya da iCloud ile; hazır analiz servisi (Firebase ya da Unity Analytics; seçim §20'de açık) | Uzaktan takvim, bulut kayıt, ölçüm | Efor M. INTERNET izni, rıza ekranı ve gizlilik metni değişir. Barındırma ücreti araştırılmadı |
| C. Kendi sunucumuz | Hesap, takım, sohbet, sıralama, makbuz doğrulama | §7.2'nin hepsi | Efor XL (aylar). Sürekli bakım, nöbet, moderasyon, çocuk güvenliği, KVKK ve GDPR yükümlülüğü. Aylık ücret oyuncu sayısına bağlı; araştırılmadı |

**Öneri (ESTIMATE).** Yayında A + analiz (B'nin ölçüm kısmı). İlk aylarda B'nin geri kalanı. C yalnız D30 ve ödeyen oyuncu sayısı gerekçe gösterirse. Not: ödüllü reklam ve satın alma gelince INTERNET izni zaten geri gelir (§20).

### 7.4 Simüle rakip dürüstlüğü

Royal Match, Lava Quest'in yardım sayfasında rakiplerin geçmiş verilerden simüle edildiğini açıkça yazıyor [K22] (FACT). Sunucusuz bir yarış yapacaksak kural ekranı aynı açıklığı göstermeli. Öneri: önce rakipsiz, kişisel hedefli sürümler (ESTIMATE).

---

## 8. Öncelik sırası

| Aşama | Ne | Neden bu sırada | Efor (ESTIMATE) | Beklenen etki | Simülatörle ilişkisi |
|---|---|---|---|---|---|
| **0. Altyapı** | Unity satın alma paketi; INTERNET izninin cevabı (deneme derlemesi, birleşik manifest); mağaza ürünleri ve satın alma geri yükleme; analiz ve rıza ekranı kararı; iOS modülü | Satış ve ölçüm olmadan sonraki her adım kör | M | Doğrudan etkisi yok; ölçmeyi mümkün kılar | Simülatöre "ödeyen oyuncu" değişkenleri eklenir |
| **1. Yayın paketi** | 4 altın paketi + Çırak Çantası; restorasyon dokunuşu; Restoratör Masası (ilk 100 bölümde 10 kez); zor bölüme ek ödül; Gala gecesi; Sabit El; Galeri Ziyaret kartı; Restoratör rütbesi; tablo hikâye kartları | Oyuncuların çoğu ilk 100 bölümü geçemiyor (Homescapes'te %18 [K55]); çeşitlilik oraya | L (toplam 4–6 hafta) | Ara oyunun ölçülen etkisi küçük (+%1 gelir [K46]) ama liderlerin hepsi yapıyor; zor bölüm ödülü korkulan bölümü istenen yapar | Hepsi yeni ücretsiz gelir: 100. bölüm 7–9. gün ve kese 2–8 × +5 kuralları; gerekirse şölen ya da tablo altını kısılır |
| **2. Yayından sonraki ilk ay** | Sergi Sezonu (ücretsiz + Hami) ve sezon kasası; Küratör Sınavı; haftalık görevler; Geçici Sergi kanadı | Pass ölçülen en büyük gelir adımı: Royal Pass ilk 30 günde +%31 [K46] (REPORTED); oyuncuya her ay yeni bir hedef | M–L | Gelir artışı beklenir, büyüklüğü bilinmiyor; tutma etkisi kanıtsız | Ücretsiz yol ücretsiz simülasyona, Hami Yolu ödeyen değişkene; bilet hızı ve sezon süresi |
| **3. İkinci ve üçüncü ay** | Dönen etkinlikler (Arşiv Kazısı, Katalog Eşleştirme); Gece Mesaisi; müze kataloğu albümü; bölüm içi booster ölçümü; sonra setler ($6.99 / $12.99) ve isteğe bağlı kumbara | Etkinlik yoğunluğu pass'ten sonra; setler ölçüm ister | L | Her etkinlik birkaç yüzde [K46] | Bölüm içi booster ölçümü setlerin ön koşulu; kumbaranın dolma hızı |
| **4. İçerik pisti (yayından itibaren paralel)** | 1000'den sonra yeni odalar (her biri 50 bölüm + 5 tablo); ilk yeni oda, iyi oyuncular 1000'e varmadan (yaklaşık 70. gün) hazır | İçerik bitince oyuncu gider; liderler 2 haftada bir 50–100 bölüm ekliyor (FACT) | Her oda S–M | Uzun vadeli tutmanın ön koşulu (ESTIMATE) | Kalibrasyon var olan bot hattıyla; ekonomi değişmez |
| **5. Sunucu ve sosyal** | B seviyesi (uzaktan takvim, bulut kayıt); veri gerekçe gösterirse C (takım, gerçek sıralama) | Pahalı ve kalıcı yük; önce veri | B: M; C: XL | Takımlar liderlerin hepsinde (FACT); bizdeki etkisi bilinmiyor | Takım ödülleri de yeni gelir: aynı kurallar |

Oda görev listesi ve dekor seçimi sahibin kararına göre 3. aşamada ya da sonra.

**Ölçüm gelince neye bakacağız.** Bulmaca oyunlarında ortanca (2024): D1 yaklaşık %20, D7 %4,3–4,8, D28 %1,1–1,3; bütün türlerde en iyi çeyreğin D1'i iOS'ta %31–33 [K62] (FACT). Küçük bir bulmaca oyunu için hedef bandı: D1 %30 ve üstü, D7 %10 ve üstü, D30 %4 ve üstü (ESTIMATE; events-social.md §4). İlk bakılacak sayı oturum süresi; bütün ekonomi hesabı 10 dakikalık oturuma dayanıyor (§25).

---

## 9. Proje sahibinin kararları

1. **Altın paketleri:** dört paket ($1.99–$19.99) uygun mu? $49.99 ve $99.99 yayında olmasın mı? (Öneri: olmasın.)
2. **Çırak Çantası:** "indirim icat edilmez" kuralının (§25, kural 3) tek istisnası olarak 2,4 kat değerle, oyuncu başına bir kez satılsın mı?
3. **Yeni denge kuralı:** gerçek parayla satılan set ve kumbaranın değeri en çok 1,6 kat olsun mu (§3.2)?
4. **Sezon pasosunun fiyatı:** $4.99 (öneri) mi, $9.99 mu?
5. **Hami Yolu canı 5'ten 8'e çıkarsın mı?** Rakiplerin hepsi çıkarıyor. Bizde "can satılmaz" kuralı var ve can neredeyse hiç bitmiyor. (Öneri: çıkarmasın.)
6. **Sezonun tablosu herkese mi** (öneri), yalnız Hami alanlara mı?
7. **XP kaynakları:** kazanma, zor bölüm, ilk deneme, tablo, oda, ara oyun ve günün sınavı evet; kalan hamle ve booster hayır. Onaylıyor musunuz?
8. **Ödüllü reklam** gerçekten gelecek mi? Gelirse "Müze Dostu Kartı" ($9.99, reklamsız ödüller) satılsın mı?
9. **Kumbara** (Bağış Kutusu, $2.99) olsun mu? Aile oyununda "biriken altını kaybetme" baskısı kabul edilir mi?
10. **Geçici Sergi paketleri** ($2.99, yalnız yapboz) parayla satılsın mı? Yan koleksiyonun "oyunla kazanılan altınla alınır" kuralının yanına ikinci bir yol olur.
11. **Ölçüm:** yayında analiz servisi ve rıza ekranı olsun mu (öneri: evet)? Hangi servis?
12. **Sunucu:** A (yok), B (statik dosya + bulut kayıt + analiz) ya da C (kendi sunucumuz)? (Öneri: yayında A + analiz, sonra B.)
13. **Simüle rakipler** açıkça yazılarak kullanılabilir mi, yoksa yalnız rakipsiz etkinlikler mi? (Öneri: önce rakipsiz.)
14. **Oyuncu seçimi:** oda görev listesinde görev sırasını oyuncu seçsin mi? Dekor seçimi gelsin mi? İkisi de 2026-10-02'deki "oyuncu seçim yapmaz" kararına dokunur.
15. **1000. bölümden sonrası:** yeni oda temposu (öneri: ayda en az bir oda, 50 bölüm) ve konusu (20 dönem bitti; tema kanatları mı, başka bölgeler mi?).
16. **Simülatör önce:** gerçek para çizelgesi ve sezon pasosu simülatörden geçmeden yayına çıkmasın; "ödeyen oyuncu" değişkenleri yazılsın mı?

---

## 10. Kaynaklar

Bütün kaynaklar 2026-10-23'te açıldı. Yayın tarihi biliniyorsa parantez içinde.

**ABD App Store sayfaları (canlı sayfa; fiyatlar FACT)**
- [K1] Royal Match: https://apps.apple.com/us/app/royal-match/id1482155847
- [K2] Royal Kingdom: https://apps.apple.com/us/app/royal-kingdom/id1606549505
- [K3] Candy Crush Saga: https://apps.apple.com/us/app/candy-crush-saga/id553834731
- [K4] Candy Crush Soda Saga: https://apps.apple.com/us/app/candy-crush-soda-saga/id850417475
- [K5] Toon Blast: https://apps.apple.com/us/app/toon-blast/id1176027022
- [K6] Toy Blast: https://apps.apple.com/us/app/toy-blast/id890378044
- [K7] Homescapes: https://apps.apple.com/us/app/homescapes/id1195621598
- [K8] Gardenscapes: https://apps.apple.com/us/app/gardenscapes/id1105855019
- [K9] Fishdom: https://apps.apple.com/us/app/fishdom/id664575829
- [K10] Matchington Mansion: https://apps.apple.com/us/app/matchington-mansion/id1216575026
- [K11] Lily's Garden: https://apps.apple.com/us/app/lilys-garden-match-design/id1437783446
- [K12] Project Makeover: https://apps.apple.com/us/app/project-makeover/id1483058899
- [K13] Jigsaw Puzzles (Easybrain): https://apps.apple.com/us/app/jigsaw-puzzles-puzzle-games/id1324604053
- [K14] Magic Jigsaw Puzzles: https://apps.apple.com/us/app/magic-jigsaw-puzzles-games/id439873467
- [K15] Art Puzzle (Easybrain): https://apps.apple.com/us/app/art-puzzle-jigsaw-games/id1519174001
- [K16] App Pricing Lab, Toy Blast (2026-03-25 kopyası): https://apppricinglab.com/iap/apple/890378044

**Resmî yardım sayfaları (FACT)**
- [K17] Royal Match, Royal Pass: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/112-royal-pass/
- [K18] Royal Kingdom, Kingdom Pass: https://dreamgames.helpshift.com/hc/en/6-royal-kingdom-1676903479/faq/315-kingdom-pass-1710944877/
- [K19] Toon Blast, Toon Pass: https://peakgames.helpshift.com/hc/en/4-toon-blast/faq/322-what-is-toon-pass/
- [K20] Homescapes, Home Pass: https://playrix.helpshift.com/hc/en/14-homescapes/faq/15526-home-pass-1755869542/?l=en
- [K21] Candy Crush Saga, Season Pass: https://candycrush.zendesk.com/hc/en-us/articles/7897323489693--Season-Pass
- [K22] Royal Match, Lava Quest: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/187-lava-quest/
- [K23] Royal Match, takımlar: https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/52-what-are-teams/
- [K24] Candy Crush Saga, Team Clash: https://candycrush.zendesk.com/hc/en-us/articles/34615452887965--Team-Clash
- [K25] Toon Blast, Crown Rush: https://peakgames.helpshift.com/hc/en/4-toon-blast/faq/24-what-is-crown-rush/
- [K26] Candy Crush Saga, Daily Stamps: https://candycrush.zendesk.com/hc/en-us/articles/28189843882013--Daily-Stamps
- [K27] Candy Crush Saga, Chocolate Box: https://candycrush.zendesk.com/hc/en-us/articles/360020946198--Chocolate-Box
- [K28] Toon Blast, Space Miner: https://peakgames.helpshift.com/hc/en/4-toon-blast/faq/628-what-is-space-miner/
- [K29] Toon Blast, Renovation Event: https://peakgames.helpshift.com/hc/en/4-toon-blast/faq/334-what-is-the-renovation-event/
- [K30] Candy Crush Saga, Candy Pins: https://candycrush.zendesk.com/hc/en-us/articles/25219182745373--Candy-Pins

**Hayran wikileri ve forumlar (REPORTED)**
- [K31] Royal Match Wiki, Royal Pass: https://royalmatch.fandom.com/wiki/Royal_Pass
- [K32] Royal Match Wiki, King's Nightmare: https://royalmatch.fandom.com/wiki/King%27s_Nightmare
- [K33] Homescapes Wiki, Mini Games: https://homescapes.fandom.com/wiki/Mini_Games
- [K34] Homescapes Wiki, Levels: https://homescapes.fandom.com/wiki/Levels
- [K35] Homescapes Wiki, Coins: https://homescapes.fandom.com/wiki/Coins
- [K36] Royal Match Wiki, Stars & Tasks: https://royalmatch.fandom.com/wiki/Stars_%26_Tasks
- [K37] Royal Match Wiki, Museum: https://royalmatch.fandom.com/wiki/Museum
- [K38] Royal Match Wiki, Hidden Temple: https://royalmatch.fandom.com/wiki/%E2%9B%BA%EF%B8%8F_Hidden_Temple
- [K39] Candy Crush Saga Wiki, Gold: https://candycrush.fandom.com/wiki/Gold
- [K40] Candy Crush Saga Wiki, Dreamworld: https://candycrush.fandom.com/wiki/Dreamworld
- [K41] Toon Blast Wiki, Coins: https://toonblast.fandom.com/wiki/Coins
- [K42] Gardenscapes Wiki, Furry Season: https://gardenscapes.fandom.com/wiki/Furry_Season
- [K43] King Community, Season Pass (2026 denemesi): https://community.king.com/en/candy-crush-saga/discussion/comment/3485774
- [K44] King Community, Gold Season Pass fiyatı: https://community.king.com/en/candy-crush-saga/discussion/473894/gold-season-pass-price

**Analistler ve basın (REPORTED)**
- [K45] GameMakers, Royal Match'in pass'i (2025-01-18): https://www.gamemakers.com/p/royal-matchs-219-battle-pass-secret
- [K46] Naavik, Royal Match derin inceleme (2022-05-25): https://naavik.co/deep-dives/royal-match/
- [K47] Naavik, bulmacada canlı operasyon eğilimleri (2025-11-16): https://naavik.co/digest/live-ops-trends-powering-mobile-puzzle/
- [K48] Naavik, Royal Match'in yinelemeyle başarısı (2025-02-23): https://naavik.co/digest/royal-match-finding-success-through-iteration/
- [K49] Deconstructor of Fun, battle pass analizi (2022-06-04): https://www.deconstructoroffun.com/blog/2022/6/4/battle-passes-analysis
- [K50] Deconstructor of Fun, kazanma serisi (2024-08-05): https://www.deconstructoroffun.com/blog/2024/8/5/win-streak-the-gift-that-keeps-on-giving
- [K51] Deconstructor of Fun, Royal Match (2021-03-21): https://www.deconstructoroffun.com/blog/2021/3/21/royal-match-the-new-king-from-turkey
- [K52] Gamigion, Candy Crush Soda pass deneyi (2023-08-28; güncelleme 2024-07-26): https://www.gamigion.com/?p=2672
- [K53] AppMagic Monetization Report 2025: https://appmagic.rocks/files/img-blog/EN_Monetization_Report_2025.pdf
- [K54] Mistplay, oyunlarda kumbara (2026-01-30): https://business.mistplay.com/resources/piggy-banks-in-games/
- [K55] Marketing-Interactive, Playrix reklamları (2020-10-14): https://www.marketing-interactive.com/popular-mobile-games-gardenscapes-and-homescapes-called-out-for-misrepresentation-of-game-in-ads
- [K56] mobilegamer.biz, King'in "iyi bölüm" tanımı (2024-03-27): https://mobilegamer.biz/how-king-defines-a-good-candy-crush-saga-level-and-why-it-constantly-prunes-the-bad-ones/
- [K57] PocketGamer.biz, match-3 D30 tutma (2021-12-01): http://www.pocketgamer.biz/dream-games-royal-match-leads-match-3-d30-retention-at-165/
- [K58] WN Hub, GameRefinery meta oranları (2022-03-31): https://wnhub.io/news/analytics/item-4490
- [K59] GameRefinery, battle pass'te 12 yol (2023-10): https://www.gamerefinery.com/12-ways-to-take-battle-passes-to-the-next-level-in-mobile-games/
- [K60] Gamigion, Tom Storr, "The Dream Live Ops Playbook" (21 Nisan, yıl sayfada yok): https://www.gamigion.com/?p=32712
- [K61] Sensor Tower, Royal Match takvimi (2026-05): https://sensortower.com/blog/what-royal-matchs-calendar-teaches-about-live-ops-in-2026
- [K62] GameAnalytics, 2025 Mobile Gaming Benchmarks (2024 verisi): https://www.gameanalytics.com/reports/2025-mobile-gaming-benchmarks
- [K63] Duolingo blog, seri araştırması (2022-01-31; oyun değil): https://blog.duolingo.com/how-duolingo-streak-builds-habit/
- [K64] Frommel ve Mandryk, "Daily Quests or Daily Pests?", CHI PLAY 2022: https://dl.acm.org/doi/10.1145/3549489
- [K65] Activision destek, Black Ops 7 Battle Pass (2025-12-04; FACT): https://support.activision.com/black-ops-7/articles/black-ops-7-battle-pass
- [K66] Game Developer, Fortnite V-Bucks fiyatı (2026-03-10): https://gamedeveloper.com/business/epic-games-increasing-the-price-of-v-bucks-to-help-pay-the-bills-
- [K67] Samsung Developers, Playrix söyleşisi (2022-03-16): https://developer.samsung.com/sdp/blog/en/2022/03/16/playrix-gardenscapespart-1

**Mağaza kuralları ve hukuk (FACT)**
- [K68] Apple App Review Guidelines (3.1.1, 3.1.2): https://developer.apple.com/app-store/review/guidelines/
- [K69] Apple App Store Small Business Program: https://developer.apple.com/app-store/small-business-program/
- [K70] Apple In-App Purchase (fiyat noktaları): https://developer.apple.com/in-app-purchase/
- [K71] Google Play ABD hizmet ücretleri: https://support.google.com/googleplay/android-developer/answer/16954621
- [K72] Google Play Ödeme politikası: https://support.google.com/googleplay/android-developer/answer/9858738
- [K73] FTC, Epic Games kararı (2023-03-14): https://www.ftc.gov/news-events/news/press-releases/2023/03/ftc-finalizes-order-requiring-fortnite-maker-epic-games-pay-245-million-tricking-users-making
- [K74] FTC, HoYoverse uzlaşması üzerine işletme notu (2025-01-17): https://www.ftc.gov/business-guidance/blog/2025/01/level-tips-businesses-ftcs-settlement-genshin-impact-developer-hoyoverse

**Bu depo (FACT, 2026-10-23)**
- `docs/GAME_DESIGN.md` §17 (etik korkuluklar), §18 (tema ve meta), §19 (ürün kararları), §20 (veri ve gizlilik), §23 (mağaza, `WalletLedger`, hoş geldin hediyesi), §25 (ekonomi simülatörü).
- `economy/default.json` (altın fiyatları, ödüller, mağaza yer tutucuları).
- `docs/research/monetization-2026-10/`: `prices-a.md`, `prices-a.json`, `prices-a-context.json`, `prices-b.md`, `prices-b.json`, `pass-level.md`, `minigames.md`, `meta.md`, `events-social.md`, `verification.md`, `price-chart.json`. Bu dosyalardaki diğer kaynaklar (her biri tarihiyle) orada listelidir.
