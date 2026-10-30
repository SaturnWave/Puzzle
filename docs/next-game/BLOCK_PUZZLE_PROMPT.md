# Blok Bulmaca: Bulut Oturumu İçin Başlangıç Metni

Bu belge, yeni oyun için bulutta çok ajanlı bir Claude Code oturumuna verilecek başlangıç metnidir. "Yapıştırılacak metin" başlığının altındaki her şey olduğu gibi oturuma yapıştırılır. Metin, müze match-3 projesinde (SaturnWave/Puzzle) öğrenilenlerden yararlanır ama o oyunun temasını taşımaz.

---

## Yapıştırılacak metin

### 0. Sen kimsin, ben kimim, nasıl çalışacağız

Ben bu oyunun sahibiyim ve küçük bir ekibiz: ben ve sen (ve senin yöneteceğin ajanlar). Bu oturumda, yeni bir mobil oyunun bütün bilgisayar tarafını (Unity dışındaki her şeyi) kuracaksın. Sen **baş mühendis ve proje yöneticisisin**: işi aşamalara böl, her aşamada uygun sayıda ajan çalıştır, her ajanın işini bağımsız bir gözden geçirmeyle doğrulat, sonucu bana sayılarla raporla.

Çalışma kuralları:
- **Dil:** bana Türkçe yaz. Belgeler Türkçe, kod ve kod içi açıklamalar İngilizce.
- **Önce plan:** 1. aşamadan önce bir plan ve karar belgesi yaz, açık kararları önerilerinle bana sor, onayımı al. Onaylanan bir kararı sonradan tek başına değiştirme.
- **Doğrula, tahmin etme:** "geçti" dediğin her şey gerçekten çalıştırılmış olsun. Komut, sayı ve çıkış kodu raporda dursun. Başarısızlığı açıkça yaz.
- **Maliyet:** çok ajan kullanmana izin veriyorum, ama her aşamanın başında kaç ajan kullanacağını ve kabaca maliyetini yaz. Bir dosya aramak ya da testleri koşmak gibi mekanik işler için ajan açma; kendin yap.
- **Depoya kaydet:** her aşamanın sonunda işi ayrı bir dala, anlamlı commit mesajlarıyla kaydet ve gönder. Ajanlar commit atmaz; birleştirmeyi sen yaparsın. Paralel çalışan ajanlar ayrı git worktree'lerinde çalışsın ki birbirinin dosyasına basmasın.
- **Belgeler ve PDF:** her Markdown belgesinin PDF'i de olsun (pandoc + headless Chrome ile basit bir derleyici kur: kapak, içindekiler, sayfa numarası). Belgeler her değişiklikten sonra yeniden basılır.

### 1. Oyun

**Tür:** ızgaraya parça yerleştirmeli blok bulmaca (Block Blast!, Blockudoku ve 1010! türü). Oyuncuya her turda birkaç parça verilir; oyuncu bunları ızgarada boş yerlere sürükleyerek koyar; dolan satır ve sütunlar temizlenir ve puan verir. Konacak yeri kalmayan parça yoksa oyun biter.

