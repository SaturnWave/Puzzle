# Google Play'e Yükleme (Kapalı Test)

**Tarih:** 2026-10-28. **Kimin için:** proje sahibi. **Ne:** oyunun Google Play'e giden ilk paketi, bir Android App Bundle (`.aab`), ve onu kapalı test kanalına yükleme. Sahibin kararı (2026-10-28): Android derlemesi şimdi kapalı teste gider; kişisel geliştirici hesabında üretime geçmeden önce **12 test kullanıcısı 14 gün** boyunca kapalı testte olmalı. Gerçek reklamlar (LevelPlay) bu 14 gün içinde gelecek; bu paket reklamları hâlâ 3 saniyelik canlandırma kartıyla gösterir. Claude hiçbir hesaba girmez, anahtarı ve parolaları görmez; anahtarı siz yaparsınız, yüklemeyi siz yaparsınız.

## 1. Bu paket ne

| Ne | Değer |
|---|---|
| Paket kimliği | `com.saturnwave.museumpuzzle` (Play Console'daki "Museum Puzzle" kaydı, 2026-10-27) |
| versionCode / versionName | 1 / 1.0 |
| Derleme türü | Mağaza derlemesi: gerçek parayla satın alma Google Play'den (`economy/default.json` → `build.realMoneyPurchases: "store"`), reklamlar canlandırma (`"simulated"`), test modu **yok**. Giriş sayfası "Sürüm 1.0" der ("test sürümü" demez). Bulut kaydı UGS'nin `production` ortamına gider |
| Hedef | Android 16 (API 36), en düşük Android 8.0 (API 26), yalnız 64-bit ARM (arm64-v8a), IL2CPP |
| Dosya | `D:/puzzle-cache/play/museumpuzzle-vc1-upload.aab` (depo dışında; depoya girmez). 28 Ekim'deki paket: 306.397.933 bayt, SHA-256 `d6eb69ff…78eb9a` |
| Boyut | Paket dosyası 306,4 milyon bayt. Telefonun indirdiği (bundletool `get-size total`): temel modül 59,6 MB (Play'in sınırı 200 MB); temel modülle birlikte `UnityDataAssetPack` 305,1 MB, parçanın kendi payı ≈245,5 MB (aşağıda) |
| İzinler | Tam beş: `VIBRATE`, `INTERNET`, `ACCESS_NETWORK_STATE`, `com.android.vending.BILLING` ve Unity'nin `…DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION`'ı. Reklam kimliği (`AD_ID`) yok |
| İmza | Sizin yükleme anahtarınız (`upload`), 3. bölüm. Unity paketi kendi hata ayıklama anahtarıyla derler; derleme o imzayı çıkarıp paketi Java'nın `jarsigner`'ıyla sizin anahtarınızla yeniden imzalar |

**Neden iki parça:** test APK'sı 306 MB, Play ise paketin temel modülünün sıkıştırılmış indirmesini en çok 200 MB ile sınırlıyor. Büyük olan oyunun verisi (tablolar ve öteki resimler, sesler, Unity'nin sahne dosyaları; APK'da `assets/bin/Data`, sıkıştırılmış 272 MB); kod, Unity'nin kütüphaneleri ve ilk sahnenin dosyaları indirmede ≈60 MB. Paket derlenirken Unity'nin **Split Application Binary** ayarı açılır: Unity bu veriyi `UnityDataAssetPack` adlı bir **install-time asset pack**'e koyar. Play bu parçayı oyunla birlikte, aynı kurulumda indirir; oyuncu için tek uygulama, ayrı bir indirme yok. Ayar yalnız paketin derlemesinde açılır; test APK'sı bugünkü gibi tek dosya kalır.

**Play Asset Delivery'nin fazlası:** asset pack'li bir paket için Unity, Google'ın Play Asset Delivery kütüphanesini (`com.google.android.play:asset-delivery` 2.3.0) ekliyor. Kütüphane, kurulumdan **sonra** indirilen paketleri (fast-follow, on-demand) açmak için `dataSync` türünde bir ön plan servisi ve bu işi yürüten WorkManager'ı (androidx.work 2.9.1) getiriyor; onlarla dört izin geliyordu: servis için `FOREGROUND_SERVICE` ve `FOREGROUND_SERVICE_DATA_SYNC` (Play Console bunun için ayrı bir beyan ve video ister), WorkManager için `WAKE_LOCK` ve `RECEIVE_BOOT_COMPLETED`. İlk derlemenin denetimi bunu yakaladı ve paketi durdurdu. Oyunun tek parçası kurulumla gelir, sonradan bir şey indirilmez ya da açılmaz. Manifest adımı artık ön plan servisini çıkarıyor ve dört izni geri çekiyor (`unity/Assets/Editor/ManifestAssetDelivery.cs`); paket test APK'sıyla aynı beş izni istiyor. Kütüphanenin geri kalanı olduğu gibi duruyor (Unity'nin oynatıcısı kütüphaneye paketini sorarsa onu bütün bulsun diye): WorkManager oyunla başlıyor ama ona iş verilmiyor, işsizken uyanık tutma ya da açılış izni gerekmiyor. Telefonda doğrulama: T579 (açılış, tablolar, `logcat`'te hata yok).

## 2. Sırayla yapılacaklar

| # | Kim | Ne | Durum |
|---|---|---|---|
| 1 | Sahip | Yükleme anahtarını yap (3.1) | Yapıldı, 2026-10-28 |
| 2 | Sahip | Anahtarı ve parolayı yedekle (3.2) | |
| 3 | Sahip | İki ortam değişkenini koy (3.3) | Yapıldı, 2026-10-28 |
| 4 | Claude | Paketi derle (4) | Yapıldı: `museumpuzzle-vc1-upload.aab` |
| 5 | Sahip | Play Console'un "uygulamanı kur" görevleri (5.1) | |
| 6 | Sahip | Paketi kapalı teste yükle (5.2) | |
| 7 | Claude | Yüklenen sürümü kayda yaz (6) | Yükleme kabul edilince |
| 8 | Sahip, Claude | Telefon testleri F33 (T578–T583), `docs/PHONE_TESTS.md` | Bu gece |

## 3. Yükleme anahtarı

**Play App Signing:** Google Play, oyuncuların telefonuna giden APK'ları **kendi tuttuğu uygulama imzalama anahtarıyla** imzalar; o anahtarı Play ilk yüklemede kendisi üretir ve saklar. Sizin anahtarınız yalnız **yükleme anahtarı**: Play'e "bu paketi gerçekten hesabın sahibi yükledi" der. Yükleme anahtarı kaybolursa oyun kaybolmaz: Play Console → **Test and release → App integrity → App signing**'den yeni yükleme anahtarı istenir (Google'ın onayı birkaç gün sürer). Yine de yedekleyin.

### 3.1 Anahtarı yapmak (yapıldı)

Kendi PowerShell pencerenizde (Unity'nin kendi `keytool`'u; başka Java kurmaya gerek yok):

```
& "C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe" -genkeypair -v -storetype PKCS12 -keystore D:\puzzle-cache\keys\museumpuzzle-upload.keystore -alias upload -keyalg RSA -keysize 2048 -validity 10000
```

`keytool` parolayı ve adınızı, kuruluşunuzu, şehrinizi sorar. Parola en az 6 karakter. PKCS12 anahtar deposunda anahtarın parolası deponunkiyle aynıdır (`keytool` ikinciyi sormaz ya da yok sayar). 10.000 gün ≈ 27 yıl; Play, 22 Ekim 2033'ten sonra biten bir anahtar ister. 28 Ekim'de yapılan anahtar: RSA 2048, `upload` takma adı, 15 Mart 2054'e kadar geçerli; sertifikanın SHA-256 parmak izi `6E:C1:8A:78:…:D6:7F:7A`. Parmak izi gizli değildir; Play Console ilk yüklemeden sonra **App integrity**'de aynı parmak izini "Upload key certificate" olarak gösterir.

### 3.2 Yedeklemek

- `D:\puzzle-cache\keys\museumpuzzle-upload.keystore` dosyasını en az iki yere kopyalayın: bir USB bellek ve bir bulut klasörü ya da parola yöneticisinin dosya eki.
- Parolayı bir parola yöneticisine yazın. Dosya parolasız işe yaramaz; parola dosyasız da.
- Dosyayı ve parolayı depoya, e-postaya ya da sohbete koymayın. Depo bu dosyayı hiçbir zaman içermez (`D:` sürücüsünde, deponun dışında).

### 3.3 Ortam değişkenleri (yapıldı)

Paket derlemesi parolayı bir dosyadan değil, sizin Windows kullanıcınızın iki ortam değişkeninden okur. Kendi PowerShell pencerenizde, parolayı siz yazarak:

```
[Environment]::SetEnvironmentVariable('PUZZLE_UPLOAD_STORE_PASS', '<parola>', 'User')
[Environment]::SetEnvironmentVariable('PUZZLE_UPLOAD_KEY_PASS', '<parola>', 'User')
```

İkisine de aynı parola (PKCS12, 3.1). Bu biçimde yazılan satır PowerShell'in geçmiş dosyasına düşer (`%APPDATA%\Microsoft\Windows\PowerShell\PSReadLine\ConsoleHost_history.txt`); o dosyadan bu iki satırı silin. Geçmişe hiç düşmeyen yol, parolayı gizli sordurmak:

```
$p = Read-Host 'Yükleme anahtarının parolası' -AsSecureString
$d = [Net.NetworkCredential]::new('', $p).Password
[Environment]::SetEnvironmentVariable('PUZZLE_UPLOAD_STORE_PASS', $d, 'User')
[Environment]::SetEnvironmentVariable('PUZZLE_UPLOAD_KEY_PASS', $d, 'User')
Remove-Variable p, d
```

İsteğe bağlı üçüncü değişken `PUZZLE_UPLOAD_KEYSTORE`, anahtar deposu başka bir yerdeyse onun yolunu söyler. Değişkenler yeni açılan pencerelere geçer; derleme, değişkeni koymadan önce açılmış bir pencereden başlatılsa da onları Windows'un kullanıcı kaydından okur. Derleme parolanın değerini hiçbir yere yazmaz: kayıtta yalnız "set" ya da "not set" görünür. **Parolalar Unity'ye hiç verilmez:** Unity paketi her zaman kendi hata ayıklama anahtarıyla derler; derleme sonra o imzayı paketten çıkarır ve paketi Java'nın `jarsigner` aracıyla (Android'in belgelerinin paket için gösterdiği araç) sizin anahtarınızla imzalar. Parolalar yalnız `jarsigner`'ın kendi ortamına gider (`-storepass:env`), komut satırına bile yazılmaz. 28 Ekim'deki ilk denemede parolalar Unity'nin anahtar ayarlarına verilmişti: Unity onları derleme klasörünün (`unity/Library/Bee`, aslında `D:\puzzle-cache\unity-Library-Bee`) sekiz dosyasına düz metin olarak yazdı (Gradle projesinin `launcher/build.gradle`'ı, Unity'nin derleme grafiği ve kayıtları). O dosyalar temizlendi ya da silindi ve o yol bırakıldı.

