# Mini Oyunlar: Hangi Oyunları Ekleyebiliriz?

**Tarih:** 2026-10-24. **Durum:** öneri. Hiçbir şey yapılmadı; kod, kayıt dosyası ve ayar dosyası değişmedi.

**Ne istendi.** Proje sahibi tekdüzeliğe karşı çok sayıda küçük, basit mini oyun istiyor: başka oyunların kullandığı türden, "eşya sıralama gibi". Bunlar her ayın belli haftalarında etkinlik olarak açılacak. Bir haftada birden çok oyun olabilir. Ödül altın, can ve başka şeyler. Oyun ve ödül çeşidi kolayca büyüyebilmeli. İlk adım, **hangi tür oyunları ekleyebileceğimizi görmek.** Bu belge o adımdır.

**Dayandığı çalışma.** Bu araştırmanın dört notu: `types.md` (türler ve pazar), `calendar.md` (takvim, ödül, telefonun saati), `build.md` (kodda nasıl yapılır), `verification.md` (doğrulama). Dördü de şimdilik `docs/research/mini-games-2026-10/` klasöründe. Fikir çizimleri aynı klasörde: `sketches.jpg` (onu üreten betik `sketches.py`). Önceki rapor `docs/research/monetization-and-retention.md` §5 ve §7'deki fikirler (Restoratör Masası, Gala gecesi, Arşiv Kazısı, Katalog Eşleştirme, Küratör Sınavı) burada yeniden anlatılmaz; yalnız yerleri gösterilir.

**Etiketler**
- **FACT**: kaynağın kendisinde gördük: resmî mağaza sayfası, oyunun resmî yardım sayfası, platform belgesi ya da bu depo.
- **REPORTED**: ikincil kaynak: analist, hayran wikisi, rehber, video. Sayıları çoğunlukla Sensor Tower ya da AppMagic tahminidir.
- **ESTIMATE**: bizim çıkarımımız ya da önerimiz. Oturum süreleri, efor, uyum puanları ve bütün öneriler ESTIMATE'tir.

[T5] gibi numaralar §8'deki kaynaklardır. Bütün web kaynakları 2026-10-24'te açıldı. Mağaza bilgileri (satıcı, çıkış tarihi, puanlama sayısı) Apple'ın ABD mağazası arama servisinden okundu. **Puanlama sayısı indirme sayısı değildir;** yalnız kitlenin kabaca büyüklüğünü gösterir.

**Efor ölçeği** (`build.md` ile aynı; ESTIMATE): **S** birkaç saat, **M** yaklaşık bir arayüz aşaması (bir gün), **L** 2–3 aşama, **XL** bir hafta ve üstü. Dikkat: `monetization-and-retention.md`'deki ölçek farklıdır (orada S 2–4 iş günü). İki belgenin efor harfleri birbiriyle karşılaştırılmamalı.

**Doğrulamadan gelen düzeltmeler uygulandı.** `verification.md` 34 web iddiasını yeniden açtı. 22'si olduğu gibi doğru çıktı, 10'unda bir ayrıntı düzeltildi, 2'si kaynağın kendi içinde çelişkili. Uydurma bir sayı bulunmadı. Bu belgede düzeltilmiş hâller kullanıldı. Tasarımı etkileyen üç düzeltme var: Royal Match'in hafta içi mini oyun zinciri kesin değil (C1); etkinlik sonundaki "son şans" her oyunda yok (C2); Google Play'de öne çıkarma herkese açık değil (C4).

---

## 1. Özet

- **Pazar sıralama, blok ve vida türleriyle büyüyor.** 2025'te bulmaca oyunlarındaki gelir artışının %13'ü sıralama (çoğu Magic Sort!), %13'ü blok (Color Block Jam), %8'i vida (Screwdom) türünden geldi [T1] (REPORTED, Sensor Tower verisi). Bu türler dil istemez; bir bölümü 1–3 dakika sürer (ESTIMATE).
- **Büyük match-3'ler bu hitleri olduğu gibi içine koymuyor.** Gömdükleri: kazı, renk sırası bulmacası (Royal Match'in Magic Cauldron'u), yapboz (Puzzle Break), hafıza, top düşürme ve fizik [T2, T3, C8] (REPORTED/FACT). Tüp sıralama, vida ve otopark türünü bir match-3'ün içinde bulamadık. Bulamamak yokluğun kanıtı değil; bu bir boşluk, fırsat da olabilir.
- **Bize uyan 15 yeni tür var (§2).** Üçü ilk dalga için en iyisi (uyum 5): **Boya Tüpleri** (tüp sıralama), **Pigment Tarifi** (renk sırası), **Takas Yapbozu** (kart takası). Beşi iyi (uyum 4), yedisi sonra denenebilir (uyum 3). Raporda zaten olan yedi fikir yerinde kalıyor. On bir tür ya da biçim uymuyor: rastgele ödüllüler, süreliler, sunucu isteyenler, fizik ve kelime oyunları, insan çizimi isteyenler.
- **Ortak çerçeve bir kez yapılır (L).** Takvim, salondaki etkinlik çerçevesi, Etkinlikler sayfası, kural kartı, aşama sahnesi, sonuç kartı, ödül defteri ve kayıt. Sonra her yeni tür yalnız kendi kuralını, görünümünü ve birkaç sözünü getirir; çoğu tür M–L (§3).
- **Takvim (§4):** ayın 1. ve 3. haftası **Atölye Haftası**. Hafta içinde (Pazartesi–Perşembe) ve hafta sonunda (Cuma–Pazar) birer **zincir** açılır; her zincirde iki mini oyun art arda gelir. Haftada dört mini oyun, ayda sekiz yuva. 2. ve 4. hafta sessiz. Takvim uygulamanın içinde bir kuraldır. Telefonun yerel saatiyle çalışır; internet istemez. Saati ileri almak yalnız geleceği "ödünç alır", fazladan ödül vermez.
- **Giriş:** kazanılan her bölüm bir **atölye jetonu** verir; zor bölüm daha çok verir. Jeton aşama açar. Mini oyun can harcatmaz, süre sınırı yoktur, her zaman atlanabilir. O gün herkes aynı bölümü oynar.
- **Ödüller (§5):** altın, booster, **süreli ücretsiz booster**, kozmetik, yan koleksiyon parçası ve Restoratör XP'si. Miktarları simülatör belirler. **Can konusunda açık olalım:** 10 dakikalık oturumda 5 can hiçbir oturumu bitirmiyor (FACT, `GAME_DESIGN.md` §25). Bu yüzden can ödülü bugün az değerli. Değerli olduğu yer uzun oturum: 60 dakikalık oturumların %63'ü canla bitiyor (FACT, §25). Öneri: can yalnız "sınırsız can süresi" olarak, az ve yerinde verilsin.
- **İlk seçim (§6):** ortak çerçeve, sonra Boya Tüpleri, Pigment Tarifi, Takas Yapbozu, Depo Sıkışıklığı; isteğe bağlı beşinci Çerçeve Vidaları. Çerçeve ve ilk dört tür için yaklaşık 6–10 arayüz aşaması; çeviri, simülatör ve telefon testleri hariç (ESTIMATE).
- **Karar bekleyen 14 konu** §7'de.

---

## 2. Katalog

### 2.1 Bütün mini oyunların uyduğu kurallar (ESTIMATE)

- Can harcatmaz. Süre sınırı yok. Her zaman atlanabilir.
- Bölümler önceden üretilip ölçülür. O gün herkes aynı bölümü oynar. Gizli zorluk ayarı yok (§17).
- Ödüller baştan yazılıdır; rastgele ödül yoktur. Hiçbir şey parayla şans satmaz (§17, §19).
- Dil istemez ya da çok az söz ister: her söz 13 dilde birlikte gelmeli (§3.6).
- Kendi taşlarımız ve tablolarımız kullanılır. Yeni tablo eklenmez: APK 16 Ekim'de 283 MB'tı, çoğu 100 tablodan (FACT, `docs/DEVLOG.md`).
- İnsan ya da maskot çizimi yok (proje sahibinin kuralı).
- Gelecek tablolar haritada örtülü kalıyor (§19, 2026-10-22 kararı). Bu yüzden tablo kullanan türler yalnız oyuncunun tamamladığı tabloları gösterir.

**Uyum puanı:** 5 = ilk dalga; 4 = iyi, ikinci dalga; 3 = olur, sonra; 2 = zayıf; 1 = uymuyor.

### 2.2 Bir bakışta

| # | Bizdeki adı | Tür | Bilinen örnek | Bir aşama (ESTIMATE) | Efor | Uyum |
|---|---|---|---|---|---|---|
| 1 | **Boya Tüpleri** | Tüp sıralama | Magic Sort! | 1–3 dk | L | **5** |
| 2 | **Pigment Tarifi** | Renk sırası (Mastermind benzeri) | Royal Match Magic Cauldron | 1–2 dk | S–M | **5** |
| 3 | **Takas Yapbozu** | Kart takası | Jigsolitaire | 1–4 dk | S–M | **5** |
| 4 | Depo Sıkışıklığı | Kayan sandık (Rush Hour kuralı) | Unblock Me, Parking Jam 3D | 1–3 dk | M–L | 4 |
| 5 | Çerçeve Vidaları | Vida sıralama | Screwdom | 1–3 dk | L | 4 |
| 6 | Depo Rafları | Raf sıralama ("goods sort") | Goods Puzzle, Hexa Sort | 1–3 dk | L | 4 |
| 7 | Katalog Taşları | Üçlü taş eşleştirme | Triple Tile, Vita Mahjong | 2–4 dk | L | 4 |
| 8 | Yaldız Çizgisi | Çizgi bağlama | Flow Free | 0,5–2 dk | M | 4 |
| 9 | Kapıları Aç | Oku çıkar ("tap away") | Arrows – Puzzle Escape | 1–2 dk | M | 3 |
| 10 | Restorasyon Izgarası | Resim mantığı (nonogram) | Nonogram.com | 1–8 dk | M–L | 3 |
| 11 | Kayan Tablo | 15 bulmacası | klasik | 1–4 dk | M | 3 |
| 12 | Döndür | Döndürmeli kareler | — | 0,5–2 dk | S–M | 3 |
| 13 | Sızıntıyı Yönlendir | Boru çevirme | Fishdom ara oyunları | 1–2 dk | M–L | 3 |
| 14 | Temizlik Makinesi | Bantlı renk atıcı | Pixel Flow! | 1–3 dk | M–L | 3 |
| 15 | Duvara As | Blok yerleştirme | Block Blast! | 1–3 dk | S–M | 3 |

### 2.3 Tür tür

Bir aşamanın süresi §2.2 tablosunda (ESTIMATE). "Ortak merdiven" §5.4'teki ödül merdivenidir.

#### 1. Boya Tüpleri (tüp sıralama) — uyum 5

