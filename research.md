# Araştırma Özeti — Match-3 Mimarisi ve Algoritmaları

Kaynak: Gemini Deep Research, "Architectural Systems and Algorithmic Frameworks in Modern Mobile
Tile-Matching Puzzle Games" — https://share.gemini.google/Wl7AYCYAO7X6

Aşağıdaki özet kaynağın ana fikirlerini ve bizim tasarımda ([docs/GAME_DESIGN.md](docs/GAME_DESIGN.md))
nasıl kullanıldığını içerir.

## 1. Matematiksel model

- Tahta bir graf: her hücre bir düğüm, komşuluk kenar. Eşleşme = aynı renkteki bitişik hücrelerin
  yatay/dikey 3+ dizisi.
- Döngü: swap → eşleşme bul → temizle → yerçekimi → boş üst hücrelere spawn → yeni eşleşme var mı
  (kaskad) → kararlı duruma kadar tekrar.
- "Bu tahta k hamlede çözülebilir mi?" sorusu **NP-zor** (3-SAT indirgemesi). Kesin çözücü yok; sezgisel
  arama ve simülasyon şart.
  → **Bizde:** bot kesin çözüm aramaz, istatistiksel olarak oynar (bkz. §4).

## 2. Bölüm üretimi (makro seviye)

| Yaklaşım | Nasıl | Artı | Eksi |
|---|---|---|---|
| Ters oynatma | Çözülmüş tahtadan geriye doğru hamle uygula | Çözülebilirlik garantisi | Tekdüze bölümler |
| Seed + bot doğrulama | Seed'den bölüm üret, botla oynat, hedef zorluktakileri seç | Ölçeklenir, hafif | Çok hesaplama |
| cVAE + simetri maskesi | Mevcut bölümlerden öğrenen üretici model | İnsan yapımı gibi | Büyük veri seti gerekir |
| Wave Function Collapse | Kısıt yayılımıyla hücre hücre yerleşim | Yerel tutarlılık | Çelişkide geri izleme |

- Tasarım ilkeleri: her bölümde bir "kanca" (yeni ilginç durum), **en fazla 4 mekanik** aynı anda,
  booster'sız çözülebilirlik, bölge mantığı (giriş / geçiş / hedef bölgesi).
  → **Bizde:** "seed + bot doğrulama" + simetrik şekil şablonları + 4 mekanik sınırı.

## 3. Spawn algoritmaları (mikro seviye)

| Algoritma | Davranış |
|---|---|
| Uniform rastgele | Yüksek varyans, renk kuraklığı |
| Ağırlıklı seçim | Renk oranları ayarlanabilir; yerel sıçramalar olabilir |
| Shuffle bag | Torbadan geri koymadan çekiliş; kuraklık imkânsız |
| Dinamik "dueness" | Çıkmayan rengin olasılığı artar, çıkanınki düşer; `s` parametresi varyansı ayarlar |

→ **Bizde:** "dueness" (sönümlü ağırlıklı) algoritması, tam sayı sabit-nokta ile.

## 4. Otomatik test ve zorluk tahmini

- **MCTS:** sağlam ama yavaş; insanların göremeyeceği kaskadları bulur → zorluğu **olduğundan düşük**
  gösterir (~%5,4 hata).
- **CNN taklit öğrenmesi (King):** milyonlarca oyuncu hamlesiyle eğitilmiş ağ, insan gibi oynar
  (~%4 hata). 9x9 tahta → 9×9×102 tensör, 144 olası swap çıktısı.
- Botun kazanma oranı regresyonla insan kazanma oranına çevrilir.
  → **Bizde:** oyuncu verisi olmadığı için parametrik greedy bot (dikkat + sıcaklık ile insanlaştırılmış);
  lansmandan sonra telemetriyle kalibrasyon. CNN, veri biriktikten sonraki aşama.

## 5. Dinamik zorluk ayarı (DDA)

- İzlenen metrikler: art arda başarısız deneme, hamle verimliliği, oyuncu beceri puanı (EWMA),
  bırakma (churn) olasılığı.
- Müdahaleler: hedef rengin spawn ağırlığını artırma, spawner'lardan özel taş düşürme, engel canını
  azaltma, "neredeyse kazanıyordun" anında ek hamle teklifi.
  → **Bizde:** Faz 3 (Unity). Çekirdekteki spawn sistemi ağırlık tabanlı olduğu için hazır.
