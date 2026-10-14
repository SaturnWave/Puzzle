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
| A01 | Telefonun saati ileri alınırsa canlar dolar. | Kurallar, §23 | Düşük; sunucusuz bir oyunda kapanmaz | Kabul edildi; T32 davranışı gözler |
| A02 | Can kaybedilirken saat geri alınmışsa, saat düzeltilince bir sonraki canın zamanı geçmişte kalır ve can hemen döner. | Kurallar, §23 | Düşük | Kabul edildi; T33 |
| A03 | Kurallar (`Puzzle.Meta`) ve kayıt dosyası telefonda ve IL2CPP derlemesinde hiç çalıştırılmadı; yalnızca Editor'da ve .NET'te. | 1. aşama | Orta: IL2CPP'de yansıma ve JSON okuma farklı davranabilir | T01, T20 |
| A04 | Bitiş şöleninin telefonda kare hızı ve gerçek parmakla atlanması ölçülmedi. | §23 | Orta | T12 |
| A05 | 10 sütunlu tahtada hücre yaklaşık 36 pt; önerilen 44 pt dokunma hedefinin altında. | §2, §22 | Orta: yanlış dokunuş | T10 |
| A06 | Bantlı bir parça, bandının çıktığı gösterilmeden hareket edebiliyor. | Oturum 20 | Düşük, görsel | Kullanıcının ayrı oturumunda ele alınıyor; sonucu henüz bu dalda yok |
| A07 | Booster'lı bir deneme kayıttan birebir yeniden oynatılamıyordu. **Kapandı (2026-10-14, aşama 3):** eylemler sırasıyla kaydediliyor ve yeniden oynatılıyor. | §18 | — | Kapandı; telefonda T30–T33 |
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
| A19 | Can denetimi yok: canı bitmiş oyuncu bölüm başlatabiliyor; kayıp sayacı 5'te durur. | Aşama 2 | Bilinçli, geçici | Arayüz planının 7–8. aşamaları; T27 |
| A20 | Cüzdan oyunda henüz görünmüyor; yalnızca kayıt dosyasında. | Aşama 2 | Geçici | Ekran aşamaları |
| A23 | Telefondaki deneme dosyası sınırsız büyür (deneme başına yaklaşık 1,1 KB); "Kayıtları sil" yok. Bölüm ortasında kapatılan oyun kayıt bırakmaz. | Aşama 3 | Düşük | Ayarlar ekranı aşamasında |
| A22 | Uzun dönem adları (ör. "Amerika: Hudson Nehri ve Sınır Boyları") koleksiyon plaketinde iki düğmenin arasına sığmak için küçük yazılıyor; harita da uzun adı küçültüyor. | Müze, 2026-10-14 | Okunurluk | T44 |
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
| T23 | Eski kayıt yeni katmana geçer | 13 Ekim APK'sıyla en az bir bölüm kazan, bir parça yerleştir; yeni APK'yı üstüne kur; bir hamle oyna, sonra hamle yapmadan çıkmadan, yuvarlak düğmeyle bölümden çık; dosyayı oku | Salon aynı bölüm ve parçalarla açılır; dosya `"version":3` ile başlar; cüzdanda 200 altın, her booster'dan 3, `"livesShort":1` (hamleden sonra çıkmak bir can) | Aşama 2 | Bekliyor |
| T24 | Kazanç ve altını kazandıran hamlede yazılır | Dosyadaki `coins` değerini not et; hamle kalmışken bir bölüm kazan; şölenin roketleri başlar başlamaz uygulamayı son uygulamalardan kaydırarak kapat; aç ve dosyayı oku | Salon sonraki bölümde açılır; `levelsWon` bir arttı; `coins` kalan her hamle için 4 arttı | Aşama 2 | Bekliyor |
| T25 | Kayıp bir can götürür, 30 dakikalık saati başlatır | Can doluyken (`"livesShort":0`) hamlesi biterek bir bölüm kaybet; kayıp kartı gelir gelmez dosyayı ve telefonun saatini (`adb shell date +%s`) oku | `"livesShort":1`; `nextLifeAtUnixSeconds` kaybettiren hamlenin anı artı 1800 (birkaç saniye içinde). Yarım saat içinde ikinci kayıp: 2, aynı `nextLifeAtUnixSeconds` | Aşama 2 | Bekliyor |
| T26 | Hamlesiz çıkış ve yarıda kapatma ücretsiz | Bir bölüm aç, hamle yapmadan yuvarlak düğmeyle müzeye dön, dosyayı oku. Sonra bir bölümde birkaç hamle oyna, uygulamayı kaydırarak kapat, aç, dosyayı oku | İkisi de `livesShort`'u değiştirmez; ikincisi yalnızca denemeyi sayar (`attemptsOnLevel` bir artar), oyun salonda açılır | Aşama 2 | Bekliyor |
| T27 | Can bitince oyun bozulmaz | 30 dakika içinde beş bölüm kaybet, sonra altıncıyı | Oyun sürer (can denetimi henüz yok, A19); dosya `"livesShort":5`'te kalır, altıncı kayıp bir şey değiştirmez | Aşama 2 | Bekliyor |
| T29 | Cihaz kimliği | Yeni APK'yı kur, oyunu bir kez aç; `adb shell run-as com.saturnwave.puzzle cat files/device.json` ile kimliği not et; uygulamayı durdurup aç, yeniden oku; aynı APK'yı üstüne kur, oku; kaldırıp kur, oku | İlk üç okumada aynı kimlik; kaldırıp kurunca yeni bir kimlik | Aşama 3 | Bekliyor |
| T30 | Kazanılan bölümün satırı | `attempts.jsonl` satır sayısını not et; hamle kalmışken bir bölüm kazan, şölen başlar başlamaz uygulamayı kapat; son satırı oku | Bir satır fazla: `"result":"won"`, `"client":"unity"`, `deviceId` device.json'daki kimlik, `moveLimit` bölümün kendi limiti, hamle kadar `{"act":"move"}` | Aşama 3 | Bekliyor |
| T31 | Kaybedilen bölümün satırı | Hamlesi biterek bir bölüm kaybet; kayıp kartı gelir gelmez dosyayı oku | Bir satır fazla: `"result":"outOfMoves"`, `movesUsed` bölümün hamle sayısı | Aşama 3 | Bekliyor |
| T32 | Bırakma ve kapatma | Hamle yapmadan bölümden çık; iki hamle oynayıp çık; bir hamle oynayıp uygulamayı kapat; her adımdan sonra satırları say | İlki satır eklemez; ikincisi `"result":"abandoned"`, `"movesUsed":2` ile bir satır ekler; üçüncüsü satır eklemez | Aşama 3 | Bekliyor |
| T33 | Araçlar telefonun dosyasını okur | Dosyayı bilgisayara al (`adb shell run-as com.saturnwave.puzzle cat files/attempts.jsonl > phone-attempts.jsonl`); `dotnet run --project src/Puzzle.Cli -c Debug -- humans phone-attempts.jsonl --out out/phone-humans.csv` | Her satır okunur, okunamayan satır yok; denemeler bölümlerine sayılır | Aşama 3 | Bekliyor |
| T28 | Kaydedilen an yeniden başlatmadan sağ çıkar | Bir kayıptan sonra `livesShort` ve `nextLifeAtUnixSeconds`'u not et; `adb shell am force-stop com.saturnwave.puzzle` ile durdur, aç; yarım saat içinde bir bölüm kazan, dosyayı oku | `livesShort` ve `nextLifeAtUnixSeconds` öncekiyle birebir aynı; `coins` ve `levelsWon` kazançla arttı | Aşama 2 | Bekliyor |

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

### G. Görünüm

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T60 | Türkçe yazılar | Bütün ekranlarda ğ, ı, İ, ş, ç, ö, ü harflerine bak | Kutu ya da eksik harf yok; yazılar net | — | Bekliyor |
| T61 | Gece modu | Telefonu koyu temaya al | Oyun kendi renkleriyle kalır, okunur | — | Bekliyor |

### H. Herkese açık web testi

| Kod | Test | Adımlar | Beklenen | Ön koşul | Durum |
|---|---|---|---|---|---|
| T70 | Web oyuncu sayfası | Telefonun tarayıcısında oyuncu sayfasını aç, giriş yap, bir bölüm oyna | Düzen sığar; dokunma çalışır; kayıt sunucuya düşer | Sunucu açık | Bekliyor |
