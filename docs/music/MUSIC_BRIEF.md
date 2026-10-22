# Müzik İstekleri (Suno)

Oyunun müziği proje sahibinin Suno Premier hesabıyla üretilecek (#9). Bu belge hangi parçaların gerektiğini, Suno'ya ne yazılacağını ve dosyaların oyuna nasıl gireceğini anlatır. Sözsüz müzik: Suno'da her parça için **Instrumental** açık olmalı.

**Karar (2026-10-22):** proje sahibi yedi parçanın hepsini Suno'da üretti ve `art/sounds/` klasörüne koydu (2026-10-20). Oyunda **tek bir genel müzik** olacak: M1 Müze teması, oyunun her yerinde (müze, harita, koleksiyon, mağaza ve bölümler). Kısa parçalar (M3–M7) ve ayrı tahta müziği (M2) kullanılmayacak. Aşağıdaki liste ve istemler üretimin kaydı olarak duruyor.

## 1. Oyunun sesi

Oyun bir müzede geçiyor: genç bir ressam tablo parçalarını toplayıp odaları onarıyor. Görsel dil sıcak, yaldızlı çerçeveli, koyu camgöbeği duvarlı bir galeri. Müzik buna eşlik etmeli:

- **Sıcak ve sakin,** ama uyutmayan. Oturumlar kısa (yaklaşık 10 dakika), aynı parça defalarca duyulacak; yorucu bir melodi kısa sürede rahatsız eder.
- **Küçük oda müziği:** piyano, pizzicato yaylılar, çelesta, glockenspiel, arp, yumuşak klarnet ya da flüt. Elektronik davul, bas düşüşü, vokal, koro yok.
- **Efektlerin altında kalmalı:** taş sesleri ve booster sesleri müziğin üstünde duyulur. Çok yüksek frekanslı parlak sesler (sürekli çan, zil) efektlerle çakışır.
- **Tek bir müzik ailesi:** bütün parçalar aynı tonalitede ya da yakın tonlarda olursa geçişler yumuşak olur. Öneri: Fa majör ya da Re majör.

## 2. Parça listesi

| # | Parça | Nerede çalar | Uzunluk | Döngü |
|---|---|---|---|---|
| M1 | Müze teması | Salon, harita, koleksiyon, mağaza | 90–120 sn | Evet, dikişsiz |
| M2 | Tahta müziği | Bölüm oynanırken | 90–120 sn | Evet, dikişsiz |
| M3 | Kazanma | Kazanma kartı inerken | 3–5 sn | Hayır |
| M4 | Kaybetme | Kaybetme kartı inerken | 2–4 sn | Hayır |
| M5 | Tablo tamamlandı | Son parça yerine oturunca | 6–10 sn | Hayır |
| M6 | Oda onarıldı | Bir odanın beş tablosu bitince | 8–12 sn | Hayır |
| M7 | Ödül | Hediye, booster açılışı, satın alma | 1–3 sn | Hayır |

İlk sürüm için M1, M2, M3, M5 yeterli; M4, M6, M7 sonra eklenebilir. İleride her dönemin (oda) kendi müze teması olabilir; şimdilik tek tema.

## 3. Suno'ya yazılacaklar

Suno İngilizce stil tanımlarını daha iyi anlıyor. Her parça için **Style of Music** alanına aşağıdaki metin, **Title** alanına parçanın adı yazılır; **Instrumental** açık. Her istemden birkaç sürüm üretip en iyisini seçmek iyi olur.

**M1 · Müze teması**
> gentle chamber music, warm museum gallery ambience, solo piano with pizzicato strings and soft celesta, calm and cozy, light waltz feel, 84 bpm, F major, no drums, no vocals, loopable, cinematic casual game menu music

**M2 · Tahta müziği**
> playful light puzzle game music, pizzicato strings, marimba and soft piano, bouncy but relaxed, steady gentle pulse, 104 bpm, F major, no heavy drums, no bass drops, no vocals, unobtrusive background, loopable, cozy casual mobile game

**M3 · Kazanma**
> short triumphant orchestral fanfare, bright strings, harp glissando, glockenspiel sparkle, joyful, ends on a clean resolved chord, 3 seconds, F major, no vocals

**M4 · Kaybetme**
> short gentle descending phrase, soft clarinet and pizzicato strings, light-hearted "oh no", not sad, ends softly, 3 seconds, F major, no vocals

**M5 · Tablo tamamlandı**
> short emotional reveal, swelling strings, harp arpeggio, soft bells, wonder and warmth, like unveiling a painting in a museum, 8 seconds, F major, ends on a warm resolved chord, no vocals

**M6 · Oda onarıldı**
> short celebratory orchestral finale, strings and woodwinds, rising to a bright resolved ending, grand but cozy, 10 seconds, F major, no vocals

**M7 · Ödül**
> tiny magical reward sting, celesta and harp sparkle, 2 seconds, F major, no vocals

Suno kısa parçalar yerine çoğu zaman uzun bir parça üretir. Bu sorun değil: kısa parçalar (M3–M7) üretilen dosyadan kesilip alınır (aşağıda).

## 4. Dosyalar ve kayıt

- **Biçim:** Suno'dan **WAV** indirilir (MP3 de olur, ama kesip döngü yapmak için WAV daha temiz).
- **Bilgisayarda yeri:** `art/music/source/` klasörü. Dosya adı parçanın kodu ve sürümüyle: `M1-muze-temasi-v1.wav`.
- **Kayıt:** her parça için `art/music/licences.json` dosyasına hangi hesapla, hangi tarihte, hangi istemle üretildiği ve Suno'daki bağlantısı yazılır.
- **Ticari kullanım:** proje sahibinin Suno Premier üyeliği ticari kullanıma izin veriyor; ürettiği müzikleri zaten YouTube kanalında yayımlıyor (2026-10-19). Parçaların ücretli üyelik sürerken üretildiği kayıtta görünür.

## 5. Oyuna girişi (#9)

Dosyalar gelince oyun tarafında şunlar yapılır:

1. **Döngüler (M1, M2):** başı ve sonu dikişsiz birleşecek bir bölüm seçilir; gerekirse birkaç yüz milisaniyelik yumuşak geçişle (crossfade) birleştirilir. Döngü noktası kulakla ve dalga biçimiyle denetlenir.
2. **Kısa parçalar (M3–M7):** üretilen dosyadan en iyi birkaç saniye kesilir; başı ve sonu yumuşatılır.
3. **Ses seviyesi:** müzik efektlerin altında durur; bütün parçalar aynı yükseklikte duyulacak şekilde dengelenir.
4. **Ayarlar:** ayarlar kartına "Müzik" anahtarı eklenir (ses ve titreşim gibi; açık başlar, hemen etki eder, saklanır).
5. **Geçişler:** müzeden tahtaya geçerken M1 yavaşça kısılıp M2 başlar; kazanma ve kaybetme anında tahta müziği kısılır, kısa parça çalar.
6. **Deneme:** proje sahibi telefonda dinleyip onaylar.
