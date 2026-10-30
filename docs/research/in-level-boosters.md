# Araştırma: Bölüm İçi Booster'lar ve Komşu Sistemler

> Bu rapor 2026-10-15'te, arayüz planının 10. aşamasından (bölüm içinde booster) önce, bir yapay zekâ
> araştırma ajanı tarafından türün yerleşik oyunlarının herkese açık kaynaklarından derlendi. Uygulama
> indirilmedi, video izlenmedi. Resmî yardım merkezleri (Dream Games, Playrix, Peak, Magic Tavern)
> okunabildi. King'in yardım sayfaları, Fandom vikileri, Medium, Twinfinite ve AppGamer otomatik okumayı
> reddetti (402/403); onlardan yalnızca arama motorunun özeti kullanıldı ve "(özet)" diye işaretlendi.
> Fiyatlar ve açılış bölümleri güncellemelerle değişir; her sayı kaynağının tarihindeki değerdir.
> Tasarıma yansıyacak kararlar: arayüz planının 10. aşaması (`out/unity/ui-plan.json`, depoda izlenmiyor)
> ve [GAME_DESIGN.md §17, §19, §23](../GAME_DESIGN.md).

**Etiketler**
- **[Resmî]**: oyunun kendi yardım merkezi ya da mağaza editoryali.
- **[Analiz]**: sektör analizi (Naavik, UserWise, Funovus).
- **[Topluluk]**: viki, rehber sitesi, oyuncu yazısı. "(özet)" yalnızca arama özetinden okunduğunu söyler.
- **[Gözlem]**: yazılı bir kaynağı bulunamayan, türün yaygın davranışı olarak bilinen bilgi.
  Doğrulanmadı. Sahibi Royal Match ya da Candy Crush'ı telefonda on dakika açarak doğrulayabilir.
- **y.d.**: herkese açık kaynakta yazılı değil.

## 1. Özet

**Türün anlaştığı yerler**

- **Dört temel araç.** Tek hücre (çekiç), satır, sütun, karıştırma. Royal Match, Toon Blast, Gardenscapes
  ve Fishdom'un bölüm içi seti tam olarak bu dörtlü [1][19][21][22]. Bizim `hit-cell`, `hit-row`,
  `hit-column` ve `shuffle` türün en yaygın setiyle birebir aynı.
- **Hamle yemez.** Kaynağın konuya değindiği her oyunda bölüm içi booster hamle saymaz
  [1][14][15][19][27][33].
- **Kurma iki dokunuştur.** Önce booster'ın simgesine, sonra hedef taşa dokunulur; hedef dokunuşu hemen
  uygular. Ayrı bir onay adımı yazan kaynak yok [15][19][28][29][33][48].
- **Satır ve sütun için hedef bir taştır.** Oyuncu satırın ya da sütunun herhangi bir taşına dokunur;
  araç o taşın satırını ya da sütununu temizler [1][21][22].
- **Bir vuruş, bir kat.** Engel tek vuruşta bir kat kaybeder [1][15][21][22]. Tek istisna Matchington
  Mansion'ın çekici: hücredeki her şeyi kaldırır [28].
- **Yer: tahtanın altı.** Booster şeridi alttadır [6][23]; bölüm öncesi booster'lar ayrı yerde, bölüm başı
  penceresinde seçilir [14][23].
- **Sıfırda aynı yerden satın alma.** Booster bitince simgesine dokunmak bölümün içinde oyun parasıyla
  satın almayı açar [15][19][33]. Para birimi her zaman oyun içi paradır (coin ya da altın külçe).
- **Can: 5 can, yarım saatte bir.** Royal Match, Homescapes ve Toon Blast'ta aynı [2][16][25]. Bizim
  kuralımızla da aynı.
- **Booster'sız geçilebilirlik sözü.** Royal Match ve Toon Blast her bölümün booster'sız ve para
  harcamadan geçilebileceğini yardım merkezinde açıkça yazıyor [4][26].

**Oyunların ayrıldığı yerler**

