# Gizlilik v1: Oyun İçi Metin, Site ve Mağaza Beyanları (Taslak)

**Tarih:** 2026-10-24. **Durum:** taslak; her metnin onayı proje sahibinde. Bu belgeden başka hiçbir dosya değişmedi: `words/*.json`, oyunun kodu ve `games-site` olduğu gibi duruyor.

**Uygulama (2026-10-24, analiz aşaması):** §3'ün metinleri oyuna girdi (13 dil; gizlilik kartı, soru kartı, ayar satırları, onay kartının ikinci satırı), §3.7'nin koşulları kodda tutuluyor, §5.2'nin Google Play cevapları `GAME_DESIGN.md` §20'de. Onay hâlâ sahipte. Yapılmayanlar: site metni (§4, sahip yayımlar), App Store etiketi (§5.1, iOS dalı), oyundan politikaya bağlantı (sitenin adresi bekleniyor). Ayrıntı: `GAME_DESIGN.md` §19, §20, §23.

**Neye dayanıyor:** sahibin v1 kararları (Unity IAP 5.4.4 sabit; Aptabase yalnız oyuncunun izniyle; Android paketi INTERNET izni ister; sunucu yok, fiş cihazda doğrulanır; reklam kimliği ve başka izleyici yok), satın alma denemesinin sonuçları [L1] ve aynı günün araştırma notu [L2]. Bu belge onlardaki kararları yeniden açmaz; yalnız metinlere ve beyanlara çevirir.

**Etiketler** (kardeş belgelerdeki gibi)
- **FACT**: resmî kaynakta, satıcının kendi belgesinde, paketin kaynak kodunda ya da bizim depomuzda gördük. Hangisi olduğu kaynağından belli.
- **REPORTED**: üçüncü taraf, haber ya da hukuk bürosu yazısı; doğrulanmadı.
- **ESTIMATE**: bizim çıkarımımız ya da önerimiz.

**Kaynaklar:** `[P..]` web, `[L..]` yerel dosya; URL ve tarihler §8'deki tabloda. Bütün web sayfalarına 2026-10-24'te bakıldı.

**Hukuk uyarısı:** §6 ve metinlerdeki hukuk dayanakları **not**tur, hukuki tavsiye değildir. Yayından önce bir uzmana okutulmalı.

---

## 1. Kısaca

1. **"Oyun hiçbir şey göndermez" v1'de doğru değil.** Telefondan üç yol çıkar:
   - **Mağaza:** satın almayı Google Play ya da App Store kendi penceresinde yapar; kart ve ödeme bilgisi oyuna gelmez. FACT [P1][P3]
   - **Unity'ye satın alma olayları:** IAP 5.4.4 her satın alma denemesinde (başlayınca, ödenince, başarısız olunca, teslim edilince) Unity'ye ürün, fiyat, fiş, cihaz bilgisi ve iOS'ta IDFV yollar. Rızaya bağlı değil, kapatma ayarı yok. Android'de INTERNET izni geldiği için artık oradan da çıkar. FACT [L1 §3.4] (paketin kaynak kodu) / ESTIMATE (çalışırken aynı davrandığı; ağ dinlemesiyle görülmedi)
   - **Aptabase:** yalnız oyuncu "İzin ver" derse; kalıcı kimlik yok; AB bölgesi. FACT [P11][P12][P14]