- **Nasıl oynanır:** Tüplerde üst üste renkli taşlar var. Üstteki taşı aynı renkli taşın üstüne ya da boş tüpe taşırsın; her tüp tek renk olunca aşama biter.
- **Bilinen oyunlar:** Magic Sort! (Grand Games; 2024-05-28; 699.605 puanlama) [T25] (FACT). Water Sort Puzzle (IEC Global, 2020) ve Ball Sort Puzzle (HM Games, 2020) [T25] (FACT). Sıralama türü 2025 bulmaca büyümesinin %13'ü, çoğu Magic Sort [T1] (REPORTED). Magic Sort, Sensor Tower'ın ABD 2025 son çeyrek yazısındaki beş bulmacadan biri, haftada 381 bin indirme [T5] (REPORTED; yazıyı bir yapay zekâ aracı yazmış, oyunları sıralamıyor). Bir match-3'ün içinde bulunamadı.
- **Neden eğlenceli:** Sakin. Şans yok. Karmaşadan düzen çıkar; geri alma çıkmazı yumuşatır (ESTIMATE).
- **Efor:** L. Kurallar S, üretici ve çözücü M, görünüm M–L [build.md].
- **Çizim:** Taşlarımız hazır. Sekiz rengin her birinin ayrı şekli var (uğur böceği, damla, kare, papatya, üçgen, ay, lale, elmas); renk körü için de okunur (FACT, build.md). Yeni: bir cam tüp ya da kavanoz ve mantar tıpa.
- **Çözülebilirlik:** Bölüm, çözülmüş durumdan geriye yürüyerek üretilir. Yalnız ileri hâli yasal olan "geri hamleler" seçilir; böylece her bölüm kesin çözülür. Sonra en kısa çözüm ve çıkmaz oranı ölçülür; gürültülü bot merdiveni zorluğu verir [build.md] (ESTIMATE). Genel problem NP-tam [W1] (FACT), ama telefon boyunda arama küçük: örnek bir çözücü zor bir bölümde yaklaşık 20 bin durum tarıyor [W2] (FACT).
- **Ödül fikri:** ortak merdiven (§5.4); son ödül altın ve booster.
- **Neden 5:** Proje sahibinin kendi örneği (eşya sıralama). Pazarın en hızlı büyüyen türlerinden. Dil istemez, taşlarımızı kullanır.

#### 2. Pigment Tarifi (renk sırası) — uyum 5

- **Nasıl oynanır:** Ustanın gizli dört pigmentlik sırasını tahmin edersin. Her tahminden sonra işaretler kaç pigmentin doğru yerde, kaçının yanlış yerde olduğunu gösterir.
- **Bilinen oyunlar:** Royal Match **Magic Cauldron**: Eylül 2023'ten beri; renkli eşyaları doğru sıraya dizme; her deneme bölümlerden kazanılan iksirle yapılır [C1, T13] (REPORTED). Resmî yardım sayfasına göre 39. bölümde açılır, zor bölüm daha çok iksir verir, etkinlikten çıkan iksirini kaybeder [C8] (FACT). Monopoly GO **Juggle Jam**: Kasım 2024 [T2]; 3–7 top, tahminde her top için bir jeton, doğru cevap ödül rafı için bilet verir [T15] (REPORTED). Gossip Harbor **Clay Color Combo** [T2] (REPORTED).
- **Neden eğlenceli:** Kısa bir mantık bulmacası. Her tahmin bilgi verir; "buldum" anı nettir (ESTIMATE).
- **Efor:** S–M [build.md].
- **Çizim:** Taşlar ve işaret noktaları hazır. Yeni: bir karıştırma kabı ya da palet.
- **Çözülebilirlik:** Her gizli sıra çözülür. Sıra, tarihten türetilen tohumla seçilir; herkese aynıdır. Bir çözücü ipucu verir ve kaç tahminde çözüldüğünü ölçer. **Deneme sınırı önermiyoruz:** kayıp yok, piyango hissi yok. Kaç tahminde bulunduğu yalnız gösterilir (ESTIMATE).
- **Ödül fikri:** son ödül gökkuşağı paleti (renk bombası) booster'ı; temaya uyar.
- **Neden 5:** Liderlerin gerçekten oyunlarına koyduğu tür. En ucuzu. Rastgelelik yok; §17'ye dokunmaz.

#### 3. Takas Yapbozu (kart takası) — uyum 5

- **Nasıl oynanır:** Bir tablo kartlara bölünüp karışır. İki karta dokununca yer değiştirirler; doğru yere oturan kart altın kenarla kilitlenir.
- **Bilinen oyunlar:** Jigsolitaire (GAMINCAT; 2025-03-07; 75.224 puanlama) [T25] (FACT); Sensor Tower'ın ABD 2025 son çeyrek yazısında Ekim sonunda haftada 1 milyonun üstünde indirme [T5] (REPORTED). Royal Match **Puzzle Break** bir yapboz etkinliği: parçalar bölüm kazanarak gelir, 39. bölümde açılır [C8] (FACT); 3 gün, 9'dan 25 parçaya büyüyen 5 tablo, yaklaşık 53 bölüm [T8] (REPORTED). Puzzle Break'te yanlış yerleştirme bir puan yakıyordu; bu kural sonradan değişmiş olabilir [T8, doğrulama T9].
- **Neden eğlenceli:** Tablo gözünün önünde tamamlanır. Bizim çekirdek döngümüze en yakın tür. Takas, match-3'ümüzdeki "değiştirme" hareketinin aynısı (ESTIMATE).
- **Efor:** S–M (ESTIMATE). Kural çok basit. Oyun tabloyu çalışırken zaten kesiyor (`JigsawCanvas`, FACT).
- **Çizim:** Yok. Tablolarımız ve çerçeve yeter.
- **Çözülebilirlik:** Her karışım çözülür. Gereken en az takas sayısı, kart sayısı eksi karışımdaki döngü sayısıdır (ESTIMATE, permütasyon hesabı). Zorluk kart sayısıyla (3×3'ten 5×4'e) ve başta yerinde duran kart sayısıyla ayarlanır. Yanlış takas cezasız.
- **Dikkat:** Yalnız oyuncunun tamamladığı tablolar kullanılır. Karışım ve kart sayısı herkese aynı, ama tablo oyuncudan oyuncuya değişebilir; bu küçük zorluk farkı ölçülmeli (ESTIMATE). Aşama kapanınca resim bellekten bırakılır (A42 bellek düzeltmesi, FACT).
- **Ödül fikri:** son ödül, çözülen tablonun hikâye kartı (rapor §6) ya da yan koleksiyon eskizi.
- **Neden 5:** Tema uyumu en yüksek tür. Yeni çizim istemiyor.

#### 4. Depo Sıkışıklığı (kayan sandıklar) — uyum 4

- **Nasıl oynanır:** 6×6'lık depoda sandıklar yalnız kendi yönünde kayar. Tablonun sandığını kapıdan çıkarırsın.
- **Bilinen oyunlar:** Unblock Me (Kiragames; 2009-05-16; 41.042 puanlama; "kırmızı bloğu dışarı kaydır", 18 binden fazla bulmaca) [Y1] (FACT). Parking Jam 3D (Popcore; 2020-02-08; 478.734 puanlama) [T25] (FACT); 2022'de yaklaşık 62 milyon indirme [T31] (REPORTED). Blok türü 2025 büyümesinin %13'ü (Color Block Jam) [T1] (REPORTED). Color Block Jam'in mağaza metni "süre bitmeden" diyor [T30] (FACT); bizde süre yok.
- **Neden eğlenceli:** Kilitli bir düzeni birkaç hamlede açmak. Renk oyunlarından farklı, mekânsal bir düşünme (ESTIMATE).
- **Efor:** M–L [build.md].
- **Çizim:** 2 ve 3 boy sandık, kapı işareti. Tahta dokusu ve tablo çerçevesi var.
- **Çözülebilirlik:** Bütün hamleler geri alınabilir. Rastgele bir yerleşimin ulaşılabilen bütün durumları taranır; kapıya istenen uzaklıktaki durum seçilir. En az hamle sayısı kesin bilinir. Bantlar yaklaşık 5–25 hamle (ESTIMATE). Genel problem PSPACE-tam; 6×6'nın en zor başlangıcı 51 hamle [W3] (REPORTED, Wikipedia).
- **Ödül fikri:** son ödül "depodan çıkan tablo": bir yan koleksiyon parçası.
- **Neden 4:** Temaya uyar (felaketten sonra müzenin deposu). Zorluk kesin ölçülür. Ama ekran kenarında sürüklemek Android'in geri hareketiyle çakışabilir [build.md].

#### 5. Çerçeve Vidaları (vida sıralama) — uyum 4

- **Nasıl oynanır:** Ödünç gelen tablonun sandığı renkli vidalarla kapalı. Vidaya dokununca kendi renginin kutusuna, kutu yoksa bekleme yerine gider; vidası biten tahta düşer ve son tahta düşünce tablo görünür.
- **Bilinen oyunlar:** Screwdom (Zego Global; 2025-01-02; 116.628 puanlama) [T25] (FACT). Vida türü 2025 büyümesinin %8'i; Screwdom 2025'in en çok kazandıran 25 bulmacası arasında [T1] (REPORTED). Wood Nuts & Bolts Puzzle (ABI Global; 2023-11-22; 234.358 puanlama) [T25] (FACT). Bir match-3'ün içinde bulunamadı.
- **Neden eğlenceli:** "Söküp açma" hissi; altından tablo çıkar. Sıralamanın planlı hâli: yanlış sıra bekleme yerini doldurur (ESTIMATE).
- **Efor:** L (ESTIMATE: katmanlı tahtalar, düşme canlandırması, bekleme yeri).
- **Çizim:** Tahta var. Vida başı = pirinç düğme üstünde taş şekli. Renk kutusu için tepsi var. Yeni: düşme canlandırması.
- **Çözülebilirlik:** Üçlü taş eşleştirmedeki gibi kurulur: önce tahtaların bir sökme sırası bulunur, renkler o sırada üçer üçer verilir. O sırayla oynayan bekleme yerini hiç doldurmaz. Sonra çözücü ve bot merdiveni (ESTIMATE). Bekleme yeri dolarsa aşama baştan; jeton gitmez.
- **Ödül fikri:** son ödül bir çerçeve süsü (kozmetik).
- **Neden 4:** İkinci "sıralama" türü ve tablo açılış anı. Ama tüpten daha pahalı.

#### 6. Depo Rafları (raf sıralama) — uyum 4

- **Nasıl oynanır:** Raflarda müze eşyaları var. Bir eşyayı boş bir yere taşırsın; aynı üç eşya bir rafta buluşunca raf temizlenir ve arkadaki sıra öne gelir.
- **Bilinen oyunlar:** Goods Puzzle: Sort Challenge (1SOFT); kural Google Play sayfasında [T27] (FACT, arama özeti); 2025 hibrit-gündelik gelirde 5. [T4] (REPORTED, zayıf: veri kaynağı yazılmamış bir video). Hexa Sort (Lion Studios Plus; 2023-11-01; 218.639 puanlama) [T25] (FACT). Goods Puzzle ve Blossom Sort 2024'ün en iyi yeni gündelik oyunları arasında [T3] (REPORTED).
- **Neden eğlenceli:** Dağınık rafı toplama hissi. Bir bakışta anlaşılır (ESTIMATE).
- **Efor:** L [build.md].
- **Çizim:** Müze eşyaları (vazo, büst, çerçeve, fırça) yeni çizim ister; ya da taşlar ve özel taşlar kullanılır. Raf var.
- **Çözülebilirlik:** "Geri temizleme" ile üretilir: boş rafa bir üçlü konur, oradaki sıra arkaya itilir; araya geri alınabilir taşımalar karışır. Böylece kurulumdan çözülür. Küçük bölümlerde tam arama, büyüklerde bot merdiveni [build.md].
- **Ödül fikri:** ortak merdiven; son ödül bir oda süsü (kozmetik).
- **Neden 4:** Proje sahibinin "eşya sıralama" örneğine en yakın tür. Ama yeni eşya çizimleri ister ve arka sıralar küçük ekranda zor okunur.

#### 7. Katalog Taşları (üçlü taş eşleştirme) — uyum 4

