# Herkese Açık Test — Kullanım Kılavuzu

> Oyun yalnızca iOS ve Android'de yayınlanacak. Bu kılavuz, mobil sürümden önce tanıdıklara tarayıcıdan oynatıp gerçek oyuncu verisi toplamak için kullanılan test sunucusunu anlatır.

## 1. Ne işe yarar

- Tanıdıkların bir internet adresinden giriş yapıp bölümleri **gerçek oyundaki gibi sırayla** oynar: kazanınca sonraki bölüm açılır, kaybedince aynı bölüm yeniden denenir.
- Her deneme, mobil oyunun da kullanacağı biçimde kaydedilir (bölüm, tohumlar, hamle limiti, hamleler, sonuç). Kayıtta kullanıcı adı değil, rastgele bir oyuncu kimliği durur.
- Bu kayıtlar botlarla karşılaştırılır: "insanlar hangi bota benziyor" sorusu veriyle cevaplanır ve hamle limitleri buna göre ayarlanır ([GAME_DESIGN.md](GAME_DESIGN.md) §20).

Oyuncu sayfası 2026-10-06'dan beri oyunun seçilen görsel tasarımıyla açılıyor (galeri duvarı, yaldız çerçeve, uğur böceği ve papatya taşları). Sayfanın dosyaları `src/Puzzle.Web/wwwroot/player/` altında; taş çizimleri `tools/web-theme/build-sprite.js` ile üretiliyor.

## 2. İki çalışma modu

| Mod | Kim için | Ne açık |
|---|---|---|
| **Test modu** (varsayılan) | Geliştirme; bilgisayarından ve yerel ağdan | Bölüm seçimi, tohumlar, ipucu, botlar, çözücü, simülasyon, kayıtlar |
| **Herkese açık mod** | İnternetten oynayan oyuncular | Yalnızca giriş ve oyun. Test paneli `/lab` adresinde, sadece yönetici anahtarıyla |

Test modu tünelden gelen istekleri kendisi reddeder; yanlışlıkla test modunda başlatılan bir sunucu internete açılmaz.

## 3. Başlatma

Windows PowerShell'de, proje klasöründe:

```bash
dotnet build -c Release
```

```bash
.\tools\public\Start-PublicServer.ps1
```

Betik sırayla şunları yapar:

1. Release derlemesi yoksa durur.
2. Hesapları listeler ve hazır test hesaplarını bildirir.
3. Yönetici anahtarını Windows kullanıcının ortam değişkeninden okur. İlk çalıştırmada rastgele bir anahtar üretir, oraya kaydeder ve **bir kez** gösterir; kopyalayıp sakla.
4. Sunucuyu herkese açık modda `http://localhost:5199` adresinde başlatır. Pencere açık kalmalı.

PowerShell betik çalıştırmayı reddederse:

```bash
powershell -ExecutionPolicy Bypass -File .\tools\public\Start-PublicServer.ps1
```

Anahtarı sonradan okumak için:

```bash
[Environment]::GetEnvironmentVariable('Puzzle__AdminKey', 'User')
```

## 4. İnternete açma (Tailscale Funnel)

Sunucu çalışırken ikinci bir pencerede:

```bash
tailscale funnel --bg --https=8443 5199
```

- **Oyuncuların adresi:** `https://arc.tail0b9256.ts.net:8443`
- **Test paneli (yalnızca sen):** `https://arc.tail0b9256.ts.net:8443/lab`, yönetici anahtarıyla.
- Bilgisayar açık ve sunucu çalışıyor olmalı. Adres sabittir.

Kapatmak için:

```bash
tailscale funnel --https=8443 off
```

`tailscale funnel reset` kullanma: bu bilgisayarın 443 portunda yayınladığı diğer servisleri de kapatır.

## 5. Hesaplar

- **Hazır test hesapları:** `src/Puzzle.Web/public-test-players.json` dosyasında tanımlıdır (kullanıcı adları ve ortak test şifresi). Sunucu herkese açık modda açılırken eksik olanları kendisi oluşturur; var olan hesaba ve ilerlemesine dokunmaz.
- Bu hesaplar yalnızca oyuncudur. Adlarında "Admin" geçmesi yetki vermez; test paneli yönetici anahtarı ister.
- Elle hesap yönetimi (sunucu kapalıyken):

```bash
dotnet run --project src\Puzzle.Cli -c Release --no-build -- players list
```