2. **Oyun içi metin (§3):** ayarlar satırı ve gizlilik kartının 1. ve 2. paragrafı değişir; 3. ve 4. paragraf kalır (3.'ye bir yan cümle önerilir). Yeni: Unity paragrafı, istatistik paragrafları, "sunucu yok" paragrafı, rıza kartı, ayarlarda "Oyun istatistikleri" anahtarı, tam politikaya bağlantı.
3. **Site (§4):** sitedeki politika satın alma ve analiz gelmeden **önce** güncelleneceğini söz veriyor; v1 yayından önce yeni metin yayımlanmalı ve tarihi değişmeli. FACT [L5]
4. **App Store (§5.1):** "Data Not Collected" olmaz. Bağlı (linked): Purchase History, Device ID, Other Diagnostic Data (Unity). Bağlı değil: Product Interaction, Coarse Location (Aptabase). Tracking: **No**. ESTIMATE, tanımlar FACT [P1]
5. **Google Play (§5.2):** zorunlu: Purchase history, Device or other IDs, Diagnostics (Unity); isteğe bağlı: App interactions, Approximate location (Aptabase). **Açık soru:** Unity kendi tablosunda "Shared: No" diyor [P7], ama sözleşmesinde satış ve fatura verisi için kendini **bağımsız veri sorumlusu** sayıyor [P9]. Google'ın tanımına göre bu "sharing" olabilir; öneri: paylaşılıyor diye beyan etmek. ESTIMATE
6. **En önemli hukuk notu (§6.1):** Unity'nin kapatılamayan satın alma olayları, AB'de cihazdan bilgi gönderten SDK'lar için rıza kuralına (ePrivacy 5(3)) [P16] ve Apple'ın 5.1.1(ii) maddesine [P2] takılabilir. Karar yeniden açılmıyor; bu uzmana sorulacak ilk soru.
7. **Sahibin yapacakları (§7):** metin onayı; Aptabase hesabı (AB bölgesi); iletişim adresi, yasal ad ve adres; site adresi; Play Console ve App Store Connect beyanları; uzman okuması.

---

## 2. v1'de telefondan ne çıkar

| Yol | Ne gider | Ne zaman | Nereye | Kimin elinde | Kaynak |
|---|---|---|---|---|---|
| **Mağaza** | Ürün sorgusu, satın alma, onay. Ödeme bilgisi mağazanın penceresinde kalır | Mağazaya bağlanırken, satın alırken | Google Play uygulaması / StoreKit üzerinden Google ve Apple | Mağazanın kendi kuralları | FACT [P1][P3][L1 §3.4] |
| **Unity satın alma olayları** | Olay kimliği ve zamanı; Unity oturum kimliği (cihazda varsa); uygulama paketi, uygulama ve Unity sürümü; mağaza; ürün kimliği, adı, açıklaması, fiyatı, para birimi; işlem kimliği; **mağazanın fişi** (Google'da imzalı satın alma kaydı ve imzası, Apple'da fiş ya da JWS kaydı); cihaz modeli, işletim sistemi sürümü, sistem dili ve dil listesi, cihazın açılış zamanı, toplam disk alanı; oyunun kurulma zamanı; **iPhone ve iPad'de IDFV** | Satın alma başlayınca, ödenince, başarısız olunca, oyun ürünü teslim edince; her biri ayrı istek | `prd.insights.analytics.unity3d.com/v1/ingest/producer` | Unity. Satış ve fatura verisinde bağımsız veri sorumlusu (kendi mali raporlaması ve denetimi, yasal yükümlülükleri, güvenlik ve sahtekârlık önleme) | FACT [L1 §3.4, §8] (paket kaynağı), [P6][P9] |
| **Aptabase** | Olay adı ve oyunun eklediği sayı/metin özellikleri; zaman; rastgele oturum numarası (yalnız bellekte; oyun yeniden açılınca ya da 60 dakika boşta kalınca yenilenir); oyun sürümü ve derleme numarası; işletim sistemi adı ve sürümü; dil ayarı (locale); hata ayıklama derlemesi mi. İstek başlığında "işletim sistemi/sürüm dil". Sunucu IP'den ülke ve bölge çıkarır, IP'yi saklamaz; `SHA(IP + User-Agent + günlük tuz)` ile günlük bir karma üretir, tuz 24 saatte atılır | Yalnız oyunun gönderdiği olaylar; dakikada bir paket (geliştirme derlemesinde 2 saniye) | `https://eu.aptabase.com` (AB; Almanya) | Biz veri sorumlusuyuz, Aptabase işleyici (GDPR md. 28); veri en çok 5 yıl | FACT [P11][P12][P14] |
| **Google Play ödeme kütüphanesinin kendi kayıtları** (`datatransport`) | Bilinmiyor | — | Google | Google | INTERNET silinmişken çıkamıyordu, artık çıkabilir. ESTIMATE [L1 §3.1, §3.4] |
| **Hiçbiri** | Reklam kimliği (GAID/IDFA), hesap, e-posta, konum izni, oyunun kendi rastgele cihaz kimliği, deneme kayıt dosyası | — | — | — | Denemenin izin dökümünde `AD_ID` ve konum izni yok: FACT [L1 §3.1, §8]. Kimliğin ve dosyanın gitmemesi analiz aşamasına bağlı: ESTIMATE (§3.7) |

**Unity'nin kendi listesi daha geniş:** 5.4 ve sonrası için "her zaman toplanır" dediği veriler: Player ID (Authentication), Unity Installation ID, cihaz bilgisi, oturum kimlikleri, ülke. İsteğe bağlı olanlar arasında IDFV de var. FACT [P6]. Biz Unity Authentication'ı ve UGS'yi başlatmıyoruz; oyuncu kimliği oluşmaz. ESTIMATE [L1 §3.3]

**Unity'nin rolü iki belgede farklı yazıyor:** IAP gizlilik sayfası "işlem ve IAP verisi"ni geliştirici adına işlenen veri örneği olarak veriyor ve bu veriler için son kullanıcıdan gelen isteği işleme almayacağını söylüyor [P6]. Sözleşme (DPA, 2026-06-30 güncellemesi) ise satış ve fatura verisi için Unity'yi **bağımsız veri sorumlusu** sayıyor; yalnız IAP panosu için işleyici [P9]. Daha yeni ve bağlayıcı olan sözleşme. FACT. Bu yüzden metinlerde Unity'yi kendi amaçları olan ayrı bir şirket olarak anlatıyoruz ve istek yolu olarak iki adresi de veriyoruz. ESTIMATE

---

## 3. Oyun içi metinler (`words/tr.json`)

Onaylı metinlerin notu "approved text: keep every fact". Bugünkü gerçeklerden hâlâ doğru olanlar korunuyor, artık doğru olmayanlar değişiyor. FACT [L3][L4]. Ton onaylı metinlerdeki gibi: "sen", kısa cümle, teknik terim yok ya da tek bir yerde.

### 3.1 Değişen ve yeni anahtarlar

| WordId | Bugün | Öneri (tr) | Neden |
|---|---|---|---|
| `SettingsPrivacyLine` | Oyun hiçbir şey göndermez | **Telefondan yalnızca satın almalar çıkar** (istatistik kapalıyken) | Satın almalar mağazaya ve Unity'ye gider [L1 §3.4]. Satır artık rızaya göre değişir |
| `SettingsPrivacyLineWithStatistics` (yeni) | — | **Satın almalar ve kimliksiz istatistikler çıkar** (istatistik açıkken). Kısa seçenek: "Satın almalar ve istatistik (kimliksiz)" | Aptabase notlarında kalıcı kimlik yok [P12][P14] |
| `SettingAnalytics` (yeni) | — | **Oyun istatistikleri** | Ayarlar kartında anahtarlı satır; Ses, Müzik, Titreşim gibi bir ad. Rıza kartının başlığı da olabilir |
| `PrivacyParagraph1` | Oyun her şeyi yalnızca bu telefonda tutar: ilerlemeni, cüzdanını, ayarlarını ve oynadığın denemelerin kayıtlarını. | **Oyun ilerlemeni, cüzdanını (satın aldığın altın dahil), ayarlarını ve oynadığın denemelerin kayıtlarını yalnızca bu telefonda tutar. Sunucumuz yoktur; bunların bizde bir kopyası olmaz.** | Dört gerçek kalır. "Her şeyi" sözü istatistik ve satın alma olaylarıyla çelişirdi. Satın alınan altın ve "sunucu yok" eklenir |
| `PrivacyParagraph2` | Bu sürüm hiçbir şeyi telefondan dışarı göndermez. Hesap, reklam ağı ve satın alma yoktur; Unity'nin istatistikleri kapalıdır. | **Satın almaları Google Play ya da App Store yapar; kart ve ödeme bilgilerin oyuna gelmez. Hesap ve reklam ağı yoktur.** | "Hesap yok", "reklam ağı yok" doğru kalır. "Satın alma yok" ve "hiçbir şey göndermez" değişir. "Unity'nin istatistikleri kapalıdır" motor ayarı olarak hâlâ doğru (`submitAnalytics: 0`) ama Unity'nin analiz adresine satın alma olayı giderken yanıltıcı olur; yerine aşağıdaki Unity paragrafı gelir |
| `PrivacyParagraphUnity` (yeni) | — | **Satın alma altyapısı Unity'nindir. Her satın alma denemesinde Unity'ye ürün, fiyat, mağazanın fişi, telefonunun modeli ve dili, iPhone ve iPad'de de Apple'ın cihaza verdiği uygulama kimliği gider. Unity bunları kendi mali kayıtları, yasal yükümlülükleri ve sahtekârlığı önlemek için kullanır. Bu gönderim kapatılamaz.** | [L1 §3.4][P6][P9]. Sahibin kararı: yamalamıyoruz, açıkça söylüyoruz |
| `PrivacyParagraphStatistics` (yeni) | — | **İzin verirsen oyun, nasıl oynandığını sayan kısa notları Aptabase adlı ölçüm hizmetine gönderir: hangi bölümü oynadığın, kazanıp kazanmadığın, kaç hamle kaldığı, oturumun ne kadar sürdüğü. Yanlarında oyunun sürümü, telefonunun işletim sistemi ve dili de gider. Aptabase bunları Avrupa Birliği'nde sayar ve IP adresini saklamaz.** | Olay listesi analiz aşamasının listesiyle birebir eşlenmeli (§3.7). Mağaza olayları da gidecekse: "…, mağazada ne aldığın (ödeme bilgisi olmadan)" eklenir |
| `PrivacyParagraphStatisticsChoice` (yeni) | — | **Notlarda adın, e-postan, reklam kimliğin ya da telefonunu tanıyan bir kimlik yoktur. İzin vermezsen oyun aynen çalışır. Kararını Ayarlar'daki "Oyun istatistikleri" anahtarıyla istediğin zaman değiştirebilirsin.** | Apple 5.1.1(ii): geri almak kolay olmalı, ücretli işlev rızaya bağlanamaz [P2] |
| `PrivacyParagraph3` | Kayıtlarda adın ya da telefonunun bilgisi yoktur, yalnızca bu telefona verilmiş rastgele bir kimlik vardır. | Kalır. Öneri, sonuna: **Oyun bu kimliği hiçbir yere göndermez.** | Kimlik ayarlarda görünür ve oyuncu destek için kendisi söyleyebilir; "telefondan çıkmaz" bu yüzden değil. Yalnız analiz aşaması kimliği olaylara koymazsa doğru (§3.7) |
| `PrivacyParagraph4` | Telefonunun yedeklemesi açıksa Android bu dosyaları kendi yedeğine katabilir. | Android'de kalır. iOS için ayrı cümle gerekebilir: "Telefonunun iCloud yedeği açıksa iOS bu dosyaları yedeğine katabilir." | iOS'ta oyunun dosyalarının iCloud yedeğine girip girmediği iOS dalında doğrulanmalı. ESTIMATE |
| `PrivacyParagraphNoServer` (yeni) | — | **Altınını ve ilerlemeni yalnızca bu telefon bilir. Oyunu silersen ya da telefon değiştirirsen bunlar kaybolur; telefonun yedeği geri getirebilir, ama oyun bunu garanti edemez. Satın alma iadesini Google Play'den ya da Apple'dan istersin.** | "Sunucu yok" kararının oyuncuya anlamı [L2 §1.7, §1.8]. Android yedeği yeniden kurulumda geri yükler [P15] ama yedeğin açık olması, 24 saatte bir alınması ve 25 MB sınırı gibi koşullara bağlı |
| (koşullu ek) | — | Çırak Çantası tüketilmeyen ürün olarak satılırsa: **Bir kez satılan Çırak Çantası'nı mağaza hatırlar; oyunu yeniden kurunca "Satın alımları geri yükle" ile geri gelir.** | IAP aşamasının ürün tipi seçimine bağlı. Tüketilen ürün olursa bu cümle yazılmaz |
| `PrivacyPolicyLink` (yeni) | — | **Gizlilik politikasının tamamı** (siteye açılır: `<GIZLILIK_URL>/tr/`, dile göre) | Apple 5.1.1(i) ve Google, politikaya oyunun içinden de bağlantı istiyor [P2][P4] |
| `BuildNoBuying` ("satın alma kapalı") | — | Bu taslağın dışında; IAP aşaması değiştirir | Ayarlar kartının alt satırı |

### 3.2 Gizlilik kartı, yeni sırasıyla (tr)

1. Oyun ilerlemeni, cüzdanını (satın aldığın altın dahil), ayarlarını ve oynadığın denemelerin kayıtlarını yalnızca bu telefonda tutar. Sunucumuz yoktur; bunların bizde bir kopyası olmaz.
2. Satın almaları Google Play ya da App Store yapar; kart ve ödeme bilgilerin oyuna gelmez. Hesap ve reklam ağı yoktur.
3. Satın alma altyapısı Unity'nindir. Her satın alma denemesinde Unity'ye ürün, fiyat, mağazanın fişi, telefonunun modeli ve dili, iPhone ve iPad'de de Apple'ın cihaza verdiği uygulama kimliği gider. Unity bunları kendi mali kayıtları, yasal yükümlülükleri ve sahtekârlığı önlemek için kullanır. Bu gönderim kapatılamaz.
4. İzin verirsen oyun, nasıl oynandığını sayan kısa notları Aptabase adlı ölçüm hizmetine gönderir: hangi bölümü oynadığın, kazanıp kazanmadığın, kaç hamle kaldığı, oturumun ne kadar sürdüğü. Yanlarında oyunun sürümü, telefonunun işletim sistemi ve dili de gider. Aptabase bunları Avrupa Birliği'nde sayar ve IP adresini saklamaz.
5. Notlarda adın, e-postan, reklam kimliğin ya da telefonunu tanıyan bir kimlik yoktur. İzin vermezsen oyun aynen çalışır. Kararını Ayarlar'daki "Oyun istatistikleri" anahtarıyla istediğin zaman değiştirebilirsin.
6. Kayıtlarda adın ya da telefonunun bilgisi yoktur, yalnızca bu telefona verilmiş rastgele bir kimlik vardır. Oyun bu kimliği hiçbir yere göndermez.
7. Telefonunun yedeklemesi açıksa Android bu dosyaları kendi yedeğine katabilir.
8. Altınını ve ilerlemeni yalnızca bu telefon bilir. Oyunu silersen ya da telefon değiştirirsen bunlar kaybolur; telefonun yedeği geri getirebilir, ama oyun bunu garanti edemez. Satın alma iadesini Google Play'den ya da Apple'dan istersin.
9. (bağlantı) Gizlilik politikasının tamamı

Altında bugünkü gibi: deneme kayıtlarının sayısı, dosya satırı, "Kayıtları sil". Kart bugünkünden yaklaşık iki kat uzun olur; kaydırma gerekebilir, düzeni görüntüyle bakılmalı (1080x2424, 2076x2152, 2732x2048). ESTIMATE

**Sahibin seçeceği bir cümle:** sitede "Oyun, bir altın paketinin iade edildiğini öğrenmez" demek doğru (sunucu olmadan Google'ın iade listesi ve Apple'ın iade bildirimi bize ulaşamaz [P31][P32]), ama iadeyi kötüye kullanmaya davet edebilir. Öneri: oyunun içinde yazmamak, sitede yalnız "İadeyi Google Play ya da Apple yönetir" demek. ESTIMATE

### 3.3 Rıza kartı (yeni)

| WordId | tr | Not |
|---|---|---|
| `ConsentTitle` | **Oyun istatistikleri** | Kartın plakası; `SettingAnalytics` ile aynı söz |
| `ConsentQuestion` | **Oyunu iyileştirmek için nasıl oynandığını sayabilir miyiz?** | |
| `ConsentParagraph1` | **İzin verirsen oyun; oynadığın bölümleri, sonuçlarını ve oturumların ne kadar sürdüğünü sayan kısa notlar gönderir. Notları Aptabase adlı ölçüm hizmeti Avrupa Birliği'nde sayar.** | Ne ölçülür. Mağaza olayları da gidecekse "…ve mağazada ne aldığını (ödeme bilgisi olmadan)" |
| `ConsentParagraph2` | **Adın, e-postan, reklam kimliğin ya da telefonunu tanıyan bir kimlik gitmez. Notlar reklam için kullanılmaz, kimseyle paylaşılmaz.** | Asla ne yapmaz [P11][P12] |
| `ConsentParagraph3` | **Hayır dersen oyun aynen çalışır, satın almalar da. Kararını Ayarlar'dan istediğin zaman değiştirebilirsin.** | Rızasız da aynı oyun [P2] |
| `ConsentAllow` | **İzin ver** | |
| `ConsentDecline` | **İzin verme** | İki düğme aynı boyda ve aynı ağırlıkta |

**Kartın kuralları** (metin değil, rıza aşamasına):
- Bir kez sorulur; yanıt `device.json`'da durur, ayarlardan değişir (sahibin kararı).
- Yanıt gelene kadar hiçbir olay gönderilmez ve kuyruğa da girmez (§3.7). Google: rıza toplamadan önce alınmalı, açık bir dokunuş ister; geri tuşu, dışarı dokunma, ana ekran tuşu rıza sayılmaz; kendiliğinden kapanan bildirim olmaz; açıklama oyunun içinde olmalı, yalnız politikada olamaz [P4]. FACT
- Geri tuşu ve × "İzin verme" demektir; önceden seçili kutu yok. ESTIMATE [P4][P17 md. 7]
- Rızayı geri almak vermek kadar kolay: ayarlardaki tek anahtar. FACT [P17 md. 7(3)][P2 5.1.1(ii)]
- Ne zaman çıkacağı sahibin kararı. Öneri: ilk bölüm bittikten sonra, salona ilk dönüşte; oyunun ilk karesinde değil. ESTIMATE

### 3.4 Ayarlar kartı

- Yeni anahtarlı satır `SettingAnalytics` "Oyun istatistikleri": Ses, Müzik, Titreşim gibi. İlk açılışta **kapalı**; rıza kartında "İzin ver" denince açılır.
- "Gizlilik ve veri" satırının küçük yazısı anahtara göre: kapalıyken `SettingsPrivacyLine`, açıkken `SettingsPrivacyLineWithStatistics`.

### 3.5 Satın alma onayında bir satır (öneri)

Sahibin kuralı her satın almadan önce bir onay adımı istiyor [L6 §3.2]. O adıma tek satır, tam yerinde bilgi verir:

| WordId | tr | Not |
|---|---|---|
| `ShopConfirmWhoSees` (yeni) | **Ödemeyi {store} alır. Satın alma bilgisi Unity'ye de gider.** | `{store}`: "Google Play" ya da "App Store"; mağaza adları çevrilmez |

Neden: Google, kullanıcının makul olarak beklemeyeceği veri gönderimi için oyunun içinde, işlemin hemen önünde açıklama istiyor [P4]. Unity'ye giden olayın beklenmeyen bir şey sayılıp sayılmayacağı yoruma açık; bu satır o soruyu kapatır. ESTIMATE

### 3.6 İngilizce karşılıklar (inceleme için; kaynak dil tr)

| WordId | en |
|---|---|
| `SettingsPrivacyLine` | Only purchases leave your phone |
| `SettingsPrivacyLineWithStatistics` | Purchases and statistics with no ID leave your phone |
| `SettingAnalytics` / `ConsentTitle` | Game statistics |
| `PrivacyParagraph1` | The game keeps your progress, your wallet (including the coins you bought), your settings and the records of the attempts you play only on this phone. We have no server and hold no copy of them. |
| `PrivacyParagraph2` | Purchases are made by Google Play or the App Store; your card and payment details never reach the game. There are no accounts and no ad network. |
| `PrivacyParagraphUnity` | The game's purchase library is Unity's. On every purchase attempt, Unity receives the product, the price, the store's receipt, your phone's model and language, and on iPhone and iPad the app identifier Apple gives your device. Unity uses them for its financial records, its legal obligations and to prevent fraud. This cannot be turned off. |
| `PrivacyParagraphStatistics` | If you allow it, the game sends Aptabase, an analytics service, short notes on how it is played: which level you played, whether you won, how many moves were left and how long the session lasted, together with the game's version and your phone's operating system and language. Aptabase counts them in the European Union and does not store your IP address. |
| `PrivacyParagraphStatisticsChoice` | The notes hold no name, e-mail address, advertising ID or ID that recognises your phone. If you say no, the game works just the same. You can change your choice at any time with the Game statistics switch in Settings. |
| `PrivacyParagraph3` (+ek) | … The game never sends this ID anywhere. |
| `PrivacyParagraphNoServer` | Only this phone knows your coins and progress. If you uninstall the game or change phones, they are lost; your phone's backup may bring them back, but the game cannot promise it. Refunds are asked for from Google Play or Apple. |
| `PrivacyPolicyLink` | The full privacy policy |
| `ConsentQuestion` | May we count how the game is played, to make it better? |
| `ConsentParagraph1` | If you allow it, the game sends short notes that count the levels you play, their results and how long your sessions last. Aptabase, an analytics service, counts them in the European Union. |
| `ConsentParagraph2` | No name, e-mail address, advertising ID or ID that recognises your phone is sent. The notes are never used for ads and never shared. |
| `ConsentParagraph3` | If you say no, the game works just the same, and so do purchases. You can change your choice in Settings at any time. |
| `ConsentAllow` / `ConsentDecline` | Allow / Don't allow |
| `ShopConfirmWhoSees` | {store} takes the payment. Unity also receives the purchase details. |

Öteki 11 dil tr'den çevrilir (A39'daki gibi makine çevirisi kabul). Translator note önerisi: "Privacy card / consent card (approved text pending): keep every fact; Aptabase, Unity, Google Play, App Store are names."