- **Beşinci araç.** İki taşı yer değiştiren eldiven (Homescapes, Matchington Mansion, Candy Crush'ın
  "Free Switch"i) [14][29][34]; satırla sütunu birden temizleyen araç (Matchington'ın süpürgesi, Project
  Makeover'ın saç fırçası, Lily's Garden'ın tırmığı) [27][32][40]; alan temizleyen araç (Project
  Makeover'ın parfümü, 5x5) [32].
- **Karıştırmada ne yerinde kalır?** Gardenscapes'te kilitli öğeler yerinde kalır [19]. Matchington ve
  Project Makeover'da özel taşlar da karışıma girer [30][32].
- **Açılış bölümü.** Candy Crush'ta çekiç 7. bölümde açılan ilk booster (özet; başka bir tablo 15 diyor)
  [34]. Toon Blast'ta örs ve zar 17-19. bölümler civarında (düşük güven) [44]. Royal Match'te bölüm içi
  booster'ların açılış bölümünü yazan bir kaynak bulunamadı.
- **"Hamle bitti" teklifinin fiyatı.** Çoğu oyunda teklif tekrarlandıkça pahalanır. Royal Match'te 2022'de
  sırasıyla yaklaşık 2; 4,35; 6,65 ve 8,95 dolarlık coin [7]; Candy Crush'ta ilk devam 10 altın, sonrakiler
  16'dan başlayıp artıyor (özet) [35]. Royal Match ikinci teklife bir roket ekliyor [8].
- **Seri ödülü.** Royal Match (Butler's Gift, 32. bölümden) ve Homescapes (Cat's Gift, 26. bölümden) art
  arda kazanan oyuncuya bölüm başında bedava güç veriyor; ilk kayıpta seri sıfırlanıyor [3][17].

**Bizim plan için sonuç**

Planın 10. aşaması türün çizgisinde. Tür bize üç yerde bir şey söylüyor:

1. **Hedef tek dokunuşla da seçilebilmeli.** Planın "parmak inince yansın, kaydırılabilsin, kalkınca
   uygulansın" hareketi bunu zaten kapsıyor; hızlı bir dokunuş da iniş ve kalkıştır.
2. **Karıştırma "olmuyor" dememeli.** Türün hiçbir kaynağında karıştırma booster'ının reddedildiği bir
   durum yok. Bizim motorun büyük açık tahtalarda ilk istekleri reddetmesi oyuncuya bozukluk gibi
   görünür; düzeltilmeli (5. bölüm, madde A).
3. **Booster'lar birer birer ve bir tanıtımla açılıyor.** Plan bunu sormuyor; ilk bölümlerin kalibrasyon
   verisini de korur (5. bölüm, madde B).

## 2. Karşılaştırma tablosu

| Oyun | Bölüm içi booster'lar | Kurma ve hedef | Hamle yer mi? | Akış sırasında? | Açılış, bedava örnek | Sıfırda |
|---|---|---|---|---|---|---|
| Royal Match | Kraliyet çekici (1 taş ya da 1 kat), Ok (satır), Top (sütun), Soytarı şapkası (karıştırma) [1] | Simge, sonra seçilen taş [1][6] | Hayır [1] | y.d.; takas akış sürerken yapılabiliyor [10] | Bölüm içi: y.d. Bölüm öncesi üç booster'dan 7. bölümde 3'er (özet) [13] | Coinle satın alınır [5]; alan sandıkları ve etkinlikler verir (özet) [12][13] |
| Candy Crush Saga | Lolipop çekici (1 şeker ya da engel), Free Switch (iki şekeri yer değiştirir), Çizgili fırça (bir şekeri çizgili yapar, yönü seçilir); sürüme göre UFO, Parti [34][37][38] | Simge, sonra şeker (King'in Jelly Saga yardımı, özet) [48] | Hayır [34][37] | y.d. | Çekiç 7. bölüm, ilk açılan; Free Switch 19, fırça 37 (özet) [34]. Oyunun başında birkaç çekiç (özet) | 3, 10, 20'lik paketler: 29, 69, 119 altın (özet) [34][49] |
| Homescapes | Çekiç (1 taş, engele hasar), Eldiven (iki komşuyu takas), Süpürge (satır), Halter (sütun), Vantilatör (karıştırma) [14] | Simge, sonra taş [15] | Hayır [14] | y.d. | y.d. Balyoz Kasım 2023'te kaldırıldı (özet) [42] | Bölüm içinde satın alınır; oyun günü ödülü [15] |
| Gardenscapes | Kürek (1 taş ya da 1 kat), Halter (sütun), Dart (satır), Vantilatör (karıştırma; kilitli öğeler kalır) [19] | Simge, sonra taş [19] | Kürek ve halter: hayır; diğerleri y.d. [19] | y.d. | y.d. | Bölüm önce ya da içinde satın alınır; şans çarkı, günlük ödül, etkinlik [19] |
| Fishdom | Çekiç (1 taş), Top (satır), Çapa (sütun; çok katlı engel birkaç vuruş ister, sütundaki güçleri tetikler), Vantilatör (karıştırma) [21] | Bir nesne seçilir [21] | y.d. | y.d. | y.d. | Bölüm önce ya da içinde satın alınır; günlük bonus, etkinlik [21] |
| Toon Blast | Çekiç (1 küp; çok parçalı engelden bir parça), Boks eldiveni (satır), Örs (sütun), Zar (karıştırma) [22] | Seçilen küp [22] | y.d. | y.d. | Örs ve zar 17-19. bölüm civarı (düşük güven) [44] | Coinle; günlük bonus [22] |
| Matchington Mansion | Çekiç (hücredeki her şey), Eldiven (iki öğeyi takas), Süpürge (satır ve sütun), Zil (karıştırma, güçler dahil) [27][28][29][30] | Simge, sonra öğe; eldivende iki öğe [28][29] | Hayır [27] | y.d. | y.d. | Bölüm içinde satın alınır; etkinlik ödülü [28][30] |
| Project Makeover | Makas (1 öğe), Parfüm (5x5 engeller), Saç fırçası (satır ve sütun), Vantilatör (karıştırma) [32] | Simge, sonra öğe [33] | Makas: hayır [33] | y.d. | y.d. | Simgeye dokununca bölüm içinde satın alınır; günlük bonus, etkinlik [33] |
| Lily's Garden | Tırmık (satır ve sütun, özet) [40]; diğerleri doğrulanamadı | y.d. | y.d. | y.d. | y.d. | y.d. |

**Tablonun eksikleri.** "Akış sırasında kullanılabilir mi" sorusuna hiçbir kaynak cevap vermiyor (3.3).
Lily's Garden'ın resmî yardım sayfası bulunamadı; satırı yalnızca bir viki özetine dayanıyor.

## 3. Desenler

### 3.1 Kurma ve vazgeçme

- **[Resmî]** Kurma her oyunda aynı: simgeye dokun, sonra hedefe dokun (Homescapes, Gardenscapes,
  Matchington, Project Makeover; King'in Jelly Saga yardımı da böyle anlatır) [15][19][28][33][48].
- **[Gözlem]** Kurulu hâlde seçilen simge öne çıkar, tahtanın dışındaki arayüz kararır ve üstte
  booster'ın adıyla tek satırlık işi yazar. Vazgeçmek için aynı simgeye yeniden dokunulur ya da yazının
  yanındaki kapatma düğmesine basılır. Bu davranış Royal Match ve Candy Crush için yaygın bilinir ama
  yazılı bir kaynakta bulunamadı.
- **[Gözlem]** Karıştırma hedef istemez; simgeye dokununca hemen çalışır (Royal Match'in şapkası, Toon
  Blast'ın zarı). Karıştırma için onay soran bir oyun bulunamadı.
- **Bizim plan:** basılan disk halkalanır, öbürleri söner, rafın üstündeki hap "Bir taş seç" der; aynı
  disk, duraklatma, geri ya da parmağı tahtanın dışında kaldırmak vazgeçirir; "Paleti karıştır" hemen
  çalışır. Türle uyumlu. Tek fark: türdeki yazılar aracın ne yaptığını da söyler, planın hap yazısı
  yalnızca ne seçileceğini söylüyor.

### 3.2 Hedef seçme ve geri bildirim

- **[Resmî]** Hedef dokunuşu hemen uygular; önizleme ya da onay yazan kaynak yok.
- **[Resmî]** Satır ve sütun araçları "seçilen taşın satırını/sütununu" temizler [1][21][22]. Takas eldiveni
  iki dokunuş ister [29].
- **[Analiz]** Royal Match'in gücü, olanı yazıyla değil animasyonla anlatmasıdır: kazanılan şeyler
  kazanıldığı yerden gideceği yere uçar [47]. Dokunuşlarda titreşim de kullanır [10].
- **Süre.** Bir booster etkisinin ekranda kaç saniye sürdüğünü yazan kaynak yok.
- **Bizim plan:** parmak inince hedef hücre, satır ya da sütun yanar; kaydırılabilir; parmak kalkınca
  uygulanır. Bu, türün tek dokunuşunun üst kümesidir: hızlı bir dokunuş türdeki gibi davranır, parmağını
  basılı tutan oyuncu ayrıca önizleme görür. 36 noktalık hücrelerde (planın 10. riski) bu önizleme türün
  yapmadığı ama bizim küçük hücrelerimizde işe yarayan bir ek.

### 3.3 Zamanlama kuralları

- **Hamle:** bölüm içi booster hamle yemez (1. bölüm).
- **Akış sırasında:** hiçbir kaynak yazmıyor. Royal Match, taşlar düşerken oyuncunun takas yapmasına izin
  veriyor ve bu bir hız artısı olarak övülüyor [10]. Booster için aynısının geçerli olup olmadığı
  bilinmiyor.
- **[Gözlem] Sıfır hamlede:** hamle bitince "hamle bitti" kartı gelir; bölüm içi booster o anda
  kullanılmaz. Royal Match son hamlede adaleti başka yoldan sağlıyor: koruyucu engeller son hamlede açık
  konuma geçiyor, oyuncu adaletsiz bir mekanikle kaybetmesin diye [8].
- **Booster bölümü bitirebilir.** Royal Match'te bölüm kazanılınca kalan her hamle tahtaya bir güç olarak
  düşer ve patlar; kırdığı her taş coin verir [11]. Bizim bitiş şöleni aynı fikrin sade hâli.
- **Booster kullanıp çıkmak.** Candy Crush'ta bölümden çıkmak kaybetmek sayılır: can gider, kullanılan
  booster'lar geri verilmez (özet) [36]. Candy Crush Jelly Saga'da çıkış canı ancak en az bir hamleden
  sonra götürür (özet) [36].

### 3.4 Engeller ve hedefler

- **Bir vuruş, bir kat:** Royal Match'in üç vuruş aracı da engelden bir kat alır [1]; Homescapes'in çekici
  iki zincirden birini kırar [15]; Toon Blast'ın çekici çok parçalı engelden bir parça alır [22]; Fishdom'un
  çapasında çok katlı engel birkaç vuruş ister, kırılmaz öğe kalır [21]. Matchington'ın çekici istisna:
  hücredeki her şeyi kaldırır [28]. Bizim motor bir eşleşmenin vurduğu gibi vurur; türün çoğunluğuyla
  aynı.
- **Hattaki özel taşlar:** Fishdom'un çapası sütundaki çoğu gücü tetikler [21]. Bizim motorda vurulan özel
  taş patlar; aynı.
- **Karıştırmada kalanlar:** Gardenscapes'te kilitli öğeler kalır [19]; Matchington'da ve Project
  Makeover'da özel taşlar da karışır [30][32]. Bizim motor kutuyu, zincirli taşı, özel taşı ve malzemeyi
  yerinde bırakır. İki tercih de türde var.
- **Hedefler:** booster'ın kırdığı her şey hedefe sayılır. Kaynaklar booster'ı açıkça hedefe ulaşmanın
  aracı olarak anlatır [15][19].

### 3.5 Açılış ve öğretici

- **Candy Crush:** çekiç 7. bölümde, oyunun açtığı ilk booster; Free Switch 19, çizgili fırça 37 (özet)
  [34]. Kaynaklar çelişiyor: bir viki tablosu çekici 15. bölüme koyuyor. Kardeş oyun Soda Saga'da çekiç
  7. bölümde tanıtılıyor ve oyun kullanmaya yönlendiriyor (2015, özet) [39]. Yeni oyuncu birkaç çekiçle
  başlıyor (özet).
- **Royal Match:** bölüm öncesi üç booster'dan 7. bölümde 3'er verilir (özet) [13]. Bölüm içi booster
  dağıtan etkinlikler 28. ve 39. bölümlerden sonra açılır (özet) [12]. Bölüm içi booster'ların kendi
  açılış bölümleri bulunamadı.
- **Toon Blast:** örs ve zar 17-19. bölümler civarında; kaynak yalnızca bir video başlığı [44].
- **Homescapes:** bölüm öncesi booster'lar 15, 17 ve 21. bölümlerde (özet) [43].
- **Seri ödülleri:** Cat's Gift 26, Butler's Gift 32. bölümde açılır [3][17].
- **Desen:** araçlar ilk 40 kadar bölüme yayılır. Her biri kendi bölümünde, tasarlanmış bir tahtada,
  oyuncuyu kullanmaya yönlendiren bir ilk kullanımla ve birkaç bedava adetle gelir. Açılış bölümleri
  kaynaklardan; tanıtımın biçimi [Gözlem].
- **[Gözlem] Kilitli görünüm:** açılmamış booster sönük ya da kilitli görünür, bazen açılacağı bölümü
  gösterir.

### 3.6 Sayılar ve satın alma

- **[Gözlem] Rozet:** diskin köşesinde adet; sıfırda adet yerine yeşil bir artı.
- **Sıfırda:** simgeye dokunmak bölümün içinde satın almayı açar [15][19][33].
- **Fiyat:** Candy Crush'ta çekiç 3, 10 ve 20'lik paketlerde 29, 69 ve 119 altın; ilk "+5 hamle" 10 altın
  (özet) [34][35]. Tek çekiç aşağı yukarı ilk "+5" kadar tutuyor. Royal Match'te ilk "+5" 900 coin [8];
  bölüm içi booster'ın coin fiyatı bulunamadı. Bizde yer tutucu fiyatlar: spatula 400, fırçalar 700,
  karıştırma 300 coin, "+5" 900 coin. Yani bizim spatulamız "+5"in yarısından ucuz. Bu yalnızca bir veri
  noktası; fiyat ekonomi simülatörünün işidir.
- **Sınır:** bölüm ya da deneme başına booster sınırı yazan bir kaynak yok.
- **Süreli sınırsız booster:** Gardenscapes, Project Makeover ve Matchington'da yalnızca bölüm öncesi
  booster'lar ve can için var [20][27][32]. Zamanlayıcı ödül alındığı an başlar ve her bölüme kendiliğinden
  uygulanır [20]. Candy Crush'ta 30 dakikalık süreli çekiç örneği de var (özet) [34].
- **Ödül olarak:** günlük bonus (Toon Blast, Fishdom) [21][22], oyun günü (Homescapes) [15], şans çarkı
  ve etkinlikler (Gardenscapes) [19], alan sandığı ve etkinlikler (Royal Match) [12][13].

### 3.7 Yerleşim

- **Alt şerit:** Royal Match [6] ve Toon Blast [23] booster'ları tahtanın altında tutar. Oyunların çoğunda
  dört, Homescapes'te beş bölüm içi booster var [14].
- **Bölüm öncesi booster'lar:** bölüm başı penceresinde [14][23].
- **Ölçü:** booster düğmesinin boyunu veren bir oyun kaynağı yok. Genel erişilebilirlik ölçüsü 44x44
  piksellik dokunma alanıdır (WCAG 2.5.5, AAA) [46].

## 4. Komşu sistemler

### Bölüm öncesi booster'lar (11. aşama)

- **Üç tür.** Her oyunda üç tane: Royal Match roket, TNT, ışık topu [1]; Homescapes gökkuşağı topu,
  bomba ve roket, çift uçak [14]; Toon Blast roket, bomba, disko topu [24]; Gardenscapes, Fishdom,
  Matchington ve Project Makeover da üçer [19][21][27][32]. Tahtada rastgele bir yere konur [1][14].
  Bölüm başı penceresinde seçilir; yoksa orada satın alınır [14].
- **Seri ödülü.** Butler's Gift (Royal Match, 32. bölüm): bir galibiyette roket ve pervane, ikincide ek
  TNT, üçüncüde ek ışık topu; kayıpta sıfır [3]. Cat's Gift (Homescapes, 26. bölüm): üç galibiyette bölüm
  başına üç güç; kayıpta sıfır [17]. Naavik bunu "seriye dayalı kayıp kaçınması" olarak okuyor ve kökenini
  Lily's Garden'a bağlıyor [7].
- **Bir deney.** Royal Match beş galibiyetten sonra zor bölümlerde 3 bedava hamle veren bir ödülü denemiş;
  deney başarısız sayılmış, çünkü coinle alınan ek hamlelerin yerini yemiş [9].

### "Hamle bitti" teklifi (12. aşama)

- **Royal Match:** ilk "+5" 900 coin [8]. 2022'de bu yaklaşık 2 dolardı, sonraki teklifler 4,35; 6,65 ve
  8,95 dolar [7]. İkinci teklif daha pahalı ve bir roket ekliyor; roket bölümü kazandıracak yere
  konuyormuş [8]. Kartta üzgün kral, sıfır sayısı ve satış teklifleri var [7].
- **Candy Crush:** ilk devam 10 altın ve 5 hamle; sonrakiler 16 altından başlayıp artıyor. Reklamla devam
  de var: üç reklam 2 ve 1 hamle, sonra bir UFO veriyor; günde sınırlı sayıda (özet) [35].
- **Kıyas (2022):** ilk "+5" Project Makeover'da yaklaşık 1,19, Fishdom'da 0,89 dolardı [7].
- **Bizde:** denemede bir kez; 900 coin ya da günün 3 bedava reklamından biri; "Az kaldı!" yalnızca
  hedeflerin en az %80'i bittiyse. Türden daha sade ve daha dürüst: fiyat merdiveni yok, ikinci teklif yok.

### Canlar

- **5 can, 30 dakikada bir:** Royal Match, Homescapes, Toon Blast [2][16][25]. Aylık pass ile en çok 8
  [2][16]. Coinle can satılır [5][16][25]. Takım arkadaşları can yollar. Süreli sınırsız can bir ödüldür.
  Lily's Garden için en çok 4 can diyen tek kaynak var (düşük güven) [41].
- **Bizde:** 5 can, yarım saatte bir; can satılmıyor.

### Adalet notları

- **[Resmî]** Royal Match ve Toon Blast her bölümün booster'sız ve parasız geçilebileceğini yazıyor
  [4][26]. Matchington imkânsız bölüm olmadığını söylüyor [31]. Homescapes zor bölümleri bölüm başı
  ekranının rengiyle açıkça işaretliyor [18].
- **[Analiz]** Royal Match bölümlerinin sık sık kıl payı kaybettirecek gibi ayarlandığı, bunun ek hamle
  satın almaya baskı yaptığı yazılıyor [7]. Oyuncular oyunu "hileli" buluyor; yazar zorluğun algoritmayla
  ayarlandığını düşünüyor ama kanıt sunmuyor [8].
- **[Resmî] Düzenleme:** AB tüketici koruma ağı (CPC) Mart 2025'te oyun içi paralar için ilkeler
  yayımladı: fiyat açık gösterilir, gerçek maliyet gizlenmez, oyuncu oyun parası almaya itilmez,
  çocuklara ayrıca özen gösterilir [45].
- **Bizim çizgimiz:** §17 ve §19 gizli zorluk ayarını, harcamaya bağlı zorluğu ve gerçek parayla şans
  kutusunu yasaklıyor. Türün dürüst yanları (booster'sız geçilebilirlik, bedava otomatik karıştırma [8])
  bizde zaten var.

## 5. ÖNERİLERİMİZ

Her madde: karar, türün desteklediği seçenek, neden, kaynak. "Bizim önerimiz" yazanların türde kaynağı
yok.

### Planın açık kararları

**1. Booster nasıl kurulur ve nişan alınır (karar 11).**
- **Öneri:** planın önerisi kalsın; üç ek yapılsın:
  - Hızlı tek dokunuş da çalışsın: en kısa basma süresi olmasın, ışık parmağın indiği karede yansın.
  - Hap yazısı aracın işini de söylesin. İki satır: "Spatula · bir taşı ya da bir katı kaldırır" ve
    "Bir taş seç". Satır ve sütun için "Yatay fırça · bir satırı boyar" ve "Bir satır seç".
  - Disk, taşlar düşerken de kurulabilsin; yalnızca uygulama tahtanın oturmasını beklesin (bizim
    önerimiz).
- **Neden:** tür tek dokunuşla çalışır ve kurulu hâlde aracın ne yaptığını söyler. Parmak altı önizleme
  bizim küçük hücrelerimizde hatayı azaltır; türden fazladır ama türle çelişmez. Kurma anında tepki
  vermek, Royal Match'in "oyuncuyu bekletme" dersiyle uyumlu.
- **Kaynak:** [1][10][15][19][28][33]; kurulu hâlin yazısı için [Gözlem].

**2. Hiçbir şey değiştirmeyecek kullanım (planın hedefi).**
- **Öneri:** planınki gibi: disk sallanır, hap nedenini söyler, hiçbir şey harcanmaz.
- **Neden:** tür booster'ı "ulaşılması zor hedef" için anlatır [15][19]; boşa giden kullanım üzerine bir
  kural yazılı değil. Candy Crush Jelly Saga vikisine göre etkisiz kalan booster oyuncuda kalabiliyor (özet)
  [36]. Bedava ret türün ruhuna uygun ve bizim §17 çizgimize de uyuyor.

**3. Boş diskteki artı (karar 9).**
- **Öneri:** planın önerisi kalsın: artı, tahtanın üstünde, o booster'ı tek fiyatından satan küçük kart
  açar; coin yetmezse düğme söner.
- **Ek (bizim önerimiz):** satın alma başarılıysa kart kapanınca booster doğrudan kurulu hâle geçsin,
  oyuncu bir dokunuş kazansın.
- **Neden:** Project Makeover, Homescapes ve Gardenscapes tam olarak böyle, simgeden bölüm içinde satar
  [15][19][33]. Tür çoğunlukla paket satıyor (Candy Crush 3, 10, 20) [34]; test sürümü için tek adet
  yeter, yayında paket düşünülebilir. Sönük düğme ve açık fiyat CPC ilkelerine uygun [45].

**4. Rafın tahtadan alacağı yer (karar 10).**
- **Öneri:** diskin görünen boyu 40 nokta kalsın, dokunma alanı 44 noktaya genişlesin; komşu disklerin
  alanları çakışmasın.
- **Neden:** oyun kaynakları ölçü vermiyor; tür dört-beş aracı tahtanın altında tutar [6][14][23].
  Erişilebilirlik ölçüsü 44x44 [46]. Görünmeyen dokunma alanı tahtadan yer almaz.

**5. Çizimi olmayan ekranlar (karar 22): kurulu booster ve satın alma kartı.**
- **Öneri:** planın önerisi kalsın: tuvalin dilinde yapılsın, sahibine resimle gösterilsin.
- **Neden:** türdeki kurulu hâl (kararmış çevre, öne çıkan disk, üstte ad ve iş yazısı) planın tarifiyle
  örtüşüyor [Gözlem].

**6. Deneme ne zaman başlar? (GAME_DESIGN §23: "Bu karar 10. aşamada verilecek")**
- **Öneri:** kabul edilen bir booster ilk hamle gibi saysın:
  - deneme başlar;
  - seçili bölüm öncesi booster'lar harcanır;
  - bundan sonra çıkmak can götürür;
  - deneme kaydı yazılır.
- **Neden:**
  - Candy Crush çıkışı kayıp sayar ve kullanılan booster'ı geri vermez (özet) [36].
  - Jelly Saga canı ancak bir hamleden sonra alır (özet) [36].
  - Yani türün çizgisi şu: tahtaya dokunmadan çıkmak bedava, tahtayı değiştirdikten sonra çıkmak kayıp.
  - Booster tahtayı değiştirir; bizim kuralımızdaki "yalnızca bakılmış tahta" artık o tahta değildir.
  - Kayıt yazılmazsa harcanan booster ekonomi ve kalibrasyon verisinde hiç görünmez.

**7. Sıfır hamlede booster (risk 7: simülatör izin veriyor, motor vermiyor).**
- **Öneri:** motorun kuralı kalsın: hamle bitince booster kullanılmaz, teklif kartı gelir. Simülatör bu
  kurala çekilsin; olmazsa fark belgede kalsın.
- **Neden:** türde hamle bitince kart gelir [Gözlem]. Royal Match son hamledeki adaleti booster'la değil,
  engelin açık konumuyla sağlıyor [8].

**8. Booster'ın ekranda süresi (planın ses ve görüntü işi).**
- **Öneri (bizim önerimiz):** aracın işareti ve etkisi bir saniyeyi geçmesin, ardından tahta bir hamleden
  sonraki gibi otursun. Bitiş şöleninden kısa olsun. Vuruşa 14. aşamada kısa bir titreşim tıkı eklensin.
- **Neden:** süre yazan bir kaynak yok. Royal Match akıcılığı ve dokunuş titreşimini bir artı olarak
  öne çıkarıyor [10][47].

**9. Kalibrasyon verisi (risk 9).**
- **Öneri:** madde B'deki açılış kapısı ilk bölümleri booster'sız tutar.
- **Neden:** tür booster'ları zaten 7. bölümden sonra açıyor [13][34]. Böylece ilk bölümlerin insan verisi
  temiz kalır.

### Planın sormadığı ama türün gösterdiği

**A. Reddedilen karıştırma. En önemli madde.**
- **Durum:** motor eşleşme içeren her dizilimi reddediyor. 3. aşamanın notuna göre büyük açık tahtalarda
  ilk istekler sık sık reddediliyor (GAME_DESIGN §23).
- **Tür:** hiçbir kaynakta karıştırma booster'ının "olmuyor" dediği bir durum yok. Royal Match hamle
  kalmayınca bile tahtayı bedava ve hemen karıştırıyor; oyuncular bunu emeğe saygı olarak görüyor [8].
- **Öneri:** motor, ilk çekilen dizilimde eşleşme varsa reddetmesin. Karıştırma akışından yeni dizilimler
  çekmeyi bir üst sınıra kadar sürdürsün ya da eşleşmesiz bir dizilimi kurarak bulsun. Ret yalnızca
  gerçekten imkânsız tahtada kalsın (örneğin tek serbest taş). "Tahta karıştırılamıyor" yazısı da yalnızca
  o zaman görünsün.
- **Dikkat:** bu motorun davranışını değiştirir.
  - Kamuya açık API aynı kalır: `Game.TryUseBooster` daha sık kabul eder.
  - Deneme kayıtlarındaki "reddedilen karıştırma" satırları ve `AttemptReplay` etkilenir. Eski kayıtların
    aynı sona yeniden oynatılabilmesi için kural ya sürümlenmeli ya da yalnızca yeni denemelerde
    geçerli olmalı.
  - Bu yüzden sahibine sorulacak bir karardır.

**B. Açılış kapısı ve ilk tanıtım.**
- **Öneri:** diskler ilk bölümlerde kilitli dursun: sönük, üstünde "Bölüm N". Her araç kendi bölümünde bir
  kez halkayla ve tek cümlelik bir hap yazısıyla tanıtılsın.
- **Bölümler:** türün aralığı çekiç için 7-10, satır ve sütun için 12-20, karıştırma için 20-25 civarı
  [34][44]. Bölümleri sahibi seçer.
- **Neden:** tür booster'ı birer birer tanıtır ve oyuncuyu ilk kullanıma yönlendirir [34][39]. Bölümler
  booster'sız kalibre edildiği için kapı zorluğu değiştirmez; yalnızca ilk verileri temiz tutar (risk 9).
- **Sahibine soru:** hoş geldin hediyesi bugün başta her booster'dan 3 veriyor (2026-10-14 kararı). Bölüm
  içi booster'ların 3'er adedi kendi açılış bölümlerinde verilebilir. Royal Match bölüm öncesi
  booster'ları böyle, 7. bölümde veriyor [13]. Bu, kararı değiştirir; sahibine sorulmalı.

**C. Yayında fiyatın gerçek para karşılığı.**
- **Öneri:** test sürümünde gerçek para yok, yapılacak bir şey yok. Yayın öncesinde coinle fiyatlanan
  booster'ın yanında gerçek para karşılığının nasıl gösterileceği ve satın alma kartının baskı yapıp
  yapmadığı CPC ilkelerine göre gözden geçirilmeli [45].

**D. Kaçınılacak iki tür alışkanlığı.**
- **Ödemeden sonra yardım:** Royal Match'in ikinci teklifine eklenen ve kazandıracak yere konan roket [8],
  ödemeden sonra devreye giren hedefli bir yardımdır. §17 ve §19 ile çelişir; bizde olmamalı.
- **Kayıpta sıfırlanan seri ödülü:** Butler's Gift ve Cat's Gift kayıpta sıfırlanıyor [3][17]; Naavik bunu
  kayıp kaçınması baskısı olarak okuyor [7]. İleride bir seri ödülü istenirse kayıpta sıfırlanmayan, yavaş
  azalan bir biçim §17'ye daha uygun olur.

**E. Açık söz.**
- **Öneri:** "Her bölüm booster'sız geçilebilir" cümlesi yardım ya da ilk tanıtım kartına konabilir.
- **Neden:** Royal Match ve Toon Blast bunu yazıyor [4][26]. Bizde ölçümle de doğru, çünkü kalibrasyon
  booster'sız yapılıyor (§18).

## 6. Kaynaklar

Hepsi 2026-10-15'te okundu. "(özet)" yazanların sayfası otomatik okumayı reddetti; yalnızca arama
motorunun özeti kullanıldı.

1. Royal Match Help Center, "Using Boosters". https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/8-using-boosters/
2. Royal Match Help Center, "Lives". https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/5-lives/
3. Royal Match Help Center, "What is Butler's Gift?". https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/78-what-is-butler-s-gift/
4. Royal Match Help Center, "Level is too hard.". https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/9-level-is-too-hard-1661775375/
5. Royal Match Help Center, "How can I obtain and use coins?". https://dreamgames.helpshift.com/hc/en/3-royal-match/faq/21-how-can-i-obtain-and-use-coins/
6. Google Play editoryali, "Boosters in Royal Match and how to use them" (tarihsiz). https://play.google.com/store/apps/editorial?id=mc_games_editorialevergreen_postinstall_boosters_royal_match_now_fcp&hl=en_US
7. Naavik, N. Tuerlings, "Royal Match: Dream Games' Regal Performance", 2022-05-25. https://naavik.co/deep-dives/royal-match/
8. Naavik, H. Karvande, "The Royal Blueprint: Easy to Copy … Or Not?", 2025-08-24. https://naavik.co/digest/why-dream-games-success-is-a-challenge-to-replicate/
9. Naavik, H. Karvande, "Royal Match: Success Through Iteration", 2025-02-23. https://naavik.co/digest/royal-match-finding-success-through-iteration/
10. UserWise, "5 Simple UX Lessons From Royal Match", 2024-01-30. https://thisweekinliveops.substack.com/p/5-simple-ux-lessons-from-royal-match
11. Oldcynic, "Royal Match Tips & Tricks for Beginners", 2024-01-28, güncelleme 2026-02-06. https://oldcynic.com/royal-match-tips-and-tricks-cheats-for-new-players
12. Royal Match Wiki (Fandom), "Boosters" ve etkinlik sayfaları (özet). https://royalmatch.fandom.com/wiki/Boosters
13. Twinfinite, "How to get boosters in Royal Match" (özet). https://twinfinite.net/guides/how-to-get-boosters-royal-match/
14. Homescapes Help Center, "What are boosters and how to use them?". https://playrix.helpshift.com/hc/en/14-homescapes/faq/16364-what-are-boosters-and-how-to-use-them/?l=en
15. Homescapes Help Center, "Hammer booster: how does it work?". https://playrix.helpshift.com/hc/en/14-homescapes/faq/16319-hammer-booster-how-does-it-work/?l=en
16. Homescapes Help Center, "What are Lives and how to refill them?". https://playrix.helpshift.com/hc/en/14-homescapes/faq/16349-what-are-lives-and-how-to-refill-them/?l=en
17. Homescapes Help Center, "Cat's Gift". https://playrix.helpshift.com/hc/en/14-homescapes/faq/16312-cat-s-gift/?l=en
18. Homescapes Help Center, "Match-3 level types by difficulty". https://playrix.helpshift.com/hc/en/14-homescapes/faq/43938-match-3-level-types-by-difficulty/?l=en
19. Gardenscapes Help Center, "What's a booster? What are they for?". https://playrix.helpshift.com/hc/en/5-gardenscapes/faq/17462-what-s-a-booster-what-are-they-for-1772790359/?l=en
20. Gardenscapes Help Center, "Why do my Unlimited Lives and Boosters start right after I claim them?". https://playrix.helpshift.com/hc/en/5-gardenscapes/faq/15711-why-do-my-unlimited-lives-and-boosters-start-right-after-i-claim-them/
21. Fishdom Help Center, "Boosters". https://playrix.helpshift.com/hc/en/4-fishdom/faq/16325-boosters/?l=en
22. Toon Blast Help Center, "What are boosters?". https://peakgames.helpshift.com/hc/en/4-toon-blast/faq/8-what-are-boosters/
23. Toon Blast Help Center, "Where are the items that I get?". https://peakgames.helpshift.com/hc/en/4-toon-blast/faq/14-where-are-the-items-that-i-get/
24. Toon Blast Help Center, "What are the special items?". https://peakgames.helpshift.com/hc/en/4-toon-blast/faq/7-what-are-the-special-items/
25. Toon Blast Help Center, "What are lives (hearts) and how do I get them?". https://peakgames.helpshift.com/hc/en/4-toon-blast/faq/6-what-are-lives-hearts-and-how-do-i-get-them/
26. Toon Blast Help Center, "I cannot pass the level / Level is too hard". https://peakgames.helpshift.com/hc/en/4-toon-blast/faq/15-i-cannot-pass-the-level-level-is-too-hard/
27. Matchington Mansion Help Center, "Boosters" bölümü. https://magictavern.helpshift.com/hc/en/3-matchington-mansion/section/14-boosters/
28. Matchington Mansion Help Center, "Hammer". https://magictavern.helpshift.com/hc/en/3-matchington-mansion/faq/81-hammer/
29. Matchington Mansion Help Center, "Glove". https://magictavern.helpshift.com/hc/en/3-matchington-mansion/faq/79-glove/
30. Matchington Mansion Help Center, "Bell". https://magictavern.helpshift.com/hc/en/3-matchington-mansion/faq/914-bell/
31. Matchington Mansion Help Center, "I've been stuck on a level for several days, are some levels impossible to win?". https://magictavern.helpshift.com/hc/en/3-matchington-mansion/faq/14-i-ve-been-stuck-on-a-level-for-several-days-are-some-levels-impossible-to-win/
32. Project Makeover Help Center, "Boosters" bölümü. https://magictavern.helpshift.com/hc/en/4-project-makeover/section/32-boosters-1698993739/
33. Project Makeover Help Center, "Scissors". https://magictavern.helpshift.com/hc/en/4-project-makeover/faq/531-scissors/
34. Candy Crush Saga Wiki (Fandom), "Lollipop Hammer", "Free Switch", "Booster" (özet). https://candycrush.fandom.com/wiki/Lollipop_Hammer
35. Candy Crush Saga Wiki (Fandom), "Extra Moves", "Continue" (özet). https://candycrush.fandom.com/wiki/Extra_Moves
36. Candy Crush Saga Wiki, "Lives"; Candy Crush Jelly Saga Wiki, "Boosters" (özet). https://candycrush.fandom.com/wiki/Lives ve https://candycrushjelly.fandom.com/wiki/Boosters
37. King Community, "Attention Candy Crush Saga Fans – Do You Know All the Boosters?", 2025-08-24, oyuncu yazısı. https://community.king.com/en/candy-crush-saga/discussion/506710/attention-candy-crush-saga-fans-do-you-know-all-the-boosters
38. BlueStacks, "Candy Crush – All the Boosters and Special Candies", 2020-02-12. https://www.bluestacks.com/blog/game-guides/candy-crush/ccs-booster-guide-en.html
39. TouchArcade, "Candy Crush Soda Saga beginner's level guide, levels 1 through 20", 2015-02-18 (özet). https://toucharcade.com/2015/02/18/candy-crush-soda-saga-beginners-level-guide-levels-1-through-20/
40. Lily's Garden Wiki (Fandom), "Boosters" (özet). https://lilysgarden.fandom.com/wiki/Boosters
41. BlueStacks, "Lily's Garden features guide", 2024-01-23. https://www.bluestacks.com/blog/game-guides/lilys-garden/lg-features-guide-en.html
42. Homescapes Wiki (Fandom), "Boosters" (özet). https://homescapes.fandom.com/wiki/Boosters
43. AppGamer, "Homescapes: what are boosters" (özet). https://www.appgamer.com/homescapes/strategy-guide/what-are-boosters
44. YouTube, "Unlock Anvil & Dice Booster! … Toon Blast Levels 17, 18, and 19": yalnızca arama sonucundaki başlık ve açıklama; video izlenmedi. https://www.youtube.com/watch?v=CfZt_rLzfdY
45. Avrupa Komisyonu, CPC ağının oyun içi para ilkeleri üzerine paydaş toplantısı haberi, 2025-06-03 (ilkeler Mart 2025). https://commission.europa.eu/news-and-media/news/european-commission-hosts-stakeholders-talks-application-cpc-networks-key-principles-games-virtual-2025-06-03_en
46. W3C, "Understanding SC 2.5.5: Target Size (Enhanced)", WCAG 2.2. https://www.w3.org/WAI/WCAG22/Understanding/target-size-enhanced.html
47. Funovus, "Royal Match Dominates Match-3: What Can ALL Designers Learn?", 2024 (tarihsiz). https://www.funovus.com/blogs/royal-match-dominates-match-3-what-can-all-designers-learn/
48. King, Candy Crush Jelly Saga yardım merkezi, "Lollipop Hammer" (özet). https://candycrushjelly.zendesk.com/hc/en-us/articles/360001150289--Lollipop-Hammer
49. King, Candy Crush Saga yardım merkezi, "What types of Boosters are available?" (özet). https://candycrush.zendesk.com/hc/en-us/articles/211320729-What-types-of-Boosters-are-available
