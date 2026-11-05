# Araştırma: Spawn Kontrolü, Çözülebilirlik ve Arama Algoritmaları

> Bu rapor 2026-10-01'de bir yapay zekâ araştırma ajanı tarafından web kaynaklarından derlendi. Her iddia
> kaynak bağlantısı ve kanıt düzeyiyle verildi. Ürün ya da hukuk kararı vermeden önce ilgili kaynakları
> doğrudan kontrol edin. Tasarıma yansıyan kararlar: [GAME_DESIGN.md §15](../GAME_DESIGN.md).

**Etiketler**
- **[Belgelenmiş]**: patent, hakemli makale ya da resmi kurum metni.
- **[Geliştirici beyanı]**: stüdyo çalışanının verdiği röportaj ya da yazdığı blog.
- **[Topluluk gözlemi/spekülasyon]**: oyuncu forumu ya da kişisel analiz.

Doğrulanamayan iddialar ayrıca "Doğrulanamadı" diye işaretlendi.

## Kısa sonuç

- **"Oyun seni bir yöne itiyor" hissi:** Kısmen destekleniyor.
  - Büyük yayıncıların rastgeleliği oyuncuya göre yönlendiren patentleri var (EA, Zynga).
  - En az bir canlı match-3 oyununda zorluğun seed seçimiyle ayarlandığı yayımlanmış (EA).
- **"Her denemede aynı taşlar düşüyor":** Desteklenmiyor.
  - Candy Crush, Royal Match, Homescapes veya Toon Blast için seviye başına sabit bir refill dizisini ya da retry'da aynı taşların düştüğünü gösteren birincil kaynak bulunamadı.
  - King'in kendi makalesi Candy Crush'ı rastgele dolan, deterministik olmayan bir oyun olarak tanımlıyor.
- **Optimal yol araması:** Sektör seviye tasarımında "optimal DFS yolu" aramıyor; insan benzeri botlarla stokastik simülasyon yapıyor. Refill'ler önceden bilinse bile mükemmel oyunu bulmak NP-zor.
- **Düzenleyici ortam:** 2026'da sertleşti. AB, 30 Eylül 2026'da King ve Playrix satıcısı dahil 9 şirkete koordineli işlem başlattı.

## 1. Ticari match-3'lerde seed'li veya deterministik refill var mı?

**Belgelenmiş olanlar**

- **King (Candy Crush, Soda Saga):** King araştırmacıları bu iki oyunu deterministik olmayan oyunlar olarak tanımlıyor. Boşalan hücreler yeni rastgele taşlarla doluyor. Kaynak: Gudmundsson vd., IEEE CIG 2018. [Belgelenmiş] https://gwern.net/doc/reinforcement-learning/imitation-learning/2018-gudmundsson.pdf
- **EA'nın canlı match-3 oyunu:**
  - Zorluk "random seed of board initialization" üzerinden ayarlanıyor; seed'ler 0–99 arası numaralı.
  - En zor seed'le oynayan, seviyeyi geçmek için en kolay seed'le oynayana göre yaklaşık 5 kat fazla deneme yapıyor.
  - Yani her denemede hangi seed'in verileceği bir kontrol kolu.
  - Kaynak: Xue vd., WWW 2017. [Belgelenmiş] https://dl.acm.org/doi/pdf/10.1145/3041021.3054170
- **Lily's Garden (Tactile Games):**
  - Rastgele öğeler ve oyuncu becerisi yüzünden zor seviyeler bazen 30'dan fazla deneme gerektiriyor.
  - Ajan verisi seviye başına 8 rastgele seed ile toplanmış.
  - Kaynak: Kristensen ve Burelli 2024. [Belgelenmiş] https://arxiv.org/html/2401.17436v1
- **King patenti:** Hamle kalmadığında taşlar yeniden karıştırılıyor, böylece her zaman bir hamle bulunuyor. Kaynak: US20140080560A1. [Belgelenmiş] https://patents.google.com/patent/US20140080560A1/en

**Doğrulanamayanlar**

- **Royal Match, Homescapes, Toon Blast:** Refill'in seed'li olduğunu ya da retry'da aynı kaldığını söyleyen stüdyo kaynağı (patent, konuşma, blog) bulunamadı. Bu, öyle olmadığını göstermez; yalnızca kamuya açık kanıt yok.

**Topluluk gözlemleri**