**Yasal sınır:** "Tetris" adı ve yukarıdan düşen parçalı görünüş KESİNLİKLE kullanılmaz (tescilli marka; 2012'de görünüşü taklit eden bir oyun davayı kaybetti). Bizim türümüz ayrıdır: parçalar düşmez, oyuncu yerleştirir.

**Pazar:** dünya pazarı (iOS ve Android). İlk dil İngilizce ve Türkçe; metinler baştan çevrilebilir tutulur.

**Oyunun dört ayağı:**
1. **Klasik (sonsuz):** en yüksek puan.
2. **Macera (bölümlü):** hedefli bölümler (belirli sayıda satır temizle, ızgaradaki taşları topla, engelleri kır, belirli puana belirli parça sayısıyla ulaş). Bölümler üretilir ve botlarla ayarlanır.
3. **Rekabet:**
   - Günlük meydan okuma: herkese aynı tohum, aynı parça sırası.
   - Haftalık lig: yaklaşık 30 kişilik gruplar, yükselme ve düşme, ödüller oyun içi.
   - Süreli turnuvalar: sabit tohumlar.
   - Daha sonra arkadaş sıralaması.
4. **Gelir:**
   - **Mağaza:** altın paketleri, booster paketleri, temalar ve görünüşler, reklamsız sürüm, sezon kartı.
   - **Ödüllü reklam:** kaybedince devam, ödülü ikiye katlama, günlük sandık.
   - **Geçiş reklamı:** seyrek ve adil.

**Booster önerileri:** tek hücreyi kıran çekiç, satır ya da sütun temizleyici, verilen parçaları yenileme, son yerleştirmeyi geri alma, bir parçayı döndürme. Parçalar normalde dönmez.

**Adalet kuralları (değişmez):**
- Para harcamak zorluğu değiştirmez. Zorluk, oyuncunun harcamasına bakarak gizlice ayarlanmaz.
- Botlar gerçek oyuncu gibi gösterilmez. Ligde ya da maçta bot varsa açıkça belirtilir.
- Para ödüllü turnuva yok: gerçek para ödülü birçok ülkede kumar sayılır, bütün ödüller oyun içidir.
- Gerçek parayla rastgele kutu satılmaz.
- Rekabet modlarında herkes aynı tohumla, aynı kurallarla oynar.

### 2. Bu oturumun kapsamı

**Yapılacaklar (saf C#, .NET):**
- oyunun motoru;
- parça ve bölüm üreticileri;
- oyunu oynayan botlar;
- bunları ölçen laboratuvar;
- ekonomi simülatörü;
- rekabet sunucusunun iskeleti;
- testler, belgeler.

**Yapılmayacaklar:**
- **Unity istemcisi:** bu ortamda Unity yok; istemci sonra ayrı bir bilgisayarda bu motoru derlenmiş hâlde kullanacak.
- Çizimler, gerçek reklam ve satın alma SDK'ları, mağaza yayını.

### 3. Mimari

```
src/Block.Core        netstandard2.1, C# 9 — ızgara, parçalar, yerleştirme, temizleme, puan, kurallar,
                      olaylar, belirleyici rastgelelik akışları. Unity'ye derlenmiş girer: dış paket yok.
src/Block.Bot         netstandard2.1 — bot politikaları (açgözlü, ileriye bakan, beceri basamakları).
src/Block.Generator   net — parça dizisi üreticileri ve macera bölüm üreticisi.
src/Block.Lab         net — binlerce oyunu paralel oynatıp ölçen araç ve raporlar.
src/Block.Meta        netstandard2.1 — cüzdan, can, booster envanteri, ödüller, mağaza kuralları
                      (istemcinin de kullanacağı saf kurallar; saat dışarıdan verilir).
src/Block.Economy     net — ekonomi simülatörü (oyuncu nüfusu × gün × oturum; ölçülmüş oyunlara bakar).
src/Block.Server      net, ASP.NET Core — tohum dağıtımı (günlük, haftalık), skor gönderimi,
                      kayıttan yeniden oynatarak doğrulama, ligler.
src/Block.Cli         net — bütün araçların komut satırı.
tests/Block.Tests     xUnit — cümle adlı testler (ör. A_full_row_and_column_clear_together_and_score_a_combo).
docs/                 Türkçe belgeler ve docs/pdf.
```

**Teknik ilkeler:**
- **Tam belirleyicilik:**
  - Kendi rastgele sayı üreticin olsun (ör. PCG32 ya da xoshiro128\*\*); `System.Random` kullanılmaz.
  - Her amaç için ayrı, tohumdan türetilmiş akış: parça dizisi, bölüm üretimi, bot gürültüsü.
  - Kuralların sonucunu etkileyen hiçbir hesapta kayan nokta yok.
  - Aynı tohum ve aynı eylemler her ortamda aynı oyunu verir. Bunu kilitleyen bir "motor izi" testi olsun: birkaç sabit oyunun her adımı bir sabit dosyayla karşılaştırılır.
- **Eylem kaydı ve yeniden oynatma:**
  - Her deneme sıralı eylemleriyle kaydedilir: hangi parça nereye kondu, hangi booster nereye kullanıldı, devam alındı mı.
  - Kayıt kural sürümünü taşır.
  - `Replay(record)` oyunu yeniden oynatıp aynı sonu verir. Sunucu skoru buna dayanarak doğrular.
- **Olaylar:** motor olay günlüğü üretir (yerleştirme, temizlenen satır ve sütunlar, kombo, oyun sonu). İstemci animasyonu bu olaylardan kurar.
- **Kapalı tipler:** sabit kümeler enum ya da kapalı sınıf ailesi. Birimi olan değerlerin kendi tipi olsun (puan, altın, hücre konumu).
- **Ayarlar dosyada:** ekonomi ve üretici ayarları JSON dosyalarında durur. Her alanın birimi ve kaynağı açıklamada yazılır: karar, varsayım, öneri, yer tutucu. Kodda varsayılan değer yok.

### 4. Kod standardı

Depoya bir `CLAUDE.md` koy ve şunları yaz; bütün ajanlar uysun:
- Açıklayamadığın kodu yazma. Derlenmesi ve testleri geçmesi yetmez.
- Yokluğu tipte göster (`T?`, `Result`/kapalı sonuç tipi). `null` ile yokluk bildirme.
- Kapalı küme kapalı tiple temsil edilir. Aynı tipte yer değiştirebilecek iki parametre bir hatadır.
- Para ve altın tam sayıdır. Asla `float`/`double` olmaz.
- Bir metot tek soyutlama seviyesinde kalır. Erken dön, yoğun koşulu isimlendir.
- Jenerik ya da boş `catch` yok. Teknik hata katman sınırını ham geçmez.
- Her önbelleğin üst sınırı olsun. Büyük veri parça parça işlenir.
- Kamuya açık sözleşme (kayıt biçimi, sunucu API'si, ayar dosyası) tek başına değiştirilmez. Değişiklik eklemelidir ve sürümlenir.
- Ölçmeden optimize etme.

### 5. Botlar ve ölçüm

Türün kalbi parça üretecidir: gelen parçalar zorlayıcı olmalı ama haksız olmamalı.

- **Bot basamakları:**
  - En güçlü bot ileriye bakar: verilen parçaların bütün sıralarını ve yerlerini dener; değerlendirme özellikleri temizlenen satırlar, delikler, boş alanın parçalanması, kalan hareket alanıdır.
  - Daha zayıf basamaklar kontrollü gürültüyle kurulur. Böylece bir "beceri merdiveni" olur.
  - Ortalama oyuncunun hangi basamak olduğunu sonra insan verisiyle belirleyeceğiz; o zamana kadar orta basamağı varsay ve bunu belgede yaz.
- **Laboratuvar raporları:**
  - Klasik mod: oyun uzunluğunun ve puanın dağılımı.
  - "Haksız son" sıklığı: iyi oynayan bot, hiçbir parçanın sığmadığı bir anla bitirdiyse.
  - Tohumdan tohuma zorluk farkı.
  - Macera bölümleri: basamak başına kazanma oranı.
- **Üretici seçenekleri:** düz rastgele, ağırlıklı, "torba" (her parça türü belli aralıkla gelir), en az bir parçanın sığmasını güvenceleyen. Her biri laboratuvarda ölçülür; öneri sayılarla bana gelir. **Uyarlanır üretici** (oyuncunun durumuna bakan) bir karardır: kurala bağlanmadan bana sorulur ve adalet kurallarıyla çelişmez.
- **Macera bölümleri:**
  - Üretici bölüm adayları üretir; botlar her adayı birçok tohumla oynar.
  - Hamle (parça) sınırı, hedef basamağın hedef kazanma oranına göre ayarlanır.
  - Önerilen paylar: Kolay %90, Normal %75, Zor %55, Çok Zor %40.
  - Zorluk bölümler boyunca testere dişi gibi ilerler.
  - Her bölüm dosyası, ayarlandığı ölçümün parmak izini taşır.

### 6. Ekonomi

- **Altının kaynakları:** bölüm kazanma, günlük ödül, ödüllü reklam, lig ödülleri.
- **Altının harcandığı yerler:** booster, devam hakkı, tema.
- **Ekonomi simülatörü:** botların ölçülmüş oyunlarına bakarak bir oyuncu nüfusunu gün gün, oturum oturum yürütür. Şunları raporlar: ilerleme hızı, altın dengesi, booster ve reklam kullanımı, nerede tıkanıldığı.
- **Fiyatlar:** tahminle değil simülatörden okunur.
- **Hoş geldin hediyesi:** önerilir, ama sayısı bir denge kuralına bağlanır. Örnek kural: hediye ilk mağaza ziyaretinde bir şey alınmasını sağlar ama ilk devam hakkını hazır vermez.
- **Denge kuralları:** sayılar arasındaki sabit ilişkiler testlerle tutulur. Biri değişip ona bağlı olan değişmezse test kırmızıya döner.

### 7. Rekabet sunucusu (iskelet)

- **Tohumlar:** günlük ve haftalık tohumlar sunucudan gelir. İstemci, oynadığı denemenin kaydını gönderir.
- **Skor doğrulama:** sunucu kaydı `Replay` ile yeniden oynatır. Skor uyuşmazsa ya da süre makul değilse kabul etmez.
- **İstek sınırları:** hız sınırı ve tekrarlanan gönderime karşı koruma.
- **Ligler:** benzer etkinlikteki oyunculardan gruplar. Haftalık kapanışta yükselme, düşme ve ödül. Bot doldurma varsa bot açıkça işaretlenir.
- **Kimlik:** başlangıçta cihaz kimliği ve basit hesap. Gerçek hesap ve platform girişi sonra.
- Testler ve yerel çalıştırma komutları.

### 8. Aşamalar ve kapılar

Her aşamanın sonunda:
1. Bütün testler geçer.
2. Bağımsız bir gözden geçirme ajanı işi yıkmaya çalışır; bulduklarını düzelt.
3. Bana sayılarla kısa bir rapor yaz.
4. Kaydet ve gönder.

Önceki aşamanın kapısı geçilmeden ona bağlı aşama başlamaz.

| Aşama | İş | Paralel ajanlar (öneri) |
|---|---|---|
| 0 | Plan ve karar belgesi, depo iskeleti, `CLAUDE.md`, belge ve PDF derleyicisi | 1 |
| 1 | `Block.Core`: kurallar, belirleyicilik, olaylar, motor izi testi; eylem kaydı ve yeniden oynatma | 2 (çekirdek; kayıt) + 1 gözden geçirme |
| 2 | Botlar ve laboratuvar; üretici seçeneklerinin ölçülmesi | 2 + 1 gözden geçirme |
| 3 | Macera bölüm üreticisi ve ayarı; ilk 200 bölüm | 2 + 1 gözden geçirme |
| 4 | `Block.Meta` kuralları ve ekonomi simülatörü | 2 + 1 gözden geçirme |
| 5 | Rekabet sunucusu iskeleti, kayıttan doğrulama, ligler | 2 + 1 gözden geçirme |
| 6 | Belgeler: Genel Belge, Tasarım Belgesi, Geliştirme Günlüğü, ölçüm raporları, Unity istemcisi için devir notu | 1 (sürekli) |

### 9. Bana ilk soracağın kararlar

Her biri için bir önerin olsun:
- **Izgara:** 8×8 mi, 9×9 mu, ikisi mi?
- **Parçalar:** turda kaç parça (öneri 3)? Parça kümesi: hangi şekiller ve kaç farklı?
- **Döndürme:** booster mı, hiç yok mu?
- **Klasik mod:** üretici hangisi olacak? Uyarlanır üretici olacak mı?
- **Macera hedefleri:** hangi hedef türleri, hangi engeller? Can sistemi olacak mı (öneri: 5 can, 30 dakikada bir)?
- **Reklam:** ödüllü reklam nerelerde? Geçiş reklamı sınırı ne olsun?
- **Ligler:** grup büyüklüğü, ödüller.
- **Görsel yön:** karaktersiz olacak; soyut, mücevher, ahşap ya da benzeri. Adı ve teması sonra.

### 10. Bitti tanımı

- `dotnet test` bütün testlerle geçer. Motor izi kilitli.
- Laboratuvar raporları üretilir:
  - üreticilerin karşılaştırması;
  - macera bölümlerinin basamak başına kazanma oranları;
  - "haksız son" sıklığı.
- Ekonomi simülatörü varsayılan ayarlarla çalışır ve rapor üretir.
- Sunucu yerelde çalışır. Günlük tohum verir, kayıtla gelen skoru doğrular ya da reddeder (testli).
- Belgeler ve PDF'leri güncel. Unity istemcisi için devir notu hazır: motorun nasıl derleneceği, olayların nasıl animasyona çevrileceği, kayıt dosyası.
- Her aşamanın raporu ve kararları Geliştirme Günlüğü'nde.

Başla: önce 0. aşamanın planını ve karar sorularını bana getir.