### 3.7 Metnin doğru kalması için koşullar (analiz ve IAP aşamalarına)

Metinler ancak bunlar tutarsa doğru. Biri değişirse metin ve mağaza beyanları da değişir.

1. **Rızadan önce hiçbir `Aptabase.TrackEvent` çağrısı yok; rıza kapalıyken de yok.** aptabase-unity 0.3.1'de `TrackEvent` yalnız adresin kurulu olup olmadığına bakıyor, `SetEnabled(false)`'a bakmıyor: kapalıyken eklenen olay kuyrukta bekler ve SDK yeniden açılınca gider. Yani kapı oyunun kendi kodunda olmalı, `SetEnabled`'e güvenilmemeli. FACT [P14 `Aptabase.cs`, `Dispatcher.cs`]
2. **SDK açılışta kendiliğinden kurulur** (`RuntimeInitializeOnLoadMethod`), ama kuyruk boşken ağa çıkmaz. FACT [P14]. Ayar dosyası oyunda olabilir.
3. **`EnableCrashReporting` kapalı kalır** (varsayılan kapalı). Açılırsa hata raporları `SetEnabled`'in varsayılan değeri (açık) yüzünden rızadan önce gidebilir ve iki mağazada "Crash Data / Crash logs" beyanı gerekir. FACT [P14 `Settings.cs`, `Aptabase.cs`] / ESTIMATE
4. **App Key AB bölgesinden** (`A-EU-…`); SDK bölgeyi anahtardan seçiyor. FACT [P14]
5. **Olay özelliklerinde kalıcı kimlik yok:** oyunun rastgele cihaz kimliği, oyuncu kimliği, işlem kimliği, serbest metin girilmez. Girerse "kimliksiz" yanlış olur, App Store'da "linked", Play'de "User IDs / Device or other IDs" değişir. ESTIMATE [P1][P3]
6. **UGS ve Unity Authentication başlatılmaz** (`UnityServices.InitializeAsync` yok), Unity Analytics paketi eklenmez, `submitAnalytics: 0` kalır. Başlatılırsa IAP'nin teşhis ölçümleri de gider [L1 §3.3, §3.4]. FACT (paket kaynağı)
7. **`AD_ID` izni, IDFA ve ATT yok.** Derlemeden sonra izinler APK'dan dökülüp bakılmalı (deneme: `VIBRATE`, `BILLING`, `ACCESS_NETWORK_STATE`, Unity'nin iç izni, artık `INTERNET`). FACT [L1 §8]
8. **Fiş cihazda doğrulanır ve bize gönderilmez** (sunucu yok). ESTIMATE [L2 §1.6]
9. **`PrivacyWords.cs`'nin açıklaması** ("this build sends none of it anywhere") ve GAME_DESIGN §20'deki "Rıza ekranı yok" satırı metinle birlikte değişir. FACT [L3][L4]

---

## 4. Oyun sitesi gizlilik sayfası (`C:/Users/arcan/games-site`, salt okundu)

### 4.1 Ne değişmeli

