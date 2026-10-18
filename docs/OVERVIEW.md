# Genel Belge — Oyundan Yazılıma

> Bu belge projeyi hiç görmemiş biri için yazıldı: yeni bir ekip üyesi, bir yayıncı ya da bir yıl sonra dönüp bakan proje sahibi. Oyunu, içeriğini, ekonomisini, yazılımını, testlerini ve bugünkü durumunu tek yerde özetler. Durum tarihi: **2026-10-15**.
>
> Ayrıntının ve gerekçelerin asıl yeri tasarım belgesidir ([GAME_DESIGN.md](GAME_DESIGN.md)); bu belgede "Tasarım §N" o belgenin N. bölümü demektir. Ne zaman ne yapıldığı proje günlüğündedir ([DEVLOG.md](DEVLOG.md)). İki kaynak çelişirse yenisi geçerlidir.

## 1. Oyun bir bakışta

**Tür.** Swap match-3: oyuncu yan yana iki taşın yerini değiştirir, aynı renkten üç ya da daha fazla taş dizilince patlar (Royal Match ve Candy Crush'ın türü). Farkı tahtadadır: tahtalar büyüktür ve boyutu bölümden bölüme değişir (9x10'dan 11x13'e; Tasarım §2, §9).

**Tema.** Genç bir ressamın sevdiği müze bir felakette hasar görmüş, tablolar parçalanıp dağılmıştır. Ressam bölüm kazandıkça parçaları toplar, tabloları yapboz gibi birleştirip yerlerine asar ve müzeyi oda oda yeniden açar. Tablolar telifsiz (kamu malı) gerçek eserlerdir; her odanın kendi dönemi vardır (Tasarım §18).

| Birim | Bölüm | Not |
|---|---|---|
| 1 tablo | 10 bölüm | Tablonun parçaları bu 10 bölümle kazanılır |
| 1 oda | 50 bölüm (5 tablo) | Her oda bir dönem; 5. tablo odanın finalidir |
| Müze | 1000 bölüm (100 tablo, 20 oda) | Yayında 1000 bölümün tamamı bulunur |

**Hedef oyuncu.** 30 yaş üstü, kısa oturumlarla oynayan rahat oyuncu; içerik aileye uygundur (Tasarım §19).

**Platform.** iOS ve Android, Unity ve C#. İstemci bugün yalnızca Android için derleniyor; iOS sonra gelecek. Oyun internetsiz oynanır; bölümler uygulamanın içinde gelir, düzeltmeler küçük paketlerle iner (Tasarım §20, §23).

**Yayın yolu (karar 2026-10-05).** Önce APK ile kapalı test (proje sahibi ve birkaç kişi), sonra doğrudan dünya geneli; sınırlı ülke yayını yok. Bu yüzden yayın öncesi zorluk bilerek yumuşak tutulur (Tasarım §20).

**İş modeli.** Ücretsiz oyun, uygulama içi satın alma (booster, ekstra hamle, altın) ve isteğe bağlı ödüllü reklam (Tasarım §19). Bugünkü derlemede gerçek parayla hiçbir şey satılmaz ve reklam yalnızca bir canlandırmadır; ayar dosyası derlemenin bunu açıkça beyan etmesini ister (`build.ads: simulated`, `build.realMoneyPurchases: notAvailable`).

**Etik sınırlar** (Tasarım §17, §19). Bunlar ürün kararıdır, motorda ve ayarlarda korunur:

- Oyuncunun para harcama eğilimi zorluğu etkilemez; harcama ya da ödeme verisi zorluk ayarına girdi olmaz.
- Zorluk gizlice ayarlanmaz. Tekrar denemede taşlar farklı desende gelir ama kurallar, hamle sayısı ve zorluk aynıdır. Dinamik zorluk (DDA) motorda yalnızca kolaylaştırma yönünde vardır ve oyunda kapalıdır.
- Gerçek parayla rastgele kutu satılmaz. Ana tabloların parçaları altınla satılmaz; yalnızca oynayarak kazanılır.
- "Az kalsın kaybettin" anları (near-miss) hedeflenmez, yalnızca ölçülür.
- Toplanan veride kimlik yalnızca rastgele bir cihaz kimliğidir; isim, e-posta ve reklam takibi yoktur. KVKK ve GDPR için rıza ekranı ve gizlilik politikası planlıdır (Tasarım §20).

**Ad ve ekip.** "Puzzle" bir çalışma adıdır; oyunun adı en sona bırakıldı (karar 2026-10-05). Paket kimliği (`com.saturnwave.puzzle`) de geçicidir. Proje sahibi SaturnWave'dir; kod, içerik ve belgeler sahibiyle Claude tarafından üretilir. Görsel yön, sahibinin Claude Design'da çizdirdiği tasarım tuvalidir (Tasarım §22).

## 2. Nasıl oynanır

### Tahta ve takas

Oyuncu bir taşı komşusuna sürükler ya da iki taşa sırayla dokunur. Takas bir eşleşme doğurmuyorsa taşlar geri seker ve hamle düşmez. Yatay ya da dikey 3+ aynı renk ya da 2x2 kare eşleşmedir. Patlayan taşların yerine üsttekiler düşer, yukarıdan yenileri gelir; yeni eşleşmeler zincirleme patlar. Geçerli hamle kalmazsa tahta ücretsiz karıştırılır (Tasarım §3, §5).

Taşlar renk körlüğüne uygun sabit bir settir; her rengin kendi şekli vardır: uğur böceği (kırmızı), damla (mavi), yuvarlak köşeli kare (yeşil), beyaz yapraklı papatya (sarı), koni (mor), hilal (turuncu); sonradan gelen iki renk elmas (camgöbeği, 301. bölümden sonra) ve laledir (pembe, 601. bölümden sonra). Bir bölümde 4 ile 8 arası renk vardır; standart 6'dır (Tasarım §9, §22).

### Özel taşlar

Büyük eşleşmeler özel taş bırakır. Özel taşlar renksizdir: eşleşmeye katılmaz, dokunarak (1 hamle), bir komşuyla yer değiştirerek ya da başka bir patlamanın vurmasıyla patlar. İki özel taşı yer değiştirmek birleşik bir etki verir (Tasarım §3).

| Desen | Motordaki adı | Oyundaki görünüşü | Etki |
|---|---|---|---|
| 4'lü düz | Roket | Fırça kovanı | Bütün satırı ya da sütunu vurur |
| L, T ya da + (5 taş) | Bomba | Mürekkep sıçraması | 3x3 alan |
| 6+ taşlı kesişim | Büyük Bomba | Yaldız kuşaklı büyük sıçrama | 5x5 alan |
| 2x2 kare | Pervane | Kelebek | Çevresini vurur, sonra hedefe en çok katkı verecek hücreye uçar |
| 5'li düz | Renk Bombası | Gökkuşağı paleti | Değiştirildiği taşın rengindeki bütün taşları vurur |

### Engeller ve hedefler

Her bölümün bir ile üç hedefi ve sınırlı bir hamle sayısı vardır. Bir bölümde en çok 4 farklı mekanik bulunur (Tasarım §4).

| Mekanik | Oyundaki görünüşü | Davranış | Hedefi |
|---|---|---|---|
| Kutu (1–3 can) | Çatlak, nakliye ya da demirli sandık | Düşmez; yanındaki eşleşme ya da bir etki bir can götürür | Kutuları kır |
| Jöle (1–2 kat) | Eski vernik katmanı | Zemindedir; üstündeki hücre vurulunca bir kat gider | Jöleleri temizle |
| Zincir (1–2 can) | Koruma bandı | Taşı kilitler; taş eşleşir ama kaymaz, vuruş önce bandı kırar | Zincirleri kır |
| Malzeme | Kayıp çerçeve köşesi | Düşer, eşleşmez; tahtanın altından çıkınca toplanır | Malzemeleri indir |
| — | — | — | N tane X renkli taş topla |

Yeni mekanikler tablo başlarına hizalı bir takvimle gelir: kutu 11, jöle 21, iki canlı kutu 31, zincir 41, malzeme 51 (ikinci oda bununla açılır), iki katlı jöle 71, üç canlı kutu 91; 120'den sonra iki, 200'den sonra üç engel türü birlikte (Tasarım §9). Özel taşların ilk kez öğretileceği bölümler (3, 5, 8, 12, 15) için kısa anlatımlar planlıdır, istemcide henüz yoktur.

### Hamleler, kazanma, kaybetme, tekrar

Hedefler tamamlanınca bölüm kazanılır; hamle biterse kaybedilir. Kaybeden oyuncu aynı bölümü yeniden dener: taşlar her denemede farklı desende gelir, ama her bölümün botlarla ölçülmüş bir **eşit zorluk havuzu** vardır (en az 8 tohum) ve k. deneme havuzun k. tohumunu kullanır. Herkesin ilk denemesi havuzun ilk tohumudur (Tasarım §17, §23).

**Bitiş şöleni.** Bölüm hamle kalmışken kazanılınca kalan her hamle madalyondan bir kıvılcım olarak tahtaya uçar, bir taşı fırça kovanına çevirir, kovanlar patlar ve kalan hamle başına 4 altın ödenir. Şölen en çok 5,5 saniye sürer; dokunuş ya da geri tuşu atlatır. Kazanım, kazandıran hamle oynandığı anda kaydedilir; şölen ölçüme ve sonuca dokunmaz (Tasarım §19, §23).

### Canlar

Oyuncunun en çok 5 canı vardır; 30 dakikada bir can dolar. Can yalnızca kayıpta gider: hamle bitince kaybetmek ya da en az bir hamle yaptıktan sonra bölümden çıkmak bir can götürür; hamle yapmadan çıkmak ve bölüm ortasında uygulamayı kapatmak ücretsizdir. Canı biten oyuncu bölüm açamaz, can kartı sonraki canın süresini gösterir (Tasarım §19, §23).

### Altın, booster'lar ve "+5 hamle"

**Altın (coin)** booster, "+5 hamle" ve ileride yan koleksiyon parçaları için harcanır. Kazanma yolları: bitiş şöleni (kalan hamle başına 4), tamamlanan tablo (80), reklam izlemek (4, günde 3 kez). Bölüm kazanmanın sabit ödülü ve günlük altın ödülü yoktur (Tasarım §25).

**Booster'lar.** Yedi tür vardır. Bölüm öncesi üç booster bölüm başı kartında seçilir (bir denemeye üçü birden alınabilir) ve ilk hamlede harcanır. Bölüm içi dört booster hamle saymaz. Hiçbir şeyi değiştirmeyecek bir kullanım reddedilir ve booster harcanmaz. Botlar kalibrasyonda booster kullanmaz: zorluk booster'sız ölçülür, booster oyuncu için gerçek bir avantaj olarak kalır (Tasarım §18).

| Booster | Ne zaman | Etki | Fiyat (altın) |
|---|---|---|---|
| Fırça + sıçrama | Bölüm öncesi | Tahtada bir roket ve bir bombayla başla | 200 |
| Gökkuşağı paleti | Bölüm öncesi | Tahtada bir renk bombasıyla başla | 500 |
| +3 hamle | Bölüm öncesi | Hamle limiti 3 artar | 200 |
| Spatula | Bölüm içi | Tek bir hücreyi vurur | 400 (yer tutucu) |
| Yatay fırça | Bölüm içi | Bir satırı vurur | 700 (yer tutucu) |
| Dikey fırça | Bölüm içi | Bir sütunu vurur | 700 (yer tutucu) |
| Paleti karıştır | Bölüm içi | Tahtayı yeniden dizer (taş boyamaz) | 300 (yer tutucu) |

**"+5 hamle" teklifi.** Hamlesi biten oyuncu 5 hamle daha alabilir: günde 3 kez reklamla ücretsiz, sonra 900 altın. Bir denemede bir kez alınır; tahtada hamle kalmadıysa önce karıştırılır, karıştırmayla da hamle çıkmayan tahtaya teklif satılmaz (Tasarım §19). Zorluk teklifsiz limite göre ayarlanır; raporlar limit+5'teki kazanma oranını da gösterir.

**Hoş geldin hediyesi.** Yeni oyuncu 200 altın ve her booster'dan 3 (toplam 21) ile başlar (karar 2026-10-14; Tasarım §25).

### Müze, harita, koleksiyon ve yapboz

Oyun müzenin **salonuna** açılır: odanın duvarı, onarılmakta olan tablo, tablonun ilerleme noktaları, büyük "Bölüm N" düğmesi ve en üstte altın ile can satırı. Alt çubukta Koleksiyon, Müze ve Harita vardır. **Harita** 20 odanın kat planıdır. **Koleksiyon** tamamlanan tabloları büyük açar, ulaşılmamış olanları örtülü gösterir (Tasarım §23).

**Yapboz.** Her kazanılan bölüm tablosunun parçalarını verir; parçalar tepsiye düşer, oyuncu hemen ya da sonra yerleştirir. Parça sürüklenir, yerine yaklaşınca oturur; döndürme yoktur; isteyen "Otomatik tamamla"ya basar. Tablo bitince kutlama, oda bitince "Oda onarıldı!" gelir. Kesimi algoritma üretir; aynı tohum aynı parçaları verir (Tasarım §18).

| Bölümler | Tablo başına parça | Bölüm başına parça | Dizilim (yatay tablo) |
|---|---|---|---|
| 1–100 | 10 | 1 | 3-4-3 (sıralar 3, 4 ve 3 parçalı) |
| 101–300 | 20 | 2 | 5×4 |
| 301–600 | 30 | 3 | 6×5 |
| 601–1000 | 40 | 4 | 8×5 |

Dikey tablolarda dizilim 90 derece döner. Müzenin onarımı hafiftir: tamamlanan tablo duvara asılır; oyuncunun dekorasyon seçimi yoktur.

### Bir bölümün yolculuğu (istemcide bugün)

1. Salonda "Bölüm N"ye basılır; can yoksa can kartı gelir.
2. **Bölüm başı kartı** iner: bölüm, verdiği parça ("ÖDÜL · PARÇA k / m"), hedefler, hamle madalyonu ve üç bölüm öncesi booster. Hiç kalmamış bir booster artıyla görünür ve karttan tek fiyatına alınabilir. "Oyna" tahtayı açar.
3. **Tahta ekranı:** solda "BÖLÜM N" ve hedefler, sağda "PARÇA k / m" ve tablonun örtülü küçük kopyası (kazanılacak parça yaldızla aydınlanır), hamle madalyonu, altta ahşap raf. Rafın beş yerinden biri duraklatmadır, dördünde bölüm içi booster'lar durur: spatula, satır fırçası, sütun fırçası, karıştırma. Bunlar 7., 12., 16. ve 20. bölümde birer birer açılır. Bir booster tek dokunuşla seçilir ve hedefe dokununca uygulanır; hamle saymaz, ama denemeyi başlatır.
4. **Duraklatma** çıkma kartını açar; kart çıkmanın bedelini söyler (hamleden sonra bir can, önce hiçbir şey).
5. **Kazanma kartı:** şölenden sonra kazanılan parça bir oyukta, ilerleme noktaları, sayarak artan "+N" altın ve "Kalan N hamle fırçaya dönüştü". "Devam" salona döner; yeni parça yapbozda yerleştirilir.
6. **Hamle bitince:** hedefler kalmışsa "+5 hamle" teklifi gelir (900 altın ya da günde 3 ücretsiz reklam; denemede bir kez). "Vazgeç" bir can götürür ve kaybetme kartını açar; "Tekrar dene" aynı bölümün kartına götürür.

**Müzenin dört yeri:** çubukta Mağaza, Koleksiyon, Müze (salon) ve Harita. Mağazada altın paketleri "Yakında" (test sürümü parayla satış yapmaz), iki booster seti ve günde 3 reklamla 4'er altın var. Salondaki "Hediye" çerçevesi takvim gününde bir booster verir; bir tablonun son parçası 80 altın ve bir booster getirir. Cüzdan satırındaki çark ayarlar kartını açar: Dil (Türkçe), Gizlilik ve veri (deneme kayıtlarının sayısı ve "Kayıtları sil"), Destek (oyuncu kimliği, sürüm, kayıt dosyasının yeri). Ses ve Titreşim anahtarları yer tutucu sesleri ve kısa titreşim tıklarını açıp kapatır. Müzenin dört sayfası arasında sola-sağa kaydırarak geçilir; koleksiyonda oda plaketinde kaydırmak odayı değiştirir. Test sürümünde gizli bir test modu var (Destek kartında sürüm satırına 7 dokunuş): altın ve can sınırsız olur, denemeler kayıtta işaretlenir. Takvim ve etkinlik çerçeveleri tasarım tuvalinde çizili, istemcide henüz yok. İpucu motorda ve web laboratuvarında var, istemcide yok.

## 3. İçerik

### Müzenin 20 odası

Odalar 1. ve 2. odadan (Empresyonizm, Post-empresyonizm) sonra zamanda geriye doğru gider; her oda bir dönemdir, tür serbesttir (öneri onayı 2026-10-14; Tasarım §18). Finaller odanın 5. tablosudur.

| Oda | Bölümler | Dönem | Final |
|---|---|---|---|
| 1 | 1–50 | Empresyonizm | Buğday Yığınları (Yaz Sonu) — Monet |
| 2 | 51–100 | Post-empresyonizm | Grande Jatte Adası'nda Bir Pazar Öğleden Sonrası — Seurat |
| 3 | 101–150 | Kuzey Işığı: İsveç, Finlandiya, Danimarka (1875–1899) | Büyük Huş Ağacının Altında Kahvaltı — Larsson |
| 4 | 151–200 | Amerika: Yaldızlı Çağ (1870–1899) | Kırbaç Oyunu — Homer |
| 5 | 201–250 | Hollanda: Lahey Okulu ve Amsterdam (1860–1899) | Sahilde Sabah At Gezintisi — Mauve |
| 6 | 251–300 | Japonya: Edo'nun Son Yılları ve Meiji (1840–1895) | Shin-Ohashi'de Ani Sağanak — Hiroşige |
| 7 | 301–350 | Fransa: Barbizon, Gerçekçilik ve Salon (1830–1885) | Prenses de Broglie — Ingres |
| 8 | 351–400 | Amerika: Hudson Nehri ve Sınır Boyları (1825–1870) | Washington Delaware Nehri'ni Geçerken — Leutze |
| 9 | 401–450 | Danimarka Altın Çağı (1810–1860) | Kolezyum'un Üç Kemeri — Eckersberg |
| 10 | 451–500 | Romantik Britanya: Constable ve Turner (1800–1850) | Lordlar ve Avam Kamaralarının Yanışı — Turner |
| 11 | 501–550 | Edo Japonyası: Hokusai ve Hiroşige (1790–1840) | Kanagawa Açıklarında Büyük Dalga — Hokusai |
| 12 | 551–600 | Devrimler Çağı: Amerika ve Avrupa (1770–1825) | Kırmızılı Çocuk — Goya |
| 13 | 601–650 | Kral George Çağı Britanyası (1740–1800) | Elizabeth Farren — Lawrence |
| 14 | 651–700 | Şehir Manzaraları: Venedik, Roma, İstanbul (1720–1790) | San Marco Meydanı — Canaletto |
| 15 | 701–750 | Rokoko Fransası (1715–1789) | Kitap Okuyan Genç Kız — Fragonard |
| 16 | 751–800 | Hollanda Altın Çağı: Vermeer Kuşağı (1650–1700) | Küçük Sokak — Vermeer |
| 17 | 801–850 | Barok Avrupa: Hollanda, Flandre ve Fransa (1600–1650) | Otoportre — Leyster |
| 18 | 851–900 | İran ve Babür Sarayları: Minyatür Çağı (1520–1650) | At Üstünde Şah Cihan — Payag |
| 19 | 901–950 | Rönesans Avrupası: Holbein'den Arcimboldo'ya (1500–1600) | Vertumnus — Arcimboldo |
| 20 | 951⁠–⁠1000 | Flandre ve İtalya: 15. Yüzyıl (1430–1500) | Giuliano de' Medici — Botticelli |

### Tabloların kaynağı ve lisansları

- **Kural:** yalnızca üzerinde telif olmayan eserler; görüntü yalnızca eserin sahibi müzenin kendisinin her kullanıma serbest bıraktığı fotoğraf olabilir. Mauritshuis görüntülerini ticari kullanıma serbest bırakmadığı için kullanılmaz (Tasarım §18; DEVLOG Oturum 20).
- **Kaynaklar:** 1. ve 2. odanın 10 tablosu Art Institute of Chicago'dan; 3–20. odaların 90 tablosu National Gallery of Art (32), The Met (30), Rijksmuseum (13), SMK (6), Nationalmuseum (3), Cleveland (2), Skokloster Şatosu (2), Getty (1) ve Finlandiya Ulusal Galerisi'nden (1).
- **Kayıt:** her tablonun müzesi, sayfası, kaynağı, lisans cümlesi ve varsa kırpma kutusu `art/paintings/room-NN/licences.json` dosyasındadır; oyunun okuduğu katalog `museum/catalogue.json`'dır (başlık, özgün başlık, ressam, yıl, yön, görüntü, kaynak, lisans).
- **Görüntüler:** uzun kenar 1920 piksel, asla büyütülmez; 50 tablonun çerçevesi ya da paspartusu kırpıldı. İndirme `tools/unity/fetch-paintings.js`, kırpma `tools/unity/prepare-painting.py` ile yapılır; aynı indirme her zaman aynı baytları verir. Dosyalar `unity/Assets/Resources/Paintings/` altındadır.
- **Yazı tipleri:** Gabarito, Paytone One ve Young Serif, SIL Open Font License ile; lisans metinleri `art/fonts/licences.json`'da.
- **Yan koleksiyon** (özgün ve etkinlik tabloları) karar olarak vardır, içerik olarak henüz yoktur.

### 1000 bölüm nasıl yapılır

Bölümler elle değil algoritmayla yapılır ve **botlarla ölçülerek** ayarlanır. Temel fikir: bot kalan hamleye bakmadan oynadığı için sınırsız hamleyle oynatılan oyunların "kaçıncı hamlede bittiği" kaydedilir; herhangi bir limitteki kazanma oranı bu dağılımdan okunur (Tasarım §8).

**Bölüm dosyası.** Her bölüm bir JSON dosyasıdır (`levels/generated/level_NNNN.json`): boyut, hamle limiti, zorluk kademesi, katmanlar karakter ızgarası olarak (hücreler, taşlar, jöle, zincir), hedefler, spawn ayarları (palet, renk dengesi, hedef ve kaskad eğilimi, malzeme), eşit zorluk havuzunun tohumları. `formatVersion` dosya biçimini, `contentVersion` bölümün içeriğini sürümler (Tasarım §10, §20).

**Zorluk eğrisi.** Dört kademe: Kolay, Normal, Zor, Süper Zor. 10'luk testere dişi (her tablo bir döngü): Kolay, Normal, Normal, Normal, **Zor**, Kolay (nefes), Normal, Normal, **Zor**, **Süper Zor**; tablonun son parçası en zor bölümle gelir. İlk 20 bölüm Kolaydır ve tahtaları düz dikdörtgendir; döngü 21'de başlar. Zor bölümlerde tahta uzar ya da genişler (Tasarım §9).

**Botlar.** Botlar oyunun kodunu kullanır ama geleceği göremez. Kurallar elle yazılmıştır; bot oynadıkça öğrenmez (Tasarım §7).

| Bot | Ne için |
|---|---|
| `random` | Alt sınır; yalnızca geçerli hamle yapar |
| Beceri merdiveni `skill-30` … `skill-100` (5'er puan, 15 bot) | İnsan benzeri oyuncu modelleri: basamak, botun fark ettiği hamle yüzdesidir. Adlı profiller merdivenin basamaklarıdır: novice = 50, casual = 70, average = 85, expert = 100 |
| `usta` | Ölçüm botu: olası gelecekleri örnekleyerek iki hamle ileri bakar; gerçekçi beceri tavanını gösterir, limit belirlemez |
| Çözücü (beam search) | Gelecek taşları bilir; "her seferinde doğru hamle"nin alt sınırını tahmin eder |

**Kimin için ayarlanır.** İlk insan verisi bot adlarının insan becerisini göstermediğini ortaya koydu: deneyimli bir oyuncu novice botla aynı oranda kazandı. Sahibinin kararıyla (2026-10-06) "ortalama oyuncu" **55. basamaktır** (Tasarım §8, §13).

| Bölümler | Limiti belirleyen bot | İlk denemede hedef kazanma oranı | Hamle payı |
|---|---|---|---|
| 1–3 | `random` | %90 (Kolay) | %25 |
| 4–10 | novice | %95 (Kolay) | %25 |
| 11–20 | novice | %90 (Kolay) | %25 |
| 21–30 | novice | Kolay %90, Normal %80, Zor %65, Süper Zor %55 | %25 |
| 31–50 | novice | Kolay %80, Normal %60, Zor %40, Süper Zor %30 | %25 |
| 51–1000 | `skill-55` | Kolay %90, Normal %75, Zor %55, Süper Zor %40 | 0 |

Hamle payı, ölçülen limite eklenen yumuşatmadır (ilk odada limit %25 uzatılır, yukarı yuvarlanır). İlk odanın (1–50) dosyaları bu kurallarla üretildi; politikada sabitlenmişlerdir ve o günden beri her derlemede bayt bayt aynı kalır.

**Üretici** (`Puzzle.Generator`, `puzzle generate`; ayarlar `profiles/default.json`). Her bölüm numarası için bir plan (kademe, boyut, renk sayısı, açık mekanikler, hamle aralığı) yapılır; şekil stilleri ve engel aileleri portföyünden aday tahtalar çizilir; her hücrenin beslenebildiği denetlenir; bot sınırsız oynar ve hedef orana göre limit okunur; limit hamle aralığına oturmazsa hedef miktarları ya da engel miktarı ayarlanır; limit, üreticinin görmediği tohumlarla doğrulanır; eşit zorluk havuzu seçilir; rapor yazılır (Tasarım §9).

**Banka ve derleyici** (`Puzzle.Bank`; 2026-10-12'den beri 51–1000 buradan gelir). Pahalı ve rastgele iş ile ucuz ve bilinçli iş ayrıldı (Tasarım §24):

- **Banka** (`D:\puzzle-bank`, depo dışında): bölüm numarasına bağlı olmayan aday tahtalar, merdivenin 15 botuyla limitsiz oynatılıp oyunlarıyla saklanır. Yayına alınan derlemede 5.420 aday, 14,5 milyon oyun, 151 MB.
- **Politika** (`policies/default.json`): kimin için ve ne kadar zor: oyuncu basamağı, kademe başına pay, hamle payı, tür hedefleri (ör. yalnızca renk toplama %35, kutulu %25), sabitlenen bölümler (ilk oda), kayıtlı bölümlere davranış.
- **Derleyici** (`puzzle assemble`): her numaranın planına uyan tahtaları bankadan seçer, limiti ve havuzu saklı oyunlardan okur, seçilen bölümü yeni oyunlarla denetler, dosyaları ve raporu yazar. Hâlâ uyan bölüm yerinde kalır; politika değişikliği bölümleri baştan üretmez.
- **Sonuç (yayında, 2026-10-12):** 55. basamak bağımsız ölçümde bölümün kendi havuzunda Kolay %91,4, Normal %76,7, Zor %56,6, Süper Zor %40,7 kazanıyor (hedef 90 / 75 / 55 / 40). Ortalama limitler 23,9 / 27,5 / 31,0 / 35,2 hamle. 52 ve 53. bölümler aralığa oturan aday olmadığı için eski hâlleriyle ve "eskimiş" işaretiyle yayında.

**Yayına alma.** `puzzle promote` derlenen klasörü `levels/generated` yerine koyar: test sunucusu çalışırken reddeder, oyuncu kaydı olan değiştirilmiş bölümleri `levels/archive/` altına alır, her değişen dosyayı yedekler. `levels/generated` her bölümün bankadaki kaydını ve karar damgasını `assembly.csv` ve `assembly.manifest` içinde taşır. Unity'nin bölüm paketi bu klasörden `sync-engine.js` ile yapılır (Tasarım §24, §23).

**Yayından sonra.** Bölümler uygulamanın içindedir; bir bölümün düzeltmesi (limit, havuz ya da tahta) sunucudan inen bir **düzeltme paketiyle** gelir. Paket düzeltilen bölümleri bütün hâlinde ve yeni içerik sürümleriyle taşır; tek paket her uygulama sürümüne hizmet eder; sıra numarası eski bir kopyanın düzeltmeyi geri almasını önler (`puzzle patch make`, `puzzle patch apply`). Paketin istemcide indirilip uygulanması henüz yazılmadı (Tasarım §20).

**İnsan verisi.** Herkese açık web testinde iki kişinin denemeleri kaydedildi ve botlarla aynı tohumlarda karşılaştırıldı (`puzzle humans`). 2026-10-12'deki 62 karşılaştırılabilir denemede insanlar %69,4 kazandı; aynı oyunlarda novice bot %66,6, casual %81,3. Örneklem küçüktür; ortalama oyuncunun basamağı daha çok veriyle yeniden kestirilecek (Tasarım §8 madde 9, §20; DEVLOG Oturum 19).

## 4. Ekonomi

Fiyatlar ve ödüller elle tahmin edilmez; bir **ekonomi simülatörü** botların 1000 bölümdeki oyunlarını kullanarak can, "+5" ve altın akışını ölçer ve sayılar ondan okunur (Tasarım §19, §25). Oyun ve simülatör aynı ayar dosyasını okur: `economy/default.json`. Dosyadaki her sayının birimi ve kaynağı yazılıdır (karar, onaylanan varsayım, öneri, yer tutucu, kayıt).

### Kararlaştırılan sayılar

| Konu | Değer | Durumu |
|---|---|---|
| Can | 5 can, 30 dakikada bir can; yalnızca kayıpta gider | Karar (2026-10-13'te yeniden onaylandı) |
| Hesapların dayandığı alışkanlık | Günde 3 oturum, 10 dakikalık oturum | Karar (2026-10-13) |
| Hız hedefi | Ortalama oyuncu 100. bölüme 7–9. günde | Karar |
| Bitiş şöleni | Kalan hamle başına 4 altın | Öneri, yürürlükte |
| Tablo tamamlama | 80 altın ve sıradaki booster | Öneri ve varsayım |
| Günlük ödül | Sıradaki booster, altın yok | Varsayım |
| "+5 hamle" | 900 altın; günde 3 kez reklamla ücretsiz | Öneri, yürürlükte |
| Bölüm öncesi booster'lar | Renk bombası 500, roket ve bomba 200, "+3 hamle" 200 | Öneri, yürürlükte |
| Bölüm içi booster'lar | 400 / 700 / 700 / 300 | Yer tutucu (ölçülmedi) |
| Hoş geldin hediyesi | 200 altın ve her booster'dan 3 | Karar (2026-10-14) |
| Reklamla altın | 4 altın, günde 3 kez | 4: karar; günde 3: yer tutucu |
| Mağaza | Altın paketleri 500 / 1.200 / 2.500 (satışta değil); booster setleri 900 ve 6.300 altın | Yer tutucu |
| Yan koleksiyon parçası | Oyunla kazanılan altınla alınır; fiyat 300 | Karar (2026-10-14); fiyat yer tutucu |

### Simülatör

Üç parçadır (`Puzzle.Economy`, `puzzle economy`):

1. **Ölçüm** (`puzzle economy measure`): beceri karışımındaki botlar (45., 55. ve 65. basamak) her bölümü kendi havuzunda limitin ötesine kadar oynar; düz tahtada ve özel taşla başlayan iki booster'la. 862.512 oyun, yaklaşık 13 dakika. "+3" ve "+5" ayrıca oynanmaz, saklı oyunun üstünde hesaplanır.
2. **Ayar dosyası:** `economy/default.json` ve tek bir alanı değiştiren sekiz değişken (`sessions-6-minutes`, `sessions-20-minutes`, `sessions-60-minutes`, `one-life`, `life-every-16-hours`, `no-offers`, `extra-moves-1800`, `no-welcome-gift`).
3. **Simülasyon** (`puzzle economy run`, `compare`): 4.000 sanal ücretsiz oyuncuyu gün, oturum, can, altın ve booster üzerinden yürütür; oyun oynamaz, saklı oyunlara bakar. Raporlar `out/economy/<ad>` altına yazılır.

**Seçilen ayarlarla sonuç** (hoş geldin hediyesiyle):

| Oyuncu | 100. bölüm | 300. bölüm | 1000. bölüm |
|---|---|---|---|
| 45. basamak | 9. gün | 30. gün | 108. gün |
| 55. basamak (ortalama) | 8. gün | 25. gün | 90. gün |
| 65. basamak | 6. gün | 21. gün | 75. gün |

- Hızı belirleyen şey oturum süresidir, can ya da fiyat değil: 10 dakikalık oturumda 5 can hiçbir oturumu bitirmez. 20 dakikalık oturumda 100. bölüm 4. güne iner ve altın daralır; 6 dakikalıkta 12. güne çıkar ve altın birikir. İlk gerçek veride bakılacak ilk sayı oturum süresidir.
- Ortalama oyuncu günde yaklaşık 34 dakika oynar; 1000 bölüm yaklaşık 13 hafta sürer. Kesesi her zaman 4–7 "+5" değerindedir.
- Hiçbir bölüm duvar değildir: ilk simülasyonda (2026-10-13) bölüm başına ortalama deneme Kolay 1,05, Normal 1,15, Zor 1,43, Süper Zor 1,80 çıktı.
- Hoş geldin hediyesi hızı değiştirmez, keseye 200–400 altın ekler; en çok zayıf oyuncuya yarar.

### Denge politikası (2026-10-14)

Sayılar kurallara bağlıdır; biri değişip ona bağlı olan değişmezse testler kırmızıya döner (`EconomyBalancePolicyTests`):

1. Hoş geldin altını en ucuz bölüm öncesi booster'ı alabilir, "+5"i alamaz.
2. Hoş geldin booster'ları yedi türün hepsinden ve eşit sayıdadır.
3. Bir booster seti, içindekilerin tek tek fiyatlarının toplamına satılır; indirim icat edilmez.
4. Bir günün reklamla altını "+5"in onda birinden azdır.
5. Şölen "+5"i geri ödemez; "+3 hamle" booster'ı 3 hamlelik altından pahalıdır.

Simülatörle her ayar değişikliğinde bakılanlar: ortalama oyuncu 100. bölüme 7–9. günde gelir; hediye bu günü en çok bir gün değiştirir; ortalama oyuncunun kesesi 100., 300. ve 1000. bölümde 2–8 "+5" değerinde kalır.

**Sınırlar ve açık kararlar.** Bölüm içi booster'lar ölçülmedi (hiçbir bot ne zaman kullanacağını bilmiyor), bu yüzden fiyatları yer tutucudur. Gerçek para simüle edilmiyor. Açık kalanlar: altın sayılarının tek tek onayı, bölüm içi booster fiyatları, yan koleksiyonun büyüklüğü ve parça fiyatı (Tasarım §25).

## 5. Yazılım mimarisi

### İlkeler

- **Tek motor, her yerde aynı kod.** Oyunun kuralları motordan bağımsız bir C# kütüphanesidir. Botlar, üretici, banka, simülatör, web testi ve Unity istemcisi **aynı derlenmiş kodu** çalıştırır: botun ölçtüğü şey oyuncunun oynadığı şeydir (Tasarım §0).
- **Determinizm.** Kendi rastgele sayı üreticimiz (PCG32) ve yalnızca tam sayı aritmetiği: aynı tohum her cihazda aynı oyunu üretir. Başlangıç, spawn, efekt ve karıştırma ayrı rastgele akışlardan beslenir (Tasarım §6). .NET ile Unity'nin aynı satırları verdiği sabitlenmiş bir izle denetlenir.
- **Unity uyumu.** Unity'ye giren projeler `netstandard2.1` ve C# 9'dur ve paket bağımlılığı yoktur.
- **Kapalı türler, dışarıdan verilen saat.** Kuralların sonucu kapalı bir türdür (alındı, altın yetmiyor, satışta değil gibi). Kurallar saati bilmez; zaman parametre olarak gelir (Tasarım §23).
- **Kod standardı.** Kod `beautiful-code` standardına göre yazılır (karar 2026-10-02): yokluğun türde görünmesi, kapalı kümelere kapalı tür, birim taşıyan değere kendi türü, jenerik ya da boş catch yok.

### Projeler

On proje `src/` altında, test projesi `tests/` altındadır. "Unity" sütunu, projenin derlenmiş hâliyle Unity istemcisine girip girmediğini söyler.

| Proje | Hedef | Bağlı olduğu | Unity | İçerik |
|---|---|---|---|---|
| **Puzzle.Core** | netstandard2.1 | — | Evet | Simülasyon motoru: tahta, eşleşme, özel taşlar, spawn yönetmeni, hedefler, booster, "+5", şölen, deneme kaydı ve yeniden oynatma |
| **Puzzle.Bot** | netstandard2.1 | Core | Evet | Botlar (greedy profilleri, merdiven, usta), çözücü, paralel simülasyon, tohum havuzu |
| **Puzzle.Museum** | netstandard2.1 | Core | Evet | Müzenin kuralları: odalar, tablolar, yapboz kesimi, ilerleme, katalog |
| **Puzzle.Meta** | netstandard2.1 | Core | Evet | Can, altın, booster rafı, ödüller, "+5" teklifi, mağaza; ayar dosyasından |
| **Puzzle.Tooling** | net10.0 | Core, Bot | Hayır | Bölüm JSON'u, bölüm kütüphanesi, merdiven ölçümü, insan–bot karşılaştırması |
| **Puzzle.Generator** | net10.0 | Core, Bot, Tooling | Hayır | Bölüm üreticisi: plan, şekil ve engel portföyü, kalibrasyon, rapor |
| **Puzzle.Bank** | net10.0 | Core, Bot, Tooling, Generator | Hayır | Bölüm bankası, ölçüm, politika, derleyici, yayına alma |
| **Puzzle.Economy** | net10.0 | Core, Bot, Tooling, Museum, Bank | Hayır | Ekonomi simülatörü: ölçüm, ayarlar, simülasyon, raporlar |
| **Puzzle.Cli** | net10.0 | Core, Bot, Tooling, Generator, Bank, Economy | Hayır | `puzzle` komut satırı aracı (17 komut) |
| **Puzzle.Web** | net10.0 | Core, Bot, Tooling | Hayır | Web test uygulaması: laboratuvar ve herkese açık test sunucusu |
| **Puzzle.Core.Tests** | net10.0, xUnit | Cli dışında hepsi | — | Bütün C# testleri; istemcinin Unity'ye bağlı olmayan dosyalarını da derler |

`Puzzle.Meta` bilerek simülatöre bağlı değildir; simülatör kendi kurallarını tutar ve testler ikisinin aynı cevabı verdiğini denetler (`MetaAgainstTheSimulatorTests`).

### Unity'ye ne girer

`node tools/unity/sync-engine.js` dört şeyi Unity projesine koyar. Hepsi depodan türetildiği için hedef klasör `unity/Assets/Generated` depoya girmez.

- **Motorun derlemeleri:** `Puzzle.Core`, `Puzzle.Bot`, `Puzzle.Museum` ve `Puzzle.Meta`, Release derlemesinden `Generated/Engine` altına.
- **Bölüm paketi:** `levels/generated` klasöründeki 1000 bölüm tek dosyada (yaklaşık 0,7 MB), `Generated/Resources/Levels.json`.
- **Müze kataloğu:** `museum/catalogue.json`, `Generated/Resources/Museum.json` olarak.
- **Ekonomi paketi:** `economy/default.json` dosyasının oyuna ait bölümü, `Generated/Resources/Economy.json` olarak.

Ekonomi ayarı değişince önce `node tools/unity/economy-pack.js --pin` (testlerdeki sabit kopyayı yeniler), sonra `sync-engine.js` çalıştırılır. Tablo görüntüleri ve çizimler kalıcı olarak Unity projesinde, depodadır.

### Unity istemcisi

**Sürüm ve hedef.** Unity 6.6 (6000.6.4f1), URP 2B, Unity Personal. Android: IL2CPP, ARM64, dikey. Proje `unity/` altında. Komutlar Tasarım §23'te.

**Editor penceresi kullanılmaz.** Sahne, çizim hattı ayarları, atlaslar ve varlık kataloğu Editor betikleriyle komut satırından üretilir (`ProjectSetup.Run`). Sahne dosyası tek bir nesnedir (`GameRoot`); görünen her şey çalışırken koddan kurulur. `ProjectSetup` ikinci çalıştırmada hiçbir dosyayı değiştirmez.

**Yapı.** `GameRoot` bir `GameSession` kurar. Oturum oyuncunun ilerlemesini, cüzdanını ve ekonomi kurallarını tutar ve üç sahne arasında geçiş yapar; üçü de `IStage` sözleşmesini uygular:

- `MuseumStage`: salon, harita ve koleksiyon sayfaları, alt çubuk, cüzdan satırı, can kartı, bölüm başı kartı.
- `JigsawStage`: yapboz masası ve parça tepsisi.
- `GameStage`: tahta, plaketler, raf, sonuç kartları.

| Klasör (`unity/Assets/Scripts`) | İçerik |
|---|---|
| `Engine` | Unity'nin paket okuyucuları (bölüm, müze, ekonomi), motor izi |
| `Flow` | Oturum, ilerleme ve kayıt dosyası, denemenin hesabı (`AttemptLedger`), deneme kaydı, cihaz kimliği, duvar saati, ekran yazıları |
| `Stage` | `IStage`, `GameStage`, ara değerleme (`Tween`) |
| `Play` | Hamleyi animasyona çeviren `TurnChoreographer`, `Timeline`, düşme yasası, dokunma yorumu, şölenin temposu |
| `Boards`, `Gallery` | Tahta görünümü, yaldız çerçeve, duvar, aplikler, plaketler, raf, sonuç kartı |
| `Screens`, `Hall`, `Maps`, `Collections`, `Jigsaws`, `Canvases` | Ortak parçalar (asılı kart, disk düğme, booster diski, büyük düğme), salon, harita, koleksiyon, yapboz, tablolar |
| `Layout` | Düzen hesabı: her ekran için Unity'siz, testli yerleşim kodu |
| `Art`, `Painting` | Varlık kataloğu, çizim adları, yazı tipleri ve harf kümesi, koddan çizilen şekiller |

**Ekran motoru izler.** Hamle önce motorda oynanır; motorun olay günlüğü zamanlı bir senaryoya çevrilir (`TurnChoreographer`) ve oynatılır; senaryo bitince ekrandaki tahta motorun tahtasıyla karşılaştırılır, uyuşmazlıkta tahta motorun durumuna döndürülür. Botlarla 70'ten fazla bölüm baştan sona oynatılıp ekranın motorla aynı bittiği görüldü (DEVLOG Oturum 16).

**Açık saat.** Bütün animasyonlar `Advance(saniye)` ile ilerler; oyun buna kare süresini verir. Aynı saat sayesinde Editor, oynatma moduna girmeden herhangi bir anın görüntüsünü çıkarabilir (`RenderHarness`, `Storyboard`, `ScreenShots`).

**Saf kod testlenir.** Düzen hesabı, çerçeve hattı, zaman çizelgesi, hamle senaryosu, dokunma yorumu, ilerleme belgesi, denemenin hesabı, deneme satırı ve ekran yazıları Unity'ye bağlı değildir; test projesi bu dosyaları doğrudan derler.

**Yazı.** TextMesh Pro, tasarımın üç yazı tipiyle; yazı on bir stille yazılır (`LetteringStyle`: yazı tipi, zemine göre renk, çevre çizgisi). Harf kümesi Türkçenin bütün harflerini kapsar; küme ya da yazı tipi değişince `ProjectSetup` yazı varlığını yeniden üretir.

**Çizimler.** Katalog 125 çizimi iki atlasta tutar; kaynakları `art/unity` altındaki SVG'lerdir. Bir çizimin dört yeri (kaynak dosya, `GalleryArt` üyesi, katalog listesi, içe aktarma ölçüleri) birbirini tutmak zorundadır; bunu testler denetler. Taş renkleri tek bir kuraldan çıkar (`tools/web-theme/gem-palette.js`); web testi ve Unity aynı derlemeden beslenir. Tablolar ikinin kuvvetine gerilerek, mipmap'li ve ASTC 6×6 sıkıştırmalı saklanır; gerçek oranları `PaintingSizes` listesinden okunur.

### Web test uygulaması

`src/Puzzle.Web` iki modda çalışır (bkz. [PUBLIC_TEST.md](PUBLIC_TEST.md)):

- **Test modu (laboratuvar):** bölüm seçimi, tohum, ipucu, botlar, çözücü, 100–1000 oyunluk simülasyon, kayıtlar ve insan–bot karşılaştırması. `dotnet run --project src/Puzzle.Web` ile `http://localhost:5180`.
- **Herkese açık mod:** tanıdıklar giriş yapıp bölümleri sırayla, oyunun tasarımlı oyuncu sayfasında oynar; her deneme `recordings/attempts.jsonl` dosyasına yazılır. Tailscale Funnel ile yayınlanır; laboratuvar yalnızca yönetici anahtarıyla açılır. Sunucu 12 Ekim'den beri kapalıdır; sahibinin kararıyla arayüz işleri bitince yeniden açılacak.

### Veri dosyaları

**Depoda, elle ya da araçla yazılanlar**

- `levels/generated/level_NNNN.json` — yayındaki 1000 bölüm (JSON). Derleyici ve `promote` yazar; web sunucusu, Unity'nin bölüm paketi ve araçlar okur. Yanında `assembly.csv` ve `assembly.manifest` (her bölümün bankadaki kaydı ve karar damgası) ile `report.csv` (bölüm başına ölçüm satırı) durur.
- `profiles/default.json` — bölüm planı: döngü, bantlar, tahta boyutları, palet, mekanik takvimi, kalibrasyon botları. Üretici ve derleyici okur.
- `policies/default.json` — zorluk politikası: oyuncu basamağı, paylar, hamle payı, tür hedefleri, sabitlenen bölümler. Derleyici okur.
- `museum/catalogue.json` — 20 oda ve 100 tablo. Unity ve simülatör okur.
- `economy/default.json` ve değişkenleri — yorumlu JSON: can, altın, ödül, fiyat, mağaza, oyuncu alışkanlığı. Unity (paketlenmiş hâliyle) ve simülatör okur.
- `reports/ladder.csv`, `reports/expert_moves.csv` — merdivenin ve expert botun bölüm ölçümleri (`puzzle ladder`, `puzzle botstats`).

**Oyuncunun cihazında (ve web testinde) yazılanlar**

- `progress.json` — telefondaki kayıt dosyası, katman 3: ilerleme, yerleştirilen parçalar, cüzdan, can saati, ödül sayaçları.
- `attempts.jsonl` — her biten deneme için bir satır: bölüm, tohumlar, hamle limiti, sıralı eylemler (hamle, booster, "+5"), sonuç, hedeflerin son durumu, içerik sürümü, cihaz kimliği. Telefon ve web testi (`recordings/attempts.jsonl`) aynı biçimi yazar; `puzzle humans` okur, `AttemptReplay` denemeyi birebir yeniden oynatır.
- `device.json` — telefonda rastgele 128 bitlik cihaz kimliği.

Kayıt dosyası sürümlüdür: eski katmanlar okunur (eski oyuncu hoş geldin hediyesi ve dolu canla açılır), bozuk dosya kenara alınır, daha yeni bir katmanın dosyasının üzerine yazılmaz (Tasarım §23).

### Araçlar ve sık kullanılan komutlar

`puzzle` = `dotnet run --project src/Puzzle.Cli -c Release --` (ya da derlenmiş `src/Puzzle.Cli/bin/Release/net10.0/puzzle.exe`).

| İş | Komut |
|---|---|
| Bütün testler | `dotnet test` |
| Bölüm dosyalarını denetle | `puzzle validate levels` |
| Bir bölümü botlarla simüle et | `puzzle sim <bölüm> --bot all --runs 300` |
| Bölümleri merdivenle ölç | `puzzle ladder levels/generated` |
| İnsan kayıtlarını botlarla karşılaştır | `puzzle humans` |
| Banka: durum, büyütme, yeniden ölçme | `puzzle bank status`, `bank grow`, `bank remeasure` |
| Derle, karşılaştır, yayına al | `puzzle assemble`, `puzzle compare`, `puzzle promote` |
| Düzeltme paketi | `puzzle patch make`, `puzzle patch apply` |
| Ekonomi | `puzzle economy measure`, `run`, `compare` |
| Test hesapları | `puzzle players list`, `add`, `password`, `delete` |
| Motoru ve verileri Unity'ye al | `node tools/unity/sync-engine.js` |
| Unity projesini kur, denetle, APK derle | `Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.<Araç>.Run` |
| Herkese açık test sunucusu | `.\tools\public\Start-PublicServer.ps1`, `Stop-PublicServer.ps1` |
| Belgelerin PDF'leri | `py tools/docs-pdf/build.py` |
| Kod grafiği | `graphify query "<soru>"` (`graphify-out`, depoya girmez) |

Öteki komutlar: `generate`, `botstats`, `seeds`, `solve`, `demo`, `bench`. Örnekler (eskiden README'deydi):

```
# profil profiles/default.json; var olan dosyalar korunur
# (--overwrite yeniden üretir, --quick hızlı önizleme)
puzzle generate --from 1 --to 1000 --out levels/generated
puzzle sim levels/samples/s03_jelly_shape.json --bot all --runs 300
# bölüm başına bitiş hamlesi, CSV (--bot usta yaklaşık 15 dakika)
puzzle botstats levels/generated --bot expert --runs 30
# parça parça çalışır; öteki bölümlerin satırları korunur
puzzle ladder levels/generated --from 1 --to 100
puzzle solve levels/samples/s03_jelly_shape.json --seeds 6 --beam 64 --prove 4
# eşit zorluk havuzunu bölüm dosyasına yazar
puzzle seeds levels/samples/s03_jelly_shape.json --write
puzzle demo levels/samples/s04_items_chains.json --seed 3
```

Tam liste ve seçenekler: Tasarım §9, §20, §23, §24, §25.

## 6. Kalite ve test

- **C# testleri:** yaklaşık 3.350 test (2026-10-14'te 3.349), xUnit, `dotnet test`. Motor, botlar, çözücü, dosya biçimleri, üretici, banka, müze, kurallar, simülatör, web sunucusu ve istemcinin Unity'siz dosyaları.
- **Sürekli entegrasyon:** GitHub Actions (`.github/workflows/ci.yml`) her PR'da ve `main`'e her gönderimde Linux'ta derler, testleri koşar ve bölüm dosyalarını denetler (`puzzle validate levels`).
- **Betik testleri:** `node --test` ile ekonomi paketi (`tools/unity/economy-pack.test.mjs`), taş paleti ve çerçeve çizgisi (`tools/web-theme/*.test.mjs`).
- **Çalışma ortamları arası eşitlik:** dört örnek bölüm üçer tohumla sonuna kadar oynatılır; .NET (`EngineTraceTests`) ve Unity (`EngineSmokeTest`) aynı satırları vermek zorundadır (`tests/fixtures/engine-trace.txt`).
- **Unity denetimleri** (komut satırından): `ProjectSetup`, `EngineSmokeTest`, `EconomyCheck`, `FinaleCheck`, `PlayModeCheck`, `TouchWhileMovingCheck`, `ProgressFileCheck`, `AttemptLogCheck`, `PaintingFormatCheck`, `MuseumReport` ve ekran yürüyüşü (`GameWalk`, `ScreenShots -screenWalk`). Görüntü araçları: `RenderHarness`, `Storyboard`, `FinaleStory`, `DesignPairs` (ekran tasarım çizimiyle yan yana), yazı ve çizim föyleri.
- **Ekonomi koruması:** denge politikası testleri (`EconomyBalancePolicyTests`), ayar dosyasının okunması (`EconomyFileTests`), oyunun kuralları ile simülatörün aynı cevabı vermesi (`MetaAgainstTheSimulatorTests`), Unity'nin paketi yazıldığı gibi okuması (`EconomyCheck`) ve simülatörün saklı ölçümü bu derlemeyle yeniden oynayıp kabul etmesi.
- **Bölümlerin bağımsız ölçümü:** her derlemeden sonra `puzzle ladder` bölümleri üreticinin görmediği oyunlarla ölçer; rapor ile bağımsız ölçüm karşılaştırılır.
- **Telefon test planı:** yalnızca gerçek bir telefonda kanıtlanabilecek davranışlar ve bilinen açıklar [PHONE_TESTS.md](PHONE_TESTS.md) belgesindedir: tam test turunun sırası, bilinen açıklar (A01–A34) ve telefon testleri (T01–T241). Testler proje sahibinin Pixel 9 Pro Fold'unda, zamanı gelince hepsi birden koşulacak.
- **İş yöntemi:** büyük işler bağımsız eleştiriden geçer (tasarım önerileri, uygulama, ayrı eleştiriciler, düzeltme); sonuçlar görüntülere bakılarak doğrulanır. Belgeler her değişiklikten sonra PDF olarak yeniden basılır.

## 7. Durum ve yol haritası

### Yapılanlar

- **2026-10-01** — Motor, botlar, CLI; iki bomba, 8 renk, spawn yönetmeni, çözücü, web laboratuvarı; eşit zorlukta tekrar; CI; üretici ve ilk 1000 bölüm.
- **2026-10-02** — Tema (ressam ve müze), ürün ve yayın kararları, motorun "hazır" tanımı.
- **2026-10-03** — Özel taş düzeltmesi, usta bot, casual koruması, deneme kaydı.
- **2026-10-04** — Herkese açık web testi; 1000 bölüm yeni kurallarla; ilk insan verisi.
- **2026-10-05** — İlk oda zayıf oyuncuya göre; %25 hamle payı; sanat yönü; doğrudan dünya yayını kararı.
- **2026-10-06** — Beceri merdiveni; ortalama oyuncu 55. basamak, paylar 90 / 75 / 55 / 40; tahta başına engel miktarı.
- **2026-10-07** — Unity kuruldu; tahta telefonda oynandı.
- **2026-10-08** — Canlı renkler, daha büyük tahta; müze (salon, harita, koleksiyon, yapboz); ikinci oda; bölüm bankası.
- **2026-10-12** — Bankadan derlenen bölümler yayında; booster'lar, "+5", şölen, içerik sürümü ve düzeltme paketi motorda: **motor hazır**.
- **2026-10-13** — Unity'de bitiş şöleni; ekonomi simülatörü ve ekonomi kararı; 15 aşamalı arayüz planı; yazı tipleri, çizimler, ortak parçalar.
- **2026-10-14** — Kurallar, cüzdan ve sıralı deneme kaydı istemcide; altın satırı ve canlar; bölüm başı kartı; plaketler, raf, kazanma kartı; 3–20. odaların 90 tablosu (**müze tamam**); hoş geldin hediyesi ve denge politikası; telefon test planı.

### Arayüz planı (15 aşama)

Tasarım tuvalindeki bütün ekranların istemciye gelmesi için yazılan plan (`out/unity/ui-plan.json`, depoya girmez). Önce kurallar ve ortak malzeme, sonra ekranlar oyuncunun karşılaştığı sırayla.

| Aşama | İş | Durum |
|---|---|---|
| 1 | Kurallar ve ekonomi ayarı istemcide | ✅ 2026-10-14 |
| 2 | Cüzdan kayıt dosyasında, duvar saati, denemenin hesabı | ✅ 2026-10-14 |
| 3 | Deneme kaydı: sıralı eylemler, yeniden oynatma, telefonda dosya, cihaz kimliği | ✅ 2026-10-14 |
| 4 | Tasarımın üç yazı tipi | ✅ 2026-10-13 |
| 5 | Eksik simgeler ve yüzeyler | ✅ 2026-10-13 |
| 6 | Ortak parçalar, asılı kart, görüntü araçları | ✅ 2026-10-13 |
| 7 | Salon, harita, koleksiyon: altın satırı ve canlar | ✅ 2026-10-14 |
| 8 | Bölüm başı kartı ve bölüm öncesi booster'lar | ✅ 2026-10-14 |
| 9 | Tahta plaketleri, duraklatmalı raf, kazanma kartı | ✅ 2026-10-14 |
| 10 | Bölüm içi booster'lar: seçme, nişan alma, kullanma, reddetme | ✅ 2026-10-15 |
| 11 | Hamle bitince: "+5" teklifi, canlandırılmış reklam, vazgeçme | ✅ 2026-10-15 |
| 12 | Mağaza, artı düğmeleri, hediye ve tablo ödülü | ✅ 2026-10-15 |
| 13 | Ayarlar, gizlilik ve veri, destek | ✅ 2026-10-15 |
| 14 | Ses ve titreşim (yer tutucu sesler) | ✅ 2026-10-16 |
| 15 | Koleksiyon ve harita ayrıntıları, baştan sona yürüyüş, her ekranın çizimle karşılaştırması, belgeler | Sonraya bırakıldı (önce APK) |

### Sıradaki işler

Proje sahibinin 2026-10-15'te belirlediği sıra (akşam güncellendi):

1. Bölüm içi booster araştırması ve bu genel belge (✅ 2026-10-15; `docs/research/in-level-boosters.md`, PDF'i "Araştırma Notu").
2. **10. aşama:** bölüm içi booster'lar, araştırmanın önerilerine dayanarak (✅ 2026-10-15).
3. **11–14. aşamalar** (✅ 2026-10-16). 15. aşama proje sahibinin kararıyla APK'dan sonraya bırakıldı.
4. **APK telefona:** proje sahibi oyunu kendi telefonunda elde dener; ardından [PHONE_TESTS.md](PHONE_TESTS.md) baştan sona koşulur. 16 Ekim'de 14. aşamaya kadarki her şeyle APK derlendi (283 MB, internet izni yok) ve proje sahibinin telefonuna kuruldu; indirme sayfası Tailscale Funnel ile herkese açık.
5. **Arayüz iyileştirmeleri:** prototip telefonda denendikten sonraki ilk iş. GitHub'da `arayüz-iyileştirme` etiketli kayıtlar olarak tutuluyor; ilki #2 (renkler genelde mat ve düşük kontrastlı).
6. **Bot deneyi:** oyunun bütün kurallarıyla oynayan, genel oyuncu kitlesini temsil eden bir bot nüfusu. Botların eğilimleri ölçülecek; sahte reklam yalnızca botun kararlarını ölçmek için var. Ayrıntıları proje sahibi verecek.
7. **Avatar:** hikâyenin genç ressamı oyunun yüzü olacak. Çizimini proje sahibi kendisi yapıp verecek; oyunun istediği çizimler tasarım belgesinin 22. bölümünde. Ertelendi: 3D animasyon gerekecek.

Planlanan öteki adımlar:

- **Herkese açık test sunucusu:** arayüz işleri bitince yeniden açılacak (karar 2026-10-14).
- **Yayın:** APK testi, ardından doğrudan dünya geneli (Tasarım §20). Yayın öncesi yapılacaklar arasında: bölüm klasörünün "yayın klasörü" olarak dondurulması, düzeltme paketinin istemcide indirilip uygulanması, analiz servisi, rıza ekranı ve gizlilik politikası, iOS derlemesi, oyunun adı.

### Açık kararlar

- Oyunun adı (en sona bırakıldı) ve kalıcı paket kimliği.
- Analiz servisi: Firebase mi, Unity Analytics mi (Tasarım §20).
- Bölüm içi booster fiyatları; altın sayılarının tek tek onayı; yan koleksiyonun büyüklüğü ve parça fiyatı (Tasarım §25).
- Malzeme indirme bölümlerinin hamle aralığı: şimdilik değişmiyor (karar 2026-10-12); bu bölümler hedefin altında kalıyor (%3,6, hedef %6).
- 10 sütunlu tahtada hücre yaklaşık 36 pt, önerilen 44 pt dokunma hedefinin altında; telefonda ölçülecek (Tasarım §22; PHONE_TESTS T10).
- Daha çok insan verisiyle ortalama oyuncunun basamağı ve ilk odanın geçici hedefleri (31–50).

### Bilinen açıklar

Bilinen ama kapatılmamış ya da bilerek kabul edilmiş eksiklerin tam listesi [PHONE_TESTS.md](PHONE_TESTS.md) §2'dedir. Başlıcaları: kurallar ve kayıt dosyası telefonda ve IL2CPP derlemesinde hiç çalıştırılmadı (A03); bitiş şöleninin telefonda kare hızı ölçülmedi (A04); telefonun saati ileri alınırsa canlar dolar, günün reklam hakları ve hediyesi yenilenir; sunucusuz bir oyunda kapanmaz (A01); bölüm içi booster'ların etkisi ölçülmedi (A09); 100 tablonun telefonda bellek ve yükleme süresi ölçülmedi (A14); APK 283 MB, Play Store'un 200 MB sınırının üstünde (A32).

## 8. Belgeler ve nerede ne var

### Belgeler

Belgeler Markdown olarak yazılır ve gözden geçirilir; PDF baskıları `docs/pdf/` altındadır ve `py tools/docs-pdf/build.py` ile üretilir (pandoc, Chrome ya da Edge, pypdf gerekir).

| Belge | PDF etiketi | Ne için |
|---|---|---|
| `docs/OVERVIEW.md` | Genel Belge | Bu belge: ilk okunacak özet |
| `docs/GAME_DESIGN.md` | Tasarım Belgesi | Her kararın ayrıntısı ve gerekçesi, ölçümler, komutlar (§0–§25) |
| `docs/DEVLOG.md` | Proje Günlüğü | Oturum oturum ne yapıldı, ne bulundu, kim neye karar verdi (en yenisi en üstte) |
| `docs/PUBLIC_TEST.md` | Kullanım Kılavuzu | Web test sunucusunu çalıştırma, internete açma, hesaplar, veriler |
| `docs/PHONE_TESTS.md` | Test Planı | Tam test turu, bilinen açıklar, telefon testleri |
| `docs/research/spawn-control-and-solvability.md` | Araştırma Raporu | Spawn kontrolü, çözülebilirlik, arama algoritmaları, DDA etiği |
| `research.md` | Araştırma Özeti | Match-3 mimarisi ve algoritmaları üzerine ilk araştırma |
| `README.md` | — | Deponun giriş sayfası |

### Depo

| Yer | İçerik | Depoda mı |
|---|---|---|
| `src/` | On proje (bölüm 5) | Evet |
| `tests/` | Test projesi ve sabitlenmiş dosyalar (`tests/fixtures`) | Evet |
| `unity/` | Unity projesi | Evet; `Library`, `Build` ve `Assets/Generated` hariç |
| `levels/generated`, `levels/samples`, `levels/archive` | Yayındaki 1000 bölüm; 4 el yapımı örnek; değiştirilen bölümlerin eski sürümleri | Evet |
| `profiles/`, `policies/`, `economy/`, `museum/` | Bölüm planı, zorluk politikası, ekonomi ayarları, müze kataloğu | Evet |
| `art/` | Tabloların lisans listeleri, yazı tipi lisansları, istemci çizimlerinin SVG kaynakları | Evet |
| `reports/` | Merdiven ve expert ölçümleri (`humans.csv` hariç) | Evet |
| `tools/` | `docs-pdf`, `public` (sunucu betikleri), `unity` (aktarma, indirme, paketleme), `web-theme` (taş çizimleri, palet) | Evet |
| `docs/` | Belgeler ve PDF'leri | Evet |
| `recordings/`, `data/` | Web testinin deneme kayıtları; hesaplar, ilerleme, anahtarlar | Hayır |
| `out/` | Geçici çıktılar: raporlar, görüntüler, tasarım tuvalinin kaynakları (`out/design`), arayüz planı | Hayır |
| `graphify-out/` | Kod grafiği | Hayır |
| `D:\puzzle-bank` | Bölüm bankası | Depo dışında |

**Bir yıl sonra dönen biri için.** Önce bu belge, sonra DEVLOG'un en üstteki iki oturumu, sonra ilgili Tasarım bölümü. Kodda bir yeri bulmak için `graphify query` ile kod grafiği (yoksa `graphify update .` ile yeniden kurulur). Bölümlere dokunmadan önce Tasarım §24, ekonomiye dokunmadan önce §25, telefona bir şey kurmadan önce PHONE_TESTS okunmalı.
