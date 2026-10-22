# iOS Derlemesi — Adım Adım Plan

> **Durum (2026-10-23):** Kod iOS'a hazırlandı ve Xcode projesini dışa aktaran araç (`IosBuild.Run`) yazıldı. Bu PC'deki Unity'de iOS Build Support modülü henüz yok; araç şimdilik "modül yok" deyip hiçbir şeyi değiştirmeden duruyor. Proje sahibinin Mac'i (Tailscale'de `arcans-macbook-pro`, 100.94.229.108) ve Apple Developer Program üyeliği var; Mac şimdilik kapalı, bu yüzden §3.2–§3.4 henüz denenmemiş bir plandır. iOS işi `feature/ios` dalında yürür. İlk TestFlight derlemesi, §2'deki "bir kez" listesi bitince ve Mac açıkken (Tailscale'e bağlı, Remote Login açık) yapılabilir.

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
   - developer.apple.com → Certificates, Identifiers & Profiles → Identifiers → **+** → App IDs → App → Explicit; bundle ID'yi yazın. Capabilities'ten hiçbirini işaretlemeyin (oyun Game Center, bildirim, iCloud kullanmıyor).
   - appstoreconnect.apple.com → Apps → **+** → New App: Platform iOS; Name (App Store'da benzersiz, en çok 30 karakter; sonra değişebilir); Primary Language; Bundle ID (listeden); SKU (yalnızca size görünür, ör. `museum-puzzle-ios`).
6. **Yükleme için App Store Connect API anahtarı.**
   - appstoreconnect.apple.com → Users and Access → Integrations → App Store Connect API → Team Keys → **Generate API Key** (ilk kez erişim isteği gerekebilir; bunu yalnızca Account Holder yapar).
   - Ad: "Mac upload"; Access: **Admin**. Admin, xcodebuild'in dağıtım sertifikasını ve profilini bu anahtarla kendisi oluşturabilmesi içindir; yalnızca yükleme yapılacaksa App Manager yeter.
   - `.p8` dosyası yalnızca bir kez indirilebilir. Mac'e kendiniz koyun: `mkdir -p ~/.appstoreconnect/private_keys && chmod 700 ~/.appstoreconnect/private_keys`, dosya `~/.appstoreconnect/private_keys/AuthKey_<KEY_ID>.p8`, sonra `chmod 600`. Bu dosya gizlidir: depoya, sohbete ya da e-postaya konmaz; ajan onu okumaz, yalnızca yolunu verir.
   - Bize iki değeri yazın: **Key ID** ve **Issuer ID** (aynı sayfanın üstünde).
7. **Uygulama ikonu.** 1024×1024 PNG, saydamlıksız, köşeleri yuvarlatılmamış. TestFlight ikonsuz derlemeyi reddeder. Projede henüz ikon yok (Android paketi de Unity'nin varsayılanıyla çıkıyor); ikonu proje sahibi verir, projeye ajan ekler.

## 3. Her derlemede (ajan yapar)

### 3.1 Windows: Xcode projesi

```
"C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe" -batchmode -quit -projectPath "C:/Users/arcan/puzzle/unity" -executeMethod Puzzle.Client.EditorTools.IosBuild.Run -logFile out/unity/ios-build.log
```

- Önce ProjectSetup'ın işini yapar (atlas, katalog, sahne); sonra `unity/Build/iOS/` klasörünü baştan yazar. Android paketiyle aynı sahne ve içerik.
- Ayarlar: bundle ID ve ad `GameIdentity`'den; dikey; iPhone ve iPad; ARM64, IL2CPP; kod ayıklama düzeyi Android'deki gibi; otomatik imza; derleme numarası Android'in sürüm koduyla aynı (Xcode, App Store Connect'te varsa yükleme sırasında artırır).
- **En düşük iOS 15.0:** Unity 6.6'nın desteklediği ve Xcode'un hâlâ derlediği en düşük sürüm. iPhone 6s ve ilk SE'den bu yana her iPhone'u kapsar; oyun daha yeni bir iOS özelliğine ihtiyaç duymuyor (dokunuşlar iOS 10'dan beri var).
- **Info.plist:** `ITSAppUsesNonExemptEncryption = false` (oyun Apple'ınkinden başka şifreleme kullanmıyor, hiç ağ çağrısı yapmıyor; her yüklemede şifreleme sorusu sorulmaz). İzleme, kamera, mikrofon ve konum açıklaması bırakılmaz.
- İlk dışa aktarmada Unity aktif hedefi iOS'a çevirir ve dokuları yeniden içe aktarır (uzun sürebilir); sonraki Android derlemesi hedefi geri çevirir.

### 3.2 PC → Mac

Windows 11'in kendi `tar` ve `ssh` araçlarıyla, tek akışta (`<kullanıcı>` §2'deki ad; ad çözülmezse `100.94.229.108`):

```
tar -C unity/Build -cf - iOS | ssh <kullanıcı>@arcans-macbook-pro "rm -rf ~/puzzle-ios/iOS && mkdir -p ~/puzzle-ios && tar -C ~/puzzle-ios -xf -"
ssh <kullanıcı>@arcans-macbook-pro "cd ~/puzzle-ios/iOS && find . -name '*.sh' -exec chmod +x {} +"
```

Klasör 1–2 GB olabilir. Windows'tan gelen dosyalar çalıştırma iznini taşımaz; ikinci satır Xcode'un betiklerine izni verir (başka araçların da izin isteyip istemediği ilk denemede görülecek). Yedek yol: `scp -r unity/Build/iOS <kullanıcı>@arcans-macbook-pro:~/puzzle-ios/` (çok sayıda küçük dosyada daha yavaş).

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
| Gizlilik politikası URL'si | Hazır değil | Oyun sitesi (SaturnWave.Apps) henüz yayında değil. Metni yalnızca Android'in Google yedeğinden söz ediyor; iPhone'un iCloud yedeği için bir cümle eklenmeli. |
| Destek URL'si | Hazır değil | Aynı sitede destek sayfası; herkese açık iletişim e-postası henüz yok. |
| App Privacy | **Data Not Collected** | Doğru, çünkü oyun hiçbir şey göndermiyor. Reklam, satın alma ya da analitik gelince bu beyan ve gizlilik metni birlikte değişir. |
| Yaş derecelendirmesi | Anket | Şiddet, kumar, kullanıcı içeriği, sohbet yok: 4+ beklenir. |
| Ekran görüntüleri | Hazırlanacak | iPhone 6.9" (1320×2868 ya da 1290×2796) zorunlu. iPad de desteklendiği için iPad 13" (2064×2752 ya da 2048×2732) de zorunlu. Her boyutta 1–10 görüntü; küçük ekranlarınki büyükten ölçeklenir. Unity'deki ScreenShots aracı bu boyutlarda görüntü alabilir. |
| Ad, alt başlık, açıklama, anahtar kelimeler, kategori | Hazırlanacak | Kategori: Games → Puzzle. Ad en çok 30 karakter ve benzersiz. |
| İçerik hakları | Beyan | Tablolar kamu malı (public domain); üçüncü taraf içerik sorusunda belirtilir. |
| Fiyat ve ülkeler | Karar | Ücretsiz; ülke seçimi proje sahibinin. |
| İkon | Bekliyor | §2 madde 7. |

## 5. Kod denetimi: iOS için yapılanlar ve kararını bekleyenler

| Konu | Durum | Ayrıntı |
|---|---|---|
| Android'e özgü API'ler | Düzeltildi | Titreşim (`Haptics.cs`) ve telefonun dili (`PhoneLanguage.cs`) yalnızca Android'in Java çağrılarını kullanıyordu. iOS'ta dokunuşlar artık Taptic Engine'den (UIImpactFeedbackGenerator: hafif ve sert, Android'in "click" ve "heavy click"i yerine), dil etiketi iPhone'un tercih edilen dillerinden geliyor (`zh-Hant-HK` gibi). Yerel kod `Assets/Plugins/iOS/` altında ve yalnızca iOS derlemesine giriyor; C# tarafı `#if UNITY_IOS && !UNITY_EDITOR` arkasında, Android ve Editor'ü değiştirmiyor. |
| Geri tuşu | Sorun yok | iPhone'da geri tuşu yok, ama her ekranın ekranda bir geri yolu var: her kartta çarpı, bölümde duraklatma diski, yapbozda geri düğmesi, öne asılmış tablo dokununca geri gider, alttaki çubuk salona götürür. |
| Güvenli alan, çentik, Dynamic Island | Sorun yok | Oyun `Screen.safeArea`'nın üst ve alt boşluklarını zaten kullanıyor; yalnızca dikey oynandığı için yan kesikler gerekmiyor. Öneri: iPhone testinde alt kenardaki ana ekran jesti tahtadaki kaydırmalarla çakışırsa alt kenar jesti ertelenir (`deferSystemGesturesMode`). |
| Dosya yolu | Karar bekliyor | Oyun zaten `Application.persistentDataPath` kullanıyor (iOS'ta uygulamanın Documents klasörü; iCloud yedeğine girer). Gizlilik kartının 4. paragrafı "Android" diyor; iPhone için Türkçe taslak: "iPhone'unun iCloud yedeklemesi açıksa iOS bu dosyaları kendi yedeğine katabilir." Onaylı metin olduğu ve 13 dili etkilediği için karar proje sahibinin. Destek kartı kayıt dosyasının tam yolunu gösteriyor; iOS'ta bu yol uzun ve oyuncunun ulaşamadığı bir yer. Öneri: test sürümlerinde dosya, Files uygulamasında "Puzzle" klasöründe görünsün (`UIFileSharingEnabled`), kart da bu yeri söylesin; böylece testçiler `attempts.jsonl` dosyasını gönderebilir. |
| Yaşam döngüsü ve ses | Sorun yok, bir öneri | Arka plana alınınca (iOS'ta Denetim Merkezi ve gelen arama da sayılır) parmak bırakılıyor, müzik bekletiliyor, ilerleme kaydediliyor. Ses ayarı iOS'ta "ambient": sessiz anahtarına uyar, oyuncunun kendi müziğiyle karışır. Öneri: oyuncunun kendi müziği çalarken oyunun müziği susabilir (Apple'ın önerisi). |
| AOT, IL2CPP | Sorun yok | JSON okuma Unity'nin `JsonUtility`'si (yansıma kullanmaz); motor DLL'lerinde yansıma yok; IL2CPP Android'de telefonda kanıtlandı ve iOS aynı ayıklama düzeyini alıyor. |
| Metin girişi | Yok | Oyunda klavye açan bir alan yok. |
| iPad | Karar bekliyor | iPad'de Taptic Engine yok; Titreşim düğmesi orada bir şey yapmaz (gizlemek ya da bırakmak). Geniş ekran (3:4) "daha çok duvar gösterir" diye tasarlandı ama hiç denenmedi; mağaza görüntülerinden önce iPad boyutunda bakılmalı. Yalnızca iPhone seçilirse iPad görüntüleri de gerekmez. |
| App Store'daki dil listesi | Öneri | App Store sayfasının dil listesi, uygulamanın beyan ettiği yerelleştirmelerden çıkar; Unity'nin projesi yalnızca İngilizce beyan ediyor. Oyunun 13 dili beyan edilebilir (Info.plist'te `CFBundleLocalizations`; gerekirse boş `.lproj` klasörleri). İlk yüklemede App Store Connect'te görülür. |
| Gizlilik manifesti | İlk yüklemede bakılacak | Unity 6 motor için kendi manifestini ekliyor; oyun izleme yapmıyor, veri toplamıyor. Eksik bir "required reason" beyanı olursa App Store Connect e-postayla söyler. |

## 6. Yedek: bulut Mac

Yalnızca Mac kullanılamazsa ya da güncel Xcode'u çalıştıramayacak kadar eskiyse. Unity'nin dışa aktarması yine PC'de yapılır; bulutta yalnızca Xcode adımı koşar. Ücretsiz kotalar değişebilir; seçmeden önce sağlayıcının sayfasına bakılır.

- **Codemagic:** bireysel hesapta ayda 500 dakika ücretsiz macOS M2; sonrası dakikası 0,095 USD. Özel (private) GitHub deposuna bağlanabilir.
- **GitHub Actions macOS:** depo özel olduğu için ücretsiz kota GitHub Free'de ayda 2.000 dakika; macOS dakikaları kotadan çok daha hızlı düşer (Linux'un 10 katı), yani pratikte ayda ~200 macOS dakikası.
- **Unity Build Automation:** ücretsiz katmanda ayda 100 Mac dakikası; proje Unity Cloud'a bağlanır, imza dosyalarını ister.

## 7. iPhone'da ilk denemede bakılacaklar

İlk TestFlight derlemesi kurulunca bunlar `docs/PHONE_TESTS.md`'ye iPhone testleri olarak eklenir: açılış ve Unity açılış ekranı; dil (Türkçe iPhone'da Türkçe); sesler, müzik ve sessiz anahtarı; titreşimin hissi (hafif ve sert dokunuş Android'dekine yakın mı); Dynamic Island ve ana ekran çubuğuyla çakışma; arka plana alıp dönme, Denetim Merkezi, gelen arama; gizlilik ve destek kartları; uygulamayı kapatıp açınca ilerlemenin yerinde olması.
