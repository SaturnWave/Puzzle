# Geliştirme Günlüğü

Her oturumun kararları, bulguları ve değişiklikleri. En yeni kayıt en üstte.

---

## 2026-10-26 · Oturum 34 — v1 sunucu

**Aşama 1: eşitleme çekirdeği (`feature/bombs-colors-director-webui`)**
- Sahibin kararları planın §10.2'sine işlendi: bütün öneriler tamam; bağlanma önerisi ilk kazanılan bölümden sonra (karar 2), silmeden sonra kalan iz 36 ay (karar 5). Planın §5.3, §5.6, §6.2 ve §6.5'i buna göre.
- Bitti (Unity'siz, sahte bulutla testli; paket yok, bulut projesi bağlı değil, oyuna henüz bağlanmadı): `CloudEnvelope` (buluttaki `save`: zarf, son 16 yazmanın kimliği, 16 KB'tan sıkıştırma), `ProgressMerge` (üç yollu birleştirme), `SyncPolicy` ve `SyncSettings` (bütçe, Remote Config'in dört anahtarı, geri çekilme, kapılar), `CloudState` ve `CloudStateFile` (`cloud.json`), `ICloudSave` ve `FakeCloudSave`, `CloudSync`, `PurchaseLedger` (sunucunun defteri), `LedgerNumbers`. İlerleme dosyası 7. düzen: `ledgerDelivered`.
- Birleştirme planın tablosunu izliyor; bir kez verilen kazancın (numaralı ödeme, booster açılış hediyesi, günün hediyesi, tablo ödülü) iki tarafta da alınınca bir kez sayılması ve ödül sayılarının "en büyüğü" yerine verilen ödül sayısı olması bu aşamada kesinleşti (plan §3.5 notu). Defterin adlandıramadığı çift teslimde birleştirme yapılmıyor.
- Cevabı kaybolan yazma (zaman aşımı) iki kez sayılmıyor: her yazma gönderilmeden `cloud.json`'a yazılıyor, sonraki okuma onu kaydın `writes` listesinde buluyor; başka telefon üstüne yazmış olsa da.
- Bütçe varsayılanı planın sıkı kuralı: okuma 12 saatte bir, yükleme en az 60 dakika arayla; Remote Config sınırlar içinde açar. Bozuk `cloud.json` anahtarı kapalı başlatıyor.
- Ölçüm: 1000. bölüm oyuncusu 21.411 bayt JSON, bulutta sıkıştırılmış 12.534 bayt.
- `ProgressFileCheck` 7. düzene güncellendi (6. düzenden beri eskiydi).
- 5.984 test geçti (101'i yeni); ekonomi denetimi temiz (100. bölüm 8. gün). Unity'de `ProgressFileCheck` (`-quit` ile), `PurchaseCheck` ("PASSED") ve `PlayModeCheck` ("game errors: False", ikisi `-quit` olmadan, sonuç satırları okundu) hatasız; yeni betikler Unity'nin derleyicisinde temiz. Telefon testleri T530–T539 (F28). Push yok, telefon ve Mac'e dokunulmadı.
- Sahipte: bulut projesini açıp proje kimliğini göndermek (plan §9 A1–A2); bütçe varsayılanının (60 dk / 12 sa) uygun olduğunu onaylamak; T530–T539 sonraki aşamalardan sonra.

## 2026-10-26 · Oturum 33 — Telefon turu düzeltmeleri

**26 Ekim otomatik telefon turunun bulguları kapandı (sahibin "evet başla, hepsini düzelt"i)**
- Google'a giden trafik (T186): kaynak Google Play ödeme kütüphanesinin kendi kullanım kayıtları. Billing 9.0.0 bunları Google'ın kayıt taşıyıcısıyla (`datatransport`, CCT arka ucu) `firebaselogging.googleapis.com`'a gönderiyordu, satın alma olmasa da. Kanıt: oyunun sürecinde açık `com.google.android.datatransport.events` veritabanı (meminfo), birleşik manifestteki taşıyıcı servisleri, kütüphanenin kodu (`javap`). Ayrıntı GAME_DESIGN §23.
- Çözüm: manifest adımı taşıyıcının arka uç kaydını (`backend:…CctBackendFactory`) `tools:node="remove"` ile çıkarıyor (`ManifestLoggingBackend`). Derleme APK'yı `aapt dump xmltree` ile denetliyor. Ödeme taşıyıcıdan geçmiyor; Billing'in kaydedicisi hatayı yutuyor. Ayar satırı, gizlilik kartı ve Data safety cevapları değişmedi. iOS'ta bu kütüphane yok.
- Tek geri hareketi (T235, A42/2): sistem çubukları `Default` davranışıyla gizleniyor; çubuklar kaydırmayla çıkarsa oyun onları 2 sn sonra yeniden gizliyor (`SystemBars`). Manifest adımı değeri denetliyor (`ManifestSystemBars`). Yerleşim değişmedi.
- Rehberli spatula adımında parmak nişan alırken rehber kartı ve büst soluklaşıyor (T503); booster yürüyüşü bunu denetliyor ve resmediyor.
- Satın alma bloğundaki tek geçersiz satır artık bütün ilerlemeyi silmiyor: satır kenara alınıyor, dosyanın o anki hâli `progress.json.purchases-set-aside` olarak saklanıyor. Ödeme kimlikleri korunuyor (iki kez teslim yok); sayısı okunmayan ürün satırı bir kez alınmış sayılıyor (Çırak Çantası tahminle yeniden satılmıyor).
- Japonca ve Çincede açılış işareti ("（", "“") artık satır sonunda kalmıyor: TextMesh Pro'nun harf listesi tam genişlik biçimleri saymıyor; oyun işareti ardındaki harfle `<nobr>` içinde tutuyor (`LineBreaks.ReadyForWrapping`).
- T420: +61 MB'ın dağılımı ölçüldü: grafik belleği +51, Unity'nin kendi belleği +8,6. 40–50 MB ölçümsüz bir tahmindi; beklenen değer en çok yaklaşık 65 MB'a düzeltildi. Daha azı bir şeyden vazgeçmek demek, karar sahibin (A52).
- Test metinleri: T84'e `--levels levels/generated`; T23, T52, T108, T121, T160, T161, T181 bu derlemeye göre. Yeni testler T520–T522, açık A52.
- 5.883 test geçti (22'si yeni); ekonomi denetimi temiz (100. bölüm 8. gün). Unity'de `CollectionMemoryCheck`, `PurchaseCheck`, `PlayModeCheck` ve `MusicCheck` (son üçü `-quit` olmadan, sonuç satırları okundu), `ConsentCheck`, `AvatarCheck`, `RotationCheck`, `EngineSmokeTest` hatasız. Japonca gizlilik kartı, rehberli nişan anı ve salon 1080×2424 ve 2732×2048'de bakıldı. APK 290 MB; `aapt`: paket `com.saturnwave.museumpuzzle`, izinler tam beş, dik, oyun kategorisi, çubuklar `Default`, `CctBackendFactory` kaydı yok. Kopyası `D:/puzzle-cache/apk-share/museumpuzzle-2026-10-26.apk`. Telefon bağlı değildi; telefonda yeniden denemeler sonraya kaldı (A52).
- Sahipte: T420 için daha az bellek isteniyorsa iki yoldan biri (A52); APK'yı telefonda denemek (T186'yı 35 dakika, T235/T412/T522'yi gerçek başparmakla, T503, T520, T521); Play Console'da ürünler açılınca T186'yı satın almayla yeniden. Telefon bu oturumda bağlı değildi.

## 2026-10-24 · Oturum 31 — v1: satın alma

**Kalıcı uygulama kimliği (2026-10-25)**
- Proje sahibinin kararı: iki mağazanın kalıcı uygulama kimliği `com.saturnwave.museumpuzzle` (Android'in paket adı, iOS'un bundle kimliği); yer tutucu `com.saturnwave.puzzle` bitti. Neden şimdi: iOS derlemesi StoreKit'i (IAP 5.4.4) bağlıyor, Xcode In-App Purchase için açık bir App ID istiyor; takımın joker geliştirme profili bunu taşıyamıyor (son Mac derlemesi: "doesn't include the In-App Purchase capability"). Oyunun adı ("Puzzle") hâlâ geçici; kimlik adla değişmeyecek.
- Bitti (ana dal): `AndroidBuild`, `ProjectSettings.asset`, `StoryStudio`'nun vekil telefon klasörü, `PackagePermissions`'ın, `StoreTalk`'un ve `EconomySettings`'in yorumları, `economy/default.json` (`[decided 2026-10-25]`), testler; GAME_DESIGN §19, OVERVIEW ve PHONE_TESTS'in adb yolları. Ürün kimlikleri onu izliyor: `com.saturnwave.museumpuzzle.coins.pouch`, `.coins.bag`, `.coins.chest`, `.coins.treasure`, `.starter.apprentice_kit`. Günlüğün eski kayıtları eski kimliği tutuyor.
- Android'de yeni paket eski test uygulamasının (`com.saturnwave.puzzle`) yanına ayrı bir uygulama olarak kurulur: eskisinin ilerlemesi (`files/progress.json` ve öteki kayıtlar) dosyaları kopyalanmadıkça yeniye gelmez. Eski uygulama telefondan kendiliğinden kalkmaz.
- 5.861 test geçti; ekonomi denetimi temiz (100. bölüm 8. gün). APK 290 MB (7,1 dk). `aapt`: paket `com.saturnwave.museumpuzzle`, sürüm 1.0 (kod 1); izinler tam beş (INTERNET, VIBRATE, BILLING, ACCESS_NETWORK_STATE, `com.saturnwave.museumpuzzle.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION`); dik açılıyor; `appCategory` oyun. Telefona kurulmadı.
- Sahipte: Play Console'da uygulamayı ve beş ürünü bu kimlikle açmak. App Store tarafı `feature/ios`'ta (IOS_BUILD §2).

**v1 incelemesinin düzeltmeleri (2026-10-25)**
- İnceleme çift teslim ve izinsiz giden veri bulmadı; on bulgu kapandı, her biri bir birim testiyle ya da Editor denetimiyle tutuluyor (ana dal).
- Kaydı tutamayan açılış (ilerleme dosyası okunamıyor ya da daha yeni bir sürümün) artık satmıyor: ilerleme deposu kapanıştan sonra tutup tutmadığını söylüyor (`ProgressKeeping`), mağaza hiç sorulmuyor ("Mağaza bu oturumda kapalı"), önceki açılışın bekleyen ödemesi onaysız kalıyor ve kaydedebilen açılışta bir kez geliyor.
- Onayı almayan mağaza: sipariş IAP "onaylandı" diyene kadar elde; alınmayan onay mağaza yeniden açılınca gidiyor, kopmuş mağaza 15 sn'de bir yeniden soruluyor.
- "Ödeme sürüyor" kartı 30 sn cevapsız kalınca ya da bağlantı kopunca "Mağazadan henüz yanıt gelmedi." ve "Tamam"; sonradan gelen ödeme yine bir kez veriliyor. Bilinmeyen ürüne verilen vazgeçme ve ret sayfanın cevabı sayılıyor. Kopan bağlantı artık "Ödeme alınmadı" demiyor.
- Google Play'in kendiliğinden geri verdiği Çırak Çantası "Geri yükleme" kartıyla geliyor; `purchase_complete` notu yalnız ödemede.
- Ulaşılamayan mağaza yeniden sorulurken sakin satır yerinde kalıyor ("…"ya dönüp gelmiyor).
- Gizlilik paragrafı ve soru Aptabase'in bağlantıdan ülke ve bölge çıkardığını söylüyor (13 dil, taslak).
- Künye'ye "Yazılım": Aptabase SDK ve Json.NET'in MIT metni, IAP'nin bildirimlerindeki Google Play Billing Library ve StoreKit 2 (Apache 2.0); kayıt `unity/Packages/licences.json`, sürümleri `packages-lock.json`'la test ediliyor.
- Mağazadan satan derleme "test sürümü" demiyor: giriş sayfası "Sürüm 1.0", ayarlar "Bu sürüm · …"; vekil derleme eskisi gibi.
- Eskimiş yorumlar düzeldi (`UnityStore`'da INTERNET, `economy/default.json`'da Çırak Çantası'nın yeri). 5 yeni, 2 değişen yazı 13 dilde.
- 5.861 test geçti (22'si yeni); ekonomi denetimi temiz (100. bölüm 8. gün). Unity'de `PurchaseCheck`, `PlayModeCheck` ve `MusicCheck` (`-quit` olmadan), `ConsentCheck`, `AvatarCheck`, `RotationCheck` ve `EngineSmokeTest` hatasız. Resimlere 1080×2424 ve 2732×2048'de bakıldı. APK 290 MB, izinler tam beş. Telefona kurulmadı; telefon testleri T516–T519, açık A51.
- Sahipte: yeni yazıların son ifadesi ("Bu sürüm", "Sürüm 1.0", konum cümlesi, sakin satırlar); Apache metninin bütünü ve URP'nin FXAA bildirimi; T516–T519 (A51).

**Ressam oyunda (avatar A)**
- Bitti (ana dal): keçe bereli yaşlı usta ressamın son seti (`out/avatar-final`) `art/avatar`'a (kaynak, lisans kaydı, işleyicinin künye notu) ve İngilizce adlarla `unity/Assets/Art/Avatar/Resources/Painter`'a girdi. Ayakta resimler metreyle ölçülüp ayaklarından döner; telefonda ASTC, mipmap yok.
- Resimler ekranla yüklenip ekranla bırakılıyor (`PainterGallery`, `PictureShelf`). `AvatarCheck` yeni: her ekranda tam olarak gösterdiği resim, kalkınca hiçbiri (22 sayım).
- Salon: ressam tezgâhın sağında (telefonda 450 piksel), Hediye çerçevesi tezgâhın solunda; yeni oyuncuya el sallar. Onarılan odada pankartın sağında sevinir; pankart ortada kalır, gerekirse kayar ya da küçülür.
- Rehber yazılarının solunda yaldız çerçevede büst: yol gösteren, yeni engelde ve uzun basmada düşünen, ilk tabloda sevinen. Kazanma kartının nişinde sevinen büst. Tablo tamamlanınca tepsinin sağ ucunun önünde sevinen ressam, ödül solunda.
- Ayarlar'da "Künye": sayfalı kart, lisans satırları İngilizce (Einar CC BY 4.0 "modified", Rocketbox MIT metni bütün, müzeler, yazı tipleri, Kenney); testler satırları kayıtlarla karşılaştırıyor. 6 yeni yazı 13 dilde.
- Hikâye: kahraman artık yaşlı bir usta (GAME_DESIGN §18, §0, §22; OVERVIEW, README, müzik isteği). Oyunun yazılarında ressam geçmiyordu; yeni cümle sahibin onayını bekliyor.
- Yeni doku işleyicisi bütün dokuları bir kez yeniden içe aktardı; Unity 254 `.meta`'ya varsayılan iOS bölümü yazdı (ayrı işleme).
- 5.839 test geçti (334 yeni); ekonomi denetimi temiz (100. bölüm 8. gün). Unity'de `AvatarCheck`, `RotationCheck`, `PlayModeCheck`, `MusicCheck` (`-quit` olmadan) hatasız; resimler telefon, açık Fold (iki yön) ve iPad'de (iki yön) bakıldı. Telefon testleri T500–T509 (F26), açık A49.
- Sahipte: hikâye cümlesinin onayı; künyeye müzik satırı istenip istenmediği; kaybetme kartı için üzgün ya da şaşıran büst ve bekleme canlandırması (yapılmadı).

**Oyun istatistikleri yalnız izinle, v1 gizlilik metni**
- Bitti (ana dal): Aptabase'in Unity paketi 0.3.1 (resmî GitHub, işlemeye sabit, MIT). Ayar dosyası boş anahtarla geliyor: anahtar yokken paket hiçbir şey kurmuyor, oyun notları yalnız günlüğe yazıyor. Yalnız AB bölgesinin anahtarı (`A-EU-…`) gönderir; başka bölge reddedilir.
- Kapı oyunun kodunda (`PlayStatistics`): not yalnız "İzin ver"den sonra pakete gider. Paket 0.3.1 kapalıyken de kuyruğa alıyor; bu yüzden yanıt yokken ve "hayır"dan sonra ona hiç not verilmiyor. Notlar: açılış, bölüm başı ve sonu (numara, kalan hamle), tablo ve oda, mağaza açıldı, satın alma (yalnız ürün). Kimlik yok.
- Soru kartı: ilk açılışta giriş sayfasından sonra salonun üstünde; iki düğme aynı boy ve renk; geri tuşu "hayır". Ayarlarda "Oyun istatistikleri" anahtarı (yeni işaret); gizlilik satırı anahtara göre. `device.json` 6. düzen (`statistics`).
- Gizlilik metni taslaktan (`privacy-v1.md` §3): kart sekiz paragraf, sayfalı ("1 / 3", oklar). Onay kartının ikinci satırı Unity'yi söylüyor. 13 dilde, makine çevirisi; sahibin onayını bekliyor.
- Android: INTERNET yeniden isteniyor. Derleme eski kütüphane manifestini siliyor; APK'nın izinleri `aapt` ile okunuyor, tam beş izin değilse derleme duruyor.
- Belgeler: GAME_DESIGN §19 (karar), §20 (eski metin ve "rıza ekranı yok" notları güncellendi, Google Play Data safety cevapları), §23. Telefon testleri T490–T499 (F25), açık A48.
- 5.505 test geçti (48'i yeni). Ekonomi denetimi temiz (100. bölüm 8. gün). Unity'de `ConsentCheck`, `RotationCheck`, `PlayModeCheck` ve `MusicCheck` (`-quit` olmadan) hatasız. Kartlar ve yürüyüş üç ekranda; Almanca ve Japonca telefonda bakıldı. Gizlilik kartı en çok 470 nokta, Türkçe 3 sayfa. Çince sans yedek yazı tiplerinin resmi 2048×2048 oldu. APK'nın izinleri tam beş.
- Sahipte: metinlerin onayı; Aptabase hesabı (AB) ve anahtar; site adresi, iletişim e-postası, yasal ad ve adres; politikanın sitede yayımı; Play Console Data safety ve App Store Connect beyanı; uzman okuması (`privacy-v1.md` §6).

**Gerçek satın almalar (istemci, IAP 5.4.4)**
- Bitti (ana dal): Unity IAP 5.4.4 projede, Unity Gaming Services olmadan. Satın almanın kapalı yolu `Checkout` (boşta → soruyor → bekliyor → verildi, vazgeçildi, olmadı ya da onay bekliyor); mağaza arabirimi `IStore` (oyunda `UnityStore`, araçlarda ve testlerde `ScriptedStore`). Ödeme `WalletLedger.Deliver` ile bir kez verilir, mağazaya onay kayıttan sonra gider; arada kapanan oyun ödemeyi bir kez verir. İlerleme dosyası 6. düzende (`purchases`).
- Mağaza: dört paket mağazanın yerel fiyatıyla; cevap yokken "…", satılmayan "Satışta değil", ulaşılamayınca "Mağaza şu anda açılamıyor" (15 sn'de bir yeniden sorar). Çırak Çantası 20. bölümden alınana kadar kesenin yerinde, "BİR KEZ" ile: ayrı panel en kısa telefonlarda ve dik iPad'de düğmeleri 38 noktanın altına indiriyordu. iOS'ta "Satın alımları geri yükle". Onay, ödeme sürüyor, satın alındı, olmadı, onay bekleniyor ve geri yükleme kartları; 35 yeni yazı 13 dilde.
- Ekonomi paketi biçim 4 (`store` pakette), `shop.coinPacks` örnekleri kalktı, `build.realMoneyPurchases: "store"`. Bu yüzden v1 derlemesinde test modu yok (kural değişmedi); araçlar test modunu `TestBuildEconomy` ile yürüyor.
- Sunucu yok: iade edilen altın oyuncuda kalır; silip kurunca altın paketleri gelmez, Çırak Çantası gelir (§19). IAP 5.4.4'ün satın alma olayları iOS'ta Unity'ye gider; Android'de INTERNET hâlâ silinmiş.
- `PurchaseCheck` yeni: oyunun oturumunda betikli mağazayla bütün yollar (resimler `walk-purchase-*`), sonra oyun kipinde IAP 5.4.4 ve sahte mağaza; kaydı tutmayan depoyla düşüyor.
- 5.457 test geçti (49'u yeni); betik testleri ve ekonomi denetimi temiz (100. bölüm 8. gün). Unity'de `PurchaseCheck`, `RotationCheck`, `PlayModeCheck`, `MusicCheck` (`-quit` olmadan) hatasız; mağaza ve kart yürüyüşleri üç ekranda bakıldı. APK yapılmadı, telefona kurulmadı.
- Sahipte: Play Console ve App Store Connect'te uygulama, beş ürün ve test hesapları; Çırak Çantası'nın yeri; test modu için ayrı derleme isteği; gizlilik metni (sonraki aşama). Telefon testleri T470–T489.

**Ödeyen oyuncular ve ürünlerin tutarları (simülatör)**
- Bitti (ana dal): v1'in ürünleri `economy/default.json`'un yeni `store` bölümünde: kese $1.99 = 1.000, torba $4.99 = 2.750, sandık $9.99 = 6.000, hazine $19.99 = 13.000 altın; Çırak Çantası $1.99 = 1.500 altın + üç bölüm öncesi booster, oyuncu başına bir kez, 20. bölümden. Her alanın birimi ve kaynağı yazılı (fiyat sent olarak, kesirsiz). Araştırmanın tutarları korundu: simülatör değiştirmek için bir neden göstermedi.
- Ürün kimlikleri uygulama kimliğine bağlı yer tutucular: `com.saturnwave.puzzle.coins.pouch`, `.coins.bag`, `.coins.chest`, `.coins.treasure`, `.starter.apprentice_kit`. Dosya yalnız noktadan sonrasını tutar; uygulama kimliği değişince kimlikler onu izler. Ürünleri Play Console'da ve App Store Connect'te proje sahibi açacak.
- Oyun bu bölümü henüz okumuyor (paket ve `Economy.json` değişmedi; mağaza üç örnek paketi gösteriyor). Mağaza sayfası üç sütun için çizili; satışı açan aşama dört paketi ve Çırak Çantası'nı yerleştirecek.
- Simülatöre üç ödeyen oyuncu eklendi, kuralları ayar dosyasında (`player.purchases`): `pays-when-short.json` (altın yetmeyince kese), `buys-the-starter-pack.json`, `pays-monthly.json` (ayda bir sandık). Yeni rapor `purchases.csv`, özette "PURCHASES" tablosu, coin tablosunda satın alınan altın.
- Ortalama oyuncu dört ayarda da 100. bölüme 8. günde geliyor (1000. bölüme 90 yerine 89). Altın yetmeyince kese alan 1000 bölümde ortalama 2,4 kez öder ($4,84; yarısı hiç almaz); zayıf oyuncu 36 kez ($71,57), karşılanmayan "+5" isteği 44,6'dan sıfıra iner. Ayda bir sandık alanın kesesi 10.000–25.000'e çıkar: v1'de altının "+5" ve booster dışında gidecek yeri yok (yan koleksiyonun parça fiyatı açık). Ayrıntı GAME_DESIGN §25.
- Denge politikasına fiyat kuralları 1, 2, 3 ve 6 eklendi (`EconomyBalancePolicyTests`): dolar başına altın 503 / 551 / 601 / 650 (en büyük %29 fazla, sınır %35); 1 / 3 / 6 / 14 kez "+5"; Çırak Çantası 2.400 altın değerinde, kesenin 2,4 katı (sınır 2,5) ve bir kez; fiyatlar .99 ile biter, en çok $19.99. Satın almanın hiçbir oyunu değiştirmediği ayrıca test edildi.
- 5.408 test geçti (32'si yeni); ekonomi denetimi temiz (100. bölüm 8. gün); oyunun paketinin testleri değişmeden geçiyor. Telefonda görünen bir değişiklik yok; telefon testi gerekmiyor.

## 2026-10-24 · Oturum 30 — Kalan işler

**İnceleme düzeltmeleri (yön, açılış hatası, yan paylar)**
- Bitti (ana dal): manifest adımı Unity etkinliğinin dik (`portrait`) açıldığını da denetliyor (`ManifestOrientation`, 7 test); başka bir değerde derleme duruyor. `ProjectSettings` iki dalda da dik ve hedef API 36 kayıtlı; derleme dosyayı artık değiştirmiyor. iOS derlemesi `AutoRotation`'ı ve "tam ekran istemez"i yalnız kendi dışa aktarması için koyup geri alıyor (`feature/ios`).
- Açılış adımı hata verirse giriş sayfası "Yükleniyor… %N"de kalmıyor: satır "Oyun açılamadı; yeniden başlatın" oluyor (13 dil), geri tuşu ve "Çık" çalışıyor. Açılış işi bitince ya da durunca bırakılıyor. `ScreenShots -screens entry` bu hâli de yürütüyor (Almanca ve Japoncada da sığıyor).
- Ekran boyu okunamayan Android cihaz tek uyarıyla telefon sayılıyor (değerler denetleniyor, geniş yakalama yok); dönme kuralı ekran değişince sayfa yerleştikten sonra uygulanıyor. Oyun açılışta ve her ekran değişiminde ekranı ve güvenli alanı günlüğe yazıyor (T460, T465).
- `RotationCheck`'e Fold'un sağ kameralı yan duruşu eklendi (`2152x2076+0+0+0+90`): 84 değişiklik, hatasız. Yan ekranların görüntü komutuna da bu ekran girdi (GAME_DESIGN §23 komut tablosu).
- 5.376 test geçti; ekonomi denetimi temiz (100. bölüm 8. gün); Unity'de `RotationCheck`, `PlayModeCheck`, `MusicCheck` (`-quit` olmadan) ve `EngineSmokeTest` hatasız.
- Yeni APK (`unity/Build/Android/puzzle.apk`, 294,7 MB, 30 dk): sürüm kodu 1, hedef API 36, en düşük 26; izinler `VIBRATE` ve Unity'nin `DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION`'ı; etkinlik `portrait`, kategori oyun. Telefona kurulmadı.

**Yan çevirme (tablet, açık Fold, iPad)**
- Bitti (ana dal): iPad her zaman, Android'de kısa kenarı 600 dp ve üstü olan ekranlar dört yöne dönüyor, ters dik de; telefonlar ve Fold'un dış ekranı dik. Android paketi dik açılıyor, oyun ilk karede kuralı uyguluyor (`ScreenTurns`, `DeviceScreen`). Manifestte `appCategory="game"` yoksa derleme duruyor; hedef API 36'ya sabitlendi.
- Seçenek A: dik düzen ortada, yanlarda galeri duvarı. Yatay tablette salonda odanın onarılmış tabloları (en çok dört), haritada geniş çerçeve (şeritte 9–10 dönem), yapbozda tablo solda ve tepsi sağda (altı yer, 2×3 ya da 3×2). Fold yan tutulunca bugünkü düzen.
- Ekran değişince basılı parmak bırakılıyor: dönmeden önce başlayan sürükleme takas etmiyor, taşınan parça tepsiye dönüyor. Yan paylar: `ScreenShape` sol ve sağ payı, `ScreenView.UsableLeft/Right`; geri düğmesi, hediye ve giriş sayfasının alt satırı paylara uyuyor.
- iPad dalından alınanlar: koleksiyonda üçlü sıra 410 noktadan (dik iPad'ler), titreşimi olmayan cihazda titreşim anahtarı yok (Android tabletler), `MusicCheck` sayfanın yerleşmesini bekliyor.
- Yeni denetim `RotationCheck`: iPad dik↔yan, Fold iç dik↔yan (kamera yanda), Fold dış→iç; taşlar düşerken, sürüklerken, bütün kartlar açıkken, finalde, parça taşınırken, giriş sayfasında 63 değişiklik. Eski kodda sürüklemenin ortasında dönünce bir hamle oynanıyordu; denetim kırılıyor.
- Pixel dış ve iç ekranın 262 görüntüsü önce ve sonra piksel piksel aynı. Yatay görüntüler 12,9" iPad, 16:10 tablet ve Fold yan ekranında bakıldı.
- 5.367 test geçti; ekonomi denetimi temiz; Unity'de `PlayModeCheck`, `MusicCheck` (`-quit` olmadan), `RotationCheck` ve öteki denetimler hatasız. Telefon testleri T450–T465; cihazda henüz denenmedi.
- iOS (`feature/ios`): Info.plist'te iPad dört yön, iPhone yalnız dik; "tam ekran zorunlu" kalktı (Pencereli Uygulamalar, Stage Manager). Ayrıntı `docs/IOS_BUILD.md`.
- Açık: tahtanın yatay düzeni (B) iPad'de denendikten sonra; Android tablette ilk karenin dik görünüp dönmesi cihazda ölçülecek (T464); iPadOS 26 pencere düğmeleri ile geri düğmesi (T457).

**Koleksiyonda bellek (A42/4)**
- Bitti (8960aa8): 23 Ekim telefon turunda koleksiyonun 20 odası gezilince bellek 401 MB'tan 649 MB'a çıkmıştı. Neden: odaların resimleri `Resources` ile yükleniyor, oda kaldırılınca bırakılmıyordu; 20 oda gezilince müzenin 100 resmi de bellekte kalıyordu (telefonda her biri yaklaşık 2,5 MB). Ağlar, malzemeler ve sahne nesneleri zaten temizleniyordu.
- Artık resmi gösteren her şey onu tutuyor ve kaldırılınca bırakıyor (`PictureShelf`, Unity'siz, 11 test). Hiçbir şeyin tutmadığı resimlerden son bırakılan 10'u (iki oda) kalıyor, fazlası boşaltılıyor; sahne değişince hepsi. Görünürde değişiklik yok (GAME_DESIGN §23).
- Yeni denetim `CollectionMemoryCheck`: 20 oda beş kez gezilir (üçü düğmelerle, ikisi kaydırarak). Eski kodda aynı anda 100 resim, denetim kırılıyor; yeni kodda en çok 20, her gezinti sonunda 15; ağ, malzeme ve nesne sayıları sabit.
- 4.122 test geçti; ekonomi denetimi temiz; Unity'de bütün denetimler hatasız. Telefon testleri T420–T425; telefonda henüz denenmedi.

**Giriş sayfası**
- Bitti: oyun her soğuk açılışta giriş sayfasıyla açılıyor (EntryTR, F simgesine uyarlandı): krem zemin ve damask, ışık huzmeleri, simgenin paleti, süzülen dört taş, yaldızlı "Müze Yapbozu" ve "ÇALIŞMA ADI" rozeti, yaldızlı yükleme çubuğu, nefes alan "Başlamak için dokun", alt satırda "SaturnWave" ve sürüm. Arka plandan dönüşte çıkmıyor.
- Çubuk gerçek açılış işini izliyor: bölümler, katalog, ekonomi, oturum (salon ve resimleri), her karede bir adım. 1,2 saniyeden kısa sürede dolmuyor. Dokununca sayfa kararıyor, salon o karanlıktan aydınlanıyor; salon, öğretici ve hediyeler eskisi gibi.
- Geri tuşu salondaki çıkış kartını indiriyor (aynı kart, aynı kural).
- Logo için simgenin çizimi tam boyunda ayrıca çizildi (`art/icon/entry-logo-1024.png`); simgenin kendi dosyaları değişmedi.
- 4.153 test geçti (31'i yeni); ekonomi denetimi temiz; Unity'de `PlayModeCheck` ve `MusicCheck` giriş sayfasından salona ve bir bölüme geçiyor. Görüntüler iki ekranda ve dört dilde bakıldı. Telefon testleri T430–T436; telefonda henüz denenmedi.
- Açık: oturumu açan adım bir kareyi yaklaşık yarım saniye tutuyor (Editör); telefondaki süre günlükten okunacak.

**Kullanıcı kararları (2026-10-24)**
- Restoratör rütbesi (XP) sonraki sürüme önerilen kaynaklarla girer: kazanılan bölüm, zor bölüm, ilk denemede kazanma, tablo, oda, mini oyunlar. Kalan hamle ve kullanılan booster XP vermez.
- Sıra: önce yan çevirme, sonra mini oyunlar.
- Yan çevirme: iPad her zaman, Android'de kısa kenarı 600 dp ve üstü olan ekranlar (tabletler, açık Fold) döner; telefonlar dik kalır. Önce dik sütun ve galeri kanatları, sonra haritanın geniş çerçevesi ve yapbozda tablo solda, tepsi sağda. iPad'de pencere kipi de olur. Plan ve fikir çizimleri: `out/landscape/`.
- Mini oyunlar: "eşya sıralama" gibi küçük, basit oyunlar, ayın belli haftalarında etkinlik olarak; altın, can ve başka ödüllerle. Önce hangi türlerin oyuna girebileceğine bakılacak.

**Mini oyun kataloğu**
- 5 ajan (3 araştırmacı, 1 doğrulayıcı, 1 tasarımcı), yaklaşık 0,9 milyon token. Doğrulayıcı 34 iddianın 22'sini olduğu gibi tuttu, 10'unda ayrıntı düzeltti; uydurma sayı bulmadı.
- Sonuç: `docs/research/mini-games.md` ("Araştırma Raporu" PDF'i); notlar, fikir çizimi ve çizimin betiği `docs/research/mini-games-2026-10/`.
- Bulgu: 2025'te bulmaca büyümesini yeni türler taşıdı (birleştirme, sıralama, blok, vida). Büyük üçlü eşleştirme oyunları liste hitlerini olduğu gibi gömmüyor; renk sırası, yapboz, kazı ve hafıza gibi oyunları etkinlik olarak koyuyor. Hepsi kazanılan bölümden gelen jetonla besleniyor, can harcatmıyor.
- Öneri: önce ortak çerçeve (takvim, etkinlik kartı, mini oyun sahnesi, sonuç ve ödül), sonra Boya Tüpleri (tüp sıralama), Pigment Tarifi (renk sırası), Takas Yapbozu (kart takası), Depo Sıkışıklığı (kayan sandıklar), isteğe bağlı Çerçeve Vidaları. Ayın 1. ve 3. haftası "Atölye Haftası": hafta içi ve hafta sonu birer zincir, telefonun saatiyle, sunucusuz. 14 soru proje sahibinin kararını bekliyor.

**Satın alma denemesi (IAP)**
- Atılan bir kopyada (`D:/puzzle-iap-spike`, 6fdd691) Unity IAP 5.4.4 eklendi; satış yapmayan en küçük kod (`coins_test` ürününü soran bir deneme) yalnız `IAP_SPIKE` tanımıyla derlendi. Üç Android derlemesi: IAP'siz, IAP'li ve INTERNET silinmiş, IAP'li ve silme kapalı. Kopya sonra silindi; oyuna hiçbir şey girmedi.
- İzinler: IAP `com.android.vending.BILLING` ve `ACCESS_NETWORK_STATE` getiriyor. INTERNET'i Google'ın `transport-backend-cct` kütüphanesi istiyor; bizim izin adımı onu siliyor (Gradle raporu: "REJECTED"). Silme kapalıyken INTERNET geri geliyor. Konum izni yok.
- Boyut: APK 294,7 MB'tan 302,7 MB'a, +8,0 MB (%2,7); çoğu derlenmiş C# kodu. Telefonda yaklaşık +26 MB.
- UGS ve bulut projesi gerekmiyor: editörün oyun kipinde UGS başlatılmadan bağlandı ve ürün geldi. Derleme yalnız "bulut projesi yok, Google Play lisans anahtarı yok" uyarısı yazıyor.
- Paketin kaynak kodundan: IAP'nin teşhis verisi UGS başlatılmadıkça gitmiyor. Ama 5.4.4 her satın alma adımını (fiş, ürün, fiyat, cihaz modeli, iOS'ta IDFV) Unity'nin sunucusuna yolluyor; kapatma ayarı yok. Android'de INTERNET yokken çıkamaz, iOS'ta çıkar. 5.3.1'de bu kod yok.
- Tuzak: silme satırı kaldırılınca Unity eski manifesti kullanıyor ve INTERNET silinmiş kalıyor; Gradle projesindeki manifest silinince düzeldi.
- Öneri: satın alma gelirse IAP 5.3.1 (Google'a göre 2027-08-31'e kadar geçerli) ve INTERNET silinmiş kalsın; analiz gelirse Aptabase, oyuncunun izniyle. Belge: `docs/research/iap-and-analytics.md` ("Araştırma Notu" PDF'i); kaynaklı araştırma notu `docs/research/iap-and-analytics-notes.md`. Kararlar sahibinde (belgenin 7. bölümü).
- Açık: gerçek Google Play satın alması INTERNET'siz denenmedi (Play Console'da uygulama ve lisanslı test hesabı gerekiyor); iPad'de ağ dinlenmedi; 5.3.1 derlenmedi.

## 2026-10-23 · Oturum 29 — Simge, iOS hazırlığı, geri tuşu, yeni APK

**Kullanıcı kararları**
- Uygulama simgesi: önce G (gece mavisi palet), sonra F seçildi: krem zemin üstünde ressam paleti, üç taş ve fırça ("daha sade gözüküyor").
- iOS işi `feature/ios` dalında; kod Windows'ta yazılır, Mac yalnızca Xcode ile derler ve cihaza kurar. Oyun ilk olarak iPad'de denenecek.
- Oyunun tekrara düşmemesi için genişletme: Battle Pass (ücretsiz ve premium), oyuncu seviyesi ve XP, mini oyunlar, Royal Match'in kalesi gibi bir meta katmanı, etkinlikler. Önce çok ajanlı bir araştırma (onaylandı); fiyat çizelgesi yalnızca ABD (USD).

**Uygulama simgesi**
- Bitti (1ed124b, 0995820, b98e823): F simgesi `art/icon/` altında; Android'de uyarlanabilir simge (ön ve arka katman), iOS'ta 1024 px tek parça. Kaynak `art/icon/tools/final_icon.py`.

**iOS hazırlığı**
- Mac'e SSH ile ulaşılıyor (kullanıcı `arcancaglayan`; Intel, macOS 15.8, Xcode 26.3). Depo Mac'te `~/Desktop/Puzzle` klasöründe, `feature/ios` dalı; Mac kendi anahtarıyla GitHub'a SaturnWave olarak ulaşıyor.
- Unity 6000.6.4f1'e iOS Build Support kuruldu (Hub'ın komut satırıyla).
- iPad'de Taptic Engine yok: ayarlar kartı orada Titreşim anahtarını göstermiyor; titreşim motoru olmayan Android tabletlerde de öyle (`feature/ios`, e9288a7).
- Xcode projesi, Android kopyasının önbelleğine dokunmamak için D: diskindeki ayrı çalışma ağacında (`D:/puzzle-ios`) üretilecek.

**Salonda geri tuşu ve dokunuş eşiği**
- Bitti (cbf5202): salonda geri tuşu "Oyundan çıkılsın mı?" kartını getiriyor; bir düğmenin üstünde 10 pt'den uzun kaydırma hiçbir şeye basmıyor (GAME_DESIGN §23). 4.111 test geçti; Unity denetimleri hatasız. Telefon testleri T410–T414.

**Yeni APK (2026-10-23)**
- Proje sahibi tablette denemek için istedi. İçinde F simgesi, oyuncunun müziğine yol veren müzik, çıkış kartı ve dokunuş eşiği var. İndirme sayfasına `puzzle-test-2026-10-23.apk` olarak kondu.

**Ressam avatarı**
- Ücretsiz yollar denendi; üç aday aynı CC0 paketten (KayKit Adventurers 1.0, Kay Lousberg): A kız ressam, B uzun saçlı ressam, C erkek ressam. Bere, palet ve fırça eklendi, renkler oyunun paletinden. Karşılaştırma sayfası `out/avatar/avatar-candidates.jpg`; karar proje sahibinde.

**Araştırma: gelir ve oyuncu tutma**
- 8 ajan: 6 araştırmacı, 1 doğrulayıcı, 1 tasarımcı. Yaklaşık 2,6 milyon token, 53 dakika. 15 oyunun ABD App Store fiyatları ve pass, XP, ara oyun, meta, etkinlik kuralları toplandı. Doğrulayıcı 158 fiyatın 150'sini, 32 iddianın 28'ini tuttu; tutmayanlar belgede kullanılmadı.
- Sonuç: `docs/research/monetization-and-retention.md` ("Araştırma Raporu" PDF'i). Ham notlar ve fiyat çizelgesinin JSON'u: `docs/research/monetization-2026-10/`.
- Öneriler: dört altın paketi ($1.99–$19.99), bir kez satılan $1.99'luk başlangıç paketi, ayda bir $4.99'luk "Sergi Sezonu" pasosu, XP'li Restoratör rütbesi, ilk 100 bölüme ara oyunlar, yayından sonra düzenli yeni odalar. Rastgele ücretli içerik ve parayla can yok. Hepsi öneri: 16 soru proje sahibinin kararını bekliyor ve her sayı yayından önce ekonomi simülatöründen geçecek.

## 2026-10-22 · Oturum 28 — Müzik

**Kullanıcı kararları**
- Proje sahibi yedi müzik parçasını Suno Premier hesabıyla üretti ve `art/sounds/` klasörüne koydu (2026-10-20; 48 kHz stereo WAV).
- Oyunda tek bir genel müzik olacak: M1 Müze teması, her yerde. Kısa parçalar ve ayrı tahta müziği kullanılmayacak (2026-10-22).

**Telefon testi turu (2026-10-23, 22 Ekim APK'sı)**
- Yeni APK telefona kuruldu (önce oyunun dosyaları yedeklendi). Test sayfası 252 teste çıktı; önceki sonuçlar yerinde.
- Bir ajan otomatik yapılabilen testleri adb ile koştu: 169 test, 121 geçti, 2 kaldı, 46'sı proje sahibine kaldı (ses ve his, iç ekran ve katlama, saat ve telefon dili, eski sürüm, göze dayalı yargılar). Sonuçlar test sayfasına işlendi; proje sahibine kalanlarda ajanın gözlemi not olarak duruyor.
- Kalanlar: salonda geri tuşu hiçbir şey yapmıyor (T43); tam ekranda kenardan tek geri hareketi yetmiyor (T235). Ek bulgular: ayarlar kartında satır üstünde kaydırma basılmış sayılıyor; koleksiyonda bellek büyüyor (çökme yok). Hepsi A42'de. T181, T190 ve T394'ün metinleri güncellendi.
- Proje sahibinin dosyaları geri kondu (ilerleme ve kayıtlar yedekle birebir); oyunun Ses ve Müzik anahtarları proje sahibinin isteğiyle kapalı. Telefonun ses düzeyine yalnızca bir kez, istek üzerine dokunuldu (medya sesi 0).

**Oyuncunun müziği öncelikli (#9)**
- Proje sahibinin kararı (2026-10-23): oyuncunun kendi müziği çalarken oyunun müziği susar, efektler sürer. Bitti (a05c580): Android'in çalan oynatıcı listesine 2 saniyede bir bakılıyor, oyunun kendi oynatıcısı etiketiyle ayrılıyor (`isMusicActive` oyunun kendi sesini de saydığı için kullanılmadı); öteki müzik durunca 2–4 sn içinde kaldığı yerden geri geliyor. iOS tarafı iOS dalında.
- 4.095 test geçti; müzik denetimine 9 yeni adım eklendi. Telefonda henüz denenmedi (yeni APK'da değil). Telefon testleri T380–T383.

**Yeni APK (2026-10-22)**
- Proje sahibinin sırasıyla en sona bırakıldı. İçinde ipucu, öğretici, booster tanıtımı, 13 dil, müzik, dünya haritası ve plaketteki iki satır var. 294 MB; yalnızca titreşim izni (internet yok).
- İndirme sayfasına kondu (`puzzle-test-2026-10-22.apk`, eski prototip de indirilebilir). Sayfanın sunucusu bir ara kapanmıştı; gizli bir pencerede yeniden açıldı. Telefon bağlı değildi, kurulmadı.

**Dünya haritası oyunda (#4)**
- Bitti (130e9e1, df8401f): onaylanan tasarım Unity'ye birebir geçti; kat planı kalktı. Kıyılar Natural Earth'ten bir araçla (`puzzle worldmap build`) çıkarılıyor, gölgelendiricide her yakınlıkta aynı incelikte çiziliyor.
- Yerler iki ajanla aynı anda: biri 100 tablonun yerini Wikidata ve müze sayfalarıyla doğruladı (59'u doğrulandı) ve 13 dilde adlandırdı (a9ede9b), öteki haritayı kurdu. Proje sahibi açık kalan beş yer seçimini olduğu gibi bıraktı.
- 4.068 test geçti (kat planının testleri gitti, 49 harita testi geldi); ekonomi denetimi temiz; Unity'de beş dilde harita yürüyüşleri hatasız. Tasarımla yan yana resim birebir. Telefon testleri T390–T403.
- Proje sahibinin kararları: uzun dönem adları plakada iki satır (bir tanesi, 14. dönem, karşılaştırma için bilerek tek küçük satır); ekran okuyucu (TalkBack) desteği bütün oyun için sonra yapılacak. Site GitHub'a taşındı (SaturnWave/SaturnWave.Apps), yayını sonra.

**Müzik (#9)**
- Bitti (6888599): M1'in girişi bir kez, sonra 36 ölçülük döngüsü boşluksuz; ekran değişince baştan başlamıyor; efektlerin 5 dB altında; ayarlarda "Müzik" anahtarı (device.json 5. düzen). Kesim aracı ve lisans kaydı `art/sounds/music.json`.
- Asıl M1 dosyası ve kesilmiş iki parça depoda (yazı tiplerindeki gibi); kullanılmayan altı dosya yalnızca bilgisayarda.
- 4.223 test geçti; ekonomi denetimi temiz; Unity'de denetimler ve yeni müzik denetimi hatasız. Müzik henüz kulakla dinlenmedi. Telefon testleri T370–T379.

**Harita (#4)**
- Proje sahibinin kararları: harita gerçek bir dünya haritası; iğne tablonun yapıldığı ya da gösterdiği yerde (bugün durduğu müzeler neredeyse hep ABD'de olduğu için); eski müze haritası görünüşü; ileri dönemler görünür, resimleri örtülü.
- Etkileşimli tasarım tasarım tuvaline eklendi ("Dünya haritası · dönemler"): Natural Earth kıyıları (kamu malı) oyunun renklerinde; açılışta dünyadan oyuncunun dönemine kayan kamera; 20 dönemlik şerit; aynı yerdeki tablolar bir sıra hâlinde, ince çizgiyle yerlerine bağlı; tablo kartında yer, bugünkü müze, durum ve "Oyna". Oyuncunun bölümü tuvaldeki ayarlardan değiştirilebiliyor (varsayılan 287).
- 100 tablonun yeri ilk kez derlendi; 10'u yalnızca bölge düzeyinde, oyuna girmeden doğrulanacak. Kaynaklar `out/design/worldmap-src`.
- Proje sahibi tasarımı onayladı: "eskisine göre daha iyi". Sıradaki: oyuna girmesi (yeni APK'dan sonra).

**Müzik adayları**
- Her parça için 2–3 aday kesildi ve bir dinleme sayfasında sunuldu (`out/music/candidates/listen.html`). Döngüler ölçü başında başlayıp bitiyor, ek yeri çeyrek saniyelik yumuşak geçişle birleştirildi, seviyeler eşitlendi. M1 yaklaşık 84 vuruş/dk ölçüldü; üç döngü adayı 86, 92 ve 103 saniye, hepsi girişten (ilk 32 saniye) sonra başlıyor.
- Sayfanın küçük sunucusu uygulama tarafından iki kez kapatıldı; sayfa artık çift tıklamayla da açılıyor.

---

## 2026-10-19 · Oturum 27 — İpucu, kararlar, müzik istekleri

**Kullanıcı kararları**
- Yeni hafta GitHub kayıtlarından sürüyor; sıra #6 ipucu → #5 öğretici → #7 booster tanıtımı → #8 dillerin kalanı. Kararların hepsi tasarım belgesine (§19) yazıldı.
- İpucu: düğme yok; hareketsizlikte kendiliğinden; denemede 2–3 ipucu; hamleyi uzman olmayan bir bot seçer ("yardım etsin ama o kadar da değil"); görünüş telefonda denenecek.
- Öğretici: ilk 3 bölüm, hedef ve hamle sayısı, engeller ilk görüldüğünde, müze ve yapboz. Denemede gerekirse kısaltılır.
- Müzik: proje sahibinin Suno Premier hesabıyla sözsüz müzik üretilecek; istekler `docs/music/MUSIC_BRIEF.md`.
- Booster tanıtımı (#7): bölüm içi booster açıldığı bölümde bir kez elle ve bedava kullandırılır; uzun basınca döngülü canlandırma kartı; bölüm öncesi booster'lar ilk kayıptan sonraki başlangıç kartında tanıtılır.

**Git kimliği**
- Commit'ler iş hesabının adıyla atılıyordu (ArcanCaglayan, otekyazilim adresi). Proje sahibinin isteğiyle bilgisayarın ortak git ayarı (`C:\Users\arcan\.gitconfig`) SaturnWave kimliğine geçirildi; e-posta GitHub'ın gizli adresi. Eski dosyanın kopyası yanında duruyor. Geçmiş commit'ler eski adla kalıyor.

**İpucu (#6)**
- Bitti (133107c): hareketsizlikte iki taşın etrafında sıcak hale ve kıpırdama; titreşim açıksa tık; denemede en çok 3; hamleyi tahtanın kopyasında merdivendeki bir bot seçiyor; motor izi aynı.
- Ölçüm (500 bölüm, bot başına 10.000 deneme, her ipucuna uyulursa): casual ipucu ortalama oyuncuya +3,3–4,4, zorlanana +4,8–7,1 puan. Proje sahibi casual'ı, 8 saniye beklemeyi ve ipucu görülen denemelerin ayrı grup olmasını seçti; bu ikisi öğreticiden sonra işlenecek.
- 3.920 test geçti; Unity'deki bütün denetimler hatasız. Görüntüye baktım: hale görünüyor, asıl karar telefonda. Telefon testleri T290–T299.

**On üç dil (#8, 2. adım)**
- Bitti (a675d0d): on dil bekleme klasöründen oyuna geçti; booster tanıtımının 13 yazısı çevrildi, eski 4 kimlik atıldı. Dil kartı 13 dili iki sütunda gösteriyor.
- Rusça, Japonca ve Çince için Noto yedek yazı tipleri (OFL, notofonts sürümlerinden, toplam yaklaşık 45 MB indirildi; yalnızca gereken dosya zip arşivinin içinden çekildi). Atlaslar yalnızca tablolarda geçen harflerden; APK'ya tahminen 4,5 MB. Yazı tipi dosyaları Korecedeki gibi depoda (44,6 MiB kaynak, 27,5 MiB atlas).
- Her dil kendi Han biçimini önce soruyor; Japonca ve Çince satırlar TextMesh Pro'nun kurallarıyla kırılıyor. Yedi satır 9 puntonun altına düştüğü için kısaltıldı.
- 4.175 test geçti; ekonomi denetimi temiz; Unity'de denetimler ve on bir dilde, iki ekranda yürüyüşler hatasız; durgun sayfalarda taşma yok. Japonca, Rusça, İspanyolca görüntülere ve dil kartına baktım. Telefon testleri T350–T363; telefon dilini değiştirmek gerekenler proje sahibinin.

**Booster tanıtımı (#7)**
- Bitti (c390a84, aa8d2b7): bölüm içi booster açıldığı bölümde (7, 12, 16, 20) bir kez elle ve bedava kullandırılıyor; hedefi tahtanın kopyasında hedeflere en çok ilerleten kullanım. Herhangi bir booster'a basılı tutunca kart iner, etkisini küçük gerçek bir tahtada tekrar tekrar oynar. Başlangıç booster'ları bir bölüm ilk kez kazanılamadan bırakılınca kartta sırayla tanıtılıyor.
- Proje sahibinin üç cevabı: uzun basma ilk rehberli kullanımdan sonra bir kez öğretiliyor; bedava kullanım tek başına denemeyi başlatmıyor (hemen çıkmak can götürmez); başlangıç tanıtımı hem kayıpta hem hamleden sonra çıkışta.
- Kayıtta isteğe bağlı `guidedBoosters`; deneme yardımlı sayılıyor. Eski "Yeni: …" yazısının dört kimliği kalktı; bekleme klasöründeki on dilde 13 yeni kimlik eksik, 4 eski kimlik atılacak.
- 3.994 test geçti; ekonomi denetimi temiz; Unity'deki denetimler ve üç dilde öğretici ve booster yürüyüşleri hatasız. Görüntülere baktım: karartma öğreticiyle aynı, kartlar okunuyor. Telefon testleri T330–T345.

**Çeviriler ve öğretici**
- On dilin çevirileri hazır (9fd70d0): sözcük tabloları `words/staging`, tablo adları `museum/titles/staging`. Yerleşik tablo adları Wikidata'dan, yalnızca katalogdaki tablo ve müzeyle eşleşince alındı (100 adın Almancada 40'ı, Fransızcada 52'si, Endonezcede 2'si); kalanı çeviri. Denetim temiz; öğreticinin 23 yazısı da çevrildi.
- Oyuna girmeden önce: harf setine ¡ ¿ œ; Rusça, Japonca ve Çince için yedek yazı tipleri; İspanyolca "MOVIMIENTOS" hamle madalyonu için uzun. İspanyolca sorgu 86. adda kesildi (18–20. odalar çeviri).
- Öğretici (#5) bitti (d10de7a): 1. bölümde hedef plaketi, elle gösterilen eşleşme ve hamle sayacı; 2. ve 3. bölümde dörtlü (fırça) ve beşli (palet); sandık, vernik, bant ve çerçeve köşesi ilk görüldükleri bölümde; ilk parça ve ilk tablo. Gösterilecek hamle tahtanın kopyasında aranıyor; tahta sunmazsa adım bekliyor. El çizimi yok.
- 3.953 test geçti; Unity'deki bütün denetimler ve üç dilde öğretici yürüyüşleri hatasız. Görüntüye baktım: aydınlatma ve yazılar okunuyor. Telefon testleri T310–T321.
- İpucunun kararları uygulandı (9ad22f0): bekleme 8 sn; üç ayar "karar" işaretli; ipucu görülen denemeler ayrı bir grupta, casual'ın ipuçlarına uyan botlarla karşılaştırılıyor. Botların ipucu görme sıklığı varsayım (8 sn'de hamlelerin %22'si); telefon kayıtları gelince ölçülene çevrilecek. 3.963 test geçti; ekonomi denetimi temiz, 100. bölüm 8. gün.

---

## 2026-10-18 · Oturum 26 — Kaydırarak geçiş, dil desteği

**Kullanıcı kararları**
- Telefon testleri bir süre bekleyecek; önce GitHub kayıtlarından bazıları yapılacak: kaydırarak geçiş (#3) ve dil desteği (#8).
- Kaydırma sayfa değiştirir; koleksiyonda odalar oklarla ve oda plaketinin üstünde kaydırarak değişir.
- Dil: geniş bir yelpaze, İngilizce, Türkçe ve Korece mutlaka. İlk sürümde 13 dil: İngilizce, Türkçe, Korece, Japonca, Almanca, Fransızca, İspanyolca, Portekizce (Brezilya), İtalyanca, Rusça, Çince (basitleştirilmiş ve geleneksel), Endonezce. Arapça ve Tayca sonra (harf birleştirme, sağdan sola düzen).
- İlk dil telefonun dilinden gelir, desteklenmiyorsa İngilizce; ayarlardan değiştirilir. Tablo adları seçilen dilde, ressam adları değişmez.
- Pazar verisi az ama tutarlı: match-3 gelirinde ABD açık ara önde, Japonya ve Almanya izliyor (Royal Match, AppMagic 2025: ABD %54,5, Japonya %9, Almanya %5,35).

**Kaydırarak geçiş (#3)**
- Dört müze sayfası arasında parmağı izleyen kaydırma; uçlarda esneme; koleksiyonda plaketle oda değişimi; tahtada, yapbozda ve kart açıkken kayma yok. Dokunuşla kaydırma ayrımı ve Android geri hareketinin kenar bölgesi düşünüldü.
- 3.721 test geçti; Unity'deki bütün denetimler hatasız. Görüntüye baktım: ortası doğru; uçta esnerken düz bir şerit görünüyor (A34).
- Telefon testleri T230–T241.

**Dil desteği (#8)**
- İki adım: önce altyapı ile Türkçe, İngilizce ve Korece (Korece yeni yazı tipi düzenini kanıtlar); sonra kalan 10 dil.
- **1. adım bitti:** 200 anahtarlı çeviri tabloları (Türkçe kaynak, çevirmen notlarıyla), dile göre sayı ve süre biçimleri, telefonun dilinden gelen ilk dil, ayarlarda dil kartı, Korece için Noto yazı tipleri, tablo ve oda adları dil başına dosyalarda (Korecede 22 yerleşik ad Wikidata'dan). Uzun tablo adları artık iki satır.
- 3.882 test geçti; Unity'deki bütün denetimler ve üç dilde yürüyüşler hatasız. İngilizce ve Korece ekranlara baktım: hepsi sığıyor ve okunuyor.
- Çeviriler makine çevirisi; yayından önce ana dili olan biri gözden geçirmeli (A35). Telefon testleri T250–T265.
- 2. adım iki parçada: Latin harfli altı dil (Almanca, Fransızca, İspanyolca, Portekizce, İtalyanca, Endonezce), sonra Rusça, Japonca ve iki Çince.

**Play Console'a bakış (proje sahibinin isteğiyle, Chrome'dan)**
- Masa: kapalı test açık, 6 kişi katılmış. Dress & Paint: kapalı test açık, 1 kişi katılmış. Sketch & Paint: taslak, kapalı testte sürüm yok, yalnızca iç test. Yayına çıkmak için kapalı testte 12 kişinin 14 gün katılmış olması gerekiyor. Hiçbir ayar değiştirilmedi.

---

## 2026-10-16 · Oturum 25 — Ses ve titreşim, ilk prototip APK'sı

**Kullanıcı kararları**
- Gizlilik metni onaylandı. INTERNET izni paketten çıkarıldı. Android yedeklemesi açık kalıyor.
- 15. aşama sonraya bırakıldı; önce prototip telefona kuruluyor.
- APK için Tailscale üzerinden bir indirme bağlantısı istendi.

**Arayüz: planın 14. aşaması (§23)**
- Yer tutucu sesler (15 ses işareti, Kenney'nin CC0 paketlerinden, toplam 114 KB) ve dört anda kısa titreşim tıkı. Ses ve titreşim anahtarları ayarlar kartında; değişiklik hemen geçerli ve saklanıyor.
- Sesler tahtanın zaman çizelgesinde çalıyor, hiçbir turu uzatmıyor; aynı ses 0,12 saniyeden sık yinelenmiyor.
- 3.666 test geçti; Unity'deki bütün denetimler hatasız. Görüntüye baktım: ayarlar kartı iki anahtarla çizime uyuyor (müzik satırı yok).
- Seslerin nasıl duyulduğunu ve tıkların nasıl hissedildiğini yalnızca telefon gösterecek (A30, A31).
- Telefon testleri T190–T201; toplam 32 bilinen açık, 130 telefon testi.

**İlk prototip APK'sı**
- APK 16 Ekim'de derlendi (Unity 6.6, IL2CPP, ARM64; 12,9 dakika): 283 MB. 8 Ekim'deki 61 MB'tı; fark 100 tablonun tam boy görüntüleri (A32).
- 16 Ekim APK'sında `aapt` ile doğrulandı: paket yalnızca VIBRATE iznini istiyor.
- APK 16 Ekim 01.23'te proje sahibinin telefonuna USB ile kuruldu (eskisinin üstüne) ve açıldı. Eski kayıt okundu: ilk tablonun 5/10 parçası yerinde; cüzdan eski kayıt düzeninden hoş geldin hediyesiyle başladı (200 altın, 5 can).
- İndirme sayfası önce yalnızca Tailscale hesabındaki cihazlara açıldı (`tailscale serve`); aynı gece proje sahibinin isteğiyle, tablete indirebilmek için herkese açıldı (`tailscale funnel --https=10000`; kapatmak için `tailscale funnel --https=10000 off`): https://arc.tail0b9256.ts.net:10000/ (bu bilgisayar açıkken ve D: takılıyken çalışır; sunucu `tools/apk-share/serve.mjs`, yalnızca 127.0.0.1'de)

**Prototipten sonra proje sahibinin notları (2026-10-17)**
Proje sahibi oyunu bir süre oynadıktan sonra şu eksikleri yazdı; her biri GitHub'da bir kayıt oldu (kilometre taşı "Prototip sonrası arayüz iyileştirmeleri"). Telefon testinden sonra, proje sahibi "başla" deyince ele alınacak.
- #3 Ekranlar arasında kaydırarak geçiş yok; yalnızca düğmelerle geçiliyor.
- #4 Harita yeniden tasarlanmalı: oyuncunun yeri ve sonraki hedefler görünmüyor.
- #5 Oyun için öğretici yok (yeni başlayan için ilk bölümler rehberli olmalı).
- #6 İpucu yok. İpucu algoritması kararlarıyla birlikte motorun parçası olarak tasarlanmalı.
- #7 Yeni açılan booster için görsel tanıtım (döngülü canlandırma ya da oyun içinde rehberli kullanım).
- #8 Dil desteği yok; oyun yalnızca Türkçe.
- #9 Oyun müziği yok; proje sahibinin Suno Premier üyeliğiyle üretilecek.
- İlk kayıt #2 (renkler mat ve düşük kontrastlı) 15 Ekim'den beri açık.
- Ekranların hepsi "Oyun Ekranları" sayfasında (E01–E31); proje sahibi ekranların üstüne işaretli not bırakabiliyor.

**Test modu (#10)**
- Proje sahibi telefonda sınırsız altın ve can istedi. Destek kartındaki sürüm satırına 7 dokunuşla açılan gizli bir test modu yapıldı; ayarlardan kapatılıyor. Kurallar ve ekonomi değişmedi; test modunun denemeleri kayıtta işaretli ve karşılaştırmaya girmiyor.
- İlk turda kaybetme kartı hâlâ "1 can gitti" diyordu; düzeltildi. Soru kartındaki "Jeton" sözcüğü oyunun diliyle "Altın" yapıldı.
- 3.685 test geçti; Unity'deki bütün denetimler hatasız. Telefon testleri T210–T223.
- Test modlu APK 17 Ekim'de derlendi (9,7 dakika, 283 MB; `aapt`: yalnızca VIBRATE izni) ve indirme sayfasında 16 Ekim APK'sının yerini aldı.

**Telefon testi turu (durduruldu)**
- Proje sahibi A ve B bölümlerini tablette iki gün boyunca denedi, sorun görmedi. C'den sonrakileri Claude telefonda USB üzerinden yapıyor: ekran görüntüsünden tahtayı okuyup hamle yapan küçük bir çözücüyle (`out/phone-test/`).
- 130 testin 43'ü geçti (10'u proje sahibinin, 33'ü Claude'un). Sonuçlar "Telefon Testi" kontrol listesinde.
- Telefonda görüldü: A25 telefonda da geçerli; "+5" teklifi açıkken uygulama kapatılırsa can gitmiyor.
- Proje sahibinin isteğiyle tur durduruldu; sonra sürecek. Saat, katlanır ekran ve ses testleri proje sahibinin.

---

## 2026-10-15 · Oturum 24 — Bölüm içi booster araştırması, Genel Belge

**Kullanıcı kararları**
- 10. aşamanın kodlamasından önce öteki match-3 oyunlarının bölüm içi booster'ları (ve komşu sistemleri) nasıl kullandığına bakılsın, not edilsin; sonra kodlamaya geçilsin.
- İş bitince bir bot deneyi yapılacak; ayrıntısı sonra. Bot deneyi en sonda.
- PDF'ler güncellensin; oyunu ve yazılımı baştan sona anlatan genel bir belge hazırlansın.
- Bu iki iş için iki ajan aynı anda çalışabilir.

**Araştırma: bölüm içi booster'lar (`docs/research/in-level-boosters.md`)**
- Candy Crush, Royal Match, Homescapes/Gardenscapes, Toon Blast, Fishdom, Lily's Garden, Matchington Mansion ve Project Makeover incelendi. 49 kaynak var; her bilginin ne kadar güvenilir olduğu not edildi.
- Bulgular:
  - Bizim dört booster'ımız türün standart takımı: tek taş, satır, sütun, karıştırma.
  - Booster hamle saymaz. Bir vuruş engelden bir kat alır.
  - Kurma her oyunda iki dokunuştur: önce simge, sonra hedef. Onay adımı yok.
  - Hiçbir oyun karıştırmayı reddetmiyor. Bizim motorumuz büyük açık tahtalarda ilk istekleri sık reddediyor.
  - Booster'lar birer birer, ilk kullanımı gösterilerek açılıyor; çekiç 7–10. bölüm civarında.
- Bizim §17'mizle çelişen iki alışkanlık not edildi: ödemeden sonra kazandıracak yere konan yardım, kayıpta sıfırlanan seri ödülü. Bunlar bizde olmayacak.
- Sahibine sorulacak kararlar: karıştırma reddi (motor değişikliği), booster kullanımının denemeyi başlatıp başlatmadığı, booster'ların açılış bölümleri ve bunun hoş geldin hediyesine etkisi.

**Genel Belge (`docs/OVERVIEW.md`)**
- Yeni belge, 19 sayfa. Sekiz bölüm: oyun bir bakışta, nasıl oynanır, içerik, ekonomi, yazılım mimarisi, kalite ve test, durum ve yol haritası, belgelerin rehberi.
- Tasarım belgesinin eskimiş durum satırları düzeltildi (her düzeltme tarihli): proje listesi, yol haritası, açık sorular, odaların sırası, Unity istemcisinin durumu. README kısa bir giriş sayfasına dönüştü. Kullanım kılavuzuna sunucunun kapalı olduğu ve ne zaman açılacağı yazıldı.
- Test planında üç eskimiş satır düzeltildi (T23, T27, T111).
- Kod grafiği (graphify) güncellendi.

**Kullanıcı kararları: 10. aşama (§23)**
- Karıştırma booster'ı hep karıştırsın; motorun kendi karıştırması ve bölümlerin ayarı değişmesin; kural sürümlensin.
- Kabul edilen bölüm içi booster ilk hamle gibi denemeyi başlatsın.
- Bölüm içi booster'lar birer birer açılsın: spatula 7., satır 12., sütun 16., karıştırma 20. bölüm. Hediyedeki 3'er adetleri açıldıkları bölümde verilsin.
- Her Markdown belgesinin yanında PDF'i de olsun (zaten böyle; kural kalıcı).
- Bu kararlarla 10. aşama tek ajanla başladı.

**Avatar (§23'e sonra girecek)**
- Kullanıcı, oyunun müze ve yapboz üzerinden gittiğini, bu yüzden bir avatara ihtiyaç olduğunu söyledi; avatar hikâyenin genç ressamı olacak.
- İkinci bir ajan tasarım tuvalinin üslubunda dört taslak çizdi (Defne, Kerem, Ela, Deniz).
- **Kullanıcı taslakları beğenmedi** ("çok kötü, çok çirkin"); avatarı kendisi çizip verecek. Taslaklar belgelerden kaldırıldı (git geçmişinde duruyor). Oyunun istediği çizimlerin listesi tasarım belgesinin 22. bölümüne yazıldı.

**Bot deneyi (sonra)**
- Booster'lar bitince oyunun bütün kurallarıyla oynayan, genel oyuncu kitlesini temsil eden bir bot hazırlanacak. Botun eğilimleri ölçülecek; sahte reklam yalnızca botun kendi kararlarını ölçmek için kullanılacak. Ayrıntılar kullanıcıdan gelecek.

**Arayüz: planın 10. aşaması (§23)**
- Dört bölüm içi booster rafta, dokunuşla kullanılıyor. Booster tek dokunuşla seçiliyor; seçilince adını ve işini söylüyor. Boşa kullanım hiçbir şey harcamıyor; boş diskteki artıdan satın alınıyor.
- Üç karar uygulandı:
  - Karıştırma artık hep karıştırıyor. 2000 gerçek tahtada eski kural 449 kez reddediyordu, yenisi hiç; motor izi aynı ve eski kayıtlar eski kuralla oynatılıyor.
  - Booster denemeyi başlatıyor.
  - Booster'lar 7., 12., 16. ve 20. bölümde birer birer, hediyeleriyle açılıyor.
- 3.424 test geçti; ekonomi denetimi ve simülatörün sayıları değişmedi; Unity'deki bütün denetimler hatasız. Görüntülere baktım.
- Bilinen açık: bazı ekranlarda booster yazısı en alt sıranın bir kısmını örtüyor (A24).
- Ajan bu aşamada yaklaşık 955 bin token kullandı; şimdiye kadarki en büyük aşama.

**Kullanıcı kararları (akşam)**
- Önce 11–15. aşamalar bitecek; sonra APK telefona kurulup elde denenecek; sonra telefon testleri. Avatar şimdilik atlanıyor (3D animasyon gerekecek).
- Prototip telefonda denendikten sonraki ilk iş arayüz iyileştirmeleri; GitHub'da issue olarak takip edilecek (etiket `arayüz-iyileştirme`, kilometre taşı "Prototip sonrası arayüz iyileştirmeleri"). İlk kayıt: renkler genelde mat ve düşük kontrastlı (#2).
- Kullanıcının bir sonraki oyunu: ızgaraya parça yerleştirmeli blok bulmaca (dünya pazarı; rekabet, mağaza, reklam), ayrı bir bulut oturumunda yapılacak. Başlangıç metni `docs/next-game/BLOCK_PUZZLE_PROMPT.md` (PDF'i "Başlangıç Metni").

**Arayüz: planın 11. aşaması (§23)**
- Hamle bitince "+5 hamle" teklif kartı (altın ya da günde 3 ücretsiz reklam), "TEST SÜRÜMÜ" yazılı reklam canlandırması, vazgeçme ve kaybetme kartı tasarımdaki hâlinde. Teklif açıkken deneme bitmiyor.
- Fiyat tekrarlarda artmıyor, "+5" denemede bir kez; ödeme sonrası gizli yardım yok (§17).
- 3.443 test geçti; Unity'deki bütün denetimler hatasız. Görüntüye baktım: kart çizimle birebir.

**Arayüz: planın 12. aşaması (§23)**
- Mağaza (altın paketleri "Yakında", iki booster seti, günde 3 reklamla 4'er altın), cüzdan satırında artı düğmeleri, salonda günün hediyesi, tablonun son parçasında 80 altın ve bir booster.
- Bütün cüzdan istekleri tek defterden kurallara gidiyor; ret hiçbir şeyi değiştirmiyor. Motor ve ekonomi değişmedi.
- 3.560 test geçti; Unity'deki bütün denetimler hatasız. Görüntüye baktım: mağaza ve salon çizimle neredeyse birebir; Usta seti 1.250 altında doğru olarak sönük.
- İki yeni bilinen açık: simülatörle oyunun tablo ödülü anı farklı (A26), eski kayıtlardaki tamamlanmış tablolar ödülsüz (A27).

**Arayüz: planın 13. aşaması (§23)**
- Cüzdan satırındaki çark ayarlar kartını açıyor: Dil, Gizlilik ve veri, Destek. Ses ve titreşim anahtarları 14. aşamada.
- Gizlilik kartı: taslak metin, deneme kayıtlarının sayısı, bir soruyla "Kayıtları sil". Unity'nin istatistik bayrağı kapatıldı.
- Ajan APK'nın neler gönderebileceğini inceledi: oyunun kodu ağa hiç çıkmıyor; reklam, satın alma ya da analiz paketi yok; Unity servisleri kapalı. Ama 8 Ekim APK'sı INTERNET iznini taşıyor ve Android yedeklemesi açık (A28, A29).
- 3.648 test geçti; Unity'deki bütün denetimler hatasız. Görüntüye baktım: kart çizimin dilinde, anahtar satırları olmadığı için kısa.
- Proje sahibinin kararını bekleyenler: gizlilik metni, INTERNET izninin kaldırılması, Android yedeklemesi.

## 2026-10-14 · Oturum 23 — Hoş geldin hediyesi, denge politikası, müzenin 100 tablosu, cüzdan kayıtta

**Kullanıcı kararları**
- Yeni oyunculara başta yardım: 200 altın ve her booster'dan 3. Sayıların kabaca yazıldığı söylendi; bir denge politikası istendi.
- Reklamla altın: 4 (reklam başına; günde 3 kez).
- 3–20. odaların tabloları onaylandı ("tabloları onaylıyorum"). K2–K4 öneriler doğrultusunda: Washington kırpılıp final oldu, üç dosya müzenin beyanıyla kabul, notlu üç eser kaldı.
- Bilinen açıklar not edilsin; telefon testleri hazırlansın, zamanı gelince bütün testler koşulsun.
- Sıradaki işlerden devam.

**Ekonomi: hoş geldin hediyesi ve denge politikası (§25)**
- Hediye ve reklam sayıları ayar dosyasında, kullanıcı kararı olarak işaretli.
- Simülatör artık hediyeyi de veriyor (ilk bölümden önce, ayrı bir gelir sütunu); karşılaştırma için `economy/no-welcome-gift.json`. Sonuç: ortalama oyuncu hediyeyle de 100. bölüme 8., 1000. bölüme 90. günde geliyor; kese 200–400 altın dolu. En çok zayıf oyuncuya yarıyor.
- Denge politikası: dört sabit ilişki testlerle (`EconomyBalancePolicyTests`), üç sonuç simülatörle denetleniyor. Kullanıcının sayıları bütün kurallardan geçiyor.
- Hata: hediye sayılarını "karar" olarak işaretleyen ilk kayıt iki testi kırdı (CI kırmızıya döndü); ikisi de düzeltildi.

**Müze: 3–20. odalar (§18)**
- 90 tablo kataloğa, lisans listelerine ve oyuna girdi; odalar 1–2'den geriye doğru, 15. yüzyıla kadar gidiyor. 50 tablo kırpıldı (çerçeve, paspartu, renk kartı, kâğıt kenarı); hepsine bütün olarak bakıldı.
- Getirme betiği her müzeyi tanıyor ve aynı indirmeden her zaman aynı baytları üreten bir kırpma adımı var. 1. ve 2. odalar birebir aynı kaldı.
- İş ayrı bir kopyada tek ajanla yapıldı; Unity'de içe aktarma ve müze raporu ana kopyada.

**Arayüz: planın 2. aşaması (§23)**
- Cüzdan kayıt dosyasında (katman 3); eski dosyalar hoş geldin hediyesi ve dolu canla okunuyor. Bölüm kazanılınca altın, kayıpta can aynı anda kaydediliyor (`AttemptLedger`).
- 3.194 test geçti; Unity'de kayıt dosyası, şölen, dokunma ve ekran yürüyüşü denetimleri hatasız. Kayıt dosyası denetimini ayrıca ben çalıştırdım.
- Henüz yok: can denetimi ve cüzdanın ekranda görünmesi.

**Arayüz: planın 3. aşaması (§23)**
- Motor, oyuncunun yaptıklarını sırasıyla kaydedebiliyor (hamle, booster, "+5", reddedilen istekler); deneme kaydından birebir yeniden oynatılabiliyor. §18'deki sınır kapandı.
- Kayıt kapalıyken motor aynen oynuyor: motor izi aynı, ekonomi denetimi 48 saklı oyunu bu derlemeyle kabul etti.
- Telefonda her biten deneme `attempts.jsonl` dosyasına bir satır yazıyor; `device.json` rastgele bir cihaz kimliği tutuyor.
- 3.250 test geçti; Unity'de yeni `AttemptLogCheck` dahil bütün denetimler hatasız. Kayıt dosyası denetimini ben de çalıştırdım.

**Kullanıcı kararları (akşam)**
- Yan koleksiyon parçaları oyunla kazanılan altınla alınır (§25).
- Herkese açık test sunucusu arayüz işleri bitince yeniden açılacak; o zamana kadar kapalı kalıyor.

**Arayüz: planın 7. aşaması (§23)**
- Kullanıcı müze levhalarına ve kırpmalara baktı: "güzel".
- Altın ve can satırı salon, harita ve koleksiyonda tasarımdaki yerinde; canlar gerçek saatle dönüyor, kazanılan altın sayarak artıyor. Yeni can kartı; can bitince bölüm açılmıyor. Salon "Tablo" noktalarını ve tezgâhtaki bekleyen parçayı kazandı.
- 3.276 test geçti; Unity'de bütün denetimler ve ekran yürüyüşü hatasız. Görüntülere ve tasarımla yan yana karşılaştırmalara bakıldı.
- Ajanın bulduğu bir sorun (müze birleşmesinden kalma): koleksiyon plaketi oda adlarını 6 puntoya düşürüyordu. Düzelttim: her ad kendi boyunda, uzun ad iki satırda.

**Arayüz: planın 8. aşaması (§23)**
- Her bölüm tasarımdaki bölüm başı kartından açılıyor: hedefler, hamle ve üç bölüm öncesi booster. Seçilen booster ilk hamlede harcanıyor; hiç kalmamış booster karttan tek fiyatına alınabiliyor. Kayıptan sonra "Tekrar dene" aynı karta götürüyor; can yoksa can kartı geliyor.
- Booster'la başlayan denemeler kayıttan aynı sona yeniden oynatılıyor (gerçek bölümlerle test edildi). Motor, kurallar ve ekonomi ayarları değişmedi.
- 3.322 test geçti; Unity'de bütün denetimler, kartın içinden geçen yürüyüş ve tasarımla karşılaştırma hatasız. Görüntülere baktım: kart çizime çok yakın.

**Arayüz: planın 9. aşaması (§23)**
- Tahta plaketleri, duraklatmalı yüksek raf ve kazanma kartı tasarımdaki hâlinde. Kazanma kartı ödenen altını sayarak gösteriyor; salonun altın satırı tam o kadar artıyor.
- Duraklatma çıkma kartını açıyor; kart hamleden önce mi sonra mı çıkıldığına göre can bedelini söylüyor.
- 3.349 test geçti; Unity'de bütün denetimler hatasız, şölen 5,5 saniye sınırında. Görüntülere baktım: tahta ve kazanma kartı çizimle neredeyse aynı.

**Telefon test planı**
- Yeni belge `docs/PHONE_TESTS.md` ("Test Planı" PDF'i): tam test turunun sırası, 29 bilinen açık, 118 telefon testi.

**Diğer**
- Kullanım kılavuzunda bozuk bir komut yolu düzeltildi; bölümlerin yayına alınma notu güncellendi.
- Herkese açık test sunucusu çalışmıyor; en son 12 Ekim akşamı veri yazmış. Bu oturumda durdurulmadı. Yeniden başlatmak kullanıcının kendi PowerShell penceresinden yapılmalı.

## 2026-10-14 · Oturum 22 — Kurallar istemcide: can, coin, booster (arayüz planı 1. aşama)

**Yapılan (§23)**
- Yeni proje `src/Puzzle.Meta`: cüzdan, can sayacı, kazanma ödülü, "+5 hamle" teklifi, bölüm öncesi booster seçimi, tablo ve gün ödülleri, mağaza. Düz C#; saat dışarıdan verilir; her kuralın sonucu kapalı bir türdür. Ekranda bir şey değişmedi.
- Oyun sayılarını `economy/default.json` dosyasından okuyor: `tools/unity/economy-pack.js` oyuna ait bölümleri paketliyor, `sync-engine.js` paketi ve `Puzzle.Meta` derlemesini Unity'ye koyuyor, istemcide `EconomyPack` okuyor.
- Ayar dosyasına oyuna özel alanlar eklendi (başlangıç, bölüm öncesi booster sayısı, mağaza, derleme işaretleri); simülatör biçimlerini denetliyor, simüle etmiyor.
- İş tek ajanla yapıldı; sonucu kendim denetledim.

**Doğrulama**
- 3.160 test geçti (99'u yeni); paket betiğinin 8 testi geçti.
- Simülatörün yürüttüğü yolculuklar oyunun kurallarından geçirildi: can, coin ve booster her adımda aynı. Can kuralı bilerek bozulduğunda 13 test kırmızıya döndü.
- `puzzle economy run`: 100. bölüm 8. gün, 300. bölüm 25. gün, 1000. bölüm 90. gün; yeni alanlar tek bir sayıyı değiştirmedi.
- Unity: `ProjectSetup`, `EngineSmokeTest`, `EconomyCheck`, ekran yürüyüşü hatasız. `EconomyCheck` bilerek bozulan iki pakette hata verdi.

**Benim değiştirdiğim tek sayı**
- Reklamla coin 50 yerine 20 (günde 3): 50, ortalama ücretsiz oyuncunun günlük gelirine (yaklaşık 420 coin) üçte bir ekliyordu ve dün okunan dengeyi bozardı; 20 yedide bir ekler. Yer tutucu.

**Kullanıcıya soruldu (yer tutucular)**
- Yeni oyuncunun başlangıcı: 0 coin ve her booster'dan bir tane.
- Mağaza: coin paketleri 500 / 1.200 / 2.500 (fiyatsız, satışta değil); booster setleri `starter` 900 ve `master` 6.300 coin.
- Bir denemede üç bölüm öncesi booster'ın üçü de alınabilir.

**Bilinen açıklar**
- Telefonda ve IL2CPP derlemesinde denenmedi.
- İleri alınmış saat canları dolduruyor (sunucusuz oyunda kapanmaz); geri alınmış saat can getirmiyor.
- Sıradaki aşama (cüzdanın kayıt dosyasına girmesi) için not: can sayacı zamanı tam tutuyor; kayıt tam saniyeye yuvarlarsa okunan değer eşit çıkmaz.

## 2026-10-13 · Oturum 21 — Arayüzün ortak malzemesi Unity'de: yazı tipleri, çizimler, ortak parçalar

**Kullanıcı kararları**
- Kullanıcı komut vermedikçe her iş tek ajanla yapılır; çok ajanlı iş akışı yalnızca açık onayla. Kod sorularında önce kod grafiğine bakılır (graphify, yalnızca kod; `graphify-out`, depoya girmez).
- Tablolarda odaların sırası önemli değil, dönem önemli (yeni yön Oturum 20'de yazılı). Sıra için seçimim: 1. ve 2. odadan geriye doğru.

**Arayüz: planın 4–6. aşamaları (§23)**
- Tasarımın üç yazı tipi (Gabarito, Paytone One, Young Serif) oyunda; Roboto kaldırıldı. Tuvalden 51 yeni çizim geldi; katalog 125 çizim, iki atlas. Asılı kart, yuvarlak düğme, booster diski, rozet, küçük düğme, bölüm başlığı, ilerleme sırası ve sayı yazımı ortak parça olarak yazıldı. Yeni ekran yok; var olan ekranlar yeni yazı tipleriyle çiziliyor.
- İş, Unity lisansı düştüğü için Editor'sız yazılmış ve ayrı bir dalda (`ui-means-wip`) bekletilmişti. Kullanıcı Hub'ı yeniden başlatınca lisans geldi; bütün denetimler Unity'de tek tek çalıştırıldı: `ProjectSetup` (iki kez; ikincisi hiçbir dosyayı değiştirmedi), `EngineSmokeTest`, `FinaleCheck`, ekran yürüyüşü, yazı föyü, çizim föyü, ortak parçaların föyü, `DesignPairs`. Hepsi hatasız.
- Bakılan görüntüler: yedi ekranın tasarım çizimiyle yan yana hâli, üç föy, koleksiyonun 2. odası ("Post-empresyonizm" iki düğmenin arasına sığıyor; "10. tablo" satır boyu rakamlarla). Yazılar ve var olan parçalar çizimle örtüşüyor; eksikler sonraki aşamaların işi.
- Bir hata bulundu ve düzeltildi: `DesignPairs` önce çizimi yükleyip sonra oyunun görüntüsünü alıyordu; görüntü almak yeni sahne açtığı için yüklenen çizim yok oluyordu. Sıra değişti.
- Testler: 3.061 testin 3.060'ı geçti. Kalan tek test (`PublicModeTests`, istek seli) arayüzle ilgili değil; bütün takım birlikte koşarken zamanlamaya takıldı, tek başına geçiyor. Ayrı iş olarak not edildi.
- İş ana dala (`feature/bombs-colors-director-webui`) birleştirildi.

**Tablo önerisi dönemlere göre yenilendi (onay bekliyor)**
- 3–20. odalar için 18 dönem, 90 eser ve 52 yedek; hepsi müzenin kendi serbest bıraktığı görüntüler. 144 eser tek tek doğrulandı: 122'si sorunsuz, 20'si notlu, 2'si elendi. Sayfa güncellendi; kararlar K2 (bir tablonun kenarlarından yüzde 3 kırpma), K3 (üç eserde müzenin kendi beyanına güvenmek) ve K4 (üç eserde içerik notu). Onaysız kataloğa bir şey girmiyor.

**Ekonomi: ayar araması**
- 5 canla ortalama ücretsiz oyuncu 100. bölüme, başka ne ayarlanırsa ayarlansın, 8. günde geliyor. 10–14 gün hedefini yalnızca 1 can (10. gün) ya da 16 saatlik can dolumu (11. gün) tutturuyor. Önerim: 5 can ve 8. gün; 10 dakikalık oturum esas alınsın. 5 canla altın dengesi için kalan hamle başına 4 altın, tablo başına 80 altın, "+5" 900, bölüm başı booster'ları 500 / 200 / 200 çıkıyor.
- Arama sırasında yazılan öneri dosyaları depoya girmedi.

**Ekonomi kararı (kullanıcı: önerilerle başla)**
- 5 can ve 30 dakika kalıyor; ortalama oyuncunun 100. bölüme 8. günde gelmesi kabul edildi. Hesaplar 10 dakikalık oturuma dayanıyor.
- `economy/default.json` artık oyunun ayarı: kalan hamle başına 4 coin, tablo başına 80, "+5 hamle" 900, renk bombası 500, roket ve bomba 200, "+3 hamle" 200. Coin sayıları dosyada "öneri" olarak işaretli; hız hedefi 7–9. gün olarak "karar". Bölüm içi booster fiyatları ve yan koleksiyon parçası yer tutucu.
- Değişken dosyalar sayıyla adlandırıldı (`sessions-6-minutes`, `sessions-20-minutes`, `sessions-60-minutes`); bırakılan iki seçenek karşılaştırma için duruyor (`one-life`, `life-every-16-hours`). Tek canlı öneri dosyaları ve arama dosyaları depoya girmedi (`out/economy/settings-explored`).
- Doğrulama: ölçüm bu derlemeyle ve bölüm dosyalarıyla denetlendi (48 saklı oyun yeniden oynandı); sekiz ayar dosyası simüle edildi; 112 ekonomi testi geçti. Sonuç: 100. bölüm 8. gün, 300. bölüm 25. gün, 1000. bölüm 90. gün; ortalama oyuncunun kesesi 4–7 "+5" değerinde (§25).
- Duyarlılık: 20 dakikalık oturumda 100. bölüm 4. güne iner ve coin daralır (kese yaklaşık 900); 6 dakikalıkta 12. güne çıkar ve coin birikir.
- Açık: yan koleksiyon parçaları oyun coin'iyle alınabilsin mi (önerim evet), coin sayılarının tek tek onayı, bölüm içi booster fiyatları.
**Diğer**
- Yere konma esnemesi düzeltmesi ana dalda (§23).
- Yapılmadı: APK ve telefon denemesi (ertelenmişti), ses, planın 1–3. ve 7–15. aşamaları.

## 2026-10-13 · Oturum 20 — Unity'de bitiş şöleni; oda önerisi, UI planı ve ekonomi simülatörü yolda

**Kullanıcı kararları (2026-10-12 akşamı)**
- Kalan işlerin sırası onaylandı: Unity'de bitiş şöleni, 3–20. odaların tablo önerisi, ekonomi simülatörü.
- Ekonomi simülatörü önerdiğim varsayımlarla başlasın: ücretsiz ortalama oyuncu 100. bölüme 10–14 günde; günde 3 oturum; reklamla "+5" günde 3 kez; booster hem coin'le hem ödül olarak.
- "UI tarafı da tamamlansın": tasarım tuvalindeki bütün ekranlar Unity istemcisine gelecek.

**Bitiş şöleni Unity'de (§23)**
- Kazanınca kalan her hamle madalyondan bir kıvılcım olarak tahtaya uçuyor, taşı fırça kovanına çeviriyor, kovanlar patlıyor; sonra sonuç kartı geliyor. Dokunmak ya da geri tuşu atlatıyor.
- Süreler: 1 hamle 1,3–1,7 s; 3 hamle 2–3 s; 12 hamle 4–4,6 s; 30 hamle yaklaşık 4 s; üst sınır 5,5 s.
- İş akışı: iki bağımsız tasarım önerisi, tek tasarım, uygulama, üç bağımsız eleştiri (akış, görünüm, kod), düzeltme. Eleştiri 15 bulgu çıkardı; 13'ü düzeltildi. En önemlileri: uçan kıvılcımın parçaları aynı çizim sırasını paylaştığı için görünüşü şölenden şölene değişiyordu; bir roketin fırça darbesi, başka bir etkinin sonradan patlattığı taşın üstünden geçiyordu (gerçek şölenlerin yaklaşık yüzde 3'ünde); raf, şölen sırasında "sürükle ya da dokun" demeye devam ediyordu; ikinci parmak şöleni atlatamıyordu.
- Motor değişmedi. Kazanım, kazandıran hamle oynandığı anda kaydediliyor; şölen bunu etkilemiyor (akış denetimi ilerleme dosyasına bakarak kanıtlıyor).
- Doğrulama: 2.839 test geçti (118'i yeni). Unity'de `FinaleCheck`, `FinaleStory` (üç ekran boyu, engelli tahtalar, iki dalgalı şölen, atlama), `TouchWhileMovingCheck`, `EngineSmokeTest`, `PlayModeCheck` ve ekran yürüyüşü hatasız. Karelere bakıldı.
- Yapılmadı: APK ve telefon denemesi (ertelenmişti); kare hızı ve gerçek parmakla atlama ölçülmedi. Ses yok.
- Ayrı iş olarak not edildi (şölenden eski): yere konup hemen yeniden düşen parçanın esnemesi bir sonraki hareketiyle çakışabiliyor; görünür bir etkisi bulunmadı.

**3–20. odaların tablo önerisi (onay bekliyor)**
- Oda planı, altı araştırma ve üç bağımsız denetimle 18 akım ve 90 tablo önerildi; her tablonun Commons dosyası, boyutu, müzesi ve lisansı tek tek doğrulandı. 22 tablo notsuz uygun, 64'ü notlu, 4'ü fazla karanlık bulunup reddedildi (yedekleri önerildi). 25 tablonun dosyası müzenin kendi açık erişim fotoğrafı değil, "kamu malı reprodüksiyon".
- Geçici listedeki Barok, Neoklasizm, Maniyerizm, Puantilizm, Barbizon ve Venedik Okulu kurallardan geçemedi; yerlerine Hudson Nehri Okulu, Amerikan Empresyonizmi, ikinci bir Japon baskı odası, Venedik manzaraları, İspanyol ustaları ve ikinci bir Empresyonizm odası önerildi.
- Öneri küçük görselli tek bir sayfa olarak kullanıcıya sunuldu. Onaylanmadan kataloğa bir şey girmiyor, tam boy görüntü inmiyor. Veri ve sayfa üreticisi `out/museum-proposal` altında (depoya girmez).
- Bulgu: Mauritshuis görüntülerini ticari kullanım için serbest bırakmıyor; §18'deki açık erişim müzeleri listesine girmez.

**UI tamamlama planı (hazır, başlamadı)**
- Tasarım tuvali, istemci ve kurallar ayrı ayrı envanterlendi; 15 aşamalı bir plan çıktı: önce kurallar (can, coin, booster envanteri, ödüller; düz C#), sonra yazı tipleri ve ortak parçalar, sonra ekranlar oyuncunun karşılaştığı sırayla. 23 kararın her birinin bir önerisi var.
- Hesap ya da para gerektiren hiçbir şey planda yok: gerçek para paketleri "Yakında" olarak, reklam "test sürümü" etiketli bir canlandırma olarak duracak; fiyatlar ekonomi ayar dosyasından okunacak.
- Başlaması için ekonomi simülatörünün ana dala alınması gerekiyor.

**Ekonomi simülatörü (ana dalda; §25)**
- Ayrı bir kopyada kuruldu (iki tasarım önerisi, tek tasarım, uygulama, üç eleştiri, düzeltme) ve ana dala birleştirildi. `puzzle economy measure | run | compare`; ayarlar `economy/default.json` ve beş değişkeni.
- İlk tam ölçüm: 862.512 oyun, yaklaşık 13 dakika. Varsayılan ayarlarla ortalama ücretsiz oyuncu 100. bölüme 4. günde, 1000. bölüme 48. günde geliyor; hedef (10–14 gün) ancak 6 dakikalık oturumla tutuyor. Hızı oturum süresi belirliyor; 20 dakikalık oturumda canlar hiç devreye girmiyor.
- Bölümlerin ayarı bağımsız olarak doğrulandı: ilk tohumda 55. basamak yüzde 90,5 / 75,8 / 56,2 / 41,6.
- Eleştiri 29 bulgu çıkardı; 22'si düzeltildi. En önemlileri: "eksik coin" sütunu gerçek açığın yarısını gösteriyordu; kural testleri ayarlanabilir dosyaya bağlıydı; koruma, ölçümdeki "+3" ve "+5" değerlerini bu derlemeyle karşılaştırmıyordu.
- Kullanıcıya iki karar soruldu: hangi oturum alışkanlığı esas alınsın, 1000 bölümün 7–13 haftada bitmesi kabul mü.

**Yere konma esnemesi düzeltildi (kullanıcının ilettiği görev)**
- Şölen işi sırasında bulunan eski kusur: yere konan parça esneme bitmeden yeniden hareket ederse esneme sonraki hareketin içine taşıyordu. Görünenden yaygınmış: hamle senaryolarının yüzde 11'i, şölenlerin yüzde 36'sı. Testlerin kendi kuralını bozan durum nadirdi (356 bin senaryoda 8; hepsi art arda çıkan malzemeler).
- Düzeltme `TurnChoreographer.Travel` içinde: parçanın son esnemesi, yeni hareket başladığı anda biter. Eski ve yeni kodla yazılan 1,29 milyon senaryo yan yana karşılaştırıldı: taşan esnemelerin süresi dışında hiçbir şey değişmedi.
- Bağımsız inceleme iki küçük bulgu çıkardı (bir yuvarlama, şansa geçen bir test); ikisi de düzeltildi.
- Ayrı iş olarak not edildi: bantlı bir parça, bandının çıktığı gösterilmeden hareket edebiliyor.

**UI'ın ilk bölümü (4–6. aşamalar): yazıldı, Unity'de kanıtlanamadı**
- Tasarımın üç yazı tipi (Gabarito, Paytone One, Young Serif), 51 yeni sembol ve yüzey, ortak parçalar (asılı kart, disk düğme, booster diski, küçük düğme, rozet, bölüm başlığı) yazıldı; iki eleştiri ve düzeltme yapıldı.
- **Engel:** Unity Editor 13 Ekim 02:20'den beri lisans bulamıyor (çıkış kodu 198); Unity Hub'ın oturumu düşmüş. Yalnızca kullanıcı yeniden giriş yapabilir. 4. aşama o saatten önce Unity'de görüntülenmişti; 5 ve 6. aşamalar ile düzeltmeler Unity'ye hiç girmedi.
- Bu yüzden iş ayrı bir dalda bekliyor (`ui-means-wip`); ana dalda değil. Yeni görsellerin Unity dosyaları ve varlık kataloğu `ProjectSetup` çalışınca oluşacak; o dalda iki test bu yüzden kırmızı.

**Tablolar için yeni yön (kullanıcı, 2026-10-13)**
- Yalnızca üzerinde telif olmayan eserler; müze dönem dönem ilerler ve her tablo odasının döneminden olur; tür serbest; ressamın ünlü olması gerekmez, ünlü portreler (Washington portresi gibi) aranır. 1. ve 2. odalar kalır.
- İlk öneri (akımlara göre; 25 tablosu "kamu malı reprodüksiyon" dosyası) geçersiz. Yeni araştırma 3–20. odaları yaklaşık 1400–1900 arasında on sekiz dönem olarak, yalnızca müzesinin her kullanıma açıkça serbest bıraktığı görüntülerle kuruyor; ilk önerinin bu ölçüyü tutan 96 eseri başlangıç havuzu.

## 2026-10-12 · Oturum 19 — Bankadan derlenen bölümler yayında

**Kullanıcı kararları**
- Malzeme bölümlerinin hamle aralığı şimdilik değişmiyor.
- Yayına alınacak küme: büyüyen bankadan yeni derlenen ("en sonuncu").
- Telefonda deneme şimdilik yok; kalan işlerle devam.

**Yapılanlar**
- **Derleme:** büyüyen bankadan (5.420 aday) 1000 bölüm sıfırdan derlendi: `puzzle assemble --policy policies/default.json --base out/assemble/pilot-base --bank D:\puzzle-bank` (9,9 dakika; 665 bin oyun bankadan okundu, 773 bin denetim oyunu oynandı).
- **Sonuç:** 50 bölüm sabit (ilk oda, bayt bayt aynı), 948 yeni tahta, 2 bölüm (52, 53) aralığa oturmadığı için eski hâliyle ve "eskimiş" işaretiyle kaldı (açıkça kabul edilerek). 1000/1000 geçerli.

| 51–1000, 55. basamak | Kolay | Normal | Zor | Süper Zor |
|---|---|---|---|---|
| Hedef | %90 | %75 | %55 | %40 |
| Derleyici, bölümü kabul eden oyunlar | %91,4 | %76,2 | %55,7 | %41,0 |
| Derleyici, kabulden sonra oynanan oyunlar | %91,6 | %76,8 | %56,0 | %41,5 |
| Bağımsız ölçüm (`ladder`), bölümün kendi havuzunda | %91,4 | %76,7 | %56,6 | %40,7 |
| Ortalama limit | 23,9 | 27,5 | 31,0 | 35,2 |

- **Tür dağılımı (1000 bölüm):** yalnızca renk toplama %34,8 (hedef %35), kutulu %25,1 (%25), zincirli %24,1 (%25), jöleli %18,3 (%18), malzeme indirme %3,6 (hedef %6; hamle aralığı değişmediği için).
- **Yayın:** devam eden deneme yoktu (en ilerideki oyuncu 40. bölümü kazanmış; 50'nin üstünde kayıt yok). Sunucu durduruldu, `puzzle promote ... --accept 52,53` çalıştırıldı (52 dosya yerinde, 948 değişti; değişen her dosyanın yedeği `out/promote/2026-10-12T151154Z`), sunucu WMI ile yeniden başlatıldı. Açık adres yanıt veriyor; `humans`: 77 kayıt, klasörde olmayan 0.
- `levels/generated` artık `assembly.csv` ve `assembly.manifest` de taşıyor: her bölümün bankadaki hangi kayıttan geldiği ve hangi kurallarla karar verildiği. Bundan sonra derlemeler `--base levels/generated` ile yapılır: hâlâ uyan bölüm yerinde kalır.
- `reports/ladder.csv` ve `reports/expert_moves.csv` yeni bölümlerde yenilendi; Unity'nin bölüm paketi de (`sync-engine.js`).

**İnsan verisi (77 kayıt, 62'si karşılaştırılabilir):** insanlar %69,4; aynı oyunlarda novice bot %66,6, casual %81,3. Oyuncu hâlâ en çok novice bota benziyor.

**Açık**
- Malzeme bölümleri az (36); 52 ve 53 eskimiş. Türe özel hamle aralığı kararı ertelendi.
- Yeni bölümlerle APK derlenmedi (telefon denemesi ertelendi).

### Aynı gün: motorun "hazır" tanımındaki son adım (§21, 4. adım)

**Yapılanlar**
- **Booster'lar.** Bölüm öncesi üç booster oyun seçeneği oldu (roket + bomba, renk bombası, +3 hamle); bölüm içi dört booster oyuncunun hamle saymayan eylemi (hücre, satır, sütun vuruşu, karıştırma). Botlar kullanmaz; ölçümler booster'sız kalır.
- **"+5 hamle" teklifi.** Hamlesi biten oyun 5 hamleyle sürebiliyor; tahtada hamle kalmadıysa önce karıştırılıyor.
- **Bitiş şöleni.** Kazanılan oyunda istemci isteyince kalan her hamle bir rokete dönüşüp patlıyor; sonuç, kullanılan hamle ve oyun toplamları değişmiyor.
- **İçerik sürümü.** Her bölümün bir içerik sürümü var; 1. sürüm dosyaya yazılmıyor, deneme kaydı oynanan sürümü taşıyor.
- **Düzeltme paketi.** Değişen bölümleri bütün hâlinde ve yeni sürümleriyle taşıyan paket; cihazdaki bölümlerin üstüne serilir. `puzzle patch make` ve `puzzle patch apply` komutları.
- Web sunucusunun olay eşlemesi booster olayını tanıyor; Unity'nin bölüm okuyucusu yazılmamış sürümü 1 sayıyor.

**Benim verdiğim kural kararları (belgede yoktu; itiraz edilebilir)**
- "+5 hamle" bir denemede bir kez alınır (deneme kaydında tek alan var).
- Hiçbir şeyi değiştirmeyecek booster kullanımı reddedilir, booster harcanmaz. Karıştırma yalnızca gerçek bir yeniden dizilişi kabul eder (taş boyamaz).
- Karıştırmayla da hamle çıkmayan tahtaya "+5 hamle" satılmaz.
- Vuruş booster'ları roketin vurduğu gibi vurur: hücre başına bir vuruş, yanındaki kutulara yan hasar yok.
- Bölüm öncesi özel taşlar rastgele serbest taşların yerine konur; aynı booster'lar hangi sırayla istenirse istensin aynı başlangıcı verir.
- Şölende yalnızca kalan hamleler rokete döner; tahtada kalmış özel taşlar patlatılmaz. Coin miktarı motorun dışında.
- Düzeltme paketi alan alan yama değil, bölümün tamamıdır. Tek paket her uygulama sürümüne hizmet eder, sıra numarası taşır; bir sürüm numarası bir içeriği sonsuza dek adlandırır.

**Bağımsız inceleme (commit'ten önce) ve düzeltilenler**
- *Sürüm bir içeriği tek başına adlandırmıyordu:* ikinci uygulama sürümünde ya da önceki paket unutulduğunda aynı numara iki içeriğe verilebiliyor, eski uygulama düzeltmesini kaybedebiliyordu. Paket yeniden tasarlandı: her sürüme hizmet eden tek paket, sıra numarası, önceki paketin zorunlu olması, yayın klasöründeki sürümlerin de okunması, çelişkide durma.
- *İçerik sürümü kayda yazılıyor ama hiçbir yerde okunmuyordu:* özet ve insan–bot karşılaştırması artık sürüme göre grupluyor ve eşliyor.
- *Şölen hedef sayaçlarını ilerletiyordu:* şölenden sonra yazılan kayıt yanlış olurdu. Sayaçlar şölenden önceki hâline döndürülüyor.
- *Hiçbir şeyi değiştirmeyen karıştırma kabul ediliyordu* (az serbest taşlı tahtalarda üçte bire varan oranda). Artık yalnızca gerçek yeniden diziliş kabul ediliyor.
- *Diriltilemeyen tahtaya "+5 hamle" satılıyordu* ve oyun hemen kaybediliyordu. İstek artık reddediliyor.
- Paket dosyasındaki boş alanlar belgelenmemiş hatalarla düşüyordu; kopyalar booster listesini paylaşıyordu; kayıttaki hamle limiti booster'ın +3'ünü içerebilirdi. Hepsi düzeltildi.
- Düzeltilmedi, sınır olarak belgelendi: booster'lı deneme kayıttan birebir yeniden oynatılamaz (tek bir eylem kaydı gerekir); "+5 hamle" istemciye bağlandığında kayıt, teklif sonuçlandıktan sonra yazılmalı.

**Doğrulama**
- 2.721 test geçti (51'i yeni). Motorun çalışma izi (`engine-trace.txt`) ve kayıt biçimi örnekleri bayt bayt aynı.
- Gerçek veriyle: `puzzle humans` 87 kaydın hepsini bölümüyle eşledi (klasörde olmayan 0), yani parmak izleri değişmedi. `puzzle assemble --base levels/generated` yeni derlemeyle 36 saniyede 1000 bölümü olduğu gibi bıraktı (948 yerinde, 50 sabit, 52–53 eskimiş); bankanın dönemi aynı, saklı oyunlar bayt bayt aynı oynandı.
- Düzeltme paketi dört bölümün kopyasında uçtan uca denendi: ilk paket, ondan üretilen ikinci yayın klasörü, ikinci yayına göre üretilen ikinci paketin ilk yayındaki üç bölümü de düzeltmesi.
- Unity yeni motorla derlendi; kendi duman testi geçti (1000 bölüm okundu, 12 sabit oyun aynı).

**Açık**
- İstemci (Unity): booster düğmeleri ve animasyonları, şölenin oynatılması, paketin indirilip uygulanması.
- İlk yayında bölüm klasörünü "yayın klasörü" olarak dondurmak (paketler ona göre üretilir).
- Booster sayıları, coin ve fiyatlar: ekonomi simülatörü (Faz 3).

## 2026-10-08 · Oturum 18 — İkinci oda ve zorluk için kalıcı çözüm: bölüm bankası

**Kullanıcı kararları**
- İkinci odanın beş tablosu onaylandı.
- Telefonda deneme sonraya bırakıldı.
- "Bölüm zorluk ayarlarına kalıcı bir çözüm bulmamız gerekiyor; üretilen bölümlerin çoğunu harddiske kaydedebiliriz."

**İkinci oda (Post-empresyonizm)**
- Otoportre (Van Gogh, 1887), Elma Sepeti (Cézanne, y. 1893), Moulin Rouge'da (Toulouse-Lautrec, 1892–95), Yatak Odası (Van Gogh, 1889) ve oda finali olarak Grande Jatte Adası'nda Bir Pazar Öğleden Sonrası (Seurat, 1884–86). Hepsi Art Institute of Chicago'da kamu malı; kaynak ve lisans `art/paintings/room-02/licences.json`. Müze artık 100. bölüme kadar gidiyor.
- "Müze tabloların bittiği yerde biter" davranışını sınayan testler, oyunun büyüyen kataloğu yerine dondurulmuş tek odalı bir kataloğa bağlandı (`tests/fixtures/catalogue-one-room.json`).
- APK: 58,6 MB (on tabloyla).

**Bölüm bankası ve derleyici (GAME_DESIGN §24)**
- **Neden:** zorlukla ilgili her karar (oyuncuyu hangi bot temsil ediyor, hangi pay, hamle payı, çeşitlilik) 1000 bölümün baştan üretilmesi demekti: 40 dakika, her seferinde başka tahtalar, eski tahtalardaki oyuncu kayıtları boşta.
- **Ne yapıldı:** pahalı ve rastgele olan iş (tahta çizmek, botlarla oynatmak) ile ucuz ve bilinçli olan iş (hangi bölüm numarasına hangi tahta, hangi limit) ayrıldı.
  - **Banka** (`D:\puzzle-bank`, depo dışında): bölüm numarasına bağlı olmayan aday tahtalar; her biri merdivendeki 15 botla limitsiz oynatılıp oyunlarıyla saklanıyor. "Şu oyuncu şu payı kazansın, limit kaç?" sorusu kayıttan okunuyor.
  - **Derleyici** (`puzzle assemble`): bir politika dosyası (`policies/default.json`: oyuncu, kademe başına pay, hamle payı, tür hedefleri, sabitlenen bölümler) ve her bölüm numarasının planı ile bankadan tahta seçer; seçilen her bölümü yeni oyunlarla denetler; bölüm dosyalarını ve raporu yazar.
  - **Yayına alma** (`puzzle promote`): sunucu çalışırken reddeder, klasörü kendi dökümüyle karşılaştırır, kaydı olan değiştirilen bölümleri arşive alır, her değişen dosyayı yedekler.
- **Nasıl yapıldı:** sekiz ajanlı iş akışı: tasarım → iki tasarım eleştirisi (istatistik, mühendislik; kod yazılmadan) → banka → derleyici → deneme → bağımsız eleştiri → düzeltme. 7,8 saat, 3,0 milyon token. Yeni proje `src/Puzzle.Bank` (7.400 satır); testler 2489 → 2670.
- **Tasarımda düzeltilen varsayımlar:** miktar hedefleri (N taş topla) tek uzun oyundan okunamıyor, çünkü bot hedefe göre oynuyor: her miktar ayrı ölçülen bir aday. Oyuncu yalnızca havuz tohumlarını oynadığı için denetim rastgele tohumlarda değil, bölümün kendi havuz tohumlarında yapılıyor. "Saniyeler içinde yeniden derleme" yalnızca hiçbir şey değişmediğinde doğru; pay değişikliği dakikalar, yeni bir oyuncu basamağı ilk seferde bir yeniden üretim kadar sürüyor, ama tahtalar yerinde kalıyor.

**Deneme sonuçları (banka: 2.465 aday, 8,85 milyon oyun, 87 MB)**

| 51–1000, 55. basamak | Kolay | Normal | Zor | Süper Zor |
|---|---|---|---|---|
| Hedef | %90 | %75 | %55 | %40 |
| Derleyicinin kendi denetimi | %91,5 | %76,6 | %56,8 | %41,3 |
| Bağımsız ölçüm, bölümün kendi havuzunda | %91,6 | %76,5 | %57,1 | %40,7 |
| Bağımsız ölçüm, yeni tohumlarda | %91,6 | %77,5 | %58,1 | %43,6 |
| Ortalama limit (yayındaki) | 24,7 (25,2) | 28,0 (29,3) | 31,3 (31,9) | 35,7 (36,8) |

- **Tür dağılımı artık hedef:** yalnızca renk toplama 335 (hedef %35), kutulu 233, zincirli 243, jöleli 174 (yayında 110), malzeme indirme 41 (yayında 61; hedef %6).
- **İki hazır küme** (ikisi de `out/assemble` altında, yayında değil): *sıfırdan* (948 yeni tahta; çeşitlilik hedeflerde) ve *yayındakinden* (941 bölüm bayt bayt aynı; aralık dışında kalmış 7 bölüm uygun tahtayla değişti: 358, 730, 771, 795, 813, 841, 980). İkisinde de 1–50 bayt bayt aynı.
- **Politika değişikliği:** iki kademede pay değişince 285 bölüm için 3,7 dakika; çoğunda tahta aynı kaldı, yalnızca limit değişti.

**Bağımsız eleştirinin bulduğu ve düzeltilenler**
- Engelleyici: motor ya da bot kuralı değişince bankanın tamamı tek bayrakla devre dışı kalıyordu. Artık `bank remeasure` var, yeni dönem açmak adıyla onay istiyor, her bölüm hangi kurallarla ölçüldüğünün damgasını taşıyor.
- Plan değişikliği yerinde duran bölümlerde fark edilmiyordu; aralık dışı ve eskimiş bölümler özetlerde görünmüyordu; kayıtlar bölüm numarasıyla korunuyordu (artık parmak iziyle); yayına alma ve iz sürme yoktu; hasarlı bankayı derleyici fark etmiyordu; çeşitlilik bir dilekti, denetim değildi.

**Bulgular**
- **Malzeme bölümleri hâlâ az.** 55. basamak botu için üç malzemeli bir tahta Normal aralığına (20–33) altı yüz çizimde bir oturuyor. Politika artık türe özel hamle aralığı kabul ediyor (`kindMoves`); malzeme için Normal 20–42 ve Zor 24–44 denemesinde bankadaki uygun malzeme tahtası 81'den 159'a çıkıyor. Karar kullanıcıda.
- 52. ve 53. bölümler (malzemenin tanıtıldığı bölümler) aralığa oturmuyor; "eskimiş" olarak işaretli.
- Derlenen bölümler kendi havuzlarında hedefin 1,5–2 puan üstünde (biraz kolay).
- Zamanlamaya duyarlı bir kayıt testi (`AttemptRecordingTests`) bütün paket birlikte koşarken ara sıra düşüyor, tek başına geçiyor; bu işle ilgisi yok.

**Bankanın büyütülmesi (aynı gün, 12:14–14:36)**
- Büyütme oturumdan bağımsız bir işlem olarak, makinenin yarı gücüyle (6 iş parçacığı) ve aracın bankanın yanına alınmış bir kopyasıyla başlatıldı (`D:\puzzle-bank-tools`), böylece depodaki araç bu sırada yeniden derlenebiliyor.
- 141,7 dakikada kendiliğinden bitti: "banka, çizimlerin verebildiği kadar derin". 2.955 yeni aday saklandı (dakikada 20,9).
- **Banka şimdi:** 5.420 aday, 5.142 tahta, 14,5 milyon oyun, 151 MB, 29 parça dosya. Yarım kayıt ya da hasar yok; `bank verify` saklı oyunları yeniden oynatıp bayt bayt aynı buldu.
- **Zamanla dolmayan tek yer:** malzemenin tanıtıldığı bölümlerin sınıfları (Normal, 20–33 hamle, 45 / 55 / 60. basamaklar). Çizimler bu aralığa oturan malzeme tahtası vermiyor; aracın kendi sonucu: "daha çok zaman değil, tür için daha uzun bir hamle aralığı gerekir".
- Kullanıcının isteğiyle bu noktada duruldu: derleme yapılmadı, hiçbir şey yayına alınmadı.

**CI düzeltmesi**
- PR'daki denetim bir testte düştü: bankanın "aynı anda tek yazıcı" kilidi Windows'ta çalışıyor, Linux'ta ikinci yazıcıyı da içeri alıyordu (.NET, okumaya açık bırakılan dosyayı Unix'te paylaşılan kilide çeviriyor). Kilit dosyası artık hiçbir şekilde paylaşılmıyor; kimin tuttuğu yanındaki not dosyasına yazılıyor. Denetim geçti.

**Açık**
- Hangi kümenin yayına alınacağı (banka büyüdü; yeniden derlemek gerekir).
- Malzeme bölümlerinin hamle aralığı.
- Dar tahtalar (8–9 sütun) bankaya aynı yolla eklenebilir; istenirse.

## 2026-10-08 · Oturum 17 — Renkler, daha büyük tahta ve müze tarafı

**Kullanıcı kararları (2026-10-07 akşamı, telefonda oynadıktan sonra)**
- Taş renkleri daha canlı ve uyumlu olsun: altı panelli karşılaştırmadan **4. panel** seçildi (doygun taşlar, koyu camgöbeği zemin).
- Oyun alanı biraz daha büyük olsun ("küçük olunca göz yorma ihtimali yüksek").
- Müze tarafına başlansın; kurallar §18'deki kararlar.
- "İndir ve bu küçük işlemler için izin isteme": küçük ve gerekli işlemler sorulmadan yapılır, sonra bildirilir.
- Harici disk bağlandı (D:, 720 GB boş).

**Nasıl yapıldı**
- Yedi aşamalı sıralı iş akışı: renkler → daha büyük tahta → müzenin kuralları → gezinme, ana ekran, harita → yapboz ve koleksiyon → bağımsız eleştiri → düzeltme. Yaklaşık 4,5 saat, 7 ajan, 3,5 milyon token.
- Depolama: `tools/unity/Move-CachesToDrive.ps1 -Drive D` Unity'nin derleme ara dosyalarını (`Library/Bee`, 4,4 GB), derleme çıktısını ve görüntü klasörünü D: sürücüsüne taşıyıp yerlerine bağlantı bıraktı. Disk takılı değilken derleme ve görüntü alma çalışmaz.

**Renkler (4. panel)**
- Kural OKLCH'de: renkli tonların doygunluğu 1,3 katı (koyu tonlarda 1,15), açıklık hafifçe düşer; yeşil sarıya (140°), elmas maviye (220°) doğru kayar; beyazlar ve nötrler aynı kalır. İlk denemede renkleri açmak pastel sonuç verdi ve atıldı.
- Kaynakta uygulandı: `tools/web-theme/gem-palette.js` (testli) ve çizim derlemesindeki iki adım (palet, koyu zemin için gölge). Web testindeki `pieces.svg` ve Unity'nin PNG'leri aynı derlemeden çıkar.
- Tepsi: koyu camgöbeği dama (#124652 / #0f3e49), web sayfasında ve Unity'de. Koyu zeminde okunmayan şeyler ayarlandı: taş gölgesi, mürekkep (fırça darbesi ve leke), seçili taş işareti. Web sayfası değişiklikleri kaydedildiği anda oyunculara yansıdı.

**Daha büyük tahta**
- Düzen üç ekran biçimine göre ayarlanıyor; çerçeve payı daraldı, plaketler ve raf gerektiğinde küçülüyor.

| Ekran | Tahta | Taş boyu: önce → sonra |
|---|---|---|
| Dış ekran (1080×2424) | 10 sütun | 99 px (5,96 mm) → 102 px (6,14 mm) |
| Dış ekran | 11 sütun | 90 px (5,42 mm) → 93 px (5,60 mm) |
| İç ekran (2076×2152) | 10 satır | 151 px (10,3 mm) → 167 px (11,4 mm) |
| İç ekran | 13 satır | 116 px (7,9 mm) → 128 px (8,7 mm) |

- **Dış ekranda sınır genişlik.** Tahta zaten genişliğin %92'sini kullanıyordu, şimdi %94–95. Daha iri taş için daha dar tahta gerekir (9 sütun 113 px, 8 sütun 127 px); bu bölüm tasarımı kararıdır.

**Müze**
- **Kurallar `src/Puzzle.Museum` (netstandard2.1, yeni proje):** 20 oda × 5 tablo × 10 bölüm, yapboz boyutları (10 / 20 / 30 / 40 parça; 3-4-3, 5×4, 6×5, 8×5), dikey tabloda dönen dizilim, aynı tohumla aynı kesim (girintili çıkıntılı klasik parçalar), ilerleme (kazanılan parçalar kazanılan bölümlerden türetilir; yerleştirilenler ayrı tutulur; bir parça iki kez kazanılamaz ya da yerleştirilemez). Sunucu ve araçlar da kullanabilsin diye Unity'nin dışında.
- **Katalog veri dosyası:** `museum/catalogue.json` (20 oda; 1. oda Empresyonizm beş tablosuyla; 2. oda Post-empresyonizm; sonrakilerin adı geçici).
- **Tablolar:** 1. odanın beş tablosu tam boyda indirildi (`tools/unity/fetch-paintings.js`; Art Institute of Chicago'nun kendi çekimleri, Wikimedia Commons üzerinden; müzenin kendi sunucusu otomatik istekleri reddediyor). Görüntüler oyunun içinde (`unity/Assets/Resources/Paintings/room-01`), kaynak ve lisans listesi `art/paintings/room-01/licences.json` içinde.
- **Ekranlar:** oyun artık müzenin salonuna açılıyor (odanın duvarı, onarılan tablo, büyük "Bölüm N" düğmesi). Alt çubuk: Koleksiyon, Müze, Harita. Harita 20 odanın kat planını gösterir. Koleksiyonda tamamlanan tablolar büyük açılır, ulaşılmayanlar örtülüdür.
- **Yapboz:** tablo yaldız çerçevede; parçalar tepsiden sürüklenir, kendi yerine yaklaşınca oturur; "Otomatik tamamla" hepsini yerleştirir. Tablo tamamlanınca kutlama, oda tamamlanınca "Oda onarıldı!".
- **Akış:** kazanılan bölümün sonuç kartı kazanılan parçayı gösterir ("Parçaları yerleştir" / "Devam"). Bölümden çıkış eklendi: rafın solundaki geri düğmesi ve telefonun geri tuşu önce sorar; hamle yapıldıysa deneme sayılır.
- **Eski kayıt:** önceki sürümün ilerleme dosyası okunuyor; kazanılmış bölümlerin parçaları yerleştirilmeyi bekler.
- **Müze tabloların bittiği yerde biter:** 51. bölümden sonra parça verilmez, salon "Yeni tablolar hazırlanıyor" der. Çalışmayan hiçbir şey gösterilmiyor (altın, can, mağaza, takvim yok).

**Bağımsız eleştirinin bulduğu ve düzeltilenler**
- Engelleyici: oyun küçük önizleme resimleriyle derlenecekti (tam boy dosyalar kullanıldı); 51. bölümden sonrası boş bej panellerdi (müze tabloların bittiği yerde bitiyor).
- Bölümden çıkılamıyordu; yapboz her parçadan sonra yarım saniye dokunuşu yutuyordu; koleksiyon rozetleri ekran kenarında kesiliyordu; örtü kumaş gibi durmuyordu; oda tamamlanmasının kendi anı yoktu.
- Web sayfasında mürekkep lekesi koyu zeminde neredeyse görünmüyordu (karşıtlık 1,1 : 1): renkler Unity'dekilerle eşitlendi (bu oturumda, iş akışından sonra).

**Paket boyutu bulgusu (iş akışından sonra)**
- İlk derleme 87 MB çıktı (önceki 36 MB): beş tablo pakete sıkıştırılmadan girmişti. Neden: Unity, kenarları ikinin kuvveti olmayan bir resmi mipmap'i varsa sıkıştırmıyor (RGBA32 bırakıyor); her tablo 10–18 MB tutuyordu.
- Mipmap'i kapatmak sıkıştırmayı getirdi ama koleksiyon duvarındaki küçük tablolar kumlu göründü. Kalıcı çözüm: tablolar ikinin kuvvetine gerilerek, mipmap'li ve ASTC 6×6 sıkıştırmalı saklanıyor; tablonun gerçek oranı dokudan değil, Editor'ın resim dosyalarından yazdığı listeden okunuyor (`Resources/Paintings/sizes.json`, `PaintingSizesBuilder`, `PaintingSizes`). Yapboz kesimleri aynı kaldı.
- `PaintingFormatCheck`: her tablonun Android için sıkıştırılmış ve mipmap'li olduğunu komut satırından denetler. Son paket 46,8 MB.

**Doğrulama (benim yaptığım)**
- `dotnet test`: 2489 test geçiyor (824'ten). Unity içinde: proje kurulumu, motor denetimi, ilerleme dosyası denetimi, müze raporu ve oyun sahnesi hatasız.
- APK derlendi (46,8 MB). Telefon o sırada bağlı değildi; müze ekranları cihazda henüz denenmedi.

**Açık**
- 2–20. odaların tabloları: seçim ve indirme.
- Dış ekranda daha iri taş: daha dar tahtalar (bölümlerin yeniden üretimi) kararı.
- Yeni oyuncunun kazandığı ilk parça düz gökyüzü; küçük yazılar (ressam ve yıl, "ODA 1 / 20") dış ekranda yaklaşık 1 mm.
- Telefonda ölçülmedi: 40 parçalı otomatik tamamlama, geri tuşu, uzun oynama.
- Yazı tipleri hâlâ geçici (Roboto Bold).

## 2026-10-07 · Oturum 16 — Unity istemcisi: oynanabilir tahta telefonda

**Kullanıcı kararı**
- "Evet başla": tahta sahnesi yapılacak (ilk APK kapsamı: tahta, kazandın/kaybettin, sonraki bölüm, kayıt; harita, meta, mağaza sonraya).

**Nasıl yapıldı**
- Altı aşamalı, sıralı bir iş akışı (Unity projesini aynı anda tek işlem kullanabildiği için paralel değil): altyapı → galeri görünümü → oynanış → akış → bağımsız eleştiri → düzeltme. Yaklaşık 4 saat 20 dakika, 6 ajan, 2,7 milyon token. Ajanlar depoya yazmadı, APK derlemedi; sonuç gözden geçirilip tek seferde işlendi.
- Editor penceresi hiç açılmadı: sahne, ayarlar ve varlıklar Editor betikleriyle komut satırından üretiliyor (`ProjectSetup.Run`).

**Ne var**
- **Çizim hattı:** proje URP 2B'de. SRP Batcher kapalı: açıkken taşlar başka taşların resmiyle çiziliyordu (Editor'ın toplu çiziminde görüldü).
- **Görseller:** 41 taş çizimi tek atlasta (4096 px, Android'de ASTC). Duvar deseni, plaket, madalyon, raf ve aplik için SVG kaynakları `art/unity/` altında; `tools/unity/export-gallery-art.js` PNG'ye çeviriyor.
- **Görünüm:** camgöbeği damask duvar, aplikler, tele asılı BÖLÜM / HAMLE / HEDEF plaketleri, tahtanın çevresini ve iç deliklerini izleyen basamaklı ve boncuklu yaldız çerçeve, dokulu tepsi, alt raf. Web testindeki sayfayla yan yana çok yakın.
- **Yazı:** TextMesh Pro; tasarımın yazı tipleri indirilmediği için Unity ile gelen Roboto Bold kullanılıyor (geçici).
- **Düzen:** uzun telefon (1080×2340, 1080×1920) ve katlanır telefonun kare iç ekranı (2076×2152); güvenli alan payları hesaba katılıyor. Düzen hesabı Unity'siz saf kod (`ScreenLayout`), testli.
- **Oynanış:** sürükleyerek ya da iki taşa sırayla dokunarak takas; özel taşa dokunmak onu patlatır; geçersiz takas geri seker. Her hamle motorun olay günlüğünden zamanlı bir animasyona çevriliyor (`TurnChoreographer`, `Timeline`): patlama, düşme (tek düşme yasası, hafif esneme), roket, bomba, büyük bomba, renk bombası, kelebek, kutu/bant/jöle hasarı, malzemenin çıkışı, karıştırma. Düz bir hamle 0,5–0,7 saniye.
- **Güvence:** her hamleden sonra ekrandaki tahta motorun tahtasıyla karşılaştırılıyor. Botlarla 70'ten fazla bölüm baştan sona oynatıldı; hepsinde ekran motorla aynı bitti.
- **Akış:** bölüm bitince oda kararır, sonuç kartı tellerle iner ("Bölüm tamamlandı!", "Hamle bitti!"), yeşil düğme sonraki bölüme ya da aynı bölümün sıradaki tahtasına götürür. Deneme sayımı web sunucusunun kuralıyla aynı (k. deneme havuzun k. tohumunu kullanır).
- **Kayıt:** ilerleme telefonda bir JSON dosyasında; bozuk dosya kenara alınır, daha yeni bir sürümün dosyasına dokunulmaz.
- **Komut satırı araçları:** `RenderHarness` (istenen bölüm, ekran boyu ve andaki görüntü), `Storyboard` (botla oynatıp animasyon karelerini çıkarır), `FlowStory`, `PlayModeCheck`, `TouchWhileMovingCheck`, `ProgressFileCheck`.

**Bağımsız eleştirinin bulduğu ve düzeltilenler**
- **Engelleyici:** derin bir zincirlemede taşın "konma" animasyonu taşın kendisinden uzun yaşayabiliyor, hata verip tahtayı kalıcı donduruyordu (285. bölüm, expert bot, 5. hamle). Senaryo ve görüntü düzeyinde iki ayrı yerden kapatıldı; aynı sınıftan bir hata daha geniş taramada bulunup giderildi.
- Hata veren bir hamleden geri dönüş yoktu: artık tahta motorun durumuna döndürülüyor.
- Animasyon sürerken yapılan dokunuş atılıyordu: tahta durmadan en çok 0,8 saniye önce yapılan kaydırma bekletilip oynanıyor.
- Seçili taş işareti yeşil taşta görünmüyordu; iç çerçevenin köşe birleşimleri; rakamların fazla kalınlığı; bölüm geçişindeki takılma.

**Doğrulama (benim yaptığım)**
- `dotnet test`: 824 test geçiyor (639'dan; yeni testler düzen, çerçeve hattı, zaman çizelgesi, hamle senaryosu, dokunma, akış).
- Unity içinde: proje kurulumu güncel, motor denetimi geçiyor (1000 bölüm, 12 sabit oyun aynı), oyun sahnesi hatasız açılıyor.
- **APK:** 36 MB, 13,6 dakikada derlendi. **Telefonda (Pixel 9 Pro Fold, dış ekran 1080×2424) çalışıyor:** 1. bölüm doğru görünümle açıldı; komutla yapılan bir kaydırma hamleyi oynattı (HAMLE 24 → 23); cihaz günlüğünde oyun hatası yok.

**Açık**
- Gerçek parmakla uzun oynama, arka plana alma, iç (kare) ekran ve ucuz telefonlarda akıcılık henüz denenmedi.
- Yazı tipleri (tasarımdaki Gabarito, Paytone One, Young Serif) indirilmedi; Roboto Bold geçici.
- Ses, titreşim, ipucu, uzun zincirlemeyi atlama yok. Bir renk bombasının bombayla birleşmesi 5 saniyeyi buluyor.
- Harita ekranı, tasarım tuvalindeki zengin kazanma/kaybetme ekranları, booster rafı yok.
- Bölüm geçişinde duvar, plaketler ve çerçeve yeniden kuruluyor; telefonda süresi ölçülmedi.
- Disk: 5,9 GB boş.

## 2026-10-07 · Oturum 15 — Unity kurulumu ve proje iskeleti

**Kullanıcı kararları**
- "Unity bedava mı, bu bilgisayar kaldırır mı" soruları yanıtlandı: Unity Personal 200.000 dolar gelir ve yatırım sınırına kadar ücretsiz; i7-1255U, 16 GB bellek ve Iris Xe yeterli; dar nokta disk.
- Kurulumu kullanıcı başlattı (Unity Hub 3.22.2); gerisinin komut satırından yapılması ve adımların raporlanması istendi.

**Olanlar**
- Unity 6.3 LTS (6000.3.25f1) Hub'ın komut satırıyla indirildi, ama kurulum adımı Windows izin penceresi (UAC) onaylanmadığı için zaman aşımına uğradı. Hiçbir şey kurulmadı; indirme silindi.
- Bu sırada Hub'ın açılış sihirbazı **Unity 6.6 (6000.6.4f1)** ile Web modülünü kurdu. Editor kurulumu yarıda hata verdiği hâlde Hub onu kurulu saydı: `Data/Tools` ve paket yöneticisi sunucusu eksikti, eğitim projesi açılamadı.
- **Onarım:** Hub'ın indirdiği kurulum dosyası doğrulandı (boyut ve Unity Technologies imzası) ve yarım klasörün üstüne yeniden çalıştırıldı; kullanıcı UAC'yi onayladı. Kurulum hatasız bitti; Web modülü bu sırada kalktı.
- **Karar:** iki Editor'a disk yetmediği için kurulu olan Unity 6.6 ile devam ediliyor (LTS değil, "Supported" kolu).
- **Proje:** oyunun Unity projesi depoda `unity/` altında komut satırından oluşturuldu (`Unity.exe -batchmode -quit -createProject`; 77 saniye, hatasız). Lisans Unity Personal, etkin. Web testindeki 41 çizim `unity/Assets/Art/Pieces` altında, `.meta` dosyalarıyla.
- `.gitignore`: Unity'nin ürettiği klasörler (Library, Temp, Logs, UserSettings, Build) depoya girmiyor.
- **Motor Unity içinde doğrulandı.** `node tools/unity/sync-engine.js` motor derlemelerini (`Puzzle.Core`, `Puzzle.Bot`) ve 1000 bölümün tek dosyalık paketini (0,7 MB) projeye koyar; bunlar depodan türetildiği için `unity/Assets/Generated` depoya girmez. Unity içinde 1000 bölümün hepsi okunuyor (`LevelPack`, Unity'nin kendi JSON okuyucusuyla).
- **Çalışma ortamları arası eşitlik:** el yapımı 4 örnek bölüm üçer tohumla rastgele botla sonuna kadar oynatılıyor; Unity'nin içindeki sonuç araçlardaki .NET 10 sonucuyla satır satır aynı (kazanma durumu, hamle sayısı, her hamleden sonraki tahta özeti). Sabitlenen dosya `tests/fixtures/engine-trace.txt`; .NET tarafını `EngineTraceTests`, Unity tarafını `EngineSmokeTest` (komut satırından `-executeMethod`) denetliyor. Bölümler bir ortamda ayarlanıp başka bir ortamda oynanacağı için bu denetim kalıcı.

- **Android modülleri kuruldu** (Android Build Support, OpenJDK 17, Android SDK platform 34/36/37, NDK r27c). Hub'ın komut satırı her kurulumda yeni bir Windows izin penceresi açıyor ve kimse basmazsa iki dakikada iptal ediyor; o pencereye hiçbir program basamaz. Çözüm: kullanıcının ekran izniyle Hub'ın kendi penceresinden kurmak (Installs → Manage modules). Hub'ın daha önce onaylanmış yetkili yardımcısı açık olduğu için yeni izin sorulmadı. Varsayılan işaretli gelen Visual Studio Community seçilmedi; Google'ın Android SDK ve NDK lisans şartları kullanıcının verdiği yetkiyle kabul edildi.
- **İlk Android paketi derlendi:** `Unity.exe -batchmode -quit -projectPath unity -buildTarget Android -executeMethod Puzzle.Client.EditorTools.AndroidBuild.Run` → `unity/Build/Android/puzzle.apk`, 15 MB, 5,4 dakika (IL2CPP, ARM64, dikey, Unity'nin deneme imzasıyla). İçinde henüz oyun yok, boş bir sahne var; amaç derleme hattını uçtan uca görmekti. Geçici kimlik: `com.saturnwave.puzzle` (oyunun adı en sona kaldığı için).
- `AndroidToolchainCheck`: Unity'nin Android'i görüp görmediğini ve hangi SDK, NDK, JDK'yı kullanacağını komut satırından söyler.
- Bilgisayara USB ile bir telefon bağlı (Pixel 9 Pro Fold); USB hata ayıklama izni telefonda henüz onaylanmamış.

**Açık**
- Tahta sahnesi: bölümü seçilen tasarımdaki çerçeveli tahtayla göstermek, dokunarak takas, motor olaylarının animasyonu. Kullanıcının onayı bekleniyor.
- Unity 6.5 ve sonrasında yerleşik çizim hattı (Built-In Render Pipeline) kullanımdan kalkıyor; tahta sahnesine başlamadan proje URP 2B'ye alınmalı.
- Telefona kurulum: kullanıcı telefonda USB hata ayıklamayı onaylayınca `adb install` ile denenecek.
- Disk: 13 GB boş.

## 2026-10-06 · Oturum 14 — Çeşitlilik: tahta başına engel miktarı; APK işine başlangıç

**Kullanıcı kararları**
- "Çeşitlilik önerini onaylıyorum, uygula": tahta başına engel miktarı araması ilk aramada da kullanılacak.
- "Bir yandan da APK yapımına başlanmalı": Unity istemcisi ve Android APK işi, bölüm ayarıyla paralel başlıyor.

**Çeşitlilik**
- **Üretici:** her tahta kendi engel miktarını arar (en çok dört adım, planlanan miktarın üçte biri ile üç katı arası); adaydan adaya taşınan çarpan ve ayrı "ikinci arama" kaldırıldı. Miktar değişince aynı kalan tahta yeniden oynatılmaz (`Candidate.IsSameLevelAs`).
- **Sonuç (51–1000, 950 bölüm):** yalnızca renk toplama 528 → 400; kutulu 158 → 223; zincirli 185 → 272; jöleli 78 → 110; malzeme indirme 70 → 61. Paylar yerinde: 55. basamak %90 / %76 / %56 / %41. Ortalama limitler 25,2 / 29,3 / 31,9 / 36,8. Gevşetilen 9 bölüm ve hedeften sapan 899 aynı. 303 dosya değişti, 647'si bayt bayt aynı.
- **Malzeme bölümleri geri gelmedi.** 55. basamak botu bir malzemeyi 15–20 hamlede indiriyor; üç malzemeli tahta Normal aralığına oturmuyor. Malzeme sayısını ve jöle bandını miktara bağlamak 51–250 örneğinde denendi: 200 bölümde 4 malzeme bölümü kazandırdı, jölede fark yok; tek sıralık jöle bantları ve düşük miktarda kullanılamayan tahtalar getirdiği için geri alındı.
- **İlk oda** dosyaları eski aramayla üretilmiş hâliyle duruyor (kayıtları var); bugünkü kodla yeniden üretilirse değişir (GAME_DESIGN §9).
- **Yayın:** sunucu durduruldu (devam eden deneme yoktu; Admin 30. bölümde), Release derlendi, WMI ile başlatıldı; açık adres yanıt veriyor. `reports/ladder.csv` ve `reports/expert_moves.csv` yeni bölümlerde yenilendi: 55. basamak bağımsız ölçümde %90 / %76 / %55 / %39, novice %87 / %72 / %50 / %35, casual %98 / %91 / %77 / %65. `humans`: 66 kayıt, klasörde olmayan 0.
- Testler: 637 (ikinci aramanın testi, tahta başına aramanın testiyle değişti: 114. bölümün ilk tahtası planlanan kutularla uzun, daha az kutuyla oturuyor).

**APK işine başlangıç**
- Bu bilgisayarda Unity Hub ve Editor kurulu değil; Android SDK ve NDK var; C: sürücüsünde 17 GB boş. Kurulum ve hesap girişi kullanıcıda: Unity 6.3 LTS + Android Build Support (OpenJDK, SDK ve NDK araçları).
- Önerilen ilk APK kapsamı: bölüm haritası, seçilen tasarımdaki çerçeveli tahta, kazandın/kaybettin panelleri; 1000 bölüm pakette; meta, mağaza, booster ve hesap sonraya.
- Çizimler: web testindeki taş, özel taş, engel ve hedef simgeleri Unity için PNG olarak dışa aktarılıyor (`node tools/unity/export-sprites.js`; 41 görsel, 512 px, saydam zemin; kontrol sayfasıyla gözden geçirildi). PNG dosyaları Unity projesi kurulunca `.meta` dosyalarıyla birlikte depoya girecek. Duvar lambası ve çerçeve sayfanın stiliyle çizildiği için ayrı ele alınacak.
- Motor hazır: `Puzzle.Core` ve `Puzzle.Bot` netstandard2.1; animasyon için olay günlüğü (`GameEvent`); bölüm dosyası modeli Unity'nin JSON okuyucusuna uygun.

**Açık**
- Malzeme bölümleri: daha uzun hamle aralığı mı, insan verisini beklemek mi.
- Unity kurulumu (kullanıcı), ardından proje iskeleti, çizimlerin PNG'ye aktarımı, ilk APK.

## 2026-10-06 · Oturum 13 — Onaylanan paylar: 51–1000 ortalama oyuncuya göre yeniden üretildi

**Kullanıcı kararları**
- "Admin hesabı ortalama oyuncu profili gibi düşünülebilir, daha fazla veri gelecektir." Admin'in kayıtları merdivende en iyi 55. basamağa uyuyor (40–60 makul, 65 ve üstü bağdaşmıyor).
- Önerilen ilk deneme payları onaylandı: 51. bölümden itibaren ortalama oyuncu (55. basamak) Kolay %90, Normal %75, Zor %55, Süper Zor %40 kazanır.

**Yapılanlar**
- **Bant başına hamle payı:** `bands[].moveMargin` (yoksa `calibration.moveMargin`). İlk oda 0,25'te kaldı; 51 ve sonrasının bandı 0 yazıyor, çünkü onaylanan paylar limitin kendisinde tutulmalı.
- **Profil:** 51. bölümün bandı `calibrationBot: skill-55`, yeni hedefler, casual koruması yok; 301. bölümdeki sıkılaşma kaldırıldı. Hamle aralıkları, renkler, boyutlar, mekanik takvimi aynı.
- **Üretici, ikinci arama:** ilk üretimde 31 bölüm hamle aralığının dışında kaldı (969. bölüm: 4 kutu, 5 hamle). Neden: engel miktarı adaydan adaya tek çarpanla taşınıyor; malzeme ve jöle bandı tahtaları art arda uzun çıkınca çarpan çöküyor. Düzeltme: hiçbir adayı oturmayan bölümde aynı sekiz tahta tek tek, kendi miktarıyla yeniden denenir. Gevşetilen bölümün limiti de artık yeni tohumlarla denetlenip gerekirse oradan okunur. Öteki bölümler bayt bayt aynı kalır (iki üretim arasında 26 dosya değişti).
- **Yeniden üretim:** 1000 bölüm 41 dakikada; 1000/1000 geçerli; ilk oda 50/50 bayt bayt aynı. 55. basamak botu limitte Kolay %91, Normal %76, Zor %56, Süper Zor %41. Ortalama limitler 30/35/39/44'ten 25/29/32/36'ya indi; hedefler küçüldü. Gevşetilen 9 bölüm, hedeften 7 puandan fazla sapan 1 bölüm (ayrıntı: GAME_DESIGN §13).
- **Bağımsız ölçüm:** `puzzle ladder` yeni bölümlerde yeniden çalıştırıldı (10 dakika; `reports/ladder.csv`). 55. basamak üreticinin görmediği oyunlarda %90 / %76 / %55 / %40; 50. basamak (novice) %88 / %72 / %50 / %36; casual %98 / %91 / %77 / %65. `reports/expert_moves.csv` de yenilendi.
- **Yayın:** sunucu durduruldu (yarım kalmış deneme yoktu), Release derlendi, WMI ile yeniden başlatıldı. Açık adres yanıt veriyor; laboratuvarın bot listesinde artık merdiven botları da var. `humans`: 62 kayıt, klasörde olmayan 0.

**Bulgular**
- **Çeşitlilik düştü.** Zayıf botla engelli tahtalar aralığa daha zor oturuyor; yalnızca renk toplanan bölümler 335'ten 528'e çıktı, malzeme indirilen bölümler 223'ten 70'e, jöleli bölümler 187'den 78'e indi. Çare adayı (ilk aramada da tahta başına miktar) bütün bölümleri değiştirir; karar bekliyor.
- **Tahtalar değişti:** 950 bölümün 406'sı eski tahta stilini korudu. 51 ve sonrasını kimse oynamadığı için kayıt kaybı yok.
- **Yeni insan verisi:** Admin yumuşatılmış ilk odada 10–26. bölümleri oynadı: 17 ilk denemenin 16'sı kazanıldı (tek kayıp 23. bölüm), çoğu limitin yarısına yakın hamleyle. Bütün kayıtlarda insan %61,7, novice bot aynı oyunlarda %62,3.
- `generate --repair` 899. bölümü her seferinde yeniden üretiyor (limit doğrulama oyunlarından okunmuş, oran hedefin 7,5 puan üstünde; sonuç hep aynı dosya). Zararsız; bilinen bir pürüz.

**Açık**
- Çeşitlilik için ilk aramanın değiştirilmesi (yukarıda). 730 (65 hamle) ve 795 (54 hamle) elle gözden geçirilebilir.
- Daha çok oyuncu verisiyle ortalama oyuncunun basamağı yeniden kestirilecek.

## 2026-10-06 · Oturum 12 — Beceri merdiveni: ara botlar ve 1000 bölümün ölçümü

**Kullanıcı gözlemi ve kararı**
- Zorluk bir botun kazanma oranına bağlı, oyuncuların yüzdesine değil; bot çeşitliliği de az. Öneri: novice, casual ve average arasını yüzde 5'lik basamaklarla doldurmak ve **bölümleri yeniden üretmeden** mevcut 1000 bölümü bu botlarla ölçmek.
- Token harcaması ölçülü tutulacak: bir script yetiyorsa yalnızca o çalıştırılır.

**Yapılanlar**
- `skill-30` … `skill-100` basamakları (15 bot) eklendi; dört adlı profil merdivenin 50, 70, 85 ve 100. basamaklarıyla aynı ve değişmedi. Üreticinin profil yükleyicisi basamak adlarını kalibrasyon ve koruma botu olarak kabul ediyor; test panelinin bot listesi de basamakları sunuyor (sunucu bir sonraki derleme ve yeniden başlatmada gösterir).
- Yeni komut `puzzle ladder`: mevcut bölümleri merdivenle ölçer; her bot aynı oyunlarda oynar; çıktı parça parça alınabilir. 633 test (91'i yeni). Kodun kendi kontrolleri geçti; başlatılan bağımsız inceleme turu token tasarrufu için yarıda kesildi.
- 1000 bölüm × 15 bot × 60 oyun, Release derlemesiyle 15 dakikada ölçüldü: `reports/ladder.csv` (15.000 satır). Tablo GAME_DESIGN §7'nin sonunda.
- Sonuç: basamak başına Normal bölümlerde kabaca 6–9 puan kazanma oranı; novice (50) ile casual (70) arası %39 → %72. Zayıf oyuncu için Zor ve Süper Zor bölümleri yalnızca hamle ekleyerek düzeltmek mümkün değil (50. basamak için ortalama 72 hamle gerekirdi; bugün 39).
- İkinci oyuncu (Admin1) yeni bölümlerde 1, 2 ve 3. bölümleri kazandı.

**Sıradaki:** hangi basamağın "tipik oyuncu" sayılacağı ve kademe başına hedef yüzdeler (kullanıcı kararı); ona göre limitlerin, gerekiyorsa Zor kademelerde hedeflerin yeniden yazılması.

---

## 2026-10-05 · Oturum 11 — Kararlar: yüzde 25 yumuşatma, beyaz papatya, tema web testine, doğrudan dünya yayını

**Kullanıcı kararları**
- **Bütün bölümler yüzde 25 yumuşatılsın.** Uygulama tanımı: kalibrasyonun bulduğu hamle limitine yüzde 25 eklenir (yukarı yuvarlanır); tohum havuzu ve rapor yeni limitte ölçülür. Profilde tek bir sayı (`calibration.moveMargin`).
- **Papatya beyaz yapraklı olsun.**
- **Bölümler ve profiller PR'a gönderilsin.**
- **Yayın doğrudan dünya geneli** olacak; sınırlı ülke yayını adımı kaldırıldı.
- **Oyunun adı en sona** bırakıldı.
- **Sunucu güncellenen temayla yeniden başlatılabilir:** seçilen tasarım web testindeki oyuncu sayfasına uygulanacak; sunucuyu yeniden başlatma yetkisi verildi.
- Özgün "Level 47 board" çizimindeki taşlar da değiştirildi (Oturum 9).

**Yapılanlar**
- Sunucu yeniden başlatıldı (yerel saatle 20:10): konsoluna Ctrl+C gönderilerek düzgün durduruldu, kendi penceresinde yeniden açıldı. Günlük: `Public mode: 1000 levels`. Oyuncular artık ilk odanın kolaylaştırılmış bölümlerini görüyor; Admin yeni 10. bölümden (39 yeşil, 21 hamle) devam ediyor. Eski 10. bölümde toplam 23 deneme, hepsi kayıp.
- İlk oda değişiklikleri commit edilip PR'a gönderildi (`bd84183`).
- `tools/public/Stop-PublicServer.ps1` eklendi: sunucunun konsoluna Ctrl+C gönderir, zorla kapatmaz. Aynı yöntem bu yeniden başlatmada elle kullanıldı; betiğin kendisi bir sonraki yeniden başlatmada denenecek.
- Başlatılan işler (arka planda): (1) üreticiye bant başına kalibrasyon botu ve hamle payı, iki geçici profilin `default.json` içine katlanması; (2) beyaz yapraklı papatya ve motorun 7. ve 8. renkleri için iki yeni taş; (3) seçilen tasarımın oyuncu sayfasına uygulanması (canlı sunucuya dokunmadan, ayrı bir kopyada); (4) harita, koleksiyon, mağaza ve ayarlar çizimleri.

- Papatya beyaz yapraklı olarak yeniden çizildi (yapraklar beyaz, göbek altın sarısı, kenar çizgisi sıcak koyu sarı; açık tepside ve krem kartta okunuyor) ve onu gösteren beş çizim yeniden üretildi (kullanıcının "Level 47 board" çizimi dahil). Motorun 7. ve 8. renkleri için iki taş çizildi: camgöbeği elmas ve pembe lale; taş föyüne "sonraki bölümlerde" başlığıyla eklendi. Koruma bandı inceltildi (beyaz papatyanın üstünde göbek görünsün diye).
- Dört ekran daha çizilip tuvale eklendi: müze haritası (kat planı; 1. oda açık, 2. oda kilitli, sonrakiler yalnızca numarayla), koleksiyon (onarılan dört tablo salon duvarında, beşincisi yapboz hâlinde), mağaza (üç altın paketi, iki güçlendirici seti, ödüllü reklam; fiyatlar `[fiyat]`) ve ayarlar. Tuvalde artık on üç çizim var.

**Kesinti (5 Ekim gecesi)**
- 20:10'da yeniden başlatılan sunucu, onu başlatan oturum kullanım sınırına takılıp kapandığında oturumla birlikte öldürüldü (günlükte kapanış satırı yok). Yayın adresi, fark edilip yeniden başlatılana kadar (6 Ekim 00:25) 502 verdi. Kayıt ve hesap kaybı yok: 41 kayıt yerinde.
- Neden: `Start-Process` ile açılan pencere oturumun alt sürecidir. Çözüm: sunucu artık WMI üzerinden (`Win32_Process.Create`) başlatılıyor; oturumdan bağımsız yaşıyor. Her oturum başında 5199 portu ve yayın adresi kontrol ediliyor.
- Aynı kesinti iki arka plan işini de yarıda bıraktı (üretici değişikliğinin doğruluk incelemesi; temanın oyuncu sayfasına uygulanması). İkisi kaldıkları yerden sürdürüldü; tema işi kesildiğinde ekranlar büyük ölçüde hazırdı.

**6 Ekim: yüzde 25 hamle payı yayında**
- **Üretici:** her bant kendi kalibrasyon botunu adlandırıyor (`bands[].calibrationBot`); iki geçici profil `default.json` içine katlandı ve silindi. Yeni alan `calibration.moveMargin` (0,25): bölümün limiti kalibre limitin yüzde 25 fazlası (yukarı yuvarlanır); havuz ve rapor yeni limitte ölçülür; rapora `calibratedMoves`, `moveMargin`, `calibrationBot` sütunları eklendi. Profil yükleyici artık bilinmeyen alanı reddediyor. İki bağımsız inceleme (doğruluk; sözleşme ve testler) 13 bulgu verdi, 9'u düzeltildi; testler 461'den 526'ya çıktı.
- **Kabul:** pay 0 iken 1–60. bölümler eskisiyle bayt bayt aynı; pay 0,25 iken iki çalıştırma aynı dosyaları veriyor; `--repair` aynı profille hiçbir şey üretmiyor.
- **Yeniden üretim:** 1000 bölüm 50'şerlik aralıklarla yaklaşık 50 dakikada üretildi. Tahtalar, hedefler ve kalibre limitler 1000'de 1000 aynı kaldı; yalnızca limit ve havuzlar değişti. Gevşetilen yok, havuzlar en az 8 tohum. Sonuç tabloları GAME_DESIGN §13'te.
- **Ölçüm:** casual bot 51–300'de Normal %51 → %78, Zor %20 → %46. Novice bot ise 51–100'de ortalama %39 (Zor %16, Süper Zor %5): **pay, novice gibi oynayan biri için 51 sonrasındaki duvarı kaldırmıyor.** İlk oda kendi botu için neredeyse kaybedilmez oldu (%98–100).
- **Canlıya alma:** bölümler yerine kondu, 526 test geçti, sunucu `Stop-PublicServer.ps1` ile düzgün durduruldu (betik ilk kez denendi, çalışıyor) ve oturumdan bağımsız yeniden başlatıldı. Takastan hemen önce Admin1 hesabı ilk kez oynadı (1. bölümün novice sürümü, ilk denemede 14/19 hamlede kazandı); o sürüm ve komşuları `levels/archive/before-margin-2026-10-06/` altına alındı, 42 kaydın hepsi karşılaştırılabiliyor.

**6 Ekim: tema oyuncu sayfasında, yayında**
- Seçilen tasarım herkese açık testin oyuncu sayfasına uygulandı: giriş kartı, tele asılı bölüm / hamle / hedef plaketleri, bölümün şekline göre üretilen basamaklı yaldız çerçeve ve tepsi (her boyutta ve delikli tahtalarda), tasarımın taşları ve engelleri (uğur böceği, beyaz papatya, sandıklar, vernik, bant, özel taşlar; 7. ve 8. renk dahil), kazanma ve kaybetme panelleri. Oynanış mantığı değişmedi; yalnızca çalışan şeyler gösteriliyor (booster, coin, can, yapboz parçası yok).
- Çalışma canlı sunucuya dokunmadan ayrı bir kopyada yapıldı: haritalama, yapım, iki bağımsız inceleme (görünüm 8/10, 14 bulgu; davranış 8/10, 11 bulgu), düzeltme ve baştan sona doğrulama (iki hesapla tam yolculuk iki kez; 542 test).
- **Canlıya alma:** yeni dosyalar eski sayfanın yanına kopyalandı, `index.html` tek bir taşımayla değiştirildi; sunucu yeniden derlenmedi ve durdurulmadı. Yayın adresindeki giriş sayfası yeni görünümde.
- **Yazı tipleri:** sunucunun bugünkü güvenlik politikası Google Fonts'u engelliyor; sayfa şimdilik sistemdeki kalın yazı tipleriyle çalışıyor. Politika değişikliği koda eklendi ama sunucu yeniden derlenene kadar etkisiz. İki yol var: sunucuyu yeniden derleyip başlatmak (oyuncuların tarayıcıları her ziyarette Google'a bağlanır) ya da üç yazı tipini sunucuya koymak (dosyaların indirilmesi gerekir). Karar kullanıcıda.
- **Sınırlar:** yalnızca bu bilgisayardaki başsız Chrome'da denendi; gerçek telefon, Safari ve ekran okuyucu denenmedi. Taş çizimlerinin kaynakları hâlâ `out/design` altında (git'e girmiyor); temiz bir kopyada `pieces.svg` yeniden üretilemez.

- **Yazı tipi kararı (kullanıcı, 6 Ekim):** "yazı tipi Design'daki gibi olsun". Sunucu durduruldu, Release olarak yeniden derlendi (0 uyarı) ve yeniden başlatıldı; yeni güvenlik politikası Google Fonts'un stil dosyasına ve yazı tipi dosyalarına izin veriyor. Yayın adresindeki giriş sayfası artık Gabarito, Paytone One ve Young Serif ile açılıyor (ekran görüntüsüyle doğrulandı). Bedeli: her oyuncunun tarayıcısı her ziyarette Google'a bağlanıyor. Yazı tiplerini sunucuya koymak (dosyaları indirerek) hâlâ açık bir seçenek.

**Sıradaki:** 51 sonrası için novice kalibrasyonu kararı (insan verisi geldikçe); çizim kaynaklarının depoya alınması; gerçek telefonda deneme.

---

## 2026-10-04 · Oturum 10 — İlk insan verisi: ilk bölümler zor

**Geri bildirim**
- Herkese açık test yayında (Tailscale Funnel, 8443). İlk oyuncu (Admin hesabı, 12 deneme, 1–7. bölümler) ilk bölümlerin zor olduğunu söyledi. Sonuç: 12 denemede 6 galibiyet; 1. bölümde 3'te 1, 7. bölümde 4'te 0.
- Yayın kontrol edildi: adres giriş ekranını gösteriyor; test paneli ve kayıt uçları anahtarsız isteği reddediyor.

**Bulgular** (ayrıntı `out/onboarding/` altında; depoya girmiyor)
- **Limitleri yanlış oyuncu modeli belirliyor.** Limitler average bota göre ayarlı; oyuncu novice botun hızında ya da biraz altında (hamle başına 6,57 hedef taşı; novice 7,34; average 10,97). Bugünkü bölümlerde novice bot 1–5'te %79, 6–10'da %23, 9. bölümde %5, 10. bölümde %2 kazanıyor.
- **Zorluk döngüsü 1. bölümden başlıyor:** 9. bölüm Zor, 10. bölüm Süper Zor.
- **Oyuncu özel taşı ihmal etmiyor.** Kendi eşleşmeleriyle botlardan iki kat özel taş kuruyor ve hamlelerinin %78'inde hedef rengi eşliyor. Geride kalma nedeni: tahtadaki zincirleme düşüşlerden neredeyse hiç taş almıyor (hamle başına 0,46; novice 1,55) ve özel taşları tek tek patlatıyor.
- **Tohum şansı zayıf oyuncuya dengesiz:** bugünkü 2. bölümün ilk tohumunda novice %45 kazanıyor (havuz içi fark %45–88).
- **Sektör:** başarılı oyunlarda ilk ~20 bölüm neredeyse kaybedilmiyor; ilk "Zor" bölüm 24–54, ilk "Süper Zor" 28–65 arasında (topluluk verisi; hiçbir stüdyo oran yayımlamadı).
- Veri sınırı: tek kişi, 12 deneme. Yönü destekliyor, hiçbir sayının büyüklüğünü desteklemiyor.

**Öneri (kullanıcı onayı bekliyor; şartname `out/onboarding/spec.md`)**
- Birinci odada (1–50) limitleri zayıf oyuncu modeli belirlesin: 1–3 random bot %90, 4–10 novice %95, 11–20 novice %90, 20'ye kadar hepsi Kolay. 21'den sonra döngü yumuşak hedeflerle döner (ilk Zor 25, ilk Süper Zor 30). 51 ve sonrası aynen kalır.
- Aşama 0: kod değişikliği yok, iki geçici profil dosyası ve bir sunucu yeniden başlatması. Aşama 1: `bands[].calibrationBot` alanı; iki dosya `default.json` içine katlanır.
- İpucu ve öğretici bu adımda yok: bölümlerle birlikte değişirse hangisinin işe yaradığı anlaşılamaz.

**Prova** (canlı bölümlere dokunmadan, `out/onboarding/rehearsal`; bölüm ve bot başına 600 oyun)

| Bölüm | Ort. limit | random | novice | casual | average |
|---|---|---|---|---|---|
| 1–3 | 16,7 | %90 | %100 | %100 | %100 |
| 4–10 | 19,6 | %49 | %95 | %100 | %100 |
| 11–20 | 17,6 | %29 | %92 | %99 | %100 |
| 21–30 | 23,8 | %23 | %77 | %95 | %100 |
| 31–40 | 22,9 | %11 | %60 | %84 | %96 |
| 41–50 | 22,9 | %9 | %57 | %82 | %96 |

- Şartnamedeki 21 kabul ölçütünün tamamı tuttu; gevşetilen bölüm yok; 51–55 yeni 46–50'nin arkasında bayt bayt aynı üretildi.
- Novice botun ilk 50 bölüm için beklenen deneme sayısı yaklaşık 418'den 72'ye iniyor.

**Süreç notu**
- İş akışındaki ayrı çalışma kopyaları dalın ucundan değil `main`'den (Faz 1) açıldı; ölçüm ajanları dalın ucunu kopyaya çıkarıp öyle çalıştı. Sonuçlar ana kopyadaki derlenmiş CLI ile yapılan provayla doğrulandı.

**2026-10-05: yeni veri, onay ve takas**
- Aynı oyuncu (kullanıcının deyişiyle usta bir oyuncu) devam etti: toplam 37 deneme. 7. bölümü 7. denemede, 8'i 2. denemede, 9'u (Zor) ilk denemede geçti; **10. bölümü (Süper Zor, 144 yeşil / 31 hamle) 19 kez üst üste kaybetti** (en iyisi 126/144). Geri bildirim: "çok zor, özellikle ilk bölümler".
- Karşılaştırılabilir 26 denemede insan %35, novice bot %34, casual %58, average %80, expert %82. **Deneyimli bir insan novice botla aynı oranda kazanıyor;** bot adları insan becerisini göstermiyor.
- Kullanıcı planı onayladı ("plan yapıp başlanılabilir"). İlk oda diskte takas edildi:
  - Eski 50 dosya `levels/archive/before-onboarding-2026-10-05/` altına alındı; `data/` ve `recordings/` yedeklendi (`out/backups/`).
  - `profiles/onboarding-1-3.json` ve `profiles/onboarding-4-50.json` eklendi; 1–50 yerinde yeniden üretildi. 50 dosyanın 50'si provayla bayt bayt aynı; 1000/1000 geçerli; 461 test geçiyor.
  - Takastan sonra `puzzle humans` 37 kaydın hepsini okuyor (arşiv sayesinde).
- **Sunucu yeniden başlatılana kadar oyuncular eski bölümleri görür** (sunucu bölümleri açılışta okur). Yeniden başlatma kullanıcının adımı.
- Belgeler: GAME_DESIGN §8 madde 7, §9, §13, §18, §20 ve yeni §22 (görsel tasarım); PUBLIC_TEST §7 (test sürerken bölüm değiştirme).

**Açık kalanlar**
- 51 ve sonrası average bota göre ayarlı kaldı (novice 51–60'ta %19): duvar oraya taşındı. İnsan verisi average botun insanüstü olduğunu gösterdiği için 51+ kalibrasyonu yeniden ele alınmalı.
- Kalıcı çözüm (profilde bant başına kalibrasyon botu; iki geçici dosyanın `default.json` içine katlanması) kullanıcı onayını bekliyor.
- `puzzle humans`, havuzun ötesindeki tekrar denemelerini "hand-picked seeds" diye sayıyor; etiket yanıltıcı.

---

## 2026-10-04 · Oturum 9 — Claude Design ile ilk ekran denemeleri

**Kullanıcı kararı**
- Görseller en sonda yapılacak; ama Claude Design ile bir çizim denemesi şimdi yapıldı ("deneyebiliriz").

**Değişiklikler**
- Tasarım tuvali: "Ressam ve Müze — oyun ekranları" (özel bağlantı, depoda değil). Dört çizim: oyun ekranı, müze odası, yapboz ekranı, taş ve engel seti.
- Kullanılan tablolar kamu malı, Art Institute of Chicago açık erişim görselleri: Van Gogh *Yatak Odası*, Seurat *Grande Jatte*, Monet *Nilüferler*, Caillebotte *Paris Sokağı; Yağmurlu Gün*.
- Yapboz çizimi, önceki denemedeki 3-4-3 kesim algoritmasının ürettiği gerçek parça yollarını kullanıyor.

**Denemedeki varsayımlar (karar değil, öneri)**
- Renk düzeni: gece mavisi zemin, pirinç çerçeve vurgusu. Yazı tipleri Fredoka (başlık) ve Figtree (metin).
- Tema adları: roket → fırça darbesi, bomba → boya sıçraması, pervane → tablodan uçan kuş, renk bombası → gökkuşağı paleti, kutu → nakliye sandığı, jöle → eski vernik, zincir → koruma bandı, indirilecek eşya → çerçeve köşesi. Motordaki adlar değişmez.
- Her boya renginin kendi şekli var (renk körlüğünde de ayırt edilsin diye); test arayüzündeki kuralla aynı.
- Oda adı, coin ve can sayıları, booster adları yer tutucudur.

**Kullanıcı tepkisi ve ikinci tur**
- İlk çizimler reddedildi ("oyun ekranları çok kötü"): düz geometrik şekiller, kutulu hücreler, tel kafes gibi bir HUD. Kullanıcı kalite çıtası olarak ticari bir match-3 oyununun ekran görüntüsünü verdi.
- Referanstan yalnızca işçilik düzeyi alındı (hacimli, parlak taşlar; çerçeveli tahta; sahne arka planı; kabartmalı paneller). Karakteri, taş motifleri ve panel düzeni kopyalanmadı; başka bir şirketin tasarımı.
- Oyun ekranı üç ayrı sanat yönünde yeniden çizildi; her yön çizildi, ayrı bir gözle eleştirildi (ilk puanlar 6/10; 16–19 bulgu), düzeltildi. Bu kez her adımda görüntüye bakıldı.
  - **A · Atölye nesneleri:** damla, kavanoz, ayçiçeği, tüp, bere, kalem; yaldızlı çerçeve, koyu yeşil galeri duvarı.
  - **B · Boya damlaları:** emaye boya topakları; ahşap çerçeve, gün ışıklı atölye.
  - **C · Pigment kristalleri:** fasetli kristaller; gece mavisi müze, altın çerçeve.
- Çizimlerin kaynağı (SVG taşlar, üreteç betikleri, görüntüler) `out/design/art/` altında; depoya girmiyor.
- **Bilinen eksik:** çizimlerdeki tahta 9×9; oyunun tahtası 10×10. Seçilen yön 10 sütunla yeniden kurulacak (hücre yaklaşık 36 px).

**Yön seçimi (2026-10-05)**
- Kullanıcı başka bir oturumda ayrı bir tasarım tuvali çizdirdi ("Design": ana ekran ve 47. bölüm tahtası) ve onu bu oturumdaki üç yönün hepsinden daha iyi buldu. **Sanat yönü artık o tuval;** A, B ve C rafa kalktı (silinmedi).
- O tasarımın farkı: her renk için ayrı ayrıntılı bir nesne yerine, tek bir ışık tarifini paylaşan sade ve iri şekiller (girdaplı disk, damla, yuvarlak kare, çiçek, koni, hilal); üst üste dizilmiş paneller yerine kurulmuş bir sahne (tele asılı plaketler, şekilli tahta, ahşap booster rafı); az renkli arayüz (camgöbeği duvar, yaldız, krem); onarılan tablonun çevresinde kurulmuş bir ana ekran.
- Yerel kopyası `out/design/chosen/` altında (iki çizim, tablolar, görüntüler).
- **Oyuna göre eksikleri:** sandık, koruma bandı, indirilecek eşya ve renk bombası çizimi yok; tahta 9 sütun (oyunda 10); metinler İngilizce; yapboz yerleştirme ve bölüm sonu ekranları yok.

**Seçilen tasarım üzerinde çalışma (2026-10-05, kullanıcı: "beğendiğim dizayn üzerinden çalışmalara başlanılabilir")**
- Kullanıcı isteği: bazı taşların yerine papatya ve uğur böceği. Eşleme: uğur böceği kırmızı diskin, papatya sarı çiçeğin yerine; diğer dört taş aynı.
- Yeni taşlar seçilen tasarımdaki taş tarifiyle çizildi (tek sade siluet, alt kenarda kalınlık, sol üstten ışık, iki kenar parıltısı, tek beyaz yansıma). İki ressam ayrı ayrı çizdi; ayrı bir göz ikisinde de yukarıdan görünen, simetrik olanı seçti (36 pikselde daha sakin ve okunur), 21 bulgu düzeltildi.
- Eksik parçalar aynı dille çizildi: sandık (çatlak / sağlam / demirli), kalın vernik, koruma bandı (tek / çift), çerçeve köşesi, gökkuşağı paleti, büyük sıçrama; hedef simgeleri ve "+5 hamle" simgesi.
- Tuvale iki yeni çizim eklendi; kullanıcının iki çizimine dokunulmadı: oyunun gerçek genişliğinde (10 sütun, 36 px hücre) Türkçe tahta ve "Taşlar ve engeller" föyü. Çerçeve, tahtanın şekline göre aynı katmanlarla yeniden üretiliyor.
- Kaynak: `out/design/next/` (taş parçaları `sym/final`, üreteç `tools/build.js`, biçim denetimi `tools/check.js`).
- Aynı gün beş ekran daha çizilip tuvale eklendi (her biri çizildi, ayrı bir gözle eleştirildi, düzeltildi): Türkçe ana ekran, bölüm başı (hedefler, hamle, üç bölüm öncesi booster, "Oyna"), yapboz yerleştirme (tepsideki parça yerinin gerçek kesimiyle), kazanma (yeni parça, coin, kalan hamlelerin fırçaya dönüşmesi) ve kaybetme ("+5 hamle": coin ya da reklam, "Vazgeç · 1 can gider"). Ekranlar birbirine bağlı; tuvalde oynatılınca aralarında geçilebiliyor. Plan ve yer tutucular GAME_DESIGN §22'de.
- **Disk doldu:** C: sürücüsünde yaklaşık 240 MB boş yer kalmıştı. Bu oturumun bıraktığı tarayıcı profil klasörleri silindi (yaklaşık 170 MB geri geldi); asıl yer açma işi kullanıcıda. Yer açılana kadar yeni büyük iş başlatılmıyor.

- Kullanıcı yeni taşları tuvalde bulamadı (yedi yeni çizimin yalnızca ikisinde taş var; kendi iki çizimine dokunulmamıştı). İsteği üzerine özgün "Level 47 board" çiziminde de kırmızı disk uğur böceğiyle (12 taş), sarı çiçek papatyayla (14 taş) yerinde değiştirildi; çizimin başka hiçbir yeri değişmedi. Özgün hâli `out/design/chosen/Board.dc.html` olarak duruyor; değişikliği `out/design/next/tools/swap-original.js` yapıyor.
- Çizimler artık tuvalin kendi çizim motoruyla da yerelde denenebiliyor (tuvalin `dc-runtime.js` dosyası `support.js` olarak çizimin yanına konup başsız Chrome ile açılıyor); uygulama içi tarayıcı tuvale giriş yapamadığı için doğrulama böyle yapıldı.
- Sıradaki ekranlar çiziliyor: müze haritası, koleksiyon, mağaza, ayarlar.

**Sıradaki:** harita, koleksiyon, mağaza ve ayarlar ekranları; papatyanın rengi; görünümün web testindeki oyuncu sayfasına uygulanıp uygulanmayacağı kararı. Yol haritasındaki sıra değişmedi (4. adım, sonra tema/meta tasarımı).

---

## 2026-10-04 · Oturum 8 — Herkese açık test modu ve 1000 bölümün yeniden üretimi

**Kullanıcı kararları**
- Web test uygulaması internete açılacak; tanıdıklar oynayacak ve gerçek oyuncu verisi APK'dan önce toplanacak. Bu iş 4. adımın önüne alındı.
- Giriş kullanıcı adı ve şifreyle; kayıt ekranı yok. Üç hazır test hesabı, basit ortak şifreyle ("test ortamı, zorlaştırmaya gerek yok").
- Bölümler gerçek oyundaki gibi sırayla oynanır.
- Yayın Tailscale Funnel ile (bilgisayarda zaten kurulu); Cloudflare Tunnel yerine.

**Bulgular**
- **Test paneli asla tünelden açılmamalı:** varsayılan mod test modu olduğu için "herkese açık" ayarını unutmak çözücüyü, botları ve herkesin kayıtlarını şifresiz açardı. Test modu artık bir vekil sunucudan geçen her isteği reddediyor.
- **Tailscale, istemcinin yazdığı `CF-Connecting-IP` başlığını olduğu gibi geçiriyor;** sunucu bu başlığa güvenseydi her istek kendine yeni bir "adres" seçip deneme sınırını aşabilirdi. Artık yalnızca bu makinedeki vekilin yazdığı adrese güveniliyor.
- **HSTS makinenin bütün adını bağlar:** bu bilgisayar aynı ad altında başka servisler de yayınlıyor; Tailscale üzerinden gelen isteklere HSTS gönderilmiyor.
- **Yeniden üretim tahmini doğruladı:** Normal'de average %89 → %81 → %73 (tahmin 88 → 81 → 75); casual koruması 513 bölümde limiti belirledi.
- **Uzun işler arka planda 10 dakikada kesiliyor:** üretim ayrı bir pencerede tamamlandı; yarıda kesilen ilk çalıştırmanın rapor satırları onarım komutuyla geri üretildi, bölüm dosyaları değişmedi.
- **İncelemeler:** herkese açık modda 13 + 3 + 3 bulgu doğrulandı ve kapatıldı (güvenlik ve doğruluk); her tur ayrı bir ajanla doğrulandı.

**Değişiklikler**
- **Web:** `Puzzle:Mode` = Lab / Public. Oyuncu sayfası (giriş, oyun, sonuç, sonraki bölüm), oyuncu API'si, çerez oturumu, PBKDF2 şifre karması, kilitleme ve deneme sınırları, HTTPS yönlendirmesi, önbellek yasağı. Oyuncunun yarım kalan denemesi diske yazılıyor ve yeniden başlatmada hamleler tekrar oynatılarak kuruluyor. Test paneli `/lab` altında, yönetici anahtarıyla.
- **Hesaplar:** `data/players.json` (atomik yazma); hazır test hesapları `src/Puzzle.Web/public-test-players.json`; CLI `players list | add | password | delete`.
- **Betik:** `tools/public/Start-PublicServer.ps1` (modu ayarlar, yönetici anahtarını üretir, sunucuyu başlatır).
- **Bölümler:** `levels/generated` yeni kurallarla yeniden üretildi; `reports/expert_moves.csv` yenilendi.
- **Testler:** 242'den 461'e.
- **Belgeler:** yeni [PUBLIC_TEST.md](PUBLIC_TEST.md); GAME_DESIGN §13, §20, §21.

**Sıradaki:** sunucunun yayına açılması (kullanıcının adımı), ilk insan verisi; ardından 4. adım (booster'lar, bitiş şöleni, düzeltme paketleri).

---

## 2026-10-03 · Oturum 7 — Kalibrasyon kuralları, deneme kaydı ve yeni test arayüzü (3. ve 5. adım)

**Kullanıcı kararları**
- **Casual koruması:** sabit 80/60 yerine bölüm aralığına göre inen alt sınır: Kolay 80/75/70, Normal 60/50/40 (1–50 / 51–300 / 301–1000).
- **Özel taş tanıtımları** (roket 3, bomba 5, pervane 8, renk bombası 12, büyük bomba 15) bir öğretici takvimi; motor kuralı değil. Arayüz fazında yapılacak.
- **Şekilli tahtalar** 21. bölümde başlar; 11. bölüm yalnızca kutuyu öğretir.
- **5. adım paralel yürütüldü** (kullanıcının önerisi): ayrı bir git çalışma kopyasında, 3. adımla aynı anda.
- **Web test uygulaması** oyuncuya sunulmayacak (oyun yalnızca iOS ve Android), ama özenli bir arayüzle geliştirilecek: "test ortamı" diye stilsiz bırakılmayacak.

**Bulgular**
- **Sabit casual eşiği koruma değil, kalibrasyonun kendisi oluyordu:** 80/60, 51. bölümden sonra Kolay ve Normal bölümlerin %95'inden fazlasında limiti belirliyor, Normal'de average'ı her aralıkta %89'a sabitliyordu. Fark bölümlerden değil botlardan geliyor: casual, average'dan 19–32 puan az kazanıyor ve hiçbir bölüm tasarımı bunu kapatmıyor.
- **"+5 hamle" teklifi** mevcut 1000 bölümde average'ın kazanma oranını %65,7'den %80,3'e çıkarıyor.
- **Koruma çoğu zaman "daha kolay hedef" olarak görünüyor:** alt sınır limiti hamle aralığının dışına itince üretici hamle eklemek yerine hedefi küçültüyor.
- **Botları insanın oynadığı tahtada oynatmak şart:** ilk sürümde botlar rastgele tahtalarda oynuyordu; aynı tohumda novice %100 kazanırken rastgele tohumlarda %93 görünüyordu.
- **İncelemeler:** 3. adımda 4 + 5, 5. adımda 9 + 11 bulgu doğrulandı ve düzeltildi; her düzeltmeyi ayrı bir ajan kontrol etti.

**Değişiklikler**
- **Profil:** mekanik takvimi tablo başlangıçlarına hizalı (kutu 11, eşya indirme 51); `casualFloor`, `floorBot` (kalibrasyon botundan zayıf bir oyuncu modeli), `sizes.plainShapesUntil` = 20; Kolay ve Normal hamle üst sınırı 51. bölümden sonra +3.
- **Üretici:** limit = `max(average'ın hedef limiti, casual'ın alt sınır limiti)`; doğrulama iki botu da yeni tohumlarla denetliyor. Rapor sütunları: `winPlus5`, `floor`, `floorWin`, `floorSetLimit`, `floorBot`. `generate --repair` alt sınırı, koruma botu ya da hedefi değişen bölümleri yeniden üretiyor.
- **Core:** `ExtraMovesOffer.Moves` (= 5); `Puzzle.Core/Recording`: deneme kaydı tipi (`AttemptRecord`), mobil oyunla ortak biçim. `LevelFileMapper.ParseGoal` ve `ToGoalFile` public oldu.
- **Bot:** `LevelReport.WinRateWithExtraMoves`.
- **Tooling:** kayıt JSON'u (`AttemptRecordJson`), eşzamanlı yazmaya dayanıklı kayıt dosyası (`AttemptLog`), insan–bot karşılaştırması (`HumanBotComparison`), bölüm parmak izi.
- **CLI:** `botstats` çıktısına `winAtLimitPlus5`; yeni `humans` komutu.
- **Web:** her deneme kaydediliyor (bitince, yarıda bırakılınca, zaman aşımında, kapanışta); deneme numarasını sunucu veriyor. Yeni "Kayıtlar" paneli ve botlarla karşılaştırma grafiği, kayıt göstergesi. Görsel yenileme: her rengin kendi şekli olan taş seti, özel taşlar ve engeller için çizimler, daha akıcı animasyonlar, sekmeli paneller, telefon düzeni.
- **Testler:** 143'ten 242'ye.

**Sıradaki:** herkese açık oyun modu (tanıdıklara oynatıp insan verisi toplamak) kullanıcıyla konuşuluyor; ardından 4. adım (booster'lar, bitiş şöleni, düzeltme paketleri) ve 6. adım (1000 bölümün yeniden üretimi).

---

## 2026-10-03 · Oturum 6 — Özel taş düzeltmesi ve usta bot (yol haritası 2. adım)

**Kullanıcı kararları**
- Kod `beautiful-code` standardına göre yazılıyor; tasarım belgeleri profesyonel PDF olarak da tutuluyor.
- Motora yeni public metot onaylandı: `Game.CreateImagined(ulong seed)`.
- Usta botu örneklemesiz iki hamle aramaya çevirme önerisi onaylandı; ölçüm önerinin yanlış olduğunu gösterince geri alındı (aşağıda).

**Nasıl çalışıldı:** dört iş akışı, toplam 22 ajan.
1. Okuma, teşhis ve tasarım: üç okuyucu, bir ölçüm ajanı, üç bağımsız tasarım, iki değerlendirici.
2. Uygulama ve ölçüm: motor, bot, ölçüm.
3. Örneklemesiz iki hamle denemesi ve ölçümü.
4. Bağımsız inceleme (dört boyut, her bulguyu çürütmeye çalışan doğrulayıcılar) ve düzeltmeler.

**Bulgular**
- **Biriktirme gerçek ama dar:** 0,5 cezasıyla expert özel taşlarının %19'unu kullanmadan bitiriyordu; zarar büyük "renk topla" hedefli bölümlerdeydi. 0,25 iki profil için de en iyi değer (1000 bölüm × 100 oyun): expert limitle kazanma %84,8 → %86,8, average %64,2 → %65,3. 0 değeri eşya indirme bölümlerinde özel taşları erken harcatıyor.
- **Ağaç araması bu bütçede değmiyor:** karar başına ~550 deneme oyunu var; üç tasarımdan değerlendiriciler örneklemeli iki hamle aramayı seçti.
- **Usta, expert'ten çok güçlü:** 1000 bölüm × 30 oyun, aynı tohumlar: limitle kazanma %86,8 → %98,6, ortanca hamle 20,4 → 15,8. Her kademede ve her hedef türünde önde; en büyük kazanç eşya indirme bölümlerinde. Rapor 13,4 dakika.
- **Örneklemesiz iki hamle daha zayıf çıktı:** 40 bölümlük örneklemde biraz daha iyi görünmüştü; 1000 bölümde limitle kazanmada 1,0 puan geride, ortancada 0,9 hamle fazla, rapor 24 dakika. Örneklem temsil edici değildi (356 "sadece renk topla" bölümüne karşı 5). Değişiklik geri alındı; bot kararları artık 1000 bölümde doğrulanıyor.
- **İnceleme:** 18 bulgu; 10'u çürütüldü, 8'i doğrulandı (hepsi küçük önemde) ve düzeltildi. Öne çıkanlar: kalan hamle testi botun kalan hamleye bakmasını yakalamıyordu; web test uygulaması pahalı `usta-<n>` bütçelerine açıktı; `botstats` bir oyun hata verince önceki satırları kaybedebiliyordu.

**Değişiklikler**
- **Bot:** `MoveEvaluator.SpendFactor` 0,25. Yeni `UstaPolicy`, `UstaSettings`, `HalvingPlan`; `BotPolicies`'e `usta` / `usta-<n>` (oyuncu modelleri listesi değişmedi). `TryCreate` boş isimde hata fırlatmak yerine `false` döndürüyor.
- **Motor:** `Game.CreateImagined` (olası bir gelecek: bölümün spawn kuralları, botun kendi tohumu, hamle limiti yok); `ITileSource.Reseed`. Gizli taşlı kopyalardan gelecek üretilemez.
- **Korumalar:** üretici ustayı kalibrasyon botu olarak kabul etmiyor ve boş `reportBots` girdisini reddediyor. Web yalnızca `usta`yı kabul ediyor, en fazla 100 simülasyon.
- **CLI:** `botstats` bütün çekirdekleri kullanıyor (çıktı birebir aynı, hata olursa önceki satırlar korunuyor); `bench`'e usta satırı.
- **Testler:** 98'den 139'a. Kritik testler kasıtlı hata eklenerek sınandı (gerçek rastgeleliği sızdırmak, kalan hamleye bakmak, hamle limitini geri getirmek).
- **Belgeler:** GAME_DESIGN §7, §13, §15, §21.

**Sıradaki:** GAME_DESIGN §21, 3. adım — casual koruması, limit+5 sütunu, mekanik takviminin tablo başlangıçlarına hizalanması.

---

## 2026-10-02 · Oturum 5 — Expert raporu, tema ve yol haritası kararları

**Kullanıcı kararları** (soru-cevap oturumu; ayrıntı [GAME_DESIGN.md](GAME_DESIGN.md) §18–§21)
- **Tema:** genç bir ressam hasar görmüş müzeyi onarıyor; tablolar yapboz parçaları olarak toplanıyor. Fikir kullanıcının.
- **Yapı:** 1 tablo = 10 bölüm, 1 müze odası = 50 bölüm, 1000 bölüm = 100 tablo = 20 oda. Odalar en sevilen akımlarla başlar; her odanın son tablosu o akımın en ünlü eseri.
- **Tablolar:** telifsiz ünlü tablolar; bazılarını biz çizeceğiz (coinle alınan yan koleksiyon).
- **Yapboz:** gerçek mini yapboz, döndürme yok. 10 parçada 3-4-3 dizilimi (prototipte 5×2 ile karşılaştırıldı). Gerçek parayla rastgele paket yok.
- **Müze:** hafif; tablo asılır, oda adım adım onarılır.
- **Hedef oyuncu:** 30 yaş üstü rahat oyuncu, aileye uygun içerik.
- **İş modeli:** ücretsiz oyun + uygulama içi satın alma + isteğe bağlı ödüllü reklam. 5 can + sınırsız can ödülleri, "+5 hamle" teklifi, 7 booster, bitiş şöleni.
- **Kalibrasyon:** average bot + casual koruması (Kolay ≥ %80, Normal ≥ %60). Raporlara limit+5 sütunu.
- **Botlar:** özel taş biriktirme sorunu şimdi düzeltilecek; adil MCTS "usta" botu ayrı bir ölçüm botu olarak yazılacak.
- **Mekanik takvimi:** tablo başlangıçlarına hizalandı (kutu 11, malzeme 51, renk bombası tanıtımı 12).
- **Veri ve yayın:** web test uygulaması oyunları kaydedecek; Play Store'dan önce APK testi; hazır analiz servisi; sınırlı ülke yayını, sonra dünya. Yayında 1000 bölüm. Bölümlere sunucudan düzeltme paketi inebilecek.
- **Sıra:** motor "hazır" → tema ve meta tasarımı → arayüz ve isim → Unity. Görseller en son. Ekip: kullanıcı ve Claude.
- **Motor "hazır" tanımı:** GAME_DESIGN §21'deki 6 adım. v2 engelleri dahil değil.
- **Çalışma düzeni:** kod `beautiful-code` standardına göre yazılacak; tasarım belgeleri profesyonel PDF olarak da tutulacak.

**Bulgular**
- **Expert botun hamle raporu:** expert 1000 bölümün hepsini 30 denemenin 30'unda bitirdi (üst sınır 150 hamle). Limitle kazanma oranı Kolay'da %96, Süper Zor'da %59. 26 bölümde expert'in medyanı limiti aşıyor; en büyük fark #780 (medyan 44, limit 33).
- **Botlar eğitilmedi:** kurallar ve puanlar elle yazıldı. Expert tek hamle ileriyi görüyor ve özel taş biriktiriyor; average'dan belirgin güçlü değil.
- **Üretici temaya hazırdı:** 10 bölümlük zorluk döngüsü ve 50 bölümlük palet grupları, "1 tablo = 10 bölüm, 1 oda = 50 bölüm" yapısıyla birebir örtüşüyor.
- **Tablo kaynağı:** Art Institute of Chicago'da yaklaşık 2.000 kamu malı tablo var. Aynı müzede telifli eserler de bulunuyor; her görüntü tek tek kontrol edilmeli.
- **Yapboz dizilimi:** 10 parçayı düz ızgarayla bölmenin tek yolu 5×2 ve ince uzun parça veriyor. Sıraları 3, 4 ve 3 parçalı dizmek parçaları kareye yakın tutuyor.
- **Bölüm dosyasında `formatVersion` zaten var.** Düzeltme paketleri için eksik olan, bölüm başına içerik sürümü.

**Değişiklikler**
- **CLI:** `botstats` komutu; bir botun her bölümü kaç hamlede bitirdiğinin dağılımını CSV'ye yazar. `LevelReport.CompletionMoves`.
- **Rapor:** `reports/expert_moves.csv`.
- **Prototip:** yapboz kesimi (JS, atılabilir; repoda değil). Van Gogh'un "Yatak Odası" tablosu, 3-4-3 ve 5×2 dizilimleri.
- **Belgeler:** GAME_DESIGN v0.2 — yeni §18 (tema ve meta), §19 (ürün), §20 (kalibrasyon, veri, yayın), §21 (motor "hazır" tanımı); yol haritası, mekanik takvimi ve açık sorular güncellendi.
- **PDF baskıları:** `tools/docs-pdf/build.py` dört belgeyi `docs/pdf/` altına PDF olarak üretir: kapak, belge künyesi, sayfa numaralı içindekiler, üst ve alt bilgi, PDF yer imleri. Kaynak Markdown dosyaları olarak kalır.
- **Araçlar:** Claude Code'a `mattpocock-skills` eklentisi kuruldu (soru-cevap oturumu bununla yapıldı).

**Sıradaki:** GAME_DESIGN §21, 2. adım — bot düzeltmesi ve usta bot.

---

## 2026-10-01 · Oturum 4 — Faz 2: bölüm üreticisi ve ilk 1000 bölüm

**Kullanıcı kararları**
- Web uygulaması yalnızca hata ayıklama ve test için. Mobil tasarıma motor hazır olduğunda geçilecek.
- **Başlangıç tahtası:** her denemede farklı.
- **Zorluk hedefleri:** şimdilik uygun; farklı kombinasyonlar test edilebilmeli.
- **Hedef oyuncu:** emin değil.
- **Hamle sayısı:** bölüme ve bölüm zorluğuna göre değişebilmeli.
- **Tahta boyutu:** bölüm içinde sabit, ilerledikçe değişebilir.
- **Mekanik takvimi:** şimdilik belirsiz.

**Kararlar**
- **Profil dosyası:** bütün üretim ayarları `profiles/default.json`'da. Bantlar miras alıyor; hamle aralığı, kazanma hedefi ve renk sayısı kademe ve bölüm aralığına göre tanımlanıyor. Farklı kombinasyonlar profil kopyalarıyla üretilip raporlar karşılaştırılacak.
- **Hedef oyuncu:** limitler `average` bota göre kalibre ediliyor; raporda `casual` ve `expert` kazanma oranları da var. Karar ertelendi.
- **Örnek bölüm:** `s02` değişken tahtaya döndürüldü.

**Faz 2 — `Puzzle.Generator`**
- **Planlayıcı:** zorluk testere dişi, kademe, bant, tahta boyutu, bölüm grubuna göre renk paleti, mekanik takvimi ve "kanca" bölümleri.
- **Portföy:** 7 şekil stili (dolu, köşe kesme, elmas, çentik, orta delik, sütun boşluğu, kum saati) × kutu desenleri (çift, sütun, küme, dağınık) × jöle desenleri (alt bant, üst bant, merkez, çerçeve, dama, tam) × zincir × malzeme. Hepsi sağ-sol simetrik.
- **Beslenebilirlik analizi:** kutu ve zincir hiç kırılmasa bile her hücreye taş ulaşabilmeli; kapanan yerleşim reddediliyor.
- **Kalibrasyon:**
  - Ölçeklenebilir hedefler, limit aralığa oturana kadar ayarlanıyor.
  - Engel yoğunluğu adaydan adaya uyarlanıyor.
  - Limit, hiç kullanılmamış seed'lerle doğrulanıyor.
  - Hiçbir aday aralığa oturmazsa en yakını `relaxed` olarak tutuluyor.
- **Eşit zorluk havuzu:** seed'ler artık bölümün doğrulanmış kazanma oranını hedefliyor (`--target`). Medyan seed'i hedeflemek zor bölümlerde yanlış sonuç veriyordu: #310'da kazanma %26, ortanca seed'ler %0–8.
- **Rapor:** `levels/generated/report.csv` ve kademe özeti.
- **CLI ve web:** `generate` komutu (`--quick`, `--overwrite`, `--repair`); web laboratuvarında bölüm arama kutusu.
- **Testler:** 61'den 68'e.

**İlk 1000 bölüm — sonuçlar**

Üretim 61 dakika, onarım 41 dakika sürdü. Hiç başarısız ya da gevşetilmiş bölüm yok. Sorunu düzeltilmiş olarak 257 bölüm yeniden üretildi, 743'ü aynen korundu.

| Kademe | Bölüm | Ort. hamle | Hedef | Average | Casual | Expert | Near-miss |
|---|---|---|---|---|---|---|---|
| Kolay | 204 | 22,6 | %87 | %87 | %67 | %96 | %10 |
| Normal | 497 | 26,5 | %68 | %69 | %39 | %90 | %23 |
| Zor | 199 | 31,1 | %43 | %44 | %18 | %74 | %35 |
| Süper Zor | 100 | 35,5 | %28 | %29 | %10 | %57 | %41 |

- **Onarım:** ilk tam üretimde 231 bölümün seed havuzu 8'in altındaydı (50'si 4 veya altı), 29 bölüm hedeften 7 puandan fazla sapıyordu.
  - Havuz en az 8 seed olacak şekilde garantiye alındı.
  - Aralığa sığmayan doğrulama artık adayı reddediyor.
  - `--repair` yalnızca sorunlu bölümleri yeniden üretti. Sonuç: sapan bölüm 29'dan 2'ye indi, en küçük havuz 8.
- **Determinizm doğrulandı:** sağlam bir bölüm yeniden üretilince dosyası bayt bayt aynı çıktı.
- **Bulgu: expert bot average'dan güçlü değil** (#30'da 300 oyun: expert %45, average %48,7; expert'in kötü oyunları daha kötü). Muhtemel sebep, özel taş harcamayı cezalandıran puanlama yüzünden özel taşları biriktirmesi. Rapordaki expert sütunu şimdilik bilgi amaçlı. Gerçek "usta oyuncu" sınırı için geleceği bilmeyen güçlü ajan gerekiyor (plan).
- **Hedef oyuncu sorusuna veri:** Normal bir bölümü average oyuncu %69, casual oyuncu %39 kazanıyor.

---

## 2026-10-01 · Oturum 3 — Eşit zorlukta tekrar denemeler, optimum kanıtı, CI, LAN sunucusu

**Kullanıcı kararları**
- **Tekrar deneme:** taşlar farklı desende gelir, ama algoritmik matematik denemeden öncekiyle aynı kalır.
- **Başlangıç tahtası:** sabit de olabilir değişken de; zorluk aynı olmalı. Kesin karar web laboratuvarında denendikten sonra verilecek.
- GitHub Actions eklensin.
- **NP-zor itirazı:** "Kural belli olduğu için en iyi yolu bulmak zor olmaz; zor olan kuralı tersine mühendislikle çözmek."
- Aynı Wi-Fi'deki her cihazdan erişilebilen bir sunucu başlatılsın.

**Kararlar**
- **Eşit zorluk havuzu:** her bölüm, tipik zorluğa (limitteki kazanma oranı ±5 puan) yakın 16 seed taşıyor. Deneme k havuzdaki (k mod 16). seed'i kullanıyor.
- **Ayrı seed'ler:** tahta seed'i ve oyun seed'i ayrıldı (`AttemptSeeds`). Böylece "sabit tahta, değişen taşlar" mümkün.
- **DDA yok:** denemeler arasında gizli kural değişikliği yapılmıyor (`AssistPercent` = 0).
- **Örnek bölümler:** zor örnekler (s03, s04) 6 renge çekildi. Dördü de havuz aldı; s02 "sabit tahta" örneği.

**Bulgular**
- **Rastgele seed'ler eşit değil:**
  - Jöle bölümünde (6 renk) seed başına medyan 8 ile 29 hamle arası; limitteki kazanma oranı %55 ile %100 arası.
  - Havuz bunu %83–91'e indiriyor.
  - Seçim ölçüsü medyan hamleden limitteki kazanma oranına çevrildi: aynı medyana sahip iki seed'in "kuyrukları" farklı olabiliyor.
- **Optimum kanıtı:** tam arama (`ExhaustiveSearch`) bütün kısa dizileri denedi.
  - Seed 2 ve 3: beam 64'ün yolu gerçekten en iyisi (kanıtlandı).
  - Seed 0 ve 5: gelecek taşları bilen beam 64 beş hamle buldu; tam arama dört hamlelik daha kısa bir kazanç buldu.
  - Pozisyon başına ~37–40 geçerli hamle var. 4 hamle derinlik ~1 milyon pozisyon (1,5 sn); 6–7 hamle derinlik saatler veya günler sürüyor.
  - Özet: kuralı izlemek kolay, iyi yol bulmak kolay, en iyiyi kanıtlamak zor.
- **Güvenlik duvarı:** Windows'ta .NET Host için gelen bağlantıları engelleyen kurallar ve "Public" Wi-Fi profili var. Diğer cihazların bağlanması için kullanıcının bunları kendisinin açması gerekiyor (README'de komutlar).

**Değişiklikler**
- **Core:** `AttemptSeeds`, `Game.Start(level, AttemptSeeds)`, `StartBoard` (Varied / Fixed), `AttemptPolicy`, bölüm dosyasında `attempts`.
- **Bot:**
  - `SeedPoolCalibrator`, `ExhaustiveSearch`.
  - Çözücü dallanma faktörünü raporluyor.
  - `SimulationRunner` artık `AttemptSeeds` alıyor.
- **CLI:** `seeds` komutu (havuzu ölçer ve `--write` ile yazar), `solve --prove`.
- **Web:**
  - Yeni kontroller: "Yeni deneme" (havuz), "Aynı seed" (birebir tekrar), "Başlangıç tahtası" seçici.
  - Statik dosyalar `no-cache` ile sunuluyor.
  - Telefonda üst çubuk düzeltildi.
  - LAN için `web-lan` başlatma ayarı eklendi (derlenmiş kopya, 0.0.0.0:5180).
- **CI:** `.github/workflows/ci.yml` (build, test, bölüm doğrulama).
- **Testler:** 55'ten 61'e.

---

## 2026-10-01 · Oturum 2 — İki bomba, renk paleti, spawn yönetmeni, çözücü, web laboratuvarı

**Kullanıcı istekleri**
- "Neden iki farklı bomba olmasın?"
- Özel taşa dokunarak patlatma kalsın.
- Bölümler ilerledikçe yeni renkler, yeni engeller, yeni özel taşlar ve temalar gelsin.
- Repo bağlansın: https://github.com/SaturnWave/Puzzle
- Şimdilik basit bir web arayüzünden test edebilsin.
- Oyunda paralel çalışan algoritmalar olsun. Gözlem: "doğru hamlelerle geçilen bir yol var, yanlış hamle şansı düşürüyor, gelen taşlar seni bir duruma itiyor" (DFS benzeri). Bunun üzerine araştırma yapılsın.
- Bütün gelişmeler bir yere not edilsin (bu dosya).

**Kararlar**
- **İki bomba.** L/T/+ (tam 5 taş) → Bomba 3x3. 6+ taşlı kesişim → Büyük Bomba 5x5. İki bomba birleşince yarıçaplar toplanıyor (5x5 / 7x7 / 9x9). Roket + bomba 3 sıra/sütun, roket + büyük bomba 5 sıra/sütun temizliyor.
- **Renkler.** 8 renk var (camgöbeği ve pembe eklendi). Her bölüm 3–8 renklik kendi paletini seçiyor. İlerleme planı: 4 → 5 → 6 → 7 → 8 renk.
- **Spawn yönetmeni ("karma algoritma").** Her yeni taşı dört strateji birlikte belirliyor: denge (dueness), hedef eğilimi, kaskad kontrolü, DDA yardımı.
- **Etik korkuluk.** Oyun içi DDA yardımı yalnızca 0..100, yani sadece kolaylaştırıyor. Zorlaştırmak tasarım zamanı kararı.
- **Çözücü.** Mükemmel bilgili beam search eklendi: oyunun birebir replikası üzerinde gelecek taşları bilerek oynuyor ve "her seferinde doğru hamle" yolunu buluyor.
- **Retry politikası önerisi.** Her denemede yeni refill seed'i. Karar kullanıcıda (GAME_DESIGN §14).
- **Web laboratuvarı.** Gerçek motor sunucuda (ASP.NET Core) çalışıyor, tarayıcı sadece event'leri canlandırıyor. GitHub Pages private repoda ücretli plan istediği için yerel sunucu seçildi.

**Bulgular**
- **Çözücü.** Jöle bölümünde (6 renk) gelecek taşları bilen oyun 7,7 hamle, insan benzeri bot 18,2 hamle, rastgele bot ~90 hamle. Kullanıcının gözlemi sayılarla doğrulandı: doğru hamle yolu var, yanlış hamleler ihtiyacı 1,6–2,4 katına çıkarıyor.
- **Kaskad kontrolü.** Kaskadları ~%25 azaltıyor ama hamle ihtiyacını yalnızca ~%5–8 artırıyor. Ana zorluk kolu hâlâ renk sayısı.
- **Gizli durum hatası.** Yerçekimindeki çapraz kayma yön sırası hamleler arasında taşınan gizli bir durumdu ve replika kopyalarına geçmiyordu; çözücünün bulduğu yol tekrar oynatılınca tutmuyordu. Düzeltme: yerçekimi artık sadece tahtanın durumuna bağlı. Test, replikayı oyun ortasında oluşturacak şekilde sıkılaştırıldı.
- **Kapalı küme testleri işe yaradı.** Büyük Bomba eklenince hasar kodundaki "bilinmeyen tür" kolu eksik switch'i hemen yakaladı.
- **Araştırma** ([docs/research/spawn-control-and-solvability.md](research/spawn-control-and-solvability.md)):
  - Ticari oyunlarda retry'da aynı refill'in kullanıldığına dair birincil kaynak yok.
  - Belgelenmiş olan şu: her denemede hangi seed'in verileceğinin bir zorluk kolu olarak seçilmesi (EA) ve beceriye göre taş torbası (Zynga patentleri).
  - Taşlar bilinse bile mükemmel oyunu bulmak NP-zor.
  - AB 30 Eylül 2026'da King ve Playrix'in satıcısı dahil 9 şirket hakkında koordineli işlem başlattı.

**Değişiklikler**
- **Core**
  - `TileKind.BigBomb`, `RuleSet.BombRadius` (1) ve `BigBombRadius` (2); bomba kombolarında yarıçap toplanıyor.
  - `EffectKind.Cross` artık yarıçaplı; eski `WideCross` kaldırıldı.
  - `GemColor.Cyan` ve `Pink`; `SpawnRules.Palette`, `GoalBiasPercent`, `CascadeBiasPercent`.
  - `SpawnDirector` ve strateji sınıfları; `GameOptions.AssistPercent`.
  - `Game.CreateReplica()` (mükemmel bilgili kopya).
  - Aktive edilen özel taşlar için de `TileDestroyed` event'i.
- **Bot:** `BeamSearchSolver`, `BotPolicies`, `BatchSimulator`, `MoveEvaluator.ValueOf`.
- **CLI:** `solve` komutu.
- **Yeni projeler:**
  - `Puzzle.Tooling`: bölüm JSON'u ve bölüm kütüphanesi.
  - `Puzzle.Web`: test laboratuvarı.
- **Testler:** 44'ten 55'e. Kapsanan konular: iki bomba, palet, kaskad eğilimi, replika determinizmi, çözücü yolunun tekrar oynatılması.

---

## 2026-10-01 · Oturum 1 — Faz 1: çekirdek motor ve test botu

**Kararlar**
- **Platform ve motor:** Unity + C#, swap match-3, iOS + Android.
- **Çekirdek:** motordan bağımsız `netstandard2.1` / C# 9 kütüphanesi; bot, üretici ve oyun aynı kodu çalıştırıyor.
- **Tahta:** büyük ve değişken boyutlu (10x10, 10x12…), boşluklar geçirgen.
- **Özel taşlar:** renksiz (Royal Match tarzı) ve dokunarak patlatılabilir.

**Bulgular**
- 10x10 tahtada 5 renk çok cömert: hamle başına 2 kaskad. Standart 6 renk olmalı.
- Kutu sırasının altındaki hücreler kutu kırılana kadar boş kalabiliyor. Bu bir üretici kısıtı.
- Hız: motor 10–16 bin hamle/sn; 12 çekirdekte bir bölümün kalibrasyonu ~1 sn.

**Değişiklikler**
- Puzzle.Core, Puzzle.Bot, Puzzle.Cli.
- 44 test.
- 4 örnek bölüm.
- docs/GAME_DESIGN.md ve research.md.