- **"In short / Kısaca":** "sends nothing", "no purchases", "Unity's analytics are turned off", "does not ask for permission to use the internet" cümlelerinin dördü de v1'de yanlış. FACT [L5][L1]
- **Yeni bölümler:** Purchases (mağaza, fiş, Unity olayları, Unity'nin rolü ve politikası, aktarım), Refunds and reinstalling (sunucu yok), Play statistics (Aptabase, rıza, veri, bölge, saklama, silme), Legal basis and rights, Who is responsible.
- **"Children":** "collects no personal data from anyone" v1'de söylenemez; yeniden yazılır.
- **"Changes":** bugünkü söz ("updated before they arrive") yerine getirilmiş olur; v1'den önce yayımlanmalı ve `updated` tarihi yayın günü olmalı. FACT [L5]
- **Yer:** `games.ts`'deki `museumPuzzle.privacy` nesnesi bütünüyle değişir; yapı (`title`, `updated`, `intro`, `sections[{heading, paragraphs}]`) aynı kalır. İletişim bölümünü `PrivacyPolicyView` kendisi ekler; `site.contactEmail` boşken "yayından önce eklenecek" yazar. FACT [L5]
- **Platform:** sayfa Android ve iOS için tek. Politikanın içeriği (veri türleri, alıcılar, saklama, silme, iletişim) Google'ın ve Apple'ın istediği başlıkları karşılar [P2 5.1.1(i)][P4]. ESTIMATE

### 4.2 Yapıştırmaya hazır taslak (`games.ts`, `privacy:` nesnesinin yerine)

`<…>` yer tutucularını sahip doldurur (§4.3). Köşeli parantezli cümleler koşullu.

```ts
  privacy: {
    en: {
      title: "Privacy Policy",
      updated: "<DAY> <MONTH> <YEAR>",
      intro:
        "This policy explains what the game Museum Puzzle, made by SaturnWave, does with your data. It applies to version 1 of the game on Android, iPhone and iPad.",
      sections: [
        {
          heading: "In short",
          paragraphs: [
            "The game keeps your progress, your coins and your settings only on your device. We run no server and hold no copy of them.",
            "Three things can leave your device. Google Play or the App Store handles every purchase. Unity, whose purchase library the game uses, is told about each purchase. And only if you say yes, the game sends play statistics that carry no identifier to Aptabase. The game has no accounts and no advertising network, and it does not use an advertising identifier.",
          ],
        },
        {
          heading: "What stays on your device",
          paragraphs: [
            "The game keeps your progress, your wallet (coins, including the coins you bought, lives and boosters), your settings and the records of the level attempts you play only on your device.",
            "An attempt record holds the level's number, the moves, the result and the boosters used. It helps balance the game. The records themselves never leave the device; with your consent, short notes about levels are sent as described under Play statistics.",
          ],
        },
        {
          heading: "Purchases",
          paragraphs: [
            "You buy in Google Play's window on Android and in the App Store's window on iPhone and iPad. Your card and payment details never reach the game or us; the store's own terms and privacy policy apply to the payment.",
            "The game checks the store's receipt on your device before it gives you what you bought. The receipt is not sent to us.",
            "The game sells through Unity In-App Purchasing 5.4.4, a library made by Unity Technologies. Each time a purchase starts, is paid, fails or is delivered, the library sends Unity a purchase event. It holds the product with its name, price and currency, the transaction and the store's receipt, the game's version and the time it was installed, your device's model, operating-system version and language settings, the time the device was last started and its total storage, a Unity session identifier if the device holds one, and on iPhone and iPad the identifier for vendor (IDFV) that Apple gives your device. The library offers no way to turn this off, so it is sent whether or not you agree to play statistics.",
            "For transaction and billing data, Unity acts as an independent controller. Unity says it uses this data for its financial reporting and auditing, its legal obligations, and security and fraud prevention. Unity may process it in the United States and other countries, and relies on adequacy decisions or Standard Contractual Clauses for transfers out of the European Union and the United Kingdom. Unity's privacy policy: https://unity.com/legal/game-player-and-app-user-privacy-policy. You can write to Unity about this data at unity-iap-contact@unity3d.com, or write to us and we will pass your request on.",
          ],
        },
        {
          heading: "Refunds, reinstalling and new devices",
          paragraphs: [
            "Because there is no server, your coins and progress exist only on your device. If you uninstall the game or move to a new device, they are lost, unless your device's own backup brings them back (see Backups). We cannot restore them for you.",
            "Refunds are requested from Google Play or Apple and decided by them under their own rules.",
            "[The store remembers the one-time Çırak Çantası (starter pack): after reinstalling, Restore Purchases brings it back.]",
          ],
        },
        {
          heading: "Play statistics, only with your consent",
          paragraphs: [
            "<WHEN>, the game asks once whether it may send play statistics. Until you say yes, it sends none. You can change your answer at any time with Settings > Game statistics; once it is off, nothing new is sent.",
            "With your consent, the game sends short notes to Aptabase: <EVENT LIST, for example which level you played, whether you won, how many moves were left and how long a session lasted>. Each note also carries the time, the game's version and build, your device's operating system and its version, your language setting, and a random session number that lives only in the device's memory and changes when the game restarts or after an hour without play.",
            "Aptabase is run by Technov Solutions SRL in Romania and receives the notes on its servers in the European Union (Germany). From your connection's IP address it works out your country and region and a code that changes every day; it does not store the IP address. It keeps statistics for up to five years, processes them only on our behalf and never sells them. Aptabase's privacy policy: https://aptabase.com/legal/privacy.",
            "The notes hold no name, e-mail address, advertising identifier or device identifier, and nothing that stays the same from one session to the next. So we cannot tell which notes came from you, and we cannot show them to you or delete them one by one. Turning statistics off stops all new notes.",
            "We use the statistics only to see how levels play and to make the game better: whether a level is too hard, where players stop, how long a session lasts. We never use them for advertising and never share them with anyone else.",
          ],
        },
        {
          heading: "Who you are",
          paragraphs: [
            "The records on your device hold no name, no e-mail address and no information about your device, only a random identifier the game gave this device. The game never sends this identifier anywhere.",
          ],
        },
        {
          heading: "Backups",
          paragraphs: [
            "If your device's backup is turned on, Android may include the game's files in its own backup in your Google account and put them back when the game is installed again. [On iPhone and iPad, the device's iCloud backup may include them.] These backups follow Google's and Apple's terms; the game cannot reach them.",
          ],
        },
        {
          heading: "Deleting your data",
          paragraphs: [
            "You can delete the attempt records in the game: Settings > Privacy and data > Delete records. Uninstalling the game deletes all of its data from the device.",
            "Turning off Settings > Game statistics stops the statistics. For the purchase events Unity holds, see Purchases.",
          ],
        },
        {
          heading: "Legal basis and your rights",
          paragraphs: [
            "We send play statistics only on the basis of your consent (Article 6(1)(a) of the GDPR; for players in Türkiye, explicit consent under Article 5(1) of Law No. 6698 on the Protection of Personal Data). You can withdraw it at any time; withdrawing does not affect what was sent before. <LEGAL BASIS FOR PURCHASES AND THE PURCHASE EVENTS, TO BE CONFIRMED BY A LAWYER>",
            "Depending on where you live, you may have the right to ask what data about you is held, to have it corrected or deleted, to object to its use, and to complain to a data protection authority (in Türkiye, the Personal Data Protection Authority, https://www.kvkk.gov.tr). To use these rights, write to us at the address below.",
          ],
        },
        {
          heading: "Who is responsible",
          paragraphs: ["The controller of the data described here is <LEGAL NAME> (SaturnWave), <ADDRESS>."],
        },
        {
          heading: "Children",
          paragraphs: [
            "The game is not made for children. It asks no one for their age, name or e-mail address, and it sends play statistics only after a yes, without any identifier.",
          ],
        },
        {
          heading: "Changes",
          paragraphs: [
            "If the game starts to send anything else, such as advertising, this policy will be updated before the change arrives, and the game will ask for any consent it needs. The date at the top shows the latest change.",
          ],
        },
      ],
    },
    tr: {
      title: "Gizlilik Politikası",
      updated: "<GÜN> <AY> <YIL>",
      intro:
        "Bu politika, SaturnWave'in geliştirdiği Müze Yapbozu oyununun verilerinizle ne yaptığını anlatır. Oyunun Android, iPhone ve iPad'deki 1. sürümü için geçerlidir.",
      sections: [
        {
          heading: "Kısaca",
          paragraphs: [
            "Oyun ilerlemenizi, altınınızı ve ayarlarınızı yalnızca cihazınızda tutar. Sunucumuz yoktur; bunların bizde bir kopyası olmaz.",
            "Cihazınızdan üç şey çıkabilir. Her satın almayı Google Play ya da App Store yapar. Oyunun kullandığı satın alma kütüphanesinin sahibi Unity'ye her satın alma bildirilir. Yalnızca izin verirseniz oyun, hiçbir kimlik taşımayan oyun istatistiklerini Aptabase'e gönderir. Oyunda hesap ve reklam ağı yoktur, reklam kimliği kullanılmaz.",
          ],
        },
        {
          heading: "Cihazınızda kalanlar",
          paragraphs: [
            "Oyun ilerlemenizi, cüzdanınızı (satın aldığınız altın dahil altın, can ve güçlendiriciler), ayarlarınızı ve oynadığınız bölüm denemelerinin kayıtlarını yalnızca cihazınızda tutar.",
            "Bir deneme kaydı bölümün numarasını, hamleleri, sonucu ve kullanılan güçlendiricileri içerir. Oyunu dengelemeye yarar. Kayıtların kendisi cihazdan çıkmaz; izin verirseniz bölümler hakkında kısa notlar, Oyun istatistikleri bölümünde anlatıldığı gibi gönderilir.",
          ],
        },
        {
          heading: "Satın almalar",
          paragraphs: [
            "Android'de Google Play'in, iPhone ve iPad'de App Store'un kendi penceresinden satın alırsınız. Kart ve ödeme bilgileriniz oyuna da bize de gelmez; ödemeye mağazanın kendi koşulları ve gizlilik politikası uygulanır.",
            "Oyun, satın aldığınızı vermeden önce mağazanın fişini cihazınızda denetler. Fiş bize gönderilmez.",
            "Oyun, Unity Technologies'in Unity In-App Purchasing 5.4.4 kütüphanesiyle satar. Bir satın alma başladığında, ödendiğinde, başarısız olduğunda ya da teslim edildiğinde bu kütüphane Unity'ye bir satın alma olayı gönderir. Olayda ürün (adı, fiyatı ve para birimiyle), işlem ve mağazanın fişi, oyunun sürümü ve kurulduğu zaman, cihazınızın modeli, işletim sistemi sürümü ve dil ayarları, cihazın en son açıldığı zaman ve toplam depolama alanı, cihazda varsa Unity'nin bir oturum kimliği ve iPhone ile iPad'de Apple'ın cihazınıza verdiği uygulama kimliği (IDFV) bulunur. Kütüphanede bunu kapatacak bir ayar yoktur; bu yüzden oyun istatistiklerine izin verseniz de vermeseniz de gönderilir.",
            "Unity, satış ve fatura verisi için bağımsız veri sorumlusudur. Unity bu veriyi kendi mali raporlaması ve denetimi, yasal yükümlülükleri, güvenlik ve sahtekârlığı önleme için kullandığını söyler. Unity bu veriyi Amerika Birleşik Devletleri'nde ve başka ülkelerde işleyebilir; Avrupa Birliği ve Birleşik Krallık dışına aktarımda yeterlilik kararlarına ya da standart sözleşme maddelerine dayanır. Unity'nin gizlilik politikası: https://unity.com/legal/game-player-and-app-user-privacy-policy. Bu veriyle ilgili olarak Unity'ye unity-iap-contact@unity3d.com adresinden yazabilir ya da bize yazabilirsiniz; isteğinizi Unity'ye iletiriz.",
          ],
        },
        {
          heading: "İade, yeniden kurma ve yeni cihaz",
          paragraphs: [
            "Sunucumuz olmadığı için altınınız ve ilerlemeniz yalnızca cihazınızda bulunur. Oyunu kaldırırsanız ya da yeni bir cihaza geçerseniz, cihazınızın kendi yedeği geri getirmedikçe (Yedekleme bölümüne bakın) bunlar kaybolur. Bunları sizin için geri yükleyemeyiz.",
            "İadeyi Google Play'den ya da Apple'dan istersiniz; kararı onlar kendi kurallarına göre verir.",
            "[Bir kez satılan Çırak Çantası'nı mağaza hatırlar: oyunu yeniden kurduktan sonra Satın Alımları Geri Yükle ile geri gelir.]",
          ],
        },
        {
          heading: "Oyun istatistikleri, yalnızca izninizle",
          paragraphs: [
            "<NE ZAMAN> oyun, oyun istatistiği gönderip gönderemeyeceğini bir kez sorar. Evet diyene kadar hiçbir şey göndermez. Kararınızı istediğiniz zaman Ayarlar > Oyun istatistikleri ile değiştirebilirsiniz; kapattığınız andan sonra yeni bir şey gönderilmez.",
            "İzin verirseniz oyun Aptabase'e kısa notlar gönderir: <OLAY LİSTESİ, ör. hangi bölümü oynadığınız, kazanıp kazanmadığınız, kaç hamle kaldığı ve oturumun ne kadar sürdüğü>. Her notta ayrıca zaman, oyunun sürümü ve derleme numarası, cihazınızın işletim sistemi ve sürümü, dil ayarınız ve yalnızca cihazın belleğinde duran, oyun yeniden açılınca ya da bir saat oynanmayınca değişen rastgele bir oturum numarası bulunur.",
            "Aptabase'i Romanya'daki Technov Solutions SRL işletir; notları Avrupa Birliği'ndeki (Almanya) sunucularında alır. Bağlantınızın IP adresinden ülkenizi, bölgenizi ve her gün değişen bir kodu çıkarır; IP adresini saklamaz. İstatistikleri en çok beş yıl tutar, yalnızca bizim adımıza işler ve asla satmaz. Aptabase'in gizlilik politikası: https://aptabase.com/legal/privacy.",
            "Notlarda ad, e-posta adresi, reklam kimliği ya da cihaz kimliği yoktur; bir oturumdan ötekine aynı kalan hiçbir şey yoktur. Bu yüzden hangi notların sizden geldiğini ayırt edemeyiz; onları size gösteremez ya da tek tek silemeyiz. İstatistikleri kapatmak bütün yeni notları durdurur.",
            "İstatistikleri yalnızca bölümlerin nasıl oynandığını görmek ve oyunu iyileştirmek için kullanırız: bir bölüm fazla mı zor, oyuncular nerede bırakıyor, bir oturum ne kadar sürüyor. Onları asla reklam için kullanmaz, kimseyle paylaşmayız.",
          ],
        },
        {
          heading: "Kimliğiniz",
          paragraphs: [
            "Cihazınızdaki kayıtlarda adınız, e-posta adresiniz ya da cihazınızın bilgisi yoktur; yalnızca oyunun bu cihaza verdiği rastgele bir kimlik vardır. Oyun bu kimliği hiçbir yere göndermez.",
          ],
        },
        {
          heading: "Yedekleme",
          paragraphs: [
            "Cihazınızın yedeklemesi açıksa Android oyunun dosyalarını Google hesabınızdaki kendi yedeğine katabilir ve oyun yeniden kurulduğunda geri koyabilir. [iPhone ve iPad'de cihazın iCloud yedeği de bu dosyaları içerebilir.] Bu yedekler Google'ın ve Apple'ın kurallarına tabidir; oyun onlara erişemez.",
          ],
        },
        {
          heading: "Verilerinizi silmek",
          paragraphs: [
            "Deneme kayıtlarını oyunun içinden silebilirsiniz: Ayarlar > Gizlilik ve veri > Kayıtları sil. Oyunu kaldırmak bütün verilerini cihazdan siler.",
            "Ayarlar > Oyun istatistikleri'ni kapatmak istatistikleri durdurur. Unity'deki satın alma olayları için Satın almalar bölümüne bakın.",
          ],
        },
        {
          heading: "Hukuki dayanak ve haklarınız",
          paragraphs: [
            "Oyun istatistiklerini yalnızca izninize dayanarak göndeririz (GDPR madde 6(1)(a); Türkiye'deki oyuncular için 6698 sayılı Kişisel Verilerin Korunması Kanunu madde 5(1) uyarınca açık rıza). İzninizi istediğiniz zaman geri alabilirsiniz; geri almak daha önce gönderilenleri etkilemez. <SATIN ALMALARIN VE SATIN ALMA OLAYLARININ HUKUKİ DAYANAĞI, HUKUKÇU ONAYLAYACAK>",
            "Yaşadığınız yere göre, hakkınızda hangi verinin tutulduğunu sorma, düzeltilmesini ya da silinmesini isteme, kullanılmasına itiraz etme ve bir veri koruma kurumuna şikâyet etme hakkınız olabilir (Türkiye'de Kişisel Verileri Koruma Kurumu, https://www.kvkk.gov.tr). Bu hakları kullanmak için aşağıdaki adrese yazın.",
          ],
        },
        {
          heading: "Veri sorumlusu",
          paragraphs: ["Burada anlatılan verilerin sorumlusu <YASAL AD> (SaturnWave), <ADRES>."],
        },
        {
          heading: "Çocuklar",
          paragraphs: [
            "Oyun çocuklar için yapılmadı. Kimseye yaşını, adını ya da e-posta adresini sormaz; oyun istatistiklerini yalnızca evet denince ve hiçbir kimlik olmadan gönderir.",
          ],
        },
        {
          heading: "Değişiklikler",
          paragraphs: [
            "Oyun başka bir şey göndermeye başlarsa (ör. reklam), bu politika değişiklik gelmeden önce güncellenir ve oyun gereken izni sorar. En son değişikliğin tarihi sayfanın başında yazar.",
          ],
        },
      ],
    },
  },
```

**Metnin dayanakları:** mağaza ve ödeme [P1][P3]; fiş cihazda [L2 §1.6]; Unity olayının içeriği [L1 §3.4]; Unity'nin rolü ve amaçları [P6][P9]; Unity'nin aktarımları ve politika adresi [P9][P10]; istek adresi [P6][P10]; Aptabase şirketi, bölgesi, saklama, işleyici rolü, satmama [P11]; IP saklamama, ülke ve bölge [P12][P13]; günlük karma [P11]; oturum numarası ve gönderilen alanlar [P14]; Android yedeği [P15]; haklar ve dayanaklar [P17][P22] (uzman okuyacak, §6).

**Dikkat:** "Delete records" oyunun İngilizce düğmesinin adı (`en.json`); bugünkü sitede "Delete the records" yazıyor. Oyundakiyle aynı olsun diye değiştirildi. FACT [L3]

### 4.3 Sahibin dolduracağı yer tutucular

| Yer tutucu | Ne | Nerede |
|---|---|---|
| `<DAY> <MONTH> <YEAR>` / `<GÜN> <AY> <YIL>` | Politikanın yayımlandığı gün (v1 mağazaya çıkmadan önce) | `updated` |
| `<WHEN>` / `<NE ZAMAN>` | Rıza kartının çıktığı an (öneri: "After you finish the first level" / "İlk bölümü bitirdiğinizde") | İstatistik bölümü |
| `<EVENT LIST>` / `<OLAY LİSTESİ>` | Analiz aşamasının gönderdiği olaylar, sade dille | İstatistik bölümü; oyundaki `PrivacyParagraphStatistics` ile aynı |
| `<LEGAL NAME>`, `<ADDRESS>` / `<YASAL AD>`, `<ADRES>` | Veri sorumlusunun yasal adı ve adresi (GDPR md. 13, KVKK md. 10 kimlik ister [P17][P22]) | "Who is responsible" |
| `<LEGAL BASIS …>` | Satın alma ve Unity olayları için dayanak | Uzman (§6) |
| `site.contactEmail` | İletişim adresi (Google politikada iletişim ister [P4]) | `site.ts`; oyunun Destek kartı da kullanır |
| `<GIZLILIK_URL>` | Sitenin adresi (GitLab Pages) | Oyundaki bağlantı, Play Console, App Store Connect |

---

## 5. Mağaza beyanları

İki konsol İngilizce; alan adları konsoldaki gibi İngilizce, gerekçe Türkçe. **Beyan, kütüphanenin yapabildiğine değil v1'in gerçekte gönderdiğine göre.** §3.7'deki koşullar değişirse beyan da değişir. ESTIMATE

### 5.1 App Store Connect > App Privacy

**Tanımlar (FACT [P1]):** "collect" = veriyi cihaz dışına, gerçek zamanlı isteğe gereğinden uzun erişilebilir biçimde göndermek. Apple'ın topladığı veri ve uygulamanın hiç görmediği ödeme bilgisi beyan edilmez. Uygulamaya eklenen üçüncü taraf kodunun (analiz araçları, SDK'lar) topladığı veri beyan edilir. Kişisel veri "linked" sayılır. "Tracking" = uygulamadan toplanan kullanıcı ya da cihaz verisini reklam için üçüncü taraf verisiyle birleştirmek ya da veri simsarıyla paylaşmak. IP'den çıkarılıp saklanan kaba konum "Coarse Location"dır; saklanmayan IP beyan edilmez.

