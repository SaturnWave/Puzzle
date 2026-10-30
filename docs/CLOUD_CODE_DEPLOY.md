# Cloud Code Modülünü Yükleme

**Tarih:** 2026-10-26. **Kimin için:** proje sahibi. **Ne:** v1 sunucunun Cloud Code modülü `MuseumPuzzleServer` (planın Aşama 3'ü, `docs/research/server-v1.md` §4). Modül yazıldı ve testli; **henüz hiçbir yere yüklenmedi**, oyun da onu henüz çağırmıyor (oyundaki doğrulama Aşama 4). Bu belge planın §9 A6 adımının ayrıntısı. Claude hiçbir hesaba girmez, hiçbir anahtarı görmez; aşağıdaki her adımı siz yaparsınız.

## 1. Modül ne yapar

| Uç | Kim çağırır | Ne yapar |
|---|---|---|
| `ValidatePurchase` | Oyun, her satın almadan sonra | Google Play'e ya da App Store'a sorar; gerçekse oyuncunun defterine (`purchases`) sıra numarasıyla yazar; cevabı döner (geçerli, daha önce doğrulanmış, beklemede, geçersiz, şu an karar yok) |
| `CheckRefunds` | Oyun, açılışta; App Store satın alması olan oyuncu için | Apple'ın iade geçmişine en çok 20 saatte bir bakar; iade edileni defterde "refunded" işaretler (altın geri alınmaz) |
| `DeleteMyData` | Oyun, "Verilerimi sil" | Oyuncunun Cloud Save'deki bütün verisini siler; fiş kayıtlarından oyuncu kimliğini siler, 36 ay kalacak izi bırakır |
| `PollGoogleVoided` | Zamanlayıcı, her gün 03:00 UTC | Google'ın iptal edilen satın almalar listesini okur, defterde "voided" işaretler |
| `ForgetOldTraces` | Zamanlayıcı, her gün 04:00 UTC | 36 ayı dolan izleri siler |

Son ikisini bir oyuncu çağırırsa modül reddeder. Modülün Cloud Save'de tuttukları:

- Oyuncu verisi, Protected sınıf: `purchases` (defter; oyuncu okur, yalnız modül yazar).
- Custom item, Private sınıf (oyuncu okuyamaz): `rcpt-…` (fiş başına bir kayıt), `forgotten-YYYY-MM-DD` (o gün izi başlayan fişler), `server-state` (Google listesinin nereye kadar okunduğu).

## 2. Ön koşullar

| # | Ne | Nerede | Not |
|---|---|---|---|
| 1 | Bulut projesi açık ve Editor'a bağlı, `development` ortamı var | Plan §9 A1–A3 | |
| 2 | Unity'de **Cloud Code** paketi `com.unity.services.cloudcode` 2.10.4 | Planın paket listesi (§2.3) | Projede (Aşama 2, 2026-10-26) |
| 3 | Unity'de **Deployment** paketi `com.unity.services.deployment` | Projede: Unity In-App Purchasing 5.4.4 getiriyor (`packages-lock.json`: 1.7.2) | Eklenecek bir şey yok (Aşama 2'de görüldü, 2026-10-26). Deployment penceresi bu paketle gelir; yalnız Editor'da çalışır, oyuna girmez |
| 4 | .NET SDK | Bu bilgisayarda 10.0.400 kurulu | Unity → **Edit → Preferences → Cloud Code → .NET path**: `C:\Program Files\dotnet\dotnet.exe`. Modül .NET 9'u hedefler; ilk derlemede gerekli paketler NuGet'ten kendiliğinden iner |
| 5 | Dört sır | Plan §9 A5 (Dashboard → proje → **Development → Secrets**) | `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON`, `APPLE_IAP_PRIVATE_KEY`, `APPLE_IAP_KEY_ID`, `APPLE_ISSUER_ID`; her birinde "erişebilecek hizmet: Cloud Code". Sırlar girilmeden de modül yüklenir; o zaman satın almalara "şu an karar yok" der ve oyun bugünkü gibi davranır |

Hiçbir sayfa kart ya da ödeme bilgisi istemez. İsterse durun ve bize yazın.

## 3. Yükleme (development)

1. Unity Hub'dan projeyi açın.
2. Project penceresinde `Assets/CloudCode/MuseumPuzzleServer.ccmr` dosyasını seçin. Inspector'da **Solution Path** `../../../server/CloudCode/MuseumPuzzleServer.sln` olmalı. **"Generate Solution" düğmesine basmayın**: çözüm hazır, düğme yerine yeni bir şablon yazar.
3. **Services → Deployment** penceresini açın. Üstteki **Deployment Settings**'te **Editor Environment** olarak `development`'ı seçin.
4. Listede **MuseumPuzzleServer**'ı işaretleyin → **Deploy Selected**. Pencere modülü derler (`dotnet publish … -c Release -r linux-x64`), `MuseumPuzzleServer.ccm` olarak paketler (bizim denememizde 2,4 MB açık, sınır 10 MB) ve yükler. Yeşil tik başarı demek.
5. Dashboard → proje → **Cloud Code → Modules → MuseumPuzzleServer**: beş fonksiyon görünmeli: `ValidatePurchase`, `CheckRefunds`, `DeleteMyData`, `PollGoogleVoided`, `ForgetOldTraces`.

