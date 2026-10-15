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
| 4 | Unity denetimleri | `ProjectSetup`, `EngineSmokeTest`, `EconomyCheck`, `FinaleCheck`, `PlayModeCheck`, `TouchWhileMovingCheck`, `ScreenShots -screenWalk` (komutlar: GAME_DESIGN §23) |
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
| A10 | Ses ve titreşim yok. | §23 | Bilinçli | Sonraki faz |
| A11 | Bölüm geçişinde duvar, plaketler ve çerçeve yeniden kuruluyor; süresi telefonda ölçülmedi. | Oturum 16 | Düşük | T15 |
| A12 | 40 parçalı otomatik tamamlama, geri tuşu ve uzun oynama telefonda denenmedi. | Oturum 17 | Orta | T41, T42, T13 |
| A13 | İç (kare) ekran, ucuz telefonlar ve uygulamayı arka plana alıp dönme denenmedi. | Oturum 16 | Orta | T02, T14 |
| A14 | 100 tablonun bellek ve yükleme süresi telefonda ölçülmedi (her biri yaklaşık 1 MB, uzun kenarı yaklaşık 1920 piksel). | Müze | Orta: bellek | T40 |
| A15 | Beyaz yazının alt çevre çizgisi tasarım çizimlerindekinden ince; bölüm başlığı ahşap üstünde soluk. | §23 | Görsel, küçük | Ekranlar aşamasında |
| A16 | Web oyuncu sayfası gerçek telefonda, Safari'de ve ekran okuyucuyla denenmedi. | Oturum 13 | Herkese açık test için | T70 |
| A17 | Bir sunucu testi (`PublicModeTests`, istek seli) bütün takım birlikte koşarken zamanlamaya takılabiliyor; tek başına geçiyor. | Oturum 21 | Telefonla ilgisi yok | Ayrı iş |
| A18 | `generate --repair` 899. bölümü her seferinde yeniden üretiyor. | Oturum 12 | Zararsız | — |
| A19 | Can denetimi yoktu. **Salondan kapandı (2026-10-14, aşama 7):** canı bitmiş oyuncu "Bölüm N" ile bölüm açamıyor. Bölüm başı kartı 8. aşamada gelecek. | Aşama 2 | Düşük | T93, T94 |
| A20 | Cüzdan oyunda görünmüyordu. **Kapandı (2026-10-14, aşama 7):** altın ve canlar müze sayfalarında; can bitince bölüm açılmıyor. | Aşama 2 | — | T90–T97 |
| A23 | Telefondaki deneme dosyası sınırsız büyür (deneme başına yaklaşık 1,1 KB); "Kayıtları sil" yok. Bölüm ortasında kapatılan oyun kayıt bırakmaz. | Aşama 3 | Düşük | Ayarlar ekranı aşamasında |
| A24 | Booster yazısı 9:16 telefonda uzun tahtalarda ve katlanır telefonun yatay iç ekranında en alt sıranın bir kısmını örtüyor. Dokunuşlar geçiyor, ama ilk denemedeki yazı ilk hamleye kadar o taşları gizliyor. | Aşama 10 | Orta | T134; yer ayırmak tahtayı küçültür |
| A25 | Teklif ya da canlandırma reklamı açıkken uygulama kapatılırsa can gitmez ve kayıt yazılmaz. | Aşama 11 | Düşük | Planın bildiği açık |
| A26 | Ekonomi simülatörü tablo ödülünü onuncu kazanmada veriyor, oyun son parça yerleşince. Parçalar geç yerleştirilirse ödül oyunda simülatördekinden geç gelir. | Aşama 12 | Düşük; tahminin günü biraz kayabilir | Simülatör son parça anına geçirilecek ya da fark ölçülecek |
| A27 | Bu sürümden önce tamamlanmış tablolar için ödül verilmez; ödül yalnızca son parçayla gelir. | Aşama 12 | Yalnızca eski test kayıtları | Kabul edildi |
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
| T43 | Geri tuşu | Her ekranda Android geri tuşuna bas | Bir önceki ekrana döner; salonda uygulamadan çıkış sorulur ya da uygulama arka plana gider | — | Bekliyor |

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

### G. Görünüm

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T60 | Türkçe yazılar | Bütün ekranlarda ğ, ı, İ, ş, ç, ö, ü harflerine bak | Kutu ya da eksik harf yok; yazılar net | — | Bekliyor |
| T61 | Gece modu | Telefonu koyu temaya al | Oyun kendi renkleriyle kalır, okunur | — | Bekliyor |

### H. Herkese açık web testi

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T70 | Web oyuncu sayfası | Telefonun tarayıcısında oyuncu sayfasını aç, giriş yap, bir bölüm oyna | Düzen sığar; dokunma çalışır; kayıt sunucuya düşer | Sunucu açık | Bekliyor |