| Soru | Cevap | Gerekçe |
|---|---|---|
| Do you or your third-party partners collect data from this app? | **Yes, we collect data from this app** | Unity olayları ve Aptabase. FACT [L1][P13] |

| Data type | Collected | Linked to the user | Used for tracking | Purposes | Kimden, neden |
|---|---|---|---|---|---|
| **Purchases > Purchase History** | Yes | **Yes** | No | App Functionality; Analytics; Other Purposes | Unity: ürün, fiyat, işlem kimliği, fiş; iOS'ta IDFV ile birlikte → bağlı. Unity'nin amaçları: sahtekârlık ve güvenlik (App Functionality), mali raporlama, denetim, yasal yükümlülük (Other Purposes) [P6][P9]. Aptabase mağaza olayı alırsa o kısım bağlı değil, ama tür için tek cevap var. ESTIMATE |
| **Identifiers > Device ID** | Yes | **Yes** | No | App Functionality; Analytics; Other Purposes | Unity olayındaki IDFV [L1 §3.4]; Apple'ın Device ID tanımı "advertising identifier, or other device-level ID" [P1]. ESTIMATE |
| **Identifiers > User ID** | No | — | — | — | Hesap yok, Unity Authentication başlatılmıyor [L1 §3.3]. ESTIMATE |
| **Usage Data > Product Interaction** | Yes | **No** | No | Analytics | Aptabase olayları ("saved place in a game" dahil [P1]). Aptabase'in kendi rehberi: yalnız bu tür, amaç Analytics, linked No, tracking No [P13]. FACT (satıcı rehberi) / ESTIMATE |
| **Location > Coarse Location** | Yes | **No** | No | Analytics | Aptabase IP'den ülke ve bölge çıkarıp saklar; rehberi Coarse Location'ı işaretletiyor [P13][P1]. Unity'nin ülke bilgisi Google tablosunda geçici [P7]. ESTIMATE |
| **Diagnostics > Other Diagnostic Data** | Yes | **Yes** | No | App Functionality; Analytics | Unity olayındaki cihaz modeli, işletim sistemi sürümü, dil, açılış zamanı, disk alanı, kurulma zamanı [L1 §3.4]. Apple'da "cihaz bilgisi" diye ayrı tür yok; Unity Google tablosunda bunları "App diagnostics / Other app performance data" sayıyor [P7]. Seçenek: "Other Data > Other Data Types". ESTIMATE |
| Diagnostics > Crash Data, Performance Data | No | — | — | — | Aptabase'in hata raporlaması kapalı [P14][P13]; IAP'nin teşhis ölçümleri UGS başlamadan gitmiyor [L1 §3.4]. ESTIMATE |
| Financial Info > Payment Info | No | — | — | — | Ödeme App Store'da; uygulama görmüyor [P1]. FACT |
| Contact Info, Health & Fitness, Sensitive Info, Contacts, User Content, Browsing History, Search History, Usage Data > Advertising Data, Other Usage Data | No | — | — | — | Oyun bunları toplamıyor. ESTIMATE |

