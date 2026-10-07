# Geliştirme Günlüğü

Her oturumun kararları, bulguları ve değişiklikleri. En yeni kayıt en üstte.

---

## 2026-10-07 · Oturum 16 — Unity istemcisi: oynanabilir tahta telefonda

**Kullanıcı kararı**
- "Evet başla": tahta sahnesi yapılacak (ilk APK kapsamı: tahta, kazandın/kaybettin, sonraki bölüm, kayıt; harita, meta, mağaza sonraya).

**Nasıl yapıldı**
- Altı aşamalı, sıralı bir iş akışı (Unity projesini aynı anda tek işlem kullanabildiği için paralel değil): altyapı → galeri görünümü → oynanış → akış → bağımsız eleştiri → düzeltme. Yaklaşık 4 saat 20 dakika, 6 ajan, 2,7 milyon token. Ajanlar depoya yazmadı, APK derlemedi; sonuç gözden geçirilip tek seferde işlendi.
- Editor penceresi hiç açılmadı: sahne, ayarlar ve varlıklar Editor betikleriyle komut satırından üretiliyor (`ProjectSetup.Run`).

**Ne var**
- **Çizim hattı:** proje URP 2B'de. SRP Batcher kapalı: açıkken taşlar başka taşların resmiyle çiziliyordu (Editor'ın toplu çiziminde görüldü).
- **Görseller:** 41 taş çizimi tek atlasta (4096 px, Android'de ASTC). Duvar deseni, plaket, madalyon, raf ve aplik için SVG kaynakları `art/unity/` altında; `tools/unity/export-gallery-art.js` PNG'ye çeviriyor.
- **Görünüm:** camgöbeği damask duvar, aplikler, tele asılı BÖLÜM / HAMLE / HEDEF plaketleri, tahtanın çevresini ve iç deliklerini izleyen basamaklı ve boncuklu yaldız çerçeve, dokulu tepsi, alt raf. Web testindeki sayfayla yan yana çok yakın.
- **Yazı:** TextMesh Pro; tasarımın yazı tipleri indirilmediği için Unity ile gelen Roboto Bold kullanılıyor (geçici).
- **Düzen:** uzun telefon (1080×2340, 1080×1920) ve katlanır telefonun kare iç ekranı (2076×2152); güvenli alan payları hesaba katılıyor. Düzen hesabı Unity'siz saf kod (`ScreenLayout`), testli.
- **Oynanış:** sürükleyerek ya da iki taşa sırayla dokunarak takas; özel taşa dokunmak onu patlatır; geçersiz takas geri seker. Her hamle motorun olay günlüğünden zamanlı bir animasyona çevriliyor (`TurnChoreographer`, `Timeline`): patlama, düşme (tek düşme yasası, hafif esneme), roket, bomba, büyük bomba, renk bombası, kelebek, kutu/bant/jöle hasarı, malzemenin çıkışı, karıştırma. Düz bir hamle 0,5–0,7 saniye.
- **Güvence:** her hamleden sonra ekrandaki tahta motorun tahtasıyla karşılaştırılıyor. Botlarla 70'ten fazla bölüm baştan sona oynatıldı; hepsinde ekran motorla aynı bitti.
- **Akış:** bölüm bitince oda kararır, sonuç kartı tellerle iner ("Bölüm tamamlandı!", "Hamle bitti!"), yeşil düğme sonraki bölüme ya da aynı bölümün sıradaki tahtasına götürür. Deneme sayımı web sunucusunun kuralıyla aynı (k. deneme havuzun k. tohumunu kullanır).
- **Kayıt:** ilerleme telefonda bir JSON dosyasında; bozuk dosya kenara alınır, daha yeni bir sürümün dosyasına dokunulmaz.
- **Komut satırı araçları:** `RenderHarness` (istenen bölüm, ekran boyu ve andaki görüntü), `Storyboard` (botla oynatıp animasyon karelerini çıkarır), `FlowStory`, `PlayModeCheck`, `TouchWhileMovingCheck`, `ProgressFileCheck`.

**Bağımsız eleştirinin bulduğu ve düzeltilenler**
- **Engelleyici:** derin bir zincirlemede taşın "konma" animasyonu taşın kendisinden uzun yaşayabiliyor, hata verip tahtayı kalıcı donduruyordu (285. bölüm, expert bot, 5. hamle). Senaryo ve görüntü düzeyinde iki ayrı yerden kapatıldı; aynı sınıftan bir hata daha geniş taramada bulunup giderildi.
- Hata veren bir hamleden geri dönüş yoktu: artık tahta motorun durumuna döndürülüyor.
- Animasyon sürerken yapılan dokunuş atılıyordu: tahta durmadan en çok 0,8 saniye önce yapılan kaydırma bekletilip oynanıyor.
- Seçili taş işareti yeşil taşta görünmüyordu; iç çerçevenin köşe birleşimleri; rakamların fazla kalınlığı; bölüm geçişindeki takılma.

**Doğrulama (benim yaptığım)**
- `dotnet test`: 824 test geçiyor (639'dan; yeni testler düzen, çerçeve hattı, zaman çizelgesi, hamle senaryosu, dokunma, akış).
- Unity içinde: proje kurulumu güncel, motor denetimi geçiyor (1000 bölüm, 12 sabit oyun aynı), oyun sahnesi hatasız açılıyor.
- **APK:** 36 MB, 13,6 dakikada derlendi. **Telefonda (Pixel 9 Pro Fold, dış ekran 1080×2424) çalışıyor:** 1. bölüm doğru görünümle açıldı; komutla yapılan bir kaydırma hamleyi oynattı (HAMLE 24 → 23); cihaz günlüğünde oyun hatası yok.

**Açık**
- Gerçek parmakla uzun oynama, arka plana alma, iç (kare) ekran ve ucuz telefonlarda akıcılık henüz denenmedi.
- Yazı tipleri (tasarımdaki Gabarito, Paytone One, Young Serif) indirilmedi; Roboto Bold geçici.
- Ses, titreşim, ipucu, uzun zincirlemeyi atlama yok. Bir renk bombasının bombayla birleşmesi 5 saniyeyi buluyor.
- Harita ekranı, tasarım tuvalindeki zengin kazanma/kaybetme ekranları, booster rafı yok.
- Bölüm geçişinde duvar, plaketler ve çerçeve yeniden kuruluyor; telefonda süresi ölçülmedi.
- Disk: 5,9 GB boş.

## 2026-10-07 · Oturum 15 — Unity kurulumu ve proje iskeleti

**Kullanıcı kararları**
- "Unity bedava mı, bu bilgisayar kaldırır mı" soruları yanıtlandı: Unity Personal 200.000 dolar gelir ve yatırım sınırına kadar ücretsiz; i7-1255U, 16 GB bellek ve Iris Xe yeterli; dar nokta disk.
- Kurulumu kullanıcı başlattı (Unity Hub 3.22.2); gerisinin komut satırından yapılması ve adımların raporlanması istendi.

**Olanlar**
- Unity 6.3 LTS (6000.3.25f1) Hub'ın komut satırıyla indirildi, ama kurulum adımı Windows izin penceresi (UAC) onaylanmadığı için zaman aşımına uğradı. Hiçbir şey kurulmadı; indirme silindi.
- Bu sırada Hub'ın açılış sihirbazı **Unity 6.6 (6000.6.4f1)** ile Web modülünü kurdu. Editor kurulumu yarıda hata verdiği hâlde Hub onu kurulu saydı: `Data/Tools` ve paket yöneticisi sunucusu eksikti, eğitim projesi açılamadı.
- **Onarım:** Hub'ın indirdiği kurulum dosyası doğrulandı (boyut ve Unity Technologies imzası) ve yarım klasörün üstüne yeniden çalıştırıldı; kullanıcı UAC'yi onayladı. Kurulum hatasız bitti; Web modülü bu sırada kalktı.
- **Karar:** iki Editor'a disk yetmediği için kurulu olan Unity 6.6 ile devam ediliyor (LTS değil, "Supported" kolu).
- **Proje:** oyunun Unity projesi depoda `unity/` altında komut satırından oluşturuldu (`Unity.exe -batchmode -quit -createProject`; 77 saniye, hatasız). Lisans Unity Personal, etkin. Web testindeki 41 çizim `unity/Assets/Art/Pieces` altında, `.meta` dosyalarıyla.
- `.gitignore`: Unity'nin ürettiği klasörler (Library, Temp, Logs, UserSettings, Build) depoya girmiyor.
- **Motor Unity içinde doğrulandı.** `node tools/unity/sync-engine.js` motor derlemelerini (`Puzzle.Core`, `Puzzle.Bot`) ve 1000 bölümün tek dosyalık paketini (0,7 MB) projeye koyar; bunlar depodan türetildiği için `unity/Assets/Generated` depoya girmez. Unity içinde 1000 bölümün hepsi okunuyor (`LevelPack`, Unity'nin kendi JSON okuyucusuyla).
- **Çalışma ortamları arası eşitlik:** el yapımı 4 örnek bölüm üçer tohumla rastgele botla sonuna kadar oynatılıyor; Unity'nin içindeki sonuç araçlardaki .NET 10 sonucuyla satır satır aynı (kazanma durumu, hamle sayısı, her hamleden sonraki tahta özeti). Sabitlenen dosya `tests/fixtures/engine-trace.txt`; .NET tarafını `EngineTraceTests`, Unity tarafını `EngineSmokeTest` (komut satırından `-executeMethod`) denetliyor. Bölümler bir ortamda ayarlanıp başka bir ortamda oynanacağı için bu denetim kalıcı.

- **Android modülleri kuruldu** (Android Build Support, OpenJDK 17, Android SDK platform 34/36/37, NDK r27c). Hub'ın komut satırı her kurulumda yeni bir Windows izin penceresi açıyor ve kimse basmazsa iki dakikada iptal ediyor; o pencereye hiçbir program basamaz. Çözüm: kullanıcının ekran izniyle Hub'ın kendi penceresinden kurmak (Installs → Manage modules). Hub'ın daha önce onaylanmış yetkili yardımcısı açık olduğu için yeni izin sorulmadı. Varsayılan işaretli gelen Visual Studio Community seçilmedi; Google'ın Android SDK ve NDK lisans şartları kullanıcının verdiği yetkiyle kabul edildi.
- **İlk Android paketi derlendi:** `Unity.exe -batchmode -quit -projectPath unity -buildTarget Android -executeMethod Puzzle.Client.EditorTools.AndroidBuild.Run` → `unity/Build/Android/puzzle.apk`, 15 MB, 5,4 dakika (IL2CPP, ARM64, dikey, Unity'nin deneme imzasıyla). İçinde henüz oyun yok, boş bir sahne var; amaç derleme hattını uçtan uca görmekti. Geçici kimlik: `com.saturnwave.puzzle` (oyunun adı en sona kaldığı için).
- `AndroidToolchainCheck`: Unity'nin Android'i görüp görmediğini ve hangi SDK, NDK, JDK'yı kullanacağını komut satırından söyler.
- Bilgisayara USB ile bir telefon bağlı (Pixel 9 Pro Fold); USB hata ayıklama izni telefonda henüz onaylanmamış.

**Açık**
- Tahta sahnesi: bölümü seçilen tasarımdaki çerçeveli tahtayla göstermek, dokunarak takas, motor olaylarının animasyonu. Kullanıcının onayı bekleniyor.
- Unity 6.5 ve sonrasında yerleşik çizim hattı (Built-In Render Pipeline) kullanımdan kalkıyor; tahta sahnesine başlamadan proje URP 2B'ye alınmalı.
- Telefona kurulum: kullanıcı telefonda USB hata ayıklamayı onaylayınca `adb install` ile denenecek.
- Disk: 13 GB boş.

## 2026-10-06 · Oturum 14 — Çeşitlilik: tahta başına engel miktarı; APK işine başlangıç

**Kullanıcı kararları**
- "Çeşitlilik önerini onaylıyorum, uygula": tahta başına engel miktarı araması ilk aramada da kullanılacak.
- "Bir yandan da APK yapımına başlanmalı": Unity istemcisi ve Android APK işi, bölüm ayarıyla paralel başlıyor.

**Çeşitlilik**
- **Üretici:** her tahta kendi engel miktarını arar (en çok dört adım, planlanan miktarın üçte biri ile üç katı arası); adaydan adaya taşınan çarpan ve ayrı "ikinci arama" kaldırıldı. Miktar değişince aynı kalan tahta yeniden oynatılmaz (`Candidate.IsSameLevelAs`).
- **Sonuç (51–1000, 950 bölüm):** yalnızca renk toplama 528 → 400; kutulu 158 → 223; zincirli 185 → 272; jöleli 78 → 110; malzeme indirme 70 → 61. Paylar yerinde: 55. basamak %90 / %76 / %56 / %41. Ortalama limitler 25,2 / 29,3 / 31,9 / 36,8. Gevşetilen 9 bölüm ve hedeften sapan 899 aynı. 303 dosya değişti, 647'si bayt bayt aynı.
- **Malzeme bölümleri geri gelmedi.** 55. basamak botu bir malzemeyi 15–20 hamlede indiriyor; üç malzemeli tahta Normal aralığına oturmuyor. Malzeme sayısını ve jöle bandını miktara bağlamak 51–250 örneğinde denendi: 200 bölümde 4 malzeme bölümü kazandırdı, jölede fark yok; tek sıralık jöle bantları ve düşük miktarda kullanılamayan tahtalar getirdiği için geri alındı.
- **İlk oda** dosyaları eski aramayla üretilmiş hâliyle duruyor (kayıtları var); bugünkü kodla yeniden üretilirse değişir (GAME_DESIGN §9).
- **Yayın:** sunucu durduruldu (devam eden deneme yoktu; Admin 30. bölümde), Release derlendi, WMI ile başlatıldı; açık adres yanıt veriyor. `reports/ladder.csv` ve `reports/expert_moves.csv` yeni bölümlerde yenilendi: 55. basamak bağımsız ölçümde %90 / %76 / %55 / %39, novice %87 / %72 / %50 / %35, casual %98 / %91 / %77 / %65. `humans`: 66 kayıt, klasörde olmayan 0.
- Testler: 637 (ikinci aramanın testi, tahta başına aramanın testiyle değişti: 114. bölümün ilk tahtası planlanan kutularla uzun, daha az kutuyla oturuyor).

**APK işine başlangıç**
- Bu bilgisayarda Unity Hub ve Editor kurulu değil; Android SDK ve NDK var; C: sürücüsünde 17 GB boş. Kurulum ve hesap girişi kullanıcıda: Unity 6.3 LTS + Android Build Support (OpenJDK, SDK ve NDK araçları).
- Önerilen ilk APK kapsamı: bölüm haritası, seçilen tasarımdaki çerçeveli tahta, kazandın/kaybettin panelleri; 1000 bölüm pakette; meta, mağaza, booster ve hesap sonraya.
- Çizimler: web testindeki taş, özel taş, engel ve hedef simgeleri Unity için PNG olarak dışa aktarılıyor (`node tools/unity/export-sprites.js`; 41 görsel, 512 px, saydam zemin; kontrol sayfasıyla gözden geçirildi). PNG dosyaları Unity projesi kurulunca `.meta` dosyalarıyla birlikte depoya girecek. Duvar lambası ve çerçeve sayfanın stiliyle çizildiği için ayrı ele alınacak.
- Motor hazır: `Puzzle.Core` ve `Puzzle.Bot` netstandard2.1; animasyon için olay günlüğü (`GameEvent`); bölüm dosyası modeli Unity'nin JSON okuyucusuna uygun.

**Açık**
- Malzeme bölümleri: daha uzun hamle aralığı mı, insan verisini beklemek mi.
- Unity kurulumu (kullanıcı), ardından proje iskeleti, çizimlerin PNG'ye aktarımı, ilk APK.

## 2026-10-06 · Oturum 13 — Onaylanan paylar: 51–1000 ortalama oyuncuya göre yeniden üretildi

**Kullanıcı kararları**
- "Admin hesabı ortalama oyuncu profili gibi düşünülebilir, daha fazla veri gelecektir." Admin'in kayıtları merdivende en iyi 55. basamağa uyuyor (40–60 makul, 65 ve üstü bağdaşmıyor).
- Önerilen ilk deneme payları onaylandı: 51. bölümden itibaren ortalama oyuncu (55. basamak) Kolay %90, Normal %75, Zor %55, Süper Zor %40 kazanır.

**Yapılanlar**
- **Bant başına hamle payı:** `bands[].moveMargin` (yoksa `calibration.moveMargin`). İlk oda 0,25'te kaldı; 51 ve sonrasının bandı 0 yazıyor, çünkü onaylanan paylar limitin kendisinde tutulmalı.
- **Profil:** 51. bölümün bandı `calibrationBot: skill-55`, yeni hedefler, casual koruması yok; 301. bölümdeki sıkılaşma kaldırıldı. Hamle aralıkları, renkler, boyutlar, mekanik takvimi aynı.
- **Üretici, ikinci arama:** ilk üretimde 31 bölüm hamle aralığının dışında kaldı (969. bölüm: 4 kutu, 5 hamle). Neden: engel miktarı adaydan adaya tek çarpanla taşınıyor; malzeme ve jöle bandı tahtaları art arda uzun çıkınca çarpan çöküyor. Düzeltme: hiçbir adayı oturmayan bölümde aynı sekiz tahta tek tek, kendi miktarıyla yeniden denenir. Gevşetilen bölümün limiti de artık yeni tohumlarla denetlenip gerekirse oradan okunur. Öteki bölümler bayt bayt aynı kalır (iki üretim arasında 26 dosya değişti).
- **Yeniden üretim:** 1000 bölüm 41 dakikada; 1000/1000 geçerli; ilk oda 50/50 bayt bayt aynı. 55. basamak botu limitte Kolay %91, Normal %76, Zor %56, Süper Zor %41. Ortalama limitler 30/35/39/44'ten 25/29/32/36'ya indi; hedefler küçüldü. Gevşetilen 9 bölüm, hedeften 7 puandan fazla sapan 1 bölüm (ayrıntı: GAME_DESIGN §13).
- **Bağımsız ölçüm:** `puzzle ladder` yeni bölümlerde yeniden çalıştırıldı (10 dakika; `reports/ladder.csv`). 55. basamak üreticinin görmediği oyunlarda %90 / %76 / %55 / %40; 50. basamak (novice) %88 / %72 / %50 / %36; casual %98 / %91 / %77 / %65. `reports/expert_moves.csv` de yenilendi.
- **Yayın:** sunucu durduruldu (yarım kalmış deneme yoktu), Release derlendi, WMI ile yeniden başlatıldı. Açık adres yanıt veriyor; laboratuvarın bot listesinde artık merdiven botları da var. `humans`: 62 kayıt, klasörde olmayan 0.

**Bulgular**
- **Çeşitlilik düştü.** Zayıf botla engelli tahtalar aralığa daha zor oturuyor; yalnızca renk toplanan bölümler 335'ten 528'e çıktı, malzeme indirilen bölümler 223'ten 70'e, jöleli bölümler 187'den 78'e indi. Çare adayı (ilk aramada da tahta başına miktar) bütün bölümleri değiştirir; karar bekliyor.
- **Tahtalar değişti:** 950 bölümün 406'sı eski tahta stilini korudu. 51 ve sonrasını kimse oynamadığı için kayıt kaybı yok.
- **Yeni insan verisi:** Admin yumuşatılmış ilk odada 10–26. bölümleri oynadı: 17 ilk denemenin 16'sı kazanıldı (tek kayıp 23. bölüm), çoğu limitin yarısına yakın hamleyle. Bütün kayıtlarda insan %61,7, novice bot aynı oyunlarda %62,3.
- `generate --repair` 899. bölümü her seferinde yeniden üretiyor (limit doğrulama oyunlarından okunmuş, oran hedefin 7,5 puan üstünde; sonuç hep aynı dosya). Zararsız; bilinen bir pürüz.

**Açık**
- Çeşitlilik için ilk aramanın değiştirilmesi (yukarıda). 730 (65 hamle) ve 795 (54 hamle) elle gözden geçirilebilir.
- Daha çok oyuncu verisiyle ortalama oyuncunun basamağı yeniden kestirilecek.

## 2026-10-06 · Oturum 12 — Beceri merdiveni: ara botlar ve 1000 bölümün ölçümü

**Kullanıcı gözlemi ve kararı**
- Zorluk bir botun kazanma oranına bağlı, oyuncuların yüzdesine değil; bot çeşitliliği de az. Öneri: novice, casual ve average arasını yüzde 5'lik basamaklarla doldurmak ve **bölümleri yeniden üretmeden** mevcut 1000 bölümü bu botlarla ölçmek.
- Token harcaması ölçülü tutulacak: bir script yetiyorsa yalnızca o çalıştırılır.

**Yapılanlar**
- `skill-30` … `skill-100` basamakları (15 bot) eklendi; dört adlı profil merdivenin 50, 70, 85 ve 100. basamaklarıyla aynı ve değişmedi. Üreticinin profil yükleyicisi basamak adlarını kalibrasyon ve koruma botu olarak kabul ediyor; test panelinin bot listesi de basamakları sunuyor (sunucu bir sonraki derleme ve yeniden başlatmada gösterir).
- Yeni komut `puzzle ladder`: mevcut bölümleri merdivenle ölçer; her bot aynı oyunlarda oynar; çıktı parça parça alınabilir. 633 test (91'i yeni). Kodun kendi kontrolleri geçti; başlatılan bağımsız inceleme turu token tasarrufu için yarıda kesildi.
- 1000 bölüm × 15 bot × 60 oyun, Release derlemesiyle 15 dakikada ölçüldü: `reports/ladder.csv` (15.000 satır). Tablo GAME_DESIGN §7'nin sonunda.
- Sonuç: basamak başına Normal bölümlerde kabaca 6–9 puan kazanma oranı; novice (50) ile casual (70) arası %39 → %72. Zayıf oyuncu için Zor ve Süper Zor bölümleri yalnızca hamle ekleyerek düzeltmek mümkün değil (50. basamak için ortalama 72 hamle gerekirdi; bugün 39).
- İkinci oyuncu (Admin1) yeni bölümlerde 1, 2 ve 3. bölümleri kazandı.

**Sıradaki:** hangi basamağın "tipik oyuncu" sayılacağı ve kademe başına hedef yüzdeler (kullanıcı kararı); ona göre limitlerin, gerekiyorsa Zor kademelerde hedeflerin yeniden yazılması.

---

## 2026-10-05 · Oturum 11 — Kararlar: yüzde 25 yumuşatma, beyaz papatya, tema web testine, doğrudan dünya yayını

**Kullanıcı kararları**
- **Bütün bölümler yüzde 25 yumuşatılsın.** Uygulama tanımı: kalibrasyonun bulduğu hamle limitine yüzde 25 eklenir (yukarı yuvarlanır); tohum havuzu ve rapor yeni limitte ölçülür. Profilde tek bir sayı (`calibration.moveMargin`).
- **Papatya beyaz yapraklı olsun.**
- **Bölümler ve profiller PR'a gönderilsin.**
- **Yayın doğrudan dünya geneli** olacak; sınırlı ülke yayını adımı kaldırıldı.
- **Oyunun adı en sona** bırakıldı.
- **Sunucu güncellenen temayla yeniden başlatılabilir:** seçilen tasarım web testindeki oyuncu sayfasına uygulanacak; sunucuyu yeniden başlatma yetkisi verildi.
- Özgün "Level 47 board" çizimindeki taşlar da değiştirildi (Oturum 9).

**Yapılanlar**
- Sunucu yeniden başlatıldı (yerel saatle 20:10): konsoluna Ctrl+C gönderilerek düzgün durduruldu, kendi penceresinde yeniden açıldı. Günlük: `Public mode: 1000 levels`. Oyuncular artık ilk odanın kolaylaştırılmış bölümlerini görüyor; Admin yeni 10. bölümden (39 yeşil, 21 hamle) devam ediyor. Eski 10. bölümde toplam 23 deneme, hepsi kayıp.
- İlk oda değişiklikleri commit edilip PR'a gönderildi (`bd84183`).
- `tools/public/Stop-PublicServer.ps1` eklendi: sunucunun konsoluna Ctrl+C gönderir, zorla kapatmaz. Aynı yöntem bu yeniden başlatmada elle kullanıldı; betiğin kendisi bir sonraki yeniden başlatmada denenecek.
- Başlatılan işler (arka planda): (1) üreticiye bant başına kalibrasyon botu ve hamle payı, iki geçici profilin `default.json` içine katlanması; (2) beyaz yapraklı papatya ve motorun 7. ve 8. renkleri için iki yeni taş; (3) seçilen tasarımın oyuncu sayfasına uygulanması (canlı sunucuya dokunmadan, ayrı bir kopyada); (4) harita, koleksiyon, mağaza ve ayarlar çizimleri.

- Papatya beyaz yapraklı olarak yeniden çizildi (yapraklar beyaz, göbek altın sarısı, kenar çizgisi sıcak koyu sarı; açık tepside ve krem kartta okunuyor) ve onu gösteren beş çizim yeniden üretildi (kullanıcının "Level 47 board" çizimi dahil). Motorun 7. ve 8. renkleri için iki taş çizildi: camgöbeği elmas ve pembe lale; taş föyüne "sonraki bölümlerde" başlığıyla eklendi. Koruma bandı inceltildi (beyaz papatyanın üstünde göbek görünsün diye).
- Dört ekran daha çizilip tuvale eklendi: müze haritası (kat planı; 1. oda açık, 2. oda kilitli, sonrakiler yalnızca numarayla), koleksiyon (onarılan dört tablo salon duvarında, beşincisi yapboz hâlinde), mağaza (üç altın paketi, iki güçlendirici seti, ödüllü reklam; fiyatlar `[fiyat]`) ve ayarlar. Tuvalde artık on üç çizim var.

**Kesinti (5 Ekim gecesi)**
- 20:10'da yeniden başlatılan sunucu, onu başlatan oturum kullanım sınırına takılıp kapandığında oturumla birlikte öldürüldü (günlükte kapanış satırı yok). Yayın adresi, fark edilip yeniden başlatılana kadar (6 Ekim 00:25) 502 verdi. Kayıt ve hesap kaybı yok: 41 kayıt yerinde.
- Neden: `Start-Process` ile açılan pencere oturumun alt sürecidir. Çözüm: sunucu artık WMI üzerinden (`Win32_Process.Create`) başlatılıyor; oturumdan bağımsız yaşıyor. Her oturum başında 5199 portu ve yayın adresi kontrol ediliyor.
- Aynı kesinti iki arka plan işini de yarıda bıraktı (üretici değişikliğinin doğruluk incelemesi; temanın oyuncu sayfasına uygulanması). İkisi kaldıkları yerden sürdürüldü; tema işi kesildiğinde ekranlar büyük ölçüde hazırdı.

**6 Ekim: yüzde 25 hamle payı yayında**
- **Üretici:** her bant kendi kalibrasyon botunu adlandırıyor (`bands[].calibrationBot`); iki geçici profil `default.json` içine katlandı ve silindi. Yeni alan `calibration.moveMargin` (0,25): bölümün limiti kalibre limitin yüzde 25 fazlası (yukarı yuvarlanır); havuz ve rapor yeni limitte ölçülür; rapora `calibratedMoves`, `moveMargin`, `calibrationBot` sütunları eklendi. Profil yükleyici artık bilinmeyen alanı reddediyor. İki bağımsız inceleme (doğruluk; sözleşme ve testler) 13 bulgu verdi, 9'u düzeltildi; testler 461'den 526'ya çıktı.
- **Kabul:** pay 0 iken 1–60. bölümler eskisiyle bayt bayt aynı; pay 0,25 iken iki çalıştırma aynı dosyaları veriyor; `--repair` aynı profille hiçbir şey üretmiyor.
- **Yeniden üretim:** 1000 bölüm 50'şerlik aralıklarla yaklaşık 50 dakikada üretildi. Tahtalar, hedefler ve kalibre limitler 1000'de 1000 aynı kaldı; yalnızca limit ve havuzlar değişti. Gevşetilen yok, havuzlar en az 8 tohum. Sonuç tabloları GAME_DESIGN §13'te.
- **Ölçüm:** casual bot 51–300'de Normal %51 → %78, Zor %20 → %46. Novice bot ise 51–100'de ortalama %39 (Zor %16, Süper Zor %5): **pay, novice gibi oynayan biri için 51 sonrasındaki duvarı kaldırmıyor.** İlk oda kendi botu için neredeyse kaybedilmez oldu (%98–100).
- **Canlıya alma:** bölümler yerine kondu, 526 test geçti, sunucu `Stop-PublicServer.ps1` ile düzgün durduruldu (betik ilk kez denendi, çalışıyor) ve oturumdan bağımsız yeniden başlatıldı. Takastan hemen önce Admin1 hesabı ilk kez oynadı (1. bölümün novice sürümü, ilk denemede 14/19 hamlede kazandı); o sürüm ve komşuları `levels/archive/before-margin-2026-10-06/` altına alındı, 42 kaydın hepsi karşılaştırılabiliyor.

**6 Ekim: tema oyuncu sayfasında, yayında**
- Seçilen tasarım herkese açık testin oyuncu sayfasına uygulandı: giriş kartı, tele asılı bölüm / hamle / hedef plaketleri, bölümün şekline göre üretilen basamaklı yaldız çerçeve ve tepsi (her boyutta ve delikli tahtalarda), tasarımın taşları ve engelleri (uğur böceği, beyaz papatya, sandıklar, vernik, bant, özel taşlar; 7. ve 8. renk dahil), kazanma ve kaybetme panelleri. Oynanış mantığı değişmedi; yalnızca çalışan şeyler gösteriliyor (booster, coin, can, yapboz parçası yok).
- Çalışma canlı sunucuya dokunmadan ayrı bir kopyada yapıldı: haritalama, yapım, iki bağımsız inceleme (görünüm 8/10, 14 bulgu; davranış 8/10, 11 bulgu), düzeltme ve baştan sona doğrulama (iki hesapla tam yolculuk iki kez; 542 test).
- **Canlıya alma:** yeni dosyalar eski sayfanın yanına kopyalandı, `index.html` tek bir taşımayla değiştirildi; sunucu yeniden derlenmedi ve durdurulmadı. Yayın adresindeki giriş sayfası yeni görünümde.
- **Yazı tipleri:** sunucunun bugünkü güvenlik politikası Google Fonts'u engelliyor; sayfa şimdilik sistemdeki kalın yazı tipleriyle çalışıyor. Politika değişikliği koda eklendi ama sunucu yeniden derlenene kadar etkisiz. İki yol var: sunucuyu yeniden derleyip başlatmak (oyuncuların tarayıcıları her ziyarette Google'a bağlanır) ya da üç yazı tipini sunucuya koymak (dosyaların indirilmesi gerekir). Karar kullanıcıda.
- **Sınırlar:** yalnızca bu bilgisayardaki başsız Chrome'da denendi; gerçek telefon, Safari ve ekran okuyucu denenmedi. Taş çizimlerinin kaynakları hâlâ `out/design` altında (git'e girmiyor); temiz bir kopyada `pieces.svg` yeniden üretilemez.

- **Yazı tipi kararı (kullanıcı, 6 Ekim):** "yazı tipi Design'daki gibi olsun". Sunucu durduruldu, Release olarak yeniden derlendi (0 uyarı) ve yeniden başlatıldı; yeni güvenlik politikası Google Fonts'un stil dosyasına ve yazı tipi dosyalarına izin veriyor. Yayın adresindeki giriş sayfası artık Gabarito, Paytone One ve Young Serif ile açılıyor (ekran görüntüsüyle doğrulandı). Bedeli: her oyuncunun tarayıcısı her ziyarette Google'a bağlanıyor. Yazı tiplerini sunucuya koymak (dosyaları indirerek) hâlâ açık bir seçenek.

**Sıradaki:** 51 sonrası için novice kalibrasyonu kararı (insan verisi geldikçe); çizim kaynaklarının depoya alınması; gerçek telefonda deneme.

---

## 2026-10-04 · Oturum 10 — İlk insan verisi: ilk bölümler zor

**Geri bildirim**
- Herkese açık test yayında (Tailscale Funnel, 8443). İlk oyuncu (Admin hesabı, 12 deneme, 1–7. bölümler) ilk bölümlerin zor olduğunu söyledi. Sonuç: 12 denemede 6 galibiyet; 1. bölümde 3'te 1, 7. bölümde 4'te 0.
- Yayın kontrol edildi: adres giriş ekranını gösteriyor; test paneli ve kayıt uçları anahtarsız isteği reddediyor.

**Bulgular** (ayrıntı `out/onboarding/` altında; depoya girmiyor)
- **Limitleri yanlış oyuncu modeli belirliyor.** Limitler average bota göre ayarlı; oyuncu novice botun hızında ya da biraz altında (hamle başına 6,57 hedef taşı; novice 7,34; average 10,97). Bugünkü bölümlerde novice bot 1–5'te %79, 6–10'da %23, 9. bölümde %5, 10. bölümde %2 kazanıyor.
- **Zorluk döngüsü 1. bölümden başlıyor:** 9. bölüm Zor, 10. bölüm Süper Zor.
- **Oyuncu özel taşı ihmal etmiyor.** Kendi eşleşmeleriyle botlardan iki kat özel taş kuruyor ve hamlelerinin %78'inde hedef rengi eşliyor. Geride kalma nedeni: tahtadaki zincirleme düşüşlerden neredeyse hiç taş almıyor (hamle başına 0,46; novice 1,55) ve özel taşları tek tek patlatıyor.
- **Tohum şansı zayıf oyuncuya dengesiz:** bugünkü 2. bölümün ilk tohumunda novice %45 kazanıyor (havuz içi fark %45–88).
- **Sektör:** başarılı oyunlarda ilk ~20 bölüm neredeyse kaybedilmiyor; ilk "Zor" bölüm 24–54, ilk "Süper Zor" 28–65 arasında (topluluk verisi; hiçbir stüdyo oran yayımlamadı).
- Veri sınırı: tek kişi, 12 deneme. Yönü destekliyor, hiçbir sayının büyüklüğünü desteklemiyor.

**Öneri (kullanıcı onayı bekliyor; şartname `out/onboarding/spec.md`)**
- Birinci odada (1–50) limitleri zayıf oyuncu modeli belirlesin: 1–3 random bot %90, 4–10 novice %95, 11–20 novice %90, 20'ye kadar hepsi Kolay. 21'den sonra döngü yumuşak hedeflerle döner (ilk Zor 25, ilk Süper Zor 30). 51 ve sonrası aynen kalır.
- Aşama 0: kod değişikliği yok, iki geçici profil dosyası ve bir sunucu yeniden başlatması. Aşama 1: `bands[].calibrationBot` alanı; iki dosya `default.json` içine katlanır.
- İpucu ve öğretici bu adımda yok: bölümlerle birlikte değişirse hangisinin işe yaradığı anlaşılamaz.

**Prova** (canlı bölümlere dokunmadan, `out/onboarding/rehearsal`; bölüm ve bot başına 600 oyun)

| Bölüm | Ort. limit | random | novice | casual | average |
|---|---|---|---|---|---|
| 1–3 | 16,7 | %90 | %100 | %100 | %100 |
| 4–10 | 19,6 | %49 | %95 | %100 | %100 |
| 11–20 | 17,6 | %29 | %92 | %99 | %100 |
| 21–30 | 23,8 | %23 | %77 | %95 | %100 |
| 31–40 | 22,9 | %11 | %60 | %84 | %96 |
| 41–50 | 22,9 | %9 | %57 | %82 | %96 |

- Şartnamedeki 21 kabul ölçütünün tamamı tuttu; gevşetilen bölüm yok; 51–55 yeni 46–50'nin arkasında bayt bayt aynı üretildi.
- Novice botun ilk 50 bölüm için beklenen deneme sayısı yaklaşık 418'den 72'ye iniyor.

**Süreç notu**
- İş akışındaki ayrı çalışma kopyaları dalın ucundan değil `main`'den (Faz 1) açıldı; ölçüm ajanları dalın ucunu kopyaya çıkarıp öyle çalıştı. Sonuçlar ana kopyadaki derlenmiş CLI ile yapılan provayla doğrulandı.

**2026-10-05: yeni veri, onay ve takas**
- Aynı oyuncu (kullanıcının deyişiyle usta bir oyuncu) devam etti: toplam 37 deneme. 7. bölümü 7. denemede, 8'i 2. denemede, 9'u (Zor) ilk denemede geçti; **10. bölümü (Süper Zor, 144 yeşil / 31 hamle) 19 kez üst üste kaybetti** (en iyisi 126/144). Geri bildirim: "çok zor, özellikle ilk bölümler".
- Karşılaştırılabilir 26 denemede insan %35, novice bot %34, casual %58, average %80, expert %82. **Deneyimli bir insan novice botla aynı oranda kazanıyor;** bot adları insan becerisini göstermiyor.
- Kullanıcı planı onayladı ("plan yapıp başlanılabilir"). İlk oda diskte takas edildi:
  - Eski 50 dosya `levels/archive/before-onboarding-2026-10-05/` altına alındı; `data/` ve `recordings/` yedeklendi (`out/backups/`).
  - `profiles/onboarding-1-3.json` ve `profiles/onboarding-4-50.json` eklendi; 1–50 yerinde yeniden üretildi. 50 dosyanın 50'si provayla bayt bayt aynı; 1000/1000 geçerli; 461 test geçiyor.
  - Takastan sonra `puzzle humans` 37 kaydın hepsini okuyor (arşiv sayesinde).
- **Sunucu yeniden başlatılana kadar oyuncular eski bölümleri görür** (sunucu bölümleri açılışta okur). Yeniden başlatma kullanıcının adımı.
- Belgeler: GAME_DESIGN §8 madde 7, §9, §13, §18, §20 ve yeni §22 (görsel tasarım); PUBLIC_TEST §7 (test sürerken bölüm değiştirme).

**Açık kalanlar**
- 51 ve sonrası average bota göre ayarlı kaldı (novice 51–60'ta %19): duvar oraya taşındı. İnsan verisi average botun insanüstü olduğunu gösterdiği için 51+ kalibrasyonu yeniden ele alınmalı.
- Kalıcı çözüm (profilde bant başına kalibrasyon botu; iki geçici dosyanın `default.json` içine katlanması) kullanıcı onayını bekliyor.
- `puzzle humans`, havuzun ötesindeki tekrar denemelerini "hand-picked seeds" diye sayıyor; etiket yanıltıcı.

---

## 2026-10-04 · Oturum 9 — Claude Design ile ilk ekran denemeleri

**Kullanıcı kararı**
- Görseller en sonda yapılacak; ama Claude Design ile bir çizim denemesi şimdi yapıldı ("deneyebiliriz").

**Değişiklikler**
- Tasarım tuvali: "Ressam ve Müze — oyun ekranları" (özel bağlantı, depoda değil). Dört çizim: oyun ekranı, müze odası, yapboz ekranı, taş ve engel seti.
- Kullanılan tablolar kamu malı, Art Institute of Chicago açık erişim görselleri: Van Gogh *Yatak Odası*, Seurat *Grande Jatte*, Monet *Nilüferler*, Caillebotte *Paris Sokağı; Yağmurlu Gün*.
- Yapboz çizimi, önceki denemedeki 3-4-3 kesim algoritmasının ürettiği gerçek parça yollarını kullanıyor.

**Denemedeki varsayımlar (karar değil, öneri)**
- Renk düzeni: gece mavisi zemin, pirinç çerçeve vurgusu. Yazı tipleri Fredoka (başlık) ve Figtree (metin).
- Tema adları: roket → fırça darbesi, bomba → boya sıçraması, pervane → tablodan uçan kuş, renk bombası → gökkuşağı paleti, kutu → nakliye sandığı, jöle → eski vernik, zincir → koruma bandı, indirilecek eşya → çerçeve köşesi. Motordaki adlar değişmez.
- Her boya renginin kendi şekli var (renk körlüğünde de ayırt edilsin diye); test arayüzündeki kuralla aynı.
- Oda adı, coin ve can sayıları, booster adları yer tutucudur.

**Kullanıcı tepkisi ve ikinci tur**
- İlk çizimler reddedildi ("oyun ekranları çok kötü"): düz geometrik şekiller, kutulu hücreler, tel kafes gibi bir HUD. Kullanıcı kalite çıtası olarak ticari bir match-3 oyununun ekran görüntüsünü verdi.
- Referanstan yalnızca işçilik düzeyi alındı (hacimli, parlak taşlar; çerçeveli tahta; sahne arka planı; kabartmalı paneller). Karakteri, taş motifleri ve panel düzeni kopyalanmadı; başka bir şirketin tasarımı.
- Oyun ekranı üç ayrı sanat yönünde yeniden çizildi; her yön çizildi, ayrı bir gözle eleştirildi (ilk puanlar 6/10; 16–19 bulgu), düzeltildi. Bu kez her adımda görüntüye bakıldı.
  - **A · Atölye nesneleri:** damla, kavanoz, ayçiçeği, tüp, bere, kalem; yaldızlı çerçeve, koyu yeşil galeri duvarı.
  - **B · Boya damlaları:** emaye boya topakları; ahşap çerçeve, gün ışıklı atölye.
  - **C · Pigment kristalleri:** fasetli kristaller; gece mavisi müze, altın çerçeve.
- Çizimlerin kaynağı (SVG taşlar, üreteç betikleri, görüntüler) `out/design/art/` altında; depoya girmiyor.
- **Bilinen eksik:** çizimlerdeki tahta 9×9; oyunun tahtası 10×10. Seçilen yön 10 sütunla yeniden kurulacak (hücre yaklaşık 36 px).

**Yön seçimi (2026-10-05)**
- Kullanıcı başka bir oturumda ayrı bir tasarım tuvali çizdirdi ("Design": ana ekran ve 47. bölüm tahtası) ve onu bu oturumdaki üç yönün hepsinden daha iyi buldu. **Sanat yönü artık o tuval;** A, B ve C rafa kalktı (silinmedi).
- O tasarımın farkı: her renk için ayrı ayrıntılı bir nesne yerine, tek bir ışık tarifini paylaşan sade ve iri şekiller (girdaplı disk, damla, yuvarlak kare, çiçek, koni, hilal); üst üste dizilmiş paneller yerine kurulmuş bir sahne (tele asılı plaketler, şekilli tahta, ahşap booster rafı); az renkli arayüz (camgöbeği duvar, yaldız, krem); onarılan tablonun çevresinde kurulmuş bir ana ekran.
- Yerel kopyası `out/design/chosen/` altında (iki çizim, tablolar, görüntüler).
- **Oyuna göre eksikleri:** sandık, koruma bandı, indirilecek eşya ve renk bombası çizimi yok; tahta 9 sütun (oyunda 10); metinler İngilizce; yapboz yerleştirme ve bölüm sonu ekranları yok.

**Seçilen tasarım üzerinde çalışma (2026-10-05, kullanıcı: "beğendiğim dizayn üzerinden çalışmalara başlanılabilir")**
- Kullanıcı isteği: bazı taşların yerine papatya ve uğur böceği. Eşleme: uğur böceği kırmızı diskin, papatya sarı çiçeğin yerine; diğer dört taş aynı.
- Yeni taşlar seçilen tasarımdaki taş tarifiyle çizildi (tek sade siluet, alt kenarda kalınlık, sol üstten ışık, iki kenar parıltısı, tek beyaz yansıma). İki ressam ayrı ayrı çizdi; ayrı bir göz ikisinde de yukarıdan görünen, simetrik olanı seçti (36 pikselde daha sakin ve okunur), 21 bulgu düzeltildi.
- Eksik parçalar aynı dille çizildi: sandık (çatlak / sağlam / demirli), kalın vernik, koruma bandı (tek / çift), çerçeve köşesi, gökkuşağı paleti, büyük sıçrama; hedef simgeleri ve "+5 hamle" simgesi.
- Tuvale iki yeni çizim eklendi; kullanıcının iki çizimine dokunulmadı: oyunun gerçek genişliğinde (10 sütun, 36 px hücre) Türkçe tahta ve "Taşlar ve engeller" föyü. Çerçeve, tahtanın şekline göre aynı katmanlarla yeniden üretiliyor.
- Kaynak: `out/design/next/` (taş parçaları `sym/final`, üreteç `tools/build.js`, biçim denetimi `tools/check.js`).
- Aynı gün beş ekran daha çizilip tuvale eklendi (her biri çizildi, ayrı bir gözle eleştirildi, düzeltildi): Türkçe ana ekran, bölüm başı (hedefler, hamle, üç bölüm öncesi booster, "Oyna"), yapboz yerleştirme (tepsideki parça yerinin gerçek kesimiyle), kazanma (yeni parça, coin, kalan hamlelerin fırçaya dönüşmesi) ve kaybetme ("+5 hamle": coin ya da reklam, "Vazgeç · 1 can gider"). Ekranlar birbirine bağlı; tuvalde oynatılınca aralarında geçilebiliyor. Plan ve yer tutucular GAME_DESIGN §22'de.
- **Disk doldu:** C: sürücüsünde yaklaşık 240 MB boş yer kalmıştı. Bu oturumun bıraktığı tarayıcı profil klasörleri silindi (yaklaşık 170 MB geri geldi); asıl yer açma işi kullanıcıda. Yer açılana kadar yeni büyük iş başlatılmıyor.

- Kullanıcı yeni taşları tuvalde bulamadı (yedi yeni çizimin yalnızca ikisinde taş var; kendi iki çizimine dokunulmamıştı). İsteği üzerine özgün "Level 47 board" çiziminde de kırmızı disk uğur böceğiyle (12 taş), sarı çiçek papatyayla (14 taş) yerinde değiştirildi; çizimin başka hiçbir yeri değişmedi. Özgün hâli `out/design/chosen/Board.dc.html` olarak duruyor; değişikliği `out/design/next/tools/swap-original.js` yapıyor.
- Çizimler artık tuvalin kendi çizim motoruyla da yerelde denenebiliyor (tuvalin `dc-runtime.js` dosyası `support.js` olarak çizimin yanına konup başsız Chrome ile açılıyor); uygulama içi tarayıcı tuvale giriş yapamadığı için doğrulama böyle yapıldı.
- Sıradaki ekranlar çiziliyor: müze haritası, koleksiyon, mağaza, ayarlar.

**Sıradaki:** harita, koleksiyon, mağaza ve ayarlar ekranları; papatyanın rengi; görünümün web testindeki oyuncu sayfasına uygulanıp uygulanmayacağı kararı. Yol haritasındaki sıra değişmedi (4. adım, sonra tema/meta tasarımı).

---

## 2026-10-04 · Oturum 8 — Herkese açık test modu ve 1000 bölümün yeniden üretimi

**Kullanıcı kararları**
- Web test uygulaması internete açılacak; tanıdıklar oynayacak ve gerçek oyuncu verisi APK'dan önce toplanacak. Bu iş 4. adımın önüne alındı.
- Giriş kullanıcı adı ve şifreyle; kayıt ekranı yok. Üç hazır test hesabı, basit ortak şifreyle ("test ortamı, zorlaştırmaya gerek yok").
- Bölümler gerçek oyundaki gibi sırayla oynanır.
- Yayın Tailscale Funnel ile (bilgisayarda zaten kurulu); Cloudflare Tunnel yerine.

**Bulgular**
- **Test paneli asla tünelden açılmamalı:** varsayılan mod test modu olduğu için "herkese açık" ayarını unutmak çözücüyü, botları ve herkesin kayıtlarını şifresiz açardı. Test modu artık bir vekil sunucudan geçen her isteği reddediyor.
- **Tailscale, istemcinin yazdığı `CF-Connecting-IP` başlığını olduğu gibi geçiriyor;** sunucu bu başlığa güvenseydi her istek kendine yeni bir "adres" seçip deneme sınırını aşabilirdi. Artık yalnızca bu makinedeki vekilin yazdığı adrese güveniliyor.
- **HSTS makinenin bütün adını bağlar:** bu bilgisayar aynı ad altında başka servisler de yayınlıyor; Tailscale üzerinden gelen isteklere HSTS gönderilmiyor.
- **Yeniden üretim tahmini doğruladı:** Normal'de average %89 → %81 → %73 (tahmin 88 → 81 → 75); casual koruması 513 bölümde limiti belirledi.
- **Uzun işler arka planda 10 dakikada kesiliyor:** üretim ayrı bir pencerede tamamlandı; yarıda kesilen ilk çalıştırmanın rapor satırları onarım komutuyla geri üretildi, bölüm dosyaları değişmedi.
- **İncelemeler:** herkese açık modda 13 + 3 + 3 bulgu doğrulandı ve kapatıldı (güvenlik ve doğruluk); her tur ayrı bir ajanla doğrulandı.

**Değişiklikler**
- **Web:** `Puzzle:Mode` = Lab / Public. Oyuncu sayfası (giriş, oyun, sonuç, sonraki bölüm), oyuncu API'si, çerez oturumu, PBKDF2 şifre karması, kilitleme ve deneme sınırları, HTTPS yönlendirmesi, önbellek yasağı. Oyuncunun yarım kalan denemesi diske yazılıyor ve yeniden başlatmada hamleler tekrar oynatılarak kuruluyor. Test paneli `/lab` altında, yönetici anahtarıyla.
- **Hesaplar:** `data/players.json` (atomik yazma); hazır test hesapları `src/Puzzle.Web/public-test-players.json`; CLI `players list | add | password | delete`.
- **Betik:** `tools/public/Start-PublicServer.ps1` (modu ayarlar, yönetici anahtarını üretir, sunucuyu başlatır).
- **Bölümler:** `levels/generated` yeni kurallarla yeniden üretildi; `reports/expert_moves.csv` yenilendi.
- **Testler:** 242'den 461'e.
- **Belgeler:** yeni [PUBLIC_TEST.md](PUBLIC_TEST.md); GAME_DESIGN §13, §20, §21.

**Sıradaki:** sunucunun yayına açılması (kullanıcının adımı), ilk insan verisi; ardından 4. adım (booster'lar, bitiş şöleni, düzeltme paketleri).

---

## 2026-10-03 · Oturum 7 — Kalibrasyon kuralları, deneme kaydı ve yeni test arayüzü (3. ve 5. adım)

**Kullanıcı kararları**
- **Casual koruması:** sabit 80/60 yerine bölüm aralığına göre inen alt sınır: Kolay 80/75/70, Normal 60/50/40 (1–50 / 51–300 / 301–1000).
- **Özel taş tanıtımları** (roket 3, bomba 5, pervane 8, renk bombası 12, büyük bomba 15) bir öğretici takvimi; motor kuralı değil. Arayüz fazında yapılacak.
- **Şekilli tahtalar** 21. bölümde başlar; 11. bölüm yalnızca kutuyu öğretir.
- **5. adım paralel yürütüldü** (kullanıcının önerisi): ayrı bir git çalışma kopyasında, 3. adımla aynı anda.
- **Web test uygulaması** oyuncuya sunulmayacak (oyun yalnızca iOS ve Android), ama özenli bir arayüzle geliştirilecek: "test ortamı" diye stilsiz bırakılmayacak.

**Bulgular**
- **Sabit casual eşiği koruma değil, kalibrasyonun kendisi oluyordu:** 80/60, 51. bölümden sonra Kolay ve Normal bölümlerin %95'inden fazlasında limiti belirliyor, Normal'de average'ı her aralıkta %89'a sabitliyordu. Fark bölümlerden değil botlardan geliyor: casual, average'dan 19–32 puan az kazanıyor ve hiçbir bölüm tasarımı bunu kapatmıyor.
- **"+5 hamle" teklifi** mevcut 1000 bölümde average'ın kazanma oranını %65,7'den %80,3'e çıkarıyor.
- **Koruma çoğu zaman "daha kolay hedef" olarak görünüyor:** alt sınır limiti hamle aralığının dışına itince üretici hamle eklemek yerine hedefi küçültüyor.
- **Botları insanın oynadığı tahtada oynatmak şart:** ilk sürümde botlar rastgele tahtalarda oynuyordu; aynı tohumda novice %100 kazanırken rastgele tohumlarda %93 görünüyordu.
- **İncelemeler:** 3. adımda 4 + 5, 5. adımda 9 + 11 bulgu doğrulandı ve düzeltildi; her düzeltmeyi ayrı bir ajan kontrol etti.

**Değişiklikler**
- **Profil:** mekanik takvimi tablo başlangıçlarına hizalı (kutu 11, eşya indirme 51); `casualFloor`, `floorBot` (kalibrasyon botundan zayıf bir oyuncu modeli), `sizes.plainShapesUntil` = 20; Kolay ve Normal hamle üst sınırı 51. bölümden sonra +3.
- **Üretici:** limit = `max(average'ın hedef limiti, casual'ın alt sınır limiti)`; doğrulama iki botu da yeni tohumlarla denetliyor. Rapor sütunları: `winPlus5`, `floor`, `floorWin`, `floorSetLimit`, `floorBot`. `generate --repair` alt sınırı, koruma botu ya da hedefi değişen bölümleri yeniden üretiyor.
- **Core:** `ExtraMovesOffer.Moves` (= 5); `Puzzle.Core/Recording`: deneme kaydı tipi (`AttemptRecord`), mobil oyunla ortak biçim. `LevelFileMapper.ParseGoal` ve `ToGoalFile` public oldu.
- **Bot:** `LevelReport.WinRateWithExtraMoves`.
- **Tooling:** kayıt JSON'u (`AttemptRecordJson`), eşzamanlı yazmaya dayanıklı kayıt dosyası (`AttemptLog`), insan–bot karşılaştırması (`HumanBotComparison`), bölüm parmak izi.
- **CLI:** `botstats` çıktısına `winAtLimitPlus5`; yeni `humans` komutu.
- **Web:** her deneme kaydediliyor (bitince, yarıda bırakılınca, zaman aşımında, kapanışta); deneme numarasını sunucu veriyor. Yeni "Kayıtlar" paneli ve botlarla karşılaştırma grafiği, kayıt göstergesi. Görsel yenileme: her rengin kendi şekli olan taş seti, özel taşlar ve engeller için çizimler, daha akıcı animasyonlar, sekmeli paneller, telefon düzeni.
- **Testler:** 143'ten 242'ye.

**Sıradaki:** herkese açık oyun modu (tanıdıklara oynatıp insan verisi toplamak) kullanıcıyla konuşuluyor; ardından 4. adım (booster'lar, bitiş şöleni, düzeltme paketleri) ve 6. adım (1000 bölümün yeniden üretimi).

---

## 2026-10-03 · Oturum 6 — Özel taş düzeltmesi ve usta bot (yol haritası 2. adım)

**Kullanıcı kararları**
- Kod `beautiful-code` standardına göre yazılıyor; tasarım belgeleri profesyonel PDF olarak da tutuluyor.
- Motora yeni public metot onaylandı: `Game.CreateImagined(ulong seed)`.
- Usta botu örneklemesiz iki hamle aramaya çevirme önerisi onaylandı; ölçüm önerinin yanlış olduğunu gösterince geri alındı (aşağıda).

**Nasıl çalışıldı:** dört iş akışı, toplam 22 ajan.
1. Okuma, teşhis ve tasarım: üç okuyucu, bir ölçüm ajanı, üç bağımsız tasarım, iki değerlendirici.
2. Uygulama ve ölçüm: motor, bot, ölçüm.
3. Örneklemesiz iki hamle denemesi ve ölçümü.
4. Bağımsız inceleme (dört boyut, her bulguyu çürütmeye çalışan doğrulayıcılar) ve düzeltmeler.

**Bulgular**
- **Biriktirme gerçek ama dar:** 0,5 cezasıyla expert özel taşlarının %19'unu kullanmadan bitiriyordu; zarar büyük "renk topla" hedefli bölümlerdeydi. 0,25 iki profil için de en iyi değer (1000 bölüm × 100 oyun): expert limitle kazanma %84,8 → %86,8, average %64,2 → %65,3. 0 değeri eşya indirme bölümlerinde özel taşları erken harcatıyor.
- **Ağaç araması bu bütçede değmiyor:** karar başına ~550 deneme oyunu var; üç tasarımdan değerlendiriciler örneklemeli iki hamle aramayı seçti.
- **Usta, expert'ten çok güçlü:** 1000 bölüm × 30 oyun, aynı tohumlar: limitle kazanma %86,8 → %98,6, ortanca hamle 20,4 → 15,8. Her kademede ve her hedef türünde önde; en büyük kazanç eşya indirme bölümlerinde. Rapor 13,4 dakika.
- **Örneklemesiz iki hamle daha zayıf çıktı:** 40 bölümlük örneklemde biraz daha iyi görünmüştü; 1000 bölümde limitle kazanmada 1,0 puan geride, ortancada 0,9 hamle fazla, rapor 24 dakika. Örneklem temsil edici değildi (356 "sadece renk topla" bölümüne karşı 5). Değişiklik geri alındı; bot kararları artık 1000 bölümde doğrulanıyor.
- **İnceleme:** 18 bulgu; 10'u çürütüldü, 8'i doğrulandı (hepsi küçük önemde) ve düzeltildi. Öne çıkanlar: kalan hamle testi botun kalan hamleye bakmasını yakalamıyordu; web test uygulaması pahalı `usta-<n>` bütçelerine açıktı; `botstats` bir oyun hata verince önceki satırları kaybedebiliyordu.

**Değişiklikler**
- **Bot:** `MoveEvaluator.SpendFactor` 0,25. Yeni `UstaPolicy`, `UstaSettings`, `HalvingPlan`; `BotPolicies`'e `usta` / `usta-<n>` (oyuncu modelleri listesi değişmedi). `TryCreate` boş isimde hata fırlatmak yerine `false` döndürüyor.
- **Motor:** `Game.CreateImagined` (olası bir gelecek: bölümün spawn kuralları, botun kendi tohumu, hamle limiti yok); `ITileSource.Reseed`. Gizli taşlı kopyalardan gelecek üretilemez.
- **Korumalar:** üretici ustayı kalibrasyon botu olarak kabul etmiyor ve boş `reportBots` girdisini reddediyor. Web yalnızca `usta`yı kabul ediyor, en fazla 100 simülasyon.
- **CLI:** `botstats` bütün çekirdekleri kullanıyor (çıktı birebir aynı, hata olursa önceki satırlar korunuyor); `bench`'e usta satırı.
- **Testler:** 98'den 139'a. Kritik testler kasıtlı hata eklenerek sınandı (gerçek rastgeleliği sızdırmak, kalan hamleye bakmak, hamle limitini geri getirmek).
- **Belgeler:** GAME_DESIGN §7, §13, §15, §21.

**Sıradaki:** GAME_DESIGN §21, 3. adım — casual koruması, limit+5 sütunu, mekanik takviminin tablo başlangıçlarına hizalanması.

---

## 2026-10-02 · Oturum 5 — Expert raporu, tema ve yol haritası kararları

**Kullanıcı kararları** (soru-cevap oturumu; ayrıntı [GAME_DESIGN.md](GAME_DESIGN.md) §18–§21)
- **Tema:** genç bir ressam hasar görmüş müzeyi onarıyor; tablolar yapboz parçaları olarak toplanıyor. Fikir kullanıcının.
- **Yapı:** 1 tablo = 10 bölüm, 1 müze odası = 50 bölüm, 1000 bölüm = 100 tablo = 20 oda. Odalar en sevilen akımlarla başlar; her odanın son tablosu o akımın en ünlü eseri.
- **Tablolar:** telifsiz ünlü tablolar; bazılarını biz çizeceğiz (coinle alınan yan koleksiyon).
- **Yapboz:** gerçek mini yapboz, döndürme yok. 10 parçada 3-4-3 dizilimi (prototipte 5×2 ile karşılaştırıldı). Gerçek parayla rastgele paket yok.
- **Müze:** hafif; tablo asılır, oda adım adım onarılır.
- **Hedef oyuncu:** 30 yaş üstü rahat oyuncu, aileye uygun içerik.
- **İş modeli:** ücretsiz oyun + uygulama içi satın alma + isteğe bağlı ödüllü reklam. 5 can + sınırsız can ödülleri, "+5 hamle" teklifi, 7 booster, bitiş şöleni.
- **Kalibrasyon:** average bot + casual koruması (Kolay ≥ %80, Normal ≥ %60). Raporlara limit+5 sütunu.
- **Botlar:** özel taş biriktirme sorunu şimdi düzeltilecek; adil MCTS "usta" botu ayrı bir ölçüm botu olarak yazılacak.
- **Mekanik takvimi:** tablo başlangıçlarına hizalandı (kutu 11, malzeme 51, renk bombası tanıtımı 12).
- **Veri ve yayın:** web test uygulaması oyunları kaydedecek; Play Store'dan önce APK testi; hazır analiz servisi; sınırlı ülke yayını, sonra dünya. Yayında 1000 bölüm. Bölümlere sunucudan düzeltme paketi inebilecek.
- **Sıra:** motor "hazır" → tema ve meta tasarımı → arayüz ve isim → Unity. Görseller en son. Ekip: kullanıcı ve Claude.
- **Motor "hazır" tanımı:** GAME_DESIGN §21'deki 6 adım. v2 engelleri dahil değil.
- **Çalışma düzeni:** kod `beautiful-code` standardına göre yazılacak; tasarım belgeleri profesyonel PDF olarak da tutulacak.

**Bulgular**
- **Expert botun hamle raporu:** expert 1000 bölümün hepsini 30 denemenin 30'unda bitirdi (üst sınır 150 hamle). Limitle kazanma oranı Kolay'da %96, Süper Zor'da %59. 26 bölümde expert'in medyanı limiti aşıyor; en büyük fark #780 (medyan 44, limit 33).
- **Botlar eğitilmedi:** kurallar ve puanlar elle yazıldı. Expert tek hamle ileriyi görüyor ve özel taş biriktiriyor; average'dan belirgin güçlü değil.
- **Üretici temaya hazırdı:** 10 bölümlük zorluk döngüsü ve 50 bölümlük palet grupları, "1 tablo = 10 bölüm, 1 oda = 50 bölüm" yapısıyla birebir örtüşüyor.
- **Tablo kaynağı:** Art Institute of Chicago'da yaklaşık 2.000 kamu malı tablo var. Aynı müzede telifli eserler de bulunuyor; her görüntü tek tek kontrol edilmeli.
- **Yapboz dizilimi:** 10 parçayı düz ızgarayla bölmenin tek yolu 5×2 ve ince uzun parça veriyor. Sıraları 3, 4 ve 3 parçalı dizmek parçaları kareye yakın tutuyor.
- **Bölüm dosyasında `formatVersion` zaten var.** Düzeltme paketleri için eksik olan, bölüm başına içerik sürümü.

**Değişiklikler**
- **CLI:** `botstats` komutu; bir botun her bölümü kaç hamlede bitirdiğinin dağılımını CSV'ye yazar. `LevelReport.CompletionMoves`.
- **Rapor:** `reports/expert_moves.csv`.
- **Prototip:** yapboz kesimi (JS, atılabilir; repoda değil). Van Gogh'un "Yatak Odası" tablosu, 3-4-3 ve 5×2 dizilimleri.
- **Belgeler:** GAME_DESIGN v0.2 — yeni §18 (tema ve meta), §19 (ürün), §20 (kalibrasyon, veri, yayın), §21 (motor "hazır" tanımı); yol haritası, mekanik takvimi ve açık sorular güncellendi.
- **PDF baskıları:** `tools/docs-pdf/build.py` dört belgeyi `docs/pdf/` altına PDF olarak üretir: kapak, belge künyesi, sayfa numaralı içindekiler, üst ve alt bilgi, PDF yer imleri. Kaynak Markdown dosyaları olarak kalır.
- **Araçlar:** Claude Code'a `mattpocock-skills` eklentisi kuruldu (soru-cevap oturumu bununla yapıldı).

**Sıradaki:** GAME_DESIGN §21, 2. adım — bot düzeltmesi ve usta bot.

---

## 2026-10-01 · Oturum 4 — Faz 2: bölüm üreticisi ve ilk 1000 bölüm

**Kullanıcı kararları**
- Web uygulaması yalnızca hata ayıklama ve test için. Mobil tasarıma motor hazır olduğunda geçilecek.
- **Başlangıç tahtası:** her denemede farklı.
- **Zorluk hedefleri:** şimdilik uygun; farklı kombinasyonlar test edilebilmeli.
- **Hedef oyuncu:** emin değil.
- **Hamle sayısı:** bölüme ve bölüm zorluğuna göre değişebilmeli.
- **Tahta boyutu:** bölüm içinde sabit, ilerledikçe değişebilir.
- **Mekanik takvimi:** şimdilik belirsiz.

**Kararlar**
- **Profil dosyası:** bütün üretim ayarları `profiles/default.json`'da. Bantlar miras alıyor; hamle aralığı, kazanma hedefi ve renk sayısı kademe ve bölüm aralığına göre tanımlanıyor. Farklı kombinasyonlar profil kopyalarıyla üretilip raporlar karşılaştırılacak.
- **Hedef oyuncu:** limitler `average` bota göre kalibre ediliyor; raporda `casual` ve `expert` kazanma oranları da var. Karar ertelendi.
- **Örnek bölüm:** `s02` değişken tahtaya döndürüldü.

**Faz 2 — `Puzzle.Generator`**
- **Planlayıcı:** zorluk testere dişi, kademe, bant, tahta boyutu, bölüm grubuna göre renk paleti, mekanik takvimi ve "kanca" bölümleri.
- **Portföy:** 7 şekil stili (dolu, köşe kesme, elmas, çentik, orta delik, sütun boşluğu, kum saati) × kutu desenleri (çift, sütun, küme, dağınık) × jöle desenleri (alt bant, üst bant, merkez, çerçeve, dama, tam) × zincir × malzeme. Hepsi sağ-sol simetrik.
- **Beslenebilirlik analizi:** kutu ve zincir hiç kırılmasa bile her hücreye taş ulaşabilmeli; kapanan yerleşim reddediliyor.
- **Kalibrasyon:**
  - Ölçeklenebilir hedefler, limit aralığa oturana kadar ayarlanıyor.
  - Engel yoğunluğu adaydan adaya uyarlanıyor.
  - Limit, hiç kullanılmamış seed'lerle doğrulanıyor.
  - Hiçbir aday aralığa oturmazsa en yakını `relaxed` olarak tutuluyor.
- **Eşit zorluk havuzu:** seed'ler artık bölümün doğrulanmış kazanma oranını hedefliyor (`--target`). Medyan seed'i hedeflemek zor bölümlerde yanlış sonuç veriyordu: #310'da kazanma %26, ortanca seed'ler %0–8.
- **Rapor:** `levels/generated/report.csv` ve kademe özeti.
- **CLI ve web:** `generate` komutu (`--quick`, `--overwrite`, `--repair`); web laboratuvarında bölüm arama kutusu.
- **Testler:** 61'den 68'e.

**İlk 1000 bölüm — sonuçlar**

Üretim 61 dakika, onarım 41 dakika sürdü. Hiç başarısız ya da gevşetilmiş bölüm yok. Sorunu düzeltilmiş olarak 257 bölüm yeniden üretildi, 743'ü aynen korundu.

| Kademe | Bölüm | Ort. hamle | Hedef | Average | Casual | Expert | Near-miss |
|---|---|---|---|---|---|---|---|
| Kolay | 204 | 22,6 | %87 | %87 | %67 | %96 | %10 |
| Normal | 497 | 26,5 | %68 | %69 | %39 | %90 | %23 |
| Zor | 199 | 31,1 | %43 | %44 | %18 | %74 | %35 |
| Süper Zor | 100 | 35,5 | %28 | %29 | %10 | %57 | %41 |

- **Onarım:** ilk tam üretimde 231 bölümün seed havuzu 8'in altındaydı (50'si 4 veya altı), 29 bölüm hedeften 7 puandan fazla sapıyordu.
  - Havuz en az 8 seed olacak şekilde garantiye alındı.
  - Aralığa sığmayan doğrulama artık adayı reddediyor.
  - `--repair` yalnızca sorunlu bölümleri yeniden üretti. Sonuç: sapan bölüm 29'dan 2'ye indi, en küçük havuz 8.
- **Determinizm doğrulandı:** sağlam bir bölüm yeniden üretilince dosyası bayt bayt aynı çıktı.
- **Bulgu: expert bot average'dan güçlü değil** (#30'da 300 oyun: expert %45, average %48,7; expert'in kötü oyunları daha kötü). Muhtemel sebep, özel taş harcamayı cezalandıran puanlama yüzünden özel taşları biriktirmesi. Rapordaki expert sütunu şimdilik bilgi amaçlı. Gerçek "usta oyuncu" sınırı için geleceği bilmeyen güçlü ajan gerekiyor (plan).
- **Hedef oyuncu sorusuna veri:** Normal bir bölümü average oyuncu %69, casual oyuncu %39 kazanıyor.

---

## 2026-10-01 · Oturum 3 — Eşit zorlukta tekrar denemeler, optimum kanıtı, CI, LAN sunucusu

**Kullanıcı kararları**
- **Tekrar deneme:** taşlar farklı desende gelir, ama algoritmik matematik denemeden öncekiyle aynı kalır.
- **Başlangıç tahtası:** sabit de olabilir değişken de; zorluk aynı olmalı. Kesin karar web laboratuvarında denendikten sonra verilecek.
- GitHub Actions eklensin.
- **NP-zor itirazı:** "Kural belli olduğu için en iyi yolu bulmak zor olmaz; zor olan kuralı tersine mühendislikle çözmek."
- Aynı Wi-Fi'deki her cihazdan erişilebilen bir sunucu başlatılsın.

**Kararlar**
- **Eşit zorluk havuzu:** her bölüm, tipik zorluğa (limitteki kazanma oranı ±5 puan) yakın 16 seed taşıyor. Deneme k havuzdaki (k mod 16). seed'i kullanıyor.
- **Ayrı seed'ler:** tahta seed'i ve oyun seed'i ayrıldı (`AttemptSeeds`). Böylece "sabit tahta, değişen taşlar" mümkün.
- **DDA yok:** denemeler arasında gizli kural değişikliği yapılmıyor (`AssistPercent` = 0).
- **Örnek bölümler:** zor örnekler (s03, s04) 6 renge çekildi. Dördü de havuz aldı; s02 "sabit tahta" örneği.

**Bulgular**
- **Rastgele seed'ler eşit değil:**
  - Jöle bölümünde (6 renk) seed başına medyan 8 ile 29 hamle arası; limitteki kazanma oranı %55 ile %100 arası.
  - Havuz bunu %83–91'e indiriyor.
  - Seçim ölçüsü medyan hamleden limitteki kazanma oranına çevrildi: aynı medyana sahip iki seed'in "kuyrukları" farklı olabiliyor.
- **Optimum kanıtı:** tam arama (`ExhaustiveSearch`) bütün kısa dizileri denedi.
  - Seed 2 ve 3: beam 64'ün yolu gerçekten en iyisi (kanıtlandı).
  - Seed 0 ve 5: gelecek taşları bilen beam 64 beş hamle buldu; tam arama dört hamlelik daha kısa bir kazanç buldu.
  - Pozisyon başına ~37–40 geçerli hamle var. 4 hamle derinlik ~1 milyon pozisyon (1,5 sn); 6–7 hamle derinlik saatler veya günler sürüyor.
  - Özet: kuralı izlemek kolay, iyi yol bulmak kolay, en iyiyi kanıtlamak zor.
- **Güvenlik duvarı:** Windows'ta .NET Host için gelen bağlantıları engelleyen kurallar ve "Public" Wi-Fi profili var. Diğer cihazların bağlanması için kullanıcının bunları kendisinin açması gerekiyor (README'de komutlar).

**Değişiklikler**
- **Core:** `AttemptSeeds`, `Game.Start(level, AttemptSeeds)`, `StartBoard` (Varied / Fixed), `AttemptPolicy`, bölüm dosyasında `attempts`.
- **Bot:**
  - `SeedPoolCalibrator`, `ExhaustiveSearch`.
  - Çözücü dallanma faktörünü raporluyor.
  - `SimulationRunner` artık `AttemptSeeds` alıyor.
- **CLI:** `seeds` komutu (havuzu ölçer ve `--write` ile yazar), `solve --prove`.
- **Web:**
  - Yeni kontroller: "Yeni deneme" (havuz), "Aynı seed" (birebir tekrar), "Başlangıç tahtası" seçici.
  - Statik dosyalar `no-cache` ile sunuluyor.
  - Telefonda üst çubuk düzeltildi.
  - LAN için `web-lan` başlatma ayarı eklendi (derlenmiş kopya, 0.0.0.0:5180).
- **CI:** `.github/workflows/ci.yml` (build, test, bölüm doğrulama).
- **Testler:** 55'ten 61'e.

---

## 2026-10-01 · Oturum 2 — İki bomba, renk paleti, spawn yönetmeni, çözücü, web laboratuvarı

**Kullanıcı istekleri**
- "Neden iki farklı bomba olmasın?"
- Özel taşa dokunarak patlatma kalsın.
- Bölümler ilerledikçe yeni renkler, yeni engeller, yeni özel taşlar ve temalar gelsin.
- Repo bağlansın: https://github.com/SaturnWave/Puzzle
- Şimdilik basit bir web arayüzünden test edebilsin.
- Oyunda paralel çalışan algoritmalar olsun. Gözlem: "doğru hamlelerle geçilen bir yol var, yanlış hamle şansı düşürüyor, gelen taşlar seni bir duruma itiyor" (DFS benzeri). Bunun üzerine araştırma yapılsın.
- Bütün gelişmeler bir yere not edilsin (bu dosya).

**Kararlar**
- **İki bomba.** L/T/+ (tam 5 taş) → Bomba 3x3. 6+ taşlı kesişim → Büyük Bomba 5x5. İki bomba birleşince yarıçaplar toplanıyor (5x5 / 7x7 / 9x9). Roket + bomba 3 sıra/sütun, roket + büyük bomba 5 sıra/sütun temizliyor.
- **Renkler.** 8 renk var (camgöbeği ve pembe eklendi). Her bölüm 3–8 renklik kendi paletini seçiyor. İlerleme planı: 4 → 5 → 6 → 7 → 8 renk.
- **Spawn yönetmeni ("karma algoritma").** Her yeni taşı dört strateji birlikte belirliyor: denge (dueness), hedef eğilimi, kaskad kontrolü, DDA yardımı.
- **Etik korkuluk.** Oyun içi DDA yardımı yalnızca 0..100, yani sadece kolaylaştırıyor. Zorlaştırmak tasarım zamanı kararı.
- **Çözücü.** Mükemmel bilgili beam search eklendi: oyunun birebir replikası üzerinde gelecek taşları bilerek oynuyor ve "her seferinde doğru hamle" yolunu buluyor.
- **Retry politikası önerisi.** Her denemede yeni refill seed'i. Karar kullanıcıda (GAME_DESIGN §14).
- **Web laboratuvarı.** Gerçek motor sunucuda (ASP.NET Core) çalışıyor, tarayıcı sadece event'leri canlandırıyor. GitHub Pages private repoda ücretli plan istediği için yerel sunucu seçildi.

**Bulgular**
- **Çözücü.** Jöle bölümünde (6 renk) gelecek taşları bilen oyun 7,7 hamle, insan benzeri bot 18,2 hamle, rastgele bot ~90 hamle. Kullanıcının gözlemi sayılarla doğrulandı: doğru hamle yolu var, yanlış hamleler ihtiyacı 1,6–2,4 katına çıkarıyor.
- **Kaskad kontrolü.** Kaskadları ~%25 azaltıyor ama hamle ihtiyacını yalnızca ~%5–8 artırıyor. Ana zorluk kolu hâlâ renk sayısı.
- **Gizli durum hatası.** Yerçekimindeki çapraz kayma yön sırası hamleler arasında taşınan gizli bir durumdu ve replika kopyalarına geçmiyordu; çözücünün bulduğu yol tekrar oynatılınca tutmuyordu. Düzeltme: yerçekimi artık sadece tahtanın durumuna bağlı. Test, replikayı oyun ortasında oluşturacak şekilde sıkılaştırıldı.
- **Kapalı küme testleri işe yaradı.** Büyük Bomba eklenince hasar kodundaki "bilinmeyen tür" kolu eksik switch'i hemen yakaladı.
- **Araştırma** ([docs/research/spawn-control-and-solvability.md](research/spawn-control-and-solvability.md)):
  - Ticari oyunlarda retry'da aynı refill'in kullanıldığına dair birincil kaynak yok.
  - Belgelenmiş olan şu: her denemede hangi seed'in verileceğinin bir zorluk kolu olarak seçilmesi (EA) ve beceriye göre taş torbası (Zynga patentleri).
  - Taşlar bilinse bile mükemmel oyunu bulmak NP-zor.
  - AB 30 Eylül 2026'da King ve Playrix'in satıcısı dahil 9 şirket hakkında koordineli işlem başlattı.

**Değişiklikler**
- **Core**
  - `TileKind.BigBomb`, `RuleSet.BombRadius` (1) ve `BigBombRadius` (2); bomba kombolarında yarıçap toplanıyor.
  - `EffectKind.Cross` artık yarıçaplı; eski `WideCross` kaldırıldı.
  - `GemColor.Cyan` ve `Pink`; `SpawnRules.Palette`, `GoalBiasPercent`, `CascadeBiasPercent`.
  - `SpawnDirector` ve strateji sınıfları; `GameOptions.AssistPercent`.
  - `Game.CreateReplica()` (mükemmel bilgili kopya).
  - Aktive edilen özel taşlar için de `TileDestroyed` event'i.
- **Bot:** `BeamSearchSolver`, `BotPolicies`, `BatchSimulator`, `MoveEvaluator.ValueOf`.
- **CLI:** `solve` komutu.
- **Yeni projeler:**
  - `Puzzle.Tooling`: bölüm JSON'u ve bölüm kütüphanesi.
  - `Puzzle.Web`: test laboratuvarı.
- **Testler:** 44'ten 55'e. Kapsanan konular: iki bomba, palet, kaskad eğilimi, replika determinizmi, çözücü yolunun tekrar oynatılması.

---

## 2026-10-01 · Oturum 1 — Faz 1: çekirdek motor ve test botu

**Kararlar**
- **Platform ve motor:** Unity + C#, swap match-3, iOS + Android.
- **Çekirdek:** motordan bağımsız `netstandard2.1` / C# 9 kütüphanesi; bot, üretici ve oyun aynı kodu çalıştırıyor.
- **Tahta:** büyük ve değişken boyutlu (10x10, 10x12…), boşluklar geçirgen.
- **Özel taşlar:** renksiz (Royal Match tarzı) ve dokunarak patlatılabilir.

**Bulgular**
- 10x10 tahtada 5 renk çok cömert: hamle başına 2 kaskad. Standart 6 renk olmalı.
- Kutu sırasının altındaki hücreler kutu kırılana kadar boş kalabiliyor. Bu bir üretici kısıtı.
- Hız: motor 10–16 bin hamle/sn; 12 çekirdekte bir bölümün kalibrasyonu ~1 sn.

**Değişiklikler**
- Puzzle.Core, Puzzle.Bot, Puzzle.Cli.
- 44 test.
- 4 örnek bölüm.
- docs/GAME_DESIGN.md ve research.md.
