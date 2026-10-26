# Sunucu v1: Görünmez Hesap, Bulut Kaydı ve Satın Alma Doğrulaması (Plan)

**Tarih:** 2026-10-26. **Durum:** plan, sahibin kararları §10.2'de (2026-10-26). **Aşama 1 (eşitleme çekirdeği) yapıldı (2026-10-26):** Unity'siz kod ve sahte bulutla testler; ayrıntı `GAME_DESIGN.md` §23, kesinleşen kurallar §3.2–§3.5'teki "Aşama 1" notlarında. Hesap açılmadı, hiçbir yere giriş yapılmadı; bulut projesi henüz bağlı değil.

**Dayandığı kararlar (proje sahibi, 2026-10-26):** v1'e hafif bir sunucu; tam koruma v2'de. v1'de görünmez oyuncu hesabı, ilerlemenin ve cüzdanın bulut kaydı, satın alma fişlerinin sunucuda doğrulanması, yeni telefonda ya da yeniden kurulumda geri yükleme. Oyun internetsiz oynanmaya devam eder. Geri yükleme Google Play Games (Android) ve Game Center (iOS) ile; e-posta ve parola yok. Barındırma **Unity Gaming Services (UGS)**, yalnız ücretsiz katman: ücretli plan yok, kart yok. v2'de cüzdan sunucuda olacak ve kazanılan bölümler deneme kayıtlarının motorla yeniden oynatılmasıyla doğrulanacak; v1 buna uyacak biçimde kurulur.

**Okunanlar:** `GAME_DESIGN.md` §17, §19, §20, §23 (satın almalar, istatistik, inceleme ve 26 Ekim düzeltmeleri), §25; [iap-and-analytics.md](iap-and-analytics.md); [privacy-v1.md](privacy-v1.md); [monetization-and-retention.md](monetization-and-retention.md) §7.3; kod grafiği ve kod: `ProgressFile`, `IProgressStore`, `PlayerProgress`, `ProgressDocument`, `PurchasesMade`, `WalletLedger`, `AttemptLogFile`, `Puzzle.Core/Recording`; IAP 5.4.4'ün kaynağı (`unity/Library/PackageCache/com.unity.purchasing@215c35b51cf4`).

**Etiketler** (kardeş belgelerdeki gibi)
- **FACT**: resmî belgede, paketin kaynak kodunda, paket kaydında ya da depomuzda gördük.
- **REPORTED**: arama sonucunun özeti, forum ya da satıcının iddiası; sayfanın kendisi okunmadı ya da doğrulanmadı.
- **ESTIMATE**: bizim çıkarımımız, hesabımız ya da önerimiz.

**Kaynaklar:** `[U..]` Unity, `[G..]` Google, `[A..]` Apple, `[L..]` yerel dosya; URL'ler ve tarihler §11'de. Bütün web sayfalarına 2026-10-26'da bakıldı.

**Hukuk uyarısı:** §6'daki hukuk notları tavsiye değildir; yayından önce bir uzmana okutulmalı.

**Kısa sözlük**
- **UGS:** Unity Gaming Services, Unity'nin bulut hizmetleri. **Bulut projesi:** bu hizmetler için Unity'nin sitesinde açılan proje; Unity Editor'daki oyun projesine "bağlanır" (link).
- **Authentication:** UGS'nin oyuncu hesabı hizmeti. **Anonim hesap:** e-postasız, parolasız, telefona verilen hesap. **Bağlama (link):** anonim hesaba bir Google Play Games ya da Game Center kimliği eklemek.
- **Cloud Save:** UGS'nin anahtar–değer deposu. **Player Data** oyuncuya ait; **Protected** sınıfını oyuncu okur ama yalnız sunucu yazar. **Custom item (Game Data):** oyuncuya değil oyuna ait kayıt.
- **Cloud Code:** UGS'de çalışan sunucu kodumuz; C# modülü (`.ccm` dosyası). **Secret Manager:** UGS'nin gizli anahtar kasası.
- **PGS:** Google Play Games Services v2. **GC:** Apple Game Center.
- **writeLock:** Cloud Save'in sürüm belirteci. Okuyunca alınır, yazarken geri verilir; arada başkası yazdıysa yazma reddedilir.
- **Satın alma belirteci (purchase token):** Google'ın bir satın almaya verdiği uzun kimlik. **JWS:** Apple'ın imzalı işlem kaydı (`jwsRepresentation`).
- **Defter (ledger):** sunucunun bir oyuncu için tuttuğu doğrulanmış satın alma listesi.

---

## 1. Özet

### 1.1 Oyuncu için ne değişir