- **Candy Crush'ta seviyeden çıkıp girme:**
  - Bazı oyuncular ilk hamleden önce çıkıp can kaybetmeden yeniden girebiliyormuş; bazılarında bu sonradan kaldırılmış (King forumu, 2019). https://community.king.com/en/candy-crush-saga/discussion/260620/losing-a-life-when-i-quit-before-making-a-move
  - Reddit'te bunun istenen dizilim gelene kadar tekrarlanabildiği yazılmış; bu, başlangıç tahtasının her girişte değiştiğini düşündürüyor. https://www.reddit.com/r/changemyview/comments/2ga9na/cmv_candy_crush_is_rigged/
  - [Topluluk gözlemi]
- **"Şanslı tahtayı algoritma belirliyor":** Bir King forumu kullanıcısı (Temmuz 2025) seviye zorluklarının yeniden yazıldığını iddia ediyor. Topluluk yöneticisi ne doğruluyor ne yalanlıyor. [Topluluk spekülasyonu] https://community.king.com/en/candy-crush-saga/discussion/503294/bring-back-human-level-designers
- **Royal Match ve Toon Blast'ta kolaylaşma:** Oyuncular birkaç başarısızlıktan ya da bir gün ara verdikten sonra seviyenin kolaylaştığını yazıyor; kontrollü bir ölçüm yok. [Topluluk spekülasyonu]
  - https://oldcynic.com/royal-match-royal-league-guide-tips
  - https://medium.com/@ekinmelissezer/game-analysis-of-toon-blast-mechanics-level-design-difficulty-patterns-and-monetization-signals-022748ae51b4
  - https://www.reddit.com/r/ToonBlast/comments/1ge1qjz/are_levels_impossible/

**Değerlendirme:** Belgelenmiş model "her denemede aynı refill" değil. Refill her denemede rastgele, ama oyuncuya hangi seed'in verileceği seçilebiliyor.

## 2. Kontrollü rastgelelik ve spawn yönlendirme

- **EA, US20170259177A1** (başvuru 8 Mart 2016; mucitler arasında Xue, Kolen, Aghdaie var):
  - Oyuncunun churn ve oynama süresi tahminine göre zorluk otomatik ayarlanıyor.
  - Ayar kolları olayların olasılığını değiştiren seed'ler olabiliyor; yüksek churn, zorluğu düşüren seed değerlerine eşlenebiliyor.
  - Özette "difficulty adjustment is undetectable by a user" deniyor.
  - Aynı ekip, EA'nın match-3 oyununda DDA ile bağlılığın %9'a kadar arttığını raporluyor (Xue 2017).
  - [Belgelenmiş] https://patents.google.com/patent/US20170259177A1/en
- **Zynga, US10363487B2** (öncelik 18 Kasım 2014):
  - Bir "System Tuner" seviye başlamadan önce taş torbasının kompozisyonunu ve düşme oranını oyuncunun becerisine göre ayarlıyor.
  - Oyuncunun ilerleme hızı referanstan saparsa değiştirilmiş ayarı devreye sokuyor.
  - [Belgelenmiş] https://patents.google.com/patent/US10363487B2/en
- **Zynga, US9757650:** Taş eşleştirme oyununda torbadaki taş türlerinin dağılımı becerisine göre ayarlanıyor. [Belgelenmiş] https://patents.justia.com/patent/9757650
- **Zynga, US11406900** (öncelik 10 Eylül 2014): Etkinliklerde nesnelerin düşme oranı ve başlangıçta verilen nesneler, beceriye ve referans ilerleme hızına göre belirleniyor. [Belgelenmiş] https://patents.justia.com/patent/11406900
- **King, US20140080560A1:**
  - Oyuncuların seviyede ortalama kaç kez başarısız olduğuna göre hedef puan, süre veya hamle sayısı değiştirilebiliyor ya da booster veriliyor.
  - Zor bir seviyenin ardından bilerek kolay bir seviye konuyor.
  - Patent spawn yönlendirmesi **tarif etmiyor**.
  - [Belgelenmiş]
- **Bulunamayanlar:**
  - King'e ait, spawn renk olasılığını oyuncuya göre değiştiren bir patent.
  - Gereken rengi bilerek esirgeyen ya da refill ile near-miss üreten bir match-3 mekanizmasını tarif eden birincil kaynak.
  - Bunlar şu an spekülasyon düzeyinde.
- **Oyuncu tarafı:** DDA oyuncularda belirgin bir aşırı özgüvene yol açabiliyor (Constant ve Levieux, CHI 2019). [Belgelenmiş] https://hal.science/hal-02141897

