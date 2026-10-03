# Geliştirme Günlüğü

Her oturumun kararları, bulguları ve değişiklikleri. En yeni kayıt en üstte.

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
