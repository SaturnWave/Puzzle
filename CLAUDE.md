# Puzzle: proje kuralları

Her Claude oturumu ve ajanı bu dosyayı okur. Burada yalnız çiğnenmesi pahalıya patlamış kurallar var; gerekçeleri ve ayrıntıları belgelerde.

## Mağazaya giden paket

- **Yüklemeden önce `docs/PLAY_RELEASE.md` 10'daki liste eksiksiz yapılır.** Paketin kendi APK seti telefonda kurulur, soğuk açılır, kaydı okunur ve oynanır. Sonra paket Play'in dahili testinden kurulup açılır. Kapalı teste ya da üretime ancak ondan sonra terfi ettirilir. Test APK'sında geçen bir deneme mağaza paketini kanıtlamaz: versionCode 1 bu yüzden her açılışta kapandı (`docs/LESSONS.md` H5).
- **Kütüphanenin tanımladığı kanıtsız çıkarılmaz.** Bir kütüphanenin manifestte tanımladığı bileşen ya da izin manifestten çıkarılmadan önce kütüphanenin o bileşene dokunan kodu okunur (`javap -c`). Sonuç, gönderilecek paket biçiminde telefonda denenir.
- **Derleme denetimi bir varsayımı kilitlemez.** "X olmamalı" diyen bir denetim, X'in çalışırken kullanılmadığının kanıtını belgesinde gösterir.
- **Telefonda kanıtı "Bekliyor" olan değişiklik mağazaya gitmez** (`docs/PHONE_TESTS.md`).

## Hata kaydı

Geç fark edilen her hata `docs/LESSONS.md`'ye eklenir: ne oldu, neden, neden yakalanmadı, bir daha olmasın diye konan kural ve denetim.
