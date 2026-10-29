# Google Play'e Yükleme (Kapalı Test)

**Tarih:** 2026-10-28. **Kimin için:** proje sahibi. **Ne:** oyunun Google Play'e giden ilk paketi, bir Android App Bundle (`.aab`), ve onu kapalı test kanalına yükleme. Sahibin kararı (2026-10-28): Android derlemesi şimdi kapalı teste gider; kişisel geliştirici hesabında üretime geçmeden önce **12 test kullanıcısı 14 gün** boyunca kapalı testte olmalı. Gerçek reklamlar (LevelPlay) bu 14 gün içinde gelecek; bu paket reklamları hâlâ 3 saniyelik canlandırma kartıyla gösterir. Claude hiçbir hesaba girmez, anahtarı ve parolaları görmez; anahtarı siz yaparsınız, yüklemeyi siz yaparsınız.

## 1. Bu paket ne

| Ne | Değer |
|---|---|
| Paket kimliği | `com.saturnwave.museumpuzzle` (Play Console'daki "Museum Puzzle" kaydı, 2026-10-27) |
| versionCode / versionName | 2 / 1.0 (29 Ekim). versionCode 1 / 1.0 (28 Ekim) Play'den kurulunca her açılışta kapanıyordu: 9. bölüm |
| Derleme türü | Mağaza derlemesi: gerçek parayla satın alma Google Play'den (`economy/default.json` → `build.realMoneyPurchases: "store"`), reklamlar canlandırma (`"simulated"`), test modu **yok**. Giriş sayfası "Sürüm 1.0" der ("test sürümü" demez). Bulut kaydı UGS'nin `production` ortamına gider |
| Hedef | Android 16 (API 36), en düşük Android 8.0 (API 26), yalnız 64-bit ARM (arm64-v8a), IL2CPP |
| Dosya | `D:/puzzle-cache/play/museumpuzzle-vc2-upload.aab` (depo dışında; depoya girmez): 331.463.587 bayt, SHA-256 `5bde01e4…7dba64`; 25 MB'lık fark yerel kütüphanelerin sembol tabloları (telefona inmez). 28 Ekim'deki versionCode 1: `museumpuzzle-vc1-upload.aab`, 306.397.933 bayt, SHA-256 `d6eb69ff…78eb9a` |
| Boyut | Paket dosyası 306,4 milyon bayt. Telefonun indirdiği (bundletool `get-size total`): temel modül 59,6 MB (Play'in sınırı 200 MB); temel modülle birlikte `UnityDataAssetPack` 305,1 MB, parçanın kendi payı ≈245,5 MB (aşağıda) |
| İzinler | Tam beş: `VIBRATE`, `INTERNET`, `ACCESS_NETWORK_STATE`, `com.android.vending.BILLING` ve Unity'nin `…DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION`'ı. Reklam kimliği (`AD_ID`) yok |
| İmza | Sizin yükleme anahtarınız (`upload`), 3. bölüm. Unity paketi kendi hata ayıklama anahtarıyla derler; derleme o imzayı çıkarıp paketi Java'nın `jarsigner`'ıyla sizin anahtarınızla yeniden imzalar |

**Neden iki parça:** test APK'sı 306 MB, Play ise paketin temel modülünün sıkıştırılmış indirmesini en çok 200 MB ile sınırlıyor. Büyük olan oyunun verisi (tablolar ve öteki resimler, sesler, Unity'nin sahne dosyaları; APK'da `assets/bin/Data`, sıkıştırılmış 272 MB); kod, Unity'nin kütüphaneleri ve ilk sahnenin dosyaları indirmede ≈60 MB. Paket derlenirken Unity'nin **Split Application Binary** ayarı açılır: Unity bu veriyi `UnityDataAssetPack` adlı bir **install-time asset pack**'e koyar. Play bu parçayı oyunla birlikte, aynı kurulumda indirir; oyuncu için tek uygulama, ayrı bir indirme yok. Ayar yalnız paketin derlemesinde açılır; test APK'sı bugünkü gibi tek dosya kalır.

**Play Asset Delivery'nin fazlası:** asset pack'li bir paket için Unity, Google'ın Play Asset Delivery kütüphanesini (`com.google.android.play:asset-delivery` 2.3.0) ekliyor. Kütüphane, kurulumdan **sonra** indirilen paketleri (fast-follow, on-demand) açmak için `dataSync` türünde bir ön plan servisi ve bu işi yürüten WorkManager'ı (androidx.work 2.9.1) getiriyor; onlarla dört izin geliyordu: servis için `FOREGROUND_SERVICE` ve `FOREGROUND_SERVICE_DATA_SYNC` (Play Console bunun için ayrı bir beyan ve video ister), WorkManager için `WAKE_LOCK` ve `RECEIVE_BOOT_COMPLETED`. İlk derlemenin denetimi bunu yakaladı ve paketi durdurdu. Oyunun tek parçası kurulumla gelir, sonradan bir şey indirilmez ya da açılmaz. Manifest adımı dört izni geri çekiyor; paket test APK'sıyla aynı beş izni istiyor. **Ön plan servisi manifestte kalmalı:** Unity'nin oynatıcısı açılışta kütüphaneden paketini soruyor, kütüphane de önce iki servisini (`AssetPackExtractionService`, `ExtractionForegroundService`) açıyor; manifestte olmayan bir servisi Android açmıyor ve oyun kapanıyor. versionCode 1'de servis çıkarılmıştı, oyun her açılışta kapandı (9. bölüm). versionCode 2'den beri manifest adımı servisi bırakıyor, yalnız türünü (`foregroundServiceType="dataSync"`) siliyor: manifestte hiçbir ön plan servis türü yok, Play Console beyan istemiyor (`unity/Assets/Editor/ManifestAssetDelivery.cs`). Servis yalnız Play sonradan indirdiği bir parçanın açılmasını istediğinde başlar; oyunun öyle bir parçası yok. WorkManager oyunla başlıyor ama ona iş verilmiyor, işsizken uyanık tutma ya da açılış izni gerekmiyor. Telefonda doğrulama: T579 (açılış, tablolar, `logcat`'te hata yok).

## 2. Sırayla yapılacaklar

| # | Kim | Ne | Durum |
|---|---|---|---|
| 1 | Sahip | Yükleme anahtarını yap (3.1) | Yapıldı, 2026-10-28 |
| 2 | Sahip | Anahtarı ve parolayı yedekle (3.2) | Yapıldı, 2026-10-28: iki kopya, özetleri aynı |
| 3 | Sahip | İki ortam değişkenini koy (3.3) | Yapıldı, 2026-10-28 |
| 4 | Claude | Paketi derle (4) | Yapıldı: `museumpuzzle-vc1-upload.aab` |
| 5 | Sahip | Play Console'un "uygulamanı kur" görevleri ve mağaza sayfası (5.1; cevaplar, metinler ve görseller 8. bölümde, sıra 8.1) | Yapıldı, 2026-10-28: uygulamayı sahip açtı; formları, mağaza sayfasını ve 12 çeviriyi Claude sahibin oturumu açık Chrome'undan girdi (8.8) |
| 6 | Sahip | Paketi kapalı teste yükle (5.2) | Yapıldı, 2026-10-28: sahip uzaktan yükledi; Play işledi: 1 (1.0), API 26+, hedef SDK 36, arm64-v8a, yeni kurulumda 305 MB |
| 7 | Claude | Yüklenen sürümü kayda yaz (6) | Yapıldı, 2026-10-28: `1 1.0 2026-10-28 closed` |
| 8 | Sahip, Claude | Telefon testleri F33 (T578–T583), `docs/PHONE_TESTS.md` | versionCode 1 yüklenmeden önce yapılmadı (9). versionCode 2: T578 geçti, T579'un açılış ve oyun kısmı geçti (2026-10-29); gerisi bekliyor |
| 9 | Claude | Mağaza sayfası: 13 dilde metin, simge, tanıtım görseli, telefon ve tablet ekran görüntüleri, formların cevapları, ürünlerin listesi (8) | Yapıldı, 2026-10-28 |
| 10 | Sahip | Ürünleri aç (8.7): paket yüklendikten ve ödeme profili kurulduktan sonra | Paket yüklendi; ödeme profili bekleniyor |
| 11 | Claude | Değişiklikleri incelemeye gönder (5.2, 8. adım) | Gönderildi, 2026-10-28: 28 değişiklik. Google 29 Ekim'de onayladı; test bağlantısı açık |
| 12 | Claude | versionCode 1'in açılış çökmesi: neden, düzeltme, versionCode 2 (9) | Yapıldı, 2026-10-29: `museumpuzzle-vc2-upload.aab`, telefonda açılıyor |
| 13 | Sahip | versionCode 2'yi kapalı teste yükle (Create new release; sahibin kararı, 2026-10-29: tek seferlik istisna, 10'un 6. adımı atlanıyor) | Yapıldı, 2026-10-29: Play işledi, 2 (1.0) |
| 14 | Claude | Sürüm notları, incelemeye gönder, kayda yaz (`2 1.0 2026-10-29 closed`); onaydan hemen sonra Play'den kurup dene (T583) | Gönderildi ve kayda yazıldı, 2026-10-29; Play'den deneme onaydan sonra |

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
4. Unity'nin ayarlarını **yalnız bu derleme için** değiştirir (App Bundle açık, Split Application Binary açık, versionCode, Unity'nin hata ayıklama anahtarı, yerel kütüphanelerin sembol tabloları pakete: `DebugSymbols` = `SymbolTable`, `IncludeInBundle`; versionCode 2'den beri) ve derleme bitince, başarılı da olsa başarısız da olsa, eski hâllerine döndürür. `unity/ProjectSettings/ProjectSettings.asset` değişmez; anahtar ve parola oraya hiç girmez.
5. Anahtar varsa paketi yeniden imzalar: hata ayıklama anahtarının imza dosyaları `META-INF`'ten çıkar, `jarsigner` imzalar (SHA-256 ile RSA) ve kendi denetimiyle doğrular; `META-INF`'te yalnız `MANIFEST.MF`, `UPLOAD.SF` ve `UPLOAD.RSA` kalmalı.
6. Paketi okur, Google'ın bundletool'uyla (sürüm 1.18.3, `D:\puzzle-cache\tools\bundletool-all-1.18.3.jar`, Unity'nin kendi Java'sıyla) Play'in yapacağı APK'ları yapar ve denetler:
   - Temel APK'nın paket kimliği, versionCode'u ve versionName'i.
   - Test APK'sının denetimleri Android'in kendi aracıyla (`aapt`): izinler tam beş; Google'ın kayıt arka ucu (`CctBackendFactory`) yok; Play Games açılışta başlamıyor (`PlayGamesInitProvider` yok); yedek açık ve iki kural dosyası adlı. Play Asset Delivery'de ikisi ayrılır: paketin temel APK'sında kütüphanenin açılışta açtığı iki servis (`AssetPackExtractionService`, `ExtractionForegroundService`) tanımlı olmalı ve hiçbir ön plan servis türü olmamalı; test APK'sında kütüphaneden hiçbir şey olmamalı.
   - Paketteki sembol tabloları (`BUNDLE-METADATA/com.android.tools.build.debugsymbols/`) kayda yazılır; yoksa uyarı (durdurmaz).
   - Paketteki her yerel kütüphane (`.so`) 16 KB bellek sayfasında yüklenebiliyor: Play, Android 15 ve sonrasını hedefleyen uygulamalarda bunu istiyor. 28 Ekim'de altı kütüphane (`libunity`, `libil2cpp`, `libmain`, `libgame`, `libc++_shared`, `libswappywrapper`), her yüklenebilir parçası 16 KB hizalı. Kütüphaneler APK'larda sıkıştırılmış durur ve kurulumda açılır (`extractNativeLibs`), bundletool'un ayarı da 16 KB (`PAGE_ALIGNMENT_16K`); `zipalign -c -P 16` temel ABI parçasını geçiriyor.
   - Temel modülün indirmesi 200 MB'ın altında (bundletool `get-size total`; sınır 200 milyon bayt olarak okunuyor, ikisinin sıkısı).
7. Son satır:
   - `Play bundle: succeeded, signed with the upload key …`: yüklenebilir. Hemen altındaki satır, yükleme kabul edilince kayda eklenecek satırı söyler.
   - `Play bundle: succeeded, but signed with Unity's DEBUG key, so it is NOT UPLOADABLE …`: anahtar deposu ya da değişkenlerden biri yok; nedeni satırda. Dosya adı `…-debug.aab`. Play bu paketi reddeder; yalnız denetim ve telefonda deneme içindir.
   - `Play bundle: …` ile başlayan hata satırı: neyin durdurduğu.

Çıkanlar `D:\puzzle-cache\play\` klasöründe:

| Dosya | Ne için |
|---|---|
| `museumpuzzle-vc<N>-upload.aab` | Play Console'a yüklenen paket (N = versionCode; 29 Ekim'den beri 2) |
| `museumpuzzle-vc<N>-upload.apks` | Aynı paketten bundletool'un her telefon türü için yaptığı APK seti, bundletool'un hata ayıklama anahtarıyla imzalı: telefona kablo ile kurmak için (`bundletool install-apks --apks=…`, `docs/PHONE_TESTS.md` F33). Play'e yüklenmez. **Her yüklemeden önce telefonda açılır** (T578, T579): Play'in kuracağı parçalarla aynı parçalar, versionCode 1'i kapatan hata ancak böyle görünür (9) |

**Telefonda dikkat:** kabloyla kurulan test APK'sı ve `.apks` hata ayıklama anahtarıyla imzalı; Play'den gelen ise Google'ın anahtarıyla. Android imzası başka olan uygulamayı üstüne kurmaz: telefonunuzda test APK'sı varken kapalı testten kurmak için önce onu kaldırın (`adb uninstall com.saturnwave.museumpuzzle` ya da Ayarlar → Uygulamalar). Telefondaki ilerleme yalnız bulut kaydında kalır (Bulut kaydı açıksa).

## 5. Play Console

### 5.1 Uygulamayı kurma görevleri

Play Console → Museum Puzzle → **Dashboard → Set up your app**. Kapalı test sürümü incelemeye ancak bunlar bitince gider; her biri bir form:

| Görev | Not |
|---|---|
| App access | Bütün oyun girişsiz açılıyor: "All functionality is available without special access" (8.5) |
| Ads | Bu paket gerçek reklam göstermiyor: **No**. LevelPlay'li paketten önce **Yes** olmalı (8.5) |
| Content rating | IARC anketi, cevaplar 8.5'te: şiddet yok, uygulama içi satın alma var, rastgele ücretli ürün yok |
| Target audience | 13–15, 16–17, 18+ (sahibin kararı, 2026-10-28; politika 13 yaş altına yönelik değil diyor); 8.5 |
| Data safety | Yayımlanan politika ve sunucu planının §6.3'ü; satır satır 8.6'da |
| Privacy policy | `https://saturn-wave-apps.vercel.app/games/museum-puzzle/privacy/en/` (2026-10-28'de yayımlandı) |
| Store listing | Ad, kısa ve uzun açıklama (13 dil), 512×512 simge, 1024×500 tanıtım görseli, telefon ve tablet ekran görüntüleri: 8.2, 8.3 |

### 5.2 Paketi yüklemek

1. **Test and release → Testing → Closed testing**. Hazır gelen kanal (ör. "Closed testing - Alpha") → **Manage track**; yoksa **Create track**.
2. **Testers** sekmesi: test listesi (e-postalar ya da bir Google Grubu). Öteki üç oyunun ortak listesi ("SketchPaint testers", 30 adres) seçilebilir. **Countries / regions**: test edilecek ülkeler.
3. **Releases → Create new release**.
4. İlk yüklemede Play imzalamayı sorar: **"Use Google-generated key"** (önerilen). Yüklediğiniz paketin anahtarı kendiliğinden yükleme anahtarı olarak kaydedilir.
5. **App bundles → Upload**: `D:\puzzle-cache\play\museumpuzzle-vc<N>-upload.aab` (ilk yükleme vc1; 29 Ekim'den beri vc2). Play paketi işler (birkaç dakika). Sürüm adı kendiliğinden "N (1.0)".
6. **Release notes**: `<tr-TR>` ve `<en-US>` için birkaç satır (ör. "İlk kapalı test sürümü").
7. **Next**. Play'in uyarıları:
   - "This App Bundle contains native code, and you've not uploaded debug symbols": yalnız uyarı; çökme raporlarındaki yerel satırlar adsız kalır. versionCode 1'de çıktı; versionCode 2'den beri sembol tabloları paketin içinde, uyarı çıkmamalı.
   - "There is no deobfuscation file associated with this App Bundle": yalnız uyarı; R8/ProGuard eşleme dosyası yok, Java tarafındaki çökme satırları olduğu gibi okunur.
   - İlk yüklemede (2026-10-28) yalnız bu iki uyarı çıktı, hata çıkmadı.
   - Hata çıkarsa (imza, versionCode, izin) bize metniyle yazın.
8. **Save → Go to overview → Send changes for review**. Kapalı test incelemesi genelde birkaç saat, ilk sürümde birkaç gün sürebilir.
9. Onaydan sonra kanalın **Testers** sekmesindeki **"Join on the web"** bağlantısı test kullanıcılarına gönderilir. **14 günlük sayım**, en az 12 kullanıcı teste katılıp (opt-in) kalınca başlar; Play Console → Dashboard bunu gösterir.

**Satın almalar:** ürünler (`com.saturnwave.museumpuzzle.coins.pouch`, `…coins.bag`, `…coins.chest`, `…coins.treasure`, `…starter.apprentice_kit`) Play Console'da ancak `BILLING` izinli bir paket yüklendikten sonra açılabilir: bu yüklemeden sonra **Monetize with Play → Products → One-time products** (eski adı In-app products); ürünlerin adları, açıklamaları ve fiyatları 8.7'de. Test kullanıcılarının gerçek para ödemeden satın alabilmesi için hesaplarını **Settings → License testing**'e ekleyin. Ürünler yokken oyunun mağazası "Mağaza şu anda açılamıyor" der (T470).

## 6. versionCode ve sonraki yüklemeler

Play bir versionCode'u **bir kez** alır, sonra yalnız daha büyüğünü. Kod, Play Console dosyayı kabul ettiği anda harcanır; sürüm sonra silinse bile.

- Kayıt: `release/google-play-uploads.txt` (depoda). Her yükleme bir satır: `versionCode versionName gün kanal`, ör. `1 1.0 2026-10-28 closed`. Kanal: `internal`, `closed`, `open` ya da `production`.
- Derleme versionCode'u kendisi seçer: **kayıttaki son satırın bir fazlası**; kayıt boşken 1. Kimse elle kod yazmaz; kayıttaki kodlar satır satır artmıyorsa ya da bir satır bu biçimde değilse derleme durur (`unity/Assets/Editor/PlayUploads.cs`, testleri `PlayUploadsTests`).
- Yükleme kabul edilince satır eklenir ve depoya işlenir (Claude yapar; derlemenin kaydındaki satırı kopyalamak yeter). Eklenmeden yeniden derlenen paket aynı kodu alır: henüz yüklenmemiş bir paketi yeniden derlemek için doğru olan bu. Satır unutulursa Play ikinci yüklemeyi "Version code 1 has already been used" diye reddeder; zararsız: satır eklenir, paket yeniden derlenir.
- **Bir sonraki yükleme:** kayıtta `2 1.0 2026-10-29 closed` var (9. bölüm); sonraki derleme **versionCode 3** üretir.
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

## 8. Mağaza sayfası, formlar ve ürünler

**Hazırlayan:** Claude, 2026-10-28. Bölüm sahibin elle girmesi için yazıldı; aynı gün cevapları, metinleri ve görselleri Claude sahibin oturumu açık Chrome'undan Play Console'a girdi (hiçbir hesaba kendisi girmedi; neyin nasıl girildiği 8.8'de), paketi sahip yükledi. Form ve alan adları Play Console'daki gibi İngilizce. **(yargı)** işaretli cevaplar bir karardır, gerekçesi yanında: başka karar verirseniz bize söyleyin; metinler, bu belge ve gerekiyorsa sitedeki politika birlikte değişir.

### 8.1 Play Console'da sıra

| # | Nerede | Ne | Bölüm |
|---|---|---|---|
| 1 | Grow users → Store presence → Store settings | Kategori, etiketler, iletişim | 8.4 |
| 2 | Policy and programs → App content (ya da Dashboard → Set up your app) | Privacy policy, App access, Ads, Content rating, Target audience, Data safety, öteki beyanlar | 8.5, 8.6 |
| 3 | Grow users → Store presence → Main store listing | Varsayılan dil **English (United States) – en-US**: ad, kısa ve uzun açıklama, simge, tanıtım görseli, telefon ve tablet ekran görüntüleri | 8.2, 8.3 |
| 4 | Aynı sayfa → Manage translations → Add your own translations | Öteki 12 dil: ad ve açıklamalar; **Türkçe – tr-TR**'ye Türkçe görseller de (öteki diller İngilizce görselleri gösterir) | 8.2, 8.3 |
| 5 | Test and release → Testing → Closed testing | Paketin yüklenmesi | 5.2 |
| 6 | Setup → Payments profile, sonra Monetize with Play → Products → One-time products | Ödeme profili (yoksa ücretli ürün açılmaz), beş ürün | 8.7 |

Menü adları Play Console'un sürümüne göre biraz değişebilir; Dashboard'daki "Set up your app" görevleri aynı formlara götürür.

### 8.2 Metinler (13 dil)

- **Kaynak:** `release/store-listing/<locale>.json` (depoda): `title` (App name), `shortDescription`, `fullDescription`; `review` (`source`: elle yazıldı; `machine`: makine çevirisi taslağı) ve `reviewNote`. **Kopyalamak için:** `D:\puzzle-cache\play\listing\text\<locale>.txt`, her alan başlığı ve uzunluğuyla (`py tools/store-listing/store-assets.py text` yeniden yazar).
- İngilizce ve Türkçe elle yazıldı. Öteki 11 dil, oyunun kelime tablolarındaki gibi makine çevirisi taslağı: oyunun kendi sözcükleriyle (taş, fırça, palet, altın, can, ayarların adları) ve tabloların her dildeki adlarıyla. Kapalı test için yeter; yayından önce ana dili olan biri okumalı. Play Console'un kendi makine çevirisini seçmeyin.
- Her söz oyunun bugün yaptığı bir şey (`docs/GAME_DESIGN.md`, `economy/default.json`, `words/*.json`, yayımlanan politika). Bilerek yazılmayanlar: reklam (bu paket göstermiyor), "çevrimdışı oyun" (yalnız politikadaki "bağlantı yokken bölümler aynen oynanır, kayıt sonra eşitlenir"), "ücretsiz", fiyatlar, sıralama sözleri, başka oyunların adları, ressamın yaşı (hikâye cümlesi onay bekliyor, GAME_DESIGN §18).
- **Ad:** oyunun giriş sayfasındaki adı (`EntryTitle`). "Museum Puzzle" bir çalışma adı; Play'de ad sonra değişebilir, paket kimliği değişmez.
- **(yargı)** Uzun açıklamanın sondan ikinci paragrafı oyundaki künyeyi gösterir (Ayarlar > Destek > Künye): tanıtım görselindeki ressamın başı Einar Rig'den (CC BY 4.0) yapıldı; lisans, atfın "ortama uygun makul bir biçimde" yapılmasını ister ve mağaza sayfasında görselin yanında atıf yeri yok. Satırı istemezseniz ressam tanıtım görselinden çıkar ya da atıf oyun sitesine konur.

| Locale | Ad (30) | Kısa (80) | Uzun (4.000) | Durum |
|---|---|---|---|---|
| en-US | 13 | 79 | 3.172 | kaynak |
| tr-TR | 12 | 77 | 3.013 | kaynak |
| de-DE | 13 | 78 | 3.626 | makine |
| fr-FR | 15 | 77 | 3.753 | makine |
| es-ES | 15 | 69 | 3.504 | makine |
| pt-BR | 22 | 79 | 3.428 | makine |
| it-IT | 16 | 70 | 3.527 | makine |
| ru-RU | 13 | 75 | 3.330 | makine |
| ja-JP | 6 | 34 | 1.539 | makine |
| ko-KR | 6 | 37 | 1.767 | makine |
| zh-CN | 5 | 30 | 1.140 | makine |
| zh-TW | 5 | 30 | 1.141 | makine |
| id | 13 | 70 | 3.454 | makine |

**Denetim:** `StoreListingTests` (`dotnet test tests/Puzzle.Core.Tests -c Debug`): 13 dosya, başka dosya yok; sınırlar (Play tam ve yarım genişlikte her karakteri bir sayar); ad, oyunun o dildeki adı; emoji, sembol ve büyük harfle yazılmış söz yok (yüzyılın Roma rakamı hariç); fiyat, para birimi, "ücretsiz", "en iyi", "#1" ve indirim sözleri her dilde yok; başka oyun adı yok; `review` İngilizce ve Türkçede `source`, ötekilerde `machine`.

**Sürüm notları** (5.2'nin 6. adımı):

```
<en-US>
First closed test of Museum Puzzle. Thank you for playing! Tell us what you think: saturnwave.apps@gmail.com
</en-US>
<tr-TR>
Müze Yapbozu'nun ilk kapalı testi. Oynadığın için teşekkürler! Görüşlerini saturnwave.apps@gmail.com adresine yaz.
</tr-TR>
```

### 8.3 Görseller

Klasör `D:\puzzle-cache\play\listing\` (depo dışında). Hepsi Play'in kurallarından geçti (`py tools/store-listing/store-assets.py check`). Kurallar 2026-10-28'de Play Console Help'ten okundu: simge 512×512 32-bit PNG, en çok 1.024 KB; tanıtım görseli 1024×500 JPEG ya da saydamlıksız 24-bit PNG; ekran görüntüsü JPEG ya da 24-bit PNG, her kenarı 320–3.840 px, uzun kenarı kısanın en çok iki katı, türü başına 2–8 tane; Play'in öne çıkarmaları için oyunlarda en az üç 9:16 dikey görüntü (en az 1080×1920); tablette 16:9, 1.080–7.680 px.

| Dosya | Play'deki yeri | Piksel | Bayt |
|---|---|---|---|
| `icon-512.png` | App icon (bütün diller) | 512×512, 32-bit | 235.246 |
| `feature-graphic-en-US.png` | Feature graphic, en-US (öteki 11 dil de bunu gösterir) | 1024×500, 24-bit | 574.140 |
| `feature-graphic-tr-TR.png` | Feature graphic, tr-TR | 1024×500, 24-bit | 572.560 |
| `phone-en-US\01…08` | Phone screenshots, en-US (öteki 11 dil de) | 1080×1920 | 0,85–2,24 MB |
| `phone-tr-TR\01…08` | Phone screenshots, tr-TR | 1080×1920 | 0,85–2,24 MB |
| `tablet-en-US\01…06` | 7-inch **ve** 10-inch tablet screenshots, en-US (aynı altı dosya iki yere) | 2560×1440 | 1,26–2,77 MB |
| `tablet-tr-TR\01…06` | Aynısı, tr-TR | 2560×1440 | 1,26–2,77 MB |

- **Simge:** `art/icon/app-icon-1024.png` küçültüldü (krem zeminde palet ve fırça, sahibin seçimi); yeniden çizilmedi.
- **Tanıtım görseli:** oyunun duvarı (damask), yaldızlı çerçevesi, tablo ışığı, taşları, Büyük Dalga (Hokusai, kamu malı), giriş sayfasının yaldızlı başlığı (Young Serif, OFL) ve alt yazısı (Gabarito, OFL), sahibin onayladığı ressamın sevinç pozu (`art/avatar`). Tablodan bir yapboz parçası kalkmış; yerinde oyundaki gibi krem boşluk. Elle çizilmiş insan, maskot yok. Başlık ve alt yazı kenarlardan uzak (Play bazı yerlerde kırpar).
- **Telefon (dikey, 9:16), sıra:** 1 oyun (47. bölüm, yatay fırça bir sırayı boyuyor), 2 yapboz (parça yerine taşınıyor), 3 müzenin salonu (ressam, tablo 7/10), 4 koleksiyon (Post-empresyonizm odası), 5 yakından tablo (Van Gogh), 6 dünya haritası, 7 "Oda onarıldı!", 8 280. bölüm (sandıklar, vernik, başlangıç booster'ları tahtada).
- **Tablet (yatay, 16:9):** salon, yapboz, 280. bölüm, koleksiyon, harita, "Oda onarıldı!". Oyun tablette döndüğü için yeni kod gerekmedi. BoosterShots booster'ı iş başında yalnız ilk ekranda çiziyor; tablette onun yerine 280. bölüm.
- **Mağaza ekranı kullanılmadı (yargı):** Editor'da mağaza olmadığı için fiyatlar "…" görünüyor; ekranda "Reklam izle, 4 altın kazan" satırı da var, oysa form "reklam yok" diyor (8.5). İkisi birbirini yalanlamasın diye.
- **Yeniden üretmek** (ekranlar değişince; Unity kapalıyken, sırayla, her biri 2–3 dk; `Unity.exe` = `"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe"`):

```
Unity.exe -batchmode -quit -projectPath C:\Users\arcan\puzzle\unity -executeMethod Puzzle.Client.EditorTools.ScreenShots.Run -screens home,map,collection,viewer,jigsaw,restored,entrance -screenStates "96+94,99+97,100+100,279+458" -screenSizes 1080x1920,2560x1440 -jigsawMoments carried -language en -logFile out\unity\listing-shots-en.log
Unity.exe ... (aynısı, -language en olmadan: Türkçe) -logFile out\unity\listing-shots-tr.log
Unity.exe -batchmode -quit -projectPath C:\Users\arcan\puzzle\unity -executeMethod Puzzle.Client.EditorTools.BoosterShots.Run -boosterSizes 1080x1920,2560x1440 -language en -logFile out\unity\listing-boosters-en.log
Unity.exe ... (aynısı, -language en olmadan) -logFile out\unity\listing-boosters-tr.log
py tools/store-listing/store-assets.py all
```

### 8.4 Store settings

| Alan | Değer |
|---|---|
| App or game | Game |
| Category | Puzzle |
| Tags (en çok 5) | Match 3, Jigsaw puzzle, Tile-matching, Casual, Single player **(yargı)**: konsolun listesinden; birebir yoksa en yakını. "Offline" seçmeyin: satın alma ve bulut kaydı bağlantı ister |
| Email address | `saturnwave.apps@gmail.com` |
| Phone number | Boş (isteğe bağlı) |
| Website | `https://saturn-wave-apps.vercel.app/` |
| External marketing | Açık (varsayılan) |

### 8.5 App content

| Form | Cevap | Not |
|---|---|---|
| Privacy policy | `https://saturn-wave-apps.vercel.app/games/museum-puzzle/privacy/en/` | Türkçesi `/tr/`; ikisi de 2026-10-28 |
| App access | All functionality in my app is available without any access restrictions | Hesap, parola, davet yok; görünmez hesap kendiliğinden açılıyor |
| Ads | No, my app does not contain ads | **(yargı, sahibin kararı)** Reklam ağı yok; "+5 hamle için reklam" ve mağazadaki "Reklam izle" 3 saniyelik bir canlandırma kartı açıyor, kartta reklam yok. LevelPlay'li paket yüklenmeden **önce** "Yes" olmalı; politika da reklamdan önce güncellenir ("Changes"). İnceleyen "Reklam izle"yi sorarsa: bu test paketinde reklam gösterilmiyor |
| Advertising ID | Does your app use advertising ID? **No** | Paketin izinlerinde `AD_ID` yok; derleme denetliyor |
| Content rating | Aşağıdaki tablo | |
| Target audience and content | **13–15, 16–17, 18 and over**; 5 ve altı, 6–8, 9–12 işaretsiz | Sahibin kararı (politika: 13 yaş altına yönelik değil). Sonuç: oyun Families programına girmez ve onun kurallarına (aileye uygun reklam SDK'ları, çocuk verisi) bağlı olmaz; karşılığında mağaza sayfası çocuklara yönelik görünmemeli |
| Appeal to children (sorulursa) | No | **(yargı)** Konu sanat tarihi, metin yetişkine yazıldı; renkli taşlar ve ressam var ama çocuk dili, çocuk karakteri yok |
| News apps | No | |
| Government apps | No | |
| Financial features | My app doesn't provide any financial features | Oyun içi satın alma finansal özellik sayılmaz |
| Health | My app does not have any health features | |
| Data safety | 8.6 | |

**Content rating (IARC anketi):** e-posta `saturnwave.apps@gmail.com`, kategori **Game**.

| Konu | Cevap | Neden |
|---|---|---|
| Violence (şiddet, kan) | No | Taşlar patlar, kimse incinmez. Tablolarda asker ve tüfek var (ör. Washington Delaware'i Geçerken), çatışma ya da yara yok |
| Fear, horror | No | |
| Sexuality, nudity | No | 100 tablonun hepsine bakıldı: çıplaklık yok |
| Language, crude humour | No | |
| Controlled substances | **Alcohol and tobacco: depicted, not used or encouraged** **(yargı)** | 9. odadaki "Roma'da Bir Grup Danimarkalı Sanatçı" (Hansen) pipo içen adamları gösteriyor; natürmortlarda şarap şişesi ve kadehler (Cézanne, Heda), Moulin Rouge'da kadehli masa. Oyuncu kullanmıyor, özendirilmiyor. "No" da savunulabilir (sanat eserinin arka planı), ama eksik beyan derecenin sonradan düzeltilmesine yol açabilir; fazlası en çok bazı ülkelerde biraz daha yüksek bir derece (tahmin), hedef kitle zaten 13+ |
| Gambling (simulated or real) | No | |
| Random items for real money (loot box) | No | Hiçbir ürünün içi rastgele değil ve satın almadan önce görünüyor; günlük hediye ve tablo ödülü sıralı ve ücretsiz (`economy/default.json`) |
| In-app purchases of digital goods | Yes | Altın paketleri ve Çırak Çantası, Google Play'den |
| Users interact or exchange content | No | Sohbet, oyuncular arası içerik, arkadaş listesi yok; Google Play Games yalnız kaydı bağlamak için |
| Shares the user's location | No | |
| Unrestricted internet, web browser | No | |

Beklenen derece (tahmin): IARC 3+ / PEGI 3 / ESRB Everyone, "In-Game Purchases" notuyla; tütün ve alkol beyanı bazı ülkelerde derecenin bir basamak yükselmesine yol açabilir.

### 8.6 Data safety

Cevaplar yayımlanan politikaya (2026-10-28) ve sunucu planının §6.3'üne (sahibin 9. kararı) dayanıyor: politikada olmayan bir veri formda yok, formdaki her veri politikada var. Bütün cevaplar Android paketi için.

**Data collection and security**

| Soru | Cevap |
|---|---|
| Does your app collect or share any of the required user data types? | Yes |
| Is all of the user data collected by your app encrypted in transit? | Yes (Unity, Aptabase ve Google adresleri HTTPS) |
| Account creation | Kullanıcı hesap açabiliyor: **OAuth** (Google Play Games'e bağlanma) ve **Other** ("An anonymous player account is created automatically; it can be linked to Google Play Games.") **(yargı)**: oyuncu bir şey yapmadan açılan görünmez hesap da Unity kimliği, bulutta kaydı ve silme yolu olan bir hesap; "hesap yok" demek politikayla çelişir |
| Delete account URL | `https://saturn-wave-apps.vercel.app/games/museum-puzzle/privacy/en/` ("Deleting your data" bölümü) **(yargı)**: aşağıdaki nota bakın |
| Can users request that some or all of their data is deleted without deleting their account? | No (oyundaki "Verilerimi sil" hesabı ve kaydı birlikte siler; istatistik notları kimliksiz, ayrılamaz) |

**Silme sayfası notu:** Google, silme yolunun sayfada belirgin olmasını (ör. bir çapa bağlantısı) ve kullanıcıyı uygulamayı yeniden kurmaya göndermemesini istiyor. Sayfanın bölümleri çapasız ve e-postayla silme isteği oyunda görünen oyuncu kodunu istiyor; oyunu silmiş biri kodunu bilmeyebilir. Öneri (sitenin işi, bu işte yapılmadı): bölüme `#deleting-your-data` çapası ve kodu olmayan için başka bir yol (ör. Google Play sipariş numarasıyla istek); sonra URL çapalı verilir.

**Data types** (tabloda olmayan türlerin hepsi "No")

| Category › Type | Collected | Shared | Ephemeral | Required / optional | Purposes | Politikada |
|---|---|---|---|---|---|---|
| Location › Approximate location | Yes | No | No | Optional | Analytics | Aptabase, izinle: bağlantının IP'sinden ülke ve bölge, IP saklanmıyor ("Game statistics") |
| Personal info › User IDs | Yes | No | No | Required **(yargı)** | App functionality; Account management; Fraud prevention, security, and compliance | Unity oyuncu kimliği, Google Play Games'in bu oyundaki kimliği ("Cloud save…", "Google Play Games…") |
| Financial info › Purchase history | Yes | **Yes (yargı)** | No | Required | Toplama: App functionality; Analytics; Account management; Fraud prevention, security, and compliance. Paylaşma: Fraud prevention, security, and compliance | Satın alma kaydı ve doğrulaması, 36 aylık kimliksiz iz, Unity'nin satın alma kütüphanesi ("Purchases", "What Unity receives"); izinli notlarda alınan ürün |
| App activity › App interactions | Yes | No | No | Optional | Analytics | Aptabase notları, izinle ("Game statistics") |
| App activity › Other actions | Yes | No | No | Required **(yargı)** | App functionality | Bulut kaydındaki ilerleme ve cüzdan ("Cloud save…") |
| App info and performance › Diagnostics | Yes | **Yes (yargı)** | No | Required | Toplama: App functionality; Analytics. Paylaşma: Analytics; Fraud prevention, security, and compliance | Unity'nin giriş olayı ve satın alma olayı: model, sistem sürümü, dil ve bölge, depolama, açılış zamanı, oyunun sürümü ve kurulumu ("What Unity receives") |
| Device or other IDs | Yes | **Yes (yargı)** | No | Required | Toplama: App functionality; Analytics; Fraud prevention, security, and compliance. Paylaşma: Analytics; Fraud prevention, security, and compliance | Unity'nin kurulum, analiz ve oturum kimlikleri; reklam kimliği yok ("What Unity receives") |

"No" olanlar: Name, Email address, User payment info (kartı Google Play alır, oyun görmez), Precise location, Crash logs, Other app performance data, Installed apps, Contacts, Photos and videos, Audio, Files, Calendar, Messages, Health, Web browsing ve ötekiler. Oyuncu bize e-posta yazarsa o e-posta uygulamanın topladığı veri değildir.

**Yargıların gerekçesi**

- **Unity'nin üç türü "Shared: Yes":** Unity satın alma verisi için kendini bağımsız veri sorumlusu sayıyor (sözleşmesi, 2026-06-30) ve politika "Unity bunları kendi mali kayıtları, yasal yükümlülükleri ve sahtekârlığı önlemek için kullanır" diyor: Play'in tanımında bu, hizmet sağlayıcı istisnasına girmiyor. Unity'nin kendi Data safety rehberi "Shared: No" diyor; fazla beyan güvenli taraf (privacy-v1 §5.2'nin önerisi). Diagnostics ve Device IDs aynı kütüphanenin olaylarıyla gittiği için aynı cevap.
- **User IDs ve Other actions "Required":** Ayarlar'da "Bulut kaydı" anahtarı var, ama varsayılan açık ve hesap ilk açılışta, oyuncu bir şey seçmeden açılıyor; Play "optional"ı toplanıp toplanmayacağını kullanıcının seçebildiği veriye ayırıyor. "Users can choose" da savunulabilir.
- **Google Play Games kütüphanesinin kendi verisi** (Google'ın sayfası: oyun hesabı kimliği, kütüphanenin analizi ve teşhisi, kendiliğinden): plan §6.3 bunu Google'ın kendi beyanına bıraktı (sahibin kararı). Kimlik "User IDs"te, teşhis "Diagnostics"te zaten var. Daha temkinli seçenek: "App interactions"ı "Required" yapmak; o zaman mağaza sayfası istatistikler zorunluymuş gibi okunur. Karar sizde.

### 8.7 Uygulama içi ürünler (One-time products)

Önce paket yüklenmiş (`BILLING` izni) ve **Setup → Payments profile** kurulmuş olmalı. Sonra **Monetize with Play → Products → One-time products → Create one-time product**, her ürün için: Product ID (birebir; sonradan değişmez, silinse de yeniden kullanılamaz), Name ve Description (varsayılan dil en-US, **Add translation** ile tr-TR), satın alma seçeneği "Buy", varsayılan fiyat USD, **Activate**.

| Product ID | Name (en) | Description (en) | Name (tr) | Description (tr) | Fiyat |
|---|---|---|---|---|---|
| `com.saturnwave.museumpuzzle.coins.pouch` | Pouch of coins | 1,000 coins to spend on boosters, and on five more moves when a level's moves run out. | Altın kesesi | Booster'lara ve bir bölümün hamleleri bitince 5 hamle daha almaya harcanacak 1.000 altın. | $1.99 |
| `com.saturnwave.museumpuzzle.coins.bag` | Bag of coins | 2,750 coins to spend on boosters, and on five more moves when a level's moves run out. | Altın torbası | Booster'lara ve bir bölümün hamleleri bitince 5 hamle daha almaya harcanacak 2.750 altın. | $4.99 |
| `com.saturnwave.museumpuzzle.coins.chest` | Chest of coins | 6,000 coins to spend on boosters, and on five more moves when a level's moves run out. | Altın sandığı | Booster'lara ve bir bölümün hamleleri bitince 5 hamle daha almaya harcanacak 6.000 altın. | $9.99 |
| `com.saturnwave.museumpuzzle.coins.treasure` | Treasure of coins | 13,000 coins to spend on boosters, and on five more moves when a level's moves run out. | Altın hazinesi | Booster'lara ve bir bölümün hamleleri bitince 5 hamle daha almaya harcanacak 13.000 altın. | $19.99 |
| `com.saturnwave.museumpuzzle.starter.apprentice_kit` | Apprentice's Bag | 1,500 coins, with one brush and splash, one rainbow palette and one +3 moves to start levels with. Once per player. | Çırak Çantası | 1.500 altın; bölümlere başlamak için birer fırça + sıçrama, gökkuşağı paleti ve +3 hamle. Oyuncu başına bir kez. | $1.99 |

- Adlar oyunun mağazasındaki adlar (`words`: `PackPouch`…, `StarterPackName`); içerikler `economy/default.json` → `store`; fiyatlar sahibin kararı (2026-10-24). Adlarda oyunun adı, "Google" ve "Play" yok (Google'ın ürün adı önerisi).
- **Fiyatlar ülkelere:** varsayılan USD fiyatı girilince Play öteki ülkelerin fiyatlarını döviz kuruyla ve yerel fiyat alışkanlıklarıyla kendisi çevirir (Türkiye dahil, vergili); ülke ülke düzeltilebilir. Oyun fiyatı hiç kendisi yazmaz, mağazanın oyuncunun para birimindeki fiyatını gösterir.
- **Tür:** Play Console'da "tüketilir / tüketilmez" ayarı yok, oyun karar verir. Altın paketlerini tüketir (her zaman yeniden alınabilir); Çırak Çantası'nı tüketmez (`NonConsumable`): Play onu oyuncunun malı olarak tutar, ikinci kez satın alınamaz, oyun yeniden kurulunca kendiliğinden geri gelir. Oyun da onu 20. bölümden önce ve bir kez alındıktan sonra satmaz. Çoklu adet (multi-quantity) kapalı kalsın.
- Test: kapalı test kullanıcılarının hesaplarını **Settings → License testing**'e ekleyin; ödeme alınmaz (5.2'nin sonu).

### 8.8 Play Console'a girilenler (2026-10-28)

Claude, sahibin oturumu açık Chrome'undan girdi (Claude in Chrome); formlar kaydedildikten sonra Play Console'dan geri okundu. Paketi sahip yükledi (Chrome'un dosya seçicisi; tarayıcı aracı bir seferde en çok 10 MB taşıyor, paket 306 MB).

- **Kapalı test kanalı:** "Closed testing - Alpha"; test listesi "SketchPaint testers" (öteki üç oyunla ortak; iki adres eklendi, bir Hotmail adresi Google hesabı olmadığı için eklenemedi), geri bildirim adresi `saturnwave.apps@gmail.com`, 177 ülke ve "rest of world".
- **Sürüm 1 (1.0):** sürüm notları en-US ve tr-TR'de; en-US: "First closed test: the museum, 20 rooms of public-domain paintings, cloud save and purchases. Ads are only a test stand-in." Play'in uyarıları 5.2'deki iki uyarı.
- **App content:** 8.5 ve 8.6'daki cevaplar. İçerik derecelendirmesinin sonucu (tütün ve alkol cevabıyla, 8.5): PEGI 3, USK 0, ESRB E10+, ClassInd 14, Kore 15+, Tayvan PG 15.
- **Mağaza sayfası:** en-US metni, simge, tanıtım görseli, 8 telefon görüntüsü; 7 inç ve 10 inç tablete aynı altı görüntü. 12 dilin metni `release/store-listing/<locale>.json`'dan birebir (girildikten sonra dil dil geri okundu). tr-TR'nin kendi tanıtım görseli, telefon ve tablet görüntüleri var; öteki 11 dil İngilizce görselleri devralıyor.
- **Yapay zekâ beyanı (sahibin kararı):** "Don't label assets". Ekranlar oyunun gerçek görüntüleri, tablolar kamu malı eserler; arayüzün çizimleri Claude'un yazdığı SVG kodu, görüntü üreten bir model kullanılmadı. Google'ın yardım sayfası hangi kullanımın etiket istediğini tanımlamıyor; beyan varlık başına sonradan değiştirilebilir (Asset library → varlığın ayrıntıları).
- **Google Play Games on PC:** gönderilen değişikliklerde Advanced settings → Form factors "opted-in" görünüyor. Claude açmadı; Play'in oyunlar için varsayılanı. Paket yalnız arm64-v8a. PC'de istenmezse aynı yerden kapatılır (sahibin kararı).
- **İncelemeye gönderildi:** 28 değişiklik. Önce Google'ın hızlı denetimleri (en çok 14 dakika), sonra inceleme (Google'a göre genelde 7 güne kadar). Onaydan sonra test bağlantısı: `https://play.google.com/apps/testing/com.saturnwave.museumpuzzle` (yalnız listedeki Google hesaplarıyla açılır).
- **Kalan:** ödeme profili ve beş ürün (8.7), lisans test kullanıcıları, LevelPlay'li paketten önce Ads formunda "Yes".

## 9. versionCode 1 açılışta kapanıyordu (2026-10-29)

**Belirti:** Google kapalı testi 29 Ekim'de onayladı, bağlantı açıldı. Sahip oyunu Play'den kurdu (12:33); oyun her açılışta bir iki saniye içinde kapandı. Telefonun kaydında 12:54 ile 21:55 arasında 11 yerel çökme var, hepsi aynı.

**Kayıt** (telefon kabloyla bağlıydı, `adb logcat` ve `dumpsys dropbox` okundu): Unity'nin ana iş parçacığında, pencere açılırken (`APP_CMD_INIT_WINDOW`, bellek yöneticisinden hemen sonra), `JNI DETECTED ERROR IN APPLICATION: java_object == null in call to GetObjectClass` (`libunity.so`). Çökme anında bekleyen Java hatası:

```
java.lang.IllegalArgumentException: Component class com.google.android.play.core.assetpacks.ExtractionForegroundService does not exist in com.saturnwave.museumpuzzle
  at android.app.ApplicationPackageManager.setComponentEnabledSetting(...)
  at com.google.android.play.core.assetpacks.internal.af.a(...) (com.google.android.play:asset-delivery@@2.3.0)
  at com.google.android.play.core.assetpacks.s.a() (com.google.android.play:asset-delivery@@2.3.0)
```

Play'in telefona kurduğu temel APK'nın manifesti (`aapt dump xmltree`): `AssetPackExtractionService` var, `ExtractionForegroundService` yok.

**Neden:** Unity'nin oynatıcısı açılışta Play Asset Delivery kütüphanesinden `AssetPackManager`'ı istiyor. Kütüphane (2.3.0; bytecode'u `javap` ile okundu, `s.a()` ve `internal.af.a`) önce iki servisini, `AssetPackExtractionService` ile `ExtractionForegroundService`'i, `PackageManager.setComponentEnabledSetting` ile açıyor. Manifestte bulamadığı bir servis için Android `IllegalArgumentException` atıyor; Unity boş bir nesneyle devam edip duruyor. 28 Ekim'de izinleri daraltmak için ön plan servisini manifestten çıkarmıştık (`tools:node="remove"`, 1. bölüm). Test APK'sı tek dosya ve kütüphanesi yok; bu yüzden hata yalnız paketten kurulumda çıktı. Paket yüklenmeden önce telefonda açılmamıştı: F33'ün T578 ve T579'u bekliyordu.

**Düzeltme (versionCode 2):**
- Manifest adımı ön plan servisini bırakıyor, yalnız türünü (`foregroundServiceType="dataSync"`) siliyor (`tools:remove`). Dört izin yine geri çekiliyor. Manifestte hiçbir ön plan servis türü olmadığı için Play Console beyan istemiyor. Kütüphane servisi yalnız Play'in kurulumdan sonra indirdiği bir parçayı açarken başlatıyor (`updateServiceState`); oyunun öyle bir parçası yok.
- İşaret yalnız paketin derlemesine konuyor (App Bundle ve binary split açıkken). Test APK'sında kütüphane yok; işaret orada sınıfı olmayan bir servis tanımlardı.
- Derlemenin denetimi ikiye ayrıldı. Paketin temel APK'sında iki servis tanımlı olmalı ve hiçbir ön plan servis türü olmamalı; versionCode 1 bu denetimde dururdu. Test APK'sında kütüphaneden hiçbir şey olmamalı. Kod: `unity/Assets/Editor/ManifestAssetDelivery.cs`; testleri `ManifestAssetDeliveryTests`.
- Aynı pakette sembol tabloları da var (4. bölüm): Play Console'un çökme raporları `libunity`'nin işlevlerini adlarıyla gösterecek.

**Telefonda (versionCode 2, 29 Ekim 22:35):** Play'in kopyası kaldırıldı (imzası başka; içinde hiç açılmamış oyunun boş verisi vardı). APK seti `bundletool install-apks` ile kuruldu: `base`, `UnityDataAssetPack`, `config.arm64_v8a`. Oyun soğuk açıldı ve 20 saniye sonra süreç ayaktaydı; giriş sayfası "Version 1.0" gösterdi. Salon ve istatistik izni kartı geldi ("Don't allow" seçildi). 1. bölümün kartı ve tahtası, öğretici ve tablonun parça önizlemesi açıldı. Bir hamle oynandı: hedef 55'ten 52'ye, hamle 24'ten 23'e. Kayıtta çökme, `JNI DETECTED ERROR`, `FATAL` ya da `SecurityException` yok. UGS `production`'da başladı, bulut kaydı anonim hesapla eşitledi. Mağazanın "unreachable" demesi beklenen durum: Play Console'da ürünler yok. PlayCore'un `AssetPackServiceImpl : onError(-2)` satırı paketin Play'den değil kabloyla kurulmasından; oyun veriyi split APK'dan okuyor. Ekranlar: `out/phone-test/vc2/`.

**Yükleme ve inceleme (29 Ekim gece):** sahip paketi kapalı testin yeni sürümüne yükledi. Play işledi: 2 (1.0), API 26+, hedef SDK 36, arm64-v8a, yeni kurulumda 305 MB. Desteklenen cihazlar değişmedi: 8.367 telefon, 4.733 tablet. Sürüm 1 bu sürüme dahil değil. Sürüm notları: en-US "Fixes the game closing right after it starts. …", tr-TR "Oyunun açılır açılmaz kapanması düzeltildi. …" (öncekiler sürüm 1'den kopyalandı). Play'in tek uyarısı R8 eşleme dosyasının olmaması; sembol uyarısı bu kez çıkmadı. Claude incelemeye gönderdi ("Changes in review"), kayda `2 1.0 2026-10-29 closed` yazıldı. Yönetimli yayın kapalı: onaylanınca sürüm kendiliğinden yayına çıkar.

**Test kullanıcıları:** versionCode 1'i kuran herkes aynı çökmeyi görür. versionCode 2 onaylanınca Play güncellemeyi getirir. Teste katılım (opt-in) kalıcı; oyunu kaldırmak ya da yeniden katılmak gerekmez.

**Ders:** her yüklemeden önce paketin APK seti telefonda açılır (10. bölüm; hata kaydı `docs/LESSONS.md` H5).

## 10. Her yüklemeden önce (zorunlu)

versionCode 1'in açılış çökmesinden (9) sonra konan sıra. Bir adım geçmezse paket yüklenmez; adımın kaydı DEVLOG'a ve `docs/PHONE_TESTS.md`'ye yazılır. Gerekçe ve öteki dersler: `docs/LESSONS.md`.

1. **Paketi derle** (4). Son satır `Play bundle: succeeded, signed with the upload key …` olmalı. Kayıttaki bütün `Android build:` ve `Play bundle:` satırları okunur: izinler, Play Asset Delivery'nin iki servisi, sembol tabloları, 16 KB hizası, temel modülün boyutu.
2. **Telefonu hazırla.** USB'yle bağlı, `adb devices` "device" diyor. Telefonda imzası başka bir kopya varsa (Play'den kurulan ya da test APK'sı) önce kaldırılır: `adb uninstall com.saturnwave.museumpuzzle`. İlerleme yalnız bulut kaydında kalır; kaldırmadan önce sahibe söylenir.
3. **Paketin APK setini kur** (T578):
   ```
   java -jar D:\puzzle-cache\tools\bundletool-all-1.18.3.jar install-apks --apks=D:\puzzle-cache\play\museumpuzzle-vc<N>-upload.apks --adb=<adb.exe>
   adb shell pm path com.saturnwave.museumpuzzle
   ```
   Üç parça çıkmalı: `base`, `UnityDataAssetPack`, `config.arm64_v8a`. `java` Unity'nin kendi Java'sı, `adb` Unity'nin SDK'sındaki.
4. **Soğuk aç, kaydı oku** (T579). `adb logcat -c`, sonra `adb shell monkey -p com.saturnwave.museumpuzzle -c android.intent.category.LAUNCHER 1`. 20 saniye sonra `adb shell pidof com.saturnwave.museumpuzzle` bir sayı vermeli (oyun ayakta). `adb logcat -d` içinde oyunun sürecinde `FATAL`, `JNI DETECTED ERROR`, `Fatal signal` ya da `ANR` olmamalı. `adb shell dumpsys dropbox` içinde paket için yeni `data_app_native_crash` ya da `data_app_crash` olmamalı.
5. **Oyna** (T579). Giriş sayfası ("Version …"), salon, 1. bölüm ve bir hamle, koleksiyon, harita. Sonra telefonu yeniden başlat ve oyunu yine aç. Ekranlar `out/phone-test/vc<N>/` altına.
6. **Play üzerinden dene.** Paket önce **Internal testing** kanalına yüklenir; dahili test birkaç dakikada açılır, uzun inceleme beklemez. Telefondaki yan yüklenen kopya kaldırılır, oyun dahili test bağlantısından Play ile kurulur. Play'in imzası ve otomatik koruması (`com.pairip`) bu kopyaya eklenir. 4. ve 5. adımlar yinelenir (T583).
7. **Terfi ettir.** Ancak bundan sonra aynı paket **Promote release** ile kapalı teste (ya da üretime) taşınır ve incelemeye gönderilir. İkinci yükleme yok.
8. **Kayda yaz** (6): Play paketi kabul edince `release/google-play-uploads.txt` satırı eklenir ve depoya işlenir.

Kütüphanenin tanımladığı bir bileşeni ya da izni manifestten çıkaran her değişiklikte, 1. adımdan önce kütüphanenin o bileşene dokunan kodu okunur (`javap -c`) ve sonuç belgeye yazılır (`docs/LESSONS.md` kural 2).

**İstisna (sahibin kararı, 2026-10-29):** versionCode 2 kapalı teste doğrudan gidiyor, 6. adım atlanıyor. Gerekçe: kapalı testteki herkes açılmayan versionCode 1'de; 1–5. adımlar geçti. Play'in işlediği kopya onaydan hemen sonra denenir (T583). versionCode 3'ten başlayarak liste eksiksiz uygulanır.