## 4. Paketi derlemek

Unity Editor kapalıyken (aynı anda tek Unity), depo klasöründe:

```
"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" -batchmode -quit -projectPath C:\Users\arcan\puzzle\unity -executeMethod Puzzle.Client.EditorTools.PlayBundle.Run -logFile out\unity\play-bundle.log
```

Kod: `unity/Assets/Editor/PlayBundle.cs`. Süre ≈15–20 dakika (28 Ekim: 18 dakika). Derleme sırayla:

1. Editor'ın türettiği dosyaları günceller (test APK'sındaki gibi).
2. Ekonomi ayarının mağaza derlemesi olduğuna bakar: satın alma `"store"` değilse durur.
3. versionCode'u yükleme kaydından alır (6. bölüm), anahtarı seçer (3).
4. Unity'nin ayarlarını **yalnız bu derleme için** değiştirir (App Bundle açık, Split Application Binary açık, versionCode, Unity'nin hata ayıklama anahtarı) ve derleme bitince, başarılı da olsa başarısız da olsa, eski hâllerine döndürür. `unity/ProjectSettings/ProjectSettings.asset` değişmez; anahtar ve parola oraya hiç girmez.
5. Anahtar varsa paketi yeniden imzalar: hata ayıklama anahtarının imza dosyaları `META-INF`'ten çıkar, `jarsigner` imzalar (SHA-256 ile RSA) ve kendi denetimiyle doğrular; `META-INF`'te yalnız `MANIFEST.MF`, `UPLOAD.SF` ve `UPLOAD.RSA` kalmalı.
6. Paketi okur, Google'ın bundletool'uyla (sürüm 1.18.3, `D:\puzzle-cache\tools\bundletool-all-1.18.3.jar`, Unity'nin kendi Java'sıyla) Play'in yapacağı APK'ları yapar ve denetler:
   - Temel APK'nın paket kimliği, versionCode'u ve versionName'i.
   - Test APK'sının bütün denetimleri Android'in kendi aracıyla (`aapt`): izinler tam beş; Google'ın kayıt arka ucu (`CctBackendFactory`) yok; Play Games açılışta başlamıyor (`PlayGamesInitProvider` yok); Play Asset Delivery'nin ön plan servisi (`ExtractionForegroundService`) yok; yedek açık ve iki kural dosyası adlı.
   - Paketteki her yerel kütüphane (`.so`) 16 KB bellek sayfasında yüklenebiliyor: Play, Android 15 ve sonrasını hedefleyen uygulamalarda bunu istiyor. 28 Ekim'de altı kütüphane (`libunity`, `libil2cpp`, `libmain`, `libgame`, `libc++_shared`, `libswappywrapper`), her yüklenebilir parçası 16 KB hizalı. Kütüphaneler APK'larda sıkıştırılmış durur ve kurulumda açılır (`extractNativeLibs`), bundletool'un ayarı da 16 KB (`PAGE_ALIGNMENT_16K`); `zipalign -c -P 16` temel ABI parçasını geçiriyor.
   - Temel modülün indirmesi 200 MB'ın altında (bundletool `get-size total`; sınır 200 milyon bayt olarak okunuyor, ikisinin sıkısı).
