# Hata Kaydı ve Dersler

**Ne için:** geliştirmede yapılan ve geç fark edilen hatalar. Her biri için ne olduğu, neden olduğu, neden yakalanmadığı ve bir daha olmaması için konan kural ve denetim. Sahibin isteği (2026-10-29): "Hata yapılan noktaları kaydet; sonraki geliştirme aşamalarında bu hata yaşanmamalı." Yeni bir hata bulununca en üste eklenir. Kurallar sonraki her aşamada geçerlidir; Claude'un oturumları ve ajanları onları depodaki `CLAUDE.md`'den de okur.

## Kurallar

1. **Gönderilen şey denenir.** Mağazaya giden App Bundle, test APK'sından başka bir üründür: bölünmüş APK'lar, kurulumla gelen asset pack, Play Asset Delivery kütüphanesi, Play'in imzası ve otomatik koruması (`com.pairip`) yalnız onda var. Test APK'sında geçen bir deneme paketi kanıtlamaz. Her yüklemeden önce paketin kendi APK seti telefonda açılır. Kapalı teste ya da üretime gitmeden önce paket dahili testten Play üzerinden de kurulup açılır (`docs/PLAY_RELEASE.md` 10).
2. **Kütüphanenin tanımladığı, kanıtsız çıkarılmaz.** Bir kütüphanenin manifestte tanımladığı bileşen (servis, sağlayıcı, alıcı, etkinlik) ya da izin çıkarılmadan önce iki şey yapılır. Önce kütüphanenin kodu okunur: o bileşene nasıl dokunuyor (`javap -c`; sınıf adı, `setComponentEnabledSetting`, `getComponentEnabledSetting`, `startService`, `bindService`). Bileşen bir başlatıcıysa, erken çalışmasının neyi mümkün kıldığı da okunur: kurduğu geri çağırmalar (`registerActivityLifecycleCallbacks`) geç başlatmada kaçırılan olayları bekler (H7). Sonra değişiklik, gönderilecek paket biçiminde telefonda denenir.
3. **Denetim bir varsayımı kilitlemez.** "X olmamalı" diyen bir derleme denetimi, X'in çalışırken kullanılmadığının kanıtını belgesinde gösterir. Kanıtsız bir yasak hatayı korumaya alır: versionCode 1'de denetim, oyunu kapatan servisin olmamasını şart koşuyordu.
4. **Varsayım olgu gibi yazılmaz.** Doğrulanmamış bir öngörü belgede "denenmedi" diye işaretlenir ve bir telefon testine bağlanır.
5. **Yazılan test koşulmadan iş bitmiş sayılmaz.** Bir değişikliğin telefonda kanıtı "Bekliyor" durumundaysa o değişiklik mağazaya gitmez. Zaman baskısı gerekçe değildir.
6. **Çökme okunabilir olur.** Paket yerel kütüphanelerin sembol tablolarını taşır. Telefondaki çökme kaydı okunur: `adb logcat -b crash`, `adb shell dumpsys dropbox`, Unity'nin iş parçacığı dökümündeki "Pending exception" satırı.
7. **Metin derlemeyle uyuşur.** Mağaza metni ve sürüm notları yalnız o derlemenin yaptığını söyler. Kapalı olan ya da kurulmamış bir özellik (açılış kaydındaki "not set up", "turned off" satırları) yüklemeden önce sahibe söylenir: ya kurulur ya metin düzeltilir.
8. **Beklenti kodun kuralıyla karşılaştırılır; bulgu yayına sessiz gitmez.** Bir telefon testinin beklentisi, kodun bütçe, süre ve kapılarıyla karşılaştırılarak yazılır. İki cihazlı her beklenti bir birim testiyle de denenir: iki kurulum, tek sahte bulut, gerçek `SyncPolicy`, saat ileri sarılmadan. Bir bütçe kararının oyuncunun göreceği sonucu (örneğin "öteki cihaz 12 saate kadar geç görür") sahibe söylenir. Yüklemeden önce bilinen her bulgu ya düzeltilir ya bir işe bağlanıp sahibe söylenir (H9).