- **Tracking:** hiçbir türde "Yes" yok. Aptabase reklam ve takip yapmıyor [P11][P13]; Unity'nin satış ve fatura verisi için yazdığı amaçlar arasında reklam yok [P6][P9]. Bu yüzden App Tracking Transparency sorusu ve `NSUserTrackingUsageDescription` gerekmez [P2 5.1.2(i)]. ESTIMATE. Not: Unity'nin oyuncu gizlilik politikası, Unity Ads için geliştiricilerin uygulamalarından gelen veriyi kişiselleştirilmiş reklamda kullandığını söylüyor [P10]; bu Unity Ads'e ait, bizde Unity Ads yok. FACT [P10][L1]
- **Ürün sayfasında görünen:** "Data Linked to You": Purchases, Identifiers, Diagnostics. "Data Not Linked to You": Usage Data, Location. ESTIMATE
- **Privacy Policy URL:** `<GIZLILIK_URL>/` (İngilizce sayfa). Apple politikaya hem App Store Connect'te hem uygulamanın içinde bağlantı istiyor [P2 5.1.1(i)]. FACT
- **Gizlilik manifesti (iOS dalı):** Unity IAP 5.4+'ın kendi manifesti toplanan veri türü bildirmiyor, tracking false; gerekçeli API'ler FileTimestamp C617.1 ve DiskSpace E174.1 [P8]. Unity'ye göre manifest uygulamanın ve SDK'ların topladığı veri türlerini listeler [P8]. Uygulamanın kendi `PrivacyInfo.xcprivacy`'sinde yukarıdaki türlerin aynısı yazılmalı; aptabase-unity 0.3.1'in deposunda manifest dosyası görmedik [P14]. ESTIMATE (iOS aşaması)

### 5.2 Google Play Console > App content > Data safety

**Tanımlar (FACT [P3]):** "collect" = uygulamadan cihaz dışına göndermek; kütüphane ve SDK'lar dahil. "Sharing" = üçüncü tarafa aktarmak; hizmet sağlayıcıya (bizim adımıza işleyen) aktarım, yasal amaçlı, kullanıcının başlattığı ve tam anonimleştirilmiş aktarım sayılmaz. Yalnız bellekte işlenen veri (ephemeral) forma yazılır ama sayfada gösterilmez. Google Play faturalandırmasının topladığı ve uygulamanın görmediği ödeme bilgisi beyan edilmez; "Purchase history" ayrı bir türdür. IP'den çıkarılan konum "Approximate location" olarak beyan edilir. Bir veri yalnız bütün kullanıcılar seçebiliyorsa "optional" olur. Silme isteği rozeti, bir silme yolu sunuluyorsa ya da veri 90 gün içinde silinip anonimleşiyorsa verilir. SDK'ların topladığını beyan etmek geliştiricinin sorumluluğu.

**Data collection and security**

| Soru | Cevap | Gerekçe |
|---|---|---|
| Does your app collect or share any of the required user data types? | **Yes** | Unity olayları, Aptabase. [L1][P14] |
| Is all of the user data collected by your app encrypted in transit? | **Yes** | Aptabase adresleri `https://` [P14]; Unity kendi tablosunda "encrypted in transit: Yes" [P7]. FACT |
| Account creation | **Hesap yok** (formdaki "my app does not allow users to create an account" seçeneği) | Hesap yok; hesap silme kuralı yalnız hesap açtıran uygulamalara [P4]. FACT / ESTIMATE (seçeneğin tam adı) |
| Do you provide a way for users to request that their data is deleted? | **No** (öneri) | Aptabase notlarında kimlik yok, bir oyuncununkini ayıramayız [P12][P14]; Unity IAP'nin kendi silme yolu yok ve Unity kendi tablosunda "No" diyor [P6][P7]. Seçenek: sahip e-posta isteklerini Unity'ye iletmeyi üstlenirse "Yes"; o zaman yolun gerçekten çalışması gerekir. ESTIMATE |

**Data types** (Android; IDFV yalnız iOS'ta olduğu için burada yok)

| Category > type | Collected | Shared | Ephemeral | Required / optional | Purposes | Gerekçe |
|---|---|---|---|---|---|---|
| **Location > Approximate location** | Yes | No | No | **Users can choose** | Analytics | Saklanan kaba konum yalnız Aptabase'ten (rızayla) [P11][P12]; Unity'ninki kendi tablosunda geçici [P7]. Daha temkinli seçenek: "Required". ESTIMATE |
| **Financial info > Purchase history** | Yes | **Yes** (öneri) | No | **Required** | Collection: App functionality; Analytics; Fraud prevention, security, and compliance. Sharing: Fraud prevention, security, and compliance | Unity satış ve fatura verisi için bağımsız veri sorumlusu [P9]: hizmet sağlayıcı istisnasına girmez [P3]. Unity'nin kendi tablosu "Shared: No" [P7]; o sayfa "4 ay önce" güncellenmiş, yani DPA'nın 2026-06-30 açıklamasından önce olabilir. Satın alma oyuncunun seçimi, ama satın alınca gönderim kapatılamıyor → Required. ESTIMATE |
| **App activity > App interactions** | Yes | No | No | **Users can choose** | Analytics | Aptabase; işleyici, yani hizmet sağlayıcı [P11][P3]. FACT / ESTIMATE |
| **App info and performance > Diagnostics** | Yes | Purchase history ile aynı cevap | No | **Required** | App functionality; Analytics | Unity olayındaki cihaz bilgisi [L1 §3.4]; Unity'nin tablosu "App diagnostics: Yes" [P7]. ESTIMATE |
| App info and performance > Crash logs | No | — | — | — | — | Aptabase hata raporu kapalı [P14]; IAP teşhisi UGS'siz gitmiyor [L1 §3.4]. Unity'nin tablosu "Yes" diyor [P7]; bu büyük olasılıkla UGS'nin başlatıldığı kurulum için. ESTIMATE |
| App info and performance > Other app performance data | No | — | — | — | — | Aynı gerekçe. Temkinli seçenek: Unity'nin tablosu gibi "Yes, Required, App functionality; Analytics". ESTIMATE |
| **Device or other IDs** | Yes | Purchase history ile aynı cevap | No | **Required** | App functionality; Analytics | Unity olayındaki Unity oturum kimliği; Unity "Unity Installation ID" ve oturum kimliklerini her zaman topluyor diyor [P6][L1 §3.4]. ESTIMATE |
| Personal info > User IDs | No | — | — | — | — | Unity'nin tablosundaki "Personal identifiers: Yes" Authentication oyuncu kimliğinden geliyor [P6][P7]; biz başlatmıyoruz [L1 §3.3]. ESTIMATE |
| Personal info > Email address | No | — | — | — | — | Unity'nin tablosundaki e-posta web mağazası ve doğrudan ödeme içindir [P6]; kullanmıyoruz. ESTIMATE |
| Financial info > öteki türler, Personal info > öteki türler, Health and fitness, Messages, Photos and videos, Audio, Files and docs, Calendar, Contacts, Web browsing, App activity > öteki türler | No | — | — | — | — | Oyun bunları toplamıyor. ESTIMATE |

**Aynı konsolda ilgili beyanlar**
- **Advertising ID:** "Does your app use advertising ID?" → **No**. Android 13 ve sonrasını hedefleyen uygulama reklam kimliği kullanıyorsa `AD_ID` iznini bildirmeli [P5]; birleşik manifestte bu izin yok [L1 §8]. Her derlemeden sonra izin dökümüyle bakılmalı. FACT / ESTIMATE
- **Privacy policy:** Play Console'daki alana etkin, herkese açık, bölgeye göre kapanmayan bir URL (PDF olamaz) [P4]. FACT
- **Prominent disclosure:** rıza kartı (§3.3) bu kuralın biçimine uygun yazıldı [P4]. ESTIMATE
- **Google Play ödeme kütüphanesinin kendi kayıtları:** Google'ın ödeme sistemine ait; uygulama görmüyor. Beyan edilmemesi öneriliyor, ama Play Console'un SDK uyarılarına ve Google'ın SDK Index sayfasına bakılmalı. ESTIMATE [P3][L2 §1.4]

---

## 6. Hukuk notları (uzmana sorulacak; tavsiye değil)

### 6.1 Avrupa Birliği (GDPR, ePrivacy)

1. **Unity'nin kapatılamayan satın alma olayları ve rıza.** EDPB'ye göre cihaza yüklenen yazılımın ya da bir SDK'nın cihazı bir sunucuya bilgi göndermeye yönlendirmesi ePrivacy 5(3) anlamında "erişim"dir; bilgiyi gönderten ve alan farklı şirketler olabilir [P16 §32–34, §42]. Kesin gerekli olmayan erişim rıza ister. Satın alma bu olaylar olmadan da çalışıyor (5.3.1'de bu kod yok) [L1 §3.4]; yani "kesin gerekli" sayılmayabilirler. Apple da kullanıcı verisi toplayan uygulamadan, veri anonim olsa bile rıza istiyor [P2 5.1.1(ii)]. **Soru:** sahibin kararıyla (5.4.4, yama yok) AB'de bu gönderim savunulabilir mi; açıklama (§3.5'teki satır, politika) yeter mi? ESTIMATE
2. **Unity ile kim, neyin sorumlusu?** Unity satış ve fatura verisi için kendini bağımsız veri sorumlusu sayıyor [P9]. Gönderimi tetikleyen kod bizim uygulamamızda. Avrupa Adalet Divanı, sitesine üçüncü tarafın eklentisini koyan işletmeyi, verinin toplanıp aktarılması aşamasında ortak sorumlu saymıştı [P18]. **Soru:** toplama aşamasında Unity'yle ortak sorumlu muyuz; ortak sorumluluk anlaşması gerekir mi? ESTIMATE
3. **Hukuki dayanak.** İstatistik için rıza (md. 6(1)(a)) [P17]. Satın almanın kendisi ve Unity'ye giden olaylar için dayanak (sözleşmenin ifası mı, meşru menfaat mi) uzman kararı. Politika taslağında yer tutucu bırakıldı. ESTIMATE
4. **Rızanın geçerliliği ve yaş.** Rıza özgür, belirli, bilgilendirilmiş, açık olmalı; geri almak vermek kadar kolay (md. 4(11), 7(3)) [P17]. Çocuğa doğrudan sunulan hizmette rızaya dayanılıyorsa 16 yaş altı için ebeveyn izni gerekir; üye devletler sınırı 13'e kadar indirebilir (md. 8) [P17]. Oyun yaş sormuyor. **Soru:** yaş sormadan rıza istemek yeterli mi, yoksa yaş beyanı mı gerekir? ESTIMATE
5. **AB temsilcisi.** AB'de yerleşik olmayan bir sorumlu, AB'deki kişilere mal ya da hizmet sunuyor ya da davranışlarını izliyorsa GDPR uygulanır (md. 3(2)) ve bir AB temsilcisi atanmalıdır; ara sıra yapılan, büyük ölçekli özel nitelikli veri içermeyen, risk doğurmayan işleme istisnadır (md. 27) [P17]. **Soru:** istisna bize uyar mı? ESTIMATE
6. **Silme ve erişim istekleri.** Sorumlu kişiyi tanıyamıyorsa, yalnız GDPR'ye uymak için ek bilgi tutmak zorunda değil; erişim ve silme gibi haklar o ölçüde sınırlanır (md. 11) [P17]. Politikadaki "notlarınızı ayıramayız" cümlesi buna dayanıyor. **Soru:** Aptabase'in sakladığı veri (ülke, işletim sistemi, dil, 60 dakikalık oturum numarası, günlük karma) kişisel veri sayılır mı? ESTIMATE
7. **AB dışına aktarım.**
   - Aptabase: AB'de yerleşik (Romanya) ve AB bölgesinde (Almanya) işliyor [P11]; AB oyuncularının verisi için üçüncü ülkeye aktarım görünmüyor. ESTIMATE
   - Unity: ABD ve başka ülkeler; DPA standart sözleşme maddelerine dayanıyor, AB-ABD Veri Gizliliği Çerçevesi'ni (DPF) anmıyor [P9][P10]. DPF için yeterlilik kararı 2023-07-10 [P19]; Genel Mahkeme 2025-09-03'te geçerli saydı (T-553/23), karara karşı Adalet Divanı'na itiraz edildi (C-703/25 P, 2025-10-31) [P20]. Unity'nin DPF listesinde olup olmadığına bakamadık [P21]. FACT / ESTIMATE