```bash
dotnet run --project src\Puzzle.Cli -c Release --no-build -- players add <kullanıcı-adı>
```

```bash
dotnet run --project src\Puzzle.Cli -c Release --no-build -- players password <kullanıcı-adı>
```

```bash
dotnet run --project src\Puzzle.Cli -c Release --no-build -- players delete <kullanıcı-adı>
```

`add` ve `password` şifreyi konsolda gizli olarak sorar ve en az 8 karakter ister. `delete` hesabı, ilerlemeyi ve o oyuncunun kayıtlarını siler.

## 6. Veriler nerede

| Klasör | İçerik |
|---|---|
| `data/` | Hesaplar, ilerleme, yarım kalan denemeler, oturum anahtarları |
| `recordings/attempts.jsonl` | Her deneme için bir satır |

İkisi de git'e girmez. Yedeklerken ikisini birlikte al.

Sonuçlara bakmak için test panelindeki **Kayıtlar** sekmesi ya da:

```bash
dotnet run --project src\Puzzle.Cli -c Release --no-build -- humans
```

## 7. Test sürerken bölümleri değiştirme

Sunucu bölüm dosyalarını yalnızca açılışta okur. Bölümler diskte değiştirildikten sonra geçerli olmaları için sunucunun yeniden başlatılması gerekir:

1. Sunucu penceresinde Ctrl+C ile durdur (bekleyen kayıtlar yazılır). Pencereye ulaşamıyorsan başka bir pencereden `.	oolspublicStop-PublicServer.ps1` aynı işi yapar: sunucunun konsoluna Ctrl+C gönderir, hiçbir şeyi zorla kapatmaz.
2. Yeniden başlat: `.\tools\public\Start-PublicServer.ps1`. Günlükte `Public mode: 1000 levels` satırı görünmeli.

- Değiştirilen bölümlerin eski sürümleri `levels/archive/` altında durur; eski kayıtlar bu sayede botlarla karşılaştırılmaya devam eder.
- Yarım kalmış denemesi olan oyuncunun o denemesi düşer; oyuncu bulunduğu bölümün yeni sürümünden devam eder. Geçilmiş bölümlere dönülmez.
- Sunucuyu, kapanabilecek bir araç ya da oturumun içinden başlatma: o oturum bitince sunucu da kapanır (2026-10-05'te yaşandı). Kendi PowerShell pencerende başlat.
- Sunucu çalışırken çözümü Release olarak derleme ve `src/Puzzle.Web/wwwroot` altını düzenleme: sayfalar kaynak klasöründen canlı sunulur.
- İlk 50 bölüm 2026-10-05'te daha kolay sürümleriyle değiştirildi; ardından 1000 bölümün hepsi yüzde 25 hamle payıyla yeniden üretildi ([GAME_DESIGN.md](GAME_DESIGN.md) §8 madde 7 ve 8).

## 8. Korumalar ve bilinen sınırlar

- Şifreler düz metin saklanmaz (PBKDF2). Beş yanlış denemeden sonra kullanıcı adı beş dakika kilitlenir; adres başına ve toplamda deneme sınırı vardır.
- Oyuncular çözücüye, ipucuna, bota, bölüm ya da tohum seçimine erişemez; bunlar sunucu tarafında kapalıdır.
- Yarım kalan oyun, sunucu yeniden başlasa da kaldığı tahtadan devam eder.
- **Hazır hesapların şifresi kasıtlı olarak basittir.** Adresi bulan biri bu hesaplarla girip oynayabilir; olabilecek en kötü şey toplanan verinin kirlenmesidir. Kişisel veri ya da test paneli açığa çıkmaz.
- Oyuncu sayfası yazı tiplerini Google Fonts'tan yükler: her ziyarette oyuncunun tarayıcısı `fonts.googleapis.com` ve `fonts.gstatic.com` adreslerine bağlanır (adres ve tarayıcı bilgisi Google'a görünür). Başka hiçbir dış adrese bağlanılmaz; güvenlik politikası yalnızca bu ikisine izin verir.
- Çok sayıda adresten gelen istek, yeni girişleri geçici olarak engelleyebilir; giriş yapmış oyuncular oynamaya devam eder.
- Gerçek Funnel üzerinden ilk giriş ve betiğin baştan sona ilk çalışması sahibi tarafından yapılır; yayına açtıktan sonra adresin giriş ekranını gösterdiğini ve `/api/levels` adresinin reddedildiğini bir kez kontrol et.