## H9 — Aynı hesaptaki iki cihaz birbirini geç görüyor; ikinci cihazın kartı Ayarlar'ın arkasında (2026-11-05)

- **Ne oldu:** kapalı testin ilk testçisi tabletle telefonu eşitleyemedi. Telefon Play Games'e bağlıydı. Tablette "Bağlan"a defalarca basıldı, ama tablet kendi anonim hesabında kaldı; telefon da tabletin kaydını hiç sormadı. Sahibin ekran görüntülerinde iki ayrı oyuncu kodu vardı. Testçi tablette Ayarlar'ı kapatıp "Ona geç"i seçince iki ilerleme birleşti (T592, DEVLOG Oturum 39).
- **Neden:**
  - Play Games kimliği telefonun hesabında olduğundan tabletin bağlaması "Başka bir kayıt var" kartına düşüyor. Kart Ayarlar kapanana kadar görünmüyor. Bu 4 Kasım'da telefonda görüldü ve DEVLOG'a "küçük bulgu" diye yazıldı, ama düzeltilmeden vc3'le çıktı.
  - Aynı hesaptaki bir cihazın yazdığı, öteki cihaza açılışta en çok 12 saatte bir gelir: `SyncPolicy.ReadsFirst` açılışta yalnız `ReadEvery` (12 saat) geçtiyse okur. Cihaz ötekinin yazdığını ancak kendi yazması reddedilince (bir bölüm kazanınca) ya da anahtar kapatılıp açılınca görüyor.
  - Oturum 34'te (26 Ekim) bütçenin varsayılanı olarak planın sıkı kuralı seçildi. Plan bu kuralı büyük oyuncu sayısı için önermişti (`docs/research/server-v1.md` §2.4: varsayılan kuralla ücretsiz katman yaklaşık 50 bin aylık oyuncuya yeter).
  - Veri kaybı yok: yazma kilidi üstüne yazmayı önlüyor, iki kayıt birleşiyor.
- **Neden yakalanmadı:**
  - İki cihaz testleri (T591, T592) yüklemeden önce koşulmadı; sahibin 4 Kasım kararıyla test kullanıcılarına kaldı.
  - Birim testi iki telefonun birleşmesini saati 12 saat ileri sararak deniyor (`CloudSyncTests.Two_phones_of_one_player_add_up_what_each_earned`). Gecikme bilinen bir bütçe kararıydı, ama oyuncunun göreceği sonucu sahibe söylenmedi. T591 "telefon yeniden açılınca 5. bölümde" diyor; bu, kodun kuralıyla karşılaştırılmadan yazılmış bir beklenti.
  - Yayına giden küçük bulgu bir işe bağlanmadı; yükleme öncesinde sahibe ayrıca söylenmedi.