- **Nasıl oynanır:** Katmanlı taşlardan üstü açık olana dokunursun; taş 7 yerlik tepsiye gider. Aynı üç taş tepside buluşunca kaybolur; tepsi dolarsa aşama baştan.
- **Bilinen oyunlar:** Triple Tile (Tripledot; 2021-08-20; 271.976 puanlama) [T25] (FACT); makalenin son 52 haftasında en çok indirilen taş eşleştirme [T23] (REPORTED, 2023). Vita Mahjong (Vita Studio; 2023-12-01; 1.787.722 puanlama; mağaza metni "yaşlılar için" ve "büyük, net taşlar" diyor) [T25] (FACT). Whiteout Survival'ın mahjong benzeri mini oyunu Temmuz 2025'te gelir artışı getirdi [T2] (REPORTED).
- **Neden eğlenceli:** Düzen baştan görünür; oyuncu plan yapar. Match-3'teki zincirlerden daha az şans [T23] (REPORTED).
- **Efor:** L [build.md].
- **Çizim:** Krem, yaldız kenarlı bir taş tabanı. Yüzler taşlarımız ya da tamamlanmış tabloların ayrıntıları.
- **Çözülebilirlik:** Önce bir kaldırma sırası bulunur; türler bu sırada üçer üçer verilir. O sırayla oynayan tepside hiç ikiden fazla taş tutmaz [build.md]. Her aşamada herkese bir bedava "+1 yer" (ESTIMATE).
- **Ödül fikri:** son ödül, taşlarda görülen tablo ayrıntısının hikâye kartı.
- **Neden 4:** 30 yaş üstü için büyük taşlı sürüm iyi. Ama tepsi dolunca kayıp var; adil hissettirmeli.

#### 8. Yaldız Çizgisi (çizgi bağlama) — uyum 4

- **Nasıl oynanır:** Aynı renkli iki ucu yaldızlı bir çizgiyle bağlarsın. Çizgiler kesişmez ve bütün çerçeveyi doldurmalıdır.
- **Bilinen oyunlar:** Flow Free (Big Duck Games; 2012-06-07; 407 binden fazla puanlama; günlük bulmacalar) [T25] (FACT). Two Dots (Playdots; 2014) [T25] (FACT). Bir match-3'te en yakın örnek Fishdom'un boru çevirme ara oyunları [T21] (REPORTED).
- **Neden eğlenceli:** Yarım dakikadan iki dakikaya temiz bir mantık anı. Parmakla çizmek keyifli (ESTIMATE).
- **Efor:** M (ESTIMATE). `types.md` S diyor; tek çözümlü, bütün alanı dolduran bulmaca üretmek ve parmakla çizgi çekmek işi büyütür.
- **Çizim:** Çizgiler kodla çizilir; uçlar taşlarımız.
- **Çözülebilirlik:** Önce tahtayı dolduran yollar kurulur, sonra yalnız uçlar bırakılır. Geri izlemeli bir çözücü çözümün tek olduğunu denetler (ESTIMATE).
- **Ödül fikri:** son ödül bir yaldızlı çerçeve süsü (kozmetik).
- **Neden 4:** Ucuz, dilsiz, sakin. İlk dalgaya girmedi, çünkü üreticisi tüp sıralamadan daha az kesin.

#### 9. Kapıları Aç (oku çıkar) — uyum 3

- **Nasıl oynanır:** Her sandığın üstünde bir ok var. Dokununca sandık okun yönünde kayar; yolu açıksa depodan çıkar.
- **Bilinen oyunlar:** Arrows – Puzzle Escape (Lessmore; 2025-08-09; 512 binden fazla puanlama; "her oku çarpışmadan çıkar", "süre yok") [Y3] (FACT). Tap Away 3D (Popcore; 2021-05-21; 228 binden fazla puanlama; bloklar tek yöne uçar) [Y2] (FACT).
- **Neden eğlenceli:** Tek dokunuş. Boşalan tahta tatmin edici (ESTIMATE).
- **Efor:** M [build.md].
- **Çizim:** Depo Sıkışıklığı'nın sandıkları ve bir ok işareti.
- **Çözülebilirlik:** Sandıklar ters çıkış sırasıyla yerleştirilir. "Kim kimi engelliyor" çizgesinde döngü yoksa bölüm çözülür; çizgenin derinliği zorluktur [build.md].
- **Ödül fikri:** ortak merdiven.
- **Neden 3:** Basit ve iyi, ama Depo Sıkışıklığı'yla aynı tema; başta ikisinden biri yeter. Arrows'un mağaza metni "tek bir kalp kaybetmeden" temizlemeyi soruyor [Y3] (FACT); bizde çarpan sandık yalnız yerine döner, bir şey kaybettirmez (ESTIMATE).

#### 10. Restorasyon Izgarası (nonogram) — uyum 3

- **Nasıl oynanır:** Satır ve sütun başındaki sayılar, dolu karelerin kaç tane ve hangi sırada olduğunu söyler. Mantıkla ızgarayı doldurursun; bir süsleme motifi ya da imza ortaya çıkar.
- **Bilinen oyunlar:** Nonogram.com (Easybrain; 2019-03-08; 196.040 puanlama; süreli mevsim etkinlikleri ve kartpostallar, aylık kupa veren günlük görevler) [T25] (FACT).
- **Neden eğlenceli:** Saf mantık, şans yok, ödül bir resim (ESTIMATE).
- **Bir aşama:** 5×5 bir dakikadan az; 10×10 yaklaşık 3–8 dakika (ESTIMATE).
- **Efor:** M–L [build.md].
- **Çizim:** Izgara kodla çizilir; motifler veridir.
- **Çözülebilirlik:** Yalnız satır satır mantıkla, tahminsiz çözülen motifler tutulur; böylece çözüm tektir [build.md].
- **Ödül fikri:** ortaya çıkan motif albüme bir damga olarak girer (kozmetik).
- **Neden 3:** Mantık sevenlere iyi. Ama kuralı anlatmak zor ve küçük karelere dokunmak telefonda zor (en çok 10×10).

#### 11. Kayan Tablo (15 bulmacası) — uyum 3

- **Nasıl oynanır:** Tablo 3×3 ya da 4×4 karelere bölünür, bir kare boştur. Kareleri boşluğa kaydırıp tabloyu düzeltirsin.
- **Bilinen oyunlar:** Klasik oyun; bu araştırmada kaynaklı bir mobil hit yok. Matematik: dizilişlerin yarısı çözülemez; 3×3 en çok 31, 4×4 en çok 80 hamlede çözülür [W4] (REPORTED, Wikipedia; doğrulamada yeniden açılmadı).
- **Neden eğlenceli:** Tanıdık; 30 yaş üstü oyuncu bilir (ESTIMATE).
- **Efor:** M [build.md]. **Çizim:** yok.
- **Çözülebilirlik:** Çözülmüş tablodan yasal kaydırmalarla karıştırılır. Uzunluk tam aramayla ölçülür [build.md].
- **Ödül fikri:** ortak merdiven.
- **Neden 3:** 4×4 uzun ve yorucu olabilir. Takas Yapbozu aynı işi daha rahat yapıyor.

#### 12. Döndür (döndürmeli kareler) — uyum 3

- **Nasıl oynanır:** Tablo karelere bölünür, kareler dönmüştür. Dokundukça kare döner; hepsi düzelince aşama biter.
- **Bilinen oyunlar:** Bu araştırmada kaynaklı bir örnek yok.
- **Neden eğlenceli:** Tablo birkaç dokunuşta düzelir; hiç takılmaz (ESTIMATE).
- **Ödül fikri:** küçük; ortak merdivenin ilk basamakları.
- **Efor:** S–M [build.md]. **Çizim:** yok.
- **Çözülebilirlik:** Her zaman çözülür; zorluk dönmüş kare sayısıdır.
- **Neden 3:** Çok kolay. İlk tanıtıma ya da çok kısa bir güne iyi; tek başına bir hafta taşımaz.

#### 13. Sızıntıyı Yönlendir (boru çevirme) — uyum 3

- **Nasıl oynanır:** Çatıdan su sızıyor. Boru parçalarını çevirip suyu tabloya değil kovaya götürürsün.
- **Bilinen oyunlar:** Fishdom'un boru ve pervane çevirme ara oyunları; kazanınca elmas ve altın verir, yanlış hamle kaybettirir, yeniden oynanır [T21] (REPORTED, 2022-11-29).
- **Neden eğlenceli:** Suyun yolu birden açılır, tablo kurtulur (ESTIMATE).
- **Efor:** M–L [build.md]. **Çizim:** boru parçaları ve bir damla.
- **Çözülebilirlik:** Önce suyun yolu kurulur, sonra parçalar döndürülerek karıştırılır. Geri izlemeli bir çözücü denetler [build.md].
- **Ödül fikri:** ortak merdiven.
- **Neden 3:** Temaya uyar (tabloyu korumak). Ama boru çizimleri ister ve oyun dünyada çok yaygın.

#### 14. Temizlik Makinesi (bantlı renk atıcı) — uyum 3

- **Nasıl oynanır:** Kirli bir tablo piksellere bölünür. Renkli fırçalar bir bantta döner ve kendi rengindeki kiri temizler; tablo renk renk ortaya çıkar.
- **Bilinen oyunlar:** Pixel Flow! (Loom Games; 2025-08-17; 170.553 puanlama; atıcılar bantta döner, her birinin cephanesi var, 5 bekleme yeri, bandın kapasitesi sınırlı) [T25] (FACT). ABD 2025 son çeyrekte haftada 457 bin indirme [T5] (REPORTED).
- **Neden eğlenceli:** Tablo gözünün önünde, renk renk temizlenir (ESTIMATE).
- **Efor:** M–L (ESTIMATE). Bant zamanlaması botla ölçmeyi zorlaştırır.
- **Çizim:** Piksel kir tablodan hesaplanır; fırçalar taşlarımız.
- **Çözülebilirlik:** Simülasyonla. Bandın sırası ve zamanı önemli; zorluk ölçmek zor.
- **Ödül fikri:** son ödül, temizlenen tablonun hikâye kartı.
- **Neden 3:** Tema çok güçlü ve en yeni liste hiti. Ama bandın akışı süre baskısına yakın; kuralımızla gerilir.

#### 15. Duvara As (blok yerleştirme) — uyum 3

- **Nasıl oynanır:** Verilen çerçeve şekillerini galeri duvarındaki ızgaraya sürüklersin. Dolan satır ya da sütun "asılmış" sayılır ve temizlenir.
- **Bilinen oyunlar:** Block Blast! (geliştirici Hungry Studio, satıcı ARETIS LIMITED; 2022-04-03; 2.778.560 puanlama) [T25, doğrulama T8] (FACT). Royal Kingdom'da Tetris benzeri bir blok mini oyunu var [T3] (REPORTED). Homescapes'te deneme aşamasında Ice Crash: ızgaraya buz küpü yerleştirip satır temizleme [C13] (REPORTED).
- **Neden eğlenceli:** Basit sürükle-bırak; dolan satırın tatmini (ESTIMATE).
- **Efor:** S–M [types.md].
- **Çizim:** çerçeve şekilleri; duvar ızgarası kodla çizilir.
- **Çözülebilirlik:** Etkinlikte bölümlü sürüm: parça sırası sabit, bir çözücü arar. Sonsuz sürüm rastgele parça verir; etkinliğe uymaz (ESTIMATE).
- **Ödül fikri:** son ödül bir oda duvar rengi (kozmetik).
- **Neden 3:** Proje sahibinin sonraki oyunu bir blok bulmacası. İki oyun birbirine benzemesin diye burada küçük tutulmalı (ESTIMATE).

### 2.4 Raporda zaten olanlar (yerinde kalır)

