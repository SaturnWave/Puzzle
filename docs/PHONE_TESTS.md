# Telefon Test Planı ve Bilinen Açıklar

Bu belge iki şeyi tek yerde tutar: **bilinen açıklar** (bildiğimiz ama henüz kapatılmamış ya da bilerek kabul edilmiş eksikler) ve **telefon testleri** (yalnızca gerçek bir telefonda kanıtlanabilecek davranışlar). Bilgisayardaki testler (C# testleri, betik testleri, Unity denetimleri) her değişiklikte koşuyor; telefon testleri ise kullanıcının kendi telefonunda, zamanı geldiğinde, hepsi birden koşulacak.

**Cihaz:** Pixel 9 Pro Fold. Dış ekran 1080×2424, iç ekran 2076×2152. Oyunun Unity denetimleri bu iki boyutta ve 1080×2340'ta görüntü alıyor.

**Kayıt kuralı:** her testin sonucu bu belgedeki tabloya işlenir. Durum değerleri: *Bekliyor*, *Geçti*, *Kaldı*, *Ertelendi*. Tarih ve kısa not eklenir. Kalan bir test için ekran görüntüsü ya da ekran kaydı ve `adb logcat` çıktısı saklanır.

## 1. Tam test turu (zamanı gelince, bu sırayla)

| Adım | Ne | Komut ya da yol |
|---|---|---|
| 1 | C# testleri | `dotnet test tests/Puzzle.Core.Tests/Puzzle.Core.Tests.csproj -c Debug` |
| 2 | Betik testleri | `node --test tools/unity/economy-pack.test.mjs` |
| 3 | Motoru ve ayarları Unity'ye al | `node tools/unity/sync-engine.js` |
| 4 | Unity denetimleri | `ProjectSetup`, `EngineSmokeTest`, `EconomyCheck`, `FinaleCheck`, `PlayModeCheck`, `TouchWhileMovingCheck`, `CollectionMemoryCheck`, `ScreenShots -screenWalk` (komutlar: GAME_DESIGN §23) |
| 5 | Ekonomi | `puzzle economy run economy/default.json`: ortalama oyuncu 100. bölüme 7–9. günde (§25) |
| 6 | APK | `AndroidBuild.Run` → `unity/Build/Android/puzzle.apk` |
| 7 | Telefona kur | USB hata ayıklama telefonda onaylı; `adb install -r unity/Build/Android/puzzle.apk` |
| 8 | Telefon testleri | Aşağıdaki T listesi, gruplar sırasıyla |

Ön koşul sütunu "aşama N" diyorsa, o test arayüz planının o aşaması bitince anlam kazanır; ondan önce *Ertelendi* yazılır.

## 2. Bilinen açıklar

| Kod | Açık | Nereden | Etki | Plan |
|---|---|---|---|---|
| A01 | Telefonun saati ileri alınırsa canlar dolar; 12. aşamadan beri günün reklam hakları ve hediyesi de yenilenir. | Kurallar, §23 | Düşük; sunucusuz bir oyunda kapanmaz | Kabul edildi; T32 davranışı gözler, geri alınan saati T167 |
| A02 | Can kaybedilirken saat geri alınmışsa, saat düzeltilince bir sonraki canın zamanı geçmişte kalır ve can hemen döner. | Kurallar, §23 | Düşük | Kabul edildi; T33 |
| A03 | Kurallar (`Puzzle.Meta`) ve kayıt dosyası telefonda ve IL2CPP derlemesinde hiç çalıştırılmadı; yalnızca Editor'da ve .NET'te. | 1. aşama | Orta: IL2CPP'de yansıma ve JSON okuma farklı davranabilir | T01, T20 |
| A04 | Bitiş şöleninin telefonda kare hızı ve gerçek parmakla atlanması ölçülmedi. | §23 | Orta | T12 |
| A05 | 10 sütunlu tahtada hücre yaklaşık 36 pt; önerilen 44 pt dokunma hedefinin altında. | §2, §22 | Orta: yanlış dokunuş | T10 |
| A06 | Bantlı bir parça, bandının çıktığı gösterilmeden hareket edebiliyor. | Oturum 20 | Düşük, görsel | Kullanıcının ayrı oturumunda ele alınıyor; sonucu henüz bu dalda yok |
| A07 | Booster'lı bir deneme kayıttan birebir yeniden oynatılamıyordu. **Kapandı (2026-10-14, aşama 3):** eylemler sırasıyla kaydediliyor ve yeniden oynatılıyor. | §18 | — | Kapandı; telefonda T81–T84 |
| A08 | Reklamlar yalnızca canlandırma; gerçek para paketleri satışta değil. | §23 | Bilinçli | Yayın hazırlığında |
| A09 | Bölüm içi dört booster'ın etkisi ölçülmedi; fiyatları yer tutucu. Hoş geldin hediyesinin 12 bölüm içi booster'ı simülasyonda etkisiz sayılıyor. | §25 | Orta | Bot kuralı yazılınca |
| A10 | Ses ve titreşim yoktu. **Kapandı (2026-10-16, aşama 14):** yer tutucu sesler ve kısa titreşim tıkları; müzik yok. | §23 | — | T190–T201 |
| A11 | Bölüm geçişinde duvar, plaketler ve çerçeve yeniden kuruluyor; süresi telefonda ölçülmedi. | Oturum 16 | Düşük | T15 |
| A12 | 40 parçalı otomatik tamamlama, geri tuşu ve uzun oynama telefonda denenmedi. | Oturum 17 | Orta | T41, T42, T13 |
| A13 | İç (kare) ekran, ucuz telefonlar ve uygulamayı arka plana alıp dönme denenmedi. | Oturum 16 | Orta | T02, T14 |
| A14 | 100 tablonun bellek ve yükleme süresi telefonda ölçülmedi (her biri yaklaşık 1 MB, uzun kenarı yaklaşık 1920 piksel). 2026-10-24'ten beri koleksiyon gezilirken bellekte en çok 20 resim kalıyor (A42/4); telefonda ölçülecek. | Müze | Orta: bellek | T40, T420 |
| A15 | Beyaz yazının alt çevre çizgisi tasarım çizimlerindekinden ince; bölüm başlığı ahşap üstünde soluk. | §23 | Görsel, küçük | Ekranlar aşamasında |
| A16 | Web oyuncu sayfası gerçek telefonda, Safari'de ve ekran okuyucuyla denenmedi. | Oturum 13 | Herkese açık test için | T70 |
| A17 | Bir sunucu testi (`PublicModeTests`, istek seli) bütün takım birlikte koşarken zamanlamaya takılabiliyor; tek başına geçiyor. | Oturum 21 | Telefonla ilgisi yok | Ayrı iş |
| A18 | `generate --repair` 899. bölümü her seferinde yeniden üretiyor. | Oturum 12 | Zararsız | — |
| A19 | Can denetimi yoktu. **Salondan kapandı (2026-10-14, aşama 7):** canı bitmiş oyuncu "Bölüm N" ile bölüm açamıyor. Bölüm başı kartı 8. aşamada gelecek. | Aşama 2 | Düşük | T93, T94 |
| A20 | Cüzdan oyunda görünmüyordu. **Kapandı (2026-10-14, aşama 7):** altın ve canlar müze sayfalarında; can bitince bölüm açılmıyor. | Aşama 2 | — | T90–T97 |
| A23 | Telefondaki deneme dosyası sınırsız büyür (deneme başına yaklaşık 1,1 KB). **Kısmen kapandı (2026-10-15, aşama 13):** Gizlilik ve veri kartında "Kayıtları sil" var; oyuncu silmedikçe dosya büyümeye devam eder. Bölüm ortasında kapatılan oyun kayıt bırakmaz. | Aşama 3 | Düşük | T182, T183 |
| A24 | Booster yazısı 9:16 telefonda uzun tahtalarda ve katlanır telefonun yatay iç ekranında en alt sıranın bir kısmını örtüyor. Dokunuşlar geçiyor, ama ilk denemedeki yazı ilk hamleye kadar o taşları gizliyor. | Aşama 10 | Orta | T134; yer ayırmak tahtayı küçültür |
| A25 | Teklif ya da canlandırma reklamı açıkken uygulama kapatılırsa can gitmez ve kayıt yazılmaz. | Aşama 11 | Düşük | Planın bildiği açık |
| A26 | Ekonomi simülatörü tablo ödülünü onuncu kazanmada veriyor, oyun son parça yerleşince. Parçalar geç yerleştirilirse ödül oyunda simülatördekinden geç gelir. | Aşama 12 | Düşük; tahminin günü biraz kayabilir | Simülatör son parça anına geçirilecek ya da fark ölçülecek |
| A27 | Bu sürümden önce tamamlanmış tablolar için ödül verilmez; ödül yalnızca son parçayla gelir. | Aşama 12 | Yalnızca eski test kayıtları | Kabul edildi |
| A28 | APK INTERNET iznini taşıyordu (Unity'nin "Auto" ayarı; 8 Ekim APK'sında görüldü). **Kapandı (2026-10-16):** proje sahibinin kararıyla izin paketten çıkarıldı. 16 Ekim APK'sında `aapt` ile doğrulandı: paket yalnızca VIBRATE iznini istiyor. | Aşama 13 | — | T186 |
| A29 | Android yedeklemesi açık (`allowBackup` yazılmamış): oyunun dosyaları telefonun Google yedeğine katılabilir. Gizlilik metni bunu söylüyor. | Aşama 13 | Düşük; telefon değişince ilerlemeyi de kurtarabilir | Kabul edildi (2026-10-16): yedekleme açık kalır |
| A30 | Yer tutucu sesler ad ve ölçüyle seçildi; hiçbiri kulakla dinlenmedi. Aynı tepe seviyesindeki iki ses farklı yükseklikte duyulabilir (tablo tamamlanmasının çanı en uzun çalanı). | Aşama 14 | Düşük | T191–T197; proje sahibinin kulağı |
| A31 | Titreşim Android'in eski "vibrator" servisini kullanıyor (Android 12'den beri yerini VibratorManager aldı, hâlâ çalışıyor); telefonda denenmedi. | Aşama 14 | Düşük | T194, T201 |
| A32 | APK 283 MB (16 Ekim; 8 Ekim'de 61 MB'tı). Fark 100 tablonun tam boy görüntüleri. Play Store'da tek paketin temel modülü en çok 200 MB olabilir. | APK, 2026-10-16 | Yayını engeller | Yayından önce: tabloları küçültmek ya da ayrı indirmek (Play Asset Delivery); telefonda bellek ve yükleme süresi A14 ile birlikte ölçülür |
| A33 | Test modunda cüzdan satırında kalbin üstündeki "∞" biraz gözlüğe benziyor (can kartında iyi okunuyor). Ayarlardaki test modu satırının işareti yer tutucu. | Test modu | Düşük | Çizilmiş bir ∞ işareti, proje sahibi isterse |
| A34 | Kaydırmada son sayfanın ötesine çekince sayfa esnerken kenarda duvar deseni olmayan düz bir şerit görünüyor (en çok genişliğin %15'i, bırakınca kaybolur). Katlanırın iç ekranında daha belirgin. | #3 | Düşük, görünüm | Sayfaların arkasında duran sabit bir duvar |
| A35 | İngilizce ve Korece çeviriler makine çevirisi; Korece tablo adlarının 78'i çeviri (22'si Wikidata'daki yerleşik ad). Kayıtta en emin olunmayan ifadeler: 하트 (can), 이동 (hamle), booster adları, ressam adlarının yanındaki "atfedilen", "Yaşlı/Genç". | #8 | Orta: yayın öncesi | Her dilin ana dili olan birinin gözden geçirmesi |
| A36 | İpucunun aynı çıkması .NET ve Mono'da gösterildi, telefonun IL2CPP derlemesinde değil (botun seçimi kesirli sayı kullanıyor). İpucunun telefonda düşünme süresi ölçülmedi. | #6 | Düşük | T298; telefonda kayıtlarla karşılaştırma |
| A37 | Öğretici: 3.–6. bölümlerin hiçbiri beşli sunmazsa beşli adımı bırakılır (taramada nadir). Öteki özel taşlar için adım yok. Köşe parçasının yerini gösteren pencere çerçevenin üstüne taşıyor. 1.–3. bölümlerin telefon kayıtları rehberli hamle yüzünden çoğunlukla yardımlı sayılır. | #5 | Düşük | T315, T321 |
| A38 | Booster tanıtımı: küçük ekranda (2076×2152) uzun basma ve dikey fırça hapları tahtanın kenar hücrelerinin üstüne biniyor. Kartın küçük tahtasında düşen taşlar bazen özel taş yapıyor; +3 hamle kartında yalnızca madalyon sayıyor. Uzun basma yazısını kaçıran ve 20. bölümü geçmiş oyuncu onu hiç görmez. Kartın küçük tahtası çizim sırası kaydırılarak çiziliyor; tahta maskesi yalnızca tahtanın çizim aralığını kesiyor. | #7 | Düşük | T336, T339, T344 |
| A39 | On üç dil: çevirilerin hepsi makine çevirisi, yayından önce ana dili olan biri okumalı. Bütün yedek atlaslar her dilde yüklenir (16,5 MiB doku). CJK atlasları 40 örnekleme boyutunda; büyük başlıklarda keskinlik telefonda bakılmalı. İspanyolca "MOVIMIENTOS" madalyon halkasına yakın. Dil kartında başka dillerin adlarındaki ortak Han karakterleri seçili dilin biçiminde çıkabilir. İspanyolca 18–20. odaların adları çeviri. | #8 | Orta | T355, T356, T359, T362 |
| A40 | Müzik: henüz kulakla dinlenmedi (seviye 0,4 ve Vorbis kalitesi 0,6 ölçüyle seçildi). Android ses odağı yönetilmiyor: başka bir uygulama çalarken oyun da çalar. Ayrı bir ses düzeyi kaydırıcısı yok. Suno bağlantısı kayıtta yok. | #9 | Orta | T373, T379 |
| A41 | Dünya haritası: ekran okuyucu bağlantısı yok (bütün oyun için ayrı iş). Uzun dönem adları plakada iki satır; 14. dönem karşılaştırma için bilerek tek küçük satır (T400'de 12. ve 14. dönemin plaketine bakın). 6. dönem en yakın yakınlıkla açılıyor. Kare iç ekranda kart haritanın çoğunu kaplıyor. İğne resimleri döneme ilk gelişte yükleniyor; telefonda kısa takılma olabilir. 41 tablonun yeri yalnızca ressamın çalıştığı yer. | #4 | Orta | T391, T401, T403 |
| A42 | 23 Ekim otomatik test turunun bulguları: (1) salonda Android geri tuşu hiçbir şey yapmıyor (T43); (2) tam ekran kipte kenardan tek geri hareketi önce sistem çubuklarını gösteriyor, ikincisi geri götürüyor (T235; Android'in tam ekran davranışı, gerçek parmakla denenecek); (3) ayarlar kartında bir satırın üstünde yatay kaydırma satıra basılmış sayılıyor (dil kartı açıldı, Titreşim kapandı); (4) koleksiyonda 20 oda gezilince bellek 401 MB'tan 649 MB'a çıkıyor, çökme yok (A14 ile ilgili); (5) her açılışta zararsız bir "AssetPackManager bulunamadı" günlük satırı (A32).  **Düzeltildi (2026-10-23, cbf5202):** (1) salonda geri tuşu ve geri hareketi çıkış kartını getiriyor (T410–T412); (3) bir düğmenin üstünde 10 noktadan uzun kaydırma hiçbir şeye basmıyor (T413–T414). **Düzeltildi (2026-10-24, 8960aa8):** (4) oda kaldırılınca resimleri de bırakılıyor; bellekte gösterilen odalar ve son bırakılan 10 resim kalıyor, Editör'de 20 oda gezilince aynı anda en çok 20 resim (önce 100; `CollectionMemoryCheck`, T420–T425). (2) ve (5) açık. | Arayüz | Orta | T43, T235, T237, T40, T420 |
| A43 | Yan çevirme (2026-10-24) cihazda denenmedi: iPad'de dönme, ters dik, Pencereli Uygulamalar ve Stage Manager, sol üstteki pencere düğmeleri ile geri düğmesi; Android tablette yatay açılışta ilk karenin dik görünüp dönmesi; Fold'da yan tutarken katlama. Tahtanın gerçek yatay düzeni (seçenek B) yapılmadı: iPad'de A denendikten sonra konuşulacak. **2026-10-24 (inceleme):** paket dik açılıyor ve derleme bunu denetliyor (`ManifestOrientation`); `RotationCheck` Fold'u iki yana da çeviriyor (kamera solda ve sağda); oyun ekranı ve güvenli alanı günlüğe yazıyor, yan paylar cihazda oradan okunacak. | Yan çevirme | Orta | T450–T465 |
| A44 | Açılış adımlarından biri hata verirse giriş sayfası "Oyun açılamadı; yeniden başlatın" diyor, çubuk duruyor, geri tuşu çıkış kartını indiriyor (2026-10-24). Telefonda denenemez (bozuk bir paket gerekir); Editör'de `ScreenShots -screens entry` bu hâli her ekranda yürütüp görüntülüyor. Hatanın kendisi günlükte; oyuncuya ayrıntı gösterilmiyor. | İnceleme, 2026-10-24 | Düşük | Kabul edildi; Editör denetimi |
| A22 | Uzun dönem adları koleksiyon plaketinde 6 puntoya düşmüştü. **Düzeltildi (2026-10-14):** uzun ad iki satıra bölünüyor (en çok 10 punto). Harita uzun adı tek satırda küçültüyor. | Müze, 2026-10-14 | Okunurluk | T44 |
| A21 | Kayıt dosyası bozulursa cüzdan da kaybolur (dosya kenara alınır, oyun yeni oyuncu gibi açılır). Katman 3'ten eski bir sürüm, yeni dosyayı yalnızca bellekte oynar, üzerine yazmaz. | Aşama 2 | Düşük | T22 |

## 3. Telefon testleri

### A. Kurulum ve açılış

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T01 | APK kurulur, açılır | APK'yı kur, dış ekranda aç | Salon açılır; günlükte oyun hatası yok | — | Bekliyor |
| T02 | İç ekran | Telefonu aç, oyunu iç ekranda kullan; açıkken ekran değiştir | Düzen bozulmadan yeniden kurulur; oyun kaldığı yerden sürer | — | Bekliyor |
| T03 | Hoş geldin hediyesi | Uygulamanın verisini sil, ilk kez aç | Cüzdanda 200 altın ve her booster'dan 3; 5 can | Aşama 2 (görünmesi: cüzdan ekranı) | Bekliyor |
| T04 | Eski kayıtla açılış | Eski sürümün kaydı varken yeni APK'yı üstüne kur | İlerleme kaybolmaz; cüzdan hoş geldin hediyesiyle ve dolu canla başlar | Aşama 2 | Bekliyor |

### B. Oynanış

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T10 | Dokunma hassasiyeti | 10 sütunlu bir bölümde 30 hamle sürükleyerek, 30 hamle iki dokunuşla oyna; yanlış taşa gidenleri say | Yanlış dokunuş yüzde 5'in altında | — | Bekliyor |
| T11 | Hareket sürerken dokunma | Taşlar düşerken yeni hamle dene | Hamle kuyruğa alınmaz ya da güvenle reddedilir; tahta bozulmaz | — | Bekliyor |
| T12 | Bitiş şöleni | Kalan hamleyle bir bölüm kazan; bir kez sonuna kadar izle, bir kez dokunarak, bir kez ikinci parmakla, bir kez geri tuşuyla atla | Akıcı (gözle takılma yok); 5,5 saniyeyi geçmez; her atlamada kart gelir | — | Bekliyor |
| T13 | Uzun oynama | 30 dakika aralıksız oyna | Isınma ve yavaşlama yok; pil tüketimi not edilir | — | Bekliyor |
| T14 | Arka plana alma | Hamle ortasında, şölen sırasında ve sonuç kartında uygulamadan çık, geri dön | Oyun kaldığı yerden sürer; şölen biter ya da atlanmış sayılır; kazanım kaybolmaz | — | Bekliyor |
| T15 | Bölüm geçişi | Bir bölümden çıkıp sonrakine gir; süreyi ölç | 1 saniyenin altında | — | Bekliyor |

### C. Kayıt ve süreklilik

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T20 | Kapat-aç | Birkaç bölüm oyna, altın kazan, uygulamayı kapat (son uygulamalardan kaydırarak), aç | İlerleme, altın, booster'lar ve canlar aynı | Aşama 2 | Bekliyor |
| T21 | Kazanırken kapanma | Kazandıran hamleden hemen sonra, şölen sürerken uygulamayı kapat | Açınca bölüm kazanılmış ve altını verilmiş | Aşama 2 | Bekliyor |
| T22 | Bozuk kayıt | `adb` ile kayıt dosyasını boz, uygulamayı aç | Bozuk dosya kenara alınır; oyun yeni oyuncu gibi açılır, çökmez | Aşama 2 | Bekliyor |

### C2. Kayıt dosyasını okuyarak (cüzdan ekranı gelmeden)

Dosya: `adb shell cat /sdcard/Android/data/com.saturnwave.puzzle/files/progress.json` (izin verilmezse `adb shell run-as com.saturnwave.puzzle cat files/progress.json`).

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T23 | Eski kayıt yeni katmana geçer | Eski bir APK'yla (diskteki derleme 2026-10-08 tarihli) en az bir bölüm kazan, bir parça yerleştir; yeni APK'yı üstüne kur; bir hamle oyna, sonra hamle yapmadan çıkmadan, yuvarlak düğmeyle bölümden çık; dosyayı oku | Salon aynı bölüm ve parçalarla açılır; dosya `"version":3` ile başlar; cüzdanda 200 altın, her booster'dan 3, `"livesShort":1` (hamleden sonra çıkmak bir can) | Aşama 2 | Bekliyor |
| T24 | Kazanç ve altını kazandıran hamlede yazılır | Dosyadaki `coins` değerini not et; hamle kalmışken bir bölüm kazan; şölenin roketleri başlar başlamaz uygulamayı son uygulamalardan kaydırarak kapat; aç ve dosyayı oku | Salon sonraki bölümde açılır; `levelsWon` bir arttı; `coins` kalan her hamle için 4 arttı | Aşama 2 | Bekliyor |
| T25 | Kayıp bir can götürür, 30 dakikalık saati başlatır | Can doluyken (`"livesShort":0`) hamlesi biterek bir bölüm kaybet; kayıp kartı gelir gelmez dosyayı ve telefonun saatini (`adb shell date +%s`) oku | `"livesShort":1`; `nextLifeAtUnixSeconds` kaybettiren hamlenin anı artı 1800 (birkaç saniye içinde). Yarım saat içinde ikinci kayıp: 2, aynı `nextLifeAtUnixSeconds` | Aşama 2 | Bekliyor |
| T26 | Hamlesiz çıkış ve yarıda kapatma ücretsiz | Bir bölüm aç, hamle yapmadan yuvarlak düğmeyle müzeye dön, dosyayı oku. Sonra bir bölümde birkaç hamle oyna, uygulamayı kaydırarak kapat, aç, dosyayı oku | İkisi de `livesShort`'u değiştirmez; ikincisi yalnızca denemeyi sayar (`attemptsOnLevel` bir artar), oyun salonda açılır | Aşama 2 | Bekliyor |
| T27 | Can bitince | 30 dakika içinde beş bölüm kaybet, sonra altıncıyı açmayı dene | Dosya `"livesShort":5`; 7. aşamadan beri altıncı bölüm açılmaz, can kartı gelir (T93) | Aşama 2 ve 7 | Bekliyor |
| T28 | Kaydedilen an yeniden başlatmadan sağ çıkar | Bir kayıptan sonra `livesShort` ve `nextLifeAtUnixSeconds`'u not et; `adb shell am force-stop com.saturnwave.puzzle` ile durdur, aç; yarım saat içinde bir bölüm kazan, dosyayı oku | `livesShort` ve `nextLifeAtUnixSeconds` öncekiyle birebir aynı; `coins` ve `levelsWon` kazançla arttı | Aşama 2 | Bekliyor |

### C3. Deneme kaydı ve cihaz kimliği (aşama 3)

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T80 | Cihaz kimliği | Yeni APK'yı kur, oyunu bir kez aç; `adb shell run-as com.saturnwave.puzzle cat files/device.json` ile kimliği not et; uygulamayı durdurup aç, yeniden oku; aynı APK'yı üstüne kur, oku; kaldırıp kur, oku | İlk üç okumada aynı kimlik; kaldırıp kurunca yeni bir kimlik | Aşama 3 | Bekliyor |
| T81 | Kazanılan bölümün satırı | `attempts.jsonl` satır sayısını not et; hamle kalmışken bir bölüm kazan, şölen başlar başlamaz uygulamayı kapat; son satırı oku | Bir satır fazla: `"result":"won"`, `"client":"unity"`, `deviceId` device.json'daki kimlik, `moveLimit` bölümün kendi limiti, hamle kadar `{"act":"move"}` | Aşama 3 | Bekliyor |
| T82 | Kaybedilen bölümün satırı | Hamlesi biterek bir bölüm kaybet; kayıp kartı gelir gelmez dosyayı oku | Bir satır fazla: `"result":"outOfMoves"`, `movesUsed` bölümün hamle sayısı | Aşama 3 | Bekliyor |
| T83 | Bırakma ve kapatma | Hamle yapmadan bölümden çık; iki hamle oynayıp çık; bir hamle oynayıp uygulamayı kapat; her adımdan sonra satırları say | İlki satır eklemez; ikincisi `"result":"abandoned"`, `"movesUsed":2` ile bir satır ekler; üçüncüsü satır eklemez | Aşama 3 | Bekliyor |
| T84 | Araçlar telefonun dosyasını okur | Dosyayı bilgisayara al (`adb shell run-as com.saturnwave.puzzle cat files/attempts.jsonl > phone-attempts.jsonl`); `dotnet run --project src/Puzzle.Cli -c Debug -- humans phone-attempts.jsonl --out out/phone-humans.csv` | Her satır okunur, okunamayan satır yok; denemeler bölümlerine sayılır | Aşama 3 | Bekliyor |

### D. Can ve saat

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T30 | Can kaybı ve dönüşü | Bir bölümü kaybet; uygulama kapalıyken 30 dakika bekle | Kayıpta bir can gider; 30 dakika sonra geri gelir | Aşama 2 (görünmesi: can satırı) | Bekliyor |
| T31 | Canlar bitince | 5 canı da kaybet | Yeni deneme başlamaz; sonraki canın süresi gösterilir | Can ekranı | Bekliyor |
| T32 | Saat ileri | Can eksikken telefonun saatini 3 saat ileri al | Canlar dolar (A01, kabul edilmiş açık); oyun çökmez | Aşama 2 | Bekliyor |
| T33 | Saat geri | Saati 1 gün geri al; can kaybet; saati düzelt | Geri alınan saat can getirmez, yeni gün hakkı açmaz; düzeltince davranış A02'deki gibi | Aşama 2 | Bekliyor |
| T34 | Gece yarısı | 23:58'de oyna, gece yarısını geç | Reklamla "+5" ve reklamla altın hakları yeni güne sıfırlanır | Teklif ekranları | Bekliyor |
| T35 | Saat dilimi | Telefonun saat dilimini değiştir | Gün hakları ve canlar tutarlı kalır | Aşama 2 | Bekliyor |

### E. Müze ve yapboz

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T40 | 100 tablo | Koleksiyonda 20 odayı baştan sona gez; haritayı aç | Görüntüler 1 saniyede gelir; bellek uyarısı ya da çökme yok; kaydırma akıcı | Müze odaları | Bekliyor |
| T41 | Yapboz parmakla | Bir tablonun 10 parçasını elle yerleştir | Parça doğru yere oturur; yanlış yerde geri döner | — | Bekliyor |
| T42 | Otomatik tamamla | 40 parça biriktir, otomatik tamamla | Akıcı biter; sayaçlar doğru | — | Bekliyor |
| T44 | Uzun oda adları | Koleksiyonda 8., 12. ve 19. odaya geç; haritada aynı odalara bak | Dönem adı telefonda rahat okunuyor; okunmuyorsa ad kısaltılır ya da iki satıra bölünür | Müze odaları | Bekliyor |
| T43 | Geri tuşu | Her ekranda Android geri tuşuna bas | Bir önceki ekrana döner; salonda "Oyundan çıkılsın mı?" kartı gelir (T410–T412) | — | Bekliyor |

### F. Ekonomi ekranları

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T50 | "+5 hamle" | Hamlesi biten bir bölümde teklifi reklamla 3 kez, sonra altınla al | Günde 3 reklam ücretsiz; sonra 900 altın düşer; denemede bir kez | Teklif ekranı | Bekliyor |
| T51 | Bölüm öncesi booster | Üç bölüm öncesi booster'ı birden seç, bölümü başlat | Üçü de tahtada; raftan düşer | Bölüm başı ekranı | Bekliyor |
| T52 | Mağaza | Booster ve set al; altın paketine dokun | Altın düşer, booster rafa gelir; paketler "satışta değil" | Mağaza ekranı | Bekliyor |
| T53 | Reklamla altın | Mağazada reklam izle, günde 4 kez dene | İlk 3'ünde 4'er altın; 4.'sü reddedilir | Mağaza ekranı | Bekliyor |

### F2. Cüzdan satırı ve can kartı (aşama 7)

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T90 | İlk açılış | Uygulamayı ilk kez kur ve aç | Salonda altın hapında "200", kalpte "5" ve "Dolu"; harita ve koleksiyona geçerken satır yerinde kalır | Aşama 7 | Bekliyor |
| T91 | Geri sayım | Bir bölümde hamle yapıp çık ya da kaybet; salona dön | Kalpte "4"; hap 30:00 civarından saniye saniye geri sayar | Aşama 7 | Bekliyor |
| T92 | Can kartı | Kalbe ya da hapına dokun | Oda kararır, "Canlar" kartı iner: "4 / 5", işleyen "Sonraki can", "Canların ... sonra dolar"; "Tamam", yuvarlak X ve geri hareketi kartı kapatır | Aşama 7 | Bekliyor |
| T93 | Can bitince | Beş canı da kaybet, "Bölüm N"ye bas | Tahta açılmaz; kartta kırmızı "0 / 5" ve "Bir can gelince oynayabilirsin" | Aşama 7 | Bekliyor |
| T94 | Can gelince | Geri sayımın bitmesini bekle (ya da saati 30 dakika ileri al) | Kalpte "1"; "Bölüm N" bölümü açar | Aşama 7 | Bekliyor |
| T95 | Altının sayarak artması | Bir bölüm kazan, kartın "Müze" düğmesine bas | Salondaki tutar eski değerden yenisine yaklaşık bir saniyede sayarak çıkar | Aşama 7 | Bekliyor |
| T96 | İç ekran | Telefonu aç (2076×2152) | Satır plaketin üstünde ortalı, hiçbir şey çakışmıyor; salon, harita ve koleksiyon sığıyor | Aşama 7 | Bekliyor |
| T97 | Saat geri | Bir can eksikken telefonun saatini bir saat geri al | Can gelmez; hap en çok 30:00 gösterir ve bekler | Aşama 7 | Bekliyor |

### F3. Bölüm başı kartı (aşama 8)

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T100 | İlk kart | İlk kurulumda salonda "Bölüm 1"e bas | Kart iner: bölüm 1, hedefi, hamlesi, her birinde 3 yazan üç disk, hiçbiri seçili değil | Aşama 8 | Bekliyor |
| T101 | Seçmek, bırakmak | Fırça + sıçrama diskine dokun, sonra yine dokun; "+3 hamle"ye dokun; X ile kapat, yeniden aç | Önce yeşil halka ve tik, sonra kalkar; madalyon 3 artar; yeniden açınca hepsi yine 3, hiçbiri seçili değil | Aşama 8 | Bekliyor |
| T102 | Booster'la açılış | Fırça + sıçrama ve paleti seç, "Oyna"; hamle yapmadan geri tuşuyla çık ve onayla; kartı aç | Tahtada fırça, sıçrama ve palet en son altın ışıkla belirir; kartta hâlâ 3 ve 3 | Aşama 8 | Bekliyor |
| T103 | Harcama ilk hamlede | Aynısını yap, bir hamle oyna, bölümü bitir; sonraki kartı aç | 2 ve 2 | Aşama 8 | Bekliyor |
| T104 | Tekrar dene | Bir bölümü kaybet, "Tekrar dene" | Salona dönülür ve aynı bölümün kartı iner; "Oyna" "2. deneme" ile açılır | Aşama 8 | Bekliyor |
| T105 | Kazanınca | Bir bölüm kazan, "Devam" | Salona dönülür, altın sayarak artar, düğme sonraki bölümü gösterir; kart inmez | Aşama 8 | Bekliyor |
| T106 | Satın alma | Bir bölüm öncesi booster'ın üçünü de harca; artısına dokun; "Satın al"; sonra altını yetmeyen biriyle dene | Fiyatlı teklif kartı (+3 hamle: 200); satın alınca kart kalkar, altın geriye sayar, diskte 1; altın yetmezse düğme soluk ve "Altın yetmiyor" | Aşama 8 | Bekliyor |
| T107 | Can yokken | Bütün canları kaybet; "Tekrar dene"ye ve "Bölüm N"ye bas | İkisinde de bölüm kartı değil can kartı gelir | Aşama 8 | Bekliyor |
| T108 | Geri tuşu sırası | Teklif kartı açıkken geri tuşuna art arda bas | Önce teklif kartı, sonra bölüm kartı kapanır, sonra salonda bir şey olmaz | Aşama 8 | Bekliyor |
| T109 | Kart açıkken kapatma | Booster seçiliyken uygulamayı kapat, aç | Hiçbir şey harcanmamış | Aşama 8 | Bekliyor |

### F4. Tahta plaketleri, duraklatma ve kazanma kartı (aşama 9)

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T110 | Parça veren bölümün plaketleri | 46 bölüm kazanmış oyuncuyla 47. bölümü aç; bir hamle oyna | Solda "BÖLÜM 47", hedef ve 41; sağda "PARÇA 7 / 10", tablo karanlık, yalnızca bir parça yaldızla çevrili; sayılar tahtayla birlikte azalır | Aşama 9 | Bekliyor |
| T111 | Parça vermeyen bölüm | Parça vermeyen bir bölüm aç (2026-10-15 itibarıyla 1–1000 arasındaki her bölüm parça veriyor; bu durum yalnızca müze bittikten sonra "Tekrar oyna" ile oluşur) | Solda "BÖLÜM" ve numara, sağda "HEDEF" ve hedefler | Aşama 9 | Bekliyor |
| T112 | Zincirleme sırasında duraklatma | Uzun bir zincirlemede duraklatmaya bas, tahtada kaydır; kartı 10 saniye açık bırak; geri tuşuna bas | Hamle oynanmaz; tahta durunca "Bölümden çık?" gelir; kart açıkken hiçbir şey kıpırdamaz; geri tuşu kartı kapatır, oyun sürer | Aşama 9 | Bekliyor |
| T113 | Çıkmanın bedeli | Hamleden önce duraklat → "Henüz hamle yapmadın, can gitmez" → Müze; sonra hamleden sonra aynısı | İlkinde can aynı; ikincisinde "Çıkarsan 1 can gider" ve bir can eksik | Aşama 9 | Bekliyor |
| T114 | Kazanma kartı | Hamle kalmışken bir bölüm kazan; "Devam" | Şölen, sonra parça, "Yeni parça: k / 10", yenisi kırmızı halkalı noktalar, +0'dan sayan "+N", "Kalan N hamle fırçaya dönüştü"; N = 4 × kalan hamle; salonda altın tam N artar | Aşama 9 | Bekliyor |
| T115 | Şöleni atlama | Şölen sırasında ekrana dokun | Şölen hemen biter, aynı kart aynı +N ile gelir | Aşama 9 | Bekliyor |
| T116 | Son hamlede kazanma | Bir bölümü son hamlede kazan | Şölen yok; altın satırında yalnızca "Son hamlede tamamlandı", hap yok; altın değişmez | Aşama 9 | Bekliyor |
| T117 | Kart altında duraklatma | Herhangi bir kart açıkken soluk duraklatma diskine dokun; kazanma kartında geri tuşuna bas | Diske dokunmak bir şey yapmaz; geri tuşu salona götürür | Aşama 9 | Bekliyor |
| T118 | Tablonun son parçası | Bir tablonun son parçasını veren bölümü kazan | "Yeni parça: 10 / 10", bütün noktalar yaldızlı, sonuncusu halkalı | Aşama 9 | Bekliyor |
| T119 | Katlanır ekranda raf | Telefonu aç, sonra dış ekranda tekrarla | Raf diskleri ve duraklatma rahat basılıyor; kart tamamen ekranda | Aşama 9 | Bekliyor |

### F5. Bölüm içi booster'lar (aşama 10)

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T120 | İlk bölümlerde kilit | İlk kurulumda 1–6. bölümleri kazan; her tahtada rafa bak | Dört sönük disk: Bölüm 7, Bölüm 12, Bölüm 16, Bölüm 20; dokununca bir şey olmaz; raf yazısı ilk hamleden sonra kalkar | Aşama 10 | Bekliyor |
| T121 | Spatulanın açılışı | 7. bölümü ilk kez aç | Spatula diskinde 3 ve nefes alan bir ışık halkası; yazı "Yeni: Spatula! Diske dokun, sonra bir taşa." ve altında "Her bölüm booster'sız geçilebilir" | Aşama 10 | Bekliyor |
| T122 | Tek dokunuşla seçmek | Spatula diskine bir kez hızlıca dokun | Diskte yeşil halka, öteki diskler sönük; yazı "Spatula · bir taşı ya da bir katı kaldırır / Bir taş seç" | Aşama 10 | Bekliyor |
| T123 | Nişan ve kaydırma | Parmağını bir taşa koy, kaldırmadan yandaki hücreye kaydır, kaldır | Parmağın altındaki hücrede yaldız halka, kaydırmayı izler; yazı söner; kaldırınca spatula o hücreye iner; sayı 2 olur; HAMLE değişmez | Aşama 10 | Bekliyor |
| T124 | Booster denemeyi başlatır | Yeni bir tahtada hamle yapmadan spatulayı kullan, duraklat, çık | Çıkma kartı "Çıkarsan 1 can gider" der; çıkınca bir can gider | Aşama 10 | Bekliyor |
| T125 | Dokunulmamış tahta | Bir tahtayı açıp hiç dokunmadan çık | Kart can gitmeyeceğini söyler; can gitmez | Aşama 10 | Bekliyor |
| T126 | Satır fırçası | 12. bölümden sonra satır fırçasını seç, parmağını bir hücrede tut | Bütün satır boyunca soluk bir ışık; kaldırınca fırça satırı soldan sağa boyar ve temizler | Aşama 10 | Bekliyor |
| T127 | Taşlar düşerken | Bir hamle yap; taşlar düşerken sütun fırçasına ve bir hücreye dokun | Disk hemen seçilir; kullanım tahta oturunca uygulanır | Aşama 10 | Bekliyor |
| T128 | Bırakmanın yolları | Bir fırçayı seç, sonra sırayla: diskine yine dokun / geri hareketi / parmağı tahtanın dışına sürükleyip kaldır / duraklat | Her biri booster'ı bırakır; yalnızca duraklatma çıkmak isteyip istemediğini sorar; hiçbir şey harcanmaz | Aşama 10 | Bekliyor |
| T129 | Boşa kullanım | Spatulayı hiçbir şeyin vuramayacağı bir hücreye nişanla (malzemeli bir bölümde, ör. 51) | Disk sallanır, yazı "Burada vurulacak bir şey yok" der, sayı aynı kalır | Aşama 10 | Bekliyor |
| T130 | Raftan satın alma | Bir booster'ı bitir, yeşil artısına dokun, satın al; sonra yetersiz altınla dene | Fiyatlı kart; "Satın al" fiyatı düşer, diskte 1 ve (spatula ve fırçalarda) seçili; altın yetmezse "Altın yetmiyor", bir şey olmaz | Aşama 10 | Bekliyor |
| T131 | Karıştırma hep çalışır | 20. bölümden sonra büyük açık bir tahtada karıştırma diskine birkaç kez dokun | Her seferinde karıştırır ("Tahta karıştırılamıyor" gerçek bir bölümde hiç çıkmaz); taşlar döner, hazır eşleşme kalmaz | Aşama 10 | Bekliyor |
| T132 | Dokunma alanı | Diskin kenarına (bir parmak genişliği içinde) ve iki diskin arasına dokun | Kenar diski basar; arası en çok bir diski basar; diskler dış ve iç ekranda elde en az 40 puan | Aşama 10 | Bekliyor |
| T133 | Eski kayıtla açılış | Bu derlemeyi 20. bölümü geçmiş bir oyuncuyla 2.–9. aşama derlemesinin üstüne kur | Bölüm içi booster'lar yeniden verilmez (eski hediyeyle gelmişlerdi); cüzdandan önceki kayıtlı oyuncu (katman 1–2) her birinden 3 alır | Aşama 10 | Bekliyor |
| T134 | Katlanır, yatay | Katlanır telefonu açıp yatay tut, uzun bir tahtada booster seç | Yazı okunur; en alt sıranın ne kadarını örttüğünü not et (bilinen açık A24) | Aşama 10 | Bekliyor |

### F6. Hamle bitince (aşama 11)

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T140 | Teklif kartı | Bir bölümü hedefler kalmışken hamlesi bitene kadar oyna | "Hamle bitti!" kartı: yaldızlı +5, kalan hedeflerin üstünde "Az kaldı!" ya da "Kalan hedefler", 900'lü "+5 hamle", "Reklam izle", "Vazgeç ♥ 1 can gider"; başka hiçbir şey kıpırdamaz | Aşama 11 | Bekliyor |
| T141 | Altınla +5 | En az 900 altınla "+5 hamle"ye bas | Kart kalkar, oda aydınlanır, madalyon 1'den 5'e sayar, tahta sürer; salonda tam 900 altın eksik, can aynı | Aşama 11 | Bekliyor |
| T142 | Altın yetmezse | 900'den az altınla yeşil düğmeye bas | Düğme sönük, "Altın yetmiyor" der; bir şey olmaz | Aşama 11 | Bekliyor |
| T143 | Reklamla +5 | "Reklam izle"ye bas; aynı gün üç kez, sonra dördüncüde dene; ertesi gün yine bak | "Reklam canlandırması / TEST SÜRÜMÜ" kartı 3 saniyede dolar ve hamleler gelir; dördüncüde düğme sönük ve "Bugünlük bitti"; ertesi gün yine 3 hak | Aşama 11 | Bekliyor |
| T144 | İkinci teklif yok | +5'i aldıktan sonra hamleler yine bitsin | İkinci teklif gelmez; "–1" can hapı, "1 can gitti", Tekrar dene ve Müze; salonda bir can eksik | Aşama 11 | Bekliyor |
| T145 | Vazgeç | Teklifte "Vazgeç"; sonra "Tekrar dene"; tek can kalmışken tekrarla | Teklif kalkar, kaybetme kartı iner ("1 can gitti"); Tekrar dene bölümün kartını açar; son can gidince can kartı gelir | Aşama 11 | Bekliyor |
| T146 | Teklif açıkken | Teklif açıkken geri hareketi yap, tahtada kaydır, duraklatmaya dokun | Hiçbir şey olmaz, can gitmez, teklif kalır | Aşama 11 | Bekliyor |
| T147 | Reklam sırasında | Canlandırma reklamı sırasında geri tuşuna bas; uygulamadan ana ekrana çıkıp dön | Geri tuşu bir şey yapmaz; reklam sürer ve biter, sonra hamleler gelir | Aşama 11 | Bekliyor |
| T148 | Çıkma kartının yeni hâli | Hamle yapmadan duraklat; sonra bir hamleden sonra duraklat | Önce "Henüz hamle yapmadın, can gitmez"; sonra "–1" can hapı ve "Çıkarsan 1 can gider"; "Oyna" tahtaya döner | Aşama 11 | Bekliyor |
| T149 | +5 sonrası kazanma | +5'i al ve hamle artırarak kazan | Şölen kalan hamleleri fırçaya çevirir; kazanma kartı onların altınını gösterir | Aşama 11 | Bekliyor |
| T150 | Katlanır ekranlar | T140, T143 ve T145'i iç (kare) ve dış ekranda tekrarla | Kartlar plaketlerle raf arasına sığar; her düğme ve "Vazgeç" parmakla rahat basılır | Aşama 11 | Bekliyor |
| T151 | Kayıtlar | +5 alınan ve vazgeçilen birer denemeden sonra `attempts.jsonl` dosyasının son iki satırını oku (adb) | Deneme başına bir satır, deneme kesin bitince yazılmış; +5'li denemede `"extraMovesBought":true` ve +5 eylemi | Aşama 11 | Bekliyor |

### F7. Mağaza, hediye ve tablo ödülü (aşama 12)

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T160 | Mağazaya yollar | Salonda altının yeşil artısına bas; haritadan ve koleksiyondan çubuktaki "Mağaza"ya bas; kalbin artısına bas | Mağaza soldan gelir: lambalar arasında tabela, üç paketle ALTIN, iki setle GÜÇLENDİRİCİLER, ÜCRETSİZ şeridi; kaydırmadan hepsi görünür. Kalbin artısı can kartını açar | Aşama 12 | Bekliyor |
| T161 | Altın paketi | Bir altın paketinin altındaki "Yakında"ya bas | "Altın paketleri: Bu test sürümünde satın alma yok." kartı; Tamam kapatır; altın aynı | Aşama 12 | Bekliyor |
| T162 | Set almak | En az 900 altınla Başlangıç setinin yeşil 900 düğmesine bas; bir bölümün kartını aç; uygulamayı kapatıp yeniden aç | "Satın alındı" kartında üç başlangıç booster'ı +1; satır 900 geri sayar; bölüm kartında her başlangıç booster'ı bir fazla; yeniden açınca da duruyor | Aşama 12 | Bekliyor |
| T163 | Altın yetmezse | Usta setinin fiyatından az altınla sönük düğmesine bas | "Altın yetmiyor: Usta seti için N altın daha gerek." kartı; hiçbir şey değişmez | Aşama 12 | Bekliyor |
| T164 | Reklamla altın | Ücretsiz şeritte "İzle"ye üç kez bas; dördüncüyü dene; aynı gün uygulamayı kapatıp aç; gece yarısından sonra yine bak | Her seferinde "Reklam canlandırması / TEST SÜRÜMÜ" kartı 3 saniye sürer, sonra satır +4 sayar; üçüncüden sonra düğme sönük "Yarın yine", şerit "BUGÜNLÜK BİTTİ" (yeniden açınca da); gece yarısından sonra yine 3 hak | Aşama 12 | Bekliyor |
| T165 | Reklam sırasında | Canlandırma reklamı sürerken geri tuşuna bas; uygulamayı arka plana alıp dön; bir kez de reklam sırasında uygulamayı kapat | Geri tuşu bir şey yapmaz; reklam biter ve bir kez öder; reklam sırasında kapatılan uygulama ödemez ve hak harcamaz | Aşama 12 | Bekliyor |
| T166 | Günün hediyesi | Salonda hediye çerçevesine bas (kırmızı nokta, "Hediye"); yine bas; uygulamayı kapatıp aç | "Günün hediyesi" kartında bir booster +1; nokta kalkar, hap gece yarısına kalan saati gösterir; ikinci basış "Bugünkü hediyeni aldın. Yenisi ... sonra." der; yeniden açınca da alınmış | Aşama 12 | Bekliyor |
| T167 | Saat geri | Hediyeyi aldıktan sonra telefonun tarihini bir gün geri al, oyunu aç, hediyeye bas, reklam şeridine bak; tarihi düzelt | Geri tarihte yeni hediye ve yeni reklam hakkı yok; hap uzun bir bekleme gösterir; tarih düzelince hiçbir şey kaybolmaz | Aşama 12 | Bekliyor |
| T168 | Ertesi gün | Ertesi takvim günü salonu aç | Kırmızı nokta ve "Hediye" yine orada; hediye sıradaki booster | Aşama 12 | Bekliyor |
| T169 | Tablo ödülü | Bir tablonun son parçasını yapbozunda yerleştir (bir kez parmakla, bir kez "Otomatik tamamla" ile) | Kutlamadan sonra "Tablo tamamlandı! İşte ödülün", tepside +80 altın ve +1 booster; salonda satır +80 sayar; booster bölüm kartında ya da rafta | Aşama 12 | Bekliyor |
| T170 | Ödül bir kez | Son parça yerine oturur oturmaz uygulamayı kapat, aç; tabloyu koleksiyonda aç | Ödül tam bir kez verilmiş (+80 altın bir kez); tabloyu bütün olarak görmek bir şey vermez | Aşama 12 | Bekliyor |
| T171 | Mağazaya git | Bölüm kartında hiç sahip olmadığın ve altının yetmediği bir başlangıç booster'ının artısına bas; "Mağazaya git"e bas | İki kart da kalkar, mağaza gelir; bir şey alınmaz, can gitmez | Aşama 12 | Bekliyor |
| T172 | Katlanır ekranlar | T160, T162 ve T166'yı açık iç ekranda ve dış ekranda tekrarla | İç ekranda iki sütun (paketler ve setler yan yana, ücretsiz şerit altta), dışta tek sütun; her düğme rahat basılır; hediye çerçevesi tezgâhın yanında | Aşama 12 | Bekliyor |

### F8. Ayarlar, gizlilik ve destek (aşama 13)

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T180 | Çark | Salonda, haritada, koleksiyonda ve mağazada altın-kalp satırının sağ üstündeki yuvarlak çarka dokun | "Ayarlar" kartı her seferinde iner; arkadaki çubuğa ya da altına dokunmak bir şey yapmaz; telefonun geri hareketi kartı kapatır; başka bir sayfada × kapatır | Aşama 13 | Bekliyor |
| T181 | Kartı oku | Ayarlar kartını aç ve oku | Dil (seçili dilin adı ve ok; dokunmak dil kartını açar), Gizlilik ve veri ("Oyun hiçbir şey göndermez"), Destek; üstte Ses, Müzik ve Titreşim anahtarları; altta "Oyuncu kimliği: XXXX-XXXX · Sürüm 1.0" ve "Test sürümü · reklamlar canlandırma, satın alma kapalı"; kesik yazı yok, ğ, ı, ş, ü doğru | Aşama 13 | Bekliyor |
| T182 | Kayıt sayısı | İki bölümü sonuna kadar oyna (kazan ya da kaybet), sonra Gizlilik ve veri kartını aç | Metin rahat okunur ve sana göre doğru; "Deneme kayıtları" kayıtlar başladığından beri biten her denemeyi sayar (kazanılan, kaybedilen, bir hamleden sonra bırakılan) | Aşama 13 | Bekliyor |
| T183 | Kayıtları sil | "Kayıtları sil"e dokun, sonra "Vazgeç"; yine dokun, sonra "Sil"; kartları kapat | Önce bir soru; Vazgeç bir şey değiştirmez; Sil sayıyı 0 yapar ve "Bütün kayıtlar silindi." der; düğme sönük; altın, can, bölüm, booster'lar ve yerleştirilmiş parçalar önceki gibi | Aşama 13 | Bekliyor |
| T184 | Destek | Destek kartını aç. Telefon USB'deyken kartın gösterdiği klasörü aç | Oyuncu kimliği ayarlar kartının altındakiyle aynı; "Sürüm 1.0"; "Bölüm içeriği sürüm 1"; yol `/storage/emulated/0/Android/data/com.saturnwave.puzzle/files/attempts.jsonl`; T183'ten sonra dosya yok, bir bölüm daha bitince tek satırla geri gelir | Aşama 13 | Bekliyor |
| T185 | Yeniden açılış | T183'ten sonra oyunu tamamen kapat (kaydırıp at), yeniden aç; ayarları ve gizlilik kartını aç | Sayı hâlâ 0 (ya da o zamandan beri biten deneme kadar); oyuncu kimliği öncekiyle aynı | Aşama 13 | Bekliyor |
| T186 | Veri kullanımı | Android Ayarlar > Uygulamalar > oyun > Mobil veri ve Wi-Fi: Wi-Fi açıkken 10 dakikalık bir oturumdan önce ve sonra bak; Uygulama bilgisi > İzinler'e de bak | Oyun veri kullanmamış (0 B); izin listesinde internet yok (2026-10-16'dan beri paket istemiyor). Veri görünürse gizlilik metni değişmeli | Aşama 13 | Bekliyor |
| T187 | Üstüne kurulum | Yeni sürümü eskisinin üstüne kur (kaldırmadan), oyunu aç | İlerleme, altın ve canlar duruyor; ayarlar kartı açılıyor (1. düzendeki eski cihaz dosyası bütün ayarlar açık okunur); hata görünmüyor | Aşama 13 | Bekliyor |

### F9. Ses ve titreşim (aşama 14)

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T190 | İki anahtar | Temiz kurulumda (ya da Ayarlar > Uygulamalar > Puzzle > Depolama > Depolamayı temizle; ilerleme silinir, bu testi turun sonuna bırak) oyunu aç, cüzdan satırındaki çarka bas | Kartta "Ses", "Müzik" ve "Titreşim" anahtarları, üçü de açık; altlarında çizgi, sonra dil, gizlilik ve destek satırları | Aşama 14 | Bekliyor |
| T191 | Her sesi dinle | Medya sesi yarıda. Bir düğmeye bas; ayarlarda bir anahtarı çevir; bir bölümün kartını aç; eşleşen iki taşı değiştir; dörtlü eşleşme yap; roket patlat; spatula, fırça ve karıştırmayı kullan; eşleşmeyen iki taşı değiştirmeyi dene; bol hamleyle kazan; hamlesi biten bölümde vazgeç; mağazada bir şey al; yapbozda parça yerleştir; bir tablonun son parçasını yerleştir | 15 sesin her biri duyulur: kısa, aşağı yukarı aynı yükseklikte, hiçbiri sert ya da kırpık değil; tahta durduktan sonra çalmaya devam eden yok. Fazla yüksek, alçak, tekrarda sinir bozucu ya da yerine uymayan sesleri not et (yer tutucu sesler; karar senin kulağının) | Aşama 14 | Bekliyor |
| T192 | Şölen sesi | 10'dan fazla hamleyle bir bölümü kazan, şöleni dinle; başka bir kazançta şölen sırasında ekrana dokun | Atışlar hızlı bir dizi olarak duyulur (yaklaşık iki atışta bir), roketler birkaç patlama; sürekli takırtı yok. Şöleni geçen dokunuş seslerini de hemen keser; kazanma sesi kart inerken duyulur | Aşama 14 | Bekliyor |
| T193 | Uzun zincir | Uzun bir zincir ya da renk bombası birleşimi yapana kadar oyna | Patlamalar ve özel taşlar aralıklı duyulur, vızıltı gibi değil; tahtanın hızı seslerden önceki gibi | Aşama 14 | Bekliyor |
| T194 | Titreşimi hisset | Telefonun "Titreşim ve dokunma geri bildirimi" açık. Özel taş yap, vuran bir booster kullan, bir bölüm kazan, bir tablo tamamla; düğmelere de bas, düz eşleşmeler yap | Özel taşta ve booster vuruşunda hafif tık; kazanma kartı inerken ve tablo tamamlanınca güçlü tık; basışta, patlamada, değiştirmede ve kartlarda tık yok. Tıklar doğru mu, zayıf mı, güçlü mü, not et | Aşama 14 | Bekliyor |
| T195 | Ses kapalı | Ayarlarda "Ses"i kapat, kartı kapat, özel taş dahil birkaç hamle oyna | Anahtarın kendi tıkı duyulmaz (ayar o anda geçerli); sonra hiçbir şey duyulmaz; titreşim tıkları sürer | Aşama 14 | Bekliyor |
| T196 | Titreşim kapalı | Ses açıkken "Titreşim"i kapat, özel taş yap ve bir bölüm kazan | Hiç tık yok; bütün sesler duyulur | Aşama 14 | Bekliyor |
| T197 | Sesi geri aç | "Ses" kapalıyken aç | Anahtarın tıkı ve sonraki her ses duyulur | Aşama 14 | Bekliyor |
| T198 | Yeniden açılışta ayarlar | "Ses" kapalı, "Titreşim" açık bırak; kartı kapat; oyunu son uygulamalardan kaydırıp at, yeniden aç, çarka bas | Kart "Ses" kapalı, "Titreşim" açık gösterir ve oyun öyle davranır. Sonra ikisini yine aç | Aşama 14 | Bekliyor |
| T199 | Telefonun kendi ayarları | Medya sesini sıfıra indir, oyna; sonra telefonun dokunma geri bildirimini kapat (Ayarlar > Ses ve titreşim > Titreşim ve dokunma), "Titreşim" açıkken oyna | Ses sıfırken hiçbir şey duyulmaz. Telefonun titreşimi kapalıyken oyunun tıklarının gelip gelmediği Android'in kararı; ne olduğunu not et. Sessiz mod Android'de oyun seslerini durdurmaz (medya sesi); şaşırtırsa not et | Aşama 14 | Bekliyor |
| T200 | Arka plana alma | Bir zincir sırasında ana ekrana çık, bekle, dön; bir de telefon görüşmesi yap ya da sesli başka bir uygulama aç | Oyun arka plandayken ses çalmaz, çalmaya devam eden yok; dönünce sesler yine çalışır | Aşama 14 | Bekliyor |
| T201 | Titreşim izni | T194'te "Titreşim" ve telefonun titreşimi açıkken hiç tık gelmezse telefonu bağla, günlükte "vibrator refused a tick" ya da "cannot be reached" ara | Tıklar hissedilir; bu, VIBRATE izninin pakette olduğunu kanıtlar. Günlükte ret varsa izin adımı o derlemede çalışmamış | Aşama 14 | Bekliyor |

### F10. Test modu (#10)

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T210 | Yedi dokunuş | Çark > Destek. "Sürüm" satırına altı kez dokun, sonra yedinci kez | Altıda bir şey olmaz. Yedincide satır ve Tamam yerine "Test modu açılsın mı?", altında bir satır, Vazgeç ve Aç gelir | Test modu | Bekliyor |
| T211 | Vazgeç | Soruda Vazgeç'e bas; kartları kapat | Destek kartı ilk hâlinde (Tamam geri gelir); satır gerçek altını ve canı gösterir | Test modu | Bekliyor |
| T212 | Aç | Yine yedi dokunuş, Aç | Bütün kartlar kalkar; altın hapında ∞, kalpte ∞, can hapında "Test". Kalp can kartını açar: ∞, "Sınırsız", "Test modu açık", "Kaybedince can gitmez" | Test modu | Bekliyor |
| T213 | Kayıp can götürmez | Modu açmadan önce canı not et. Bir bölümü hamlesi bitene kadar oyna, teklifte Vazgeç, Tekrar dene. Bir bölümden de hamleden sonra çık | Bölümün kartı gelir (can kartı hiç gelmez); mod kapatılınca canlar önceki gibi (ya da zamanla daha çok) | Test modu | Bekliyor |
| T214 | Az altınla +5 | 900'den az altınla hamleleri bitir | "+5 hamle" yanar, beş hamle verir; altın değişmez | Test modu | Bekliyor |
| T215 | Mağaza ve booster bedava | En pahalı seti al; bölüm kartında hiç olmayan bir başlangıç booster'ının artısına bas, Satın al; tahtada hiç olmayan bir booster'ın artısına bas, al | Hepsi olur, booster'lar gelir, altın aynı kalır. Altın paketleri yine "Yakında" | Test modu | Bekliyor |
| T216 | Reklam ve hediye kuralları | Mağazada günün reklamlarını bitene kadar izle; günün hediyesini iki kez almayı dene | Reklamlar günün hakkından sonra durur; hediye günde bir kez. Kazanılan altın mod kapatılınca kalır | Test modu | Bekliyor |
| T217 | Ayarlardan kapatma | Çark: Titreşim'in altında "Test modu" satırı açık. Kapat, kartı kapat, yeniden aç | Kart kapanınca satır hemen gerçek altını ve canı gösterir; yeniden açılan kartta Test modu satırı yok | Test modu | Bekliyor |
| T218 | Yeniden açılışta | Modu aç, uygulamayı son uygulamalardan kapat, aç; sonra modu kapat ve yine yeniden başlat | İlk yeniden açılışta açık (∞), ikincide kapalı | Test modu | Bekliyor |
| T219 | Eski kurulumun üstüne | Bu değişiklikten önceki bir derlemenin (sesi kapalı bir device.json'la) üstüne kur | Ses kapalı kalır, test modu kapalı | Test modu | Bekliyor |
| T220 | Kayıtlar işaretli | Modda bir deneme, moddan çıkınca bir deneme oyna; destek kartındaki yoldan attempts.jsonl'u adb ile al; `puzzle humans` çalıştır | Yalnızca test modundaki denemenin satırında `"testerMode":true`; karşılaştırma onu "test modu" başlığı altında sayar, karşılaştırmaz | Test modu | Bekliyor |
| T221 | Test modunda kaybetme kartı | Mod açık. Bir bölümü hamle bitene kadar oyna, teklifte Vazgeç | Kartta yalnızca "Test modu · can gitmez"; kalp hapı ve "1 can gitti" yok. Mod kapalıyken aynı kart "–1" ve "1 can gitti" gösterir | Test modu | Bekliyor |
| T222 | Test modunda çıkma kartı | Mod açık. Bölüme başla, bir hamle yap, duraklat | Kart "Test modu · can gitmez" der, hap yok; Müze salona götürür, canlar aynı. Hamleden önce yine "Henüz hamle yapmadın, can gitmez" | Test modu | Bekliyor |
| T223 | Teklifte Vazgeç | Mod açık. Hamleleri bitir | "Vazgeç" kalp ve "1 can gider" olmadan, altı çizili, yalnız durur; mod kapalıyken yine gösterir | Test modu | Bekliyor |

### F11. Sayfalar arasında kaydırma (#3)

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T230 | Dört sayfada kaydırma | Salondan yavaşça sola kaydırarak haritaya, sonra sağa salona, koleksiyona ve mağazaya; sonra öbür yöne | Sayfa parmağı izler, komşusu yanında görünür; bırakınca komşuya oturur; çubuğun vurgusu bırakınca kayar; başlanan sayfadaki hiçbir düğmeye basılmamıştır | Kaydırma | Bekliyor |
| T231 | Uçlarda esneme | Mağazada sağa, haritada sola, ikisinde de uzun kaydır | Sayfa yalnızca biraz ve parmaktan yavaş kayar, bırakınca geri oturur; başka bir şey olmaz. Kenardaki düz şeridin görünüşünü not et (A34) | Kaydırma | Bekliyor |
| T232 | Kısa sürükleme, hızlı fiske | Bir sayfayı yavaşça ekranın dörtte biri kadar sürükleyip bırak; sonra kısa ve hızlı bir fiske at | Yavaş kısa sürükleme geri oturur; hızlı fiske komşu sayfaya geçer | Kaydırma | Bekliyor |
| T233 | Dokunuşlar dokunuş kalır | Hafif kıpırdayan bir parmakla bütün çubuk düğmelerine, oyna düğmesine, hediye çerçevesine, çarka, altına, kalbe, koleksiyonda bir tabloya, parçası bekleyen bir tabloya, oda oklarına ve haritadaki düğmelere dokun | Her biri eskisi gibi çalışır; kazara sayfa geçmez | Kaydırma | Bekliyor |
| T234 | Dikey sürüklemeler | Her sayfada yukarı-aşağı ve yaklaşık 25 dereceden dik çapraz sürükle | Hiçbir sayfa kaymaz | Kaydırma | Bekliyor |
| T235 | Android geri hareketi | Hareketli gezinmede koleksiyon ve haritada sol ve sağ kenardan geri hareketini yap; sonra tam kenardan başlayan bir yan kaydırma dene | Geri hareketi salona döner (ya da öne alınmış tabloyu yerine koyar, kartı kapatır), hiç sayfa çevirmez; kenar şeridinde başlayan kaydırma bir şey yapmaz, bir başparmak içeriden başlayan sayfa çevirir | Kaydırma | Bekliyor |
| T236 | Koleksiyonda plaketle oda | Koleksiyonda oklu üst plaketin üstünde sola-sağa kaydır; sonra duvarda ya da bir tabloda kaydır; okları kullan; ilk odadan önceye ve son odadan sonraya dene | Plakette önceki/sonraki odanın tabloları kayarak gelir, plaket yeni odayı söyler; başka yerde sayfa mağazaya ya da salona geçer; oklar çalışır; uçlarda tablolar yalnızca esneyip geri oturur | Kaydırma | Bekliyor |
| T237 | Kart açıkken | Her kartı aç (ayarlar, gizlilik, destek, hediye, can, bölüm, booster teklifi, mağazanın reklamı) ve kartın ve çevresindeki karanlığın üstünde kaydır | Hiçbir sayfa kaymaz, kart yerinde kalır; karttaki düğmeler eskisi gibi çalışır | Kaydırma | Bekliyor |
| T238 | Öne alınmış tablo | Koleksiyonda onarılmış bir tabloya dokun (öne gelir), sonra kaydır | Bir şey kaymaz; dokunuş tabloyu yerine koyar; sonra kaydırma yine çalışır | Kaydırma | Bekliyor |
| T239 | Katlanır iç ekran | T230–T236'yı açık iç ekranda tekrarla; sürüklemenin ortasında telefonu aç ya da kapat | Aynı davranış; geniş ekranın üçte biri sayfayı çevirir; sürükleme ortasında katlama sayfayı hemen yerine koyar, bir şey bozulmaz | Kaydırma | Bekliyor |
| T240 | İlk sürüklemede akıcılık | Müze yeni açılmışken salondan koleksiyona ve mağazadan koleksiyona doğru yavaş bir kaydırma başlat; ilk anlara bak | Komşu sayfa belirirken gözle görülür bir takılma yok (sayfa o anda kuruluyor) | Kaydırma | Bekliyor |
| T241 | Tahta ve yapboz | Bir bölümde ekran kenarlarına yakın taşları kaydır; yapbozda parçaları yana sürükle | Değiş tokuşlar ve parça sürüklemeleri eskisi gibi; sayfa çevrilmez | Kaydırma | Bekliyor |

### F12. Dil desteği, 1. adım (#8)

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T250 | Türkçe telefonda ilk açılış | Telefon Türkçeyken temiz kurulum | Oyun Türkçe; Ayarlar'da "Dil · Türkçe" | Diller | Bekliyor |
| T251 | İngilizce ve Korece telefonda ilk açılış | Telefon İngilizceyken, sonra Koreceyken temiz kurulum (ya da device.json silinmiş) | Oyun o dilde açılır | Diller | Bekliyor |
| T252 | Desteklenmeyen telefon dili | Telefon henüz sunulmayan bir dildeyken (o an hangisi sunulmuyorsa) | Oyun İngilizce açılır | Diller | Bekliyor |
| T253 | Dil kartı | Ayarlar > Dil | Kart sunulan dilleri kendi adlarıyla listeler, konuşulan işaretli; Tamam, yuvarlak düğme ve geri hareketi kapatır | Diller | Bekliyor |
| T254 | İngilizceye geçiş | English'i seç; kartları kapat; harita, koleksiyon, mağaza, hediye, can kartına bak | Dil kartı, arkasındaki ayarlar kartı ve salon hemen değişir; hepsi İngilizce | Diller | Bekliyor |
| T255 | Seçim kalır | Uygulamayı tamamen kapatıp aç; telefonun dilini değiştir | Telefon Türkçe olsa da seçilen dilde kalır; telefonun dili seçimi ezmez | Diller | Bekliyor |
| T256 | Korece görünüm | 한국어'yu seç | Hiçbir yerde boş kutu yok; başlıklar ve plaketler Korece serif yazıyla; yeşil ve mavi düğmelerde Korece harflerin koyu çizgisi duruyor | Diller | Bekliyor |
| T257 | Korece bölüm | Korece bir bölüm oyna | Plaketler, raf yazısı (seçili booster "블록 선택"), ilk kullanım satırı, kazanma kartı, +5 teklifi, reklam canlandırması, kaybetme kartı sığar ve doğal okunur | Diller | Bekliyor |
| T258 | Uzun adlar | Korece ve İngilizcede koleksiyonda uzun adlı tablolar; salon, harita ve koleksiyon plaketinde 12., 18. ve 19. odalar | Uzun tablo adları iki satır; oda adları sığar | Diller | Bekliyor |
| T259 | Katlanır ekran | T254–T258'i iç ekranda (2076×2152) ve dış ekranda tekrarla; oyun ortasında telefonu aç | Aynı sonuçlar; açınca bozulma yok | Diller | Bekliyor |
| T260 | Sayılar ve süreler | Altını, can bekleme süresini ve hediye hapını üç dilde oku | Altın İngilizce ve Korecede "1,250", Türkçede "1.250"; can bekleme "1h 42m" / "1시간 42분" / "1 sa 42 dk" | Diller | Bekliyor |
| T261 | Gizlilik kartı | İngilizce ve Korecede gizlilik kartını aç; kayıtları sil | Korece satırlar sözcük aralarından kırılır; "Kayıtları sil" önce sorar, sonra siler | Diller | Bekliyor |
| T262 | Destek ve test modu | İngilizce ve Korecede destek kartında sürüme 7 kez dokun | Test modu sorusu o dilde gelir | Diller | Bekliyor |
| T263 | Türkçeye dönüş | Türkçeyi seç | Bütün sayfalar ve kartlar yine Türkçe; sonraki bölümün tahtası Türkçe | Diller | Bekliyor |
| T264 | Hızlı geçişler | Dilleri art arda birkaç kez değiştir | Takılma yok, kartlar ikilenmiyor, bellek sabit | Diller | Bekliyor |
| T265 | Android'in uygulama dili | Telefon Korece, oyun İngilizce seçiliyken (telefon sunuyorsa) Android'in uygulama başına dil ayarına bak | Oyunun kendi seçimi karışmaz | Diller | Bekliyor |

### F13. İpucu (#6)

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T290 | İpucu çıkıyor | Tahtaya hiç dokunmadan bekle | Bekleme süresinden sonra iki taş sıcak bir hale ve halkayla parlar, birbirine doğru kıpırdar; titreşim açıksa bir kez tık. İki ekranda da kol mesafesinden ipucu olduğu anlaşılıyor mu? Süre erken mi? | İpucu | Bekliyor |
| T291 | Dokununca gider | İpucu görünürken tahtaya, tahtanın üstündeki duvara ve bir raf diskine dokun | İpucu her seferinde hemen gider, taşlar yerlerine döner; duvara dokunmak bir şey çalmaz | İpucu | Bekliyor |
| T292 | İpucunu oyna | İpucu verilen iki taşı kaydır | Hamle her hamle gibi oynanır; sonraki ipucu ancak yine dokunulmadan bekleme süresi geçince gelir | İpucu | Bekliyor |
| T293 | En çok üç | Tahtaya üç kez dokunmadan bekle | Üçüncüden sonra dördüncü ipucu gelmez; bölümü tekrar deneyince yine üç hak | İpucu | Bekliyor |
| T294 | Ses ve titreşim | Titreşim kapalıyken ipucunu bekle; ses açık ve kapalıyken bekle | Titreşim kapalıyken tık yok; ipucu hiçbir zaman ses çıkarmaz | İpucu | Bekliyor |
| T295 | İpucu çıkmayan anlar | Spatula seçiliyken, duraklatma kartı ya da +5 teklifi açıkken, şölende, zincirleme düşerken, bir taş tek dokunuşla seçiliyken bekle | Hiçbirinde ipucu çıkmaz | İpucu | Bekliyor |
| T296 | Koyu taşlar ve özel taşlar | Mavi ve mor taşlara, roketlerin, bombaların ve sıçramanın yanına düşen ipuçlarına bak | Parlama her renkte görünür; özel taşa verilen dokunma ipucu "buna dokun" diye okunur | İpucu | Bekliyor |
| T297 | Kenarda ipucu | Tahtanın kenarına ya da köşesine düşen bir ipucuna bak | Hale yaldız çerçeveye taşar; kabul edilebilir mi, yoksa içeride mi kalmalı? | İpucu | Bekliyor |
| T298 | Akıcılık | İpucunun belirdiği ana dikkatle bak | Gözle görülür takılma yok (bot bir karede düşünüyor) | İpucu | Bekliyor |
| T299 | Kayıt | İpucu görülen bir bölümü oyna, sonra deneme kaydına bak (adb) | Deneme kaydı ipucu sayısını tutar (hintsShown) | İpucu | Bekliyor |

### F14. Öğretici (#5)

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T310 | İlk açılışta hedef | Uygulamanın verisini sil ve başlat | 1. bölüm hedef plaketi aydınlık, gerisi koyu açılır; yazı iki ekranda da kol mesafesinden okunur; bir dokunuş ilerletir, hiçbir şey oynatmaz | Öğretici | Bekliyor |
| T311 | Elle gösterilen eşleşme | Başka bir çifti kaydır; sonra gösterilen çifti iki taştan birinden kaydır; bir kez de iki dokunuşla dene | Başka çift reddedilir (sarsılma, hamle harcanmaz); gösterilen çift her yoldan oynanır | Öğretici | Bekliyor |
| T312 | Hamle sayacı | İlk hamleden sonra bak; sonra adımlar bitince tahtayı bekle | Hamle madalyonu yazısıyla aydınlanır, dokunuş ilerletir; adım sürerken ipucu çıkmaz; son adımdan sonra ipucu olağan bekleme süresiyle gelir | Öğretici | Bekliyor |
| T313 | Adım ve duraklatma | Gösterilen eşleşme sırasında duraklat; "Oyna"; bölümden çıkıp geri gel | Adım çıkma kartının altında saklanır, dönünce gelir; yeniden girince hedef adımı tekrar gelmez, eşleşme adımı gelir | Öğretici | Bekliyor |
| T314 | İlk parça | 1. bölümü kazan, yapboz düğmesine bas; yerleştirmeden geri tuşuyla çık, yapbozu yeniden aç | Parça ve yeri aydınlık, mavi düğme gizli, sürükleme çalışır; yeniden açınca adım yine gelir | Öğretici | Bekliyor |
| T315 | Dörtlü ve beşli | 2. ve 3. bölümü oyna | 2. bölümde dörtlü bütün sırasıyla, 3. bölümde beşli gösterilir; fırça ve palet yazının dediği gibi çıkar. Bölüm böyle bir hamleyle başlamazsa, hamle çıkana kadar bir şey gösterilmez | Öğretici | Bekliyor |
| T316 | Engeller | 11. (sandık), 21. (vernik), 41. (bant) ve 51. (çerçeve köşesi) bölüme gel | Her biri bir kez yazısıyla gösterilir; dokunuş ilerletir | Öğretici | Bekliyor |
| T317 | İlk tablo | İlk tabloyu tamamla | Kutlamadan sonra tablo müze satırıyla aydınlanır; dokunuş ilerletir, hiçbir şeye basmaz; sonraki tabloda satır yok | Öğretici | Bekliyor |
| T318 | Eski kurulumun üstüne | Prototip APK'nın (eski ilerleme dosyası) üstüne, 3. bölümü geçmiş bir oyuncuyla kur | İlk bölümlerin adımları gelmez; yapboz adımları yalnızca daha önce parça yerleştirilmemiş ya da tablo tamamlanmamışsa gelir | Öğretici | Bekliyor |
| T319 | Adım ortasında kapatma | Bir adımın ortasında uygulamayı kapatıp aç | Biten adımlar tekrar gelmez; yarıda kalan yine gelir | Öğretici | Bekliyor |
| T320 | İngilizce ve Korece | Ayarlardan dili değiştir, adımları gez | Her adımın yazısı hapına sığar ve doğal okunur | Öğretici | Bekliyor |
| T321 | Akıcılık | 2.–6. bölümde dörtlü ya da beşli beklenirken tahta durduğu anlara bak | Gözle görülür takılma yok | Öğretici | Bekliyor |

### F15. Booster tanıtımı (#7)

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T330 | Spatula elle kullanımı | 7. bölüme ilk kez gir | Tahta kararır; spatula diski ışık halkasıyla, bir hedef taş aydınlık; hapta "Yeni: Spatula! Diske dokun". Diske dokununca hedefte nişan çıkar, hapta "Şimdi parlayan taşa dokun". Hedefe dokununca spatula vurur, adım biter. Karartma ve pencere iki ekranda da açık mı? | Booster tanıtımı | Bekliyor |
| T331 | Bedava | Rehberli kullanımdan önce ve sonra spatula diskindeki sayıya, bölümden sonra stoğa bak | Sayı 3 kalır, stok azalmaz | Booster tanıtımı | Bekliyor |
| T332 | Uzun basma öğretimi | 7. bölümde rehberli spatulayı kullan, tahtanın durmasını bekle | Spatula diski yeniden aydınlanır; hapta "Bir booster'ı basılı tutarsan ne yaptığını yeniden izlersin". Dokunuş kapatır, hiçbir şeye basmaz; bir daha gelmez | Booster tanıtımı | Bekliyor |
| T333 | Bedava kullanımdan sonra çıkış | 7. bölümde rehberli spatuladan sonra başka hamle yapmadan bölümden çık | Çıkış kartı can gideceğini söylemez; can ve deneme sayısı değişmez; kayıt yazılmaz; yeniden girince spatula adımı gelmez | Booster tanıtımı | Bekliyor |
| T334 | Rehberliden sonra kendi hamlen | 12. bölümde rehberli yatay fırçadan sonra bir hamle yap, sonra çık; deneme kaydına bak (adb) | Çıkış bir can götürür; kayıtta "boostersUsed":["hit-row"] ve "guidedBoosters":["hit-row"] var | Booster tanıtımı | Bekliyor |
| T335 | Başka hamle reddi | Spatula adımı sürerken taşları kaydır, kilitli diske dokun, spatulayı alıp başka bir taşa dokun | Hiçbiri oynanmaz; yanlış hedefte disk sallanır, spatula elde kalır, adım sürer | Booster tanıtımı | Bekliyor |
| T336 | Yatay ve dikey fırça | 12. ve 16. bölüme gir; fırçayı aydınlık satırın ya da sütunun başka bir hücresine dokunarak kullan | Aydınlık satır ya da sütunun her hücresi kabul edilir; fırça boyar, adım biter. Küçük ekranda dikey fırçanın hapı bir sütunun üst hücrelerini örtüyor mu? | Booster tanıtımı | Bekliyor |
| T337 | Karıştırma | 20. bölüme gir, aydınlık diske dokun | Tahta hemen karışır, adım biter, stok azalmaz | Booster tanıtımı | Bekliyor |
| T338 | İpucu yok | Bir booster adımında ve uzun basma kartı açıkken 15 sn bekle | İpucu çıkmaz; adım ya da kart kapanınca ipucu beklemesi yeniden başlar | Booster tanıtımı | Bekliyor |
| T339 | Tahtada uzun basma | Açılmış her booster diskine yarım saniye bas, bırak | Kart iner; küçük tahtada booster'ın etkisi tekrar tekrar oynar; booster seçilmez, kullanılmaz. 0,45 sn doğal mı, kısa basmayla karışıyor mu? | Booster tanıtımı | Bekliyor |
| T340 | Kartı kapatma | Kart açıkken ekrana dokun; bir kez de geri hareketini kullan | İkisi de kartı kaldırır; bölüm, stok ve hamleler değişmez | Booster tanıtımı | Bekliyor |
| T341 | Bölüm kartında uzun basma | Bölüm kartında üç başlangıç booster'ına ayrı ayrı uzun bas | Kart öne iner; fırça + sıçrama patlar, palet bir rengi toplar, +3 madalyonda sayılır; hiçbiri seçilmez | Booster tanıtımı | Bekliyor |
| T342 | Kayıptan sonra tanıtım | Bir bölümü kaybet ya da bir hamleden sonra çık; bölüm kartını yeniden aç | Üç başlangıç booster'ı sırayla aydınlanır, her birinde tek cümle; "Oyna"ya dokunmak oynatmaz, yalnızca ilerletir | Booster tanıtımı | Bekliyor |
| T343 | Tanıtım bir kez | Tanıtımı bitir, kartı kapatıp yeniden aç; uygulamayı kapatıp aç, başka bir bölümü kaybet | Tanıtım bir daha gelmez | Booster tanıtımı | Bekliyor |
| T344 | Kartın canlandırması | Yedi kartın her birini birkaç tur izle | Etki anlaşılıyor; düşen taşlar bazen özel taş yapsa da kafa karıştırmıyor; hız rahat | Booster tanıtımı | Bekliyor |
| T345 | Dillerde | Ayarlardan İngilizce ve Koreceye geçip adımları ve kartları gez | Her yazı hapına ve kartına sığar, doğal okunur | Booster tanıtımı | Bekliyor |

### F16. On üç dil (#8)

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T350 | Telefon diliyle ilk açılış | Oyunun verisini sil; telefonu sırayla Deutsch, Français, Español, Italiano, Bahasa Indonesia yap, her birinde oyunu aç (telefon dili: proje sahibi) | Oyun telefonun dilinde açılır; ayarlardaki dil satırı o dilin kendi adını gösterir | Diller | Bekliyor |
| T351 | Portekizce bölgeleri | Telefonu Português (Portugal), sonra Português (Brasil) yap; her seferinde verisi silinmiş oyunla aç | İkisinde de oyun Brezilya Portekizcesi; dil satırı "Português (Brasil)" | Diller | Bekliyor |
| T352 | Çince bölgeleri | Telefonu 中文 (简体, 中国), sonra 中文 (繁體, 台灣) ve 中文 (香港) yap; verisi silinmiş oyunla aç | Çin: 简体中文; Tayvan ve Hong Kong: 繁體中文; hiçbir yerde boş kutu yok | Diller | Bekliyor |
| T353 | Desteklenmeyen dil | Telefonu العربية, ไทย, Nederlands yap; verisi silinmiş oyunla aç | Oyun İngilizce açılır | Diller | Bekliyor |
| T354 | On üç dillik kart | Ayarlar > Dil | Kart tek başına asılı; iki sütunda 13 dil, her biri kendi yazısıyla; seçili dilde yeşil tik; Tamam, kapatma düğmesi ve geri hareketi kartı kapatır; bütün satırlara parmakla rahat basılır | Diller | Bekliyor |
| T355 | Japonca tam tur | 日本語 seç; salon, harita, koleksiyon, bölüm kartı, bir bölüm, kazanma kartı, +5 teklifi, mağaza | Boş kutu yok; kana ve kanji temiz; satır başında küçük kana, ー, 。、！ yok; plaka başlıkları doğal yerden kırılıyor | Diller | Bekliyor |
| T356 | Han karakterlerinin biçimi | Aynı ekranı 日本語, 简体中文, 繁體中文 ile sırayla aç; 次, 今, 海, 道 ve ：！ işaretlerine yakından bak | Her dil kendi biçimini çizer: basitleştirilmiş Çincede ： ve ！ hücrenin solunda, gelenekselde ortada; Japonca biçimler Japon yazısı gibi | Diller | Bekliyor |
| T357 | Rusça | Русский seç; mağaza (ücretsiz şerit), altın paketi kartı, öğretici, bölüm kartı | Kiril harfler tasarım yazısıyla uyumlu; "+1 ход / +2 хода / +5 ходов" doğru; «» tırnakları ve "1 250" ayırıcısı görünür | Diller | Bekliyor |
| T358 | Fransızca noktalama | Français seç; kazanma kartı, ayarların alt satırı, koleksiyonda "Les Deux Sœurs" | ! ? : ; satır başına düşmez; œ doğru çizilir | Diller | Bekliyor |
| T359 | İspanyolca ve Almanca uzun sözcükler | Español: bölüm tahtası ve bölüm kartı; Deutsch: başlangıç booster'ları ve booster kartları | ¡ ¿ çizilir; "MOVIMIENTOS" madalyonda rahat okunuyor mu, yoksa "JUGADAS" mı olmalı? Hiçbir şey kenardan taşmaz | Diller | Bekliyor |
| T360 | Öğretici ve booster tanıtımı yeni dillerde | Yeni oyuncu olarak ja, zh-Hant, ru, de ile ilk üç bölümü ve booster tanıtımını oyna (uzun basma dahil) | Haplar ve kartlar sığar, yazılar okunur | Diller | Bekliyor |
| T361 | İki ekran | İç ve kapak ekranında ja, ru, de ile salon, dil kartı, koleksiyon; oyun açıkken telefonu açıp kapat | Dil kartı ve plakalar iki ekranda da sığar; açıp kapatınca boş kutu ya da taşma yok | Diller | Bekliyor |
| T362 | Hızlı dil değiştirme | Dil kartında ja → zh-Hans → zh-Hant → ru → ko → tr arasında hızlıca 10 kez geç | Takılma yok; kart ve sayfa hemen yeni dilde; uygulama kapanmaz | Diller | Bekliyor |
| T363 | Seçim telefon dilini geçer | Telefon Türkçeyken oyunda Русский seç, oyunu tamamen kapatıp aç; sonra telefonu 日本語 yap, yeniden aç | İki açılışta da oyun Rusça kalır | Diller | Bekliyor |

### F17. Müzik (#9)

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T370 | Soğuk başlangıçta müzik | Oyunu tamamen kapatıp aç; salonda 40 saniye bekle | Müzik yumuşak girişle başlar; yaklaşık 33. saniyede girişten döngüye boşluksuz ve tıksız geçer | Müzik | Bekliyor |
| T371 | Döngü ek yeri | Yaklaşık 2 dakika 20 saniye dinle, özellikle 2:17 civarını | Atlama, tık, ritim kayması ya da ses sıçraması yok | Müzik | Bekliyor |
| T372 | Ekran değişince | Salon, harita, koleksiyon, mağaza; sonra bir bölüm oyna, kazan ya da kaybet, müzeye dön | Müzik hiç başa dönmez, kesilmez, düzeyi değişmez | Müzik | Bekliyor |
| T373 | Efektlerin altında | Bir bölümde eşleşme, özel taş ve booster sesleri çıkar; medya sesi orta | Taş ve booster sesleri müziğin üstünde açıkça duyulur; değilse istenen düzeyi söyleyin | Müzik | Bekliyor |
| T374 | Anahtar hemen etki eder | Ayarlarda Müzik'i kapat, sonra aç | Kapatınca yaklaşık 0,3 sn'de susar, efektler sürer; açınca kaldığı yerden gelir | Müzik | Bekliyor |
| T375 | Ayar saklanır | Müzik'i kapat, oyunu tamamen kapatıp aç; sonra açıp yeniden başlat | Kapalıyken açılışta müzik yok, anahtar kapalı; açıkken müzik çalar | Müzik | Bekliyor |
| T376 | Arka plan | Müzik çalarken ana ekrana dön, 10 sn bekle, geri gel; ekranı kilitleyip aç | Arka plandayken müzik susar; dönünce kaldığı yerden gelir | Müzik | Bekliyor |
| T377 | Eski kaydın üstüne | Önceki APK'nın kaydı olan telefona yeni APK'yı kur | Müzik açık başlar; Ses, Titreşim, dil, ilerleme ve cüzdan yerinde | Müzik | Bekliyor |
| T378 | Ayarlar kartı sığar | Türkçe, İngilizce, Korecede iki ekranda ayarlar kartı; bir kez Test modu açıkken | Ses, Müzik, Titreşim satırları hizalı; hiçbir yazı taşmaz; Test modunda dört anahtar sığar | Müzik | Bekliyor |
| T379 | Başka uygulamanın müziği | Bir müzik uygulaması çalarken oyunu aç | Ne olduğunu not edin (bugün oyun kendi müziğini üstüne çalar); istenen davranışa proje sahibi karar verir | Müzik | Bekliyor |
| T380 | Kendi müziğin çalarken açılış | Spotify ya da YouTube Music'te şarkı başlat, oyunu aç, bir bölüm oyna; ayarlarda Müzik anahtarına bak | Oyunun müziği hiç başlamaz; efektler şarkının üstünde duyulur; Müzik anahtarı açık görünür | Müzik | Bekliyor |
| T381 | Oyundayken başlat ve durdur | Oyunun müziği çalarken bildirim panelinden müziği başlat; sonra duraklat; bir kez de kulaklık düğmesiyle | Başlayınca oyunun müziği en geç 2 sn'de yumuşakça susar; durunca 2–4 sn içinde kaldığı yerden döner; efektler hiç kesilmez | Müzik | Bekliyor |
| T382 | Şarkı geçişleri | Müzik çalarken birkaç kez sonraki şarkıya geç, çalma listesinin geçişlerini bekle | Şarkı aralarında oyunun müziği bir an bile girip çıkmaz | Müzik | Bekliyor |
| T383 | Arka plandan dönüş | Oyunu arka plana al, müziği başlat, dön; sonra müziği durdur, dön | Müzik çalarken dönünce oyunun müziği sessiz; durmuşken dönünce kaldığı yerden yumuşakça gelir | Müzik | Bekliyor |

### F18. Dünya haritası (#4)

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T390 | Haritanın açılışı | Salondan Harita'ya geç (dokunarak ve kaydırarak). | Önce bütün dünya; yarım saniye kadar sonra kamera 0,85 sn'de oyuncunun dönemine kayar, iğneler kayarken görünmez, varınca belirir; takılma yok. | Harita | Bekliyor |
| T391 | Kamera akıcılığı | Şeritten 10 kez farklı dönemlere, sonra küre ve hedef düğmelerine bas. | Her kayış akıcı (60 fps hissi), kıyı çizgileri her yakınlıkta aynı incelikte, harita çerçevenin dışına taşmıyor. | Harita | Bekliyor |
| T392 | Kıyılar ve ışıltı | 6. dönemde (Tokyo, en yakın) ve dünyada kıyılara yakından bak. | İnce kahverengi kıyı çizgisi ve denize doğru yumuşak açık turkuaz ışık; diken, kopukluk ya da çift parlama yok; dalga deseni ve ızgara görünüyor. | Harita | Bekliyor |
| T393 | Şeridi kaydırma | Dönem şeridini parmakla sola ve sağa sürükle, bir kez de hızlı fiske at, uçlarda çek. | Şerit kayar, sayfa değişmez; fiske biraz devam eder; uçta hafif esneyip geri döner; çerçeve kenarındaki madalyonlar soluklaşarak kaybolur. | Harita | Bekliyor |
| T394 | Sayfa kaydırma | Haritanın üstünde (şeridin dışında) yatay kaydır; kart açıkken de dene. | Kart kapalıyken komşu sayfaya (Müze, yani salon) geçer, sağa çekince esner; kart açıkken hiçbir şey kaymaz. | Harita | Bekliyor |
| T395 | İlerideki tablonun kartı | İleride bir döneme git, bir iğneye dokun. | Kart açılır: resim örtülü ('?', 'ÖRTÜLÜ'), oda/tablo, ad, ressam · yıl, yer, 'Bugün: müze', kilitli hap 'Bölüm a–b'te açılır'; çarpı ve geri tuşu kapatır. | Harita | Bekliyor |
| T396 | Şu anki tablo | Hedef düğmesine bas; kartta 'Oyna'ya dokun, sonra geri. | Kamera oyuncunun dönemine gider ve şu anki tablonun kartı açılır: 'Parça p / N · Bölüm L'; 'Oyna' bölüm kartını getirir; geri tuşu haritaya ve aynı karta döndürür. | Harita | Bekliyor |
| T397 | Onarılmış tablo | Onarılmış bir iğneye dokun, 'Koleksiyonda gör'e bas. | Kartta resim ve yeşil 'Onarıldı'; bağlantı koleksiyonu o odada açar. | Harita | Bekliyor |
| T398 | Karttaki oklar | Bir kartta yukarı/aşağı oklarla beş tabloyu dolaş. | Sırayla dolaşır, 5'ten 1'e döner, 'k/5' doğru; seçilen iğne büyür ve turkuaz halkalıdır. | Harita | Bekliyor |
| T399 | Dokunma hedefleri | Küçük düğmelere (kart okları, çarpı, madalyonlar, iğneler) parmakla bas. | Hepsi ilk denemede basılıyor; yanındakine yanlışlıkla basılmıyor. | Harita | Bekliyor |
| T400 | Diller | Ayarlardan en, ko, ja, ru, zh-Hant seçip haritayı gez. | Plaket, efsane, okyanus adları, pusula harfi, şerit adları, kart yazıları ve yerler o dilde; hiçbir yazı kesilmiyor, eksik harf (kutu) yok. | Harita | Bekliyor |
| T401 | Açık katlanabilir (iç ekran) | Telefonu açıp haritayı gez, kart aç. | Harita kare ekrana yerleşir; kart açıkken iğneler görünür kalır; şerit ve düğmeler çalışır. | Harita | Bekliyor |
| T402 | Uzun kullanım | Haritada 2 dakika boyunca dönemler arasında gez. | Telefon belirgin ısınmıyor, akıcılık düşmüyor. | Harita | Bekliyor |
| T403 | Sayfalar arası gidip gelme | Harita ile salon/koleksiyon arasında 10 kez gidip gel. | Çökme yok; haritaya geçişte belirgin takılma yok, gecikme artmıyor. | Harita | Bekliyor |

### F19. Salonda geri tuşu ve dokunuş eşiği (23 Ekim)

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T410 | Çıkış kartı | Salonda, hiçbir kart açık değilken Android geri tuşuna bas. | "Oyundan çıkılsın mı?" kartı iner; kartın dışına dokunmak bir şey yapmaz; "Vazgeç" kartı kaldırır; kart açıkken yeniden geri de kaldırır. Yazılar kesik değil, Türkçe harfler doğru. | Geri tuşu | Bekliyor |
| T411 | Çık | Kartta "Çık"a bas; oyunu yeniden aç. | Oyun kapanır. Yeniden açınca ilerleme, cüzdan ve ayarlar kapanmadan önceki gibi. | Geri tuşu | Bekliyor |
| T412 | Kenardan geri hareketi | Hareketli gezinmede salonda ekranın sol ya da sağ kenarından geri hareketini yap. | Çıkış kartı iner (tam ekranda ilk hareketin sistem çubuklarını göstermesi ayrı bir konudur: T235). | Geri tuşu | Bekliyor |
| T413 | Satırın üstünde kaydırma | Ayarlar kartında Dil ve Titreşim satırlarının üstünde yatay kaydır; bölüm kartında ve mağazada düğmelerin üstünde de kaydır. | Hiçbir şeye basılmaz: dil kartı açılmaz, anahtar değişmez. Aynı satırlara normal dokunuş eskisi gibi çalışır. | Dokunuş | Bekliyor |
| T414 | Titrek dokunuş | Küçük düğmelere (çarpı, kart okları, booster diskleri, iğneler) parmağı hafifçe oynatarak bas. | Hepsi ilk denemede basılıyor; eşik bir dokunuşu sürükleme saymayacak kadar geniş. | Dokunuş | Bekliyor |

### F20. Koleksiyonda bellek (24 Ekim)

Bellek `adb shell dumpsys meminfo com.saturnwave.puzzle` ile ölçülür (TOTAL PSS). 23 Ekim'de 20 oda gezilince 401 MB'tan 649 MB'a çıkıyordu (A42/4).

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T420 | 20 oda, bellek | Koleksiyonu aç, belleği ölç. Yuvarlak düğmelerle son odadan 1. odaya ve geri gez, yeniden ölç. Aynı gezintiyi plakette kaydırarak tekrarla, yine ölç. | İlk gezintiden sonra bellek açılıştakinden en çok 40–50 MB fazla (eskiden yaklaşık 250 MB); ikinci gezinti belleği artırmıyor. Çökme ya da uyarı yok. | Müze odaları, test modu | Bekliyor |
| T421 | Geri dönünce resimler | Bir odadan iki oda ileri git, sonra geri dön; ardından beş oda uzağa gidip dön. | Son iki odaya dönünce resimler hemen orada. Uzak odada resimler 1 saniye içinde geliyor; boş tuval, beyaz ya da pembe kare yok. | Müze odaları | Bekliyor |
| T422 | Hızlı kaydırma | Plakette arka arkaya hızlı kaydırarak 10 oda geç, sonra yavaş sürükleyip bırak. | Kaydırma akıcı; sürüklenen komşu odanın resimleri parmak inikken görünüyor; takılma ya da yanıp sönme yok. | Müze odaları | Bekliyor |
| T423 | Tabloya yakından bakma | Onarılmış bir tabloya dokun, büyüsün; dokunup geri gönder ve hemen yan odaya geç. Birkaç odada tekrarla. | Tablo büyürken ve geri dönerken resmi hep görünüyor; yan odaya geçişte boş ya da bozuk resim yok. | Müze odaları | Bekliyor |
| T424 | Sayfalar ve sahneler | Koleksiyon, salon ve harita arasında 10 kez gidip gel; haritada iki dönemin kartını aç; sonra bir bölüm oyna, yapbozu aç ve salona dön. Belleği başta ve sonda ölç. | Salonun tablosu, plaketin küçük resimleri, haritanın iğneleri ve kartı, yapboz ve ödül resmi doğru; hiçbir resim boş değil. Sondaki bellek baştakinden belirgin fazla değil. | Müze odaları | Bekliyor |
| T425 | Uzun gezinti | 5 dakika boyunca koleksiyonda ve haritada gez; mümkünse daha az bellekli bir telefonda da dene. | Çökme yok, uygulama arka plana atılmıyor; bellek dalgalansa da sürekli artmıyor. | Müze odaları | Bekliyor |

### F21. Giriş sayfası (24 Ekim)

Açılış adımlarının süreleri `adb logcat -s Unity` çıktısında "Start-up:" satırlarıdır.

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T430 | Soğuk açılış | Oyunu son uygulamalardan kaydırıp kapat, simgeden aç. | Önce giriş sayfası: krem zemin, palet, dört taş süzülüyor, yaldızlı başlık ve "ÇALIŞMA ADI" rozeti. Çubuk "Yükleniyor… %…" ile en az 1 saniyede doluyor; sıçramıyor, geri gitmiyor. Sonra "Başlamak için dokun" nefes alıyor. Müzik bu sayfada başlıyor. | Giriş sayfası | Bekliyor |
| T431 | Dokunup salona | Çubuk dolarken ekrana dokun ve kaydır; dolunca herhangi bir yere dokun. | Dolmadan dokunuş ve kaydırma bir şey yapmaz. Hazırken dokununca sayfa kararır, salon karanlıktan aydınlanır; salon, öğretici adımı ve hediye çerçevesi giriş sayfasından önceki gibi. | Giriş sayfası | Bekliyor |
| T432 | Arka plandan dönüş | Salonda ve bir bölümde ana ekrana çık, oyuna dön. Giriş sayfasındayken de arka plana at ve dön. | Salondan ve bölümden dönünce giriş sayfası çıkmaz, oyun kaldığı yerden sürer. Giriş sayfasındayken dönünce sayfa yerinde, çubuk kaldığı yerden dolar. | Giriş sayfası | Bekliyor |
| T433 | Geri tuşu | Giriş sayfasında çubuk dolarken ve dolduktan sonra Android geri tuşuna bas ya da kenardan geri hareketi yap. | Salondaki "Oyundan çıkılsın mı?" kartı iner. "Vazgeç" ve yeniden geri kartı kaldırır; "Çık" oyunu kapatır. Yeniden açınca ilerleme, cüzdan ve ayarlar yerinde. | Giriş sayfası, geri tuşu | Bekliyor |
| T434 | İki ekran, güvenli alan | Dış ekranda aç; katlıyı açıp iç ekranda aç; sayfa açıkken katla ve aç. | Hiçbir şey kamera çentiğinin, durum çubuğunun ya da hareket çubuğunun altında değil. Başlık, rozet, çubuk ve alt satır kesik değil. Ekran değişince sayfa hemen yeni ekrana yerleşiyor. | Giriş sayfası | Bekliyor |
| T435 | Diller | Ayarlardan İngilizce, Almanca, Rusça ve Japonca seç; her birinde oyunu kapatıp aç. | Sayfanın yazıları seçilen dilde ve sığıyor; Almanca ve Rusçada yüzde işareti sayıdan sonra. "SaturnWave" her dilde aynı. | Giriş sayfası, diller | Bekliyor |
| T436 | Açılış süresi | Soğuk açılışı üç kez yap; logcat'te "Start-up:" satırlarını oku. | Simgeye dokunmaktan "Başlamak için dokun"a birkaç saniye. Oturum adımında (en uzun adım) taşların durması göze batmıyor; batıyorsa süreleri not et. | Giriş sayfası | Bekliyor |

### F23. Yan çevirme (24 Ekim)

Kural: iPad her zaman, Android'de kısa kenarı 600 dp ve üstü olan ekranlar (tablet, açık Fold) dört yöne döner; telefonlar ve Fold'un dış ekranı dik kalır. Yatay tablette salonun yanlarında odanın onarılmış tabloları, haritada geniş çerçeve, yapbozda tablo solda ve tepsi sağda. iPad testleri `feature/ios` derlemesiyle yapılır.

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T450 | iPad salonda dönme | Salonda iPad'i yan çevir, sonra iki yöne de çevir, dik tut. | Oyun her seferinde dönüyor, yatık kalmıyor; siyah bant yok. Yanda: ortada salon, lamba çerçevenin yanında, ötesinde odanın onarılmış tabloları (en çok dört). Hiçbir şey durum çubuğunun ya da ana ekran çizgisinin altında değil. | iPad | Bekliyor |
| T451 | iPad ters dik | iPad'i baş aşağı (ters dik) tut; salon, bölüm ve yapbozda dene. | Oyun ters dike de dönüyor ve dik iPad'deki gibi görünüyor. | iPad | Bekliyor |
| T452 | Taşlar düşerken ve sürüklerken | Bir bölümde hamle yap ve taşlar düşerken çevir. Sonra bir taşı sürüklemeye başla, parmak ekrandayken çevir, parmağı komşu taşa götürüp kaldır. | Düşüş yeni ekranda doğru biter, taşlar yerinde. Çevirmeden önce başlayan sürükleme hiçbir taşı takas etmez; parmağı kaldırıp yeniden sürükleyince hamle oynanır. | iPad | Bekliyor |
| T453 | Yapboz yan | Bekleyen parçası olan tablonun yapbozunu yan aç; bir parçayı taşırken çevir; sonra yan ve dik parçaları yerine koy. | Yanda tablo solda büyük, sağda pip çubukları, tepsi (en çok altı parça, iki ya da üç sütun) ve "Otomatik tamamla". Taşınırken çevrilen parça tepsiye döner, yanlış yere düşmez. Parçalar her iki düzende yerine oturur. | iPad, bekleyen parça | Bekliyor |
| T454 | Harita yan | Haritayı yan aç; bir döneme dokun, bir iğnenin kartını aç, kart açıkken çevir. | Yanda çerçeve geniş, dünya ve şeritte 9–10 dönem birden görünüyor. Kart ortada, düğmeleri basılıyor; dönünce kart yerinde kalıyor ya da düzgün kapanıyor. | iPad | Bekliyor |
| T455 | Kartlar açıkken | Bölüm kartı, can kartı, ayarlar (gizlilik, destek, dil), mağaza, bölümden çık kartı, +5 hamle kartı ve kazanma kartı açıkken çevir; düğmelerine bas. | Kart yeni ekranda ortada ve tam görünüyor; düğmeleri ilk dokunuşta çalışıyor. Arkadaki sayfa da yeni ekrana yerleşmiş. | iPad | Bekliyor |
| T456 | Finalde | Bir bölümü hamle artarak kazan; final (roketler) sürerken çevir. | Final ya da kazanma kartı yeni ekranda düzgün; tahta bozulmuyor, kart geliyor, altın doğru. | iPad | Bekliyor |
| T457 | Pencereli Uygulamalar | iPadOS ayarlarında Pencereli Uygulamalar'ı aç; oyunun penceresini küçült, büyüt, köşesinden boyutlandır. | Oyun her boyutta yeni şekle yerleşiyor (dar pencerede telefon gibi, geniş pencerede yatay düzen). Sol üstteki pencere düğmeleri geri düğmesini ya da plaketi örtmüyor; örtüyorsa not et. | iPad | Bekliyor |
| T458 | Stage Manager ve yan yana | Stage Manager'da ve başka bir uygulamanın yanında (bölünmüş ekran) oyunu oyna. | Oyun çalışıyor, donmuyor; pencere değişince sayfa yeniden yerleşiyor. Müzik ve dokunuşlar normal. | iPad | Bekliyor |
| T459 | iPad'de açılış yan | iPad yan dururken oyunu kapatıp aç; giriş sayfası dolarken çevir. | Giriş sayfası yanda düzgün; çevirince yeniden yerleşiyor, çubuk kaldığı yerden doluyor. Dokununca salon geliyor. | iPad | Bekliyor |
| T460 | Fold iç ekran dönmesi | Fold'u aç; salon, bölüm, yapboz ve haritada cihazı yan çevir ve geri çevir. | İç ekranda oyun dönüyor; yan tutuluşta bugünkü düzen (yapbozda tepsi altta, harita dar). Kamera deliği yana geçtiğinde geri düğmesi ve yazılar altında kalmıyor. `adb logcat -s Unity` içindeki "The screen is" satırı yan payı gösteriyor: kamera solda `2152x2076+0+0+N+0`, sağda `2152x2076+0+0+0+N` (N sıfırdan büyük). | Fold | Bekliyor |
| T461 | Yan tutarken katla ve aç | İç ekranda yan tutarken bir bölümde ve salonda Fold'u katla; dış ekranda oyna; yeniden aç. | Katlayınca dış ekranda oyun dik; açınca iç ekranda cihazın tutuluşuna göre dönüyor. Bölüm ve ilerleme kaybolmuyor. | Fold | Bekliyor |
| T462 | Dış ekran dönmez | Fold kapalıyken (ve bir Android telefonda) cihazı yan çevir. | Oyun dik kalıyor, hiç yan görünmüyor; her şey eskisi gibi. | Fold, telefon | Bekliyor |
| T463 | Döndürme kilidi | Android'in döndürme kilidini (otomatik döndürme kapalı) aç, iç ekranı ve tableti yan çevir; sonra kilidi kapat. | Kilit açıkken oyun dönmüyor; kilit kapalıyken dönüyor. | Fold ya da Android tablet | Bekliyor |
| T464 | Android tablet | Tablet yan dururken oyunu aç; salon, harita, yapboz ve bir bölüm oyna. Ters dik de dene. | Oyun yan açılıyor (ilk anda dik bir kare görünüp dönebilir; titreme göze batıyorsa not et). Salonda tablolar, haritada geniş çerçeve, yapbozda tablo solda. | Android tablet | Bekliyor |
| T465 | Telefon değişmedi | Pixel'de (dış ve iç ekran dik) salon, harita, koleksiyon, yapboz ve bir bölüme bak; `adb logcat -s Unity` içinde "The screen is" satırlarını oku. | Görünüm 24 Ekim'den önceki gibi; dış ekranda hiçbir şey dönmüyor. Dik ekranda satır yalnız üst ve alt payı gösteriyor (`1080x2424+üst+alt`), yan pay yok. | Telefon | Bekliyor |

### G. Görünüm

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T60 | Türkçe yazılar | Bütün ekranlarda ğ, ı, İ, ş, ç, ö, ü harflerine bak | Kutu ya da eksik harf yok; yazılar net | — | Bekliyor |
| T61 | Gece modu | Telefonu koyu temaya al | Oyun kendi renkleriyle kalır, okunur | — | Bekliyor |

### H. Herkese açık web testi

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T70 | Web oyuncu sayfası | Telefonun tarayıcısında oyuncu sayfasını aç, giriş yap, bir bölüm oyna | Düzen sığar; dokunma çalışır; kayıt sunucuya düşer | Sunucu açık | Bekliyor |
