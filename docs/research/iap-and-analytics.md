# Satın Alma ve Analiz: Deneme Derlemesi, Gizlilik ve Kararlar

**Tarih:** 2026-10-24. **Durum:** deneme bitti; karar proje sahibinde. Oyuna hiçbir şey girmedi. Deneme, işi bitince silinen ayrı bir kopyada yapıldı (`D:/puzzle-iap-spike`, ana daldaki 6fdd691'den).

**Dayandığı belgeler:** aynı günün araştırma notu, bütün kaynakları ve URL'leriyle: [iap-and-analytics-notes.md](iap-and-analytics-notes.md). Bu belgedeki [S..] ve [L..] numaraları o notun sonundaki tabloya gider. Denemenin kendi kanıtları (izin dökümleri, Gradle raporları, boyut tablosu, Unity günlük satırları, deneme kodu) yerelde `out/iap-spike/evidence/` klasöründe; depoya girmiyor. Kısa özetleri §8'de.

**Etiketler**
- **FACT**: resmî kaynakta, bizim depomuzda, bu denemenin ölçümünde ya da Unity paketinin kaynak kodunda gördük. Hangisi olduğu yanında yazar.
- **REPORTED**: üçüncü taraf, satıcının kendi iddiası ya da arşiv kopyası; doğrulanmadı.
- **ESTIMATE**: bizim çıkarımımız ya da önerimiz.

**Kısa sözlük**
- **IAP:** Unity'nin uygulama içi satın alma paketi, `com.unity.purchasing`.
- **PBL:** Google Play Billing Library, Google'ın ödeme kütüphanesi. IAP onu içine alır.
- **UGS:** Unity Gaming Services, Unity'nin bulut hizmetleri. "Bulut projesi" bu hizmetler için Unity'nin sitesinde açılan proje.
- **Birleşik manifest:** APK'nın içindeki son izin listesi. Unity'nin, bizim ve bütün kütüphanelerin istekleri burada birleşir.
- **IDFV:** iPhone ve iPad'in bir şirketin uygulamalarına verdiği kimlik. Reklam kimliği değildir, ama cihazı tanır.

---

## 1. Kısaca

| Soru | Cevap |
|---|---|
| **İnternet izni (INTERNET)** | IAP 5.4.4 ekleyince birleşik manifeste Google'ın bir kütüphanesinden INTERNET geliyor. Bizim derlemenin izin adımı onu **siliyor**: APK'da INTERNET yok. Gradle'ın raporu bunu açıkça yazıyor ("REJECTED from transport-backend-cct"). Silme adımı kapatılınca INTERNET geri geliyor. FACT (deneme) |
| **Gelen yeni izinler** | `com.android.vending.BILLING` (Google Play'in ödeme izni) ve `ACCESS_NETWORK_STATE` (ağ var mı diye bakma). İkisi de oyuncuya sorulmayan sıradan izinler. Konum izni yok. FACT (deneme) |
| **Boyut** | APK 294,7 MB'tan 302,7 MB'a: **+8,0 MB (%2,7)**. Telefona açılınca yaklaşık **+26 MB**. Farkın çoğu derlenmiş C# kodu. FACT (deneme) / ESTIMATE (telefondaki yer) |
| **UGS ve bulut projesi** | **Gerekmiyor.** Editörün oyun kipinde, sahte mağazayla, UGS başlatılmadan ve bulut projesi olmadan mağazaya bağlandı ve deneme ürünü `coins_test` geldi. Derleme yalnız bir uyarı yazıyor: "Bulut projesi yok, Google Play lisans anahtarı yok". FACT (deneme). Gerçek Google Play'de, telefonda denenmedi |
| **Ağ trafiği** | Ödeme işini Play Store uygulaması ve iPhone'da StoreKit yapıyor. IAP'nin kendi teşhis verisi UGS başlatılmadıkça hiç gitmiyor. **Ama 5.4.4, her satın alma adımını Unity'nin sunucusuna yolluyor**: UGS'den ve bulut projesinden bağımsız, kapatma anahtarı yok. İçinde fiş, ürün, fiyat, cihaz modeli, dil ve iPhone'da IDFV var. Android'de INTERNET olmadığı için çıkamaz; **iOS'ta çıkar**. 5.3.1'de bu kod yok. FACT (paketin kaynak kodu; ağ dinlemesiyle görülmedi) |
| **Öneri** | Satın alma gelirse **IAP 5.3.1** ve INTERNET silinmiş kalsın. Analiz gelirse **Aptabase**, oyuncunun izniyle. Ayrıntı §4 ve §5. ESTIMATE |

---

## 2. Deneme nasıl yapıldı

1. Ana daldan atılacak bir kopya açıldı (`git worktree`, 6fdd691), `node tools/unity/sync-engine.js` çalıştırıldı. Sahibin dosyalarından hiçbiri kopyalanmadı.
2. **Karşılaştırma derlemesi:** önce kopya hiç değiştirilmeden `AndroidBuild.Run` ile derlendi. Böylece fark yalnız IAP'den gelir.
3. **IAP eklendi:** `Packages/manifest.json`'a `com.unity.purchasing` **5.4.4**, Unity'nin kendi paket kaydından. Kaydın verdiği SHA-1 ile indirilen paket aynı. Yanında gelenler: `com.unity.services.core` 1.18.0, `com.unity.services.deployment` 1.7.2, `com.unity.services.deployment.api` 1.1.3. FACT
4. **En küçük kod:** `IapSpikeProbe` yalnız `IAP_SPIKE` tanımı olan derlemede var. UGS'yi başlatmadan mağazaya bağlanıyor, `coins_test` adlı tek bir tüketilen ürünü soruyor ve sonucu günlüğe yazıyor. **Satın alma başlatmıyor.** `IapSpikeCheck` aynı işi editörün oyun kipinde yapıp sonucu yazıyor. Hiçbir hesaba girilmedi, bulut projesi bağlanmadı.
5. **İki IAP derlemesi:** biri izin adımı olduğu gibi (INTERNET siliniyor), biri silme satırı kapatılarak. Her APK'nın izinleri Unity'nin kendi Android araçlarıyla (`aapt` 36.0.0) döküldü. Gradle'ın birleşik manifest raporu saklandı.
6. Kopya silindi (`git worktree remove`, ardından kalan klasör).

**Yapılmayanlar:** telefona kurulmadı (sahibin telefonuna dokunulmadı), gerçek bir Google Play satın alması denenmedi (Play Console'da uygulama ve lisanslı test hesabı yok), iPad'de ağ dinlenmedi, IAP 5.3.1 derlenmedi.

---

## 3. Sonuçlar

### 3.1 İzinler

| İzin | Bugünkü oyun (23 Ekim APK'sı) | Karşılaştırma (IAP yok) | IAP 5.4.4, INTERNET silinmiş | IAP 5.4.4, silme kapalı |
|---|---|---|---|---|
| `VIBRATE` (titreşim) | var | var | var | var |
| `DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION` (Android kütüphanesinin kendi içi) | var | var | var | var |
| `com.android.vending.BILLING` | — | — | **yeni** | **yeni** |
| `ACCESS_NETWORK_STATE` | — | — | **yeni** | **yeni** |
| `INTERNET` | — | — | — | **var** |

FACT (deneme, `aapt dump permissions`).

- **INTERNET'i kim istiyor:** silme kapalıyken Gradle raporu iki kaynak gösteriyor: Unity'nin kendi kütüphanesi ve Google'ın `transport-backend-cct` 3.1.8 kütüphanesi. Silme açıkken ikincisi için "REJECTED" yazıyor: `tools:node="remove"` işaretimiz kütüphaneden geleni de siliyor. Araştırmanın beklentisi [L1][L2] doğrulandı. FACT (deneme)
- **ACCESS_NETWORK_STATE:** aynı Google kütüphanesinden ve `transport-runtime`'dan geliyor. Oyuncuya sorulmaz. Ağ olmadan bir işe yaramaz; istersek bunu da aynı adımla silebiliriz, ama Google'ın kütüphanesi bu izni okumaya çalışırsa ne olacağı denenmeli. ESTIMATE
- **Konum:** `play-services-location` 19.0.0 APK'ya giriyor ama izin eklemiyor. FACT (deneme)
- **Manifeste gelen diğer parçalar:** Google Play'in ödeme hizmetini bulma kaydı (`queries`), iki ödeme ekranı (`ProxyBillingActivity`, `ProxyBillingActivityV2`), Google Play hizmetlerinin bir ekranı (`GoogleApiActivity`), Google'ın kayıt gönderme hizmeti (`datatransport` servisi ve alıcısı). FACT (deneme). Bu kayıt gönderme hizmeti INTERNET'siz bir şey gönderemez; telefonda sessizce mi düştüğü, hata mı yazdığı logcat'te bakılacak. ESTIMATE
- **Hedef:** `minSdk` 26, `targetSdk` 36; değişmedi. FACT (deneme)

### 3.2 Boyut

| | Bayt |
|---|---|
| Bugünkü oyunun APK'sı (23 Ekim, daha eski bir sürüm) | 294.396.348 |
| Karşılaştırma derlemesi (6fdd691, IAP yok) | 294.744.716 |
| IAP 5.4.4, INTERNET silinmiş | 302.726.850 |
| IAP 5.4.4, silme kapalı | 302.726.878 |
| **Fark (IAP'li − karşılaştırma)** | **+7.982.134 (≈ +8,0 MB, %2,7)** |

FACT (deneme).

Farkın parçaları (APK içindeki sıkıştırılmış boyut; parantezde açılmış boyut):

| Parça | Fark |
|---|---|
| `libil2cpp.so`: C# kodunun makine koduna çevrilmiş hâli (IAP, Unity hizmetleri, Newtonsoft JSON) | +5,06 MB (+16,9 MB) |
| `Managed`: C# kodunun tanım verisi | +1,23 MB (+4,1 MB) |
| `classes.dex`: Java/Kotlin kodu (Google'ın ödeme kütüphanesi ve Play hizmetleri) | +0,77 MB (+2,2 MB) |
| `libunity.so`: Unity motorunun ağ modülü gibi açılan parçaları | +0,55 MB (+1,1 MB) |
| Diğer veri ve kaynaklar | +0,37 MB (+1,2 MB) |

FACT (deneme). Manifest yerel kütüphanelerin kurulumda açılmasını söylüyor (`extractNativeLibs="true"`). Bu yüzden telefonda kaplanan yer yaklaşık APK farkı artı açılan iki kütüphane, yani **≈ +26 MB**. ESTIMATE. Mağazadan indirme boyutu APK farkına yakın olur. ESTIMATE. Unity'nin kod ayıklamasını (managed stripping) sıkılaştırmak farkı küçültebilir; denenmedi.

### 3.3 UGS ve bulut projesi

- **Editörün düzenleme kipinde** deneme şu hatayı verdi: `StoreException: Store Factory not present to create any new stores`. Bu UGS'yle ilgili değil: IAP mağazalarını yalnız oyun çalışırken kaydediyor. FACT (deneme)
- **Oyun kipinde**, boş bir sahnede: `IAP spike check: PASSED: connected without Unity Gaming Services (services Uninitialized); products fetched: coins_test ($0.01)`. Mağaza Unity'nin sahte mağazası; fiyat onun uydurduğu fiyat. Tek IAP uyarısı, bağlantı olayına bir dinleyici koymadığımızı söylüyor. FACT (deneme)
- **Android derlemesi** her seferinde şu uyarıyı yazdı ve başarıyla bitti: "This project is not linked to a Unity Cloud project, so it has no Google Play license key… so Google Play transactions can be verified." Satışı engellemiyor; Unity'nin sunucuda fiş doğrulaması için anahtar istiyor. Uyarı ayarlardan kapatılabiliyor. FACT (deneme) / ESTIMATE (engellemediği)
- **Paketin kaynak kodu:** Unity hizmetlerinin çekirdeği (`com.unity.services.core` 1.18.0) yalnız oyun `UnityServices.InitializeAsync()` çağırırsa başlıyor; bulut projesi yoksa o çağrı `UnityProjectNotLinkedException` ile düşüyor. IAP bu çağrıyı kendisi yalnız iki yerde yapıyor: kodsuz satın alma düğmeleri (Codeless IAP) ve Unity'nin web mağazasından dönüş bağlantısı. İkisini de kullanmıyoruz. FACT (paket kaynağı)
- APK'ya Unity hizmetlerinin bir ayar dosyası giriyor (`UnityServicesProjectConfiguration.json`): ortam "production", paket sürümleri; proje kimliği yok. FACT (deneme)
- **Sonuç:** satın alma için UGS ve bulut projesi gerekmiyor. Araştırmanın beklentisi [S4][S5] doğrulandı. Gerçek Google Play'de bunu telefonda bir deneme satın alması kesinleştirir. ESTIMATE

### 3.4 Ağ trafiği: IAP 5.4.4 neyi, ne zaman gönderir

Ağ dinlemesi yapılamadı. Onun yerine paketin bütün çalışma kodunda ağa çıkan yerler okundu (5.4.4 ve karşılaştırma için 5.3.1).

| Yol | Nereye | Bizim kurulumda çalışır mı | Etiket |
|---|---|---|---|
| Ürün, satın alma, onay | Android'de Play Store uygulaması, iPhone'da StoreKit. Ağ işini onlar yapar | Evet | FACT [S20] / REPORTED [S23] |
| IAP'nin teşhis ve süre ölçümleri (telemetry) | Unity hizmetlerinin çekirdeği üzerinden | **Hayır.** Göndericisi yalnız çekirdek başlayınca kuruluyor; o zamana kadar ölçümler bellekte bekliyor | FACT (paket kaynağı: `IapCoreInitializeCallback`, `TelemetryMetricsInstanceWrapper`) |
| Bağlantı ayarları | `insights.unity3d.com` | **Hayır.** Çekirdek başlamalı ve proje kimliği olmalı | FACT (paket kaynağı: `ConnectionsSettingsClient`) |
| **Satın alma olayları** | **`prd.insights.analytics.unity3d.com/v1/ingest/producer`** | **Evet, her satın almada.** Çekirdeğe, proje kimliğine ve rızaya bağlı değil; kapatma ayarı yok | FACT (paket kaynağı: `Stores/Data/Insights/PurchaseEventEmitter.cs`; `PurchaseServiceFactory` onu her zaman ekliyor) |
| Oturum olayları | aynı adres | Hayır. Unity Authentication paketi ister | FACT (paket kaynağı) |
| Ürün eşleme, web mağazası, doğrudan ödeme (D2C), sunucuda fiş doğrulama | `iap.services.api.unity.com`, `webshop.services.api.unity.com`, `transaction-verifier.services.api.unity.com` | Hayır. Oturum belirteci, proje kimliği ya da ayrı bir derleme tanımı ister | FACT (paket kaynağı) |
| Google'ın ödeme kütüphanesinin kendi kayıtları (`datatransport`) | Google | Android'de INTERNET silinmişse hayır | ESTIMATE |

**Satın alma olayı ne zaman gider:** satın alma başlayınca, ödenince, başarısız olunca ve oyun ürünü teslim edince. Her birinde ayrı bir istek. FACT (paket kaynağı)

**İçinde ne var:** olay kimliği ve zamanı; Unity'nin oturum kimliği (cihazda kayıtlıysa); uygulama paketi, uygulama ve Unity sürümü; mağaza; ürün kimliği, adı, açıklaması, fiyatı ve para birimi, işlem kimliği; **mağazanın fişi** (Google'da imzalı satın alma kaydı ve imzası, Apple'da fiş ya da JWS kaydı); cihaz modeli, işletim sistemi sürümü, sistem dili ve dil listesi, cihazın açılış zamanı, toplam disk alanı; oyunun kurulma zamanı; **iPhone ve iPad'de IDFV**. Reklam kimliği (IDFA/GAID) yalnız reklam rızası verilmiş ve Unity'nin uzak ayarı açmışsa ekleniyor; bizde ikisi de yok. FACT (paket kaynağı: `PlayerData.CreatePlayerIdentityAsync`, `Insights/Models`)

**Unity editörüne göre fark:** bizim 6000.6'da olaylar doğrudan bu adrese gidiyor. 6000.7.0a7 ve sonrasında motorun kendi teşhis modülüne veriliyor. FACT (paket kaynağı). O modülün bizim kapalı teşhis ayarlarımızla (`UnityConnectSettings`) ne yaptığına bakılmadı. ESTIMATE

**Android'de:** INTERNET silinmişken istek telefondan çıkamaz. FACT (deneme: izin yok) / ESTIMATE (isteğin sessizce düştüğü; kod hatayı yutuyor).

**iOS'ta:** ağ izni diye bir şey yok; satın alma olduğu anda bu istekler gider. Bu, araştırmanın açık sorusunun cevabı. Ağ dinlemesiyle görülmedi, ama kodda koşulsuz. FACT (paket kaynağı) / ESTIMATE (çalışırken aynı davrandığı)

**IAP 5.3.1:** satın alma olayı kodu, bağlantı ayarları ve oturum olayları yok. Ağa çıkan tek kendi kodu sunucuda fiş doğrulama; o da ayrı bir derleme tanımı ister, yani derlenmez. Teşhis ölçümleri 5.4.4'teki gibi yalnız çekirdek başlarsa gidiyor. FACT (paket kaynağı, 5.3.1). Unity'nin kendi gizlilik sayfası da 5.4 öncesinin kişisel veri toplamadığını söylüyor [S10][S11].

---

## 4. Ne anlama geliyor: IAP sürümü

| | **5.3.1** (öneri) | 5.4.4 |
|---|---|---|
| Google'ın ödeme kütüphanesi | PBL 8.3.0 | PBL 9.0.0 |
| Google'ın son günü (yeni sürüm yüklemek için) | 2027-08-31, uzatmayla 2027-11-01 [S15] | 2028-08-31, uzatmayla 2028-11-01 [S15] |
| Unity'ye satın alma olayı | Yok | Her satın almada; iOS'ta gider |
| iOS'ta "oyun mağaza dışında bir şey göndermez" | Savunulabilir (iPad'de ağ dinlemesiyle doğrulanmalı) | Söylenemez |
| App Store gizlilik etiketi | Büyük olasılıkla "Data Not Collected" | Purchases, Identifiers (IDFV), Usage/Diagnostics; Unity'nin işlediği veri |
| Unity 6000.6'da derlenir mi | Denenmedi (en düşük Unity 2021.3) | Evet (bu deneme) |

**Öneri (ESTIMATE):** satın alma gelirse **5.3.1** ve sürüm sabitlensin. Sebep: 5.4.4'ün satın alma olayları kapatılamıyor ve iOS'ta Unity'ye fiş, fiyat ve IDFV götürüyor; bu, gizlilik kartının "hiçbir şey göndermez" çizgisini ve App Store etiketini bozar. 5.3.1 Google'ın takvimine göre 2027 Ağustos'una kadar yeter. O tarihten önce (2027 başında) yeni bir IAP sürümüne yeniden bakılmalı: Unity bir kapatma ayarı eklemiş olabilir.

Seçenek olarak düşünülen ama önerilmeyen: 5.4.4'ü projeye kopyalayıp gönderen satırı silmek. Her IAP güncellemesinde yeniden yapmak gerekir ve Unity'nin paketini değiştirmek bakım yükü getirir. ESTIMATE

---

## 5. Analiz servisleri: karşılaştırma ve öneri

Hangi servis olursa olsun:
- **Rıza gerekir.** Apple 5.1.1(ii), veri anonim sayılsa bile kullanım verisi için oyuncunun iznini istiyor [S25]. AB'de cihazda bir şey saklamak ya da okumak, kesin gerekli değilse rıza ister; kural uygulamaları ve SDK'ları da kapsıyor [S65]. FACT
- **INTERNET geri gelir.** O zaman Android'de de IAP 5.4.4'ün satın alma olayları çıkabilir hâle gelir. Analiz gelse bile 5.3.1 önerisi geçerli. ESTIMATE

| | Unity Analytics | Firebase (Google Analytics) | GameAnalytics | **Aptabase** |
|---|---|---|---|---|
| Ne toplar | IP, kurulum kimliği, IDFV; standart olaylar kendiliğinden [S39][S41] | Uygulama kimliği, reklam kimliği (kapatılabilir), kaba konum, **satın almaları kendiliğinden** [S46] | Reklam kimliği varsayılan açık (kapatılabilir), rastgele kullanıcı kimliği, iOS'ta IDFV [S54] | Cihaz kimliği yok; sunucuda günlük değişen bir karma [S60][S62] |
| Rıza | Rıza gelene kadar uykuda; geri alma ve silme isteği var [S40] | Rıza kipi var; varsayılan değer yok, biz kurarız [S47] | Rızayı tamamen biz yönetiriz [S55] | Satıcı "rıza gerekmez" diyor (REPORTED [S59]); Apple'a göre yine gerekir |
| Ücret | Ayda 50.000 aktif oyuncuya kadar bedava [S43] | Bedava [S52] | Bedava, aktif oyuncu sınırı yok [S57] | Ayda 20.000 olay bedava; 1 milyon olay ayda 20 dolar; kendimiz de barındırabiliriz [S61][S63] |
| Ağsızken | 5 MB'a kadar diske yazar, sonra yollar [S42] | Yaklaşık saatte bir paket [S49] | Kuyrukta bekletir [S56] | Dakikada bir paket; kapanışta kaybolabilir [S62] |
| Ek bağ | UGS ve bulut projesi şart [S4]; IAP olayları Unity Analytics'e akar [S41] | Google hesabı ve ayar dosyaları | Hesap ve oyun anahtarı | AB (Almanya) bölgesi seçilebilir; veri en çok 5 yıl [S60] |
| D1/D7 (geri dönme oranı) | Ölçer | Ölçer | Ölçer | Kendisi ölçmez; ölçmek için kendi kurulum kimliğimizi koymamız gerekir |

**Sıralama ve öneri (ESTIMATE):** (1) **Aptabase**, AB bölgesinde ya da kendi sunucumuzda: veri en az, cihaz kimliği yok, Unity'ye ve Google'a bağ yok. (2) Unity Analytics: bedava ve rızaya kadar uykuda, ama UGS ve bulut projesi istiyor. (3) GameAnalytics: bedava, oyun ölçüleri hazır; reklam kimliği kapatılmalı. (4) Firebase: en geniş, ama reklam kimliği ve satın almaları kendiliğinden toplaması "reklam takibi yok" çizgimize en uzak.

Kabaca hacim: günde 1.000 aktif oyuncu × 2 oturum × 10 olay × 30 gün ≈ ayda 600 bin olay; Aptabase'de ayda 20 dolar. ESTIMATE

---

## 6. Ne değişir

### 6.1 Oyun içi gizlilik metni (`words/tr.json` ve 13 dil) [L4]

| WordId | Bugün | Yalnız satın alma (5.3.1, INTERNET silinmiş) | Analiz de gelirse |
|---|---|---|---|
| `SettingsPrivacyLine` | "Oyun hiçbir şey göndermez" | Android'de doğru kalır. iOS'ta iPad ağ dinlemesinden sonra. Öneri: "Satın almaları mağaza yapar; oyun başka bir şey göndermez" | Rızaya göre değişen satır: "Yalnız izin verirsen oyun istatistik gönderir" |
| `PrivacyParagraph1` | İlerleme, cüzdan, ayarlar, deneme kayıtları yalnız bu telefonda | "Satın aldığın altın da bu telefonda; oyunu silersen gider" eklenir | "yalnızca" sözcüğü analizle çelişir |
| `PrivacyParagraph2` | "Hesap, reklam ağı ve satın alma yoktur; Unity'nin istatistikleri kapalıdır" | Değişmeli: "Satın almaları Google Play ya da App Store yapar; ödeme bilgin oyuna gelmez" [S36][S26] | Servisin adı, ne gittiği, amacı, rızayı geri alma yolu |
| `PrivacyParagraph3` | Kayıtlarda yalnız bu telefona verilmiş rastgele bir kimlik | Değişmez | Kimlik gönderilecekse söylenmeli (Aptabase'de yok) |
| `PrivacyParagraph4` | Android yedeklemesi | Değişmez | Değişmez |
| Yeni anahtarlar | — | Mağaza kartında yerel fiyat (mağazanın verdiği), satın alma onayı, bekleyen ve ebeveyn onayı bekleyen satın alma, iOS'ta "Satın alımları geri yükle" | Rıza ekranı (başlık, metin, "İzin ver" / "Hayır"), ayarlarda rıza anahtarı, "Verilerimin silinmesini iste" |

Paragraf 1–4 sahibin onayladığı metin ("approved text: keep every fact"); her değişiklik yeniden onay ister. FACT [L4][L8]

5.4.4 seçilirse iOS'ta `SettingsPrivacyLine` ve `PrivacyParagraph2` "satın alma bilgisi Unity'ye gider" demek zorunda kalır. ESTIMATE

### 6.2 Oyun sitesi (`games-site`, İngilizce ve Türkçe gizlilik sayfası) [L5]

- "In short / Kısaca": "no purchases", "Unity's analytics are turned off", "does not ask for permission to use the internet" cümleleri değişir. INTERNET silinmiş kalırsa son cümle Android için doğru kalır.
- "Changes / Değişiklikler" bugün şunu söz veriyor: politika, satın alma ya da analiz gelmeden **önce** güncellenecek ve oyun gereken rızayı soracak. Bu söz bağlar: satın alma içeren sürüm yayımlanmadan site güncellenmeli ve tarihi değişmeli. FACT [L5]
- Yeni bölümler: **Satın almalar** (Google ya da Apple işler; ödeme bilgisi bize gelmez; telefonda sipariş kimliği ve ürün tutulur; iadeyi mağaza yapar; altın telefonda durur). Analiz gelirse **Analiz** (sağlayıcı, veri, amaç, rıza, saklama süresi, bölge, KVKK ve GDPR'a göre yurt dışına aktarım, geri alma, silme isteği). ESTIMATE
- İletişim adresi sitede hâlâ yok; Apple ve Google ikisi de politika bağlantısı istiyor [S25][S36]. FACT

### 6.3 App Store gizlilik etiketi (iOS dalı)

- **5.3.1, sunucu yok, analiz yok:** büyük olasılıkla "Data Not Collected". Apple'ın kendisinin topladığı (ödeme) beyan edilmez [S26]. iPad'de ağ dinlemesiyle doğrulanmalı. ESTIMATE
- **5.4.4:** Purchases (satın alma geçmişi), Identifiers (Device ID: IDFV), Diagnostics; Unity'nin topladığı veri bizim adımıza toplanmış sayılır. Unity'nin kendi gizlilik manifestinde toplanan veri türü boş [S9], ama bu bizi kurtarmaz: etiketi biz doldururuz. ESTIMATE
- **Analizle:** Usage Data (Product Interaction), Diagnostics; Unity veya Firebase'de Identifiers ve Coarse Location. Tracking: No. ESTIMATE

### 6.4 Google Play Data safety

- **5.3.1 ve INTERNET silinmiş:** "No data collected" kalabilir: ne bizim kod ne kütüphaneler veri gönderebilir. Google Play'in ödeme bilgisi beyan edilmez [S36]. ESTIMATE
- **5.4.4:** Unity kendi beyanında 5.4 ve sonrası için satın alma geçmişi, cihaz kimlikleri, teşhis vb. "Collected: Yes" diyor [S8]. INTERNET silinmişken Android'den gönderilemez, ama beyan formu kütüphanenin yeteneğine değil, uygulamanın gerçekte gönderdiğine bakar; bu bir yorum, proje sahibi ya da hukuk kararı. ESTIMATE
- **Analizle:** App activity, App info and performance, (Unity ve Firebase'de) Device or other IDs ve yaklaşık konum; "encrypted in transit"; silme isteği yolu. ESTIMATE

### 6.5 Kendi kodumuz ve belgelerimiz

- `AndroidBuild.Permissions`: INTERNET'i silen satır satın almada **kalabilir**. Satırın yorumu ("Ads, purchases or a reporting service bring it back") 5.3.1 için düzeltilmeli. Analiz gelirse satır "Ask" olur. ESTIMATE
- **Denemede görülen tuzak:** silme satırı kaldırılınca Unity'nin önceki derlemede yazdığı manifest kalıyor ve INTERNET silinmiş olarak gelmeye devam ediyor. Ancak `Library/Bee/Android/Prj/IL2CPP/Gradle/unityLibrary/src/main/AndroidManifest.xml` silinip yeniden üretilince INTERNET geri geldi. INTERNET'i geri getiren değişiklikte Gradle projesi temizlenmeli ve izinler APK'dan dökülerek bakılmalı. FACT (deneme)
- `PrivacyWords.cs` ve gizlilik testleri metinle birlikte değişir. GAME_DESIGN §20'deki toplanan veri listesi güncellenir. PHONE_TESTS'e INTERNET'siz satın alma, geri yükleme, bekleyen ve ebeveyn onaylı satın alma, iade sonrası hak ve rıza aç/kapat testleri eklenir. ESTIMATE [L8]

---

## 7. Proje sahibinin kararları

1. **IAP sürümü:** 5.3.1 (öneri: Unity'ye satın alma olayı gitmez; 2027 Ağustos'una kadar yeter) mi, 5.4.4 mü (iki yıl daha uzun ömür; iOS'ta Unity'ye satın alma verisi gider).
2. **Analiz gelsin mi?** Gelirse hangisi (öneri: Aptabase). D1/D7 için oyuncunun izniyle bir kurulum kimliği mi, yoksa en sade gizlilik ve tutma ölçümü yok mu?
3. **Gizlilik metni:** onaylı paragrafların (1–4) yeni hâli ve yeni ayarlar satırı. Taslağı Claude hazırlar; onay sahibin.
4. **Sunucu yok kararı (gelir raporu §7.3, seçenek A) sürsün mü?** Sürerse: iade edilen altın oyuncuda kalır; oyunu silen ya da telefon değiştiren oyuncu satın aldığı altını kaybeder. Bu, mağaza sayfasında ve destek metninde açıkça yazılmalı.
5. **Tüketilmeyen ürünler** (Çırak Çantası, Hami Yolu): iOS'ta "Satın alımları geri yükle" düğmesi gerekir. Apple'da aile paylaşımı ürün başına açılır ve **bir kez açılınca kapatılamaz**; öneri kapalı kalması [S28].
6. **Play Console ve App Store Connect:** gerçek satın alma denemesi için uygulama kaydı, deneme ürünleri ve lisanslı test hesabı gerekiyor. Bunları hesap sahibi açar; ben açamam.
7. **iPad'de ağ dinlemesi:** "iOS'ta hiçbir şey gitmez" demeden önce, sahibin Mac'inde bir ağ dinleme aracıyla (ör. Proxyman) iPad'deki deneme derlemesine bakılmalı.
8. **Hukuk:** analiz gelirse KVKK (yurt dışına aktarım) ve GDPR metninin bir uzmana okutulması.

---

## 8. Kanıt

- **Derlemeler:** üçü de `AndroidBuild.Run`, `-buildTarget Android`, Unity 6000.6.4f1. Karşılaştırma 27,9 dakika (ilk içe aktarma dahil), IAP'li 24,4 dakika, silme kapalı 3,1 dakika. Hepsi "succeeded". FACT
- **İzin dökümü** (`aapt` 36.0.0, Unity'nin Android SDK'sından), IAP'li ve INTERNET silinmiş APK: `VIBRATE`, `com.android.vending.BILLING`, `DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION`, `ACCESS_NETWORK_STATE`. Silme kapalıyken bunlara `INTERNET` ekleniyor. Bugünkü oyunun APK'sı ve karşılaştırma derlemesi: yalnız `VIBRATE` ve `DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION`. FACT
- **Gradle'ın birleşik manifest raporu**, INTERNET silinmiş: `uses-permission#android.permission.INTERNET … REJECTED from [com.google.android.datatransport:transport-backend-cct:3.1.8]`. Silme kapalı: `MERGED from [com.google.android.datatransport:transport-backend-cct:3.1.8]`. FACT
- **Unity günlüğü:** `IAP spike check: PASSED: connected without Unity Gaming Services (services Uninitialized); products fetched: coins_test ($0.01)`. FACT
- **Paket kaynağı** (5.4.4, kaydın SHA-1'i `215c35b5…`): `Runtime/Stores/Data/Insights/PurchaseEventEmitter.cs` (adres satır 182, doğrudan gönderim satır 213), `Runtime/Stores/Core/Purchasing/PurchaseServiceFactory.cs` satır 115 (olay gönderici her zaman ekleniyor), `Runtime/Stores/Data/PlayerData.cs` (kimlikler), `Runtime/Stores/ServicesCore/IapCoreInitializeCallback.cs` (teşhis ölçümleri yalnız çekirdekle), `Plugins/UnityPurchasing/Android/IAPResolver/IAPAndroidDependencies.cs` (`com.android.billingclient:billing:9.0.0`, Gradle bağımlılığı olarak). 5.3.1'de aynı dosya `billing:8.3.0` diyor ve `Insights` klasörü yok. Unity hizmetlerinin çekirdeği 1.18.0: `Core.Internal/UnityServicesInitializer.cs` (yalnız istenirse başlar), `Registration/CorePackageInitializer.cs` satır 130 (`UnityProjectNotLinkedException`). FACT
