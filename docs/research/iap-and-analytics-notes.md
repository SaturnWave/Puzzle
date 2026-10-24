# Satın alma ve analiz altyapısı: araştırma notu (aşama 0)

> Bu dosya, 2026-10-24 araştırmasının (aşama 0) kaynaklarıyla birlikte değiştirilmemiş kopyasıdır. Deneme derlemesinin sonuçları ve proje sahibi için özet: [iap-and-analytics.md](iap-and-analytics.md).

Tarih: 2026-10-24. Yalnızca araştırma: Unity çalıştırılmadı, depo dosyası değiştirilmedi.
Bütün web sayfalarına 2026-10-24'te bakıldı; "sayfa tarihi" sayfanın kendi "last updated" bilgisidir.

Etiketler:
- **FACT**: resmi kaynakta (Unity, Google, Apple, AB/ABD/kurum sayfası, paket kaydı, kaynak kod) ya da bizim depomuzda yazıyor.
- **REPORTED**: üçüncü taraf, satıcının kendi iddiası ya da arşiv kopyası; doğrulanmadı.
- **ESTIMATE**: benim çıkarımım ya da önerim.

Kaynaklar `[S..]` (web) ve `[L..]` (yerel dosya) diye anılır; URL ve tarihleri en sondaki tabloda.

---

## 0. Kısaca

1. **Paket:** `com.unity.purchasing` en son **5.4.4** (2026-10-02), Unity 2022.3 ve üstü; Google Play Billing Library (PBL) **9.0.0**'ı sarar. FACT [S2][S3]. PBL 9 Google Play'de **2028-08-31**'e kadar kabul edilir (uzatmayla 2028-11-01). FACT [S15]. Unity'nin önceki sürümü 5.3.1 PBL 8.3.0 kullanır; PBL 8'in son günü 2027-08-31. FACT [S2][S15].
2. **UGS ve bulut projesi gerekmiyor**, yalnız mağazanın kendi ödeme sistemiyle (Google Play, App Store) satan ve ürünleri yerel katalogda tanımlayan proje için. Unity'nin v5 geçiş örneği `UnityServices.InitializeAsync` çağırmadan `StoreController.Connect()` yapıyor. FACT [S4][S5].
3. **Gizlilik farkı sürüme göre:** Unity'ye göre **5.4 ve sonrası kişisel veri toplar** (Player ID, Unity Installation ID, cihaz bilgisi, oturum kimliği, ülke). 5.4'ten önceki sürümler kişisel veri toplamaz; yalnız kısa ömürlü (ephemeral) teşhis ve performans verisi gönderir. FACT [S7][S10][S11]. Unity bu toplamanın yalnız D2C kullanılınca mı olduğunu sayfada söylemiyor. FACT [S7]. Kendi teşhis kanalı da var ("existing gateway"). FACT [S2].
4. **INTERNET izni:** PBL 9.0.0, Google'ın `transport-backend-cct` kütüphanesine bağımlı ve bu kütüphanenin manifesti `INTERNET` ile `ACCESS_NETWORK_STATE` ister. FACT [S16][S17]. Yani paket gelince birleşik manifestte INTERNET belirir. Bizim derleme INTERNET'i `tools:node="remove"` ile siliyor; bu, kütüphanelerden geleni de siler. FACT [L1][L2].
5. **Play Billing'in kendisi** Play Store uygulamasıyla süreçler arası çağrıyla (IPC) konuşur; ağ işini Play Store yapar (Google'ın eski belgesi). REPORTED [S23]. Bu yüzden satın almanın INTERNET izni olmadan çalışması beklenir. ESTIMATE. Güncel Google belgelerinde iki yönde de bir cümle bulamadım; **deneme derlemesi ve lisanslı test hesabıyla bir test satın alması şart**.
6. **Fiş doğrulama:** Unity'nin yerel `CrossPlatformValidator`'ı ağsız çalışır. Google ise sunucuda doğrulamayı "strongly recommend" ediyor. FACT [S13][S20][S21]. Sunucumuz olmayınca iadeyi geri alamayız (clawback): altın iade edilse de oyuncuda kalır. ESTIMATE [S21][S29].
7. **Analiz için rıza her durumda gerekiyor:** Apple 5.1.1(ii), veri anonim sayılsa bile kullanım verisi için rıza istiyor. FACT [S25]. AB'de cihazda bilgi saklamak ya da okumak, kesin zorunlu değilse rıza ister. Uygulamalar ve SDK'lar da buna dahil. FACT [S65]. Öneri: açık rıza (opt-in) ekranı ve ayarlarda geri alma. ESTIMATE.
8. **Analiz adayları (ESTIMATE sıralaması):** (a) Aptabase. Gizlilik öncelikli; cihaz kimliği yok; AB bölgesi var; açık kaynak, kendimiz de barındırabiliriz. (b) Unity Analytics. Rızaya kadar uykuda; ayda 50.000 aktif kullanıcıya kadar bedava; Unity'nin bulut projesine ve UGS'ye bağlar. (c) GameAnalytics. Bedava; reklam kimliği varsayılan açık, kapatılabilir. (d) Firebase/GA4F. Reklam kimliği ve Google ekosistemi; "gizlilik öncelikli" çizgimize en uzak.
9. **Belgeler:** satın alma gelince oyundaki gizlilik metninin iki paragrafı ve ayarlar satırı, oyun sitesinin İngilizce ve Türkçe politikası, App Store gizlilik etiketi ve Google Play Data safety değişir. Analiz gelince hepsi baştan yazılır (§4).

---

## 1. Unity In-App Purchasing (com.unity.purchasing)

### 1.1 Sürüm, uyumluluk, bağımlılıklar

