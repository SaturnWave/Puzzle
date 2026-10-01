# Geliştirme Günlüğü

Her oturumun kararları, bulguları ve değişiklikleri. En yeni kayıt en üstte.

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