Bu fikirler `monetization-and-retention.md` §5'te. Burada yalnız etkinlik türü olarak durumları yazılı.

| Bizdeki adı | Tür | Etkinlik olarak not | Uyum |
|---|---|---|---|
| **Kayıp Eşyalar** (raporda "Ayrıntı Avı") | Gizli nesne: tabloda kaybolan eşyaları bul | Tablolar en iyi sahne. Her tabloya elle koordinat gerekir (her tablo S–M). Eşya adları söz ister; simgeyle gösterilirse dil gerekmez. June's Journey (wooga) [T25] (FACT); ABD'de 2025 ilk çeyrekte haftada yaklaşık 2,2 milyon $ [T6] (REPORTED) | 4 |
| Arşiv Kazısı | Kazı | Bulunacaklar önceden sabit ve herkese aynı olmalı; yoksa piyango olur. Royal Match Hidden Temple 39. bölümde, kazmalar kazanmaktan [C8] (FACT). Kazı, en yaygın iki mini oyun biçiminden biri [T3] (REPORTED) | 4 |
| Sahteyi Bul | Fark bulma | Değiştirilmiş kopya için her müzenin lisans şartı kontrol edilir. Differences (Easybrain; 2019-03-16; 600.711 puanlama; günlük görevler, mevsim etkinlikleri) [T25] (FACT) | 3 |
| Katalog Eşleştirme | Hafıza | Kısmen şans; çevirme hakkı sabit olmalı. Royal Kingdom Magic Carnival: 51. bölüm, küreler kazanmaktan, yanlış eşler solar, etkinlikler arası taşınmaz [C9] (FACT) | 3 |
| Kimin Eseri? | Bilgi yarışması | 13 dilde soru ve şık çevirisi | 2 |
| Pigment Atölyesi | Birleştirme | Kendi ekonomisi olan büyük iş (L); sonra | 2 |
| Vernik Temizleme | Kazıma | Bölüm yolundaki Restoratör Masası'nın işi; etkinlik değil | — |

Match-3 motorunu kullanan üç kip de raporda: **Küratörün Sınavı** (geçilmiş bir bölüm, tek deneme; S–M), **Gala gecesi / Renk Yağmuru** (kaybı olmayan altın tahtası; M), **Gece Nöbeti** (ilerleyen tehdit; motor değişikliği ve yeniden kalibrasyon ister, L–XL; ertelenmeli) [build.md]. Bunlar yeni bir oyun değil; sessiz haftaları doldurabilir.

### 2.5 Uymayanlar