7. Son satır:
   - `Play bundle: succeeded, signed with the upload key …`: yüklenebilir. Hemen altındaki satır, yükleme kabul edilince kayda eklenecek satırı söyler.
   - `Play bundle: succeeded, but signed with Unity's DEBUG key, so it is NOT UPLOADABLE …`: anahtar deposu ya da değişkenlerden biri yok; nedeni satırda. Dosya adı `…-debug.aab`. Play bu paketi reddeder; yalnız denetim ve telefonda deneme içindir.
   - `Play bundle: …` ile başlayan hata satırı: neyin durdurduğu.

Çıkanlar `D:\puzzle-cache\play\` klasöründe:

| Dosya | Ne için |
|---|---|
| `museumpuzzle-vc1-upload.aab` | Play Console'a yüklenen paket |
| `museumpuzzle-vc1-upload.apks` | Aynı paketten bundletool'un her telefon türü için yaptığı APK seti, bundletool'un hata ayıklama anahtarıyla imzalı: telefona kablo ile kurmak için (`bundletool install-apks --apks=…`, `docs/PHONE_TESTS.md` F33). Play'e yüklenmez |

**Telefonda dikkat:** kabloyla kurulan test APK'sı ve `.apks` hata ayıklama anahtarıyla imzalı; Play'den gelen ise Google'ın anahtarıyla. Android imzası başka olan uygulamayı üstüne kurmaz: telefonunuzda test APK'sı varken kapalı testten kurmak için önce onu kaldırın (`adb uninstall com.saturnwave.museumpuzzle` ya da Ayarlar → Uygulamalar). Telefondaki ilerleme yalnız bulut kaydında kalır (Bulut kaydı açıksa).

## 5. Play Console

### 5.1 Uygulamayı kurma görevleri

Play Console → Museum Puzzle → **Dashboard → Set up your app**. Kapalı test sürümü incelemeye ancak bunlar bitince gider; her biri bir form:

| Görev | Not |
|---|---|
| App access | Bütün oyun girişsiz açılıyor: "All functionality is available without special access" |
| Ads | Bu paket gerçek reklam göstermiyor: **No**. LevelPlay gelince **Yes** olarak değişmeli |
| Content rating | IARC anketi; oyun şiddetsiz bir bulmaca, uygulama içi satın alma var |
| Target audience | Yaş grubu; çocukları hedeflemiyorsa 13+ seçmek Families kurallarından kaçınır (sizin kararınız) |
| Data safety | `docs/GAME_DESIGN.md` §20 ve `docs/research/privacy-v1.md`: oyun kimliği rastgele, oyun istatistikleri izinle, satın almalar Google Play'den; reklam kimliği yok |
| Privacy policy | Oyunların ortak sitesindeki gizlilik sayfasının adresi (games-site) |
| Store listing | Kısa ve uzun açıklama, 512×512 simge, 1024×500 tanıtım görseli, en az iki telefon ekran görüntüsü |

### 5.2 Paketi yüklemek

1. **Test and release → Testing → Closed testing**. Hazır gelen kanal (ör. "Closed testing - Alpha") → **Manage track**; yoksa **Create track**.
2. **Testers** sekmesi: test listesi (e-postalar ya da bir Google Grubu). Öteki üç oyunun ortak listesi ("SketchPaint testers", 30 adres) seçilebilir. **Countries / regions**: test edilecek ülkeler.
3. **Releases → Create new release**.
4. İlk yüklemede Play imzalamayı sorar: **"Use Google-generated key"** (önerilen). Yüklediğiniz paketin anahtarı kendiliğinden yükleme anahtarı olarak kaydedilir.
5. **App bundles → Upload**: `D:\puzzle-cache\play\museumpuzzle-vc1-upload.aab`. Play paketi işler (birkaç dakika). Sürüm adı kendiliğinden "1 (1.0)".
6. **Release notes**: `<tr-TR>` ve `<en-US>` için birkaç satır (ör. "İlk kapalı test sürümü").
7. **Next**. Play'in uyarıları:
   - "This App Bundle contains native code, and you've not uploaded debug symbols": yalnız uyarı; çökme raporlarındaki yerel satırlar adsız kalır. İstenirse sonraki pakette Unity'nin sembol dosyası da üretilir (bize söyleyin).
   - Hata çıkarsa (imza, versionCode, izin) bize metniyle yazın.
8. **Save → Go to overview → Send changes for review**. Kapalı test incelemesi genelde birkaç saat, ilk sürümde birkaç gün sürebilir.
9. Onaydan sonra kanalın **Testers** sekmesindeki **"Join on the web"** bağlantısı test kullanıcılarına gönderilir. **14 günlük sayım**, en az 12 kullanıcı teste katılıp (opt-in) kalınca başlar; Play Console → Dashboard bunu gösterir.

**Satın almalar:** ürünler (`com.saturnwave.museumpuzzle.coins.pouch`, `…coins.bag`, `…coins.chest`, `…coins.treasure`, `…starter.apprentice_kit`) Play Console'da ancak `BILLING` izinli bir paket yüklendikten sonra açılabilir: bu yüklemeden sonra **Monetize with Play → Products → In-app products**. Test kullanıcılarının gerçek para ödemeden satın alabilmesi için hesaplarını **Settings → License testing**'e ekleyin. Ürünler yokken oyunun mağazası "Mağaza şu anda açılamıyor" der (T470).

## 6. versionCode ve sonraki yüklemeler

Play bir versionCode'u **bir kez** alır, sonra yalnız daha büyüğünü. Kod, Play Console dosyayı kabul ettiği anda harcanır; sürüm sonra silinse bile.

- Kayıt: `release/google-play-uploads.txt` (depoda). Her yükleme bir satır: `versionCode versionName gün kanal`, ör. `1 1.0 2026-10-28 closed`. Kanal: `internal`, `closed`, `open` ya da `production`.
- Derleme versionCode'u kendisi seçer: **kayıttaki son satırın bir fazlası**; kayıt boşken 1. Kimse elle kod yazmaz; kayıttaki kodlar satır satır artmıyorsa ya da bir satır bu biçimde değilse derleme durur (`unity/Assets/Editor/PlayUploads.cs`, testleri `PlayUploadsTests`).
- Yükleme kabul edilince satır eklenir ve depoya işlenir (Claude yapar; derlemenin kaydındaki satırı kopyalamak yeter). Eklenmeden yeniden derlenen paket aynı kodu alır: henüz yüklenmemiş bir paketi yeniden derlemek için doğru olan bu. Satır unutulursa Play ikinci yüklemeyi "Version code 1 has already been used" diye reddeder; zararsız: satır eklenir, paket yeniden derlenir.
- **Bir sonraki yükleme:** kayıtta `1 …` satırı varken derleme **versionCode 2** üretir.
- versionName `1.0` kalıyor: Unity'nin `bundleVersion`'ı; giriş sayfası "Sürüm 1.0" diye gösterir, bulut kaydı ve deneme kayıtları da bu sürümü yazar. Değiştirmek bir karar (ör. `1.0.1`): `unity/ProjectSettings` → `bundleVersion`; oyunun metinleri kendiliğinden yeni sürümü gösterir.
- Test APK'sı (`AndroidBuild.Run`) versionCode 1'de, hata ayıklama anahtarıyla, tek dosya kalır: indirme sayfası için.

## 7. Sorun giderme

| Belirti | Neden, çözüm |
|---|---|
| Son satır "NOT UPLOADABLE" | Satır neyin eksik olduğunu söyler: anahtar deposu yolu, `PUZZLE_UPLOAD_STORE_PASS` ya da `PUZZLE_UPLOAD_KEY_PASS`. 3.3'ü yapın, derlemeyi yeniden başlatın |
| "jarsigner ended with exit 1: … password was incorrect" ya da "Cannot recover key" | Değişkendeki parola anahtarınkiyle aynı değil; 3.3'ü doğru parolayla yeniden yapın. Paket `.rejected` adıyla kenara alınır, yüklenmez |
| Bir denetim durdurdu | Paket `museumpuzzle-vc…aab.rejected` adıyla kenara alınır (Play Console'un dosya seçicisi onu göstermez); hata satırı nedenini söyler |
| "no bundletool at D:/puzzle-cache/tools/…" | `bundletool-all-1.18.3.jar`'ı github.com/google/bundletool/releases/tag/1.18.3'ten o klasöre indirin (32.520.401 bayt, SHA-256 `a099cfa1…028e29`) |
| "the base module downloads in up to …, beyond Google Play's 200 MB" | Temel modüle büyük bir şey girmiş (ör. ilk sahnenin doğrudan tuttuğu resimler). Bize yazın; büyük olan asset pack'e taşınır |
| "… is aligned to less than 16 KB" | Bir eklentinin yerel kütüphanesi eski; eklentinin yeni sürümü gerekir |
| Play: "You uploaded an APK or Android App Bundle that was signed in debug mode" | `…-debug.aab` yüklenmiş; `…-upload.aab`'yi yükleyin |
| Play: "Your Android App Bundle is signed with the wrong key" | Play'de kayıtlı yükleme anahtarından başka bir anahtar; doğru anahtar deposu yolunu (`PUZZLE_UPLOAD_KEYSTORE`) verin. Anahtar kaybolduysa App integrity'den yükleme anahtarı sıfırlama |
| Telefonda Play "App not installed" | Telefonda kabloyla kurulmuş kopya var (başka imza); onu kaldırın (4. bölüm, son paragraf) |