## 3. Çözülebilirlik ve arama

- **Walsh (2014):** Genelleştirilmiş Candy Crush'ta belirli hamleyle belirli puana ulaşmak NP-zor. [Belgelenmiş] https://arxiv.org/abs/1403.1911
- **Gualà, Leucci ve Natale (2014):**
  - Oyuncunun bütün gelecek taşları bildiği "offline" sürümde bile doğal sorular NP-zor.
  - Yani "refill'ler bilinirse optimal yol kolayca bulunur" varsayımı geçersiz.
  - [Belgelenmiş] https://arxiv.org/pdf/1403.5830
- **King'de MCTS'ten insan benzeri bota geçiş:**
  - Önce MCTS ile insan başarısı tahmin edilmeye çalışıldı (Poromaa, KTH 2017). [Belgelenmiş] https://www.semanticscholar.org/paper/Crushing-Candy-Crush-%3A-Predicting-Human-Success-in-Poromaa/36b634a2e23e3734ec591d58849cb102612a3029
  - Sonra oyuncu hamleleriyle eğitilmiş bir CNN ajanı insan geçme oranıyla MCTS'ten daha iyi örtüştü ve çok daha az hesaplama gerektirdi.
  - Ajan bir yıldan uzun süre, 1.000'den fazla yeni seviyede kullanıldı.
  - Ajanlar oyunculardan çok daha az rastgele davranıyor (Gudmundsson 2018). [Belgelenmiş]
- **King AI Labs direktörü Sahar Asadi:**
  - Bot "en iyi" hamleyi değil, en insan benzeri hamleyi seçiyor.
  - Bot sayesinde elle ayarlama %95 azaldı, süreç %50 hızlandı.
  - Seviyeler binlerce kez koşturuluyor; neredeyse her seviye artık yapay zekâ yardımıyla üretiliyor.
  - [Geliştirici beyanı] https://www.gamesindustry.biz/how-king-is-using-ai-to-speed-up-development-of-new-candy-crush-levels — https://mobilegamer.biz/how-king-balances-human-and-ai-powered-design-in-candy-crush-saga/
- **King AI Center of Excellence (GAT/BERT):** Zorluk "başarı başına deneme sayısı" olarak tanımlanıyor; her seviye model başına 1.000 turla test ediliyor. [Belgelenmiş] https://arxiv.org/html/2607.11501v1
- **Rovio (Angry Birds Dream Blast):**
  - Derin pekiştirmeli öğrenme (DRL) ajanları ile oyuncu popülasyonu simülasyonu birlikte geçme ve churn oranını tahmin ediyor. https://arxiv.org/abs/2008.12937
  - DRL ile güçlendirilmiş MCTS en zor seviyelerde daha iyi; ajanın en iyi koşuları, insan verisiyle ortalama koşulardan daha güçlü korelasyon verebiliyor. https://arxiv.org/abs/2107.12061
  - [Belgelenmiş]
- **Lily's Garden:** En iyi zorluk tahmini, kohort istatistikleri simülasyon verisiyle birleşince çıkıyor. Playtest ajanı verisi olmadan modeller genel ortalamayı bile geçemiyor. [Belgelenmiş]
- **EA SEED "Avalon":**
  - Zorluğa göre koşullandırılmış bir cVAE ile match-3 seviyeleri üretiliyor; geçerli seviye oranı %43,75'ten %51,39'a çıktı.
  - Her seviye kural tabanlı bir botla 30 oyunda değerlendirildi; taşlar uniform dağılımla düşüyor.
  - [Belgelenmiş] https://media.contentapi.ea.com/content/dam/ea/seed/presentations/seed-tog-conditional-level-generation-paper.pdf
- **"Bilinen refill'lerle mükemmel oyun" seviye tasarımında kullanılıyor mu?**
  - Bunu söyleyen bir stüdyo kaynağı bulunamadı (Doğrulanamadı). Yayımlanmış örneklerin hepsi çok seed'li stokastik simülasyon ve insan benzeri ajan kullanıyor.
  - Tek bir sabit dünyayı çözüp belirsiz dünyaya genellemek "strategy fusion" hatasına açık (Long vd., AAAI 2010). [Belgelenmiş] https://cdn.aaai.org/ojs/7562/7562-13-11092-1-2-20201228.pdf

## 4. Near-miss mühendisliği ve elde tutma