- **Oyun aynı kalır.** Açılış, oynayış, mağaza, istatistik sorusu değişmez. Bölüm, internet olmadan da oynanır.
- **Görünmez hesap.** Oyun ilk açılışta oyuncu için bir hesap açar. Ad, e-posta, parola sorulmaz. Telefonda Google Play Games (iPhone ve iPad'de Game Center) açıksa hesap ona bağlanır.
- **Kayıt bulutta da durur.** İlerleme, cüzdan (satın alınan altın dahil) ve satın almaların listesi buluta eşitlenir. Oyun silinip kurulunca ya da yeni telefonda aynı Google Play Games veya Game Center hesabıyla açılınca kaldığı yerden sürer. İki telefonda oynayanın ilerlemesi ve altını birleşir.
- **Satın almalar sunucuda doğrulanır.** Ödemeden sonra kısa bir "doğrulanıyor" anı olur; sahte satın alma altın vermez. Sunucuya ulaşılamazsa oyun bugünkü gibi davranır: ödemeyi telefonda kabul eder, doğrulamayı sonra yapar.
- **Ayarlar'da yeni satırlar:** hesabın durumu ("Google Play Games ile bağlı" ya da "Bağlan"), "Bulut kaydı" anahtarı, son eşitleme, oyuncu kodu ve "Verilerimi sil".
- **Gizlilik kartı değişir:** "Sunucumuz yoktur" cümlesi kalkar (§6.5'te yeni metin taslağı).

### 1.2 Bizim için ne değişir

- Bir **UGS bulut projesi** (proje sahibi açar, Editor'da bağlar), üç Unity paketi (Authentication, Cloud Save, Cloud Code; isteğe bağlı Remote Config), iki platform eklentisi (Google Play Games eklentisi, Apple'ın GameKit eklentisi) ve bir **Cloud Code C# modülü** (satın alma doğrulama, iade işaretleme, veri silme).
- Gizli anahtarlar (Google'ın hizmet hesabı, Apple'ın In-App Purchase anahtarı) **Unity'nin Secret Manager'ında** durur; depoya, oyuna ya da bize gelmez.
- **Ücretsiz katmanın sınırları** tasarımın parçasıdır: eşitleme sıklığı bir bütçeye bağlanır (§2.4).
- **Yerel dosya asıl kayıt olarak kalır.** Bulut bir kopya ve birleştirme ortağıdır. UGS kapanırsa ya da ulaşılamazsa oyun bugünkü gibi çalışır.
- Monetizasyon raporundaki §7.3 seçenek A ("sunucusuz") v1'de bitiyor; yerine B'nin UGS'li bir sürümü geliyor. §19'daki "Sunucu yok" kuralları (iade, silip kurma) değişecek.

### 1.3 Araştırmanın önemli bulguları

1. **Ücretsiz katman (aylık):** Authentication ücretsiz; Cloud Save 5 GiB depolama, 1.000.000 yazma, 1.000.000 okuma; Cloud Code 1.000.000 çağrı, 20 işlem saati, 1 GiB dışa trafik; Remote Config ücretsiz; Secret Manager ücretsiz. FACT [U1][U21]
2. **Sınır aşılırsa:** bir hizmette ücretsiz sınırın aşılması **bütün UGS hizmetlerini ve API'lerini** kapatır, yapılandırma değiştirilemez; 30 gün içinde ödeme bilgisi girilmezse hizmetler oyuncular için de kapanır. FACT [U2]. Kart girilmediği için fatura gelmez, ama oyunun UGS tarafı durur. Tasarım bunu sıradan bir "ağ yok" durumu gibi karşılar (§3.8).
3. **Economy hizmeti yeni projelere kapandı (2026-09-08).** Unity yeni projeler için Cloud Code + Cloud Save + Remote Config öneriyor. FACT [U4]. v2'nin cüzdanı bu yüzden kendi Cloud Save + Cloud Code modelimiz olacak (§7).
4. **Cloud Code istemci paketi 3.0.0, Unity 6000.7.0b5 istiyor;** bizim 6000.6 için doğru sürüm **2.10.4**. FACT [U10]
5. **IAP 5.4.4, UGS başlatılınca daha çok şey gönderir.** Paketin kaynağında: çekirdek başlayınca teşhis ölçümleri, bağlantı ayarları, ödeme sağlayıcı, web mağazası ve canlı içerik bileşenleri kuruluyor; Authentication paketi projedeyse oturum açılınca Unity'ye bir "oturum" olayı gidiyor (`SessionEventEmitter`, IAP'nin oyuncu ve cihaz bilgisini toplayan `PlayerData`'sıyla; içeriği ölçülecek). Unity'nin kendi sunucuda doğrulama istemcisi (`IAP_TX_VERIFIER_ENABLED`) kapalı, açmıyoruz. FACT (paket kaynağı [L5]). Ne gittiği telefonda ölçülmeli; gizlilik metni buna bağlı (Aşama 0).
6. **Apple'ın GameKit eklentisi en az iOS 15.6 istiyor;** projenin iOS alt sınırı bugün 15.0. FACT [A9][L6]
7. **UGS dışarıdan çağrı (webhook) almıyor.** Apple'ın iade bildirimleri ve Google'ın anlık bildirimleri herkese açık bir adres ister. Hafif tasarımda iadeler sorgulanarak bulunur: Google için günlük zamanlanmış görev (Voided Purchases API), Apple için oyuncu açılışında günde bir sorgu (Get Refund History). FACT (Triggers'ın olay listesi [U23], Apple [A6], Google [G7]) / ESTIMATE (çözüm)
8. **Hesap silmek veriyi silmez.** `DeleteAccountAsync()` yalnız Authentication hesabını siler; Cloud Save verisini biz silmeliyiz. FACT [U13]

### 1.4 v1'in bilerek yapmadıkları (hafif tasarım)

- Cüzdan sunucuda değil; telefon hâlâ altını kendisi hesaplar. UGS'yi kendi ağında engelleyen biri satın almayı telefonda doğrulatabilir (bugünkü durum).
- İade edilen satın almaların altını geri alınmaz; yalnız işaretlenir (§4.6, sahibin kararı).
- Android ile iOS arasında kayıt taşınmaz (iki ayrı kimlik sistemi).
- Deneme kayıtları telefondan çıkmaz (v2'ye kadar).

---

## 2. Mimari

### 2.1 Parçalar

```
 Telefon (Unity 6000.6, IL2CPP)                          Unity Gaming Services (ücretsiz katman)
 ┌──────────────────────────────────────────┐            ┌────────────────────────────────────────┐
 │ progress.json (asıl kayıt, 7. düzen)     │            │ Authentication: anonim + PGS / GC      │
 │ cloud.json (taban kopya, writeLock, kuyruk)│──HTTPS──▶│ Cloud Save                             │
 │ device.json, attempts.jsonl (yerel kalır)│            │   Player Data "save"      (oyuncu yazar)│
 │                                          │            │   Protected "purchases"   (sunucu yazar)│
 │ CloudSync  ── birleştirme, bütçe, geri çekilme       │   Custom item "rcpt-…"    (yalnız sunucu)│
 │ PlayerIdentity ── PGS v2 / Game Center   │            │ Cloud Code modülü MuseumPuzzle.Server  │
 │ Checkout ── ödeme → doğrulama → teslim   │──HTTPS──▶  │   ValidatePurchase, CheckRefunds,       │
 │ IAP 5.4.4 (Google Play / StoreKit 2)     │            │   DeleteMyData, PollGoogleVoided (günlük)│
 └──────────────────────────────────────────┘            │ Secret Manager: Google + Apple anahtarı │
        │ ödeme                                          │ (Remote Config: eşitleme ayarları)      │
        ▼                                                └──────────────┬─────────────────────────┘
 Google Play / App Store ◀───────────────── doğrulama ─────────────────┘
                         (Play Developer API, App Store Server API)
```

### 2.2 Hangi UGS hizmetleri

| Hizmet | v1'de | Neden | Etiket |
|---|---|---|---|
| **Authentication** | Evet | Anonim hesap; PGS ve Game Center kimliğiyle bağlama; hesap silme | FACT [U11][U12][U13] |
| **Cloud Save, Player Data** | Evet | `save`: ilerlemenin ve cüzdanın kopyası (oyuncu yazar) | FACT [U14] |
| **Cloud Save, Protected** | Evet | `purchases`: doğrulanmış satın almaların defteri; oyuncu okur, yalnız sunucu yazar | FACT [U14] |
| **Cloud Save, Custom item (Private)** | Evet | Her satın alma belirteci için bir kayıt (`rcpt-…`); aynı fişin iki kez ya da iki hesapta kullanılmasını önler | FACT [U16] |
| **Cloud Code (C# modülü)** | Evet | Fiş doğrulama, iade işaretleme, veri silme | FACT [U18] |
| **Triggers / Scheduler** | Evet, bir görev | Günde bir Google'ın iptal edilen satın alma listesine bakmak. Ek ücreti yok; Cloud Code çağrısı sayılır; zamanlanmış çağrılar ayda 25.000'e kadar Cloud Code fiyatına dahil | FACT [U22] |
| **Secret Manager** | Evet | Google hizmet hesabı ve Apple anahtarı. Ücretsiz | FACT [U19][U21] |
| **Remote Config** | Önerilir (isteğe bağlı) | Eşitleme sıklığını ve bulut kaydını uygulama güncellemesi olmadan kısmak (ücretsiz katman yaklaşınca, ya da birleştirmede hata çıkarsa kapatmak). Ücretsiz | FACT (ücret [U1]) / ESTIMATE (gereklilik) |
| Economy | Hayır | Yeni projelere kapalı (2026-09-08); Apple tarafı eski "receipt" yolunu kullanıyor, StoreKit 2'nin JWS'ini anmıyor | FACT [U4][U5] |
| Analytics, Leaderboards, Cloud Save Files | Hayır | İstatistik Aptabase'te kalır; Files v2'de deneme kayıtları için düşünülebilir | ESTIMATE |

### 2.3 Paketler ve sürümler

Unity paket kaydından (packages.unity.com, 2026-10-26) okundu. Hepsi bugünkü `com.unity.services.core` 1.18.0 ile uyumlu; hiçbiri çekirdeği yükseltmiyor. FACT [U10]

| Paket | Sürüm (tarih) | En düşük Unity | Bağımlılıklar | Not |
|---|---|---|---|---|
| `com.unity.services.authentication` | **3.8.1** (2026-10-21) | 2022.3 | core 1.18.0, newtonsoft 3.2.2 | IAP 5.4.0'ın yeni özellikleri 3.7.1 ve üstünü istiyor [U8] |
| `com.unity.services.cloudsave` | **3.4.2** (2026-10-13) | 2022.3 | core ≥1.12.5, auth ≥3.3.1 | |
| `com.unity.services.cloudcode` | **2.10.4** (2026-07-13) | 2021.3 | core ≥1.15.1, auth ≥3.5.1, `com.unity.services.wire` 1.2.3 | **3.0.0 değil:** 3.0.0 Unity 6000.7.0b5 istiyor |
| `com.unity.remote-config` | **4.2.5** (2026-02-23) | 2021.3 | remote-config-runtime 4.0.4 | İsteğe bağlı |
| `com.unity.services.core` | 1.18.0 (değişmez) | 2022.3 | | IAP 5.4.4 zaten getiriyor |
| `com.unity.purchasing` | 5.4.4 (değişmez) | 2022.3 | | |
| Google Play Games eklentisi | **v2.3.0** (2026-09-29), GitHub `.unitypackage` | — | `play-services-games-v2` 22.1.0; minSdk 24 (bizim 26) | Unity Authentication eklentinin v11.01 ve sonrasını istiyor; v2.x onun ardılı (numaralama yeniden başladı) [U11][G9] |
| Apple Unity Plug-ins: `Apple.Core` + `Apple.GameKit` | Sürüm yayımlanmıyor; `main` dalı (son değişiklik 2026-10-19), Mac'te `python3 build.py` ile `.tgz` üretilir | — | **en düşük iOS 15.6** | Projenin iOS alt sınırı 15.0 → 15.6'ya çıkmalı [A9][L6] |

**Sunucu tarafı (Cloud Code modülü):** .NET sınıf kütüphanesi; NuGet `Com.Unity.Services.CloudCode.Core` **0.0.9** ve `Com.Unity.Services.CloudCode.Apis` **0.0.27** (kararlı sürümler; Secret Manager istemcisi 0.0.21'den beri var). Cloud Code'un .NET çalışma ortamı 9. sürüme kadar destekleniyor (REPORTED [U20]); hedef çerçeve, modül şablonunun `.csproj`'u açılınca kesinleşir (ESTIMATE: `net8.0`). Motorumuz `netstandard2.1` olduğu için modül `Puzzle.Meta.dll`'i (ürün → içerik eşlemesi) ve v2'de `Puzzle.Core.dll`'i doğrudan kullanabilir. FACT [U20][L7] / ESTIMATE

**Kullanmadığımız ama tuzak olan:** IAP'nin kendi sunucu doğrulayıcısı yalnız `IAP_TX_VERIFIER_ENABLED` tanımıyla derleniyor ve "currently only visible on staging" diyor; açılmayacak. FACT [L5]

### 2.4 Ücretsiz katman ve beklenen kullanım

**Sınırlar (aylık, [U1]):** Cloud Save 5 GiB / 1.000.000 yazma / 1.000.000 okuma; Cloud Code 1.000.000 çağrı / 20 işlem saati / 1 GiB dışa trafik; Authentication, Remote Config, Secret Manager ücretsiz. Cloud Save'in sınırları oyuncu başına: her erişim sınıfında 2.000 anahtar ve 5 MiB [U14]. Cloud Code: modül en çok 10 MB, istek başına en çok 15 saniye, yanıt en çok 2 MiB, oyuncu başına dakikada 600 istek [U18]. FACT

**Varsayımlar (ESTIMATE):**
- Günlük aktif / aylık aktif oranı **%20** (rahat bulmaca oyunları için olağan aralık %15–25).
- Günlük oyuncu: 3 oturum (§25), 2 soğuk açılış.
- v1 eşitleme kuralı (§3.4): soğuk açılışta 1 okuma; oturum sonunda değişiklik varsa ve son yüklemeden 15 dakika geçtiyse 1 yazma (günde ≈3).
- Ödeyen oyuncu aylık oyuncuların %3'ü, ayda 1,5 satın alma. Her doğrulama: 1 Cloud Code çağrısı, Cloud Save'de 2 okuma + 2 yazma, ≈1 saniye (Google ya da Apple'ı beklemek dahil).
- iOS'ta ödeyenlerin iade sorgusu günde en çok bir kez.
- `save` belgesi ≈10 KB (bin bölümü bitiren oyuncu; 64 ödeme kimliği dahil). Bir yılda biriken hesap sayısı ≈ aylık oyuncunun 5 katı (kurup bırakanlar).

| | 10 bin aylık oyuncu (≈2.000 günlük) | 100 bin aylık oyuncu (≈20.000 günlük) | Ücretsiz sınır |
|---|---|---|---|
| Cloud Save okuma | ≈121.000 (%12) | ≈1.210.000 (**%121**) | 1.000.000 |
| Cloud Save yazma | ≈181.000 (%18) | ≈1.810.000 (**%181**) | 1.000.000 |
| Cloud Save depolama (1 yıl sonra) | ≈50.000 hesap × 10 KB ≈ 0,5 GB (%10) | ≈500.000 × 10 KB ≈ 5 GB (**%100**) | 5 GiB |
| Cloud Code çağrı | ≈5.000 (%0,5) | ≈50.000 (%5) | 1.000.000 |
| Cloud Code işlem süresi | ≈1,4 saat (%7) | ≈14 saat (%70; çoğu iOS iade sorgusu) | 20 saat |
| Cloud Code dışa trafik | ≈50 MB (%5) | ≈500 MB (%50) | 1 GiB |

Hesap (ESTIMATE): okuma = günlük × 2 × 30; yazma = günlük × 3 × 30; artı satın almalar. 100 bin aylık oyuncuda Cloud Code süresinin çoğu iOS iade sorgusu (1.500 ödeyen × 30 gün × ≈1 sn); sorgu iki günde bire indirilirse yarıya iner.

**Sonuç (ESTIMATE):**
- **Varsayılan kuralla ücretsiz katman yaklaşık 50 bin aylık oyuncuya kadar yeter** (yazma sınırı: 1.000.000 / (0,2 × 3 × 30) ≈ 55 bin).
- **Daha büyük oyuncu sayısında kısma:** okuma 12 saatte bir, yükleme en az 60 dakika arayla (satın almada ve tablo/oda bitince hemen) → günlük oyuncu başına ≈1 okuma, ≈1,2 yazma. 100 bin aylık oyuncuda ≈600 bin okuma, ≈720 bin yazma: sınırın içinde ama dar. Bu ayarları güncellemesiz değiştirmek için Remote Config önerilir.
- **Depolama:** `save` belgesi sıkıştırılırsa (gzip + base64, JSON'da ≈3–4 kat) ve hiç bölüm kazanmamış hesaplar için `save` yazılmazsa 100 bin oyuncuda da sığar. 13 ay girmemiş, satın alması olmayan hesapları silen aylık bir görev v1.x'te eklenebilir.
- **Unity uyarıları:** ücretsiz sınırın %75'inde ve %100'ünde e-posta uyarısı geliyor, eşik değiştirilemiyor (REPORTED [U3]). Proje sahibi bu e-postaları görünce Remote Config'teki aralıkları açar.
- **Kural (beautiful-code ile uyumlu):** her yazma bir bütçeden düşer; aynı belge değişmediyse yazılmaz; hata sonrası yeniden deneme üstel geri çekilmeyle (1 dk → 24 sa tavan) yapılır; döngüye giren deneme yok.

### 2.5 Neden UGS, neden kendi Cloud Code'umuz

- **Google Play Games'in Saved Games'i + iCloud/GameKit kayıtları** (rapor §7.3 B): ücretsiz, ama iki ayrı sistem, sunucuda fiş doğrulama yok, v2'nin sunucu cüzdanına yol yok. ESTIMATE
- **Economy'nin gerçek para satın almaları:** yeni projeye kapalı [U4]; Google tarafı yalnız Google Play lisans anahtarı istiyor (imza denetimi; Play Developer API'ye sorduğunu belgelemiyor) [U6]; Apple tarafı uygulama fişi (`Receipt`) alıyor [U5], oysa IAP 5 StoreKit 2'de JWS veriyor ve Unity eski fiş yolunun Apple'ca kaldırıldığını söylüyor [U7]. FACT / ESTIMATE (yetersizlik)
- **IAP'nin içindeki Unity doğrulayıcısı:** kapalı ve "staging" [L5]. Unity'nin belgesi IAP'nin uzaktan doğrulama hizmeti sunmadığını, kendimizinkini kurmamızı söylüyor [U7]. FACT

---

## 3. Veri modeli ve eşitleme

### 3.1 Ne nerede durur

| Veri | Telefonda | Bulutta (v1) | Gerekçe |
|---|---|---|---|
| İlerleme: kazanılan bölüm, bölümdeki deneme sayısı, yerleştirilen yapboz parçaları, öğretici adımları | `progress.json` | `save` (Player Data, Default) | Geri yükleme |
| Cüzdan: altın, booster'lar, canlar, günlük sayaçlar, açılış hediyeleri | `progress.json` | `save` | Satın alınan altın dahil |
| Satın almalar (ürün sayıları, son 64 ödeme kimliği) | `progress.json` | `save` | Bugünkü "bir kez teslim" kaydı |
| Teslim edilen defter numaraları (yeni) | `progress.json` (7. düzen) | `save` | Cihazlar arası "bir kez teslim" (§3.7) |
| Doğrulanmış satın alma defteri | (okunur, saklanmaz) | `purchases` (Protected) | Sunucu yazar; oyuncu okur |
| Belirteç kaydı (fiş başına) | — | `rcpt-<özet>` (Custom item, Private) | Aynı fişin iki kez ya da iki hesapta kullanılmasını önler |
| Ayarlar (dil, müzik, titreşim) ve istatistik izni | `device.json` | **Yok** | Cihaza özgü; izin cihazda verilir (§19) |
| Deneme kayıtları | `attempts.jsonl` | **Yok** (v2'ye kadar) | Gizlilik metni "telefondan çıkmaz" diyor |
| Eşitleme durumu (taban kopya, writeLock, kuyruk, geri çekilme) | `cloud.json` (yeni) | — | §3.3 |
| Authentication oturum belirteci | Unity'nin PlayerPrefs'i | — | Unity yönetir |

**Test modu ve test derlemesi:** test modunda (sınırsız altın) bulut kaydı **kapalıdır**; `build.realMoneyPurchases: "notAvailable"` derlemeleri UGS'nin `development` ortamını kullanır. ESTIMATE (kural önerisi)

### 3.2 Anahtarlar, boyutlar, sürümleme

**`save`** (Player Data, Default sınıf; tek anahtar, tek yazma). Örnek (`//` satırları yalnız açıklama):

```json
{
  "envelope": 1,
  "progressLayout": 7,
  "appVersion": "1.1.0",
  "revision": 42,
  "writtenAtUtc": "2026-11-03T18:22:05Z",
  "writer": "c1f0…",           // eşitlemeye özel rastgele kurulum kimliği; deneme kayıtlarının kimliği DEĞİL
  "writes": ["…", "…"],        // Aşama 1: kaydın geldiği son 16 yazmanın rastgele kimliği, kendisininki sonda
  "progress": { … }            // ProgressDocument, 7. düzen; ≥16 KB olursa "progressGz": "<base64>"
}
```

**Aşama 1'in kesinleştirdikleri (2026-10-26):** `writes` eklendi: cevabı kaybolan bir yazma (zaman aşımı, oyunun kapanması) sonraki okumada kendi kimliğini kayıtta ya da üstüne yazılmış bir kayıtta bulur; taban o yazmanın içeriği olur, hiçbir şey iki kez sayılmaz (§3.3). Ölçüm: bütün parçaları yerleştirmiş 1000. bölüm oyuncusu (64 ödeme kimliği, 200 defter numarası) 21.411 bayt JSON; buluttaki kayıt sıkıştırılınca 12.534 bayt. Ödeme belirteçleri rastgele olduğu için az sıkışıyor; satın alması olmayan oyuncunun kaydı 16 KB'ın altında kalır, sıkıştırılmaz.

- **Boyut:** bugün ≈3–10 KB (ESTIMATE; Aşama 1'in testi gerçek bir 1000. bölüm oyuncusuyla ölçer ve 16 KB'ı aşan belgeyi sıkıştırır). Sınır 5 MiB [U14].
- **`envelope`:** zarfın biçimi. **`progressLayout`:** `ProgressDocument`'in düzeni (bugün 6; bu iş 7 yapar: `ledgerDelivered` eklenir, eklemeli değişiklik).
- **`revision`:** her başarılı yazmada bir artar; yalnız günlük ve destek için. Çakışmayı **writeLock** yakalar [U15].
- **`writer`:** eşitleme için ayrı, rastgele bir kurulum kimliği. Gizlilik kartı "kayıtların rastgele kimliği hiçbir yere gitmez" diyor; o kimlik gitmez.

**`purchases`** (Player Data, **Protected**; yalnız Cloud Code yazar):

```json
{ "next": 4,
  "items": [ { "seq": 1, "store": "GooglePlay", "product": "coins.pouch", "order": "GPA.3312-…",
               "at": "2026-11-01T10:02:11Z", "state": "valid", "test": false } , … ] }
```

`state`: `valid`, `voided` (Google iptal etti), `refunded` (Apple iade etti), `invalid`. Kapalı küme; istemcide `enum`. Bir kayıt ≈200 bayt; 5 MiB ≈25.000 satın alma.

**`rcpt-<SHA-256(store + ":" + belirteç)[0..40]>`** (Custom item, Private; oyuncu okuyamaz): `{ "player": "<oyuncu kimliği>", "seq": 3, "product": "coins.bag", "state": "valid", "at": "…" }`. Custom item sayısında sınır yok [U16].

**Sürüm kuralları (ESTIMATE):**
- Uygulama, `progressLayout`'u kendi düzeninden **büyük** bir `save`'in üstüne **yazmaz** (bugünkü `ProgressOfALaterGameException` kuralının bulut karşılığı). Okuyabildiğini gösterir, "Oyunu güncelle" der, yerel oyun sürer.
- Düzen değişiklikleri eklemelidir: yeni alan önce isteğe bağlı gelir, eski uygulama onu yok sayarak okuyabilir.
- Okunamayan (bozuk) `save` üstüne yazılmaz; günlüğe yazılır, eşitleme o hesap için durur, destek satırı görünür.

### 3.3 Telefondaki yeni dosya: `cloud.json`

- Son eşitlenen buluttaki ilerlemenin **taban kopyası** (birleştirme bunun üstüne kurulur, §3.5), onun writeLock'u ve `revision`'ı.
- Hangi oyuncu kimliğiyle eşitlendiği (hesap değişirse taban geçersizdir).
- **Doğrulama kuyruğu:** sunucuya ulaşılamadığı için telefonda kabul edilmiş ödemeler (§4.8).
- Son okuma ve son yazma zamanı, geri çekilme durumu, "bulut kaydı açık mı".
- `progress.json` gibi iki adımda yazılır (yeni dosya + yer değiştirme). Bozuksa taban yok sayılır ve ilk eşitleme "yeniden kurulum" kuralıyla yapılır (§3.6).
- **Aşama 1 (2026-10-26):** "Bulut kaydı" anahtarı bu dosyada. Bozuk dosya kenara alınır (`cloud.json.damaged`) ve durum **anahtar kapalı** başlar: oyuncunun "kapalı"sı kazara "açık"a dönmesin (karar 1, §6.2.3). Oyuncu anahtarı açınca ilk eşitleme "yeniden kurulum" kuralıyla yapılır. "Verilerimi sil"den sonra dosya silinmez, anahtar kapalı ve hesapsız yazılır. Her yazma, gönderilmeden önce kimliği ve içeriğiyle bu dosyaya yazılır (cevapsız yazma). Doğrulama kuyruğu Aşama 4'te eklenir (henüz hiçbir oyuncuda bu dosya yok, alan eklemek serbest).

### 3.4 İnternetsiz önce: ne zaman okunur, ne zaman yüklenir

**Açılış:** oyun yerel `progress.json`'la hemen açılır; UGS arka planda başlar. Oyuncu hiçbir zaman eşitlemeyi beklemez.

| Olay | Ne yapılır | Bütçe (varsayılan; Remote Config'le değişir) |
|---|---|---|
| Soğuk açılış | Oturum aç (§5), `save` ve `purchases`'ı oku, birleştir (§3.5) | Son okumadan 6 saat geçmediyse ve hesap değişmediyse okuma atlanır |
| Uygulama arka plana geçer / kapanır | Yerel belge tabandan farklıysa yükle | Son yüklemeden en az 15 dakika |
| Satın alma teslim edildi | Hemen yükle | Bütçe dışı |
| Tablo ya da oda tamamlandı | Hemen yükle | Bütçe dışı |
| Hesap bağlandı ya da değişti | Oku, birleştir, yükle | Bütçe dışı |
| Uzun süren oturum | 30 dakikada bir, değişiklik varsa | Bütçeden |

**Yükleme:** `save` taban writeLock'uyla yazılır. Cloud Save "değişmiş" derse: oku → birleştir → bir kez daha yaz. İkinci kez de reddedilirse sıradaki tetikleyiciye kalır (sonsuz deneme yok).

**İlk `save`:** hesap açılır açılmaz değil, **ilk bölüm kazanılınca** yazılır. Kurup hemen bırakanlar depolama harcamaz. ESTIMATE

**Aşama 1'in bütçesi (2026-10-26):** varsayılanlar §2.4'ün sıkı kuralı: soğuk açılışta okuma **12 saatte bir**, bütçeli yükleme (arka plan, uzun oturum, soğuk açılıştan sonraki yükleme) **en az 60 dakika** arayla; satın alma, tablo/oda ve hesap değişimi bütçe dışı. Remote Config ikisini sınırlar içinde değiştirir (yükleme 5 dk–24 sa, okuma 1 sa–7 gün; sınırın dışındaki değer en yakın sınıra çekilir), böylece yanlış yazılmış bir değer ne aylık bütçeyi bir günde harcar ne eşitlemeyi durdurur. Tablodaki 6 saat / 15 dakika Remote Config'le açılabilir. Geri çekilme: ağ yokken adım yok (buluta bir şey gitmedi, sonraki tetikleyici yine dener); öteki her hatada 1, 5, 30 dk, 2, 6, 24 sa. Yeni giriş (hesap bağlandı ya da değişti) geri çekilmeyi beklemez. Okunamayan ya da daha yeni bir oyunun kaydında yazma durur; okuma yalnız soğuk açılışta bütçeyle sürer, kayıt okunur olunca durma kalkar.

### 3.5 Birleştirme kuralları (üç yollu)

Üç belge var: **taban** (bu telefonun son eşitlediği bulut belgesi), **yerel** (telefondaki şimdiki), **bulut** (şimdi okunan). Bulut tabanla aynıysa yerel yüklenir; yerel tabanla aynıysa bulut alınır. İkisi de değiştiyse alan alan:

| Alan | Kural | Neden |
|---|---|---|
| Kazanılan bölüm (`levelsWon`) | En büyüğü | İlerleme geri gitmez |
| Bölümdeki deneme sayısı (`attemptsOnLevel`) | Aynı bölümdeyse en büyüğü; değilse ileride olanınki | Tohum havuzu (k mod 16) aynı tohumu tekrar vermesin (§17) |
| Yerleştirilen parçalar | Birleşim; kazanılan bölümlerin verdiği parçalarla sınırlı | |
| Öğretici adımları, açılış hediyeleri (`unlockGifts`) | Birleşim | Bir kez gösterilen bir kez kalır |
| Tablo ödülleri sayısı (`paintingRewards`) | En büyüğü | Tekrar ödül yok |
| **Altın, her booster** | **bulut + (yerel − taban)**, ama yerelin bulutta zaten bulunan satın alma teslimleri farktan düşülür; 0'ın altına inmez | İki telefonda kazanılanlar toplanır, hiçbiri iki kez sayılmaz |
| Canlar (`livesShort`, `nextLifeAt`) | Yerel | Kısa ömürlü; iki telefondan fazla can çıkmasın |
| Günlük sayaçlar (`day`, reklam sayaçları, `dailyRewards`, `lastDailyDay`) | Aynı gündeyse en büyüğü; değilse ileri günün değerleri | Günlük hediye iki telefonda iki kez alınmasın |
| Ürün sayıları (`bought`) | bulut + (yerel − taban), defterin sayısından az değil | Çırak Çantası'nın "bir kez"i |
| Son 64 ödeme kimliği | Birleşim, en eskisi düşer | Mağazanın yeniden bildirdiği ödeme iki kez verilmez |
| Teslim edilen defter numaraları (`ledgerDelivered`, yeni) | Birleşim; aralık listesi olarak ("1-12,14") hiç kırpılmaz | §3.7 |

**Örnek:** taban 100 altın. A telefonu +50 kazanır (yerel 150). B telefonu +30 kazanıp yükler (bulut 130). A eşitlerken: 130 + (150 − 100) = **180**. Doğru toplam 100 + 50 + 30.

**Satın alma düşümü örneği:** A bir kese alır (+1.000, defter 5), yükleyemeden B açılır, defterde 5'i görür, `save`'de yoksa teslim eder (§3.7) ve yükler. A sonra eşitlerken yerelin farkında +1.000 var ama bulutun `ledgerDelivered` kümesinde 5 zaten var: A'nın farkından 1.000 düşülür. Sonuç tek kese.

**Testler (Aşama 1, Unity'siz):** her alan için tablo testleri; özellik testleri: birleştirme sonrası altın ≤ taban + iki tarafın kazancı (hiç altın yaratılmaz); aynı belge iki kez birleştirilince değişmez; teslim numaraları kaybolmaz.

**Aşama 1'in kesinleştirdikleri (2026-10-26):**
- **Bir kez verilen kazanç bir kez sayılır.** Kuralların bir kez verdiği ve iki tarafın da tabandan sonra aldığı her kazancın içeriği yerelin farkından düşülür: aynı defter numaralı ödeme (tablodaki kural), booster'ın açılış hediyesi (hoş geldin hediyesinin oynanışa dağılmış parçası; silip kurmada iki kez gelmesin), aynı günün günlük hediyesi, iki tarafta da tamamlanan tablonun ödülü.
- **Tablo ve günlük ödül sayıları** "en büyüğü" yerine verilen ödül sayısı: taban + iki tarafın tabandan sonraki ödülleri − iki tarafta da verilenler, en az iki taraftan büyüğü. İki telefonda iki ayrı tablo tamamlanınca iki ödül sayılır (en büyüğü bir sayar, sıradaki ödül bir öncekini tekrarlardı). Son günlük hediyenin günü ikisinden geç olanı.
- **Ürün sayısı** iki tarafta da teslim edilen ödeme bir kez sayılarak, yerelin, bulutun ve birleşen defter numaralarındaki o ürünün sayısından az değil. Ödeme kimlikleri: önce bulutunkiler, sonra yerelin yenileri, son 64'ü.
- **Defter adlandıramıyorsa birleştirme yok.** İki tarafta da teslim edilen bir numara defterde yoksa ya da ürünü katalogda yoksa birleştirme yapılmaz, hiçbir şey yazılmaz (`Unaccounted`); sonraki tetikleyici yeniden dener. Tahmin altın yaratır ya da kaybettirirdi.
- **Aynı belge iki tarafta** (yazma gitti, cevabı gelmedi): yerel aynen kalır, iki kez sayılmaz.
- **Bilinen sınırlar:** (1) `cloud.json` kaybolur, `progress.json` kalır ve oyuncu anahtarı yeniden açarsa taban "yeni oyuncu"dur; buluttaki kayıt bu telefonun ilerlemesini zaten içeriyorsa bölüm ve reklam altınları iki kez sayılabilir (numaralı ödemeler, açılış hediyeleri, günlük hediye ve tablo ödülleri yine bir kez). (2) Aynı ödemeyi iki telefon doğrulanmadan teslim ederse (Google onaylanmamış ödemeyi ikinci telefona bildirir ve iki telefon da UGS'ye ulaşamaz) bir kez fazla sayılır. (3) Cloud Save'de "yalnız yoksa yaz" yok: iki telefonun ilk `save`'i aynı anda yazılırsa biri ötekini ezer; okuma hemen öncesinde yapıldığı için pencere birkaç saniye.

### 3.6 Çakışma durumları

| Durum | Ne olur |
|---|---|
| **İki telefon, aynı hesap** | §3.5'in üç yollu birleştirmesi; writeLock çakışmayı yakalar |
| **Silip kurma** (aynı telefon) | Yeni kurulumun tabanı yok; taban "yeni oyuncu" sayılır. Hesap PGS/GC'ye bağlıysa aynı hesaba girilir, bulut alınır; ilk açılışta oynanan bölümler (hoş geldin hediyesi farktan düşerek) üstüne eklenir. Bağlı değilse: Android'in yedeği (Auto Backup) Unity'nin oturum belirtecini (PlayerPrefs) geri getirdiyse aynı anonim hesap; getirmediyse yeni anonim hesap ve eski kayıt bulunamaz (Ayarlar'da açıkça yazılır). ESTIMATE (belirtecin yedeğe girdiği Aşama 7'de denenir) |
| **Yeni telefon, PGS/GC bağlı** | İlk açılışta PGS v2 kendiliğinden girer (ya da Game Center) → aynı Unity hesabı → bulut alınır, "Kaydın geri geldi: 312. bölüm, 4.200 altın" kartı |
| **Yeni telefon, eski telefonda bağlı değildi** | Kayıt bulunamaz; eski telefonun yedeğinden geri yüklendiyse yerel dosya zaten gelir (bugünkü gibi) |
| **Telefonun yedeği iki telefona açıldı** | İki telefon aynı anonim hesapta; §3.5 birleştirir |
| **Anonim oynanmış telefonda sonradan PGS/GC'ye bağlanma, ama o kimlik başka bir hesaba bağlı** (`AccountAlreadyLinked`) | Kart: "Bu Google Play Games hesabında bir kayıt var (X. bölüm). Ona geçilsin mi? Bu telefondaki ilerleme onunla birleşir." Evet → o hesaba girilir, yerel ilerleme taban "yeni oyuncu" sayılarak birleştirilir. Eski anonim hesap bırakılır (satın alması yoksa verisi silinir) |
| **Eski uygulama sürümü, yeni düzende `save`** | Eski sürüm yazmaz; yerelde oynar, "Oyunu güncelle" satırı; satın almalar sunucu defterine yine girer (§3.7) |
| **Yerel dosya bu açılışta tutulamıyor** (`ForThisRunOnly`) | Eşitleme yapılmaz (bugünkü "satmaz" kuralı gibi) |
| **Telefondaki Google hesabı değişti** (PGS başka biri) | Unity oturumu sürer, kendiliğinden hesap değiştirilmez; Ayarlar'da "Bağlı hesap: …" görünür. ESTIMATE (basit kural) |

### 3.7 Satın almalar cihazlar arasında nasıl bir kez teslim edilir

1. Her doğrulanmış satın alma sunucuda oyuncunun defterine bir **sıra numarasıyla** (`seq`) girer.
2. Telefon ödemeyi teslim ederken (bugünkü `WalletLedger.Deliver`) altını, ödeme kimliğini **ve defter numarasını** tek değişiklikte ilerlemeye yazar; mağazaya onay ancak kayıttan sonra gider (değişmez).
3. Birleştirmede `ledgerDelivered` birleşim alır; bir numara bir kez sayılır (§3.5'teki düşüm).
4. **Defterden geri yükleme:** eşitlemeden sonra defterde olup `ledgerDelivered`'da olmayan `valid` kayıtlar varsa (ödeme yapan telefon yükleyemeden kaybolduysa) teslim edilir; kart "Geri yükleme: 1.000 altın cüzdanında" der.
5. Mağazanın yeniden bildirdiği ödeme için bugünkü 64 kimlik penceresi sürer. Defter numarası hiç kırpılmadığı için çok satın alan oyuncuda da eski bir paket ikinci kez verilmez.
6. Doğrulanmadan teslim edilen ödeme (UGS'ye ulaşılamadı) numarasız teslim edilir; doğrulanınca numarası eklenir, altın eklenmez.

### 3.8 UGS'ye ulaşılamazsa ya da kapatılırsa

| Durum | Oyunun davranışı |
|---|---|
| İnternet yok | Oyun bugünkü gibi; eşitleme bekler; Ayarlar "Son eşitleme: 2 gün önce" |
| UGS yanıt vermiyor, 5xx, zaman aşımı | Üstel geri çekilme (1, 5, 30 dk, 2, 6, 24 sa); satın alma telefonda kabul edilir, doğrulama kuyruğa girer |
| **Ücretsiz sınır aşıldı, UGS kapattı** (hata kodu belgede yok) | Aynı geri çekilme. Oturum açılamıyorsa hesap "şu an ulaşılamıyor"; yerel kayıt hiçbir zaman silinmez ya da beklemez |
| 30 gün sonra hizmetler kapandı | Oyun tamamen yerel çalışır (bugünkü v1); kayıp yalnız bulut kopyası |
| Remote Config "bulut kaydı kapalı" derse | Yükleme ve okuma durur; satın alma doğrulaması sürebilir (ayrı anahtar) |
| Oyuncu "Bulut kaydı"nı kapattı | UGS hiç başlatılmaz: hesap yok, eşitleme yok, doğrulama yok; satın alma bugünkü gibi telefonda |

**Kural:** hiçbir UGS hatası oyuncuya oyunu durduran bir kart göstermez; yalnız Ayarlar'daki durum satırı değişir. Ham hata metni oyuncuya gitmez, günlüğe gider. ESTIMATE

---

## 4. Satın alma doğrulaması

### 4.1 Akış

```
Oyuncu "Satın al" → mağaza ödeme sayfası → IAP: PendingOrder
  → Checkout: "Doğrulanıyor" (en çok 10 sn)
      → Cloud Code ValidatePurchase(store, ürün, belirteç ya da JWS)
           → registry'de belirteç var mı? (rcpt-…)
           → Google: Play Developer API   |  Apple: JWS imza zinciri + Get Transaction Info
           → registry + oyuncunun defteri (seq) yazılır
      ← karar: Valid(seq) | AlreadyValidated(seq) | Invalid | Pending | Unavailable
  → WalletLedger.Deliver (altın + ödeme kimliği + seq tek kayıtta) → kaydet → ConfirmPurchase
  → hemen yükle (§3.4)
```

- Ödeme mağazada ağ gerektirdiği için doğrulama anında telefon genellikle çevrimiçidir. UGS'ye ulaşılamıyorsa (kapalı, zaman aşımı) teslim bugünkü gibi telefonda yapılır, doğrulama kuyruğa girer.
- Satın almadan önce istemci mağazaya oyuncunun hesabını iliştirir: Google'da `SetObfuscatedAccountId(SHA-256(oyuncu kimliği))`, Apple'da `SetAppAccountToken(UUIDv5(oyuncu kimliği))`. İkisi de IAP 5.4.4'te var (`IGooglePlayStoreExtendedService`, `IAppleStoreExtendedService`). Sunucu, fişteki değer bu oyuncununkiyle uyuşmazsa fişi "başka oyuncunun" sayar. FACT (paket kaynağı [L5]) / ESTIMATE (kullanım)
- Google'ın belirteci `GoogleOrderInfo.PurchaseToken`, Apple'ın JWS'i `AppleOrderInfo.jwsRepresentation`. FACT [L5][U7]

### 4.2 Cloud Code modülü `MuseumPuzzle.Server`

| Uç | Kim çağırır | Ne yapar |
|---|---|---|
| `ValidatePurchase(store, productIdSuffix, transactionId, payload)` | Oyuncu | §4.3/§4.4; registry'yi writeLock'la yazar (aynı anda iki çağrı tek kayıt üretir); defterin `next`'ini artırır; kararı döner |
| `CheckRefunds()` | Oyuncu, açılışta, günde en çok bir kez, yalnız Apple satın alması olanlar | Apple Get Refund History; iade edilenleri `refunded` işaretler |
| `DeleteMyData()` | Oyuncu, "Verilerimi sil" | `save` ve `purchases` anahtarlarını siler; registry kayıtlarından oyuncu kimliğini siler (§5.6) |
| `PollGoogleVoided()` | Scheduler, günde bir (ör. `0 3 * * *` UTC) | Voided Purchases API (son 24+ saat, sayfalı); belirteç özetini registry'de bulur, defteri `voided` işaretler |

- **Ürün → içerik eşlemesi:** sunucu `economy/default.json`'un `store` bölümünü (`Puzzle.Meta.StoreCatalogue`, `netstandard2.1`) modüle gömerek okur; bilinmeyen ürün `Invalid`. Ürün kimlikleri: `com.saturnwave.museumpuzzle.coins.pouch`, `.coins.bag`, `.coins.chest`, `.coins.treasure`, `.starter.apprentice_kit`. FACT (kimlikler [L8]) / ESTIMATE (gömme)
- **Ortam:** `production` ve `development` aynı modülü kullanır. Apple'ın sandbox işlemleri iki ortamda da kabul edilir (App Review yayın derlemesinde sandbox'la satın alır), sandbox adresinden doğrulanır ve `test: true` işaretlenir; Google'ın test satın almaları da öyle.
- **Sınırlar:** her çağrı 15 saniyenin altında kalmalı; dış istekler 5 saniye zaman aşımıyla [U18]. Modül bellekte yalnız erişim belirteçlerini tutar (Google 55 dk, Apple JWT 30 dk); önbellek iki girişle sınırlı.
- **Hata:** Google ya da Apple'a ulaşılamazsa karar `Unavailable` (istemci telefonda kabul eder, kuyruğa koyar). Ham dış hata istemciye gitmez; Cloud Code günlüğüne gider.

### 4.3 Google Play doğrulaması

1. Erişim: hizmet hesabının JSON anahtarıyla RS256 imzalı bir JWT → `https://oauth2.googleapis.com/token` → erişim belirteci (kapsam `androidpublisher`). FACT [G2][G3]
2. `GET https://androidpublisher.googleapis.com/androidpublisher/v3/applications/com.saturnwave.museumpuzzle/purchases/products/{productId}/tokens/{token}` (ya da yenisi `purchases/productsv2/tokens/{token}`). FACT [G3][G4]
3. Denetimler: ürün kimliği istenenle aynı ve katalogda; `purchaseState` satın alınmış (beklemede ise `Pending`); `obfuscatedExternalAccountId` varsa bu oyuncunun özeti; belirteç registry'de başka oyuncuda değil. Test satın alması işaretlenir. ESTIMATE (alan anlamları Aşama 3'te şemadan kesinleşir)
4. Onay (acknowledge/consume) yine istemcide IAP'nin `ConfirmPurchase`'ı ile, teslimden sonra; sunucu onaylamaz. Google onaylanmayan satın almayı 3 günde iade eder; `Invalid` kararında istemci onaylamaz. ESTIMATE
5. Kota: dakikada 3.000 istek (tek satın almalar kovası), ücretsiz. FACT [G6]

### 4.4 App Store doğrulaması

1. İstemcinin gönderdiği JWS'in başlığındaki `x5c` sertifika zinciri **Apple Root CA – G3**'e kadar doğrulanır (kök sertifika modüle gömülür), ES256 imzası denetlenir; `bundleId` = `com.saturnwave.museumpuzzle`, `productId` katalogda, `environment` ortamla uyumlu, `appAccountToken` varsa bu oyuncununki, `revocationDate` yok. FACT (alanlar [A3]) / ESTIMATE (yöntem; Apple'ın App Store Server Library'si Java, Python, Node, Swift için var, .NET yok, kendimiz yazarız)
2. Ardından `GET https://api.storekit.apple.com/inApps/v1/transactions/{transactionId}` (sandbox: `api.storekit-sandbox.apple.com`): işlemin Apple'da gerçekten var olduğunu ve şimdiki durumunu doğrular. Uç tüketilen ürünler ve bitirilmiş işlemler dahil her türü kapsıyor; saniyede 50 istek (sandbox'ta %10). FACT [A1][A7]
3. Yetki: istek başına ES256 imzalı JWT; başlıkta `kid` (anahtar kimliği), yükte `iss` (issuer ID), `iat`, `exp` (en çok 60 dk), `aud: appstoreconnect-v1`, `bid` (bundle ID). FACT [A5]
4. IAP 5 StoreKit 2 kullanıyor (iOS 15.0 ve üstü); 5.1.0'dan beri eski işletim sistemlerinde StoreKit 1'e düşebiliyor, o zaman JWS boş gelir. Bizim alt sınır 15.0, GameKit için 15.6 olacak. JWS boş gelirse `Unavailable` gibi davranılır. FACT [U8][U9] / ESTIMATE

### 4.5 Sunucuda tutulan kayıtlar

- **Registry** (`rcpt-…`, Private): belirteç ya da işlem başına bir kayıt; kimin, hangi ürün, hangi defter numarası, durum. Belirtecin kendisi saklanmaz, özeti saklanır (Google'da iptal listesini eşlemek için özet yeter). ESTIMATE
- **Defter** (`purchases`, Protected): oyuncunun okuyabildiği liste (§3.2). İstemci onu Cloud Save SDK'sıyla doğrudan okur; ayrıca Cloud Code çağrısı gerekmez (çağrı bütçesi).
- **Çırak Çantası** (tüketilmeyen ürün) mağaza hesabının malıdır: aynı mağaza hesabı başka bir Unity hesabında geri yüklenirse o hesapta da bir kez verilir (bugün yeniden kurulumda olduğu gibi). Tüketilen paketin belirteci başka bir oyuncuda görülürse `Invalid` (başka oyuncunun).

### 4.6 İade ve iptaller (hafif tasarımın elverdiği kadar)

- **Google:** `PollGoogleVoided` günde bir `GET …/purchases/voidedpurchases` çağırır (yalnız son 30 gün; günde 6.000, 30 saniyede 30 sorgu kotası; sayfa başına en çok 1.000). FACT [G7]. Anlık bildirim (RTDN) Google Cloud Pub/Sub ister; Pub/Sub faturalandırma hesabı istediği için kullanılmaz. ESTIMATE
- **Apple:** iade bildirimleri (App Store Server Notifications) herkese açık bir HTTPS adresi ister [A6]; UGS'de yok. Bu yüzden `CheckRefunds` oyuncu açılışında, Apple satın alması olan oyuncu için günde en çok bir kez Get Refund History'yi çağırır (saniyede 10 istek; tüketilenler dahil) [A2][A7]. Seçenek: App Store Connect'e oyun sitesindeki boş bir adres yazılıp Get Notification History (son 180 gün, saniyede 50) günlük zamanlanmış görevle okunabilir; Apple'ın "gönderilemedi" sayacına takılır mı denenmeli [A8]. ESTIMATE
- **v1'de iadenin sonucu (öneri):** defterde `voided`/`refunded` işaretlenir; **altın geri alınmaz** (§19'daki bugünkü kural sürer); iade edilen kayıt defterden geri yüklemeye (§3.7 madde 4) girmez. Sahip isterse "iade edilen paketin altını, cüzdanda olduğu kadar düşülsün" seçeneği eklenebilir (§10, karar 3). v2'de sunucu cüzdanıyla yeniden karar verilir.

### 4.7 Gizli anahtarlar: Unity Secret Manager

Proje sahibi ekler: Unity Dashboard → Projects → proje → **Development → Secrets → Add secret** (ya da ortama özel: Development → Environments → ortam → Secrets), değer girilir, **erişebilecek hizmet olarak Cloud Code** seçilir. Değer bir daha görüntülenemez, yalnız değiştirilebilir. Cloud Code sırları 5 dakikaya kadar önbelleğe alır. Ücretsiz. FACT [U19][U20][U21]

| Anahtar adı | Değer | Kimden |
|---|---|---|
| `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON` | Hizmet hesabının JSON anahtar dosyasının tamamı | Google Cloud (§9 B6) |
| `APPLE_IAP_PRIVATE_KEY` | `.p8` dosyasının metni | App Store Connect (§9 C3) |
| `APPLE_IAP_KEY_ID` | Anahtar kimliği (ör. `2X9R4HXF34`) | aynı yer |
| `APPLE_ISSUER_ID` | Issuer ID | aynı yer |

Modül bunları `IGameApiClient.SecretManager.GetSecret(context, "…")` ile okur [U20]. Paket adı, bundle ID ve ürün kimlikleri gizli değil, modülün içinde.

### 4.8 İstemci cevapla ne yapar

| Karar | İstemci |
|---|---|
| `Valid(seq)` | Teslim et (altın + kimlik + seq), kaydet, mağazaya onay, hemen yükle; "Satın alındı" kartı |
| `AlreadyValidated(seq)` | Yerelde bu ödeme (`HasSeen`) ya da seq varsa yalnız onayla; yoksa teslim et (doğrulama ile kayıt arasında oyun kapanmıştı) |
| `Pending` | Teslim yok, onay yok; bugünkü "Onay bekleniyor" kartı |
| `Invalid` | Teslim yok, **onay yok** (Google 3 günde iade eder); kart: "Satın alma doğrulanamadı. Ödeme alındıysa mağaza iade eder." Günlüğe neden |
| `Unavailable` / zaman aşımı / UGS kapalı | Bugünkü gibi telefonda teslim ve onay; ödeme `cloud.json`'daki doğrulama kuyruğuna; sonraki eşitlemede yeniden sorulur. Sonradan `Invalid` çıkarsa defterde işaretlenir, altın geri alınmaz (v1) |

`Checkout`'un kapalı durum kümesine (`PurchaseState`) bir "doğrulanıyor" adımı eklenir; 10 saniye içinde cevap yoksa `Unavailable` yolu. Çırak Çantası'nın kendiliğinden geri gelişi (`DeliveryReason.Restore`) de aynı doğrulamadan geçer.

---

## 5. Hesap ve geri yükleme

### 5.1 Android'de ilk açılış (PGS v2)

1. Oyun yerel kayıtla açılır. Arka planda `UnityServices.InitializeAsync` (ortam adı derlemeden) ve `PlayGamesPlatform.Activate()`.
2. **Unity oturumu önbellekte varsa** (her açılışta olağan durum): `SignInAnonymouslyAsync()` önbellekteki oyuncuya girer (hesap PGS'ye bağlı olsa da); PGS'ye ayrıca sorulmaz. ESTIMATE (Unity'nin anonim giriş davranışından; Aşama 2'de sahte ve gerçek hesapla doğrulanır). `SignInWith…` ise önbelleği hesaba katmaz, onun yerine geçer: FACT [U11]
3. **Önbellek yoksa** (ilk kurulum, silip kurma): PGS v2 kendiliğinden giriş yapar (`Authenticate` sonucu). Başarılıysa `RequestServerSideAccess(false, code)` → `SignInWithGooglePlayGamesAsync(code)`: bu kimliğe bağlı bir Unity oyuncusu varsa ona girer (geri yükleme), yoksa yenisini açar. FACT [U11][G2]
4. PGS yoksa, oyuncu reddettiyse ya da hata: `SignInAnonymouslyAsync()` → yeni anonim hesap.
5. Oturum açılınca `save` ve `purchases` okunur, §3.5/§3.6.

PGS'nin kurulumu Google tarafında web istemci kimliği (Game server kimlik bilgisi) ve uygulama imza anahtarının SHA-1'i ister (§9 B2–B5). Kimlik doğrulama yalnız Android'de. FACT [G1][G2][U11]

### 5.2 iOS'ta ilk açılış (Game Center)

1. Oyun açılır; `GKLocalPlayer.Authenticate()` (Apple.GameKit). Game Center'a girilmemişse Apple'ın kendi giriş sayfası bir kez çıkar; oyuncu kapatırsa Game Center bu uygulama için kapalı kalır.
2. Önbellekte Unity oturumu varsa §5.1 madde 2.
3. Yoksa ve Game Center açıksa: `GKLocalPlayer.Local.FetchItems()` → imza, `TeamPlayerId`, `PublicKeyUrl`, tuz, zaman → `SignInWithAppleGameCenterAsync(...)`. Zaman damgası 10 dakika geçerli; her çağrıdan önce yeniden alınır. FACT [U12]
4. Game Center yoksa: anonim hesap.
5. Unity Authentication'da kimlik sağlayıcı olarak Apple Game Center ve bundle ID tanımlanır; App ID'de Game Center yeteneği, Xcode projesinde Game Center yetkisi (entitlement) gerekir. `IosBuild` dışa aktarırken yetkiyi ekler. `IOS_BUILD.md`'deki "oyun Game Center kullanmıyor" cümlesi değişir. FACT [U12] / ESTIMATE
6. Apple kuralı 4.5.5: Game Center oyuncu kimlikleri oyunda gösterilmez, üçüncü taraflara verilmez [A10]. Unity Authentication'a doğrulama için verilmesi Unity'nin belgelediği yoldur; Ayarlar'da gösterilen "oyuncu kodu" Unity'nin kimliğidir, Game Center'ınki değil. ESTIMATE

### 5.3 Sonradan bağlama (Ayarlar)

- "Google Play Games'e bağlan" (iOS'ta "Game Center'a bağlan") → kimlik alınır → `LinkWithGooglePlayGamesAsync(code)` / `LinkWithAppleGameCenterAsync(...)`. FACT [U11][U12]
- **`AccountAlreadyLinked`:** o kimlik başka bir Unity hesabına bağlı. §3.6'daki kart; "Evet" → `SignInWith…` o hesaba geçer, yerel ilerleme onunla birleşir. Eski anonim hesabın defteri boşsa geçmeden önce `DeleteMyData` + `DeleteAccountAsync` ile silinir; satın alması varsa bırakılır (altını zaten birleşen yerel ilerlemededir). ESTIMATE
- Bağlantıyı kaldırma (`Unlink…`) v1'de yok; hesap ancak silinir. ESTIMATE (öneri)
- **Ne zaman önerilir (karar 2, sahip 2026-10-26):** anonim oyuncuya **ilk kazanılan bölümden sonra** bir kez, sakin bir kartla: "Kaydın yalnız bu telefonda. Telefon değiştirince geri getirmek için Google Play Games'e bağlan." 20. bölümde ya da ilk satın almadan sonra değil. Mağaza ve kaybetme kartında değil.

### 5.4 Oyuncu reddederse ya da PGS/GC yoksa

- Oyun anonim hesapla aynen çalışır; bulut kaydı aynı kurulumda ve (Android'de) telefonun yedeği oturum belirtecini geri getirirse işe yarar.
- Ayarlar'da açık cümle: "Hesap: bu telefon (bağlı değil). Yeni telefonda geri getirmek için bağla."
- PGS v2 her açılışta kendiliğinden girmeyi dener; reddeden oyuncuya oyun ayrıca sormaz (yalnız §5.3'teki tek kart).

### 5.5 Yeni telefonda geri yükleme

| Önceki telefonda | Yeni telefonda | Sonuç |
|---|---|---|
| PGS/GC'ye bağlıydı | Aynı Google/Apple hesabı, PGS/GC açık | Kendiliğinden geri gelir (§5.1 madde 3) |
| Bağlıydı | PGS/GC kapalı ya da reddedildi | Anonim başlar; Ayarlar'dan "Bağlan" → `AccountAlreadyLinked` → geçiş kartı |
| Bağlı değildi | — | Yalnız telefonun yedeğiyle (Android Auto Backup, iCloud yedeği) |
| Android'den iPhone'a ya da tersi | — | Geri gelmez (v1 dışı; v2'de tek kullanımlık aktarım kodu düşünülebilir) |

### 5.6 Hesap ve veri silme

**Kurallar:** Apple 5.1.1(v): hesap açtıran uygulama hesabı uygulamanın içinden de sildirmeli [A10]. Google: hesap açtıran uygulama uygulama içi silme yolu **ve** uygulama silinmiş kullanıcılar için bir **web bağlantısı** sunmalı; Data safety formunda beyan edilir; güvenlik, sahtekârlık önleme ve yasal yükümlülük için bazı veriler, politikada yazılırsa tutulabilir [G8]. Unity: `DeleteAccountAsync()` yalnız Authentication hesabını siler, öteki hizmetlerdeki veriyi biz sileriz [U13]; hesap silinince Cloud Save verisinin kendiliğinden silinmediği forumda Unity çalışanınca söylenmiş (REPORTED [U25]). Triggers'ın olay listesinde "oyuncu silindi" olayı yok [U23]. FACT

**Oyun içi yol (Ayarlar → "Verilerimi sil"):**
1. Kart: "Buluttaki hesabın ve kaydın silinir. Bu telefondaki ilerlemeyi de silmek ister misin?" Düğmeler: "Yalnız buluttakini sil", "Her şeyi sil", "Vazgeç". Silme internet ister; yoksa "İnternet bağlantısı gerekiyor".
2. `DeleteMyData` (Cloud Code): `save` ve `purchases` silinir; registry kayıtlarından oyuncu kimliği silinir, belirteç özeti, ürün, tarih ve durum **36 ay** sahtekârlık önleme için kalır (karar 5, sahip 2026-10-26; uzman okuması).
3. İstemci `AuthenticationService.Instance.DeleteAccountAsync()`; `cloud.json` silinir; "Bulut kaydı" kapanır (oyuncu isterse yeniden açar, yeni hesap açılır).
4. "Her şeyi sil": ilerleme yeni oyuncuya döner, deneme kayıtları silinir (bugünkü "Kayıtları sil" yolu). Çırak Çantası mağazanın malı olduğu için Google Play'de yeniden gelir.
5. Google Play Games ve Game Center'ın kendi verisi Google'ın ve Apple'ın; oyuncu onları kendi profilinden siler [G5].

**Web yolu (oyunu silmiş oyuncu):** oyun sitesinde "Verilerimi sil" sayfası: oyuncu kodunu (Ayarlar'da gösterilen kısa kod) ve isteği e-postayla gönderir; proje sahibi Unity Dashboard'un oyuncu yönetimi sayfasından hesabı ve Cloud Save verisini siler. Kodunu bilmeyen için: "13 ay girilmeyen ve satın alması olmayan hesaplar kendiliğinden silinir" (aylık görev, v1.x). ESTIMATE

### 5.7 Arayüze gelenler (Aşama 5)

- **Ayarlar:** "Hesap" satırı (Google Play Games ile bağlı / Game Center ile bağlı / bu telefon, "Bağlan"); "Bulut kaydı" anahtarı (açık); "Son eşitleme: …" ya da "Şu an ulaşılamıyor; ilerlemen telefonda güvende"; "Oyuncu kodu" (kopyalanabilir); "Verilerimi sil".
- **Kartlar:** "Kaydın geri geldi", "Bu hesapta başka bir kayıt var" (geçiş), "Bağlanmayı öner" (bir kez), "Satın alma doğrulanıyor", "Satın alma doğrulanamadı", "Geri yükleme" (defterden), "Oyunu güncelle" (yeni düzen), silme kartları.
- 13 dil; `tr` kaynak, makine çevirisi ve notlar (bugünkü yol). Tasarım tuvali bu kartları çizmiyor; bildirim kartının dilinde.

---

## 6. Gizlilik

### 6.1 Telefondan yeni çıkanlar

| Ne | Nereye | Ne zaman | Etiket |
|---|---|---|---|
| Unity oyuncu kimliği, oturum belirteçleri | Unity Authentication | Her açılışta | FACT [U11] |
| Google Play Games'in bu oyundaki oyuncu kimliği (sunucu kodu üzerinden) ya da Game Center'ın takım oyuncu kimliği ve imzası | Unity Authentication (Google/Apple doğrular) | İlk girişte, bağlamada | FACT [U11][U12][G2] |
| İlerleme, cüzdan, satın alma sayıları ve son ödeme kimlikleri (`save`) | Unity Cloud Save | §3.4'teki kurallarla | ESTIMATE (tasarım) |
| Satın alma belirteci / JWS, ürün, sipariş kimliği | Cloud Code'umuz → Google / Apple | Her satın almada | ESTIMATE (tasarım) |
| IAP 5.4.4'ün UGS başlayınca açılan yolları: oturum olayı (oyuncu kimliği ve cihaz bilgisi), teşhis ölçümleri, bağlantı ayarları | Unity | Açılışta, oturumda | FACT (paket kaynağı [L5]); **ne gittiği Aşama 0'da ölçülecek** |
| UGS çekirdeğinin kendi teşhis kayıtları | Unity | Belirsiz | REPORTED: paketin değişiklik günlüğü teşhis gönderimini anıyor, kapatma ayarı bulunamadı [U24] |
| PGS SDK'sının kendi analiz ve teşhis verisi | Google | Kendiliğinden | FACT [G5] |

Unity'nin verinin yerini seçtirmediği, Cloud Save ve Authentication'ın hangi bölgede durduğunun destek makalesinde yazmadığı aktarılıyor (Remote Config ve IAP verisi ABD'de, `us-central1`). REPORTED [U26]

### 6.2 Hukuki dayanak ve rıza (uzmana sorulacak; tavsiye değil)

1. **Bulut kaydı ve hesap:** oyuncunun kullandığı hizmetin (kaydın korunması, geri yükleme) ifası için gerekli sayılabilir: GDPR md. 6(1)(b), KVKK md. 5(2)(c). Rıza sorusu gerekmeyebilir. ESTIMATE
2. **Fiş doğrulama ve registry:** sahtekârlığı önleme meşru menfaati (GDPR md. 6(1)(f), KVKK md. 5(2)(f)) ve sözleşme. ESTIMATE
3. **ePrivacy 5(3) (cihazda saklama/okuma):** oturum belirtecini saklamak ve kaydı göndermek "kullanıcının açıkça istediği hizmet için kesin gerekli" mi? Bulut kaydı varsayılan açık ve görünmez olduğu için tartışmalı. Öneri: gizlilik kartında açık anlatım + Ayarlar'da "Bulut kaydı" anahtarı (kapalıyken UGS hiç başlamaz). Uzman "önce sor" derse anahtar ilk açılışta istatistik sorusunun yanına bir soru olarak gelir. ESTIMATE
4. **Apple 5.1.1(ii):** kullanıcı verisi toplamadan önce izin ister [A10]; bu kural istatistik için uygulanıyor. Hizmetin kendisi için gereken veri (kayıt, satın alma) için Apple'ın tutumu uzmana ve inceleme deneyimine kalır. ESTIMATE
5. **Unity'nin rolü:** UGS için büyük olasılıkla bizim adımıza işleyen (DPA), IAP'nin satış verisi için kendini bağımsız sorumlu sayıyor (privacy-v1 §6.1). UGS DPA'sı ve alt işleyen listesi okunmalı. ESTIMATE
6. **KVKK md. 9 (yurt dışına aktarım):** oyuncu kimliği ve kaydı Unity'nin (ABD/AB) sunucularına gider; düzenli aktarım için standart sözleşme ve Kurul'a 5 iş günü içinde bildirim konusu privacy-v1 §6.2.3'teki soruyla birlikte sorulmalı. ESTIMATE
7. **Silme ve saklama:** registry'de oyuncu kimliksiz tutulan özetlerin süresi (sahibin kararı 36 ay, 2026-10-26) ve politikada yazılması [G8]. ESTIMATE
8. **Yaş:** değişmez (privacy-v1 §6.1.4).

### 6.3 Google Play Data safety'de değişenler (öneri)

| Soru / tür | Bugün (privacy-v1 §5.2) | v1 sunucuyla | Gerekçe |
|---|---|---|---|
| Account creation | Hesap yok | **Hesap açılıyor:** "OAuth" (Google Play Games) ve kendiliğinden açılan hesap; formdaki seçenek adı konsolda kesinleşir | Görünmez hesap |
| Data deletion | No | **Yes**: uygulama içi yol + web bağlantısı (oyun sitesinin silme sayfası) | [G8] |
| Personal info > **User IDs** | No | **Yes**, Shared: No, Required (anahtar konursa "Users can choose" tartışılır), App functionality; Account management; Fraud prevention | Unity oyuncu kimliği, PGS kimliği |
| App activity > **Other actions** (oyun ilerlemesi, cüzdan) | No | **Yes**, Shared: No, Required, App functionality | Bulut kaydı. Tür seçimi ESTIMATE |
| Financial info > Purchase history | Yes (Unity) | Yes; amaçlara Account management eklenir; kaynak: Unity olayları + sunucumuz | Defter, registry |
| Device or other IDs | Yes | Aşama 0'ın ölçümüne göre (IAP oturum olayı) | |
| App info and performance > Diagnostics | Yes | Aşama 0'a göre; PGS SDK'nın teşhisi Google'ın kendi beyanında [G5] | |
| Encrypted in transit | Yes | Yes (UGS, Google, Apple `https://`) | |

### 6.4 App Store gizlilik etiketinde değişenler (öneri)

| Tür | Bugün (privacy-v1 §5.1) | v1 sunucuyla |
|---|---|---|
| Identifiers > **User ID** | No | **Yes**, linked, App Functionality (Unity oyuncu kimliği) |
| User Content > **Gameplay Content** | No | **Yes**, linked, App Functionality (kayıtlı oyun) |
| Purchases > Purchase History | Yes, linked | Yes, linked; App Functionality (doğrulama, sahtekârlık önleme dahil) |
| Identifiers > Device ID, Diagnostics | Yes (Unity) | Aşama 0'ın ölçümüne göre |
| Tracking | No | No |

Uygulamanın `PrivacyInfo.xcprivacy`'si aynı türleri yazar; Apple.GameKit'in kendi manifesti Aşama 6'da okunur. ESTIMATE

### 6.5 Yeni gizlilik metni taslağı (oyun içi, Türkçe; sahibin onayına)

privacy-v1 §3.2'deki sekiz paragraftan **1. ve 8.** değişir, **üç yeni paragraf** eklenir; 2–7 aynı kalır (3. paragrafa Aşama 0'ın sonucuna göre bir cümle eklenebilir).

> **1 (yeni hâli).** Oyun ilerlemeni, cüzdanını (satın aldığın altın dahil), ayarlarını ve oynadığın denemelerin kayıtlarını bu telefonda tutar. İlerlemenin ve cüzdanının bir kopyası bulut kaydında da durur.
>
> **Bulut kaydı (yeni).** Oyun senin için görünmez bir oyuncu hesabı açar. Adını, e-postanı ya da bir parola sormaz; hesabı rastgele bir oyuncu kimliği tanır. İlerlemen, cüzdanın ve satın almalarının listesi bu hesapta, Unity Gaming Services'in sunucularında saklanır; Unity bu hizmeti bizim adımıza verir. Böylece oyunu yeniden kurduğunda ya da telefon değiştirdiğinde kaldığın yerden sürersin. İnternet yokken oyun aynen oynanır; kayıt bağlantı gelince eşitlenir. Ayarların, istatistik iznin ve deneme kayıtların yalnız bu telefonda kalır. Bulut kaydını Ayarlar'daki "Bulut kaydı" anahtarıyla kapatabilirsin; o zaman oyun hesaba bağlanmaz ve hiçbir şey eşitlenmez.
>
> **Google Play Games ve Game Center (yeni).** Telefonunda Google Play Games (iPhone ve iPad'de Game Center) açıksa hesabın ona bağlanır. Bunun için Google ya da Apple, bu oyundaki oyuncu kimliğini Unity'ye doğrular; adın, e-postan ve arkadaş listen oyuna gelmez. Bağlamazsan hesabın yalnız bu telefonu tanır.
>
> **Satın almaların doğrulanması (yeni).** Bir satın almanın gerçek olduğunu anlamak için oyun, mağazanın verdiği satın alma kaydını (ürün, sipariş kimliği, mağazanın imzalı kaydı) Unity'deki sunucu kodumuza gönderir. Sunucu bunu Google Play'e ya da Apple'a sorar ve sonucu hesabında saklar. İade edilen satın almalar bu listede işaretlenir.
>
> **8 (yeni hâli).** Oyunu silersen ya da telefon değiştirirsen ilerlemen ve altının bulut kaydından geri gelir; Google Play Games ya da Game Center'a bağlı değilsen bu yalnız aynı telefonda ya da telefonunun yedeğiyle olur. Ayarlar'daki "Verilerimi sil" ile buluttaki hesabını ve kaydını istediğin zaman silebilirsin; telefondaki ilerlemeyi de silip silmeyeceğini sen seçersin. Sahtekârlığı önlemek için satın almaların kimliksiz bir izi 36 ay saklanır. Satın alma iadesini Google Play'den ya da Apple'dan istersin.

**Değişecek öteki yazılar:** Ayarlar'daki gizlilik satırı ("Telefondan yalnızca satın almalar çıkar" → ör. "İlerlemen bulutta da saklanır; istatistikler yalnız izinle"); geri yükleme kartındaki "Altın paketleri geri gelmez; yalnız Çırak Çantası gelir." → "Altının bulut kaydınla geri gelir."; §19'daki "Silip kurma, yeni telefon" ve "İade" maddeleri; mağaza sayfası ve destek metni. ESTIMATE

### 6.6 Site

Sitedeki politika (privacy-v1 §4) "Sunucu yok" yerine bulut kaydı, hesap, doğrulama, silme, saklama süreleri ve Unity'nin (UGS) işleyen olarak adını içermeli; **web silme sayfası** eklenmeli [G8]. Politika mağazaya çıkmadan önce yayımlanır (sitenin bugünkü sözü). ESTIMATE

---

## 7. v2'ye köprü

### 7.1 Aynı yapı nasıl sunucu otoriteli olur

| Parça | v1 | v2 |
|---|---|---|
| Cüzdan | Telefonda; `save` içinde kopya | Cloud Save **Protected** `wallet`; yalnız Cloud Code yazar. Economy yeni projelere kapalı olduğu için kendi modelimiz; `Puzzle.Meta.Wallet` ve `EconomyRules` (`netstandard2.1`) modülde aynen çalışır [U4][L7] |
| Kazanılan bölüm ve ödülleri | Telefon kendisi verir | Telefon deneme kayıtlarını (`AttemptRecord`, `Actions` ile) gönderir; Cloud Code `AttemptReplay` ile yeniden oynatır; kazanma, şölen altını (kalan hamle × 4), tablo ödülü (80) sunucuda verilir |
| "+5 hamle", booster alımı, günlük hediye | Telefon | Cloud Code uçları; gün sunucunun saatiyle |
| Satın almalar | v1'in doğrulaması | Aynı uç, altını doğrudan sunucu cüzdanına yazar |
| `save` | Oyuncu yazar | Access Control ile oyuncunun yazması kapatılır, ilerleme sunucudan yazılır |
| Çevrimdışı | Tam | Telefon "geçici" bakiyeyle oynar; bağlanınca kayıtlar uzlaştırılır, sunucu reddederse telefon düzeltilir |

**v1'in şimdiden yaptığı köprüler:** satın alma defteri ilk günden sunucuda; oyuncu kimlikleri kalıcı ve platform kimliğine bağlı; `save` zarfı ve `progressLayout` sürümlü; deneme kayıtları `Actions` listesini zaten tutuyor (`AttemptRecord`, `AttemptReplay` motorda var) [L7]; modül ilk günden `Puzzle.Meta.dll`'e dayanıyor; ilerleme alanlarının birleştirme kuralları yazılı (v2'nin uzlaştırması aynı kuralları kullanır).

### 7.2 Cloud Code'un sınırları ve motorla yeniden oynatma

| Sınır | Değer | Bizim ihtiyaç (ESTIMATE) | Sığar mı |
|---|---|---|---|
| Modül boyutu | 10 MB `.ccm`, projede toplam 128 MB, en çok 20 modül [U18] | Motor DLL'leri ≈320 KB (Core 140, Meta 69, Museum 65, Bot 46 KB); 1000 bölüm dosyası 5,3 MB ham JSON, sıkıştırınca ≈1 MB | Evet |
| Çalışma süresi | Çağrı başına 15 sn [U18] | Bir denemenin yeniden oynatılması ≪ 5 ms (botlar 862.512 oyunu 4 iş parçacığında ≈13 dakikada oynadı: oyun başına ≈3,6 ms, bot kararı dahil; §25) | Evet; 100 denemelik toplu çağrı < 1 sn |
| Bellek | 256 MB (2.x belgeleri) – 512 MB (3.0.0 belgesi) [U18] | Bir oyun durumu birkaç KB; bölümler bir kez yüklenir | Evet |
| Yanıt | 2 MiB [U18] | Karar listesi küçük | Evet |
| Oyuncu başına hız | Dakikada 600 istek [U18] | Toplu gönderimle önemsiz | Evet |
| Deneme kaydı boyutu | Cloud Save Player Data 5 MiB; Cloud Save Files oyuncu başına 1 GiB, 200 dosya [U14][U17] | Bir kayıt 1–3 KB; kazanılınca ödül için gönderilir, sonra silinebilir | Evet |

**Belirlenimcilik:** motorun rastgelesi kendi PCG32'si (`Puzzle.Core/Randomness/Rng.cs`), `System.Random` kullanmıyor; kayan nokta yalnız hedef ilerleme oranında görülüyor. IL2CPP (ARM) ile sunucudaki .NET'in aynı sonucu verdiği, web laboratuvarının kayıtlarının .NET'te yeniden oynatılmasıyla ve telefondan alınan kayıtlarla denenmeli. FACT (kod [L7]) / ESTIMATE (risk)

**Bölüm düzeltme paketleri (§20):** modül uygulamadaki bölümleri gömer; yayınlanan paketler Cloud Save'in bir custom item'ında durur ve modül onu okur. Böylece paket için modül yeniden yüklenmez. ESTIMATE

### 7.3 Ücretsiz katmanda v2 (ESTIMATE)

Ortalama oyuncu günde ≈11 bölüm kazanır (1000 bölüm 90 günde, §25), ≈15 deneme.

| | 10 bin aylık | 100 bin aylık |
|---|---|---|
| Deneme başına bir çağrı | ≈900.000 çağrı (%90) | ≈9 milyon (**%900**) |
| Oturum başına bir toplu çağrı (3/gün) | ≈180.000 (%18) | ≈1,8 milyon (**%180**) |
| **Günde bir uzlaştırma çağrısı** | ≈60.000 (%6) | ≈600.000 (%60) |
| Yeniden oynatma işlem süresi (5 ms/deneme) | ≈1,3 saat | ≈12,5 saat (%62) |

Sonuç: v2 ücretsiz katmanda kalacaksa **günde bir (ya da oturum başına en çok bir) toplu uzlaştırma** ve gerekirse örnekleme (ör. her denemeyi değil, ödül veren kazanmaları ve rastgele %10'unu oynatmak). Ödüllü reklamın sunucuda doğrulanması (S2S geri çağrısı) herkese açık bir adres ister; UGS'de yok, v2'nin açık sorusu. Kesin süre v2'nin ilk adımında ölçülür.

---

## 8. Yapım sırası ve tahmini iş

S küçük (bir oturum), M orta (birkaç oturum), L büyük. Bağlı proje olmadan yapılabilenler **"bağlantısız"** işaretli.

| # | Aşama | İş | Efor | Bağlantısız mı | Gerekenler |
|---|---|---|---|---|---|
| 0 | **Ağ ve boyut denemesi** (iap-spike gibi atılacak bir kopyada) | Paketleri ekle; APK boyutu, birleşik manifest (INTERNET, `AD_ID` yok mu, `datatransport` silme kuralıyla çakışma, PGS eklentisinin EDM4U'su ile IAP'nin Gradle çözücüsü), IAP 5.4.4 ve UGS çekirdeğinin kaynakta ağa çıkan yolları | S | Kod okuma ve derleme: evet | — |
| 0b | | Bağlı projede gerçek trafik: Android logcat ve vekil, iPad'de Mac'teki ağ dinleyiciyle; gizlilik beyanının girdisi | S | Hayır | A1–A3, cihaz |
| 1 | **Eşitleme çekirdeği** (Unity'siz, testli) | `CloudEnvelope`, `ProgressLayout 7` (`ledgerDelivered`), `ProgressMerge` (§3.5 tablosu ve özellik testleri), `SyncPolicy` (bütçe, geri çekilme), `ICloudSave` + `FakeCloudSave` (writeLock, 409, kapalı hizmet, ağ yok) | M | Evet | — **Yapıldı 2026-10-26:** ayrıca `CloudSync` (okuma, birleştirme, yazma, cevapsız yazma), `CloudStateFile` (`cloud.json`), `PurchaseLedger` (defterin okunuşu); paketsiz, oyuna henüz bağlı değil |
| 2 | **Kimlik katmanı** | `IPlayerIdentity` (kapalı küme: yok, anonim, PGS, GC), akışlar (§5), `FakeIdentity`; Unity uyarlayıcıları: `UgsIdentity` (Authentication 3.8.1), `PlayGamesSignIn` (eklenti v2.3.0), `GameCenterSignIn` (Apple.GameKit) | M | Arabirim, sahte ve derleme: evet | Gerçek giriş: A, B4, C2, cihaz |
| 3 | **Cloud Code modülü** | `MuseumPuzzle.Server`: `ValidatePurchase`, `CheckRefunds`, `DeleteMyData`, `PollGoogleVoided` + Scheduler tanımı; `GooglePlayVerifier` (RS256 JWT, products/productsv2, voided), `AppStoreVerifier` (x5c → Apple Root CA G3, ES256, JWT, Get Transaction Info, Refund History), registry ve defter; birim testleri kayıtlı örnek cevaplarla (Apple'ın kütüphanelerinin örnek imzalı verileri dahil); yerel Cloud Code sunucusuyla deneme | **L** | Kod ve testler: evet | Dağıtım: A1–A6; gerçek doğrulama: B6, C3 |
| 4 | **İstemcide satın alma doğrulaması** | `Checkout`'a "doğrulanıyor" adımı, `UnityStore`'da hesap iliştirme (`SetObfuscatedAccountId`, `SetAppAccountToken`), `WalletLedger.Deliver`'a seq, doğrulama kuyruğu, defterden geri yükleme; `ScriptedStore` + `FakeCloudCode` ile `GameWalk` yürüyüşleri | M | Evet | — |
| 5 | **Arayüz ve yazılar** | Ayarlar satırları, kartlar (§5.7), gizlilik kartı (§6.5), 13 dil; ekran resimleri 1080×2424, 2076×2152, 2732×2048 | M | Evet (sahte hizmetlerle) | Metin onayı (sahip) |
| 6 | **Platform kurulumu** | Android: PGS eklentisi, `games-ids.xml` ve web istemci kimliği (sahibin gönderdiği "resources"), manifest denetimi; iOS: Mac'te `Apple.Core` + `Apple.GameKit` derlemesi, iOS alt sınırı 15.6, `IosBuild`'e Game Center yetkisi, gizlilik manifesti; Remote Config anahtarları | S/M | Apple eklentisi derlemesi ve iOS ayarı: evet; PGS kurulumu: B4'ten sonra | Mac (SSH), B4 |
| 7 | **Bağlı projeyle uçtan uca ve cihaz testleri** | `development` ortamı; modül ve sırlar; lisanslı test hesabıyla Google test satın almaları, Apple sandbox; iki telefonda birleştirme; silip kurma, yeni telefon, `AccountAlreadyLinked`, UGS'yi kapatma (sahte DNS ile) ve silme; `PHONE_TESTS.md`'ye yeni T aralığı | M | Hayır | A, B, C'nin hepsi; Pixel, iPad |
| 8 | **Beyanlar ve belgeler** | Data safety ve App Store etiketi önerisi, site politikası ve silme sayfası taslağı, `GAME_DESIGN.md` §19/§20/§23, privacy-v1, `IOS_BUILD.md`, `DEVLOG.md`, PDF'ler | S | Evet | Sahip yayımlar |

**Toplam:** kabaca L (Aşama 3 en büyüğü). Sıra: 0 → 1 → (2, 3, 4 paralel olabilir) → 5 → 6 → 7 → 8. Sahibin adımları (§9) Aşama 1'den itibaren paralel yürür; Aşama 7 hepsini bekler. v1'in mağazaya çıkışı bu işi bekler. ESTIMATE

**Editor denetimleri (Aşama 4–6):** `CloudSyncCheck` (sahte bulutla: iki cihaz, silip kurma, eski sürüm, kapalı hizmet), `PurchaseCheck`'e doğrulama yürüyüşleri (geçerli, sahte, beklemede, ulaşılamaz, sonradan geçersiz), `IdentityCheck` (anonim, bağlama, `AccountAlreadyLinked`). `-quit`'siz çalışanlar için bugünkü kural geçerli (sonuç satırı okunur).

---

## 9. Proje sahibinin adım adım yapacakları

Hiçbir adım kart istemez. **Bir sayfa ödeme bilgisi isterse durun ve bize yazın.** Ekran adları zamanla değişebilir; değiştiyse en yakın adı seçin. Claude hiçbir hesaba girmez, hiçbir anahtarı görmez.

### A. Unity Cloud (UGS)

| # | Adım | Yol | Bekler |
|---|---|---|---|
| A1 | Bulut projesini açın | cloud.unity.com → Unity hesabınızla giriş → **Projects → Create project** (ad: "Museum Puzzle") | — |
| A2 | Editor'da projeyi bağlayın | Unity Hub'dan projeyi açın → **Edit → Project Settings → Services** → kuruluşu seçin → "Use an existing Unity Cloud project" → A1'deki proje → **Link**. `ProjectSettings.asset` değişir; dosyayı biz commit ederiz | A1 |
| A3 | `development` ortamını ekleyin | Dashboard → proje → **Development → Environments → Add environment** → `development` | A1 |
| A4 | Kimlik sağlayıcıları | Dashboard → proje → **Player Authentication → Identity providers → Add**: (a) **Google Play Games**: B4'teki *Web application* istemcisinin Client ID ve **Client Secret**'ı; (b) **Apple Game Center**: bundle ID `com.saturnwave.museumpuzzle`. Anonim giriş varsayılan açık. (Aynı ayar Editor'da Project Settings → Services → Authentication'dan da yapılabilir [U11][U12]) | A1; (a) için B4 |
| A5 | Sırları ekleyin | Dashboard → proje → **Development → Secrets → Add secret**, her biri için "erişebilecek hizmet: **Cloud Code**": `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON` (B6'daki JSON dosyasının tamamı), `APPLE_IAP_PRIVATE_KEY` (C3'teki `.p8`'in metni), `APPLE_IAP_KEY_ID`, `APPLE_ISSUER_ID`. Sonra indirdiğiniz JSON ve `.p8` dosyalarını güvenli bir yere taşıyın; masaüstünde bırakmayın | A1, B6, C3 |
| A6 | Modülü yükleyin | Aşama 3 bitince Editor'da **Services → Deployment** penceresi → modül ve zamanlanmış görev → önce `development`'a **Deploy**, testten sonra `production`'a. (İsterseniz bizim yükleyebilmemiz için ayrı bir hizmet hesabı açılır; bu ayrı bir karar) | A2, Aşama 3 |
| A7 | Kullanımı izleyin | Dashboard → **Service Usage**; Unity'nin %75/%100 e-postalarını bize iletin | A1 |
| A8 | (İsteğe bağlı) Remote Config | Dashboard → **Remote Config** → anahtarları biz dosya olarak hazırlarız, Deployment penceresinden yüklenir | A2 |

### B. Google Play

| # | Adım | Yol | Bekler |
|---|---|---|---|
| B0 | **Play Console'da uygulama kaydı** (henüz yok) | play.google.com/console → **Home → Create app** → ad, varsayılan dil, **Game**, **Free**, beyanlar → Create | Geliştirici hesabı |
| B1 | Yükleme anahtarı ve Play App Signing | Yükleme anahtarı (keystore) oluşturulur (siz oluşturursunuz ya da bizim oluşturmamıza izin verirsiniz; parolayı siz girersiniz, yedeği sizde) → ilk AAB **Test and release → Testing → Internal testing**'e yüklenir → **App integrity → Play app signing**'den uygulama imza anahtarının ve yükleme anahtarının **SHA-1**'leri okunur | B0 |
| B2 | Play Games Services projesi | **Grow users → Play Games Services → Setup and management → Configuration** → "No, my game doesn't use Google APIs" → oyun adı → **Create**; Properties'te görünen ad [G1] | B0 |
| B3 | OAuth izin ekranı | Credentials bölümü "Configure" derse Google Cloud'da izin ekranını açın: kapsamlar `games`, `games_lite`, `drive.appdata` (doğrulama istemez); **yayınlayın (In production)** [G1] | B2 |
| B4 | Kimlik bilgileri | **Add credential → Android** → Create OAuth client (Google Cloud'da tür **Android**, paket `com.saturnwave.museumpuzzle`, B1'deki **uygulama imza** SHA-1'i) → Save. Aynısını **yükleme** ve (test APK'ları için) **hata ayıklama** SHA-1'iyle tekrarlayın. Sonra **Add credential → Game server** → Create OAuth client (tür **Web application**) → Save. Web istemcisinin Client ID ve Secret'ını A4'e girin [G1][G2] | B1, B2, B3 |
| B5 | Test edenler ve yayın | **Setup and management → Testers → Add testers**: kendi hesabınız ve test edenler (birkaç saat sürebilir). Yayından önce **Publish** (PGS ayarları oyundan önce yayımlanmalı) [G1] | B2 |
| B6 | Play Developer API hizmet hesabı | Google Cloud Console'da **B2'nin açtığı projede**: **APIs & Services → Library → "Google Play Android Developer API" → Enable**; **IAM & Admin → Service accounts → Create service account** (projede rol gerekmez) → **Keys → Add key → JSON** → indirin. Play Console → **Users and permissions → Invite new users** → hizmet hesabının e-postası → **App permissions**'ta bu oyunu ekleyin → **View financial data, orders, and cancellation survey responses** ve **Manage orders and subscriptions** → **Invite user**. JSON'u A5'e koyun [G3]. (Google hesabınız bir kuruluşa bağlıysa anahtar oluşturma kuruluş kuralıyla kapalı olabilir; öyleyse bize yazın) | B0, B2 |
| B7 | Ürünler ve lisanslı test hesabı | Ürünler (v1 satın alma işinden kalan iş) + **Settings → License testing**'e test hesapları | B0 |
| B8 | Beyanlar | Data safety (§6.3), silme bağlantısı, hesap açma cevabı | Aşama 8, site |

### C. Apple

| # | Adım | Yol | Bekler |
|---|---|---|---|
| C0 | App Store Connect uygulama kaydı, Paid Apps sözleşmesi, açık App ID (In-App Purchase'lı) | `IOS_BUILD.md` §2 | Geliştirici hesabı |
| C1 | **Game Center yeteneği** | developer.apple.com → Certificates, Identifiers & Profiles → Identifiers → `com.saturnwave.museumpuzzle` → **Game Center** işaretli olsun (`IOS_BUILD.md`'deki "başka hiçbirini işaretlemeyin" kuralı Game Center için değişir) | C0 |
| C2 | App Store Connect'te Game Center | Uygulama → sürüm sayfası → **Game Center** bölümünü açın (sıralama ya da başarı tanımlamak gerekmez) | C0 |
| C3 | **In-App Purchase anahtarı** | App Store Connect → **Users and Access → Integrations → In-App Purchase → Generate In-App Purchase Key** → ad → Generate → **Download Key** (yalnız bir kez indirilir) → anahtar kimliği (Key ID) ve sayfanın üstündeki **Issuer ID**. Dördünü A5'e girin [A4][A5] | C0 |
| C4 | Sandbox test hesabı | Users and Access → **Sandbox → Test Accounts** | C0 |
| C5 | App Privacy | §6.4'e göre | Aşama 8 |

### D. Site ve metinler

| # | Adım | Bekler |
|---|---|---|
| D1 | §6.5'in metinlerini ve §10'daki kararları onaylayın | — |
| D2 | Sitede politika güncellemesi ve **"Verilerimi sil" sayfası** (iletişim e-postası gerekli) | D1 |
| D3 | Uzman okuması: §6.2 | D1 |

### Bize gönderilecekler / gönderilmeyecekler

| Gönderin | Gönderilmesin (yalnız Unity'nin Secret Manager'ına) |
|---|---|
| "A2 bağlandı" haberi (proje ve kuruluş kimliğini dosyadan okuruz), ortam adları | Web istemcisinin **Client Secret**'ı (A4'e siz girersiniz) |
| PGS **Get resources** XML'i (Configuration → Credentials sayfası) ve Web istemcisinin **Client ID**'si | Hizmet hesabının **JSON anahtarı** |
| SHA-1'lerin hangi anahtara ait olduğu (değerlerin kendisi gizli değildir) | Apple'ın **`.p8` anahtarı** |
| C1–C3'ün yapıldığı | Yükleme anahtarının parolası |

**Engeller:** B0 olmadan B1–B8 olmaz; C0 olmadan C1–C5 olmaz; A1–A2 olmadan A3–A8 olmaz; A4(a) B4'ü, A5 B6 ve C3'ü, A6 Aşama 3'ü bekler. B0 ve C0, satın almaların gerçek denemesi için zaten bekleyen işlerdir.

---

## 10. Riskler ve açık sorular

### 10.1 Riskler

| # | Risk | Etki | Önlem |
|---|---|---|---|
| 1 | Ücretsiz sınır aşılınca bütün UGS kapanır, 30 günde oyuncular için de [U2] | Bulut kaydı ve doğrulama durur | Yerel önce tasarım (§3.8), bütçe (§2.4), Remote Config, %75 e-postasına tepki |
| 2 | IAP 5.4.4 UGS başlayınca oturum olayı, teşhis vb. gönderir [L5] | Gizlilik metni ve beyanlar değişir | Aşama 0b ölçümü; metin ondan sonra kesinleşir |
| 3 | Birleştirmede hata altın çoğaltır ya da kaybettirir | Ekonomi, güven | Özellik testleri, Editor denetimi, iki telefon testi, Remote Config'le kapatma |
| 4 | UGS'yi engelleyen hileci satın almayı telefonda doğrulatır | Gelir kaybı (bugünkü durum) | v1'de kabul; v2'de sunucu cüzdanı |
| 5 | Verinin yeri (Cloud Save, Authentication) yazılı değil [U26] | KVKK md. 9, GDPR aktarım | Unity'nin DPA'sı ve alt işleyen listesi; uzman |
| 6 | Apple.GameKit iOS 15.6 ister | iOS 15.0–15.5 cihazlar düşer (çok az) | Alt sınırı yükselt; ya da küçük bir yerel köprü (`fetchItemsForIdentityVerificationSignature`) yazmak (ayrı karar) |
| 7 | PGS eklentisinin EDM4U'su IAP'nin Gradle bağımlılıklarıyla ya da `datatransport` silme kuralıyla (A52) çakışır; `AD_ID` gelir | Derleme kırılır ya da beyan değişir | Aşama 0; derlemenin `AD_ID` ve `datatransport` denetimleri zaten var |
| 8 | Apple iadeleri yalnız oyuncu açılışında görülür | Geç işaretleme | v1'de kabul; seçenek: bildirim geçmişi |
| 9 | Anonim hesaplar birikir | 5 GiB depolama | İlk `save` ilk kazanmada; sıkıştırma; aylık temizlik görevi |
| 10 | Cloud Code 2.10.4 eski dalda kalır; Unity 6000.7'ye geçince 3.x'e geçiş | Bakım | Unity yükseltmesinde yeniden bakılır |
| 11 | PGS v2 her açılışta "Hoş geldin" şeridi gösterir; Game Center ilk açılışta giriş sayfası açar | Görünüş | Sahibin telefonda bakması (T testleri) |
| 12 | Hesap silme isteği e-postayla gelirse oyuncu kodu bilinmeyebilir | Destek yükü | Kod Ayarlar'da; kendiliğinden temizlik süresi politikada |
| 13 | Unity UGS'nin belgelenmemiş "kapatıldı" hata kodu | Yanlış sınıflandırma | Bütün hatalar aynı geri çekilmeye girer |

### 10.2 Sahibin kararları

**Karar verildi (proje sahibi, 2026-10-26):** "Bütün öneriler tamam", iki ayrımla: bağlanma önerisi ilk kazanılan bölümden sonra (karar 2), silmeden sonra kalan iz 36 ay (karar 5). Aşağıda her sorunun altında karar.

1. **"Bulut kaydı" anahtarı** Ayarlar'da olsun mu (öneri: evet, varsayılan açık; kapalıyken UGS hiç başlamaz)?
   **Karar:** evet; Ayarlar'da "Bulut kaydı" anahtarı, varsayılan açık; kapalıyken UGS hiç başlamaz (`SyncPolicy.MayStartServices`). Anahtar `cloud.json`'da; bozuk dosya anahtarı kapalı başlatır (§3.3).
2. **Bağlanma önerisi** ne zaman (öneri: ilk satın almadan sonra ya da 20. bölümde, bir kez)?
   **Karar (öneriden farklı):** Google Play Games / Game Center'a bağlanma önerisi bir kez, **ilk kazanılan bölümden sonra**; 20. bölümde ya da ilk satın almadan sonra değil (§5.3).
3. **İade politikası v1:** yalnız işaretle (öneri) mi, yoksa altını cüzdanda olduğu kadar düş mü?
   **Karar:** v1'de iadeler yalnız işaretlenir; altın geri alınmaz.
4. **Sahte satın alma (`Invalid`):** teslim etme (öneri) — onay mı?
   **Karar:** `Invalid` satın alma teslim edilmez (ve mağazaya onaylanmaz).
5. **Silmeden sonra saklanan iz:** belirteç özeti, ürün, tarih, durum; süre (öneri 12 ay; uzman).
   **Karar (öneriden farklı):** iz (belirteç özeti, ürün, tarih, durum) **36 ay** saklanır (§5.6, §6.2.7, §6.5; uzman okuması yine gerekli).
6. **Remote Config** v1'e girsin mi (öneri: evet, dört anahtar: `cloud_sync_enabled`, `cloud_upload_min_minutes`, `cloud_read_min_hours`, `purchase_validation_enabled`)?
   **Karar:** evet, bu dört anahtarla (`RemoteConfigKeys`; sınırları §3.4'teki Aşama 1 notunda).
7. **iOS alt sınırı 15.6** (öneri: evet).
   **Karar:** evet, iOS en düşük 15.6.
8. **Modülü kim yükler:** siz Editor'dan (öneri) mi, bizim için ayrı bir hizmet hesabı mı?
   **Karar:** proje sahibi Cloud Code modülünü Editor'ın Deployment penceresinden yükler.
9. **Data safety'de hesap açma cevabı** ve "User IDs: Required / Users can choose".
   **Karar:** §6.3'teki önerilen cevaplar.
10. **Yükleme anahtarını** kim oluşturur (siz ya da sizin izninizle biz)?
    **Karar:** Android yükleme anahtarını proje sahibi oluşturur.

### 10.3 Açık sorular (araştırılacak)

- UGS'nin ücretsiz sınır aşılınca döndüğü hata ve oyuncu erişiminin hemen mi, 30 gün sonra mı kesildiği ("access … is blocked" ifadesi belirsiz) [U2].
- Cloud Save'in toplu yazmasının kaç "yazma" sayıldığı (tasarım tek anahtarla yazarak bundan kaçınıyor).
- Cloud Save ve Authentication verisinin bölgesi [U26].
- IAP'nin oturum olayının içeriği ve kapatılıp kapatılamadığı (Aşama 0).
- Cloud Code modülünün hedef .NET sürümü (şablondan) ve istek gövdesi sınırı (belgede yok).
- `purchases.productsv2`'nin alanları ve test satın almasının işareti (Aşama 3) [G4].
- Apple'ın bildirim geçmişinin, gönderilemeyen bildirimleri de tuttuğu (seçenek §4.6).
- PGS v2'nin `RequestServerSideAccess`'inin oyuncuya ek izin sorup sormadığı (yalnız varsayılan kapsamlar istendiğinde sormaması beklenir [G2]).

---

## 11. Kaynaklar

Bütün web sayfalarına **2026-10-26**'da bakıldı. "Güncelleme" sütunu sayfanın kendi tarihi (Unity sayfaları göreli tarih veriyor).

| No | Kaynak | URL | Sayfanın tarihi |
|---|---|---|---|
| U1 | Unity Gaming Services pricing | https://unity.com/products/gaming-services/pricing | — |
| U2 | Pricing and billing (sınır aşımı, 30 gün) | https://docs.unity.com/en-us/services/pricing-and-billing | "5 ay önce" |
| U3 | Billing FAQ ve bütçe/uyarı sayfası (arama özeti) | https://support.unity.com/hc/en-us/articles/6821475035412-Billing-FAQ ; https://docs.unity.com/ko-kr/cloud/billing/budget | — |
| U4 | Economy (2026-09-08'den beri yeni projeye kapalı) | https://docs.unity.com/en-us/economy | "3 gün önce" |
| U5 | Economy SDK purchases | https://docs.unity.com/en-us/economy/sdk/purchases | — |
| U6 | Economy: add real money purchase | https://docs.unity.com/en-us/economy/add-real-money-purchase | — |
| U7 | IAP receipt validation | https://docs.unity.com/en-us/iap/receipt-validation | "9 ay önce" |
| U8 | IAP 5.4 değişiklik günlüğü | https://docs.unity3d.com/Packages/com.unity.purchasing@5.4/changelog/CHANGELOG.html | 5.4.4: 2026-10-02 |
| U9 | IAP 5.0 değişiklik günlüğü | https://docs.unity3d.com/Packages/com.unity.purchasing@5.0/changelog/CHANGELOG.html | — |
| U10 | Unity paket kaydı (authentication, cloudsave, cloudcode, core, remote-config, economy, deployment) | https://packages.unity.com/com.unity.services.authentication (ve öteki adlar) | sürüm tarihleri §2.3 |
| U11 | Authentication: Google Play Games | https://docs.unity.com/en-us/authentication/platform-signin/google-play-games | "8 ay önce" |
| U12 | Authentication: Apple Game Center | https://docs.unity.com/en-us/authentication/platform-signin/apple-game-center | "10 ay önce" |
| U13 | Authentication: delete accounts | https://docs.unity.com/en-us/authentication/delete-accounts | "8 ay önce" |
| U14 | Cloud Save Player Data | https://docs.unity.com/en-us/cloud-save/concepts/player-data | "8 ay önce" |
| U15 | Cloud Save write locks | https://docs.unity.com/en-us/cloud-save/concepts/write-locks | — |
| U16 | Cloud Save Game Data (custom items) | https://docs.unity.com/en-us/cloud-save/concepts/game-data | "10 ay önce" |
| U17 | Cloud Save Files | https://docs.unity.com/en-us/cloud-save/concepts/files | "10 ay önce" |
| U18 | Cloud Code limits (modüller; 3.0.0 sürümü dahil) | https://docs.unity.com/en-us/cloud-code/modules/reference/limits ; https://docs.unity.com/en-us/cloud-code/3.0.0/modules/reference/limits | "3 gün önce" |
| U19 | Secret Manager: store secrets | https://docs.unity.com/en-us/services/secret-manager/tutorials/store-secrets | — |
| U20 | Secret Manager: Cloud Code modules (ve .NET sürümü, arama özeti) | https://docs.unity.com/en-us/services/secret-manager/tutorials/integrations/cloud-code/modules | — |
| U21 | Secret Manager cost | https://docs.unity.com/en-us/services/secret-manager/reference/cost | — |
| U22 | Triggers limits ve cost | https://docs.unity.com/en-us/triggers/limits ; https://docs.unity.com/en-us/triggers/use-cloud-code/cost | "≈6 ay önce" |
| U23 | Triggers: supported UGS events | https://docs.unity.com/en-us/triggers/manage-events/supported-ugs-events | — |
| U24 | Services Core değişiklik günlüğü (teşhis) | https://docs.unity3d.com/Packages/com.unity.services.core@1.12/changelog/CHANGELOG.html | — |
| U25 | Forum: Delete Player API (REPORTED) | https://discussions.unity.com/t/delete-player-api/1524706 | — |
| U26 | Where is my data stored when using UGS (arama özeti) | https://support.unity.com/hc/en-us/articles/46140780909460-Where-is-my-data-stored-when-using-Unity-Gaming-Services | — |
| U27 | Cloud Code NuGet paketleri | https://www.nuget.org/packages/Com.Unity.Services.CloudCode.Apis ; https://www.nuget.org/packages/Com.Unity.Services.CloudCode.Core | 0.0.27 / 0.0.9 |
| G1 | Set up Play Games Services | https://developer.android.com/games/pgs/console/setup | 2026-10-07 |
| G2 | PGS v2 server-side access | https://developer.android.com/games/pgs/android/server-access | 2026-10-06 |
| G3 | Google Play Developer API: getting started | https://developers.google.com/android-publisher/getting_started | 2025-12-18 |
| G4 | purchases.products.get ve productsv2 | https://developers.google.com/android-publisher/api-ref/rest/v3/purchases.products/get ; https://developers.google.com/android-publisher/api-ref/rest/v3/purchases.productsv2/getproductpurchasev2 | 2025-06-02 / 2025-06-30 |
| G5 | PGS data collection | https://developer.android.com/games/pgs/data-collection | 2026-10-06 |
| G6 | Play Developer API quotas | https://developers.google.com/android-publisher/quotas | 2026-04-29 |
| G7 | Voided Purchases API | https://developers.google.com/android-publisher/voided-purchases | 2025-12-18 |
| G8 | Account deletion requirements | https://support.google.com/googleplay/android-developer/answer/13327111 | — |
| G9 | Google Play Games eklentisi (sürümler) | https://github.com/playgameservices/play-games-plugin-for-unity/releases | v2.3.0: 2026-09-29 |
| A1 | App Store Server API: Get Transaction Info | https://developer.apple.com/documentation/appstoreserverapi/get-transaction-info | — |
| A2 | Get Refund History | https://developer.apple.com/documentation/appstoreserverapi/get-refund-history | — |
| A3 | JWSTransactionDecodedPayload | https://developer.apple.com/documentation/appstoreserverapi/jwstransactiondecodedpayload | — |
| A4 | Creating API keys | https://developer.apple.com/documentation/appstoreserverapi/creating-api-keys-to-authorize-api-requests | — |
| A5 | Generating JSON Web Tokens | https://developer.apple.com/documentation/appstoreserverapi/generating-json-web-tokens-for-api-requests | — |
| A6 | Enabling App Store Server Notifications | https://developer.apple.com/documentation/appstoreservernotifications/enabling-app-store-server-notifications | — |
| A7 | Identifying rate limits | https://developer.apple.com/documentation/appstoreserverapi/identifying-rate-limits | — |
| A8 | Get Notification History | https://developer.apple.com/documentation/appstoreserverapi/get-notification-history | — |
| A9 | Apple Unity Plug-ins (README: en düşük iOS 15.6) | https://github.com/apple/unityplugins | son değişiklik 2026-10-19 |
| A10 | App Review Guidelines (5.1.1, 4.5.3, 4.5.5) | https://developer.apple.com/app-store/review/guidelines/ | — |
| L1 | Satın alma ve analiz denemesi | `docs/research/iap-and-analytics.md` | 2026-10-24 |
| L2 | Gizlilik v1 taslağı | `docs/research/privacy-v1.md` | 2026-10-24/25 |
| L3 | Monetizasyon raporu §7.3 | `docs/research/monetization-and-retention.md` | 2026-10-23 |
| L4 | Oyun tasarımı §17, §19, §20, §23, §25 | `docs/GAME_DESIGN.md` | 2026-10-26 |
| L5 | IAP 5.4.4 kaynağı: `Runtime/Stores/ServicesCore/IapCoreInitializeCallback.cs`, `Runtime/Purchasing/Unity.Purchasing.asmdef` (`IAP_UNITY_AUTH_ENABLED`), `Runtime/Purchasing/Core/Purchasing/PurchaseService.cs` (`IAP_TX_VERIFIER_ENABLED`), `Runtime/TransactionVerifier/README.md`, `IGooglePlayStoreExtendedService.cs`, `IAppleStoreExtendedService.cs`, `GoogleOrderInfo.cs`, `AppleOrderInfo.cs` | `unity/Library/PackageCache/com.unity.purchasing@215c35b51cf4` | — |
| L6 | iOS dalı: `iOSTargetOSVersionString: 15.0`; `docs/IOS_BUILD.md` | `D:/puzzle-ios` | 2026-10-25 |
| L7 | Motor ve istemci: `Puzzle.Core/Recording` (`AttemptRecord`, `AttemptReplay`), `Randomness/Rng.cs`, `unity/Assets/Generated/Engine/*.dll`, `Flow/ProgressFile.cs`, `Flow/PlayerProgress.cs`, `Flow/ProgressDocument.cs`, `Purchases/PurchasesMade.cs`, `Flow/WalletLedger.cs` | depo | — |
| L8 | Ürün kimlikleri ve kalıcı uygulama kimliği | `GAME_DESIGN.md` §19 | 2026-10-25 |

**Bu belgede yapılmayanlar (plan yazılırken):** kod, Unity projesi, paketler ve öteki belgeler değişmedi; Unity, git ve telefon çalıştırılmadı; PDF üretilmedi (bu dosya `tools/docs-pdf/build.py`'nin listesine Aşama 8'de eklenmeli); hiçbir hesaba girilmedi; UGS'nin, IAP'nin ve PGS'nin ağ trafiği ölçülmedi (Aşama 0b). Aşama 1 (2026-10-26) kod ve testler ekledi; paket eklemedi, hiçbir hesaba girmedi.