**Ekransız yükleme (2026-10-27):** aynı yükleme bir komutla da yapılır; Claude bunu sizin yerinize çalıştırabilir. Tek koşul, bu bilgisayardaki Unity Hub'da Unity hesabınızın açık olması: yükleme Editor'ın oturumuyla yapılır, başka anahtar ya da parola gerekmez. Komut Editor'ı penceresiyle açar, ortamı seçer, Deployment penceresini açıp modülü yükler, sonucu kayda yazar ve Editor'ı kapatır (`unity/Assets/Editor/CloudCodeDeploy`):

```
Unity.exe -projectPath unity -executeMethod Puzzle.Client.EditorTools.CloudCodeDeploy.Run -deployEnvironment development -logFile out/unity/cloud-code-deploy.log
```

Kaydın son satırı `Cloud Code deploy: PASSED` ya da `FAILED` ve nedeni. Araç, Editor'ın oturumu Hub'dan alması için en çok 2 dakika bekler; Hub'da oturum yoksa satır "the Editor did not sign in to Unity" der. Unity'nin yükleme isteği 10 saniyede cevap alamazsa (`Curl error 28`, "status code 0") yükleme yeniden denenir; 27 Ekim'de ikinci deneme geçti.

**2026-10-27:** modül `development`'a ve `production`'a bu araçla yüklendi; panelde Cloud Code → Modules'ta `MuseumPuzzleServer` görünüyor, canlı yoklama (`LiveCloudProbe`) `production`'da modülün cevap verdiğini gördü. 4. bölümün günlük görevleri henüz kurulmadı. Unity'nin panelinde modül yükleme düğmesi yok (yalnız Editor ve UGS komut satırı aracı); komut satırı aracı bir hizmet hesabı anahtarı ister, o yol kullanılmıyor.

## 4. Günlük görevler (Dashboard'dan)

Deployment penceresi zamanlayıcıyı yüklemiyor (Unity'nin bunun için Editor paketi yok). Dashboard → proje → **Triggers**:

| # | Adım | Değerler |
|---|---|---|
| 1 | **Schedules → Create schedule** | Olay adı `poll-google-voided`; tür **Recurring**; cron `0 3 * * *` (UTC); payload `{}` |
| 2 | **Schedules → Create schedule** | Olay adı `forget-old-traces`; tür **Recurring**; cron `0 4 * * *`; payload `{}` |
| 3 | **Triggers → Create trigger** | Olay `com.unity.services.scheduler.poll-google-voided.v1` → eylem **Cloud Code module function**: modül `MuseumPuzzleServer`, fonksiyon `PollGoogleVoided` |
| 4 | **Triggers → Create trigger** | Olay `com.unity.services.scheduler.forget-old-traces.v1` → `MuseumPuzzleServer` / `ForgetOldTraces` |

Aynı tanımlar dosya olarak `server/CloudCode/schedules/daily-chores.sched` ve `daily-chores.tr`'de; UGS komut satırı aracıyla (`ugs deploy`) yüklemek bir hizmet hesabı ister, o ayrı bir karardır (plan karar 8: modülü siz Editor'dan yüklersiniz). Ekran adları değişmişse en yakın adı seçin. Görevin çalıştığı ertesi sabah **Cloud Code → Logs**'ta "Google's voided purchases: done" ve "Old traces: done" satırlarıyla görünür.

## 5. Deneme ve production

1. `development`'ta Aşama 7'nin testleri: `docs/PHONE_TESTS.md` F30 (T550–T558). Lisanslı Google test hesabıyla ve Apple sandbox hesabıyla.
2. Testler geçince: Deployment Settings → **production** → **Deploy Selected**; 4. bölümdeki zamanlamaları ve tetikleyicileri production ortamında da kurun (her ortamın kendi zamanlayıcısı var). Sırlar ortama özelse production'a da girin.
3. Modül her değiştiğinde (biz söyleriz) 3. bölümün 3–4. adımları yeniden yapılır. Yüklemeden sonra Unity'nin söylediği gibi birkaç saniye beklenir: eski sürüm kısa bir süre daha cevap verebilir.

## 6. Sorun giderme

| Belirti | Neden, çözüm |
|---|---|
| "Failed to retrieve main project" | `server/CloudCode` altında tek bir yayın profili olmalı (`MuseumPuzzleServer/Properties/PublishProfiles/FolderProfile.pubxml`). Silinmiş ya da ikincisi eklenmişse bize yazın |
| "dotnet not found" | 2. bölüm, 4. satır: .NET yolu |
| Derleme penceresinde NETSDK1194 uyarısı | Unity'nin kendi komutundan; zararsız |
| Satın almalar hep "şu an karar yok" (`credentialsMissing`) | Sır eksik, adı yanlış ya da Cloud Code erişimi seçilmemiş. Cloud Code sırları 5 dakikaya kadar önbellekte tutar |
| `credentialsRefused` | Google: hizmet hesabı Play Console'da bu oyuna davet edilmemiş ya da izinleri eksik (plan §9 B6). Apple: anahtar iptal edilmiş ya da Issuer ID yanlış |
| Bir oyuncunun verisi silinemiyor | Cloud Code → Logs'ta "DeleteMyData" satırları; oyun yeniden denetir, ikinci çağrı birincinin yarım bıraktığını bitirir |

**Yerel Cloud Code sunucusu** (Unity 6000.3 ve sonrası, Cloud Code araç çubuğu): bağlı proje ve bu bilgisayarda .NET 9 çalışma ortamı ister (bugün yalnız .NET 10 kurulu). Aşama 7'de istenirse kurulur; modülün testleri ona gerek duymadan bütün akışları sahte Cloud Save, sahte Google ve sahte Apple'la deniyor.