8. **Bilgilendirme içeriği.** Politika md. 13'ün saydıklarını içermeli: sorumlunun kimliği ve iletişimi, amaçlar ve dayanaklar, alıcılar, aktarımlar, saklama süreleri, haklar, şikâyet hakkı [P17]. Taslakta yasal ad ve adres yer tutucu. FACT / ESTIMATE
9. **Birleşik Krallık:** Unity'nin aktarımları için İngiltere'nin kendi maddeleri de var [P9]; UK GDPR'nin aynı soruları (temsilci, dayanak) ayrıca sorulmalı. ESTIMATE

### 6.2 Türkiye (KVKK)

1. **Veri sorumlusu Türkiye'de.** Aydınlatma yükümlülüğü (md. 10): sorumlunun kimliği, amaçlar, kime ve neden aktarıldığı, toplama yöntemi ve hukuki sebep, md. 11'deki haklar [P22]. Politika taslağı bunları içeriyor; yasal ad, adres ve iletişim eksik. FACT / ESTIMATE
2. **Açık rıza (md. 5(1), tanım md. 3):** istatistik için. **Soru:** aydınlatma metni ile rıza ayrı metinler mi olmalı; rıza kartı ile politika bağlantısı yeter mi? ESTIMATE
3. **Yurt dışına aktarım (md. 9, 7499 sayılı Kanunla 2024'te değişti).** Yeterlilik kararı yoksa uygun güvencelerden biri gerekir; bunlardan biri Kurul'un ilan ettiği standart sözleşme. Standart sözleşme imzadan sonra **beş iş günü** içinde Kurul'a bildirilir. Bu ikisi de yoksa aktarım yalnız **arızi** olmak şartıyla, örneğin riskler anlatılarak alınan açık rızayla yapılabilir [P22 md. 9(1), (4), (5), (6)]. Açık rızaya dayanan aktarımların 2024-09-01'e kadar sürebildiği, bundan sonra düzenli aktarımın açık rızaya dayanamayacağı aktarılıyor (yönetmelik: RG 10.07.2024, 32598) [P24]. FACT (Kanun metni) / REPORTED (geçiş tarihi)
   - Aptabase (Almanya, Romanya şirketi) ve Unity (ABD): düzenli istatistik "arızi" değil. **Soru:** Aptabase ile Kurul'un standart sözleşmesi (veri sorumlusundan veri işleyene) imzalanabilir mi; Aptabase'in buna yanaşıp yanaşmadığı bilinmiyor. Unity olayları cihazdan doğrudan Unity'ye gidiyor ve Unity kendini bağımsız sorumlu sayıyor: bu bizim aktarımımız mı? ESTIMATE
   - Kurul'un yeterlilik kararı verdiği ülke listesini bu çalışmada doğrulayamadık; kvkk.gov.tr'den bakılmalı [P23]. FACT (bakılamadı)
   - Seçenek (uzman ve sahip için): Aptabase sunucusunu (AGPLv3) Türkiye'de kendimiz barındırmak aktarım sorusunu ortadan kaldırır, ama "sunucu yok" kararını değiştirir [L2 §3.2]. ESTIMATE
   - **Kişisel veri mi?** Kişisel veri, kimliği belirli ya da belirlenebilir gerçek kişiye ilişkin her bilgi (md. 3) [P22]. Aptabase'in sakladığı veri anonim sayılırsa KVKK saklanan veriye uygulanmayabilir; IP'nin girişte işlenmesi yine işlemedir. Uzman kararı. ESTIMATE
4. **VERBİS.** Kayıt istisnası için 50 çalışan ve 100 milyon TL yıllık mali bilanço eşikleri (2023/1154) ve 2025'te uygulama esaslarını açıklayan bir Kurul kararı (2025/2393) aktarılıyor [P25]. Küçük bir stüdyo büyük olasılıkla istisnaya girer; istisna, Kanun'un öteki yükümlülüklerini kaldırmaz [P25]. REPORTED

### 6.3 Amerika Birleşik Devletleri

1. **Kaliforniya (CCPA):** yıllık geliri 26.625.000 doları aşan (2025-01-01'den beri) ya da 100.000 Kaliforniyalının kişisel verisini alan, satan ya da paylaşan işletmelere uygulanır [P26]. Küçük bir stüdyo büyük olasılıkla eşiğin altında. **Soru:** Unity'nin bağımsız sorumlu olarak satın alma verisi alması CCPA anlamında "sale" ya da "share" sayılabilir mi? (Eşik aşılırsa önem kazanır.) FACT / ESTIMATE
2. **Öteki eyaletler:** 2026 itibarıyla yaklaşık 20 eyaletin kapsamlı gizlilik yasası olduğu aktarılıyor; eşikler çoğunlukla on binlerce tüketici [P27]. Texas yasasında tüketici sayısı eşiği yok, ABD Küçük İşletmeler İdaresi tanımına giren küçük işletmeler kapsam dışı; ama küçük işletme de hassas veriyi satmadan önce rıza almalı [P28]. Biz veri satmıyoruz. REPORTED / ESTIMATE
3. **Çocuklar (COPPA):** kural değişiklikleri 2025-06-23'te yürürlüğe girdi, uyum son günü 2026-04-22 [P30]. Oyun çocuklara yönelik değil; çocuk olduğunu bildiğimiz oyuncu yoksa uygulanmaz. Mağazalardaki hedef kitle beyanı bunu etkiler; 13 yaş altını hedef kitleye katmak hem COPPA'yı hem mağazaların çocuk kurallarını devreye sokar. FACT [P30] / ESTIMATE
4. **Uygulama mağazası yaş yasaları.** Texas SB 2420: 2025 sonunda ihtiyati tedbirle durdurulmuştu, Beşinci Daire tedbiri durdurdu ve yasa uygulanabilir hâle geldi; Apple yasanın şartlarını 2026-06-04'ten itibaren Texas'taki yeni Apple hesaplarına uyguladığını geliştiricilere duyurdu ve Declared Age Range API'yi kullanmalarını istedi [P29]. Geliştiricinin yükümlülükleri olarak aktarılanlar: mağazanın verdiği yaş kategorisini kullanmak, küçüğün indirme ve satın alması için mağazanın aldığı ebeveyn onayına dayanmak, önemli değişiklikleri (yeni veri kullanımı, eklenen para kazanma) mağazaya bildirmek [P29]. Utah ve Louisiana'da benzer yasalar var [P29]. REPORTED. **Soru:** v1 Texas'ta satılacaksa iOS ve Android aşamalarında yaş sinyali gerekir mi; ileride bir satın alma ya da veri değişikliği "significant change" bildirimi ister mi? ESTIMATE

### 6.4 Öteki notlar

- **İade:** sunucu olmadan altın paketi iadesini öğrenemiyor ve geri alamıyoruz; Google'ın iptal edilen satın alma listesi ve Apple'ın iade bildirimi sunucu ister [P31][P32]. Bu bir gizlilik değil, kayıp kabulü; sahibin kararı (L2 §1.8). FACT / ESTIMATE
- **Tüketici hukuku:** AB'de dijital içerikte cayma hakkı ve mağazaların onay adımlarının bunu nasıl karşıladığı uzmana sorulabilir. Bu belgede araştırılmadı.

---

## 7. Sahibin yapacakları ve kararları

**Onaylar**
1. Oyun içi metinler (§3.1–§3.5): gizlilik kartının yeni hâli, ayarlar satırları, rıza kartı, satın alma onayındaki satır. Onaydan sonra 12 dile çeviri (İngilizcenin taslağı §3.6'da) ve kart düzeni ayrı bir işte.
2. Rıza kartının ne zaman çıkacağı (öneri: ilk bölümden sonra).
3. İade cümlesinin sitede ne kadar açık olacağı (§3.2).
4. Çırak Çantası'nın ürün tipi (tüketilen / tüketilmeyen), IAP aşamasıyla; geri yükleme cümlesi buna bağlı.
5. Play'de "Purchase history: Shared" cevabı (öneri: Yes) ve silme isteği cevabı (öneri: No).

**Sahibin açacakları** (Claude hesap açmaz, giriş yapmaz)
1. Aptabase hesabı ve uygulaması, **AB bölgesinde**; App Key (`A-EU-…`) analiz aşamasına verilir. Yer tutucu: `<APTABASE_APP_KEY_EU>`.
2. İletişim e-postası: `games-site/src/content/site.ts` içindeki `contactEmail` ve oyunun Destek kartı.
3. Veri sorumlusunun yasal adı ve adresi (politikanın "Who is responsible" bölümü).
4. Sitenin adresi (GitLab Pages); oyundaki bağlantı, Play Console ve App Store Connect bunu kullanır.
5. Play Console: Data safety formu (§5.2), Advertising ID beyanı, gizlilik politikası adresi, hedef kitle.
6. App Store Connect: App Privacy cevapları (§5.1), gizlilik politikası adresi.
7. Uzman okuması: §6'daki sorular; özellikle §6.1.1 (Unity olayları ve rıza) ve §6.2.3 (KVKK aktarımı, Aptabase ile standart sözleşme).
8. Politika sitede **v1 mağazaya çıkmadan önce** yayımlanır; bugünkü sayfanın sözü bu [L5].

**Bu belgede yapılmayanlar:** `words/*.json` ve kod değişmedi; PDF üretilmedi (`tools/docs-pdf/build.py` belge listesine bu dosya eklenmeli: o dosyayı değiştirmek bu işin dışında); telefonda, iPad'de ya da ağ dinlemesiyle hiçbir şey denenmedi; Unity'nin olaylarının çalışırken gerçekten gittiği, kaynak kodu okumasına dayanıyor [L1 §3.4].

---

## 8. Kaynaklar

Web sayfalarının hepsine 2026-10-24'te bakıldı. "Sayfa tarihi" sayfanın kendi tarihidir; "araştırma notu" diye işaretlenenler aynı gün [L2]'de açılmış ve bu belge için yeniden açılmamış kaynaklardır.

| ID | Kaynak | URL | Sayfa tarihi |
|---|---|---|---|
| P1 | Apple, App privacy details on the App Store | https://developer.apple.com/app-store/app-privacy-details/ | tarih yok (©2026) |
| P2 | Apple, App Review Guidelines (3.1.1, 5.1.1(i)–(ii), 5.1.2(i)) | https://developer.apple.com/app-store/review/guidelines/ | tarih görünmüyor |
| P3 | Google Play Console Help, Provide information for Google Play's Data safety section | https://support.google.com/googleplay/android-developer/answer/10787469 | değişiklik notu 2023-12-05 |
| P4 | Google Play, User Data policy (privacy policy, Prominent Disclosure and Consent, account deletion) | https://support.google.com/googleplay/android-developer/answer/10144311 | tarih yok (©2026) |
| P5 | Google Play Console Help, Advertising ID | https://support.google.com/googleplay/android-developer/answer/6048248 | araştırma notu (S37) |
| P6 | Unity IAP, Privacy overview (5.4+) | https://docs.unity.com/en-us/iap/privacy-and-consent/overview | "Last updated 4 months ago" (≈2026-06) |
| P7 | Unity IAP, Google Play data safety (5.4+) | https://docs.unity.com/en-us/iap/privacy-and-consent/google-play-data-safety | "4 months ago" |
| P8 | Unity IAP, Apple privacy manifest (5.4+) | https://docs.unity.com/en-us/iap/privacy-and-consent/apple-privacy-manifest | "4 months ago" |
| P9 | Unity Data Processing Addendum (§3.2.3 IAP 5.4+ rolleri; §5.4, §6.9 aktarım; alt işleyiciler: https://unity.com/legal/subprocessors) | https://unity.com/legal/unity-data-processing-addendum-dpa | yürürlük 2026-04-10; IAP rolleri 2026-06-30 |
| P10 | Unity, Game Player and App User Privacy Policy | https://unity.com/legal/game-player-and-app-user-privacy-policy | Last Updated 2025-07-24; Last Edit/Review 2026-06-30 |
| P11 | Aptabase, Privacy Policy (Technov Solutions SRL; bölgeler; md. 28; 5 yıl; karma) | https://aptabase.com/legal/privacy | 2026-07-24 |
| P12 | Aptabase Documentation (toplanan ve asla toplanmayan veri) | https://aptabase.com/docs.md | tarih yok |
| P13 | Aptabase, How to fill out the Apple App Privacy when using Aptabase | https://aptabase.com/docs/apple-app-privacy.md | tarih yok |
| P14 | aptabase-unity 0.3.1, kaynak kodu (`Runtime/Aptabase.cs`, `Environment.cs`, `WebRequestHelper.cs`, `Settings.cs`, `Dispatcher/Dispatcher.cs`), MIT | https://github.com/aptabase/aptabase-unity/tree/0.3.1 | etiket 0.3.1 (commit 1383186); son commit 2026-09-04 |
| P15 | Android Developers, Back up user data with Auto Backup | https://developer.android.com/identity/data/autobackup | 2026-10-23 |
| P16 | EDPB, Guidelines 2/2023 on the Technical Scope of Art. 5(3) of the ePrivacy Directive, v2 (§32–34, §42) | https://www.edpb.europa.eu/system/files/documents/2024-10/edpb_guidelines_202302_technical_scope_art_53_eprivacydirective_v2_en_0.pdf | 2024-10-07 |
| P17 | Regulation (EU) 2016/679 (GDPR): md. 3(2), 4(11), 6(1), 7(3), 8, 11, 13, 27 | https://eur-lex.europa.eu/eli/reg/2016/679/oj | 2016-04-27 (metin) |
| P18 | CJEU, C-40/17 Fashion ID | https://curia.europa.eu/juris/liste.jsf?num=C-40/17 | karar 2019-07-29 |
| P19 | European Commission, EU-US data transfers (DPF yeterlilik kararı, 2023/1795) | https://commission.europa.eu/law/law-topic/data-protection/international-dimension-data-protection/eu-us-data-transfers_en | karar 2023-07-10 |
| P20 | Official Journal, Case C-703/25 P, Latombe v Commission (Genel Mahkeme T-553/23, 2025-09-03, karara itiraz) | https://eur-lex.europa.eu/eli/C/2025/6610/oj/eng | itiraz 2025-10-31 |
| P21 | Data Privacy Framework List | https://www.dataprivacyframework.gov/list | Unity'nin kaydına bakılamadı |
| P22 | 6698 sayılı Kişisel Verilerin Korunması Kanunu (md. 3, 5, 9, 10, 11) | https://www.mevzuat.gov.tr/mevzuatmetin/1.5.6698.pdf | md. 9: "Değişik: 2/3/2024-7499/34 md." |
| P23 | KVKK, Standart sözleşmelerde dikkat edilecek hususlar duyurusu; Yurt dışına aktarım sayfası | https://www.kvkk.gov.tr/Icerik/8170/Yurt-Disina-Kisisel-Veri-Aktariminda-Kullanilacak-Standart-Sozlesmelerde-Dikkat-Edilmesi-Gereken-Hususlara-Iliskin-Kamuoyu-Duyurusu ; https://www.kvkk.gov.tr/Icerik/2053/Yurtdisina-Aktarim | tarih görülmedi |
| P24 | Cottgroup, Standart sözleşme bildirim yükümlülüğü ve yaptırımları (REPORTED) | https://cottgroup.com/tr/blog/kvkk-gdpr/item/standart-sozlesme-bildirim-yukumlulugu-ve-yaptirimlari | 2026 ceza tutarlarını anıyor |
| P25 | Erdem & Erdem, VERBİS kayıt yükümlülüğüne ilişkin yeni istisnalar (REPORTED) | https://www.erdem-erdem.av.tr/bilgi-bankasi/verbis-kayit-yukumlulugune-iliskin-yeni-istisnalarin-uygulama-esaslari-aciklandi | 2025/2393 kararını anıyor |
| P26 | California Privacy Protection Agency, CPI adjustment (eşikler) | https://cppa.ca.gov/regulations/cpi_adjustment.html | 2025-01-01'den geçerli (araştırma notu S67) |
| P27 | IAPP, US State Privacy Legislation Tracker; ikincil özet: Enzuzo (REPORTED) | https://iapp.org/resources/article/us-state-privacy-legislation-tracker/ ; https://www.enzuzo.com/blog/us-state-privacy-laws | 2026 |
| P28 | Texas State Law Library, Texas Data Privacy and Security Act; Future of Privacy Forum (REPORTED) | https://sll.texas.gov/spotlight/2024/07/texas-data-privacy-and-security-act ; https://fpf.org/blog/the-right-to-be-let-a-lone-star-state-texas-passes-comprehensive-privacy-bill/ | 2024-07 / 2023 |
| P29 | Morrison Foerster, Texas App Store Accountability Act güncellemesi; 9to5Mac, Apple'ın geliştirici duyurusu (REPORTED) | https://www.mofo.com/resources/insights/251111-texas-targets-app-stores-with-new-accountability-law ; https://9to5mac.com/2026/06/03/apple-says-texas-app-store-age-assurance-rules-start-tomorrow-after-court-ruling/ | 2026-06-10 / 2026-06-03 |
| P30 | Federal Register, Children's Online Privacy Protection Rule | https://www.federalregister.gov/documents/2025/04/22/2025-05904/childrens-online-privacy-protection-rule | 2025-04-22 (araştırma notu S68) |
| P31 | Apple, Handling refund notifications; Google Play Console Help, refunds | https://developer.apple.com/documentation/storekit/handling-refund-notifications ; https://support.google.com/googleplay/android-developer/answer/2741495 | araştırma notu (S29, S32) |
| P32 | Google Play Billing, Fight fraud and abuse (Voided Purchases API) | https://developer.android.com/google/play/billing/security | 2026-10-20 (araştırma notu S21) |
| L1 | `docs/research/iap-and-analytics.md` §3.1, §3.3, §3.4, §8 (deneme derlemesi; IAP 5.4.4 kaynak kodu) | yerel | 3c03498 |
| L2 | `docs/research/iap-and-analytics-notes.md` §1.4, §1.6–§1.8, §3.2 | yerel | 3c03498 |
| L3 | `words/tr.json` satır 200, 213–227; `words/en.json` satır 218; `unity/Assets/Scripts/Flow/PrivacyWords.cs` | yerel | 78dcb39 |
| L4 | `docs/GAME_DESIGN.md` §20 (satır 957–962), §23 arayüz aşaması 13 (satır 1271–1286) | yerel | 78dcb39 |
| L5 | `C:/Users/arcan/games-site/src/content/games.ts` ("22 October 2026"), `site.ts`, `src/components/PrivacyPolicyView.tsx`, `README.md` (salt okundu) | yerel | çalışma ağacı |
| L6 | `docs/research/monetization-and-retention.md` §3.2, §3.3, §7.3 | yerel | 78dcb39 |