- **Etkisi:** testçi tabletteki ilerlemesinin kaybolacağından korktu. Telefon tableti sormayınca "birden fazla cihazda bulut kaydı çalışmıyor" sanıldı.
- **Düzeltme (sahibin "Ok yap"ı, 5 Kasım):**
  - Remote Config: production'da `cloud_read_min_hours = 1` (Long), aynı gün yayında. Güncellemesiz; açılışta okuma saatte bire indi.
  - vc4 için kod: Play Games ya da Game Center'a bağlı hesap her oturum başında okur, soğuk açılışta ve arka plandan dönüşte (`SyncTrigger.CameBack`, `SaveWriters`; `GameRoot` dönüşte ister). Anonim hesap bütçede kalır. Remote Config'in altıncı anahtarı `cloud_linked_reads_each_session` (varsayılan açık) bağlı hesabı bütçeye döndürür.
  - Kart (SaturnWave/Puzzle#11): hesap kartındaki "Bağlan" Ayarlar'ın kartlarını hemen kaldırıyor, soru beklemeden geliyor. Aynı soru beklerken ya da asılıyken ikinci kez gelmiyor. İkinci satır bu cihazın bölümünü söylüyor ("Geçersen Bölüm X kalır, altınlar toplanır."), 13 dilde, "telefon" sözcüğü olmadan. İlk uzun hâli kartta %53'e küçülüyordu; kısaltıldı.
  - Denetim: birim testleri (iki cihazlı `CloudSyncTests`, `CloudSavingTests`, `SyncPolicyTests`; saat ileri sarılmadan, kural 8) ve Unity'de `CloudCheck`'in hesap kartı durumu. Telefonda T597 ve T598 bekliyor (PHONE_TESTS F35).
- **Bir daha olmasın diye:** kural 8.

## H7 — Yeni telefonda Play Games'in sessiz girişi hiç denenmiyordu (2026-11-04)

- **Ne oldu:** versionCode 3'ün APK seti yüklemeden önce telefonda denendi (`docs/PLAY_RELEASE.md` 10). Ayarlar'daki "Link" çalıştı. Oyunun verisi silinip yeniden açılınca ilerleme geri gelmedi: açılıştaki sessiz giriş `ApiException: 10` ile döndü ve oyun yeni bir anonim hesap açtı (T590 kaldı, iki kez aynı). İlerleme yalnız "Link" ve "Switch" ile geri geldi. Mağaza metni "Link it to Google Play Games to bring your progress to a new phone" diyor; yeni telefonda bu kendiliğinden olmuyor.
- **Neden:** Play Games SDK'sı (play-services-games-v2 22.1.0) kendiliğinden girişi yalnız bir etkinlik oluşturulurken başlatıyor. `PlayGamesSdk.initialize` bir `ActivityLifecycleCallbacks` kuruyor ve deneme yalnız `onActivityCreated`'da başlıyor (`internal.games_v2.zzar`; `onActivityStarted` ve `onActivityResumed` boş). SDK'yı süreçle birlikte başlatan `PlayGamesInitProvider` manifestten çıkarılmıştı (sahibin 1. kararı: "Bulut kaydı" kapalıyken Play Games de çalışmaz). Oyun SDK'yı ekran açıldıktan sonra başlatıyor (`PlayGamesPlatform.Activate`). Deneme hiç başlamıyor, giriş durumu "UNINITIALIZED" kalıyor ve `isAuthenticated` bu durumda hemen 10 dönüyor (`internal.games_v2.zzbq.zzs`). Elle giriş (`signIn`) bu denemeye bağlı değil, o yüzden bağlama çalışıyor.
- **Neden yakalanmadı:** 26 Ekim'de kütüphanenin kodu "SDK'yı kim başlatıyor" sorusu için okundu: eklenti başlatıyor, bu doğru. Sağlayıcının erken çalışmasının neyi mümkün kıldığı okunmadı (kural 2). Derleme denetimi sağlayıcının olmamasını şart koşuyor (`ManifestPlayGamesStart`); kanıtı bu eksik okumaydı (kural 3). Belge sonucu doğru biçimde öngörü diye işaretlemiş ve T590'a bağlamıştı (kural 4, `docs/research/server-v1.md` §5.8): yükleme öncesi kapı çalıştı.
- **Etkisi:** ilk paket yüklenmedi; versionCode 3 boş kaldı. Bağlama, "Another save found" kartı, "Switch" ve geri yükleme kartı telefonda çalışıyordu; kendiliğinden geri yükleme çalışmıyordu (A55).
- **Düzeltme (sahibin seçimi, 2026-11-04):** Oyunun kendi sağlayıcısı (`unity/Assets/Plugins/Android/PlayGamesStart.java`) SDK'yı süreçle birlikte başlatıyor, ama yalnız `cloud.json`'daki anahtar açıkken. Yeni kurulumda anahtar açık, okunmayan dosyada kapalı; sahibin 1. kararı korunuyor. Google'ınki yine çıkarılıyor.
  - Derleme sağlayıcıyı manifestte (`aapt`) ve sınıfını paketin dex'inde arıyor: sınıfı olmayan bir sağlayıcı oyunu her açılışta kapatırdı (H5).
  - Bir test, Java'nın okuduğu `cloud.json` alanlarını C#'ın dosya düzenine bağlıyor.
  - Yeni paket telefonda denendi: veri silinince ilerleme kendiliğinden geldi (T590, T545). Anahtar kapalıyken Play Games başlamadı (T541). Çökme yok.
  - Kalan kenar durumlar A55'te.
- **Bir daha olmasın diye:** kural 2'ye başlatıcının geri çağırmaları eklendi. T590 her Play Games değişikliğinde verisi silinmiş telefonda yinelenir.

## H6 — Aynı Google hesabıyla telefon ve tablette ilerleme eşitlenmiyor (2026-10-30)

- **Ne oldu:** sahip oyunu aynı Play Store hesabıyla hem telefona hem tablete kurdu; ilerleme iki cihaz arasında eşitlenmedi.
- **Neden:** cihazlar arası kayıt Google Play Games girişine dayanıyor (`docs/research/server-v1.md` §5.1, §3.6). Oyun Play Games'e girince Unity Authentication o kimliğin oyuncusuna girer ve iki cihaz aynı bulut kaydını paylaşır. Kod hazır (`Cloud/Ugs/PlayGamesSignIn.cs`, `UgsAccounts.cs`). Ama "Platform kurulumu" (plan §9 B2–B5, A4; Aşama 6) yapılmadı: derlemede Play Games'in oyun kimliği ve web istemci kimliği yok. Oyun bu yüzden her cihazda ayrı bir anonim hesap açıyor; açılış kaydı bunu söylüyor: "Google Play Games: this build is not set up for the game's Play Games project …, so the account stays on this phone".
- **Neden yakalanmadı:** eksik planda yazılıydı, ama mağaza paketi gönderilirken sahibe açıkça söylenmedi. Mağaza metni ise "Link it to Google Play Games to bring your progress to a new phone" diyor; bu derlemede bağlanma yok. Yükleme öncesi listesinde "metin derlemeyle uyuşuyor mu" maddesi yoktu.
- **Kural (7):** mağaza metni ve sürüm notları yalnız o derlemenin gerçekten yaptığını söyler. Yüklemeden önce açılış kaydındaki "not set up / turned off" satırları okunur; metnin söz verdiği bir özellik kapalıysa ya kurulur ya metin düzeltilir, karar sahibe sorulur (`docs/PLAY_RELEASE.md` 10, 5. adım).
- **Düzeltme:** Aşama 6 (Play Games kurulumu) versionCode 3'e; sahibin adımları plan §9 B2–B5 ve A4'te (web istemcisinin sırrı Unity'ye sahip tarafından girilir).

## H5 — versionCode 1 Play'den kurulunca her açılışta kapanıyordu (2026-10-29)

- **Ne oldu:** Google kapalı testi onayladı. Sahip oyunu Play'den kurdu; oyun her açılışta bir iki saniye içinde kapandı. Test bağlantısındaki herkes aynı sürümü aldı.
- **Neden:** 28 Ekim'de, paketin istediği izinleri test APK'sınınkilerle aynı tutmak için Play Asset Delivery'nin ön plan servisi (`ExtractionForegroundService`) manifestten çıkarıldı. Unity'nin oynatıcısı açılışta kütüphaneden `AssetPackManager`'ı istiyor; kütüphane önce bu servisi `setComponentEnabledSetting` ile açıyor. Android tanımadığı servisi açmayı reddetti (`IllegalArgumentException`), Unity de bu hatayla durdu (`JNI DETECTED ERROR … GetObjectClass`). Ayrıntı: `docs/PLAY_RELEASE.md` 9.
- **Neden yakalanmadı:**
  - Test APK'sında kütüphane yok; telefondaki bütün denemeler test APK'sıyla yapılmıştı.
  - Paketin APK seti yüklemeden önce telefonda açılmadı. T578 ve T579 yazılmıştı ama "Bekliyor"du (kural 1, 5).
  - Kütüphanenin kodu bu servis için okunmadı. Aynı gün Play Games'in sağlayıcısı için okunmuştu (kural 2).
  - Derlemenin denetimi servisin olmamasını şart koşuyordu; hatayı korudu (kural 3).
  - Belge "kütüphanenin geri kalanı olduğu gibi duruyor, oynatıcı onu bütün bulsun diye" diyordu: denenmemiş bir öngörüydü (kural 4).
  - Play Console çökme raporunu gösterseydi bile sembol yoktu (kural 6).
- **Etkisi:** ilk kapalı test sürümü kullanılamaz oldu; fazladan bir inceleme turu gerekti; test kullanıcılarının ilk izlenimi bir çökme.
- **Düzeltme:** servis kalıyor, yalnız türü siliniyor; izinler yine dışarıda. Denetim tersine döndü: paketin temel APK'sında kütüphanenin açılışta açtığı iki servis tanımlı olmalı ve hiçbir ön plan servis türü olmamalı; test APK'sında kütüphaneden hiçbir şey olmamalı. Paket sembol tablolarını taşıyor. versionCode 2 telefonda APK setinden açıldı ve oynandı (commit 0721947).
- **Bir daha olmasın diye:** kural 1–6, `docs/PLAY_RELEASE.md` 10 (her yüklemeden önce zorunlu liste), `CLAUDE.md`.

## Önceki dersler

| Kod | Ne oldu | Kural ve denetim | Kaynak |
|---|---|---|---|
| H8 | 4 Kasım: yeni testler derlenmeden "6.189 test geçti" diye rapor edildi. `dotnet test` çıktısının yalnız sonu okundu; derleme hatası (CS0103) uyarıların arasında kaldı, ardından `--no-build` eski DLL'i koştu. GitHub'ın CI'ı birkaç dakikada yakaladı. Düzeltilirken test projesi Release'te derlendi; proje Puzzle.Web'e bağlı olduğundan sunucunun Release dosyaları yeniden yazıldı (sunucu kapalıydı). | Test sonucu "Passed!" ya da "Failed!" satırından ve test sayısından okunur; test eklenince sayı artmalı. `--no-build` yalnız aynı oturumda başarılı bir derlemeden sonra kullanılır. Testler Debug'da derlenir: test projesi Puzzle.Web'e bağlı. | DEVLOG Oturum 39 |
| H4 | 28 Ekim, ilk paket: yükleme anahtarının parolaları Unity'nin anahtar ayarlarına verildi. Unity onları derleme klasöründeki sekiz dosyaya düz metin olarak yazdı (Gradle'ın `build.gradle`'ı dahil). | Sır Unity'ye hiç verilmez. Paket Unity'nin hata ayıklama anahtarıyla derlenir, sonra `jarsigner` ile imzalanır; parola yalnız aracın kendi ortamına gider (`-storepass:env`). | `docs/PLAY_RELEASE.md` 3.3 |
| H3 | 26 Ekim: Play Billing 9.0.0'ın kayıtçısı, Google'ın datatransport'uyla telefonun model, sistem, ülke, dil ve ağ bilgisini gönderiyordu; oyunun ayar satırı "yalnız satın almalar çıkar" diyordu. Telefonda ağ izlenince bulundu (T186). | Gizlilik metninin söylediği, telefonda ağ izlenerek doğrulanır. Kayıt arka ucu manifestten çıkarıldı (`ManifestLoggingBackend`); paket `aapt` ile denetleniyor. | `docs/DEVLOG.md`, PHONE_TESTS T186 |
| H2 | Satın alma denemesi: Unity bir önceki derlemenin Gradle manifestini yerinde bıraktı; bir iznin geri verilmesi yeni pakete geçmedi (INTERNET geri çekili kaldı). | Her derlemeden önce tutulan manifest silinir (`AndroidBuild.TakeAwayTheKeptManifest`); paketin izinleri derlemeden sonra `aapt` ile okunur. | `docs/research/iap-and-analytics.md` 3.4 |
| H1 | Unity'nin oyun içi denetimleri (`PlayModeCheck`, `MusicCheck`) `-quit` ile başlatılınca hiçbir şey denetlemeden bitiyordu. | Bu denetimler `-quit` olmadan çalıştırılır; çıkış kodu değil, sonuç satırı okunur. | `unity/Assets/Editor/PlayModeCheck.cs`, `MusicCheck.cs` (kullanım satırı) |
