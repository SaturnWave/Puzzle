# iOS Derlemesi — Adım Adım Plan

> **Durum (2026-10-23 akşam):** Kod iOS'a hazırlandı ve Xcode projesini dışa aktaran araç (`IosBuild.Run`) yazıldı. Bu PC'deki Unity 6000.6.4f1'e **iOS Build Support kuruldu** (Hub'ın komut satırıyla). Mac açık ve SSH ile ulaşılıyor: kullanıcı `arcancaglayan`, Intel, macOS 15.8, Xcode 26.3 (iOS 26.2 SDK), bir "Apple Development" sertifikası var. Depo Mac'te `~/Desktop/Puzzle` klasöründe (`feature/ios`); Mac'in kendi SSH anahtarı GitHub'a SaturnWave olarak ulaşıyor. Kod Windows'ta yazılır; Mac yalnızca dışa aktarılan Xcode projesini derler ve cihaza kurar. Dışa aktarma, Android kopyasının önbelleğine dokunmamak için D: diskindeki ayrı çalışma ağacında (`D:/puzzle-ios`) yapılır. iOS işi `feature/ios` dalında yürür; §3.2–§3.4 henüz denenmemiş bir plandır.
>
> **v1 (2026-10-24):** gerçek satın almalar (Unity IAP 5.4.4, StoreKit 2), oyun istatistiklerinin rıza sorusu ve ressam `feature/ios`'a birleşti. Dışa aktarılan Xcode projesi StoreKit'i uygulamaya ve UnityFramework'e bağlıyor; bağlamazsa dışa aktarma artık duruyor (§3.1.2). Info.plist'e yeni anahtar gelmedi. App Store'un gizlilik etiketinin cevapları §4.1'de; ürünler, sandbox hesabı ve sözleşme proje sahibinde (§2 madde 8).

## 1. Yol

| Adım | Nerede | Ne olur |
|---|---|---|
| 1 | Windows PC | Unity, Xcode projesini dışa aktarır: `IosBuild.Run` → `unity/Build/iOS/` |
| 2 | PC → Mac | Proje Tailscale üzerinden Mac'e kopyalanır (`tar` + `ssh`) |
| 3 | Mac, SSH ile | `xcodebuild archive`, sonra `xcodebuild -exportArchive`: otomatik imza, proje sahibinin Team ID'si |
| 4 | Mac → App Store Connect | Dışa aktarma, derlemeyi App Store Connect API anahtarıyla TestFlight'a yükler |

Neden Mac: Apple'ın imza ve yükleme araçları yalnızca macOS'ta, Xcode ile çalışır. Unity'nin Windows'ta yapabildiği yalnızca Xcode projesini üretmektir. Ajan Mac'e yalnızca proje sahibinin eklediği SSH anahtarıyla bağlanır; Mac'te başka hiçbir ayara dokunmaz.

## 2. Proje sahibinin bir kez yapacakları

1. **Unity'ye iOS Build Support modülü.** Unity Hub → Installs → 6000.6.4f1 → ⚙ → Add modules → **iOS Build Support** → Install. Unity Hub, Unity hesabı ve lisans yalnızca proje sahibinindir. Kurulunca "kuruldu" demek yeter.
2. **Bu PC'nin SSH anahtarı Mac'e.**
   - PC'de, PowerShell'de anahtarın tek satırı: `Get-Content $env:USERPROFILE\.ssh\id_ed25519.pub`. Yalnızca `.pub` dosyası kopyalanır; `id_ed25519` gizli anahtardır, hiçbir yere gitmez.
   - Mac'te Terminal: `mkdir -p ~/.ssh && chmod 700 ~/.ssh`; satırı `~/.ssh/authorized_keys` dosyasının sonuna ekleyin (`nano ~/.ssh/authorized_keys`); sonra `chmod 600 ~/.ssh/authorized_keys`.
   - Mac'teki kullanıcı adını bildirin (Terminal'de `whoami`).
   - Remote Login açık kalsın (System Settings → General → Sharing). Derleme sürerken Mac uyumasın: prize takılıyken ekran kapanınca uyumayı kapatın (System Settings → Battery → Options).
