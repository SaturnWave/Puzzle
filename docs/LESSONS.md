# Hata Kaydı ve Dersler

**Ne için:** geliştirmede yapılan ve geç fark edilen hatalar. Her biri için ne olduğu, neden olduğu, neden yakalanmadığı ve bir daha olmaması için konan kural ve denetim. Sahibin isteği (2026-10-29): "Hata yapılan noktaları kaydet; sonraki geliştirme aşamalarında bu hata yaşanmamalı." Yeni bir hata bulununca en üste eklenir. Kurallar sonraki her aşamada geçerlidir; Claude'un oturumları ve ajanları onları depodaki `CLAUDE.md`'den de okur.

## Kurallar

1. **Gönderilen şey denenir.** Mağazaya giden App Bundle, test APK'sından başka bir üründür: bölünmüş APK'lar, kurulumla gelen asset pack, Play Asset Delivery kütüphanesi, Play'in imzası ve otomatik koruması (`com.pairip`) yalnız onda var. Test APK'sında geçen bir deneme paketi kanıtlamaz. Her yüklemeden önce paketin kendi APK seti telefonda açılır. Kapalı teste ya da üretime gitmeden önce paket dahili testten Play üzerinden de kurulup açılır (`docs/PLAY_RELEASE.md` 10).
2. **Kütüphanenin tanımladığı, kanıtsız çıkarılmaz.** Bir kütüphanenin manifestte tanımladığı bileşen (servis, sağlayıcı, alıcı, etkinlik) ya da izin çıkarılmadan önce iki şey yapılır. Önce kütüphanenin kodu okunur: o bileşene nasıl dokunuyor (`javap -c`; sınıf adı, `setComponentEnabledSetting`, `getComponentEnabledSetting`, `startService`, `bindService`). Sonra değişiklik, gönderilecek paket biçiminde telefonda denenir.
3. **Denetim bir varsayımı kilitlemez.** "X olmamalı" diyen bir derleme denetimi, X'in çalışırken kullanılmadığının kanıtını belgesinde gösterir. Kanıtsız bir yasak hatayı korumaya alır: versionCode 1'de denetim, oyunu kapatan servisin olmamasını şart koşuyordu.
4. **Varsayım olgu gibi yazılmaz.** Doğrulanmamış bir öngörü belgede "denenmedi" diye işaretlenir ve bir telefon testine bağlanır.
5. **Yazılan test koşulmadan iş bitmiş sayılmaz.** Bir değişikliğin telefonda kanıtı "Bekliyor" durumundaysa o değişiklik mağazaya gitmez. Zaman baskısı gerekçe değildir.
6. **Çökme okunabilir olur.** Paket yerel kütüphanelerin sembol tablolarını taşır. Telefondaki çökme kaydı okunur: `adb logcat -b crash`, `adb shell dumpsys dropbox`, Unity'nin iş parçacığı dökümündeki "Pending exception" satırı.
7. **Metin derlemeyle uyuşur.** Mağaza metni ve sürüm notları yalnız o derlemenin yaptığını söyler. Kapalı olan ya da kurulmamış bir özellik (açılış kaydındaki "not set up", "turned off" satırları) yüklemeden önce sahibe söylenir: ya kurulur ya metin düzeltilir.

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
| H4 | 28 Ekim, ilk paket: yükleme anahtarının parolaları Unity'nin anahtar ayarlarına verildi. Unity onları derleme klasöründeki sekiz dosyaya düz metin olarak yazdı (Gradle'ın `build.gradle`'ı dahil). | Sır Unity'ye hiç verilmez. Paket Unity'nin hata ayıklama anahtarıyla derlenir, sonra `jarsigner` ile imzalanır; parola yalnız aracın kendi ortamına gider (`-storepass:env`). | `docs/PLAY_RELEASE.md` 3.3 |
| H3 | 26 Ekim: Play Billing 9.0.0'ın kayıtçısı, Google'ın datatransport'uyla telefonun model, sistem, ülke, dil ve ağ bilgisini gönderiyordu; oyunun ayar satırı "yalnız satın almalar çıkar" diyordu. Telefonda ağ izlenince bulundu (T186). | Gizlilik metninin söylediği, telefonda ağ izlenerek doğrulanır. Kayıt arka ucu manifestten çıkarıldı (`ManifestLoggingBackend`); paket `aapt` ile denetleniyor. | `docs/DEVLOG.md`, PHONE_TESTS T186 |
| H2 | Satın alma denemesi: Unity bir önceki derlemenin Gradle manifestini yerinde bıraktı; bir iznin geri verilmesi yeni pakete geçmedi (INTERNET geri çekili kaldı). | Her derlemeden önce tutulan manifest silinir (`AndroidBuild.TakeAwayTheKeptManifest`); paketin izinleri derlemeden sonra `aapt` ile okunur. | `docs/research/iap-and-analytics.md` 3.4 |
| H1 | Unity'nin oyun içi denetimleri (`PlayModeCheck`, `MusicCheck`) `-quit` ile başlatılınca hiçbir şey denetlemeden bitiyordu. | Bu denetimler `-quit` olmadan çalıştırılır; çıkış kodu değil, sonuç satırı okunur. | `unity/Assets/Editor/PlayModeCheck.cs`, `MusicCheck.cs` (kullanım satırı) |