- **Larche, Musielak ve Dixon (Journal of Gambling Studies, 2017):**
  - Katılımcılar 60 düzenli Candy Crush oyuncusu.
  - Seviyeyi bir-iki hamleyle kaçırmak kayıptan daha uyarıcıydı (kalp atışı yükseldi) ve en sinir bozucu sonuç olarak değerlendirildi.
  - Aynı zamanda devam etme isteğini en çok artıran sonuç buydu; oyun bu durumu oyuncuya ayrıca vurguluyor.
  - [Belgelenmiş] https://pubmed.ncbi.nlm.nih.gov/27435416/
- **Finserås vd. (IJMHA 2019):** Bir bisiklet yarışı oyununda near-miss devam etme isteğini anlamlı ölçüde artırmadı; yani kanıtlar karışık. [Belgelenmiş] https://irep.ntu.ac.uk/id/eprint/36202/1/13731_Griffiths.pdf
- **Doğrulanamadı:** Bir match-3'ün refill'i near-miss üretecek şekilde bilerek yönlendirdiğini ve bunun elde tutmayı artırdığını gösteren birincil kaynak yok. Laboratuvar etkisi ve churn'e bağlı zorluk patentleri ayrı ayrı var; ikisinin birlikte kullanıldığı iddiası spekülasyon.

## 5. Etik ve düzenleyici çerçeve

