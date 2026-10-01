# Geliştirme Günlüğü

Her oturumun kararları, bulguları ve değişiklikleri. En yeni kayıt en üstte.

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