| Tür | Örnek | Neden uymuyor |
|---|---|---|
| Top düşürme (plinko) | Royal Match Dragon Nest: zor bölüm daha çok top, yuvalar x1, x2, x3, x10 [T14, doğrulama] (REPORTED). Monopoly GO Peg-E: jetonlar "satın alınabilir ya da kazanılabilir" [T12] (REPORTED) | Sonuç rastgele. Hakkı satılırsa "parayla rastgele ürün" olur (§17, §19). Satılmasa da aile oyununda kumar biçimi |
| Çark, çekiliş | Toon Blast Wheel of Luck: her çevirme bir satın alma fiyatına [C12] (FACT). Candy Crush Yeti's Draw: gruptan rastgele bir kazanan [C10] (FACT) | Aynı neden |
| Zar ve tahta oyunları | Royal Match Train Journey: trenler rulette çevrilir, dört kişilik takımla ortak hat [T35] (REPORTED) | Rastgele, ve ortaklar sunucu ister |
| Takım ve ortak mini oyunlar | Dragon Nest: dört ejderha, her biri bir ortakla; toplam dört ortak [C1, T14, doğrulama T6] (REPORTED) | Hesap, eşleştirme, sunucu ister; bizde sunucu yok |
| Turnuva ve skor yarışı | Homescapes Ice Crash skor yarışı [C13] (REPORTED); Tile Busters turnuvaları [T25] (FACT) | Gerçek rakip sunucu ister. Simüle rakip ayrı bir karar (rapor §9, madde 13) |
| Süreli oyunlar | Color Block Jam "süre bitmeden" [T30] (FACT); Toon Blast Rescue Mission süreli [C12, doğrulama C3] (FACT); Surprise Calendar: düşen hediyelere dokun, TNT'den kaç [C12] (FACT) | Süre baskısı yok kuralı (rapor §5). Hedef oyuncu rahat oynamak istiyor: bulmaca oyuncularının %84'ü rahatlamak için oynuyor [T1] (REPORTED) |
| 3D eşleştirme | Match Factory!, Triple Match 3D (süreli) [T25] (FACT) | Çok sayıda 3D model, fizik yığını ve süre; L |
| Fizik oyunları | Pim çekme (Playrix; reklamları İngiltere'de yasaklandı, ASA kararı 30 Eylül 2020) [T16] (FACT); Toon Blast Cannon Fest, sonra bağımsız oyun Smash Fest! [T10] (REPORTED); düşür-birleştir | Her telefonda aynı sonucu veren fizik zor; bot ölçemez; L–XL. Pim çekme ileride elle yapılmış bölümlerle "Çatı Sızıntısı" olarak düşünülebilir |
| Kelime oyunları | Wordle benzeri mini oyunlar (Gossip Harbor, Hexa Sort) [T3] (REPORTED); Word Search Explorer [T5] (REPORTED) | Her dilde ayrı kelime listesi, 13 dil; yazı tiplerinin yeniden kurulması; içerik pahalı |
| İnsan ya da karakter isteyenler | King's Nightmare: reklamlardaki "kralı kurtar" bulmacası [T19] (REPORTED); otobüs ve koltuk sıkışıklığı (Bus Jam [T25] FACT, Seat Away [T4] REPORTED) | İnsan ve maskot çizmiyoruz. Soyut jetonlarla tema zayıf kalır |
| Baloncuk atma | Royal Match'te yaklaşık bir dakikalık tur [T9] (REPORTED) | Nişan ve sekme fiziği; temaya zayıf. Sonra bakılabilir |

---

## 3. Ortak çerçeve

Ortak çerçeve bir kez yapılır: **L, yaklaşık 2–3 arayüz aşaması** [build.md]. Sonra her yeni tür yalnız kendi kuralını, görünümünü ve birkaç sözünü getirir.

### 3.1 Oyuncunun yolu

1. **Salondaki etkinlik çerçevesi.** Tezgâhın solunda, hediye çerçevesinin eşi olan küçük yaldızlı bir çerçeve. İçinde açık oyunun işareti; bekleyen jeton ya da bedava aşama varsa kırmızı nokta; altında bitişe kalan süre ("2 gün kaldı"). Hediye çerçevesi tezgâhın sağında zaten böyle çalışıyor (FACT, `GiftFrame`).
2. **Etkinlikler sayfası.** Alt çubukta beşinci yer. Bu haftanın kartları; gelecek haftanınkiler soluk ve başlama tarihli.
3. **Kural kartı.** Oyunun adı, hedefin küçük resmi, 2–3 satır kural, sıradaki aşamanın ödülü, yeşil "Oyna". Bir tür ilk kez görülünce rehberli bir ilk hamle.
4. **Aşama sahnesi.** Galeri duvarı ve lambalar. Üstte "aşama 3 / 6" plaketi. Altta rafta duraklat, geri al ve ipucu.
5. **Sonuç kartı.** Ödül zaten cüzdanda. Aşama noktaları ve yeşil "Devam".
6. **Salona dönüş.** Zincirin ilk oyunu bittiyse çerçeve ikinci oyunun işaretine döner.

`sketches.jpg`'deki "Ortak çerçeve" paneli kural kartını ve salondaki çerçeveyi gösterir.

### 3.2 Parçalar

Kod adları `build.md`'den, doğrulamanın düzeltmeleriyle.

| Parça | Ne yapar | Kodda |
|---|---|---|
| Takvim | O gün açık etkinlikleri tarihten hesaplar | `EventCalendar.InEffectOn(DayStamp)`. `DayStamp` ve `IWallClock` olduğu gibi kullanılır (FACT: ikisi de var) |
| Etkinlik çerçevesi | Salondaki işaret | `EventFrame`, `GiftFrame` örnek alınır |
| Etkinlikler sayfası | Bu hafta ve gelecek hafta | `Destination.Events`. Tasarım tuvali beşinci yeri çiziyor, arkası boş (FACT, `Destination.cs` yorumu) |
| Kural kartı | Kural, ödül, "Oyna" | `EventCard`; `LevelStartCard` ve `NoticeCard` dilinde |
| Aşama sahnesi | Kabuk: duvar, plaket, raf, karanlıktan açılış, sonuç kartı | `MiniGameStage`. Bugün dört `IStage` var (`EntryStage`, `MuseumStage`, `GameStage`, `JigsawStage`); bu beşincisi olur [doğrulama B1]. Her tür bir `IMiniGameView` |
| Kural motoru | Durum, hamle, "çözüldü mü" | Yeni `Puzzle.Minis` derlemesi (netstandard2.1, C# 9), ortak `IPuzzleRules<TState, TMove>` |
| Üretici ve ölçüm | Bölüm paketleri | Yeni `Puzzle.Minis.Lab` (oyuna girmez): üretici, çözücü, gürültülü bot merdiveni. Paketler `minis/<tür>/pack.json`, birkaç yüz KB |
| Ödül defteri | Her ödülü bir kez öder | `EventLedger`. Ödül aşamanın kazanıldığı anda, aynı kayıtta verilir; tablo ödülündeki `WalletLedger.WithPiecesInPlace` kalıbı (FACT) |
| Jeton | Kazanılan bölümden jeton | Bölüm kazanmanın işlendiği `AttemptLedger` yanında (FACT: kazanma olayı orada) |
| Oturum bağlantısı | Sahneler arası geçiş | `GameSession.FollowThePlayer` içindeki `switch`'e iki satır. `Advance` dokuz adım; mini oyun "olanı sakla", "cüzdan isteği" ve "oyuncuyu izle" adımlarına bağlanır [doğrulama B2] |
| Kayıt | Etkinlik ilerlemesi | Kayıt dosyasının 6. sürümü, `events` bloğu (§3.4) |
| Sözler | 13 dil | `WordId`; paketleyici yalnız tablosu tam olan dili sunar (FACT) |
| Ekonomi | Yeni altın kaynağı | Simülatöre `CoinSource.Event`. `CoinSource` `src/Puzzle.Economy/Simulating/Decisions.cs` içinde [doğrulama B3] |
| Çekimler ve yürüyüş | Ekran denetimi | `MiniGameShots` (tür başına anlar), `GameWalk.Events`. Ekran listesi bugünküyle aynı: 1080×2424, 2076×2152, 1080×2340 ve öbürleri |

### 3.3 Ortak kurallar

- **Can yok, süre yok, atlanabilir.**
- **Jeton aşamayı açar.** Açılan aşama kazanılana kadar açık kalır. Çıkmak ya da kaybetmek jetonu yakmaz.
- **Tahta yalnız bellekte.** Oyun kapanırsa aşama baştan başlar; başka bir şey kaybolmaz. Bugünkü bölüm denemesi de böyle (FACT).
- **Ödül bir kez.** Aşama kazanıldığı anda ödül cüzdana girer ve kayıt yazılır. Aynı aşama ikinci kez ödül vermez.
- **Herkese aynı.** Bölümler önceden üretilip ölçülür. Takvim o günün bölümünü tohumla seçer. Oyuncuya göre ayar yok (§17).
- **Parayla yardım yok.** Geri alma herkese bedava ve sınırsız. "Fazladan tüp" gibi yardımlar satılmaz; satılırsa harcama zorluğu değiştirir (öneri; karar §7).
- **Kare oyun alanı.** Yatay ekran desteği mini oyunlardan önce geliyor (proje sahibinin 2026-10-24 sırası). Mini oyunlar kare bir alan ister: tüpler iki sırada, 6×6 depo, kare taş yığını. Böylece katlanır telefonun iç ekranına (2076×2152) ve yatay ekrana sığar [build.md].

### 3.4 Kayıt

- Bugün kayıt dosyası 5. sürüm (FACT, `ProgressDocument`).
- 6. sürüm bir `events` listesi ekler. Örnek: `{"instance":"2026-11-W1-weekday","tokens":2,"stagesWon":3,"freePlayDay":…}`.
- 5. sürüm dosyası etkinliksiz okunur. Sonraki bir sürümün bilmediğimiz türleri atlanır; öğretici adımlarında bugün de böyle (FACT).
- Bozuk bir kayıt (eksi sayı, olandan fazla aşama) "hasarlı" okunur; öbür bloklarda da böyle (FACT).
- **Sınır:** son günü 35 günden eski etkinlikler kaydedilirken silinir. En çok 4–8 kayıt kalır (ESTIMATE). Süresi geçen etkinliğin jetonları gider; kural kartı bunu söyler.
- Bu, kalıcı şemada bir değişiklik. Eklemeli, ama proje sahibinin onayını ister (§7).

### 3.5 Yeni bir mini oyun eklemek

| Adım | İş | Efor (ESTIMATE) |
|---|---|---|
| 1 | Kurallar ve testleri: `Puzzle.Minis/<Tür>/` | S |
| 2 | Üretici, çözücü, ölçüm ve paket | S–M |
| 3 | Görünüm (`IMiniGameView`) ve Unity'siz, testli yerleşim | M |
| 4 | `EventKind`'a bir üye, dönüşüm tablosuna bir satır. Derleyici eksik yerleri gösterir (kapalı küme) | S |
| 5 | Tür işareti ve küçük çizimler; proje sahibinin bakışı | S |
| 6 | Rehberli ilk hamle (`TutorialStep`) | S |
| 7 | 4–6 söz, 13 dilde birlikte; yazı tipleri yeniden kurulur | S kod, çeviri ayrı |
| 8 | Simülatöre süre ve ödül | S |
| 9 | Telefon testi (`docs/PHONE_TESTS.md`) | S |

Çoğu tür toplamda M–L. Takvim, defter, kayıt, kartlar, sonuç ve sesler yeniden yapılmaz. Var olan ses işaretleri çoğu ihtiyaca yeter (`Press`, `Swap`, `Match`, `PiecePlaced`, `Win`, `Coin`; FACT).

### 3.6 Riskler

- **APK boyutu.** 283 MB (16 Ekim). Mini oyunlar yalnız küçük çizim ve JSON eklemeli; yeni tablo yok.
- **Sözler.** Eksik çevirisi olan dil oyundan düşer (FACT). Bir türün sözleri 13 dilde birlikte gelmeli; ardından CJK yazı tipleri yeniden kurulur. `words/tr.json` bugün 266 söz [doğrulama B4]; çerçeve ve dört tür yaklaşık 45–50 yeni söz ekler (ESTIMATE).
- **Süre ve ekonomi.** Mini oyun dakikaları 10 dakikalık oturumdan pay alır. Simülatör bunu bilmezse "100. bölüm 8. gün" sessizce kayar (§5.5).
- **Kamu sözleşmeleri.** Kayıt 6. sürüm, `Reward`'a can süresi, beşinci çubuk yeri: hepsi proje sahibinin kararı.
- **Takvim simgesi.** `GAME_DESIGN.md` 1114. satır takvim simgelerinin tasarım tuvalinden geldiğini söylüyor. Ama depoda takvim çizimi yok: `unity/Assets/Art/Gallery`, `GalleryArt.cs`, `art/unity` ve `out/design/icons` içinde bulunmadı [doğrulama]. Belge ile depo uyuşmuyor; beşinci çubuk yerinden önce çözülmeli.

---

## 4. Aylık takvim

### 4.1 Haftanın biçimi

- Hafta iki bloktur: **hafta içi** Pazartesi 00:00 → Cuma 00:00 (4 gün) ve **hafta sonu** Cuma 00:00 → Pazartesi 00:00 (3 gün). Saat, oyuncunun telefonundaki yerel saattir.
- Royal Match ve Homescapes haftayı aynı biçimde ikiye bölüyor [C1, C13] (REPORTED). Royal Match blokları UTC 08:00'de değiştiriyor; ama oyuncuları eşleştirmeyen Lightning Rush yerel saatle çalışıyor [C1] (REPORTED). Bizde rakip yok; yerel saat yeter (ESTIMATE). Sensor Tower'a göre Royal Match orta uzunluktaki etkinlikleri hafta sonuna denk getiriyor [T7] (REPORTED).
- **Her blokta bir zincir:** iki mini oyun art arda. İlki bitince ikincisi açılır. Ekranda aynı anda tek mini oyun olur. Royal Match'in hafta sonu zinciri böyle: Magic Cauldron bitince Balloon Rise açılıyor [C1] (REPORTED, doğrulandı). Hafta içi zinciri (Hidden Temple → Pinata Party) kaynakta çelişkili [doğrulama C1]; bizim tasarım ona dayanmıyor.
- Blok, zincir bitmeden biterse ikinci oyun o hafta açılmaz. Ödüller yalnız oynanan aşamalar içindir.

### 4.2 Örnek ay: Kasım 2026

| Hafta | Tarih | Hafta içi (Pzt–Per) | Hafta sonu (Cum–Paz) | Her zaman açık |
|---|---|---|---|---|
| **1 · Atölye Haftası** | 2–8 Kasım | Boya Tüpleri → Takas Yapbozu | Pigment Tarifi → Depo Sıkışıklığı | Günün hediyesi (bugün var). İleride sezon pasosu ve Küratör Sınavı |
| 2 · Sessiz | 9–15 Kasım | — | — | Aynı |
| **3 · Atölye Haftası** | 16–22 Kasım | Takas Yapbozu → Boya Tüpleri | Depo Sıkışıklığı → Pigment Tarifi | Aynı |
| 4 · Sessiz | 23–29 Kasım | — | — | Aynı |
| 5 (bazı aylarda) | 30 Kasım–6 Aralık | Sessiz ya da mevsim etkinliği | | Aynı |

- **Hafta, pazartesisinin düştüğü aya sayılır.** 1. ve 3. hafta hiçbir zaman iki aya taşmaz: 1. haftanın pazarı en geç ayın 13'ü, 3. haftanınki en geç 27'sidir. `build.md`, ISO haftaları aya taştığı için "ayın 1–7. günleri" gibi bloklar önermişti; pazartesiye bağlı 1. ve 3. hafta aynı kaygıyı giderir ve hafta sonunu da korur (ESTIMATE).
- **Haftada 4, ayda 8 mini oyun.** Dört türle her tür ayda iki kez gelir, her seferinde başka bölümlerle. Sekiz tür olunca ayda bir.
- Her zincir bir "taş" oyununu (Boya Tüpleri, Pigment Tarifi) bir "tablo ya da mekân" oyunuyla (Takas Yapbozu, Depo Sıkışıklığı) karıştırır.
- Sessiz haftalar etkinliği özel tutar ve ekonomiyi korur (ESTIMATE). Raporun Arşiv Kazısı gibi uzun bir etkinlik ileride 2. haftaya konabilir.
- Aralık 2026'da 1. hafta 7–13 Aralık, 3. hafta 21–27 Aralık; Noel'e denk gelir. Böyle günler için kısa, tarihli bir mevsim listesi kuralın üstüne yazılabilir [calendar.md].
- `sketches.jpg`'nin alt şeridi bu ayı çizer.

### 4.3 Dönüşüm kuralı

- Takvim bir tarih listesi değil, **bir kuraldır.** Liste biter; kural bitmez [calendar.md].
- `yuva sırası` = sabit bir başlangıç pazartesisinden bu yana geçen Atölye yuvası sayısı. `tür` = `dönüşüm tablosu[yuva sırası mod tablo uzunluğu]`.
- Dört türle örnek tablo (sekiz yuva): Boya Tüpleri, Takas Yapbozu, Pigment Tarifi, Depo Sıkışıklığı, Takas Yapbozu, Boya Tüpleri, Depo Sıkışıklığı, Pigment Tarifi. Tablo başa dönerken de aynı tür art arda gelmez.
- Yeni tür = tabloya yeni satırlar. Döngü uzar, bozulmaz.
- Kurallar: aynı tür art arda gelmez; bir zincirde aynı aileden iki oyun olmaz.
- Herkes aynı gün aynı türü ve aynı bölümleri görür.
- Bir komut gelecek ayların takvimini yazdırır. Mağaza kartları buradan hazırlanır (§4.5).

### 4.4 Açılış

- **İlk Atölye Haftası 35. bölümden sonra açılır** (öneri). Royal Match ilk mini oyununu 39'da, Royal Kingdom 36'da, Toon Blast 35'te açıyor [C8, C9, C12] (FACT). Bizim hızımızla 35. bölüm yaklaşık 2.–3. gün; ilk haftanın yeniliği tam o sırada azalır (ESTIMATE; 100. bölüm 8. gün, FACT §25).
- 35'ten önce Etkinlikler sayfası kilitli görünür: "35. bölümde açılır" ve gelecek Atölye Haftasının tarihi. Rapor §6'daki "gelecek içerik görünsün" ilkesi.
- Haftanın ortasında 35'e varan oyuncu açık zincire hemen katılır.
- İlk sürümde bütün türler 35'te açılır. Her tür ilk görüldüğünde rehberli bir ilk hamle verir.

### 4.5 Duyuru

| Kanal | Ne zaman | Sunucu ister mi | Dayanak |
|---|---|---|---|
| Salondaki etkinlik çerçevesi | Blok boyunca; kırmızı nokta, kalan süre | Hayır | Ana ekranda geri sayımlı simge: Royal Match, Candy Crush, Monopoly GO [C1, C10, C15] (REPORTED/FACT) |
| İlk açılış kartı "Şimdi / Sonra" | Oyuncunun ilk Atölye Haftası | Hayır | Royal Match [C7] (REPORTED) |
| Etkinlikler sayfası | Her zaman; bu hafta ve gelecek hafta | Hayır | Candy Crush'ın Events sekmesi [C11] (REPORTED) |
| Yerel bildirim (isteğe bağlı) | Atölye Haftası başında bir tane | Hayır: Unity Mobile Notifications bildirimi telefonda zamanlar [C31] (FACT) | Android 13 ve üstü POST_NOTIFICATIONS iznini ister [C30] (FACT). Öneri: oyuncu "haber ver" derse izin sorulur |
| App Store In-App Events | Atölye Haftaları | Hayır; App Store Connect'te kurulur | En çok 31 gün; başlamadan en çok 14 gün önce tanıtılır; aynı anda 10 yayında [C26] (FACT) |
| Google Play tanıtım içeriği | Atölye Haftaları | Hayır; Play Console'da kurulur | Bütün oyunlara açık; en çok 4 hafta; en az 4 gün önce gönderilir (öne çıkarma için 14), en erken 60 gün önce. **Öne çıkarma, derin bağlantı ve kitle seçimi** yalnız "Premium growth tools" ölçütünü karşılayan uygulamalara açık [C27, doğrulama C4] (FACT). Yeni ve küçük bir oyun kartı alır ama öne çıkarılmayabilir |

Mağaza kartları için tarihlerin önceden bilinmesi gerekir. Kurala dayalı takvim bunu verir.

### 4.6 Telefonun saatiyle, internetsiz

- Oyunun sunucusu yok; oyun telefondan bir şey göndermiyor (FACT, §20). Takvim tamamen telefonda hesaplanır.
- **Bugünkü dayanaklar:** `IWallClock` telefonun saatini oyuncunun saat dilimiyle, tam saniye olarak verir; düzenleyici ve testler sabit bir saat kullanır (FACT). `DayStamp` günleri karşılaştırır, saate güvenmez. Günlük hediye, geri alınan tarihle bir şey açmıyor (FACT).
- **Kimlikler yalnız ileri gider.** Her etkinliğin bir kimliği var (yuva sırası). Kayıt, başlatılan en yüksek etkinliği ve alınan son günü tutar. Biten etkinlik bir daha açılmaz. Her ödül kimlik başına bir kez verilir.
  - Saati **ileri** almak gelecek etkinliği erken açar, ama yalnız **ödünç alır**: gerçek tarih yetişene kadar yeni bir şey gelmez. Fazladan ödül yok.
  - Saati **geri** almak hiçbir şey açmaz.
- **Sıçrama denetimi (Android).** Her kayıtta üç sayı saklanır: telefon saati, telefon açıldığından beri geçen süre (`elapsedRealtime`) ve açılış sayısı (`BOOT_COUNT`). Geçen süreyi kullanıcı değiştiremez; telefon uyurken de sayar [C21] (FACT). Açılış sayısı API 24'ten beri var [C22] (FACT). Aynı açılışta telefon saati geçen süreden çok daha fazla ilerlemişse saat ileri alınmıştır; telefon saati gerideyse geri alınmıştır.
- **Android 13 ve üstü.** `currentNetworkTimeClock()` kullanıcının değiştiremediği ağ saatini verir. Uygulamanın ağ isteği yapmasını gerektirmez; son eşitlemeyi kullanır; ağ saati yoksa hata verir. Belgesi bunun güvenlik için olmadığını da yazar [C21] (FACT). En düşük Android sürümümüz API 26 (FACT); bu saat yalnız yeni telefonlarda var. Unity'den çağrılabildiği telefonda denenmedi (ESTIMATE).
- **Otomatik saat ayarı.** `AUTO_TIME` saatin otomatik olup olmadığını söyler [C22] (FACT). Belgenin önerdiği TimeManager sistem uygulamalarına ait; bizim için tek yol ayarı doğrudan okumak [doğrulama].
- **iOS.** Gün, operatör saati ya da yaz saati değişince `significantTimeChangeNotification` gelir [C23] (FACT); takvim o an yeniden hesaplanır. `systemUptime` uykuda durur ve iOS gizlilik manifestosunda bildirilmesi gereken bir API'dir [C24] (FACT).
- **Tepki: ceza yok, mesaj yok.** Süreli ödüller (örneğin 15 dakikalık ücretsiz booster) açılış içinde değişmeyen saatle tüketilir: saati ileri almak onları yakmaz, geri almak uzatmaz. Can dolumu, saat ne derse desin, eksik can başına 30 dakikadan uzun beklemez.
- **Neden ceza yok.** Candy Crush'ın can sayacı telefon saatine bakıyor. Saat değişince (örneğin yolculukta) oyunculara çok uzun bekleme çıkıyor; King'in yardım sayfası saati elle ileri geri alarak düzeltmeyi anlatıyor [C10] (FACT). Dürüst yolcu cezalandırılmamalı. Bizde etkinlik ödülleri küçük ve kimseyle paylaşılmıyor; hile yapan yalnız kendi oyununu değiştirir (ESTIMATE). Unity forumundaki yaygın öneri de bu yönde: geri sıçramayı yakala; ileri sıçramayı internetsiz tam yakalayamazsın; etkinliklere güvenilir saat, can dolumuna düz saat [C25] (REPORTED).
- Saat dilimi değişirse blok sınırı birkaç saat kayar. Kabul edilir (ESTIMATE).

### 4.7 Sonra: uzak takvim dosyası ne katar

Oyunların sitesinde statik bir JSON dosyası. Rapor §7.3'teki B yolu; planlanan düzeltme paketleriyle aynı yol (§20).

| Ne katar | Nasıl |
|---|---|
| Güncellemesiz değiştirme: taşıma, iptal, ödül düzeltme | Dosya kuralın üstüne yazan istisnalar taşır: `{id, tür, başlangıç, bitiş, merdiven}` |
| Güvenilir saat | Aynı indirmenin HTTP `Date` başlığı. RFC 9110, saati olan bir kaynak sunucunun bu başlığı göndermesini ister [C29] (FACT) |
| Kapatma anahtarı | Bozuk çıkan etkinliğe `enabled: false` |
| Temalı etkinlikler | Türün kodu ve çizimi uygulamada (ya da bir yama paketinde) olmalı; dosya yeni tür getiremez |
| A/B testi, ülkeye göre saat | Yalnız bir analiz servisi ve rıza ekranıyla. Firebase Remote Config sürüme, dile, kitleye ve yüzdeye göre koşul sunar [C28] (FACT) |
| Bedeli | INTERNET izni, gizlilik metni, rıza. Ödüllü reklam ve satın alma gelince INTERNET izni zaten gelecek (rapor §7.3) |

**Kural:** uygulamadaki takvim dosya olmadan sonsuza kadar çalışır; dosya yalnız üstüne yazar. Firebase'in önerisi de bu: uygulamada varsayılanlar bulunur, yeni dosya bir sonraki açılışta etkinleşir, oyunun ortasında ekran değişmez [C28] (FACT).

---

## 5. Ödüller

### 5.1 Ödül türleri

| Ödül | Bizde neden anlamlı | Kural | Kodda bugün | Öneri |
|---|---|---|---|---|
| **Altın** | Booster, "+5 hamle" ve yan koleksiyon parçası alır | Yeni altın kaynağı simülatörden geçer | `Reward` altın taşır (FACT) | Merdivenin ortasında ve sonunda |
| **Booster** | Her oyuncu bir sonraki bölümde hisseder | Ücretsiz; hızı değiştirir, simülatörden geçer | `Reward` booster taşır (FACT) | Çoğu basamak tek booster; hep belli, hiç rastgele değil |
| **Süreli ücretsiz booster** ("15 dakika: her bölüm fırçayla başlar") | Herkes hemen hisseder; değeri gerçek zamanla sınırlı | Satılmaz; üst üste en çok 60 dakika | Yok; yeni bir zamanlı durum | Her 3–4 basamakta bir; can süresinin yerine. Candy Crush'ta süreli booster gerçek zamanla akar, üst üste eklenir, kaybedince gitmez [C10] (FACT). Royal Match merdivenlerinde 15 ve 30 dakikalık sınırsız booster var [C1] (REPORTED) |
| **Can** | Bugün az (§5.2) | Can satılmaz (rapor kuralı) | `Reward`'da can yok; `LifeMeter` can veremez (FACT) | Saklanan can verilmesin |
| **Sınırsız can süresi** | Yalnız uzun oturumda | — | Yok; `LifeMeter`'a "şu saate kadar sınırsız" eklenir | Hafta sonu zincirinin başında 30 dakika ve son ödülde. `GAME_DESIGN.md` §19 zaten "etkinlik ödülü olarak sınırsız can süreleri" diyor (FACT) |
| **Kozmetik**: çerçeve süsü, isim levhası, oda duvar rengi | Kalıcı, görünür; ekonomiyi bozmaz | Zorluğa etkisi yok | Çizim ister; insan yok | Son ödül |
| **Yan koleksiyon parçası / eskiz** | Oyunu metaya bağlar | Belli bir parça; rastgele paket yok | Yan koleksiyon kararı var, yapılmadı; parça fiyatı (300) yer tutucu (§25) | Son ödül; karar §7 |
| **Restoratör XP'si** | Rütbe ve unvan | Onaylı kaynak (2026-10-24) | XP sıradaki sürümde | §5.3 |

### 5.2 Can hakkında açıkça

- **Bugün can neredeyse hiç bitmiyor.** 10 dakikalık oturumda 5 can hiçbir oturumu bitirmiyor (FACT, `GAME_DESIGN.md` §25). Kayıtlı iki gerçek oyuncu oturum başına yaklaşık 5 deneme yapıyor (FACT, §25). Bu yüzden "N saat sınırsız can" bugün neredeyse hiçbir şey kazandırmaz. Dolu sayaca verilen can da boşa gider.
- **Liderlerde can neden değerli.** Kayıp sık ve sayaç boşalıyor. Royal Match ve Candy Crush'ta 5 can var ve 30 dakikada bir doluyor [C8, C10] (FACT). Royal Match'te tam dolum 900 altın [C3] (REPORTED). Orada sınırsız can çoğunlukla bir etkinlik başlarken verilen "şimdi doya doya oyna" iznidir: King's Cup başlayınca 30 dakika [C1] (REPORTED, doğrulandı).
- **Bizde can ödülünü değerli yapacak durumlar:**
  1. **Uzun oturum.** 60 dakikalık oturumların %63'ü canla bitiyor (FACT, §25, `sessions-60-minutes`). Hafta sonu uzun oynayan için zincirin başında 30 dakikalık sınırsız can gerçek bir izindir.
  2. **Sayacın üstünde yedek can.** Sayaç doluyken saklanan can. Canlar bitmediği için değeri yine düşük; `LifeMeter` değişikliği ister. Önerilmez.
  3. **Can kuralını değiştirmek** (daha az can). 5 can 2026-10-13'te yeniden onaylandı. Önerilmez.
- **Öneri:** can yalnız **sınırsız can süresi** olarak verilsin, az ve yerinde: hafta sonu zincirinin başlangıç hediyesi ve son ödül. Her 3–4 basamaktaki süreli ödül ise **süreli ücretsiz booster** olsun; onu herkes bir sonraki bölümde hisseder.
- İki ödül de `Reward`'a yeni bir alan ya da yeni bir ödül türü ister. Bu bir kamu sözleşmesi değişikliği; karar §7.

### 5.3 Restoratör XP'si

- **Onaylı kaynaklar (proje sahibi, 2026-10-24):** bölüm kazanmak, zor bölüm, ilk denemede kazanmak, tablo, oda ve mini oyunlar. Kalan hamle ve kullanılan booster XP vermez. Rapor §4.6'daki tablo ara oyuna +10 XP diyor.
- **Öneri:** kazanılan her mini oyun aşaması +10 XP; bir oyunun bütün aşamaları bitince +20 XP (ESTIMATE).
- **Aynı ilke mini oyunda da geçerli:** XP aşamayı geçmekten gelir. Kaç hamlede ya da kaç tahminde geçildiği XP vermez; bu, "kalan hamle XP vermez" kuralının eşidir. Usta sayısı yalnız gösterilir.
- **Ölçek:** bir Atölye Haftası 4 oyun × (6 × 10 + 20) = 320 XP. Ortalama bölüm yaklaşık 20 XP getiriyor (rapor §4.6, ESTIMATE); yani haftada yaklaşık 16 bölümlük XP. Rütbe hediyeleri booster olduğu için bu da simülatöre girer.

### 5.4 Örnek ödül merdiveni (6 aşama, ESTIMATE)

| Aşama | Ödül | Not |
|---|---|---|
| 1 | 1 booster | Hemen ilerleme hissi |
| 2 | Altın | |
| 3 | 15 dakika süreli ücretsiz booster | |
| 4 | 1 booster | |
| 5 | Altın | |
| 6 | Son ödül: kozmetik ya da yan koleksiyon parçası, artı altın | Türe göre değişir (§2.3) |
| Hafta sonu zincirinin başı | 30 dakika sınırsız can | Karar §7 |

- Merdiven sabittir, oyun başlamadan kartta görünür, herkese aynıdır. Royal Match'in bazı merdivenleri "performansa göre değişebilir" [C1] (REPORTED); bizde gizli ayar yok (§17).
- Liderlerin biçimi de böyle: Magic Cauldron 15, Balloon Rise 10 basamak; çoğu basamak bir ya da iki booster, altın ortada ve sonda, her 3–4 basamakta bir süreli ödül [C1] (REPORTED, doğrulamada basamak basamak tutuyor).
- **Miktarları simülatör belirler.** Simülatöre ilk deneme değeri olarak etkinlik başına toplam **150–250 altın** öneriyoruz (ESTIMATE). Ayda 8 etkinlikle bu 1.200–2.000 altın, yani bugünkü fiyatla 1,3–2,2 kez "+5 hamle" (900 altın, §25). Karşılaştırma: Magic Cauldron merdiveninde toplam 1.500, Balloon Rise'ta 4.500 altın [C1] (REPORTED); Royal Match'te bir tam can dolumu 900 altın [C3] (REPORTED).

### 5.5 Atölye jetonu

- Kazanılan her bölüm 1 jeton; Zor bölüm 2, Süper Zor 3 (öneri). Liderler zor bölümde daha çok jeton veriyor [C8] (FACT); Balloon Rise için de resmî sayfa böyle diyor [doğrulama C7]. Çarpanlar simülatör sorusu.
- Bir aşamayı açmak birkaç jeton ister; sayıyı simülatör seçer. **Hedef:** ortalama oyuncu (55. basamak), günde 3 oturumla, zincirin iki oyununu blok bitmeden bitirir; daha az oynayan ilkini bitirir (ESTIMATE).
- **Kaba hesap:** ortalama oyuncu günde yaklaşık 12 bölüm kazanıyor (100. bölüm 8. gün, FACT §25). Hafta içi bloğunda yaklaşık 50, hafta sonunda yaklaşık 37 kazanma. Bir zincir 12 aşama; aşama başına 3 jeton makul bir başlangıç (ESTIMATE).
- Günde bir aşama jetonsuz (öneri). Toon Blast'ın Surprise Calendar'ı günde bir bedava oyun veriyor [C12] (FACT).
- **Jeton satılmaz.** Royal Match Temmuz 2026'da ilk kez bir etkinliğin enerjisini doğrudan satmaya başladı [T11] (REPORTED). Bizde bu "harcama zorluğu değiştirmez" ilkesine ve parayla ilerleme sınırına takılır (ESTIMATE).
- Mini oyundan çıkmak jetonu yakmaz. Royal Match ve Royal Kingdom'da etkinlikten çıkan jetonunu kaybediyor [C8, C9] (FACT); 30 yaş üstü oyuncu için sert.
- Blok bitince kalan jeton gider; simülatör izin vermedikçe altına dönmez. Son 24 saatte çerçeve hatırlatır. "Son şans" ekranı liderlerin çoğunda var ama her yerde değil: Dragon Nest bitince kalan toplar kullanılamıyor [doğrulama C2].
- **Süre:** bir zincir 12 aşama × yaklaşık 2 dakika ≈ 24 dakika; hafta içi bloğunda günde yaklaşık 6 dakika (ESTIMATE). Bu, 10 dakikalık oturumun büyük bir payı. Simülatör ya oturumu uzatmalı ya da bölüm hızını düşürmeli. Hangisine inanacağımız bir karar (§7).

### 5.6 Simülatör kuralı

Her yeni ödül kaynağı yayından önce simülatörden geçer: altın, booster, süreli booster, can süresi ve rütbe hediyesi. Bakılacaklar:
- 100. bölüm 7–9. gün aralığında kalıyor mu (§25 hedefi)?
- Ortalama kese 2–8 kez "+5" aralığında mı (§25 denge politikası)?
- Mini oyun dakikaları nereden geliyor?

Gerekirse başka bir kaynak kısılır (rapor §5).

---

## 6. İlk seçim

| Sıra | Ne | Efor | Neden |
|---|---|---|---|
| 0 | **Ortak çerçeve** | L | Hiçbir mini oyun onsuz çalışmaz. Sonra her tür küçük bir iş olur |
| 1 | **Boya Tüpleri** | L | Proje sahibinin örneği (eşya sıralama). 2025'in en büyüyen türlerinden [T1]. Taşlar hazır. Her bölüm kurulumdan çözülür |
| 2 | **Pigment Tarifi** | S–M | En ucuzu. Liderlerin gerçekten oyunlarına koyduğu tür [C1, C8, T2]. İlk zincirin ikinci oyunu olabilir |
| 3 | **Takas Yapbozu** | S–M | Tema uyumu en yüksek. Yeni çizim yok; tablo kesme hazır. Jigsolitaire 2025'in liste hiti [T5] |
| 4 | **Depo Sıkışıklığı** | M–L | Temaya uyar. Zorluk kesin ölçülür. Renk oyunlarından farklı bir düşünme; zincirleri çeşitlendirir |
| 5 (isteğe bağlı) | Çerçeve Vidaları | L | İkinci sıralama türü; vida %8 büyüme [T1]. Tablonun ortaya çıktığı an |

**Kilometre taşları (ESTIMATE)**
- **A:** çerçeve + Boya Tüpleri + Pigment Tarifi. İlk Atölye Haftası tek zincirle açılır. Yaklaşık 4,5–7 arayüz aşaması.
- **B:** + Takas Yapbozu + Depo Sıkışıklığı. Tam Atölye Haftası: iki zincir, dört oyun. Yaklaşık 1,5–3,5 aşama daha.
- **C:** + Çerçeve Vidaları; sonra ikinci dalga: Depo Rafları, Katalog Taşları, Yaldız Çizgisi, Kayıp Eşyalar, Arşiv Kazısı.

A ile B birlikte yaklaşık **6–10 arayüz aşaması**. Çeviriler, simülatör işi ve telefon testleri buna dahil değil. Sıra: proje sahibinin 2026-10-24 kararıyla önce yatay ekran, sonra mini oyunlar.

**Neden Küratörün Sınavı ilk seçimde yok.** O yeni bir oyun değil, match-3 kipi; rapor onu sezon pasosuyla birlikte 2. aşamaya koyuyor. Sessiz haftaları doldurmak için iyi bir aday.

**Fikir çizimleri.** `docs/research/mini-games-2026-10/sketches.jpg`: beş türün ve ortak çerçevenin şeması, altta örnek ay. Yalnız oyunun kendi taşları, galeri parçaları, tabloları, yazı tipleri ve uygulama simgesinin paleti kullanıldı. Hepsinin üstünde "fikir çizimi" yazar. Son görünüş değildir: tüp, vida kutusu ve sandık çizimleri yeniden yapılacak.

---

## 7. Proje sahibinin kararları

1. **İlk seçim ve sırası** (§6): çerçeve, Boya Tüpleri, Pigment Tarifi, Takas Yapbozu, Depo Sıkışıklığı; sonra Çerçeve Vidaları. Öneri: evet.
2. **Takvim biçimi:** ayın 1. ve 3. haftası Atölye Haftası; hafta içi ve hafta sonu birer zincir; 2. ve 4. hafta sessiz; telefonun yerel saati. Öneri: evet.
3. **Zincir mi, aynı anda iki oyun mu?** Zincirde ekranda tek oyun olur ve ikincisi ilkini bitirince açılır. Öneri: zincir.
4. **Jeton:** kazanılan bölümden jeton (zor bölüm daha çok) ve günde bir bedava aşama; ya da yalnız günlük bedava oyun. Öneri: ikisi birlikte.
5. **Can ödülü:** yalnız sınırsız can süresi (hafta sonu zincirinin başında 30 dakika ve son ödülde); saklanan can yok. `Reward` ve `LifeMeter` değişir; bu bir kamu sözleşmesi. Öneri: evet, az.
6. **Süreli ücretsiz booster** yeni bir ödül türü olsun mu? Öneri: evet, üst üste en çok 60 dakika.
7. **Kayıt dosyası 6. sürüm** (`events` bloğu). Kalıcı şema değişikliği, eklemeli. Öneri: evet.
8. **Beşinci çubuk yeri "Etkinlikler" ve takvim simgesi.** `GAME_DESIGN.md` 1114. satır takvim simgelerinin tuvalden geldiğini söylüyor, ama depoda takvim çizimi yok. Simge tuvalden mi çıkarılsın, belge mi düzeltilsin?
9. **Mini oyunda parayla yardım** (fazladan tüp, ek geri alma) satılsın mı? Öneri: hayır; geri alma herkese bedava ve sınırsız.
10. **Jeton parayla satılsın mı?** Öneri: hayır.
11. **Son ödül olarak yan koleksiyon parçası** verilsin mi? Bugün parçalar oyunla kazanılan altınla alınıyor (2026-10-14 kararı); bu ikinci bir yol olur. Öneri: evet, belli bir parça.
12. **Simülatörün süre varsayımı:** mini oyun dakikaları 10 dakikalık oturumdan mı yer (bölüm hızı düşer), yoksa oturumu mu uzatır? Öneri: önce "oturumdan yer" diye ölçülsün; 100. bölüm 9. günü geçerse ödül ya da aşama sayısı azaltılsın.
13. **Açılış bölümü 35** olsun mu? Öneri: evet.
14. **Yerel bildirim:** Atölye Haftası başında tek bildirim, yalnız oyuncu isterse. Öneri: evet, ilk sürümden sonra.

---

## 8. Kaynaklar

Bütün web kaynakları 2026-10-24'te açıldı (araştırmacılar ve doğrulama). Numaralar notlardakiyle aynı: T = `types.md`, C = `calendar.md`, W = `build.md`. Y numaraları bu belge için 2026-10-24'te eklendi.

**Pazar, analiz ve oyun rehberleri**

| No | Kaynak | Adres | Kaynağın tarihi |
|---|---|---|---|
| T1 | Naavik, "What Leading Match-3 & Merge Games Do Differently" (Oindrila Mandal), Sensor Tower verisi | https://naavik.co/digest/what-leading-match-3-and-merge-games-do-differently/ | 2026-01-18 |
| T2 | Naavik, "Why Minigames Mean Major Business" (Oindrila Mandal) | https://naavik.co/digest/why-minigames-mean-major-business/ | 2025-08-17 |
| T3 | Liftoff, "2025 Casual Gaming Apps Report" (GameRefinery verisi, Şubat 2024 – Şubat 2025) | https://liftoff.ai/2025-casual-gaming-apps-report/ | yaklaşık 2025-04 (sayfada yok) |
| T4 | Nextbiggames (YouTube), "TOP 28 Highest Grossing HybridCasual Puzzles 2025"; veri kaynağı yazılmamış, zayıf | https://www.youtube.com/watch?v=wJ_qXZTyX8A | 2026-01-15 |
| T5 | Sensor Tower blog, "Top 5 Puzzle Games in the US: Q4 2025"; yazar bir yapay zekâ aracı; oyunları sıralamıyor | https://sensortower.com/blog/2025-q4-unified-top-5-puzzle-games-units-us-64bc1644e1714cfff1e615fc | 2026-01 |
| T6 | Sensor Tower blog, "Top 5 Puzzle Games Performance in the US: Q1 2025" | https://sensortower.com/blog/2025-q1-unified-top-5-puzzle%20games-revenue-us-64bc1644e1714cfff1e615fc | 2025-04 |
| T7 | Sensor Tower blog, "What Royal Match's calendar teaches about live ops in 2026" (Bryan Isagholian) | https://sensortower.com/blog/what-royal-matchs-calendar-teaches-about-live-ops-in-2026 | 2026-05 |
| T8 | Gamigion, "New event in Royal Match: Puzzle Break" (Anton Slashcev) | https://www.gamigion.com/new-event-in-royal-match-puzzle-break/ | 20 Haziran (yıl sayfada yok; 2025 tahmini) |
| T9 | Gamigion, "Royal Match runs mini-games as events!" | https://www.gamigion.com/royal-match-runs-mini-games-as-events/ | tarihsiz |
| T10 | Gamigion, "Smash Fest!: Casual LiveOps Turned Into Genre-Wide Trend" (Andrei Zubov) | https://www.gamigion.com/smash-fest-casual-liveops-turned-into-genre-wide-trend/ | 20 Ağustos (2026 tahmini) |
| T11 | Gamigion, "Royal Match launched its first Expedition event!" (Oxana Fomina), Sensor Tower verisi | https://www.gamigion.com/royal-match-launched-its-first-expedition-event/ | 15 Eylül (2026 tahmini) |
| T12 | Gamigion, "Monopoly GO Top Revenue-Driving Events" (Anton Slashcev) | https://www.gamigion.com/monopoly-go-top-revenue-driving-events/ | 21 Şubat (2025 tahmini) |
| T13 | FoxData, "Royal Match's Mini-Games — The Secret Driving Its Global Success in 2025" | https://foxdata.com/en/blogs/royal-matchs-mini-games-the-secret-driving-its-global-success-in-2025/ | 2025-09-30, güncelleme 2026-01-19 |
| T14 | Old Cynic, "Royal Match: Dragon Nest & Magic Tokens" | https://oldcynic.com/royal-match-dragon-nest-magic-tokens-balls-guide-tips | 2024-10-09, güncelleme 2025-09-05 |
| T15 | TheGamer, "Monopoly Go: Complete Juggle Jam Guide" (Gabrielle Castania) | https://www.thegamer.com/monopoly-go-juggle-jam-tutorial-rewards-carnival/ | 2025-01-21 |
| T16 | ASA (İngiltere reklam kurulu) kararı, PLR Worldwide Sales Ltd (Homescapes, Gardenscapes reklamları); rakamlar Playrix'in kendi beyanı. `types.md`'de T16 BBC haberiydi; doğrulama kararın kendisini açtı | https://www.asa.org.uk/rulings/plr-worldwide-sales-ltd-g20-1061644-plr-worldwide-sales-ltd.html | 2020-09-30 |
| T19 | Sherwood News, "Mobile game ads" (Max Knoblauch) | https://sherwood.news/business/mobile-game-ads-industry-fake-misleading | 2024-06-14 |
| T21 | Pocket Gamer, "Fishdom minigames solutions" (Allison Van Oirschot) | https://www.pocketgamer.com/fishdom/minigames-solutions | 2022-11-29 |
| T23 | Deconstructor of Fun, "Tile Match: The new Match-3 or the new Merge?" | https://www.deconstructoroffun.com/blog/2023/9/30/tile-match-the-new-match-3-or-the-new-merge | 2023-10-02 |
| T25 | Apple iTunes Search API, ABD mağazası (satıcı, çıkış tarihi, puanlama sayısı, mağaza metni) | örnek: https://itunes.apple.com/search?term=water+sort+puzzle&entity=software&country=us | 2026-10-24'te okundu |
| T27 | Google Play, Goods Puzzle: Sort Challenge (arama özeti) | https://play.google.com/store/apps/details?id=com.fc.goods.sort.matching.puzzle.triplemaster | canlı sayfa |
| T30 | ABD App Store, Color Block Jam (Rollic Games) | https://apps.apple.com/us/app/color-block-jam/id6504332779 | canlı sayfa |
| T31 | Business of Apps, "Puzzle Games Revenue and Usage Statistics (2026)", Sensor Tower verisi (arama özeti) | https://www.businessofapps.com/data/puzzle-games-market/ | 2026 sayfası, 2022 verisi |
| T35 | Old Cynic, "Royal Match: Train Journey" | https://oldcynic.com/royal-match-train-journey-guide | 2024-12-21, güncelleme 2025-04-04 |
| Y1 | ABD App Store, Unblock Me (Kiragames), iTunes arama servisiyle | https://apps.apple.com/us/app/unblock-me/id315019111 | 2026-10-24'te okundu |
| Y2 | ABD App Store, Tap Away 3D (Popcore), iTunes arama servisiyle | https://apps.apple.com/us/app/tap-away-3d/id1568058543 | 2026-10-24'te okundu |
| Y3 | ABD App Store, Arrows – Puzzle Escape (Lessmore), iTunes arama servisiyle | https://apps.apple.com/us/app/arrows-puzzle-escape/id6748397500 | 2026-10-24'te okundu |

**Oyunların resmî yardım sayfaları ve wikiler**

| No | Kaynak | Adres | Tarih |
|---|---|---|---|
| C1 | Royal Match Wiki (Fandom), etkinlik sayfaları, wikinin açık API'siyle (Magic Cauldron, Balloon Rise, Pinata Party, Hidden Temple, King's Cup, Lightning Rush, Dragon Nest) | https://royalmatch.fandom.com/wiki/%F0%9F%94%AE_Magic_Cauldron (ve aynı wikideki öbür sayfalar) | sayfa düzenleme tarihleri farklı |
| C3 | Royal Match Wiki, Lives | https://royalmatch.fandom.com/wiki/Lives | 2026-07-07 |
| C7 | Google Play editörlüğü, "Here's how Events work in Royal Match" | https://play.google.com/store/apps/editorial?id=mc_games_editorialevergreen_post_install_royal_match_events_now_fcp&hl=en_US | tarihsiz |
| C8 | Dream Games, Royal Match yardım merkezi (Hidden Temple, Magic Cauldron, Puzzle Break, Balloon Rise, Lives) | https://dreamgames.helpshift.com/hc/en/3-royal-match/ | canlı |
| C9 | Dream Games, Royal Kingdom yardım merkezi (Events, Magic Carnival) | https://dreamgames.helpshift.com/hc/en/6-royal-kingdom-1676903479/ | canlı |
| C10 | King, Candy Crush Saga yardım merkezi (süreli booster'lar 2026-06-06; sınırsız can 2026-05-29; uzun can bekleme 2026-08-10; Yeti's Draw 2026-08-07) | https://candycrush.zendesk.com/hc/en-us/articles/360002352858 (ve aynı merkezdeki öbür maddeler) | maddelerin yanında |
| C11 | Google Play editörlüğü, "Make the most of events in Candy Crush Saga" | https://play.google.com/store/apps/editorial?id=mc_editorialevergreen_post_install_candy_crush_saga_events_fcp&hl=en_US | tarihsiz |
| C12 | Peak, Toon Blast yardım merkezi (Surprise Calendar, Rescue Mission, Cannon Fest, Wheel of Luck) | https://peakgames.helpshift.com/hc/en/4-toon-blast/ | canlı |
| C13 | Homescapes Wiki (Fandom), Events, wikinin API'siyle | https://homescapes.fandom.com/wiki/Events | canlı |
| C15 | MMO Culture, "MONOPOLY GO Events Today" (José Garcia) | https://mmoculture.com/tips-guides/monopoly-go-events/ | 2026-09-22, güncelleme 2026-10-01 |

**Platform belgeleri**

| No | Kaynak | Adres |
|---|---|---|
| C21 | Android Developers, `SystemClock` | https://developer.android.com/reference/android/os/SystemClock |
| C22 | Android Developers, `Settings.Global` (`AUTO_TIME`, `BOOT_COUNT`) | https://developer.android.com/reference/android/provider/Settings.Global |
| C23 | Apple Developer, `significantTimeChangeNotification` | https://developer.apple.com/documentation/uikit/uiapplication/significanttimechangenotification |
| C24 | Apple Developer, `ProcessInfo.systemUptime` | https://developer.apple.com/documentation/foundation/processinfo/systemuptime |
| C25 | Unity Discussions, "How can I detect the passage of time even when the game is closed?" (2025-10-29 – 2025-11-03) | https://discussions.unity.com/t/how-can-i-detect-the-passage-of-time-even-when-the-game-is-closed/1692831 |
| C26 | Apple Developer, In-App Events | https://developer.apple.com/app-store/in-app-events/ |
| C27 | Play Console Help, Promotional content | https://support.google.com/googleplay/android-developer/answer/12932541?hl=en |
| C28 | Firebase, Remote Config yükleme yöntemleri | https://firebase.google.com/docs/remote-config/loading |
| C29 | RFC 9110, HTTP Semantics, §6.6.1 Date | https://www.rfc-editor.org/rfc/rfc9110.html#field.date |
| C30 | Android Developers, bildirim izni | https://developer.android.com/develop/ui/views/notifications/notification-permission |
| C31 | Unity, Mobile Notifications 2.4 | https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/manual/index.html |

**Algoritmalar** (doğrulamada yeniden açılmadı)

| No | Kaynak | Adres | Tarih |
|---|---|---|---|
| W1 | Ito ve diğerleri, "Sorting Balls and Water: Equivalence and Computational Complexity", arXiv:2202.09495 | https://arxiv.org/abs/2202.09495 | 2022-02-19 |
| W2 | octo/watersort (A* çözücü; örnek çalıştırma 20.637 durum) | https://pkg.go.dev/github.com/octo/watersort | 2022-06-06 |
| W3 | Wikipedia, "Rush Hour (puzzle)" | https://en.wikipedia.org/wiki/Rush_Hour_(puzzle) | canlı sayfa |
| W4 | Wikipedia, "15 puzzle" | https://en.wikipedia.org/wiki/15_puzzle | canlı sayfa |

**Bu depo (FACT)**
- `docs/GAME_DESIGN.md` §17 (etik korkuluklar), §19 (ürün kararları, can kuralı, haritada örtülü tablolar), §20 (veri ve yayın), §25 (ekonomi simülatörü: 10 ve 60 dakikalık oturum sonuçları, denge politikası), 1114. satır (takvim simgeleri).
- `docs/DEVLOG.md` 175. satır (APK 283 MB).
- `docs/research/monetization-and-retention.md` §4.6 (XP), §5 (mini oyunlar), §6 (meta), §7 (etkinlikler, sunucu seçenekleri), §9 (kararlar).
- Kod: `unity/Assets/Scripts/Flow/WallClock.cs`, `Flow/GameSession.cs`, `Flow/WalletLedger.cs`, `Flow/ProgressDocument.cs`, `Flow/AttemptLedger.cs`, `Screens/Destination.cs`, `Screens/EntryStage.cs`, `Hall/GiftFrame.cs`, `src/Puzzle.Meta/Rewards.cs`, `LifeMeter.cs`, `DayStamp.cs`, `src/Puzzle.Economy/Simulating/Decisions.cs`, `tools/unity/sync-engine.js`, `words/tr.json`; `unity/ProjectSettings/ProjectSettings.asset` (en düşük Android API 26). Ayrıntı `build.md` ve `verification.md` §4'te.

**Bu araştırmanın notları** (`docs/research/mini-games-2026-10/`): `types.md`, `calendar.md`, `build.md`, `verification.md`, `sketches.jpg`, `sketches.py`.

**Ulaşılamayanlar.** NamuWiki (403), BBC, Reddit ve Facebook doğrulamada açılamadı. Yalnız bunlara dayanan iddialar kullanılmadı ya da zayıf diye işaretlendi (örneğin Royal Match Train Journey'nin "ayın 3. cuması" başlaması yalnız NamuWiki'nin arama özetinde var [doğrulama C5]).