- **AB tüketici otoriteleri (CPC Ağı):**
  - 21 Mart 2025'te oyun içi sanal paralar için temel ilkeleri yayımladı: şeffaf fiyat, maliyeti gizlememe, cayma hakkı, özellikle çocuklarda kırılganlıklara saygı.
  - 30 Eylül 2026'da 9 şirkete koordineli işlem başlattı. Aralarında King.com ve Playrix oyunlarının satıcısı PLR Worldwide Sales var; hedeflenen uygulamalar arasında "addictive design" sayılıyor.
  - [Belgelenmiş]
    - https://commission.europa.eu/topics/consumers/consumer-rights-and-complaints/enforcement-consumer-protection/coordinated-actions/social-media-online-games-and-search-engines_en
    - https://www.acm.nl/en/publications/acm-and-other-european-consumer-authorities-step-action-protect-gamers-against-deception
    - (PLR'nin Playrix satıcısı olduğu: https://apps.apple.com/bs/app/gardenscapes/id1105855019)
- **Digital Fairness Act:** Komisyonun 2026 çalışma programında 2026'nın son çeyreği için planlanıyor. Dark pattern, bağımlılık yaratan tasarım ve kırılganlıkları sömüren kişiselleştirme kapsamda. [Belgelenmiş] https://www.europarl.europa.eu/legislative-train/theme-protecting-our-democracy-upholding-our-values/file-digital-fairness-act
- **Avrupa Parlamentosu (18 Ocak 2023):** Rastgele sonuçlu oyun içi satın almalarda olasılık şeffaflığı ve algoritmanın neyi optimize ettiğinin sade dille açıklanması istendi. [Belgelenmiş] https://eur-lex.europa.eu/legal-content/EN/TXT/PDF/?uri=CELEX:52023IP0008
- **App Store ve Google Play:** Parayla satılan rastgele öğelerde olasılıkların satın almadan önce gösterilmesi zorunlu. [Belgelenmiş] https://developer.apple.com/app-store/review/guidelines/ — https://www.gamedeveloper.com/business/games-on-the-google-play-store-now-required-to-disclose-loot-box-odds
- **Değerlendirme (kaynak değil, yorum):**
  - Satın alınmayan bir refill yönlendirmesi loot box kurallarına doğrudan girmez.
  - Ama şu durumlarda Digital Fairness Act'in hedeflediği alana girer:
    - ödeme anlarıyla (ekstra hamle teklifi) çakışırsa,
    - harcama eğilimine göre kişiselleştirilirse,
    - fark edilmemesi bilerek amaçlanırsa.

## Mekanizma özet tablosu

| Mekanizma | Kanıt düzeyi | Kaynak | Biz ne alıyoruz |
|---|---|---|---|
| Her denemede zorluk etiketli seed seçimi | Belgelenmiş | Xue 2017; US20170259177A1 | Evet: etiketli seed havuzu, yalnızca kolaylaştırma yönünde |
| Beceriye göre taş torbası ve düşme oranı | Belgelenmiş (patent; canlı kullanım bilinmiyor) | US10363487B2, US9757650, US11406900 | Kısmen: sınırlı DDA yardım kolu |
| Toplu başarısızlık verisiyle hedef, hamle ve booster ayarı | Belgelenmiş | US20140080560A1 | Evet: oyun dışında (offline) ayar |
| Hamle kalmayınca karıştırma, her zaman görünür bir hamle | Belgelenmiş | US20140080560A1 | Evet |
| İnsan benzeri playtest botu | Belgelenmiş + Geliştirici beyanı | Gudmundsson 2018; GI.biz; arXiv 2607.11501 | Evet |
| DRL+MCTS, en iyi koşular | Belgelenmiş | Roohi 2021 | Evet (geleceği bilmeyen güçlü ajan) |
| Zorluğa göre koşullu prosedürel üretim | Belgelenmiş | EA Avalon | Evet: üretici portföyü + simülasyon filtresi |
| Bilinen refill'lerle mükemmel oyun | NP-zor olduğu belgelenmiş; tasarımda kullanıldığı doğrulanamadı | Gualà 2014; Long 2010 | Yalnızca üst sınır ölçümü |
| Retry'da aynı refill | Doğrulanamadı | — | Ürün kararı (öneri: her denemede yeni seed) |
| Başarısızlık veya aradan sonra kolaylaşma | Topluluk spekülasyonu | oldcynic, Medium, Reddit | Şeffaf ve ölçülen DDA ile |
| Near-miss'in devam etme isteğine etkisi | Belgelenmiş (karışık) | Larche 2017; Finserås 2019 | Hayır: near-miss hedeflenmez |
| Gereken rengi bilerek esirgeme | Kanıt bulunamadı | — | Hayır (canlı oyunda) |

## Bizim tasarıma etkisi

1. **Deterministik çekirdek ve bilinçli bir retry politikası**
   - RNG akışı `seed = H(levelId, levelVersion, attemptIndex)` ile türetilsin.
   - Spawn yönetmeni girdilerinin saf bir fonksiyonu olsun; seed ve hamle listesi oyunu birebir yeniden oynatsın.
   - Retry'da aynı seed'i vermek bulmacayı ezberlenebilir yapar.
   - Öneri: başlangıç tahtası sabit olabilir, refill her denemede yeni seed'den gelsin ve seed havuzu zorlukla etiketlensin.
2. **Spawn yönetmeni: sınırlı ve denetlenebilir karışım**
   - Sıra: taban dağılım → kuraklık sönümleme → hedef eğilimi → kaskad kontrolü → DDA yardımı.
   - Renk olasılıkları taban değerin ~0,5–1,5 katına kırpılsın (ölçerek ayarlanmalı).
   - Her spawn için hangi stratejinin ne kadar bias kattığı telemetriye yazılsın.
   - DDA yalnızca yardım yönünde çalışsın ve başarısızlık sinyalleriyle tetiklensin.
   - Harcama veya ödeme eğilimi DDA'ya girdi olmasın.
3. **Üç ajan ve "hata payı" metriği**
   - Ajanlar:
     - (a) Seed'i bilen beam search: bulunan en iyi, yani alt sınır tahmini.
     - (b) Geleceği bilmeyen güçlü ajan: örneklenmiş dünyalar üzerinde MCTS veya expectimax; gerçekçi beceri tavanını verir.
     - (c) İnsan benzeri bot.
   - Metrikler:
     - `marj = hamle limiti − P80(b)`
     - `beceri aralığı = geçme(b) − geçme(c)`
     - `şans payı = seed'ler arası geçme varyansı`
   - Hamle limiti yalnızca (a)'ya göre ayarlanmasın.
4. **Üretici portföyü ve simülasyon filtresi.** Oyun canlıya çıkınca zorluk modeli kohort verisiyle yeniden kalibre edilsin.
5. **Etik korkuluklar**
   - Ekstra hamle veya booster teklifinden önceki N hamlede yönetmen bias'ı sabitlensin.
   - Near-miss hedefleme yapılmasın.
   - Parayla satılan rastgele ödüllerin olasılıkları gösterilsin.
   - DDA politikası yazılı olsun; seed ve bias kayıtları saklansın.
6. **Geliştiricinin gözlemine yanıt**
   - "Doğru hamleyle geçilir, yanlış hamle şansı düşürür" deneyimi sabit refill gerektirmez: hamle limitli stokastik bir bulmacada her hamle kazanma olasılığını değiştirir.
   - "Oyun seni belli bir duruma itiyor" hissi belgelenmiş seed seçimi ve DDA ile açıklanabilir.
   - Bunun hedef aldığımız ticari oyunlarda refill düzeyinde yapıldığı doğrulanamadı.