3. **Xcode.** App Store'dan en güncel Xcode (App Store Connect yalnızca güncel SDK ile yapılmış derlemeleri alır; Apple bu alt sınırı her yıl nisanda yükseltir). Bir kez açılıp ek bileşenleri kurdurun; Settings → Components'ta iOS platformu yüklü olsun. Terminal'de bir kez: `sudo xcodebuild -license accept`. Xcode → Settings → Accounts'ta Developer hesabının Apple ID'siyle oturum açık olsun. Sürümü bize yazın: `xcodebuild -version`. Mac bu Xcode'u çalıştıramayacak kadar eskiyse §6'daki bulut Mac yedeği devreye girer.
4. **Team ID.** developer.apple.com/account → Membership details → **Team ID** (10 karakter). Gizli değildir; bize yazın.
5. **Bundle ID kararı ve App Store Connect kaydı.**
   - **Önce karar:** `com.saturnwave.puzzle` geçicidir. App Store Connect'te kayıt oluşturulduktan sonra bundle ID değiştirilemez ve başka uygulamaya verilemez. Kalıcı kimlik (oyunun adı belli olunca) bu adımdan önce seçilmeli; Android'deki kimlik de aynı kalır (ikisini birden `unity/Assets/Editor/GameIdentity.cs` tutar).
   - developer.apple.com → Certificates, Identifiers & Profiles → Identifiers → **+** → App IDs → App → Explicit; bundle ID'yi yazın. Capabilities'te **In-App Purchase** kendiliğinden işaretli gelir, öyle kalsın (v1 satış yapıyor, §3.1.2). Başka hiçbirini işaretlemeyin (oyun Game Center, bildirim, iCloud kullanmıyor).
   - appstoreconnect.apple.com → Apps → **+** → New App: Platform iOS; Name (App Store'da benzersiz, en çok 30 karakter; sonra değişebilir); Primary Language; Bundle ID (listeden); SKU (yalnızca size görünür, ör. `museum-puzzle-ios`).
6. **Yükleme için App Store Connect API anahtarı.**
   - appstoreconnect.apple.com → Users and Access → Integrations → App Store Connect API → Team Keys → **Generate API Key** (ilk kez erişim isteği gerekebilir; bunu yalnızca Account Holder yapar).
   - Ad: "Mac upload"; Access: **Admin**. Admin, xcodebuild'in dağıtım sertifikasını ve profilini bu anahtarla kendisi oluşturabilmesi içindir; yalnızca yükleme yapılacaksa App Manager yeter.
   - `.p8` dosyası yalnızca bir kez indirilebilir. Mac'e kendiniz koyun: `mkdir -p ~/.appstoreconnect/private_keys && chmod 700 ~/.appstoreconnect/private_keys`, dosya `~/.appstoreconnect/private_keys/AuthKey_<KEY_ID>.p8`, sonra `chmod 600`. Bu dosya gizlidir: depoya, sohbete ya da e-postaya konmaz; ajan onu okumaz, yalnızca yolunu verir.
   - Bize iki değeri yazın: **Key ID** ve **Issuer ID** (aynı sayfanın üstünde).
7. **Uygulama ikonu.** 1024×1024 PNG, saydamlıksız, köşeleri yuvarlatılmamış. TestFlight ikonsuz derlemeyi reddeder. Projede henüz ikon yok (Android paketi de Unity'nin varsayılanıyla çıkıyor); ikonu proje sahibi verir, projeye ajan ekler.
8. **Satın alma (v1, 2026-10-24).** Hepsi proje sahibinin hesabında; ajan giriş yapmaz, ürün ya da hesap açmaz.
   - **Sözleşme:** App Store Connect → Business (Agreements, Tax, and Banking) → **Paid Applications** sözleşmesi, banka ve vergi bilgileri. Bu sözleşme etkin değilken StoreKit hiçbir ürünü vermez, sandbox'ta ve TestFlight'ta da; oyun o zaman "Mağaza şu anda açılamıyor" der (T470'in iOS hâli).
   - **Beş ürün:** uygulamanın sayfası → Monetization → In-App Purchases → **+**. Ürün kimlikleri tam olarak şunlar (oyun onları bundle ID'den kurar; bundle ID değişirse bunlar da değişir, `economy/default.json` `store`):

     | Tür | Product ID | ABD fiyatı | İçerik |
     |---|---|---|---|
     | Consumable | `com.saturnwave.puzzle.coins.pouch` | $1.99 | 1.000 altın |
     | Consumable | `com.saturnwave.puzzle.coins.bag` | $4.99 | 2.750 altın |
     | Consumable | `com.saturnwave.puzzle.coins.chest` | $9.99 | 6.000 altın |
     | Consumable | `com.saturnwave.puzzle.coins.treasure` | $19.99 | 13.000 altın |
     | **Non-Consumable** | `com.saturnwave.puzzle.starter.apprentice_kit` | $1.99 | Çırak Çantası: 1.500 altın ve üç bölüm öncesi booster, oyuncu başına bir kez |

     Her üründe: Reference Name (yalnız size görünür), fiyat (ABD temel fiyatı; öteki ülkeleri Apple çevirir, oyun mağazanın yerel fiyat yazısını gösterir), en az İngilizce ve Türkçe görünen ad ve açıklama (Apple'ın ödeme penceresi bunları gösterir), inceleme için mağaza ekranının bir görüntüsü (`ScreenShots -screens shop`).
   - **Aile Paylaşımı kapalı kalsın:** Çırak Çantası'nın sayfasındaki **Family Sharing** açılmaz. Apple'a göre bir ürün için açıldıktan sonra geri kapatılamaz; oyun Çırak Çantası'nı oyuncu başına bir kez verir ve aile üyesinin cihazına geri yüklemeyle gelmesini istemiyoruz (GAME_DESIGN §19). Altın paketleri tüketildiği için paylaşılamaz.
   - **İlk ürünler sürümle birlikte incelenir:** ilk satın almalar bir uygulama sürümüyle birlikte incelemeye gönderilir (sürüm sayfasındaki In-App Purchases bölümünde ürünleri seçerek). İnceleme notuna: "Mağaza: alttaki çubuktaki mağaza; geri yükleme: mağazanın altındaki 'Satın alımları geri yükle'."
   - **Sandbox hesabı:** Users and Access → Sandbox → Test Accounts → **+**: hiçbir Apple hesabında kullanılmamış bir e-posta. iPad'de Ayarlar → Geliştirici → Sandbox Apple Account (eski iPadOS'ta Ayarlar → App Store → Sandbox hesabı) ile girişi proje sahibi yapar. Ask to Buy testi (T486) için ayrıca bir aile kurulumu gerekir.

## 3. Her derlemede (ajan yapar)

### 3.1 Windows: Xcode projesi

```
"C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe" -batchmode -quit -buildTarget iOS -projectPath "D:/puzzle-ios/unity" -executeMethod Puzzle.Client.EditorTools.IosBuild.Run -logFile out/unity/ios-build.log
```

- `-buildTarget iOS` Editör'ü iOS hedefinde açar. Unity IAP'nin Xcode adımı (StoreKit'in bağlanması) yalnız Editör'ün etkin hedefi iOS'ken derlenir; o adım olmadan çıkan proje kurulur ama hiçbir şey satamaz. Dışa aktarma bunu denetler ve eksikse durur (§3.1.2).
- Önce ProjectSetup'ın işini yapar (atlas, katalog, sahne); sonra `unity/Build/iOS/` klasörünü baştan yazar. Android paketiyle aynı sahne ve içerik.
- Ayarlar: bundle ID ve ad `GameIdentity`'den; iPhone dik, iPad dört yöne döner ve tam ekran istemez (§3.1.1); iPhone ve iPad; ARM64, IL2CPP; kod ayıklama düzeyi Android'deki gibi; otomatik imza; derleme numarası Android'in sürüm koduyla aynı (Xcode, App Store Connect'te varsa yükleme sırasında artırır).
- **En düşük iOS 15.0:** Unity 6.6'nın desteklediği ve Xcode'un hâlâ derlediği en düşük sürüm. iPhone 6s ve ilk SE'den bu yana her iPhone'u kapsar; oyun daha yeni bir iOS özelliğine ihtiyaç duymuyor (dokunuşlar iOS 10'dan beri var).
- **Info.plist:** `ITSAppUsesNonExemptEncryption = false`: oyun Apple'ınkinden başka şifreleme kullanmıyor; v1'in gönderdikleri (mağazayla konuşma, IAP'nin satın alma olayları, izinle istatistikler) HTTPS ile, StoreKit ve UnityWebRequest üzerinden iOS'un kendi TLS'iyle gider; her yüklemede şifreleme sorusu sorulmaz. İzleme, kamera, mikrofon ve konum açıklaması bırakılmaz. Yönler: iPhone yalnız dik, iPad dört yön; `UIRequiresFullScreen` yok (§3.1.1). Satın alma ve Aptabase yeni anahtar istemiyor (§3.1.2).
- İlk dışa aktarmada Unity aktif hedefi iOS'a çevirir ve dokuları yeniden içe aktarır (uzun sürebilir); sonraki Android derlemesi hedefi geri çevirir.

### 3.1.1 Yan çevirme (2026-10-24)

- **Kural (sahibinin kararı):** iPhone dik kalır; iPad dört yöne döner, ters dik de. Oyun Pencereli Uygulamalar'da, başka bir uygulamanın yanında ve Stage Manager'da da çalışır; pencere hangi şekli alırsa oyun ona yerleşir. Bölüm oynanırken de döner.
- **Info.plist** (`IosBuild.InfoPlistLines`, `PlistLine.Lists`): `UISupportedInterfaceOrientations` = yalnız `UIInterfaceOrientationPortrait`; `UISupportedInterfaceOrientations~ipad` = Portrait, PortraitUpsideDown, LandscapeLeft, LandscapeRight; `UIRequiresFullScreen` (ve `~ipad`) silinir. Info.plist adımı artık dizi de yazabiliyor: Unity'nin yazdığı dizinin yerine, her satıra bir dize.
- **Ayarlar:** dışa aktarma sırasında `defaultInterfaceOrientation = AutoRotation` ve `requiresFullScreen = false`; dört yön de izinli. Oyun başlarken iPad'e dört yönü, iPhone'a yalnız diki yeniden söyler (`DeviceScreen`, `ScreenTurns`: model adı "iPad" ile başlıyorsa tablet).
  - Bu iki ayar yalnız iOS dışa aktarmasınındır (`IosBuild.TurningForTheExport`): aktarmadan önce konur, bitince (başarısız olsa da) eski değerlerine döner. Kayıtlı `ProjectSettings.asset` iki dalda da dik (`Portrait`) ve "tam ekran" açık kalır: Android paketi dik açılmalı, Android derlemesi bunu manifestte denetler (`ManifestOrientation`). Önceden iOS derlemesi bu değerleri kalıcı yazıyordu; dosya iki dal arasında her derlemede değişiyordu.
- **Neden:** Apple, tam ekran istemeyen iPad uygulamasından dört yönü istiyor (ITMS-90474). UIRequiresFullScreen iPadOS 26'dan beri kaldırılmış durumda; iOS 27 SDK ile derlenen uygulamada pencerenin boyutlanmasını artık durdurmuyor (TN3192). Unity'ye göre bu ayar kapalıyken iPadOS 26'da yön kilitlenemiyor; oyun iPad'de kilit kullanmıyor.
- **Düzen:** yan tutulunca dik düzen ortada kalır, yanlarda galerinin duvarı; salonda odanın onarılmış tabloları, haritada geniş çerçeve, yapbozda tablo solda ve tepsi sağda (GAME_DESIGN §23, "Yan çevirme"). Editör'de 12.9" Pro yan (`2732x2048+0+40`) ve dik, 11" yan görüntülendi; `RotationCheck` iPad'i oyun sürerken döndürür.
- **Dışa aktarma (2026-10-24, 2,3 dk):** Info.plist'te iPhone için yalnız dik, iPad için dört yön, `UIRequiresFullScreen` yok. UIScene yaşam döngüsü hazır: `UIApplicationSceneManifest` içinde `UISceneDelegateClassName = UnityScene` (iOS 27 SDK ile zorunlu olacak). iPad'de bakılacak: iPadOS 26'nın sol üstteki pencere düğmeleri pencere kipinde geri düğmesinin ya da plaketin üstüne gelirse not edilir (T457).
- **Yeniden dışa aktarma (2026-10-24, inceleme düzeltmesinden sonra, 20,6 dk):** Info.plist'te `UISupportedInterfaceOrientations` = yalnız Portrait; `UISupportedInterfaceOrientations~ipad` = Portrait, PortraitUpsideDown, LandscapeLeft, LandscapeRight; `UIRequiresFullScreen` yok. Aktarmadan sonra `ProjectSettings.asset` değişmemişti (yön `Portrait`, tam ekran açık).
- Telefon testleri T450–T459 (PHONE_TESTS F23).

### 3.1.2 Satın alma (v1, 2026-10-24)

- **StoreKit 2, Unity IAP 5.4.4 üzerinden.** IAP'nin iOS kodu Swift (StoreKit 2, `Plugins/UnityPurchasing/iOS/SK2`) ve UnityFramework'te derlenir; StoreKit 2 iOS 15 ister, oyunun en düşük sürümü de 15.0. Unity projede `SWIFT_VERSION = 5.0` yazıyor. Unity Gaming Services başlatılmaz; ürünler kodda (`UnityStore`).
- **Xcode projesi:** IAP'nin kendi derleme adımı (`AppleCapabilities`, com.unity.purchasing) StoreKit.framework'ü `Unity-iPhone`'a ve `UnityFramework`'e bağlar, Swift başlıklarının içe aktarımını düzeltir. Unity 6.6'nın Xcode kitaplığı eski türden yetenek listesi (`SystemCapabilities`) yazmıyor; In-App Purchase yeteneği bir yetki (entitlement) da istemiyor, App ID'de baştan açık. Projede yeteneğin karşılığı StoreKit bağıdır.
  - IAP bu adımı yalnız Editör'ün etkin hedefi iOS'ken derler. Dışa aktarma bu yüzden `-buildTarget iOS` ile çalışır (§3.1) ve StoreKit adımı (`IosStoreKitStep`, IAP'nin adımından sonra) projeyi okur: iki hedeften biri StoreKit'i bağlamıyorsa derleme adıyla durur. Okuma `XcodeProjectText`'te, Unity'siz ve testli (proje dosyasının hedefleri, framework aşamaları, derleme dosyaları ve dosya yolları).
  - İlk denemede adım bir de eski yetenek satırını arıyordu ve dışa aktarmayı durdurdu; Unity 6.6 o satırı hiç yazmadığı için kaldırıldı.
- **Info.plist:** satın alma için yeni anahtar yok, Aptabase için de. Dışa aktarılan listede şifreleme `false`; `NSUserTrackingUsageDescription`, kamera, mikrofon ve konum yok; yönler §3.1.1'deki gibi; `UIRequiresFullScreen` yok.
- **Reklam kimliği okunmuyor:** IAP'nin Swift eklentisi `import AdSupport` diyor ve IDFA'yı okuyabilen bir işlev taşıyor (AdSupport Swift'in kendiliğinden bağlamasıyla gelir, projede ayrı satırı yok). C# tarafı onu yalnız Unity'nin rıza arabirimi reklam izni verildi derse çağırır; oyun o arabirimi hiç çağırmıyor. App Tracking Transparency sorusu yok. IDFV her satın alma olayıyla Unity'ye gider (§4.1).
- **Gizlilik manifestleri:** Unity, eklentilerin manifestlerini `UnityFramework/PrivacyInfo.xcprivacy`'de birleştiriyor: gerekçeli API'ler FileTimestamp (0A2A.1, C617.1; IAP'den C617.1 bir kez daha), UserDefaults (CA92.1), DiskSpace (E174.1, IAP'den); `NSPrivacyTracking = false`. Motorun kendi manifesti `UnityRuntime.framework`'te (SystemBootTime 35F9.1, DiskSpace, UserDefaults, FileTimestamp). Toplanan veri türleri (`NSPrivacyCollectedDataTypes`) hiçbir manifestte yok: IAP'ninki boş liste ve birleştirmede düşüyor, Aptabase'in manifesti yok. App Store'un etiketi elle doldurulur (§4.1) ve onun yerini tutmaz; Xcode'un gizlilik raporu toplanan veri göstermez. Uygulamaya etiketle aynı türleri söyleyen kendi manifestini eklemek bir seçenek; ikinci bir kaynak olacağı için yapılmadı, App Store Connect e-postayla isterse eklenir.
- **Geri yükleme:** mağazanın altında "Satın alımları geri yükle" yalnız iPhone ve iPad'de (`UnityStore.OfThisPlatform`: `RuntimePlatform.IPhonePlayer` iPad'i de kapsar). Satır StoreKit'in `AppStore.sync()`'ini çağırır; iOS oyuncudan App Store hesabını isteyebilir, Apple da bunu yalnız oyuncunun dokunuşuyla istiyor. Ardından IAP oyuncunun sahip olduklarını sorar: Çırak Çantası bir kez gelir, altın paketleri hiç. Mağaza bağlı değilse kart hemen "Mağaza şu anda yanıt vermiyor." der. Çoğu zaman satıra gerek kalmaz: oyun açılışta mağazanın fiyatlarından sonra sahip olunanları sorar (`Transaction.currentEntitlements`), Çırak Çantası orada da gelir.
- **Ask to Buy:** ebeveyn onayı bekleyen ödeme "Onay bekleniyor" kartıdır; onay gelince ödeme bir kez verilir (T486).
- **Aile Paylaşımı kapalı:** proje sahibinin App Store Connect ayarı (§2 madde 8, GAME_DESIGN §19).
- **Sunucu yok, fiş cihazda:** ödeme StoreKit 2'nin cihazda doğruladığı haliyle kabul edilir. Apple bir satın almayı iade ederse oyun bunu öğrenmez (App Store Server Notifications bir sunucu ister): verilen altın ve booster'lar kalır. İade edilen Çırak Çantası StoreKit'in sahip olunanlar listesinden çıkar; silip kurulunca geri gelmez. Oyun silinince dosyaları da silinir: yeniden kurulumda altın paketleri gelmez, Çırak Çantası gelir. iPhone ya da iPad yedekten geri yüklenirse oyunun dosyaları (ilerleme ve altın) yedekle geri gelebilir, çünkü Documents klasörü iCloud yedeğine girer.
- **Görüntüler (Editör, 2026-10-24):** mağaza, izin sorusu, ayarlar, gizlilik kartı, salondaki ressam, onarılan oda ve tamamlanan tablo 12,9" (2048×2732) ve 10. nesil (1640×2360) iPad'de dik ve yan; kart ve satın alma yürüyüşleri (geri yükleme satırı ve kartı dahil) 12,9" dik ve yan. Kesik yazı, ekrandan taşan düğme yok; geri yükleme satırı yanda da altın panelinin içinde.
- **Telefon testleri:** T485, T486 (F24) ve iPad'de T510–T515 (F27); açık A50.
- **Dışa aktarma (2026-10-24, `-buildTarget iOS`, 2,6 dk, 1,9 GB):** günlükte "iOS build: Unity-iPhone and UnityFramework link StoreKit.framework."; proje dosyasında StoreKit'in iki derleme dosyası, birer tane uygulamanın ve UnityFramework'ün framework aşamasında. Info.plist 28 anahtar: şifreleme `false`, iPhone yalnız dik, iPad dört yön; izleme, kamera, mikrofon, konum, `UIRequiresFullScreen` ve satın almaya ait anahtar yok. `IPHONEOS_DEPLOYMENT_TARGET = 15.0`. `ProjectSettings.asset` değişmedi. Mac'te derlenmedi, iPad'e kurulmadı (iPad bağlı değildi).

### 3.2 PC → Mac

Windows 11'in kendi `tar` ve `ssh` araçlarıyla, tek akışta (`<kullanıcı>` §2'deki ad; ad çözülmezse `100.94.229.108`):

```
tar -C unity/Build -cf - iOS | ssh <kullanıcı>@arcans-macbook-pro "rm -rf ~/puzzle-ios/iOS && mkdir -p ~/puzzle-ios && tar -C ~/puzzle-ios -xf -"
ssh <kullanıcı>@arcans-macbook-pro "cd ~/puzzle-ios/iOS && find . -name '*.sh' -exec chmod +x {} +"
```

Klasör 1–2 GB olabilir. Windows'tan gelen dosyalar çalıştırma iznini taşımaz; ikinci satır Xcode'un betiklerine izni verir (başka araçların da izin isteyip istemediği ilk denemede görülecek). Yedek yol: `scp -r unity/Build/iOS <kullanıcı>@arcans-macbook-pro:~/puzzle-ios/` (çok sayıda küçük dosyada daha yavaş).

**Denendi (2026-10-24):** kullanıcı `arcancaglayan`; `tar -czf` ile sıkıştırılmış tek akış, 1,7 GB 115 saniyede geçti (Tailscale doğrudan bağlantı). `.sh` izni dışında başka izin gerekmedi.

### 3.2.1 iPad'e geliştirme sürümü (TestFlight'sız; 2026-10-24'te denendi)

App Store Connect kaydı olmadan, takımın geliştirme sertifikasıyla doğrudan bir cihaza kurulum.

- **Derleme, SSH'den:** `cd ~/puzzle-ios/iOS && xcodebuild -project Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Release -destination "generic/platform=iOS" -derivedDataPath ~/puzzle-ios/DerivedData DEVELOPMENT_TEAM=W4P65SPZP9 CODE_SIGN_STYLE=Automatic build`. IL2CPP'nin C++ kodu Intel Mac'te derlenir. Son adım olan imza SSH'den `errSecInternalComponent` ile durur; bu beklenen bir durumdur (oturum anahtar zinciri SSH'ye kapalı).
- **İmza ve kurulum, Mac'in kendi Terminal'inde:** `sh ~/puzzle-ios/sign-and-install.sh` (depoda `tools/ios/sign-and-install.sh`). Aynı komutu aynı DerivedData ile yeniden çalıştırır: kodu yeniden derlemez, yalnızca imzalar. Sonra `xcrun devicectl device install app` ile iPad'e kurar. İlk çalıştırmada macOS, codesign'ın anahtarı kullanmasına izin isteyebilir: Mac şifresi, "Her Zaman İzin Ver".
- **Gerekenler:**
  - Takım kimliği `W4P65SPZP9`.
  - Xcode'un yönettiği joker geliştirme profili ("iOS Team Provisioning Profile: *"); iPad'in kimliği (`00008112-0016084A0C06201E`) bu profilde kayıtlı.
  - iPad kabloyla bağlı ve Mac'le eşleşmiş; **Geliştirici Modu** açık (Ayarlar → Gizlilik ve Güvenlik → Geliştirici Modu → Aç; iPad yeniden başlar, açılınca "Aç" ve iPad şifresi).
- **Sonuç:** Puzzle.app (imzalı, 854 MB) 2026-10-24'te proje sahibinin iPad Pro 12.9" (6. nesil, iPadOS 26.5) cihazına kuruldu.
- Yeni bir sürüm için: dışa aktarma (§3.1), aktarma (§3.2), SSH'den derleme, sonra Mac'te aynı betik. TestFlight yolu (§3.3, bulut imzası) hâlâ denenmedi.

### 3.3 Mac: arşiv, imza ve TestFlight'a yükleme

SSH oturumunda Mac'in oturum anahtar zinciri kilitli görünür; yerel sertifikayla imza `errSecInternalComponent` hatasıyla durur. Bu yüzden plan, arşivi imzasız alıp imzayı dışa aktarmada Apple'ın bulutta tuttuğu dağıtım sertifikasıyla (cloud signing, API anahtarıyla) yaptırmaktır: Mac'te parola ya da anahtar zinciri gerekmez.

`~/puzzle-ios/ExportOptions.plist` (bir kez yazılır; `<TEAM_ID>` §2 madde 4):

```
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>method</key><string>app-store-connect</string>
	<key>destination</key><string>upload</string>
	<key>teamID</key><string><TEAM_ID></string>
	<key>signingStyle</key><string>automatic</string>
	<key>manageAppVersionAndBuildNumber</key><true/>
	<key>uploadSymbols</key><true/>
</dict>
</plist>
```

Arşiv, sonra dışa aktarma ve yükleme (`<KEY_ID>`, `<ISSUER_ID>` §2 madde 6):

```
cd ~/puzzle-ios/iOS
xcodebuild archive -project Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Release \
  -destination 'generic/platform=iOS' -archivePath ~/puzzle-ios/Puzzle.xcarchive \
  DEVELOPMENT_TEAM=<TEAM_ID> CODE_SIGNING_ALLOWED=NO
xcodebuild -exportArchive -archivePath ~/puzzle-ios/Puzzle.xcarchive \
  -exportOptionsPlist ~/puzzle-ios/ExportOptions.plist -exportPath ~/puzzle-ios/export \
  -allowProvisioningUpdates \
  -authenticationKeyPath ~/.appstoreconnect/private_keys/AuthKey_<KEY_ID>.p8 \
  -authenticationKeyID <KEY_ID> -authenticationKeyIssuerID <ISSUER_ID>
```

`destination = upload` olduğu için dışa aktarma derlemeyi doğrudan App Store Connect'e yükler. Bu yol ilk denemede doğrulanacak; olmazsa iki yedek var:

- **İmzalı arşiv:** arşiv komutuna `-allowProvisioningUpdates` ve aynı üç `-authentication…` bayrağı eklenir, `CODE_SIGNING_ALLOWED=NO` çıkarılır, `CODE_SIGN_STYLE=Automatic` eklenir. Bunun için anahtar zincirinin SSH oturumunda açılması gerekir; parolayı ajan girmez, proje sahibi bir kez `security unlock-keychain ~/Library/Keychains/login.keychain-db` komutunu kendisi çalıştırır.
- **Ayrı yükleme:** `ExportOptions.plist`'te `destination` `export` yapılır; çıkan `.ipa`, Xcode'la gelen `altool` ile yüklenir: `xcrun altool --upload-app -f ~/puzzle-ios/export/Puzzle.ipa -t ios --apiKey <KEY_ID> --apiIssuer <ISSUER_ID>` (`altool` anahtarı `~/.appstoreconnect/private_keys` içinde kendisi bulur). Yeni Xcode'larda `--upload-app` yerine `--upload-package` önerilir; kurulu sürümde hangisinin olduğuna `xcrun altool --help` ile bakılır. Transporter uygulaması da aynı işi pencereyle yapar.

### 3.4 TestFlight

- App Store Connect yüklenen derlemeyi 5–30 dakikada işler ve e-posta gönderir. Şifreleme sorusu sorulmaz (Info.plist).
- **İç test:** TestFlight sekmesi → Internal Testing → grup → kendinizi ve ekipteki en çok 100 kişiyi ekleyin; iPhone'da TestFlight uygulamasından kurulur. Apple incelemesi yok.
- **Dış test** (tanıdıklar, e-postayla ya da herkese açık bağlantıyla, en çok 10.000 kişi): ilk derleme Beta App Review'dan geçer (genelde bir gün); Test Information (açıklama, iletişim e-postası) ister.
- Bir TestFlight derlemesi 90 gün kurulabilir kalır.

## 4. App Store'un istedikleri (yayın için; iç test için gerekmez)

| Ne | Durum | Not |
|---|---|---|
| Gizlilik politikası URL'si | Hazır değil | Oyun sitesi (SaturnWave.Apps) henüz yayında değil; v1 metninin taslağı `docs/research/privacy-v1.md` §4, v1 mağazaya çıkmadan önce yayımlanmalı. Metni yalnızca Android'in Google yedeğinden söz ediyor; iPhone'un iCloud yedeği için bir cümle eklenmeli. Apple politikaya uygulamanın içinden de bağlantı istiyor; sitenin adresi gelince oyuna eklenir. |
| Destek URL'si | Hazır değil | Aynı sitede destek sayfası; herkese açık iletişim e-postası henüz yok. |
| App Privacy | **Veri toplanıyor** (§4.1) | v1'de "Data Not Collected" artık doğru değil: IAP 5.4.4'ün satın alma olayları IDFV ile Unity'ye gidiyor, istatistikler izinle Aptabase'e. Cevaplar §4.1'de; formu proje sahibi doldurur. |
| Uygulama içi satın almalar | Ürünler açılacak | Beş ürün, sözleşme ve sandbox hesabı (§2 madde 8). İlk ürünler bir sürümle birlikte incelenir. Apple geri yükleme düğmesi ister: mağazanın altında "Satın alımları geri yükle" var. |
| Yaş derecelendirmesi | Anket | Şiddet, kumar, kullanıcı içeriği, sohbet yok: 4+ beklenir. |
| Ekran görüntüleri | Hazırlanacak | iPhone 6.9" (1320×2868 ya da 1290×2796) zorunlu. iPad de desteklendiği için iPad 13" (2064×2752 ya da 2048×2732) de zorunlu. Her boyutta 1–10 görüntü; küçük ekranlarınki büyükten ölçeklenir. Unity'deki ScreenShots aracı bu boyutlarda görüntü alabilir. |
| Ad, alt başlık, açıklama, anahtar kelimeler, kategori | Hazırlanacak | Kategori: Games → Puzzle. Ad en çok 30 karakter ve benzersiz. |
| İçerik hakları | Beyan | Tablolar kamu malı (public domain); üçüncü taraf içerik sorusunda belirtilir. |
| Fiyat ve ülkeler | Karar | Ücretsiz; ülke seçimi proje sahibinin. |
| İkon | Bekliyor | §2 madde 7. |

### 4.1 App Privacy: gizlilik etiketinin cevapları (v1, 2026-10-24)

App Store Connect → uygulama → **App Privacy**. Cevaplar taslağın `docs/research/privacy-v1.md` §5.1 tablosundan; bu dalda paketlerin kaynağıyla ve dışa aktarılan projeyle karşılaştırıldı (aşağıda "Dayanak"). Beyan, kütüphanelerin yapabildiğine değil v1'in gerçekte gönderdiğine göre; GAME_DESIGN §19'daki notlar ya da Unity'nin davranışı değişirse etiket de değişir. Google Play'in Data safety cevapları GAME_DESIGN §20'de. Formu proje sahibi doldurur ve onaylar; uzman okuması taslağın §6'sında.

**Do you or your third-party partners collect data from this app?** → **Yes, we collect data from this app.**

| Data type | Collected | Linked to the user | Used for tracking | Purposes | Kim gönderiyor, ne |
|---|---|---|---|---|---|
| **Purchases > Purchase History** | Yes | **Yes** | No | App Functionality; Analytics; Other Purposes | Unity IAP 5.4.4'ün her satın alma adımındaki olayı: ürün, fiyat, para birimi, işlem kimliği, App Store'un JWS kaydı. IDFV ile birlikte gittiği için bağlı. Unity'nin amaçları sahtekârlık ve güvenlik (App Functionality), mali raporlama ve yasal yükümlülük (Other Purposes). İzin verilmişse Aptabase'in `purchase_complete` notu da (yalnız ürün, kimliksiz); tür için tek cevap var. |
| **Identifiers > Device ID** | Yes | **Yes** | No | App Functionality; Analytics; Other Purposes | Unity'nin olayındaki IDFV (`identifierForVendor`). Reklam kimliği (IDFA) okunmuyor (aşağıda). |
| Identifiers > User ID | No | | | | Hesap yok; Unity Authentication ve Unity Gaming Services başlatılmıyor. |
| **Usage Data > Product Interaction** | Yes | **No** | No | Analytics | Aptabase'in notları, yalnız oyuncu "İzin ver" derse: açılış, bölüm başı ve sonu (numara, kalan hamle), tablo ve oda, mağaza açıldı, satın alma. Kalıcı kimlik yok. Apple'ın "isteğe bağlı beyan" istisnası düzenli toplanan veriye uymadığı için izne bağlı olsa da beyan edilir. |
| **Location > Coarse Location** | Yes | **No** | No | Analytics | Aptabase sunucusu IP'den ülkeyi ve bölgeyi çıkarıp saklar, IP'yi saklamaz (izinle). |
| **Diagnostics > Other Diagnostic Data** | Yes | **Yes** | No | App Functionality; Analytics | Unity'nin olayındaki cihaz modeli, iOS sürümü, sistem dili, açılış zamanı, toplam disk alanı, oyunun kurulma zamanı. |
| Diagnostics > Crash Data, Performance Data | No | | | | Aptabase'in hata raporu kapalı; IAP'nin teşhis ölçümleri UGS başlamadan gitmiyor. |
| Financial Info > Payment Info | No | | | | Ödeme App Store'un penceresinde; oyun kart bilgisini görmüyor. |
| Contact Info, Health & Fitness, Financial Info (öteki), Location > Precise, Sensitive Info, Contacts, User Content, Browsing History, Search History, Usage Data > Advertising Data ve Other Usage Data, Other Data | No | | | | Oyun bunları toplamıyor. |

- **Tracking: hiçbir türde Yes yok.** App Tracking Transparency sorulmaz; `NSUserTrackingUsageDescription` Info.plist'ten silinir (§3.1).
- **Ürün sayfasında görünecek:** "Data Linked to You": Purchases, Identifiers, Diagnostics. "Data Not Linked to You": Usage Data, Location.
- **Oyunun içi aynı şeyi söylüyor (2026-10-25):** gizlilik kartının istatistik paragrafı ve izin sorusu artık Aptabase'in bağlantının IP adresinden yaklaşık konumu (ülke ve bölge) çıkardığını da söylüyor; etiketin Coarse Location satırı değişmedi. Metin taslak, onay sahipte (GAME_DESIGN §23, 2026-10-25).
- **Privacy Policy URL:** sitedeki İngilizce politika (`<GIZLILIK_URL>`, sahibin adresi); yayımlanmadan etiket gönderilemez.
- **Dayanak (2026-10-24, bu dal):**
  - IDFV: IAP'nin Swift eklentisi (`UnityStoreKitPlugin.swift`) `identifierForVendor`'ı okur, C# tarafı (`PlayerData.CreatePlayerIdentityAsync`) her olaya koyar.
  - IDFA: aynı eklentide `ASIdentifierManager` çağrısı ve `import AdSupport` var, ama C# tarafı onu yalnız Unity'nin kendi rıza arabirimi "reklam izni verildi" derse çağırır (`EndUserConsent`, `adsIntentGranted`). Oyun bu arabirimi hiç çağırmıyor, yani IDFA okunmuyor ve App Tracking Transparency sorusu yok. Firebase köprüsü (`UnityFirebaseAnalytics.m`) Firebase yokken ve reklam izni yokken boş döner; Unity Ads köprüsü (`UnityPurchasingAttribution.m`) Unity Ads yokken hiçbir şey yapmaz.
  - Aptabase 0.3.1: yalnız C#; UnityWebRequest ile `https://eu.aptabase.com`; cihaz kimliği okumuyor; işletim sistemi adı ve sürümü gider, cihaz modeli yalnız "iPadOS" ile "iOS"u ayırmak için okunur, gönderilmez (`Environment.cs`).
  - Gizlilik manifestleri: §5'teki "Gizlilik manifesti" satırı.

## 5. Kod denetimi: iOS için yapılanlar ve kararını bekleyenler

| Konu | Durum | Ayrıntı |
|---|---|---|
| Android'e özgü API'ler | Düzeltildi | Titreşim (`Haptics.cs`) ve telefonun dili (`PhoneLanguage.cs`) yalnızca Android'in Java çağrılarını kullanıyordu. iOS'ta dokunuşlar artık Taptic Engine'den (UIImpactFeedbackGenerator: hafif ve sert, Android'in "click" ve "heavy click"i yerine), dil etiketi iPhone'un tercih edilen dillerinden geliyor (`zh-Hant-HK` gibi). Yerel kod `Assets/Plugins/iOS/` altında ve yalnızca iOS derlemesine giriyor; C# tarafı `#if UNITY_IOS && !UNITY_EDITOR` arkasında, Android ve Editor'ü değiştirmiyor. |
| Geri tuşu | Sorun yok | iPhone'da geri tuşu yok, ama her ekranın ekranda bir geri yolu var: her kartta çarpı, bölümde duraklatma diski, yapbozda geri düğmesi, öne asılmış tablo dokununca geri gider, alttaki çubuk salona götürür. |
| Güvenli alan, çentik, Dynamic Island | Sorun yok | Oyun `Screen.safeArea`'nın dört kenarını da kullanıyor: üst ve alt boşluklar, 2026-10-24'ten beri yan tutulunca yan boşluklar da (`ScreenShape`, `ScreenView.UsableLeft/UsableRight`). Öneri: iPhone testinde alt kenardaki ana ekran jesti tahtadaki kaydırmalarla çakışırsa alt kenar jesti ertelenir (`deferSystemGesturesMode`). |
| Dosya yolu | Karar bekliyor | Oyun zaten `Application.persistentDataPath` kullanıyor (iOS'ta uygulamanın Documents klasörü; iCloud yedeğine girer). Gizlilik kartının 4. paragrafı "Android" diyor; iPhone için Türkçe taslak: "iPhone'unun iCloud yedeklemesi açıksa iOS bu dosyaları kendi yedeğine katabilir." Onaylı metin olduğu ve 13 dili etkilediği için karar proje sahibinin. Destek kartı kayıt dosyasının tam yolunu gösteriyor; iOS'ta bu yol uzun ve oyuncunun ulaşamadığı bir yer. Öneri: test sürümlerinde dosya, Files uygulamasında "Puzzle" klasöründe görünsün (`UIFileSharingEnabled`), kart da bu yeri söylesin; böylece testçiler `attempts.jsonl` dosyasını gönderebilir. |
| Yaşam döngüsü ve ses | Sorun yok, bir öneri | Arka plana alınınca (iOS'ta Denetim Merkezi ve gelen arama da sayılır) parmak bırakılıyor, müzik bekletiliyor, ilerleme kaydediliyor. Ses ayarı iOS'ta "ambient": sessiz anahtarına uyar, oyuncunun kendi müziğiyle karışır. Öneri: oyuncunun kendi müziği çalarken oyunun müziği susabilir (Apple'ın önerisi). |
| AOT, IL2CPP | Sorun yok | JSON okuma Unity'nin `JsonUtility`'si (yansıma kullanmaz); motor DLL'lerinde yansıma yok; IL2CPP Android'de telefonda kanıtlandı ve iOS aynı ayıklama düzeyini alıyor. |
| Metin girişi | Yok | Oyunda klavye açan bir alan yok. |
| iPad | Titreşim düzeltildi; ekran Editör'de denendi, koleksiyon düzeltildi | Proje sahibi iPhone ve iPad'i seçti; oyun ilk olarak iPad Pro 12.9" (6. nesil) üzerinde denenecek. iPad'de Taptic Engine yok: ayarlar kartı orada Titreşim anahtarını göstermez (`Haptics.DeviceHasNone`, `DeviceAbilities.OfThisBuildWithoutVibrator`); titreşim motoru olmayan Android tabletlerde de öyle. **Ekran (2026-10-24):** her ekran ve kart, yürüyüşleriyle birlikte, üç iPad boyutunda görüntülendi: 12.9" Pro 2048×2732, 10. nesil 1640×2360, mini 1488×2266; dikey, 24 noktalık durum çubuğu ve 20 noktalık ana ekran çizgisiyle (`ScreenShots -screenSizes 2048x2732+48+40,...`). iPad, katlının iç ekranı gibi düzenleniyor: yüksekliği tasarımın 9:16 telefonu kadar (640 nokta), genişliği 420–480 nokta; artan genişlik duvar oluyor, salonda tablonun iki yanına lamba asılıyor. Kesik, taşan, durum çubuğunun ya da ana ekran çizgisinin altında kalan bir şey yok; yazılar elde telefondakinden büyük (12.9" Pro'da bir nokta 0,41 mm, Pixel'in dış ekranında 0,18 mm). Bulunan tek sorun koleksiyondu: bir odanın beş tablosu üç sıra hâlinde ikişer asılıyor, her biri yaklaşık 67 nokta yüksek kalıyor, duvar boş görünüyordu (katlıda 135). Artık 410 noktadan geniş her ekran üçer asıyor (`CollectionLayout`); 12.9" Pro'da tablolar 95–130 nokta. Ayarlar kartının iPad'deki hâli (Ses ve Müzik) ilk kez görüntülendi (`settings-without-vibrator`) ve doğru. Telefonlar değişmedi. Bilerek bırakılanlar: rafın üstündeki yazı (ilk denemede "Sürükle ya da...") tahta çerçevesinin alt kenarına biniyor, katlıda da öyle ve parmak tahtada nişan alırken soluklaşıyor; mağaza tek sütun ve ortada (iki sütun 3:4'e sığmıyor). Telefon testleri T440–T449 (PHONE_TESTS F22). **İkinci bakış (2026-10-24):** içe aktarmanın ve dışa aktarımın kalan değişiklikleri ayıklandı (görüntülerin iOS satırları ve dışa aktarımın iOS ayarları tutuldu), ana dal birleştirildi. Her şey Pixel'de ve üç iPad'de yeniden görüntülendi; 12.9" Pro bir de durum çubuğu gizliyken (`2048x2732+0+40`, oyunun gerçek hâli büyük olasılıkla bu) ve bütün yürüyüşlerle. Düzen değişmedi. Salon 3:4'te 9:16 telefonu kadar dar (tablo 128–136 nokta, tezgâh küçük); bilerek bırakıldı, karar iPad'deki izlenime bağlı (T441). `MusicCheck` bu Editör'de sayfa çevrilmesi bitmeden basıyordu, düzeltildi. Xcode projesi yeniden dışa aktarıldı (4,4 dk). **Yan çevirme (2026-10-24):** iPad artık dört yöne döner ve tam ekran istemez (§3.1.1). |
| App Store'daki dil listesi | Öneri | App Store sayfasının dil listesi, uygulamanın beyan ettiği yerelleştirmelerden çıkar; Unity'nin projesi yalnızca İngilizce beyan ediyor. Oyunun 13 dili beyan edilebilir (Info.plist'te `CFBundleLocalizations`; gerekirse boş `.lproj` klasörleri). İlk yüklemede App Store Connect'te görülür. |
| Gizlilik manifesti | Bakıldı (2026-10-24); ilk yüklemede yine bakılacak | Unity 6 motor için kendi manifestini ekliyor, eklentilerinkini (IAP 5.4.4 dahil) `UnityFramework/PrivacyInfo.xcprivacy`'de birleştiriyor; izleme yok (`NSPrivacyTracking = false`). v1 veri topluyor ama hiçbir manifest toplanan veri türü söylemiyor; etiket elle doldurulur (§3.1.2, §4.1). Eksik bir "required reason" beyanı olursa App Store Connect e-postayla söyler. |

## 6. Yedek: bulut Mac

Yalnızca Mac kullanılamazsa ya da güncel Xcode'u çalıştıramayacak kadar eskiyse. Unity'nin dışa aktarması yine PC'de yapılır; bulutta yalnızca Xcode adımı koşar. Ücretsiz kotalar değişebilir; seçmeden önce sağlayıcının sayfasına bakılır.

- **Codemagic:** bireysel hesapta ayda 500 dakika ücretsiz macOS M2; sonrası dakikası 0,095 USD. Özel (private) GitHub deposuna bağlanabilir.
- **GitHub Actions macOS:** depo özel olduğu için ücretsiz kota GitHub Free'de ayda 2.000 dakika; macOS dakikaları kotadan çok daha hızlı düşer (Linux'un 10 katı), yani pratikte ayda ~200 macOS dakikası.
- **Unity Build Automation:** ücretsiz katmanda ayda 100 Mac dakikası; proje Unity Cloud'a bağlanır, imza dosyalarını ister.

## 7. iPhone'da ilk denemede bakılacaklar

İlk TestFlight derlemesi kurulunca bunlar `docs/PHONE_TESTS.md`'ye iPhone testleri olarak eklenir: açılış ve Unity açılış ekranı; dil (Türkçe iPhone'da Türkçe); sesler, müzik ve sessiz anahtarı; titreşimin hissi (hafif ve sert dokunuş Android'dekine yakın mı); Dynamic Island ve ana ekran çubuğuyla çakışma; arka plana alıp dönme, Denetim Merkezi, gelen arama; gizlilik ve destek kartları; uygulamayı kapatıp açınca ilerlemenin yerinde olması. v1'den beri ayrıca: izin sorusu, mağazanın yerel fiyatları, sandbox hesabıyla satın alma, geri yükleme ve Ask to Buy (T485, T486; iPad'de F27).