| Sürüm | Tarih | En düşük Unity | PBL | Not | Kaynak |
|---|---|---|---|---|---|
| 5.4.4 (latest) | 2026-10-02 | 2022.3 | 9.0.0 | `com.unity.services.deployment` 1.7.2 bağımlılığı eklendi; Codeless IAP yeni projelere kapandı | FACT [S2][S3] |
| 5.4.0 | 2026-06-29 | 2022.3 | 9.0.0 (8.3.0'dan) | D2C, Webshop, Remote Catalog, Purchase Options UI; bunlar `com.unity.services.authentication` 3.7.1+ ister; `services.core` 1.18.0 | FACT [S2] |
| 5.3.1 | 2026-05-27 | 2021.3 | 8.3.0 | 5.4 öncesi gizlilik profili | FACT [S2][S3][S10] |
| 5.2.0-pre.1 | 2026-01-26 | | 8.0.0 → 8.3.0 | | FACT [S2] |
| 5.1.2 | 2025-12-24 | | | "Unity 6.5+ build compatibility" | FACT [S2] |
| 5.0.0 | 2025-08-07 | | 7.1.1 → 8.0.0 | v5 API (StoreController) | FACT [S2] |

- 5.4.4 çalışma zamanı bağımlılıkları: `com.unity.ugui`, `com.unity.services.core` 1.18.0, `com.unity.modules.androidjni`, `jsonserialize`, `unitywebrequest`. FACT [S3].
- Bizim editör 6000.6.4f1 [L7]; 5.4.4 için 2022.3 yeterli. FACT [S3]. 6000.7'den önceki editörlerde IAP teşhis verisini kendi eski kanalından gönderiyor (§1.3). FACT [S2].
- Projede bugün satın alma paketi yok; `UnityPurchasingSettings.m_Enabled: 0`, `UnityAnalyticsSettings.m_Enabled: 0`, Insights ve bulut teşhisi kapalı, `cloudProjectId` boş. FACT [L3][L7].

### 1.2 UGS başlatma ve bulut projesi

- Kurulum sayfası: bulut projesine bağlamak "Remote Catalog, D2C payment providers, Unity Authentication, IAP analytics, or any other Unity service" kullanılıyorsa gerekiyor. Yalnız platformun kendi mağazasıyla satan ve ürünleri yerel katalogda tanımlayan proje bu adımı atlayabilir. FACT [S4] (sayfa: "Last updated a day ago", 2026-10-23 dolayı).
- Aynı sayfanın başlangıç betiği önce `await UnityServices.InitializeAsync()` çağırıyor, ama UGS'nin her IAP kurulumu için zorunlu olduğunu söylemiyor. FACT [S4]. v4'ten v5'e geçiş sayfasının örneği UGS'yi hiç başlatmadan `UnityIAPServices.StoreController()` → `Connect()` → `FetchProducts` → `FetchPurchases` yapıyor. FACT [S5].
- 4.x zamanından forumlarda "requires Unity Gaming Services to have been initialized" uyarısı anlatılıyor. Bu bir uyarı, hata değil. REPORTED (Unity Discussions, [S4] sayfasındaki arama özeti).
- 5.4.4: Android derlemesi, bulut projesinde Google Play lisans anahtarı yoksa ya da proje bağlı değilse uyarı yazar. FACT [S2]. Bunun satışı engellemediğini düşünüyorum. ESTIMATE.
- **Risk (ESTIMATE):** 5.4.4'te Codeless IAP yeni projelere kapandı ve ayarlar sayfası yerel kataloğu "Legacy Catalog" olarak gösteriyor. FACT [S2]. Unity, kataloğu buluta (Remote Catalog + Authentication) taşımaya itiyor. İleride yerel kataloğun kaldırılması ihtimali var. Biz ürünleri kodda `ProductDefinition` ile tanımlarsak bugün etkilenmeyiz.

### 1.3 Ağ üzerinden ne gönderir

| Ne | Ne zaman | Etiket |
|---|---|---|
| Mağaza trafiği: ürün bilgisi, satın alma, onay | Android'de PBL, Play Store uygulamasına IPC ile; iOS'ta StoreKit. Ağ işini mağaza uygulaması ya da işletim sistemi yapar | FACT [S20] (bağlantı Play'e `startConnection` ile); IPC ayrıntısı REPORTED [S23] |
| IAP teşhis verisi (telemetry) | 6000.7.0a7+ editörlerde Insights modülüyle: oturum ve yaşam döngüsü olayları, cihaz ve işletim sistemi bilgisi, proje ve oturum kimlikleri; Developer Data çerçevesiyle yönetilir. Daha eski editörlerde (bizimki) "the existing gateway, unchanged" | FACT [S2] |
| 5.4+ "default personal data": Player ID (Authentication), Unity Installation ID, Device Info, Session IDs, Country | Unity'ye göre "always collected in order for the product to work"; yalnız D2C'de mi, belirtilmemiş | FACT [S7] |
| 5.4+ isteğe bağlı: analytics_id, user_id, IDFA, IDFV, IAP SDK IDFI, e-posta, bağlı üçüncü taraf analiz kimlikleri | Geliştiricinin ya da oyuncunun seçimiyle | FACT [S7] |
| Google Analytics kimlikleri | Google Analytics bağlıysa ya da Developer Data Collection "Recommended" ise; reklam rızası da gerekir (5.4.3 Android, 5.4.4 iOS) | FACT [S2] |
| Satın alma olayları Unity Analytics'e | Analytics SDK projedeyse "The IAP plugin forwards transaction events" | FACT [S41] |
| Remote Catalog, D2C, Webshop | Yalnız bu özellikler açılırsa (oturum açmış oyuncu ister) | FACT [S4][S2] |

- Google Play Data safety beyanı. **5.4'ten önce:** "collect or share" = No; yalnız "App diagnostics" ve "Other app performance data", ikisi de ephemeral, amaç App functionality. FACT [S11]. **5.4 ve sonrası:** yaklaşık konum, e-posta, kişisel kimlikler, satın alma geçmişi, çökme kayıtları, teşhis, cihaz kimlikleri "Collected: Yes"; hiçbiri paylaşılmıyor; silme isteği yolu yok. FACT [S8].
- Apple privacy manifest (`PrivacyInfo.xcprivacy`): 5.4+ toplanan veri türü boş, `NSPrivacyTracking` false; gerekçeli API'ler FileTimestamp C617.1 ve DiskSpace E174.1. FACT [S9]. 5.4 öncesi yalnız FileTimestamp C617.1. FACT [S12].
- Rıza: 5.4+ "This product does not have a consent service"; rıza ya da vazgeçme yolu gerekiyorsa geliştirici kendisi kurar. FACT [S7].
- **Açık soru (ESTIMATE):** 6000.6'da bu eski kanal `UnityConnectSettings` kapalıyken de veri gönderir mi? Unity belgesi söylemiyor. Android'de INTERNET silindiği için hiçbir şey çıkamaz. **iOS'ta ağ izni diye bir şey yok**, gönderebilir. iPad derlemesinde ağ dinlemesi (Proxyman/Charles) yapılmadan "Oyun hiçbir şey göndermez" iOS için söylenemez.

### 1.4 Android manifesti: INTERNET nereden gelir

- `billing-9.0.0.pom` bağımlılıkları: `androidx.activity` 1.2.3, `androidx.core` 1.15.0, `datatransport:transport-api` 3.0.0, **`transport-backend-cct` 3.1.8**, `transport-runtime` 3.1.8, `play-services-base` 18.5.0, `play-services-basement` 18.9.0, **`play-services-location` 19.0.0**, `play-services-tasks` 18.2.0. FACT [S16].
- `transport-backend-cct` manifesti `android.permission.ACCESS_NETWORK_STATE` ve `android.permission.INTERNET` ister. FACT [S17] (firebase-android-sdk kaynak kodu).
- Billing AAR'ının kendi manifesti: `com.android.vending.BILLING` izni; `com.android.vending.billing.InAppBillingService.BIND` için `<queries>`; `ProxyBillingActivity` ve `ProxyBillingActivityV2` etkinlikleri. REPORTED [S18] (bağımsız AAR analizi, 2026-09-26; billing 9.1.0).
- Google Play SDK Index billing sayfası: "No permissions found. This SDK may use permissions that we couldn't detect." Hedef API 35, en düşük 23. FACT [S19].
- Unity'nin Android "Internet Access" ayarı: Auto, bir ağ API'si kullanılıyorsa INTERNET ekler; Require her zaman ekler ve geliştirme derlemelerinde varsayılandır. FACT [S24]. IAP `com.unity.modules.unitywebrequest`'e bağlı [S3]; bu yüzden Auto'da Unity de INTERNET ekler. ESTIMATE.
- Bizim derleme: `AndroidBuild.Permissions` içinde `PermissionLine.Withdraw("android.permission.INTERNET")`. `ManifestPermissions`, `tools:node="remove"` yazarak birleştiricinin bunu "what Unity and the libraries" eklediklerinden de çıkarmasını sağlıyor. FACT [L1][L2].
- **Doğrulanacaklar (ESTIMATE):** (1) birleşik manifestte INTERNET gerçekten yok mu (aapt dump); (2) `ACCESS_NETWORK_STATE` kalıyor; bu sıradan (normal) bir izin, oyuncuya sorulmaz; (3) `play-services-location` 19.0.0 konum izni ekliyor mu; (4) datatransport ağsız kalınca çöküyor mu, yoksa sessizce mi düşüyor (logcat).

### 1.5 Google'ın PBL takvimi

| PBL | Yeni uygulama ve güncelleme son günü | Uzatma | Kaynak |
|---|---|---|---|
| 7 | 2026-08-31 | 2026-11-01 | FACT [S15] |
| 8 (IAP 5.0.0–5.3.1) | 2027-08-31 | 2027-11-01 | FACT [S15] |
| 9 (IAP 5.4.x) | 2028-08-31 | 2028-11-01 | FACT [S15] |

Sayfa üstündeki şerit: "By Aug 31, 2026, all new apps and updates to existing apps must use Billing Library version 8 or later". Tablodaki tarih bir sürümün kabul edildiği son gün; ikisi tutarlı. FACT/ESTIMATE [S15] (sayfa 2026-10-20). En yeni PBL 9.1.0 (2026-06-18); Unity henüz 9.0.0'da. FACT [S14][S2].

**Öneri (ESTIMATE):** 5.4.4. Sebep: PBL 9 iki yıl geçerli; 5.3.1 en geç 2027-08'de yükseltme ister. 5.4'ün kişisel veri beyanı Android'de INTERNET silinmişken pratikte bir şey göndermez. iOS ölçümü (§1.3) "5.3.1'e sabitle" kararını değiştirebilir.

### 1.6 Fiş doğrulama (cihazda mı, sunucuda mı)

- Unity yerel doğrulama: `CrossPlatformValidator(GooglePlayTangle.Data(), AppleTangle.Data(), bundleId)`. Obfuscator, Google Play açık anahtarını ve Apple kök sertifikasını koda gömer. Anahtarı değiştirebilen kullanıcı doğrulamayı aşabilir. REPORTED [S13] (Unity'nin eski 2020.1 kılavuzu). v5'te de var: 5.0.0 örnekleri "Google Play Store - 06 Local Receipt Validation" ve "04 Integrating Self-Provided Backend Receipt Validation"; 5.2.0'da `CrossPlatformValidator` düzeltmesi. FACT [S2].
- Google: satın alma belirtecini (token) "we strongly recommend passing the token to your secure backend server"; satın almayı Play Developer API ile sunucuda doğrulayın. FACT [S20][S21].
- Onay (acknowledge): **3 gün** içinde yapılmazsa satın alma otomatik iade edilir. Bekleyen (PENDING) satın alma onaylanmaz; süre PURCHASED'a geçince başlar. FACT [S20].
- **Bizim için (ESTIMATE):** sunucu yok (§7.3 kararı A). Yerel doğrulama yeter: küçük altın paketlerinde sahtekârlık riski kabul edilebilir. `ConfirmPurchase` hemen ve oyuncu ürünü aldıktan sonra çağrılmalı ki 3 günlük iade tetiklenmesin.

### 1.7 Satın almaları geri yükleme (restore)

- Apple 3.1.1: "you should make sure you have a restore mechanism for any restorable in-app purchases". Satın alınan oyun parası sona eremez. FACT [S25].
- `AppStore.sync()`: "Include some mechanism in your app, such as a Restore Purchases button". Yalnız açık bir kullanıcı eylemiyle çağrılır, çünkü App Store girişi ister. Uygulama yeniden kurulunca işlemler zaten kendiliğinden gelir. FACT [S27].
- Google: `queryPurchasesAsync()` bağlantı kurulunca ve `onResume()`'da çağrılmalı. FACT [S20]. Unity: 5.0.2'den beri `RestoreTransactions` sonuçları `OnPurchasesFetched` ile gelir; 5.4.3'te ürünü hiç alınmamış satın almaların sessizce düşmesi düzeltildi. FACT [S2].
- Hangi ürün geri yüklenir: Apple'da paylaşım ve geri yükleme tüketilmeyen (non-consumable) ürünlere ve aboneliklere özgü. FACT [S28]. Altın paketi tüketilen (consumable) üründür; geri yüklenmez. ESTIMATE.
- **Bizim için (ESTIMATE):** altın paketleri tüketilir. Çırak Çantası (oyuncu başına bir kez) ve Hami Yolu tüketilmeyen ürün olursa iOS'ta "Satın alımları geri yükle" düğmesi gerekir. Bulut kaydı (§7.3 B) yokken oyunu silen oyuncu altınını kaybeder. Bunu gizlilik ve destek metninde açıkça yazmak gerekir.

### 1.8 İadeler

- Google: geliştirici Play Console'dan tam ya da kısmi iade yapar; "Refund and revoke" aboneliği hemen kaldırır. FACT [S32]. Voided Purchases API iptal, iade ve ters ibrazı bildirir ve geri alınacak içeriği gösterir; bu bir sunucu API'si. FACT [S21].
- Apple: oyuncu Apple Destek'ten, reportaproblem.apple.com'dan ya da kart çıkaranından iade ister. App Store sunucusu, tüketilen, tüketilmeyen ve yenilenmeyen ürünlerde sunucumuza `REFUND` bildirimi gönderir. FACT [S29].
- **Bizim için (ESTIMATE):** sunucu yokken tüketilen altının iadesini öğrenemeyiz ve geri alamayız. Bu kabul edilecek bir kayıp. Tüketilmeyen ürün iade edilince cihaz bunu görür: Apple'da hak geri alınır [S28], Google'da ürün `queryPurchasesAsync` listesinden düşer. Oyun hakkı yeniden hesaplamalı.

### 1.9 Aile paylaşımı

- Apple: aile paylaşımı yalnız aboneliklerde ve tüketilmeyen ürünlerde, en çok beş aile üyesine. App Store Connect'te ürün başına açılır ve **açıldıktan sonra kapatılamaz**. Paylaşım biterse uygulama erişimi geri almalı ve arayüz paylaşılabilirliği (`isFamilyShareable`) göstermeli. FACT [S28].
- Google Play: "You can't share in-app purchases and apps downloaded at no charge with your family members." FACT [S33].
- **ESTIMATE:** bizde tüketilmeyen ürün (Çırak Çantası, Hami) aile paylaşımına açılmamalı, çünkü karar geri alınamaz. Varsayılan kapalı kalsın.

### 1.10 Mağaza sayfası ve satın alma akışı kuralları

**Apple**
- 3.1.1: satın alınan para ve kredi sona eremez; geri yüklenebilir ürünler için geri yükleme yolu; rastgele ürünlerde olasılıklar satın almadan önce. FACT [S25].
- 3.1.2(c), abonelik varsa: ücretle ne alındığı açıkça yazılır. FACT [S25].
- Fiyat: `Product.displayPrice`, "the localized string representation of the product price, suitable for display"; yerel ayarı mağaza belirler. FACT [S31].
- Ebeveyn onayı (Ask to Buy): StoreKit 2'de `PurchaseResult.pending`. Onaylanırsa işlem sonradan `Transaction.updates` ile gelir. FACT [S30]. Unity'de `OnPurchaseDeferred` ve "Apple App Store - 02 Handling Deferred Purchases" örneği. FACT [S2].
- Çocuk kategorisi (1.3 ve 5.1.4) ebeveyn kapısı ister ve üçüncü taraf analiz yasaktır. Bizim oyun çocuklara yönelik değil, kategoriye girmiyor. FACT [S25] / ESTIMATE.

**Google**
- Ödeme politikası, bölüm 2: oyun içi dijital ürünler (sanal para, ek can, booster) Google Play faturalandırmasıyla satılır. Bölüm 5: sanal para yalnız satın alındığı oyunda kullanılır. Bölüm 6: "In-app pricing must match the pricing displayed in the user-facing Play billing interface". Bölüm 7: rastgele ürün olasılıkları önceden açıklanır. FACT [S35].
- Fiyat ürün bilgisinden okunur: `queryProductDetailsAsync` yerelleştirilmiş bilgi döndürür; `ProductDetails` önbelleğe alınmamalı. FACT [S20].
- Bekleyen işlemler (pending) tek seferlik ürünlerde desteklenmeli; hak yalnız PURCHASED'da verilir. FACT [S20].
- Ebeveyn onayı: Play'in satın alma onayı (Family Link) yalnız Google Play faturalandırmasından geçen alımlara uygulanır. "Only in-app purchases" seçeneği var. FACT [S34].

**AB ve ABD tüketici kuralları**
- AB CPC Ağı'nın oyun içi sanal para ilkeleri (Mart 2025, IP/25/831): fiyatlar gerçek parayla da gösterilmeli; maliyeti gizleyen uygulamalar, istenmeyen miktarda para almaya zorlama, cayma hakkı, çocuklar dahil savunmasız oyuncular. Bağlayıcı değil, ama uygulama yönünü gösteriyor. REPORTED/FACT [S38].
- ABD FTC'nin Epic kararı (tek dokunuşla ödeme, 245 milyon dolar) ve HoYoverse kararı monetizasyon raporunun §2.6'sında var. FACT [L6].
- **ESTIMATE:** mağaza kartında her altın paketinin yerel gerçek para fiyatı (`displayPrice`); booster ve can satırlarında altının yanında "≈ $x" yok, ama paket kartında var. Her satın almada bizim onay adımımız ve ardından sistem sayfası. iOS'ta ayarlarda "Satın alımları geri yükle".

---

## 2. Play Billing INTERNET izni ister mi, yoksa Play Store'la IPC ile mi konuşur?

- Google'ın eski IAB v3 belgesi: Google Play uygulaması "handles all communication between your application and the Google Play server". Uygulama faturalandırma isteklerini Google Play uygulamasına "IPC method calls" olarak gönderir; uygulamanın bildirmesi gereken izin `com.android.vending.BILLING`. REPORTED [S23] (Google belgesinin arşiv kopyası, eski API).
- Güncel PBL'de istemci Play'e `startConnection` ile bağlanır; servis bağlantısı kopabilir (`SERVICE_DISCONNECTED`, 8.0.0'dan beri otomatik yeniden bağlanma). FACT [S20][S14]. AAR'ın `<queries>` öğesi `InAppBillingService.BIND`. REPORTED [S18]. Bunlar bir servise bağlanmayı, yani IPC'yi gösteriyor. ESTIMATE.
- PBL'in kendisi INTERNET istemez; ama bağımlılığı `transport-backend-cct` ister. FACT [S16][S17]. Bu kütüphane Google'ın teşhis ve kayıt taşıma altyapısı. Billing ağsız olunca yalnız kendi kayıtlarını gönderemez. ESTIMATE.
- Güncel Google belgeleri (integrate, getting-ready, release notes) INTERNET'ten hiç söz etmiyor. FACT [S20][S22][S14].
- PBL 6.0.0 `NETWORK_ERROR` dönüş kodunu ekledi. FACT [S14]. Bu hata Play'in ağ sorunu için; uygulamanın izniyle ilgili değil. ESTIMATE.
- **Sonuç (ESTIMATE):** Play Billing satın almanın INTERNET izni olmadan çalışması çok olası. Kanıt yok: lisanslı test hesabıyla INTERNET'siz bir deneme derlemesinde ürün çekme, satın alma, onay ve `FetchPurchases` denenmeli. Logcat'te datatransport hataları izlenmeli. Bu, raporun aşama 0'ındaki "deneme derlemesi, birleşik manifest" maddesi [L6].

---

## 3. Analiz servisleri

### 3.1 Hukuk çerçevesi

- **Apple 5.1.1(ii):** "Apps that collect user or usage data must secure user consent for the collection, even if such data is considered to be anonymous". Rızayı geri almak kolay olmalı; ücretli işlev rızaya bağlanamaz. 5.1.1(i): uygulama içinde ve App Store Connect'te gizlilik politikası bağlantısı. 5.1.2: takip için ATT izni. FACT [S25].
- **AB ePrivacy 5(3):** EDPB Rehberi 2/2023 sürüm 2 (2024-10-07): kural çerezlerle sınırlı değil, "similar technologies" de girer. Ağ arabirimi olan her cihaz "terminal equipment" sayılır. Saklamak ve okumak ayrı işlemlerdir; bu SDK'ları da kapsar. FACT [S65]. Kesin zorunlu olmayan analiz rıza ister. ESTIMATE (rehberin genel okuması).
- **Fransa CNIL istisnası:** yalnız yayıncı için ve yalnız kitle ölçümü; anonim istatistik; birleştirme ya da paylaşım yok; bir uygulamaya sınırlı kimlik; yaklaşık 13 aylık ömür; oyuncu bilgilendirilir ve itiraz edebilir. Yalnız Fransa'da geçerli; çoğu büyük sağlayıcı bu istisnanın dışında. REPORTED [S66] (CNIL sayfaları, arama özeti).
- **GDPR rolleri:** Unity Analytics işleyici (processor), biz sorumluyuz (controller). FACT [S39]. GameAnalytics da işleyici (Madde 28). FACT [S55].
- **ABD:** CCPA, yıllık geliri 26.625.000 doları aşan (2025-01-01'den beri) ya da 100.000 Kaliforniyalının verisini alan, satan veya paylaşan işletmelere uygulanır. FACT [S67]. Küçük bir bağımsız stüdyo büyük olasılıkla eşiğin altında kalır. Yine de mağaza beyanları ve Apple 5.1.1 her durumda bağlar. ESTIMATE. Diğer eyalet yasaları ayrı eşikler taşır; bu notta incelenmedi. ESTIMATE.
- **COPPA:** değişiklikler 2025-06-23'te yürürlüğe girdi, uyum son günü 2026-04-22. FACT [S68]. Oyun çocuklara yönelik değil; çocuk olduğunu bildiğimiz bir oyuncu yoksa uygulanmaz. Mağazanın hedef kitle beyanı bunu belirler. ESTIMATE.
- **KVKK (geliştirici Türkiye'de):** 6698 sayılı yasanın yurt dışına aktarım maddesi (9), 7499 sayılı yasayla 2024-06-01'den beri değişti (standart sözleşme vb.); yönetmelik 2024-07-10. REPORTED [S69]. ABD ya da AB sunucusuna analiz verisi göndermek bunu tetikleyebilir. ESTIMATE. GAME_DESIGN zaten "KVKK ve GDPR için rıza ekranı ve gizlilik politikası" diyor. FACT [L8].

### 3.2 Karşılaştırma

| | Unity Analytics (UGS) | Firebase / GA4F | GameAnalytics | Aptabase (gizlilik öncelikli, barındırılabilir) |
|---|---|---|---|---|
| Sürüm | `com.unity.services.analytics` 6.3.0, 2026-03-04, Unity 2022.3+. FACT [S44] | Firebase Unity SDK 14.1.0 (23 Ekim; yıl sayfada yok); en düşük Unity 2022.3; Android minSdk 24. REPORTED/FACT [S51][S50] | Unity SDK 8.3.0, 2026-09-30 (Android 7.2.0, iOS 5.1.0). FACT [S58] | `aptabase-unity`, MIT, git URL'siyle kurulur. FACT [S62] |
| Varsayılan toplanan | IP, kuruluma özgü kimlik, User ID (varsayılan kurulum kimliği), Authentication Player ID, IDFV. FACT [S39]. Standart olaylar (`gameStarted`, `gameEnded`…) otomatik. FACT [S41] | Uygulama örneği kimliği, Android reklam kimliği, maskeli IP'den kaba konum, **satın alma ve abonelik olayları (ürün kimliği, adı, fiyatı)**, ekran görüntülemeleri ve oturumlar. FACT [S46] | Reklam kimliği izni varsa reklam kimliği; kullanıcı kimliği Android'de rastgele, iOS'ta IDFV. FACT [S54]. IP teknik kayıtlarda 12 ay. REPORTED [S55] | Cihaz kimliği yok. Sunucuda `SHA(IP + User Agent + günlük tuz)`, tuz 24 saatte atılır. Olaya işletim sistemi ve uygulama sürümü eklenir. FACT (satıcı politikası) [S60][S62] |
| Rıza | 5.0.0'dan beri rızaya kadar uykuda, olayları yok sayar. Unity 6.2+ ve SDK 6.1+'da `EndUserConsent` ile `AnalyticsIntent` Granted/Revoked. Silme `RequestDataDeletion`. Saklama 13 ay. FACT [S40][S39] | Consent Mode v2 dört tür (`analytics_storage`, `ad_storage`, `ad_user_data`, `ad_personalization`); "By default, no consent mode values are set"; manifestte varsayılanlar; AB için `eu_consent_policy`. FACT [S47]. Tamamen kapatma bayrağı var. FACT [S48] | Oyuncu rızası tamamen geliştiricide: "must not access, collect or share any personal data until the player consents". Kapatma `SetEnabledEventSubmission(false)`. FACT [S55][S54] | Satıcıya göre kişisel veri toplamadığı için GDPR/CCPA/PECR'de rıza gerekmez. REPORTED [S59]. **Apple 5.1.1(ii) yine rıza ister** (FACT [S25]); AB'de de cihazda bir şey saklıyorsa 5(3) devreye girer. ESTIMATE |
| Google Play Data safety (satıcının beyanı) | Yaklaşık konum, kişisel kimlikler (kurulum kimliği), satın alma geçmişi (IAP eklentisinden), teşhis ve diğer performans (Shared: Yes), cihaz kimlikleri (Installation ID, IDFV, IDFA); silme isteği: Yes. FACT [S41] | Uygulama örneği kimliği, reklam kimliği, yaklaşık konum, satın almalar, uygulama etkileşimleri. FACT [S46] → Data safety satırları ESTIMATE | Resmi beyan sayfası bulamadım. Cihaz/diğer kimlikler, uygulama etkileşimleri, teşhis. ESTIMATE | Uygulama etkileşimleri ve teşhis; tanımlayıcı yok. ESTIMATE |
| App Store etiketi (ESTIMATE) | Identifiers (User ID / Device ID), Usage Data > Product Interaction, Diagnostics, Location > Coarse, Purchases (IAP olayları gelirse); hepsi "Linked to You" [S26: kişisel veri linked sayılır]; Tracking: No | Aynısı, artı reklam kimliği alınırsa Device ID; Google Signals açılırsa takip riski | Device ID (IDFA kapatılmazsa), Product Interaction, Diagnostics | Product Interaction, Diagnostics; büyük olasılıkla "Not Linked to You" |
| Maliyet | Ayda 50.000 aktif kullanıcıya kadar bedava; sonra aktif kullanıcı başına $0,0036 (150 bin üstü $0,00315…). FACT [S43] | Analytics her iki planda "No-cost". FACT [S52] | AnalyticsIQ Free $0, "No MAU cap"; Pro $49/ay. FACT [S57] | Ayda 20.000 olay bedava; 200 bin $10, 1 milyon $20, 2 milyon $40 … 50 milyon $450 (USD/ay). FACT [S61]. Sunucu AGPLv3, kendimiz barındırabiliriz. FACT [S63]. Docker'la. REPORTED [S64] |
| SDK boyutu | Yalnız C#; bağımlılıklar `services.core` 1.16+, ugui, jsonserialize. FACT [S44]. Küçük (yüzlerce KB). ESTIMATE | Yerel C++ kütüphaneleri, Google Play services bağımlılıkları, External Dependency Manager. Birkaç MB. ESTIMATE [S50] | Yerel Android ve iOS SDK'sı sarılı. FACT [S58]. Yaklaşık 1 MB. ESTIMATE | Yalnız C#, çok küçük. ESTIMATE |
| Çevrimdışı | Bellekte biriktirir, 60 saniyede bir gönderir. Kapanırken ağ yoksa 5 MB'a kadar olayı diske yazar, sonraki oturumda yollar. FACT [S42] | Olaylar yaklaşık 1 saatlik paketlerle gider. FACT [S49]. Çevrimdışı saklama belgede yok; sonra gönderdiği biliniyor. ESTIMATE | Çevrimdışıyken olaylar kuyruğa girer, çevrimiçi olunca gider (C# SDK belgesi). FACT [S56]. 8 saniyelik kuyruk. FACT [S54] | 60 saniyede bir paket. Hata raporları diske yazılmaz, uygulama kapanınca kaybolur. FACT [S62]. Olayların kalıcılığı belgelenmemiş. ESTIMATE: kapanışta kaybolabilir |
| Reklam kimliği olmadan | Evet; reklam kimliği gerekmez (IDFA yalnız beyanda listeli). ESTIMATE [S41] | Evet: toplama kapatılabilir ("can be disabled by configuring Analytics data collection"). FACT [S46]. `AD_ID` izni SDK manifestinden birleşir. FACT [S37]. `tools:node="remove"` ile silmek forumlarda anlatılıyor. REPORTED [S53] | Evet: `EnableAdvertisingIdTracking(false)`; kullanıcı kimliği tamamen rastgele olur. FACT [S54] | Evet; hiç kimlik yok. FACT [S60] |
| Ek bağ | UGS init ve bulut projesi gerekir ("IAP analytics, or any other Unity service"). FACT [S4] | Google hesabı ve `google-services.json` / `GoogleService-Info.plist`. ESTIMATE | Hesap ve oyun anahtarı. ESTIMATE | AB (Almanya) ya da ABD (Virginia) bölgesi; veri en çok 5 yıl saklanır. FACT [S60] |

### 3.3 Bizim oyun için (ESTIMATE)

- İlk ölçüm ihtiyacı oturum süresi, bölüm başına deneme ve kazanma, D1/D7/D30 ve satın alma dönüşümü [L6 §8]. Bunun için kullanıcı düzeyinde kimlik gerekmiyor, ama tutma (D1/D7) için bir kohort anahtarı gerekiyor. Aptabase kalıcı kimlik tutmadığı için D1/D7'yi kendi ölçmez. Kendi rastgele kimliğimizi olaya koyarsak "anonim" iddiası düşer ve olay kişisel veri olur. **Karar noktası:** tutma ölçümü mü, en sade gizlilik mi?
- Kabaca olay hacmi: günlük 1.000 aktif oyuncu × 2 oturum × 10 olay × 30 gün = ayda 600 bin olay. Aptabase'de ayda $20 [S61]; Unity Analytics ve GameAnalytics'te bedava [S43][S57]. ESTIMATE.
- Sıralama: (1) **Aptabase AB bölgesi ya da kendi sunucumuz**: veri en az, rıza ekranı yine var; tutma için ayrı bir "rıza verirse kurulum kimliği" seçeneği. (2) **Unity Analytics**: ekosistemde, bedava, rızaya kadar uykuda; ama UGS/bulut bağı ve IAP olaylarının Unity'ye akması [S41]. (3) **GameAnalytics**: bedava, oyun metrikleri hazır; reklam kimliğini kapatmak şart; benchmark için kimliksizleştirilmiş veri kullanılıyor. REPORTED [S55]. (4) **Firebase**: en geniş ama "satın alma olaylarını otomatik toplar" [S46] ve reklam kimliği. "Oyun reklam takibi yapmaz" çizgisine en uzak.
- Her seçenek INTERNET'i geri getirir. O zaman Android'de de IAP teşhis verisi (§1.3) çıkabilir hale gelir; deneme derlemesinde ağ dinlemesi gerekir.

---

## 4. Belgelerimizde ne değişir

### 4.1 Oyun içi gizlilik metni (`words/tr.json`, 13 dil) [L4]

| WordId | Bugün | Yalnız satın alma gelirse (ESTIMATE) | Analiz de gelirse (ESTIMATE) |
|---|---|---|---|
| `SettingsPrivacyLine` | "Oyun hiçbir şey göndermez" | Android'de INTERNET silinmişse doğru kalır. iOS'ta IAP teşhis verisi ölçülene kadar söylenemez (§1.3). Öneri: "Satın almaları mağaza yapar; oyun başka bir şey göndermez" | "Yalnız izin verirsen oyun istatistiği gönderir" ve rızaya göre değişen satır |
| `PrivacyParagraph1` | ilerleme, cüzdan, ayarlar, deneme kayıtları telefonda | Cüzdana "satın aldığın altın da bu telefonda; oyunu silersen gider" eklenir (§1.7) | Değişmez, ama "yalnız" sözcüğü analizle çelişir |
| `PrivacyParagraph2` | "Hesap, reklam ağı ve satın alma yoktur; Unity'nin istatistikleri kapalıdır." | Değişmeli: "Satın almaları Google Play / App Store yapar; ödeme bilgin oyuna gelmez" (FACT dayanağı [S36][S26]) | Servis adı, ne gittiği, amaç, rızayı geri alma yolu |
| `PrivacyParagraph3` | "rastgele bir kimlik" yalnız telefonda | Değişmez | Kimlik gönderilecekse söylenmeli (Unity: kurulum kimliği; Aptabase: hiçbiri) |
| `PrivacyParagraph4` | Android yedeklemesi | Değişmez | Değişmez |
| Yeni anahtarlar | — | Mağaza kartı fiyat satırı (`displayPrice`), satın alma onayı, bekleyen ve ebeveyn onayı bekleyen durumlar, iade ya da geri alma bildirimi, iOS "Satın alımları geri yükle" | Rıza ekranı (başlık, metin, "İzin ver" / "Hayır"), ayarlarda rıza anahtarı, "Verilerimin silinmesini iste" (Unity'de `RequestDataDeletion`) |

Not: Paragraf 1–4'ün notu "approved text: keep every fact"; değişiklik için proje sahibinin onayı gerekir. FACT [L4][L8].

### 4.2 Oyun sitesi gizlilik sayfası (`games-site/src/content/games.ts`, en ve tr; salt okunur) [L5]

- "In short / Kısaca": "no purchases", "Unity's analytics are turned off" ve "does not ask for permission to use the internet" cümleleri değişir. Satın almalarda INTERNET silinmiş kalırsa son cümle Android için doğru kalır, iOS için ölçülmeli. ESTIMATE.
- "Changes / Değişiklikler" zaten şunu vaat ediyor: "updated before they arrive, and the game will ask for any consent they need". **Politika satın alma ya da analiz içeren sürümden önce güncellenmeli** ve `updated` tarihi değişmeli. FACT [L5].
- Yeni bölümler (ESTIMATE):
  - **Purchases:** satın almaları Google ya da Apple işler; ödeme bilgisi bize gelmez [S36][S26]; telefonda sipariş kimliği ve ürün tutulur; iadeyi mağaza yapar; satın alınan altın telefonda durur.
  - **Analytics:** sağlayıcı, veri türleri, amaç, hukuki dayanak (rıza), saklama süresi (Unity 13 ay [S39], Aptabase en çok 5 yıl [S60]), bölge ve aktarım (GDPR, KVKK [S69]), rızayı geri alma, silme ya da erişim isteği.
  - **Children:** "çocuklar dahil hiç kimseden kişisel veri toplamaz" analizle değişir; oyun çocuklara yönelik değil.
  - **Contact:** iletişim adresi sitede hâlâ yok (README).
- Apple 5.1.1(i) politikanın üçüncü tarafları ve verinin bütün kullanımlarını açıkça saymasını istiyor; uygulama içinde de bağlantı olmalı. FACT [S25]. Google, veri toplamayan uygulamalardan da politika bağlantısı istiyor. FACT [S36].

### 4.3 App Store gizlilik etiketi (iOS dalı; App Store Connect henüz yok)

- "Collect" demek, veriyi gerçek zamanlı isteğe gereğinden uzun süre erişilebilir biçimde cihaz dışına göndermek demek. "You are not responsible for disclosing data collected by Apple." FACT [S26].
- **Yalnız IAP, yerel doğrulama, sunucu yok:** etiket büyük olasılıkla "Data Not Collected" kalır. Unity IAP'nin manifestinde toplanan veri türü yok [S9][S12]. Ama iOS'ta IAP teşhis verisinin (§1.3) Unity'ye gidip gitmediği ölçülmeli. Giderse Diagnostics olarak beyan gerekebilir. ESTIMATE.
- **Analizle:** Usage Data > Product Interaction ("saved place in a game" dahil), Diagnostics, Identifiers > User ID / Device ID, Purchases > Purchase History (satın alma olayı gönderilirse), Location > Coarse (IP'den ülke saklanırsa). Kişisel veri "linked" sayılır. Tracking: No (reklam SDK'sı yok). FACT tanımlar [S26] / ESTIMATE seçim.
- Bizim `PrivacyInfo.xcprivacy` dosyamız da toplanan türleri saymalı; IAP'nin manifesti kendi gerekçeli API'lerini getirir [S9]. ESTIMATE.

### 4.4 Google Play Data safety

- "Collect" demek, uygulamadan cihaz dışına göndermek demek; SDK'lar dahil. Hizmet sağlayıcıya aktarım "sharing" sayılmaz. Ephemeral işleme formda beyan edilir ama mağazada gösterilmez. Google Play faturalandırmasının topladığı ödeme bilgisi beyan edilmez; ama "purchase history" gibi uygulamaya dönen veriler düşünülmeli. Veri toplamayan uygulama da formu doldurur ve politika bağlantısı verir. FACT [S36].
- **Yalnız IAP, INTERNET silinmiş:** "No data collected" kalabilir; ne bizim kod ne SDK veri gönderebilir. 5.4+ için Unity'nin "Yes" beyanları [S8], gönderim yolu kapalıyken uygulanmaz diye düşünüyorum. Proje sahibi ya da hukuk kararı. ESTIMATE.
- **Analizle:** App activity > App interactions; App info and performance > Diagnostics; Device or other IDs (kurulum kimliği); Financial info > Purchase history (satın alma olayı gönderilirse); Location > Approximate (Unity/Firebase). "Encrypted in transit: Yes"; silme isteği yolu (Unity: Yes [S41]). ESTIMATE seçim; FACT tanımlar [S36].
- Firebase ya da GameAnalytics reklam kimliğini getirirse Play Console'daki reklam kimliği beyanı ve `AD_ID` izni de değişir. FACT [S37] / ESTIMATE.

### 4.5 Kendi kodumuz ve belgelerimiz (yan etki, ESTIMATE)

- `AndroidBuild.Permissions` (INTERNET Withdraw) satın almada kalabilir; analizle "Ask" olur. Satırdaki "nothing leaves the phone" yorumu da değişir [L1].
- GAME_DESIGN §20 (satır 957–959) toplanan veri ve onaylı metin; PHONE_TESTS'e INTERNET'siz satın alma, geri yükleme, bekleyen ve ebeveyn onaylı satın alma, iade sonrası hak, rıza aç/kapat testleri [L8].
- Simülatöre "ödeyen oyuncu" değişkeni (rapor §8 aşama 0) bu notun dışında.

---

## 5. Deneme derlemesinde ölçülecekler (ESTIMATE)

1. Android, IAP 5.4.4, INTERNET silinmiş: birleşik manifest (aapt dump permissions), lisanslı test hesabıyla tüketilen ürün satın al → onayla → yeniden aç → `FetchPurchases`; logcat'te datatransport ve IAP uyarıları; çökme yok.
2. Aynısı INTERNET açıkken: ağ dinlemesi; IAP'nin Unity'ye gönderdiği istekler (alan adları, içerik). `UnityConnectSettings` kapalıyken de gidiyor mu?
3. iPad (iOS dalı): StoreKit sandbox satın alma; ağ dinlemesiyle Unity'ye giden istekler; "Data Not Collected" etiketi savunulabilir mi?
4. AAB boyut farkı (IAP öncesi ve sonrası); `play-services-location` ve başka izinlerin gelip gelmediği.
5. 5.3.1 ile 5.4.4 arasında aynı ölçüm: iOS'ta fark varsa sürüm kararı.

---

## 6. Kaynaklar

Hepsine 2026-10-24'te bakıldı.

| ID | Kaynak | URL | Sayfa tarihi |
|---|---|---|---|
| S1 | Unity IAP paket kılavuzu (5.4.4) | https://docs.unity3d.com/Packages/com.unity.purchasing@5.4/manual/index.html | sürüm 5.4.4 |
| S2 | Unity IAP CHANGELOG | https://docs.unity3d.com/Packages/com.unity.purchasing@5.4/changelog/CHANGELOG.html | girdiler 2025-08-07 … 2026-10-02 |
| S3 | Unity paket kaydı, com.unity.purchasing (JSON) | https://packages.unity.com/com.unity.purchasing | 5.4.4: 2026-10-03T00:47Z; 5.3.1: 2026-05-27 |
| S4 | Set up and initialize IAP | https://docs.unity.com/en-us/iap/set-up-in-app-purchasing | "a day ago" (≈2026-10-23) |
| S5 | Upgrade from IAP v4 to v5 | https://docs.unity.com/en-us/iap/upgrade-to-iap-v5 | tarih yok |
| S6 | IAP privacy and consent | https://docs.unity.com/en-us/iap/privacy-and-consent | "4 months ago" (≈2026-06) |
| S7 | IAP privacy overview (5.4+) | https://docs.unity.com/en-us/iap/privacy-and-consent/overview | tarih yok |
| S8 | IAP Google Play data safety (5.4+) | https://docs.unity.com/en-us/iap/privacy-and-consent/google-play-data-safety | tarih yok |
| S9 | IAP Apple privacy manifest (5.4+) | https://docs.unity.com/en-us/iap/privacy-and-consent/apple-privacy-manifest | tarih yok |
| S10 | IAP legacy privacy overview (<5.4) | https://docs.unity.com/en-us/iap/privacy-and-consent/legacy-overview | tarih yok |
| S11 | IAP legacy Google Play data safety (<5.4) | https://docs.unity.com/en-us/iap/privacy-and-consent/legacy-google-play-data-safety | tarih yok |
| S12 | IAP legacy Apple privacy manifest (<5.4) | https://docs.unity.com/en-us/iap/privacy-and-consent/legacy-apple-privacy-manifest | tarih yok |
| S13 | Unity IAP receipt validation (eski kılavuz, arama özeti) | https://docs.unity3d.com/2020.1/Documentation/Manual/UnityIAPValidatingReceipts.html | 2020.1 kılavuzu |
| S14 | Play Billing Library release notes | https://developer.android.com/google/play/billing/release-notes | 2026-10-20 |
| S15 | Play Billing deprecation FAQ | https://developer.android.com/google/play/billing/deprecation-faq | 2026-10-20 |
| S16 | billing 9.0.0 POM (Google Maven) | https://dl.google.com/android/maven2/com/android/billingclient/billing/9.0.0/billing-9.0.0.pom | PBL 9.0.0: 2026-05-19 |
| S17 | transport-backend-cct AndroidManifest.xml (kaynak) | https://raw.githubusercontent.com/firebase/firebase-android-sdk/main/transport/transport-backend-cct/src/main/AndroidManifest.xml | main dalı, 2026-10-24 |
| S18 | akanjs, "FCM·Play Billing AAR closure" analizi (REPORTED) | https://github.com/akan-team/akanjs/blob/329bd79d830ab49b38f5ab88a93351fd9c21254d/pkgs/@akanjs/native/docs/research/android-aar-closure.md | 2026-09-26 |
| S19 | Google Play SDK Index, Google Play Billing | https://play.google.com/sdks/details/com-android-billingclient-billing | önbellek 2026-10-21 |
| S20 | Integrate the Google Play Billing Library | https://developer.android.com/google/play/billing/integrate | 2026-10-20 |
| S21 | Play Billing: Fight fraud and abuse (security) | https://developer.android.com/google/play/billing/security | 2026-10-20 |
| S22 | Play Billing: Getting ready | https://developer.android.com/google/play/billing/getting-ready | 2026-10-20 |
| S23 | Google "Implementing In-app Billing (IAB v3)" arşiv kopyası (REPORTED) | https://iut-fbleau.fr/docs/android/google/play/billing/billing_integrate.html | eski (IAB v3) |
| S24 | Unity Android Player settings, Internet Access | https://docs.unity3d.com/6000.3/Documentation/Manual/class-PlayerSettingsAndroid.html | Unity 6.3 kılavuzu |
| S25 | Apple App Review Guidelines (1.3, 3.1.1, 3.1.2, 5.1.1, 5.1.2, 5.1.4) | https://developer.apple.com/app-store/review/guidelines/ | tarih görünmüyor |
| S26 | Apple App privacy details | https://developer.apple.com/app-store/app-privacy-details/ | tarih yok |
| S27 | Apple `AppStore.sync()` | https://developer.apple.com/documentation/storekit/appstore/sync() (JSON: https://developer.apple.com/tutorials/data/documentation/storekit/appstore/sync().json) | tarih yok |
| S28 | Apple Supporting Family Sharing in your app | https://developer.apple.com/documentation/storekit/supporting-family-sharing-in-your-app | ©2026 |
| S29 | Apple Handling refund notifications | https://developer.apple.com/documentation/storekit/handling-refund-notifications | ©2026 |
| S30 | Apple `Product.PurchaseResult.pending` | https://developer.apple.com/documentation/storekit/product/purchaseresult/pending | tarih yok |
| S31 | Apple `Product.displayPrice` | https://developer.apple.com/documentation/storekit/product/displayprice | tarih yok |
| S32 | Google Play Console Help, refunds | https://support.google.com/googleplay/android-developer/answer/2741495 | tarih yok |
| S33 | Google Play Help, Family Library | https://support.google.com/googleplay/answer/7007852 | tarih yok |
| S34 | Google Play Help, Purchase approvals | https://support.google.com/googleplay/answer/7039872 ve https://support.google.com/googleplay/answer/15775086 | ©2026 |
| S35 | Google Play Payments policy | https://support.google.com/googleplay/android-developer/answer/9858738 | tarih görünmüyor |
| S36 | Google Play Data safety form help | https://support.google.com/googleplay/android-developer/answer/10787469 | son not 2023-12-05 |
| S37 | Google Play Advertising ID help | https://support.google.com/googleplay/android-developer/answer/6048248 | uygulama 2022-04-01 |
| S38 | European Commission IP/25/831, CPC key principles on in-game virtual currencies | https://ec.europa.eu/commission/presscorner/api/files/document/print/en/ip_25_831/IP_25_831_EN.pdf ; ilkeler PDF: https://commission.europa.eu/document/download/8af13e88-6540-436c-b137-9853e7fe866a_en?filename=Key+principles+on+in-game+virtual+currencies.pdf | Mart 2025 (arama özeti) |
| S39 | Unity Analytics privacy overview | https://docs.unity.com/en-us/analytics/privacy-and-consent/privacy-overview | "9 months ago" (≈2026-01) |
| S40 | Unity Analytics SDK compliance (opt-in) | https://docs.unity.com/en-us/analytics/privacy-and-consent/manage-data-privacy/sdk-compliance-opt-in | tarih yok |
| S41 | Unity Analytics Google Play Data safety | https://docs.unity.com/en-us/analytics/privacy-and-consent/google-data-safety | tarih yok |
| S42 | Unity Analytics SDK behaviour | https://docs.unity.com/en-us/analytics/sdks-and-apis/sdk-behaviour | tarih yok |
| S43 | Unity Gaming Services pricing | https://unity.com/products/gaming-services/pricing | ©2026, tarih yok |
| S44 | Unity paket kaydı, com.unity.services.analytics (JSON) | https://packages.unity.com/com.unity.services.analytics | 6.3.0: 2026-03-04 |
| S45 | Unity Developer Data collection settings (arama özeti; §1.3 için arka plan) | https://docs.unity.com/en-us/cloud/developer-data/collection-settings | tarih yok |
| S46 | Google Analytics for Firebase, Play data disclosure | https://support.google.com/analytics/answer/11582702 | ©2026 |
| S47 | Google consent mode for apps (Android) | https://developers.google.com/tag-platform/security/guides/app-consent?platform=android | 2026-07-30 |
| S48 | Firebase Configure Analytics data collection | https://firebase.google.com/docs/analytics/configure-data-collection | 2026-10-23 |
| S49 | Firebase DebugView (event batching) | https://firebase.google.com/docs/analytics/debugview | 2026-10-23 |
| S50 | Firebase Unity setup | https://firebase.google.com/docs/unity/setup | 2026-10-23 |
| S51 | Firebase Unity SDK releases | https://github.com/firebase/firebase-unity-sdk/releases | 14.1.0, "23 Oct" |
| S52 | Firebase pricing | https://firebase.google.com/pricing | tarih yok |
| S53 | firebase-android-sdk issue #2582, AD_ID silme (REPORTED) | https://github.com/firebase/firebase-android-sdk/issues/2582 | — |
| S54 | GameAnalytics Unity configuration | https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/game-engine-sdks/unity/configuration/ | tarih yok |
| S55 | GameAnalytics Developer Policy / Privacy FAQ (+ arama özeti: IP kayıtları, benchmark) | https://www.gameanalytics.com/trust/privacy-faq ; https://www.gameanalytics.com/trust/terms | 2023-05-23 |
| S56 | GameAnalytics C# SDK features (offline) | https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/open-source-sdks/c-sharp/sdk-features/ | tarih yok |
| S57 | GameAnalytics pricing | https://gameanalytics.com/pricing/ | ©2025 |
| S58 | GameAnalytics Unity SDK releases | https://github.com/GameAnalytics/GA-SDK-UNITY/releases | 8.3.0: 2026-09-30 |
| S59 | Aptabase ana sayfa / Unity sayfası (satıcı iddiası) | https://aptabase.com/ ; https://aptabase.com/for-unity | tarih yok |
| S60 | Aptabase privacy policy | https://aptabase.com/legal/privacy | 2026-07-24 |
| S61 | Aptabase pricing | https://aptabase.com/pricing | tarih yok |
| S62 | aptabase-unity README | https://github.com/aptabase/aptabase-unity | tarih yok |
| S63 | Aptabase sunucu deposu (AGPLv3) | https://github.com/aptabase/aptabase | tarih yok |
| S64 | SaaSCity, Aptabase review 2026 (REPORTED) | https://saascity.io/blog/aptabase-review-2026 | 2026 |
| S65 | EDPB Guidelines 2/2023, Art. 5(3) ePrivacy, v2 | https://www.edpb.europa.eu/system/files/documents/2024-10/edpb_guidelines_202302_technical_scope_art_53_eprivacydirective_v2_en_0.pdf | 2024-10-07 |
| S66 | CNIL, analytics sheet n°16 ve mesure d'audience (arama özeti) | https://www.cnil.fr/en/sheet-ndeg16-use-analytics-your-websites-and-applications ; https://www.cnil.fr/fr/mesurer-la-frequentation-de-vos-sites-web-et-de-vos-applications | 2020 kuralları, 2026-01 tavsiye |
| S67 | CPPA, updated monetary thresholds | https://cppa.ca.gov/regulations/cpi_adjustment.html | 2025-01-01'den geçerli |
| S68 | Federal Register, COPPA Rule amendments | https://www.federalregister.gov/documents/2025/04/22/2025-05904/childrens-online-privacy-protection-rule | 2025-04-22 |
| S69 | KVKK md. 9 değişikliği (Mondaq; Erdem & Erdem) (REPORTED) | https://mondaq.com/turkey/data-protection/1442172/recent-amendments-to-the-law-on-the-protection-of-personal-data ; https://www.erdem-erdem.av.tr/en/insights/personal-data-protection-bulletin-2024-third-quarter | 2024 |
| L1 | `unity/Assets/Editor/AndroidBuild.cs` satır 69–72 | yerel | 3deaa27 |
| L2 | `unity/Assets/Editor/ManifestPermissions.cs` satır 43, 85–92 | yerel | 3deaa27 |
| L3 | `unity/ProjectSettings/UnityConnectSettings.asset`, `ProjectSettings.asset` (`cloudProjectId`, `submitAnalytics: 0`) | yerel | çalışma ağacı |
| L4 | `words/tr.json` satır 200, 213–227 | yerel | 3deaa27 |
| L5 | `C:/Users/arcan/games-site/src/content/games.ts` (salt okunur) | yerel | "22 October 2026" |
| L6 | `docs/research/monetization-and-retention.md` §2.6, §8, §9 | yerel | 3deaa27 |
| L7 | `unity/Packages/manifest.json`, `ProjectVersion.txt` (6000.6.4f1) | yerel | 3deaa27 |
| L8 | `docs/GAME_DESIGN.md` satır 957–959, 1281 | yerel | 3deaa27 |
