// Fetches the paintings of the museum's rooms into the game (unity/Assets/Resources/Paintings/<room>/) and writes their
// licence list to art/paintings/<room>/licences.json (docs/GAME_DESIGN.md section 18: every image's source and licence
// is recorded).
// Every picture is the owning museum's own open-access photograph. Most are taken from Wikimedia Commons, where the
// museums put them: several museums answer scripts with a browser challenge, Commons serves its files to a named client.
// A few come from the museum's own image server, which serves them to a script.
//
// A download that is a JPEG in sRGB, no larger than the game shows and with nothing to cut away is kept byte for byte
// (rooms 1 and 2 are such). Any other is made into the game's picture by tools/unity/prepare-painting.py (Python with
// Pillow): it cuts the painting out of the download where the photograph also shows a frame edge, mount, colour chart or
// paper margin (the painting's crop box), scales it to LONGER_SIDE pixels on its longer side and writes a JPEG. The same
// download and box always give the same bytes. Downloads are kept in out/painting-sources/<room>/ (not in git), so a
// changed box needs no new download.
//
// usage (from the repository root): node tools/unity/fetch-paintings.js
// Add a room by adding its paintings to ROOMS. A picture that is already there is neither fetched nor made again:
// delete it to make it anew (after changing its crop box, say).
const fs = require("fs");
const path = require("path");
const { execFileSync } = require("child_process");

const USER_AGENT = "PuzzleGameDev/1.0 (museum-themed game; fetches a handful of public-domain paintings)";
/** The game's pictures are this many pixels on their longer side, or fewer where the museum's file is smaller. */
const LONGER_SIDE = 1920;
/** One request at a time, with this pause after each. */
const PAUSE_MS = 1500;
const DOWNLOADS = path.join("out", "painting-sources");
const PYTHON = process.env.PYTHON ?? (process.platform === "win32" ? "py" : "python3");
const PREPARE = path.join("tools", "unity", "prepare-painting.py");

/** The museums whose photographs hang in the game, each with the licence note of its photographs in one sentence. */
const ART_INSTITUTE = { name: "Art Institute of Chicago", licence: "Public domain work; the museum's photograph is released under CC0 (Art Institute of Chicago open access)" };
const MET = { name: "The Metropolitan Museum of Art", licence: "Public domain work; the museum's photograph is released under CC0 (The Metropolitan Museum of Art Open Access)" };
const NGA = { name: "National Gallery of Art", licence: "Public domain work; the museum's photograph is released under CC0 (National Gallery of Art open access)" };
const GETTY = { name: "J. Paul Getty Museum", licence: "Public domain work; the museum marks its photograph CC0 (J. Paul Getty Museum Open Content)" };
const SKOKLOSTER = { name: "Skokloster Castle", licence: "Public domain work; the museum marks its photograph with the Public Domain Mark, free to use in every way (Skoklosters slott/SHM)" };
const RIJKSMUSEUM = { name: "Rijksmuseum", licence: "Public domain work; the museum marks its photograph with the Public Domain Mark, free for any use, commercial included (Rijksmuseum)" };
const CLEVELAND = { name: "Cleveland Museum of Art", licence: "Public domain work; the museum's photograph is released under CC0 (Cleveland Museum of Art Open Access)" };
const SMK = { name: "Statens Museum for Kunst", licence: "Public domain work; the museum has waived its rights in the photograph, which carries the Public Domain Mark (SMK Open)" };
const NATIONALMUSEUM = { name: "Nationalmuseum", licence: "Public domain work; the museum's photograph carries the Public Domain Mark and may be used in any way (Nationalmuseum)" };
const FINNISH_NATIONAL_GALLERY = { name: "Finnish National Gallery", licence: "Public domain work; the museum's photograph is released under CC0 (Finnish National Gallery open data)" };

/** The museum that owns a painting, its page for the painting, and the licence of its photograph. */
const at = (museum, museumPage) => ({ museum: museum.name, museumPage, licence: museum.licence });
const artInstitute = id => at(ART_INSTITUTE, `https://www.artic.edu/artworks/${id}`);
/** The part of a download that is the painting: its left, top, right and bottom edges as fractions of the download's width and height. */
const box = (left, top, right, bottom) => ({ left, top, right, bottom });
const WHOLE = box(0, 0, 1, 1);

// A painting: file (its picture's name in the game), title, titleTr, artist, year (as the plate writes it), the museum
// (at), and where its picture comes from: commons, a file on Wikimedia Commons asked for at `width` pixels wide (at its
// own size when no width is given; Commons answers with its nearest standard width at or above the one asked), or
// download, an address on the owning museum's own image server. crop (optional): the part kept, see box().
// The width asked is the smallest that leaves the kept part at least 95 % of LONGER_SIDE on its longer side.
const ROOMS = {
  "room-01": [
    { file: "beach-at-sainte-adresse.jpg", title: "The Beach at Sainte-Adresse", titleTr: "Sainte-Adresse Plajı", artist: "Claude Monet", year: "1867",
      ...artInstitute(14598), commons: "Claude Monet - The Beach at Sainte-Adresse - 1933.439 - Art Institute of Chicago.jpg", width: 1920 },
    { file: "two-sisters-on-the-terrace.jpg", title: "Two Sisters (On the Terrace)", titleTr: "Kız Kardeş (Terasta)", artist: "Pierre-Auguste Renoir", year: "1881",
      ...artInstitute(14655), commons: "Pierre-Auguste Renoir - Two Sisters (On the Terrace) - 1933.455 - Art Institute of Chicago.jpg", width: 1280 },
    { file: "paris-street-rainy-day.jpg", title: "Paris Street; Rainy Day", titleTr: "Yağmurlu Gün", artist: "Gustave Caillebotte", year: "1877",
      ...artInstitute(20684), commons: "Gustave Caillebotte - Paris Street, Rainy Day - 1964.336 - Art Institute of Chicago.jpg", width: 1920 },
    { file: "water-lilies.jpg", title: "Water Lilies", titleTr: "Nilüferler", artist: "Claude Monet", year: "1906",
      ...artInstitute(16568), commons: "Claude Monet - Water Lilies - 1933.1157 - Art Institute of Chicago.jpg", width: 1920 },
    { file: "stacks-of-wheat-end-of-summer.jpg", title: "Stacks of Wheat (End of Summer)", titleTr: "Buğday Yığınları (Yaz Sonu)", artist: "Claude Monet", year: "1890–91",
      ...artInstitute(64818), commons: "Claude Monet - Stacks of Wheat (End of Summer) - 1985.1103 - Art Institute of Chicago.jpg", width: 1920 },
  ],
  "room-02": [
    { file: "van-gogh-self-portrait.jpg", title: "Self-Portrait", titleTr: "Otoportre", artist: "Vincent van Gogh", year: "1887",
      ...artInstitute(80607), commons: "Vincent van Gogh - Self-Portrait - 1954.326 - Art Institute of Chicago.jpg", width: 1280 },
    { file: "basket-of-apples.jpg", title: "The Basket of Apples", titleTr: "Elma Sepeti", artist: "Paul Cézanne", year: "y. 1893",
      ...artInstitute(111436), commons: "Paul Cézanne - The Basket of Apples - 1926.252 - Art Institute of Chicago.jpg", width: 1920 },
    { file: "at-the-moulin-rouge.jpg", title: "At the Moulin Rouge", titleTr: "Moulin Rouge'da", artist: "Henri de Toulouse-Lautrec", year: "1892–95",
      ...artInstitute(61128), commons: "Henri de Toulouse-Lautrec - At the Moulin Rouge - 1928.610 - Art Institute of Chicago.jpg", width: 1920 },
    { file: "the-bedroom.jpg", title: "The Bedroom", titleTr: "Yatak Odası", artist: "Vincent van Gogh", year: "1889",
      ...artInstitute(28560), commons: "Vincent van Gogh - The Bedroom - 1926.417 - Art Institute of Chicago.jpg", width: 1920 },
    { file: "sunday-on-la-grande-jatte.jpg", title: "A Sunday on La Grande Jatte — 1884", titleTr: "Grande Jatte Adası'nda Bir Pazar Öğleden Sonrası", artist: "Georges Seurat", year: "1884–86",
      ...artInstitute(27992), commons: "Georges Seurat - A Sunday on La Grande Jatte -- 1884 - Google Art Project.jpg", width: 1920,
      licence: "Public domain work; the photograph is Google Arts & Culture's faithful reproduction of it (public domain), taken from Wikimedia Commons." },
  ],
  // Research room 20: Northern Light: Sweden, Finland, Denmark
  "room-03": [
    { file: "apple-tree-in-blossom.jpg", title: "Apple Tree in Blossom", titleTr: "Çiçek Açmış Elma Ağacı", artist: "Carl Fredrik Hill", year: "1877",
      ...at(NATIONALMUSEUM, "https://collection.nationalmuseum.se/en/collection/item/18868/"), commons: "Apple Tree in Blossom (Carl Fredrik Hill) - Nationalmuseum - 18868.tif", width: 1920, crop: box(0.003, 0.004, 0.997, 0.997) },
    { file: "breton-girl-in-the-hothouse.jpg", title: "Breton Girl Looking After Plants in the Hothouse", titleTr: "Serada Bitkilerle İlgilenen Bretonyalı Kız", artist: "Anna Petersen", year: "1884",
      ...at(SMK, "https://open.smk.dk/artwork/image/KMS8289"), commons: "Anna Petersen, Bretagne-pige ordner planter i et drivhus, 1884, KMS8289, Statens Museum for Kunst.jpg", width: 1920 },
    { file: "luxembourg-gardens.jpg", title: "The Luxembourg Gardens, Paris", titleTr: "Paris'te Luxembourg Bahçeleri", artist: "Albert Edelfelt", year: "1887",
      ...at(FINNISH_NATIONAL_GALLERY, "https://www.kansallisgalleria.fi/en/object/387173"), commons: "Albert Edelfelt (1854–1905)- The Luxembourg Gardens, Paris - Pariisin Luxembourgin puistossa (29614654616).jpg", width: 1920 },
    { file: "midsummer-dance.jpg", title: "Midsummer Dance", titleTr: "Yaz Dönümü Dansı", artist: "Anders Zorn", year: "1897",
      ...at(NATIONALMUSEUM, "https://collection.nationalmuseum.se/en/collection/item/18607/"), commons: "Midsummer Dance (Anders Zorn) - Nationalmuseum - 18607.tif", width: 1920 },
    { file: "breakfast-under-the-big-birch.jpg", title: "Breakfast under the Big Birch (from A Home)", titleTr: "Büyük Huş Ağacının Altında Kahvaltı", artist: "Carl Larsson", year: "1895",
      ...at(NATIONALMUSEUM, "https://collection.nationalmuseum.se/en/collection/item/24220/"), commons: "Breakfast under the Big Birch. From A Home (26 watercolours) (Carl Larsson) - Nationalmuseum - 24220.tif", width: 1920, crop: box(0.01, 0.01, 0.99, 0.99) },
  ],
  // Research room 19: America: The Gilded Age
  "room-04": [
    { file: "the-dinner-horn.jpg", title: "The Dinner Horn (Blowing the Horn at Seaside)", titleTr: "Yemek Borusu", artist: "Winslow Homer", year: "1870",
      ...at(NGA, "https://purl.org/nga/collection/artobject/89664"), commons: "Winslow Homer, The Dinner Horn (Blowing the Horn at Seaside), 1870, NGA 89664.jpg", width: 1920 },
    { file: "champion-single-sculls.jpg", title: "The Champion Single Sculls (Max Schmitt in a Single Scull)", titleTr: "Tek Çifte Şampiyonu (Tek Çiftede Max Schmitt)", artist: "Thomas Eakins", year: "1871",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/10819"), commons: "The Champion Single Sculls (Max Schmitt in a Single Scull) MET DT86.jpg", width: 1920, crop: box(0.015, 0.022, 0.988, 0.982) },
    { file: "setting-out-to-fish.jpg", title: "En route pour la pêche (Setting Out to Fish)", titleTr: "Balığa Çıkarken", artist: "John Singer Sargent", year: "1878",
      ...at(NGA, "https://purl.org/nga/collection/artobject/166472"), commons: "John Singer Sargent, En route pour la pêche (Setting Out to Fish), 1878, NGA 166472.jpg", width: 1920 },
    { file: "a-friendly-call.jpg", title: "A Friendly Call", titleTr: "Dost Ziyareti", artist: "William Merritt Chase", year: "1895",
      ...at(NGA, "https://purl.org/nga/collection/artobject/3205"), commons: "William Merritt Chase, A Friendly Call, 1895, NGA 3205.jpg", width: 1920 },
    { file: "snap-the-whip.jpg", title: "Snap the Whip", titleTr: "Kırbaç Oyunu", artist: "Winslow Homer", year: "1872",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/11140"), commons: "Snap the Whip MET DT1395.jpg", crop: box(0.06, 0.024, 0.986, 0.974) },
  ],
  // Research room 18: The Netherlands: The Hague School and Amsterdam
  "room-05": [
    { file: "zuiderhavendijk-enkhuizen.jpg", title: "The Zuiderhavendijk, Enkhuizen", titleTr: "Enkhuizen'de Zuiderhavendijk", artist: "Cornelis Springer", year: "1868",
      ...at(RIJKSMUSEUM, "https://www.rijksmuseum.nl/nl/collectie/object/SK-A-2388--a84a20a4ce29168b6de07bb377e6e2f8"), commons: "De Zuiderhavendijk in Enkhuizen Rijksmuseum SK-A-2388.jpeg", width: 1920 },
    { file: "girl-with-flowers-on-the-grass.jpg", title: "A Girl with Flowers on the Grass", titleTr: "Çimenlerde Çiçekli Kız", artist: "Jacob Maris", year: "1878",
      ...at(RIJKSMUSEUM, "https://www.rijksmuseum.nl/nl/collectie/object/SK-A-3680--2d9416ce8507baeb2d613e573fe88920"), commons: "Een meisje met bloemen in het gras Dochter van Jacob Maris met bloemen in het gras, SK-A-3680.jpg", width: 1280 },
    { file: "yellow-riders.jpg", title: "The Yellow Riders (Horse Artillery)", titleTr: "Sarı Süvariler (Atlı Topçular)", artist: "George Hendrik Breitner", year: "1885–86",
      ...at(RIJKSMUSEUM, "https://www.rijksmuseum.nl/nl/collectie/object/SK-A-1328--ca80fe396099a6b215dcde3d9ab94e66"), commons: "De Gele Rijders Rijksmuseum SK-A-1328.jpeg", width: 1280, crop: box(0, 0.003, 1, 1) },
    { file: "in-the-month-of-july.jpg", title: "In the Month of July (A Windmill on a Polder Waterway)", titleTr: "Temmuz Ayında (Polder Kanalında Bir Yel Değirmeni)", artist: "Paul Joseph Constantin Gabriël", year: "y. 1889",
      ...at(RIJKSMUSEUM, "https://www.rijksmuseum.nl/nl/collectie/object/SK-A-1505--3e97d6f21dd17609968549d9d3b76922"), commons: "Een molen aan een poldervaart, bekend als ‘In de maand juli’, SK-A-1505.jpg", width: 1280, crop: box(0, 0, 1, 0.995) },
    { file: "morning-ride-along-the-beach.jpg", title: "Morning Ride along the Beach", titleTr: "Sahilde Sabah At Gezintisi", artist: "Anton Mauve", year: "1876",
      ...at(RIJKSMUSEUM, "https://www.rijksmuseum.nl/nl/collectie/object/SK-A-3602--6102580b0dc8656aa3ce497c8395c93d"), commons: "Morgenrit langs het strand Rijksmuseum SK-A-3602.jpeg", width: 1920 },
  ],
  // Research room 17: Japan: The Last Years of Edo and Meiji
  "room-06": [
    { file: "cats-of-the-tokaido.jpg", title: "Cats Suggested as the Fifty-three Stations of the Tokaido (the last sheet of three)", titleTr: "Tokaido'nun Elli Üç Konağı Olarak Kediler (üç yapraktan sonuncusu)", artist: "Utagawa Kuniyoshi", year: "1847–50",
      ...at(RIJKSMUSEUM, "https://www.rijksmuseum.nl/en/collection/object/AK-MAK-909--8819cf0d15c4bbaac6a30634e3007dc2"), commons: "Katten voor de 53 stations van de Tokaido, AK-MAK-909.jpg", width: 1920, crop: box(0.034, 0.034, 0.868, 0.968) },
    { file: "plum-park-in-kameido.jpg", title: "Plum Park in Kameido, from One Hundred Famous Views of Edo", titleTr: "Kameido'da Erik Bahçesi (Edo'nun Yüz Ünlü Manzarası)", artist: "Utagawa Hiroshige", year: "1857",
      ...at(RIJKSMUSEUM, "https://www.rijksmuseum.nl/en/collection/object/RP-P-1956-743--835003111f8a3f0b1d1e6532cad2d416"), commons: "De pruimenboomgaard te Kameido Kameido umeyashiki (titel op object) Honderd beroemde gezichten op Edo (serietitel) Meisho Edo hyakkei (serietitel op object), RP-P-1956-743.jpg", width: 1920, crop: box(0.054, 0.058, 0.944, 0.967) },
    { file: "snow-at-ochanomizu.jpg", title: "Snow at Ochanomizu", titleTr: "Ochanomizu'da Kar", artist: "Kobayashi Kiyochika", year: "1880",
      ...at(RIJKSMUSEUM, "https://www.rijksmuseum.nl/en/collection/object/RP-P-1988-283--b050562994c3f7fca270b77070ff6f5e"), commons: "Sneeuw te Ochanomizu Chanomizu yuki (titel op object), RP-P-1988-283.jpg", width: 3840, crop: box(0.048, 0.121, 0.954, 0.945) },
    { file: "moon-of-musashi-plain.jpg", title: "Moon of Musashi Plain, from One Hundred Aspects of the Moon", titleTr: "Musashi Ovası'nda Ay (Ayın Yüz Görünümü)", artist: "Tsukioka Yoshitoshi", year: "1891",
      ...at(RIJKSMUSEUM, "https://www.rijksmuseum.nl/en/collection/object/RP-P-1994-70--e1a09c972ea9726ef2e00044f304ef5e"), commons: "Maan van Musashi Musashino no tsuki (titel op object) Honderd aspecten van de maan (serietitel) Tsuki hyakushi (serietitel op object), RP-P-1994-70.jpg", width: 1920, crop: box(0.052, 0.077, 0.939, 0.955) },
    { file: "sudden-shower-over-shin-ohashi.jpg", title: "Sudden Shower over Shin-Ohashi Bridge and Atake, from One Hundred Famous Views of Edo", titleTr: "Shin-Ohashi Köprüsü ve Atake'de Ani Sağanak (Edo'nun Yüz Ünlü Manzarası)", artist: "Utagawa Hiroshige", year: "1857",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/37386"), commons: "Ōhashi Atake no yūdachi-名所江戸百景 大はしあたけの夕立-Sudden Shower over Shin-Ōhashi Bridge and Atake (Ōhashi Atake no yūdachi), from the series One Hundred Famous Views of Edo (Meisho Edo hyakkei) MET DP123602.jpg", width: 1920, crop: box(0.089, 0.072, 0.951, 0.947) },
  ],
  // Research room 16: France: Barbizon, Realism and the Salon
  "room-07": [
    { file: "young-ladies-of-the-village.jpg", title: "Young Ladies of the Village", titleTr: "Köyün Genç Hanımları", artist: "Gustave Courbet", year: "1851–52",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/438820"), commons: "Young Ladies of the Village MET DT1967.jpg", width: 1920, crop: box(0.004, 0.005, 0.996, 0.995) },
    { file: "festival-in-the-harbor-of-honfleur.jpg", title: "Festival in the Harbor of Honfleur", titleTr: "Honfleur Limanı'nda Şenlik", artist: "Eugène Boudin", year: "1858",
      ...at(NGA, "https://purl.org/nga/collection/artobject/61360"), commons: "Eugène Boudin, Festival in the Harbor of Honfleur, 1858, NGA 61360.jpg", width: 1920 },
    { file: "fantin-latour-still-life.jpg", title: "Still Life with Flowers and Fruit", titleTr: "Çiçekli ve Meyveli Natürmort", artist: "Henri Fantin-Latour", year: "1866",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/436293"), commons: "Henri Fantin-Latour - Still Life with Flowers and Fruit MET DT1980.jpg", width: 1920, crop: box(0.024, 0.019, 0.974, 0.99) },
    { file: "weaning-the-calves.jpg", title: "Weaning the Calves", titleTr: "Buzağıların Sütten Kesilmesi", artist: "Rosa Bonheur", year: "1879",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/435703"), commons: "Weaning the Calves MET DT2579.jpg", width: 1920, crop: box(0.016, 0.018, 0.985, 0.974) },
    { file: "princesse-de-broglie.jpg", title: "Princesse de Broglie", titleTr: "Prenses de Broglie", artist: "Jean-Auguste-Dominique Ingres", year: "1851–53",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/459106"), commons: "Joséphine-Éléonore-Marie-Pauline de Galard de Brassac de Béarn (1825–1860), Princesse de Broglie MET DT717.jpg", width: 1920, crop: box(0.014, 0.01, 0.982, 0.99) },
  ],
  // Research room 15: America: The Hudson River and the Frontier
  "room-08": [
    { file: "fur-traders-descending-the-missouri.jpg", title: "Fur Traders Descending the Missouri", titleTr: "Missouri'den Aşağı İnen Kürk Tüccarları", artist: "George Caleb Bingham", year: "1845",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/10159"), commons: "Fur Traders Descending the Missouri MET DT73.jpg", width: 1920, crop: box(0.018, 0.017, 0.983, 0.98) },
    { file: "cornell-farm.jpg", title: "The Cornell Farm", titleTr: "Cornell Çiftliği", artist: "Edward Hicks", year: "1848",
      ...at(NGA, "https://purl.org/nga/collection/artobject/50261"), commons: "Edward Hicks, The Cornell Farm, 1848, NGA 50261.jpg", width: 1920 },
    { file: "merced-river-yosemite.jpg", title: "Merced River, Yosemite Valley", titleTr: "Merced Nehri, Yosemite Vadisi", artist: "Albert Bierstadt", year: "1866",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/10150"), commons: "Merced River, Yosemite Valley MET DT2961.jpg", width: 1920, crop: box(0.012, 0.016, 0.992, 0.986) },
    { file: "the-oxbow.jpg", title: "The Oxbow (View from Mount Holyoke, Northampton, Massachusetts, after a Thunderstorm)", titleTr: "Oxbow: Fırtınadan Sonra Holyoke Dağı'ndan Görünüm", artist: "Thomas Cole", year: "1836",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/10497"), commons: "View from Mount Holyoke, Northampton, Massachusetts, after a Thunderstorm—The Oxbow MET DT71.jpg", crop: box(0.01, 0.012, 0.99, 0.99) },
    { file: "washington-crossing-the-delaware.jpg", title: "Washington Crossing the Delaware", titleTr: "Washington Delaware Nehri'ni Geçerken", artist: "Emanuel Leutze", year: "1851",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/11417"), commons: "Washington Crossing the Delaware MET DP215410.jpg", crop: box(0.06, 0.011, 0.969, 0.982) },
  ],
  // Research room 14: The Danish Golden Age
  "room-09": [
    { file: "view-from-the-artists-window.jpg", title: "View from the Artist's Window", titleTr: "Sanatçının Penceresinden Görünüm", artist: "Martinus Rørbye", year: "1823–27",
      ...at(SMK, "https://open.smk.dk/artwork/image/KMS7452"), commons: "Martinus Rørbye, Udsigt fra kunstnerens vindue, , KMS7452, Statens Museum for Kunst.jpg", width: 1920 },
    { file: "street-in-osterbro.jpg", title: "A View of a Street in Østerbro outside Copenhagen. Morning Light", titleTr: "Sabah Işığında Østerbro'da Bir Sokak", artist: "Christen Købke", year: "1836",
      ...at(SMK, "https://open.smk.dk/artwork/image/KMS844"), commons: "Christen Købke, Parti af Østerbro i morgenbelysning, 1836, KMS844, Statens Museum for Kunst.jpg", width: 1920, crop: box(0.004, 0.004, 0.996, 0.997) },
    { file: "danish-artists-in-rome.jpg", title: "A Group of Danish Artists in Rome", titleTr: "Roma'da Bir Grup Danimarkalı Sanatçı", artist: "Constantin Hansen", year: "1837",
      ...at(SMK, "https://open.smk.dk/artwork/image/KMS3236"), commons: "Constantin Hansen, Et selskab af danske kunstnere i Rom, 1837, KMS3236, Statens Museum for Kunst.jpg", width: 1920, crop: box(0.002, 0.002, 0.995, 0.996) },
    { file: "beech-wood-in-may.jpg", title: "A Beech Wood in May near Iselingen Manor, Zealand", titleTr: "Mayıs'ta Kayın Ormanı", artist: "P. C. Skovgaard", year: "1857",
      ...at(SMK, "https://open.smk.dk/artwork/image/KMS4580"), commons: "PC Skovgaard, Bøgeskov i maj Motiv fra Iselingen, 1857, KMS4580, Statens Museum for Kunst.jpg", width: 1920 },
    { file: "three-arches-of-the-colosseum.jpg", title: "A View through Three Arches of the Third Storey of the Colosseum", titleTr: "Kolezyum'un Üçüncü Katındaki Üç Kemerden Görünüm", artist: "Christoffer Wilhelm Eckersberg", year: "1815",
      ...at(SMK, "https://open.smk.dk/artwork/image/KMS3123"), commons: "CW Eckersberg, Udsigt gennem tre buer i Colosseums tredje stokværk, 1815, KMS3123, Statens Museum for Kunst.jpg", width: 1920, crop: box(0.006, 0.006, 0.99, 0.994) },
  ],
  // Research room 13: Romantic Britain: Constable and Turner
  "room-10": [
    { file: "salisbury-cathedral.jpg", title: "Salisbury Cathedral from the Bishop's Grounds", titleTr: "Piskoposun Bahçesinden Salisbury Katedrali", artist: "John Constable", year: "y. 1825",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/435922"), commons: "Salisbury Cathedral from the Bishop's Grounds MET DP164837.jpg", width: 1920, crop: box(0.014, 0.013, 0.99, 0.985) },
    { file: "lady-templetown-and-her-son.jpg", title: "Lady Mary Templetown and Her Eldest Son", titleTr: "Leydi Mary Templetown ve Büyük Oğlu", artist: "Sir Thomas Lawrence", year: "1802",
      ...at(NGA, "https://purl.org/nga/collection/artobject/103"), commons: "Sir Thomas Lawrence, Lady Mary Templetown and Her Eldest Son, 1802, NGA 103.jpg", width: 1280 },
    { file: "mortlake-terrace.jpg", title: "Mortlake Terrace", titleTr: "Mortlake Terası", artist: "Joseph Mallord William Turner", year: "1827",
      ...at(NGA, "https://purl.org/nga/collection/artobject/116"), commons: "Joseph Mallord William Turner, Mortlake Terrace, 1827, NGA 116.jpg", width: 1920 },
    { file: "bonington-grand-canal.jpg", title: "The Grand Canal", titleTr: "Büyük Kanal, Venedik", artist: "Richard Parkes Bonington", year: "1826–27",
      ...at(NGA, "https://purl.org/nga/collection/artobject/119184"), commons: "Richard Parkes Bonington, The Grand Canal, 1826-1827, NGA 119184.jpg", width: 1920 },
    { file: "burning-of-the-houses-of-parliament.jpg", title: "The Burning of the Houses of Lords and Commons, 16 October 1834", titleTr: "Lordlar ve Avam Kamaralarının Yanışı, 16 Ekim 1834", artist: "Joseph Mallord William Turner", year: "1835",
      ...at(CLEVELAND, "https://www.clevelandart.org/art/1942.647"), commons: "Joseph Mallord William Turner - The Burning of the Houses of Lords and Commons, 16 October 1834 - 1942.647 - Cleveland Museum of Art.tiff", width: 1920 },
  ],
  // Research room 12: Edo Japan: Hokusai and Hiroshige
  "room-11": [
    { file: "otani-oniji-as-yakko-edobei.jpg", title: "Kabuki Actor Otani Oniji III as Yakko Edobei", titleTr: "Kabuki Oyuncusu III. Otani Oniji, Uşak Edobei Rolünde", artist: "Tōshūsai Sharaku", year: "1794",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/37358"), commons: "三代目大谷鬼次の奴江戸兵衛-Kabuki Actor Ōtani Oniji III as Yakko Edobei in the Play The Colored Reins of a Loving Wife (Koi nyōbō somewake tazuna) MET DP130228.jpg", width: 1280, crop: box(0.031, 0.013, 0.968, 0.985) },
    { file: "red-fuji.jpg", title: "South Wind, Clear Sky (Red Fuji), from Thirty-six Views of Mount Fuji", titleTr: "Güney Rüzgârı, Açık Hava (Kızıl Fuji)", artist: "Katsushika Hokusai", year: "y. 1830–32",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/55736"), commons: "冨嶽三十六景 凱風快晴-South Wind, Clear Sky (Gaifū kaisei), also known as Red Fuji, from the series Thirty-six Views of Mount Fuji (Fugaku sanjūrokkei) MET DP141053.jpg", width: 1920, crop: box(0.022, 0.033, 0.976, 0.965) },
    { file: "hakone-lake.jpg", title: "Hakone: Picture of the Lake, from Fifty-three Stations of the Tokaido", titleTr: "Hakone: Göl Manzarası (Tokaido'nun Elli Üç Konağı)", artist: "Utagawa Hiroshige", year: "1833",
      ...at(CLEVELAND, "https://www.clevelandart.org/art/1940.982"), commons: "Ando Hiroshige - Picture of the Lake at Hakone (from the series 53 Stations of the Tokaido) - 1940.982 - Cleveland Museum of Art.tif", width: 3840, crop: box(0.083, 0.064, 0.917, 0.938) },
    { file: "tsukuda-island.jpg", title: "Tsukuda Island, from Famous Places in the Eastern Capital", titleTr: "Tsukuda Adası (Doğu Başkentinin Ünlü Yerleri)", artist: "Utagawa Kuniyoshi", year: "1831–35",
      ...at(RIJKSMUSEUM, "https://www.rijksmuseum.nl/en/collection/RP-P-1956-729"), commons: "Het eiland Tsukuda Tsukudajima (titel op object) Beroemde plaatsen in de Oostelijke hoofdstad (serietitel) Toto meisho (serietitel op object), RP-P-1956-729.jpg", width: 1920, crop: box(0.02, 0.026, 0.973, 0.97) },
    { file: "great-wave.jpg", title: "Under the Wave off Kanagawa (The Great Wave), from Thirty-six Views of Mount Fuji", titleTr: "Kanagawa Açıklarında Büyük Dalga", artist: "Katsushika Hokusai", year: "y. 1830–32",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/36491"), commons: "冨嶽三十六景 神奈川沖浪裏-Under the Wave off Kanagawa (Kanagawa oki nami ura), also known as The Great Wave, from the series Thirty-six Views of Mount Fuji (Fugaku sanjūrokkei) MET DP141063.jpg", width: 1920, crop: box(0.027, 0.036, 0.977, 0.968) },
  ],
  // Research room 11: The Age of Revolutions: America and Europe
  "room-12": [
    { file: "benjamin-and-eleanor-laming.jpg", title: "Benjamin and Eleanor Ridgely Laming", titleTr: "Benjamin ve Eleanor Ridgely Laming", artist: "Charles Willson Peale", year: "1788",
      ...at(NGA, "https://purl.org/nga/collection/artobject/50684"), commons: "Charles Willson Peale, Benjamin and Eleanor Ridgely Laming, 1788, NGA 50684.jpg", width: 1920 },
    { file: "washington-reviewing-the-western-army.jpg", title: "Washington Reviewing the Western Army at Fort Cumberland, Maryland", titleTr: "Washington, Maryland'deki Cumberland Kalesi'nde Batı Ordusu'nu Denetliyor", artist: "Attributed to Frederick Kemmelmeyer", year: "1795 sonrası",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/11302"), commons: "Washington Reviewing the Western Army at Fort Cumberland, Maryland MET DT2045.jpg", width: 1920, crop: box(0.01, 0.014, 0.99, 0.99) },
    { file: "general-gerard.jpg", title: "General Étienne-Maurice Gérard", titleTr: "General Étienne-Maurice Gérard", artist: "Jacques-Louis David", year: "1816",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/436107"), commons: "General Étienne-Maurice Gérard (1773–1852) MET DP-13141-001.jpg", width: 1920, crop: box(0.01, 0.01, 0.99, 0.99) },
    { file: "lady-with-a-harp.jpg", title: "Lady with a Harp: Eliza Ridgely", titleTr: "Arplı Kadın: Eliza Ridgely", artist: "Thomas Sully", year: "1818",
      ...at(NGA, "https://purl.org/nga/collection/artobject/32577"), commons: "Thomas Sully, Lady with a Harp - Eliza Ridgely, 1818, NGA 32577.jpg", width: 1280 },
    { file: "the-red-boy.jpg", title: "Manuel Osorio Manrique de Zuñiga (The Red Boy)", titleTr: "Manuel Osorio Manrique de Zúñiga (Kırmızılı Çocuk)", artist: "Francisco de Goya", year: "1787–88",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/436545"), commons: "Manuel Osorio Manrique de Zuñiga (1784–1792) MET DP287624.jpg", width: 1920, crop: box(0.01, 0.01, 0.986, 0.99) },
  ],
  // Research room 10: Georgian Britain
  "room-13": [
    { file: "maynard-family-at-waltons.jpg", title: "Members of the Maynard Family in the Park at Waltons", titleTr: "Maynard Ailesi Waltons Parkı'nda", artist: "Arthur Devis", year: "y. 1755–62",
      ...at(NGA, "https://purl.org/nga/collection/artobject/47182"), commons: "Arthur Devis, Members of the Maynard Family in the Park at Waltons, c. 1755-1762, NGA 47182.jpg", width: 1920 },
    { file: "duke-of-dorsets-hunter.jpg", title: "The Third Duke of Dorset's Hunter with a Groom and a Dog", titleTr: "Üçüncü Dorset Dükü'nün Av Atı, Seyis ve Köpekle", artist: "George Stubbs", year: "1768",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/437764"), commons: "The Third Duke of Dorset's Hunter with a Groom and a Dog MET DP164858.jpg", width: 1920, crop: box(0.01, 0.01, 0.99, 0.986) },
    { file: "lady-caroline-howard.jpg", title: "Lady Caroline Howard", titleTr: "Leydi Caroline Howard", artist: "Sir Joshua Reynolds", year: "1778",
      ...at(NGA, "https://purl.org/nga/collection/artobject/113"), commons: "Sir Joshua Reynolds, Lady Caroline Howard, 1778, NGA 113.jpg", width: 1920 },
    { file: "mrs-richard-brinsley-sheridan.jpg", title: "Mrs. Richard Brinsley Sheridan", titleTr: "Bayan Richard Brinsley Sheridan", artist: "Thomas Gainsborough", year: "1785–87",
      ...at(NGA, "https://purl.org/nga/collection/artobject/99"), commons: "Thomas Gainsborough, Mrs. Richard Brinsley Sheridan, 1785-1787, NGA 99.jpg", width: 1920 },
    { file: "elizabeth-farren.jpg", title: "Elizabeth Farren, Later Countess of Derby", titleTr: "Elizabeth Farren, Sonradan Derby Kontesi", artist: "Sir Thomas Lawrence", year: "1790",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/436851"), commons: "Elizabeth Farren (born about 1759, died 1829), Later Countess of Derby MET DP169218.jpg", width: 1280, crop: box(0.01, 0.028, 0.99, 0.99) },
  ],
  // Research room 9: City Views: Venice, Rome, Istanbul
  "room-14": [
    { file: "view-of-istanbul-from-pera.jpg", title: "View of Istanbul from the Dutch Embassy at Pera", titleTr: "Pera'daki Hollanda Elçiliği'nden İstanbul Görünümü", artist: "Jean Baptiste Vanmour", year: "y. 1720–37",
      ...at(RIJKSMUSEUM, "https://www.rijksmuseum.nl/en/collection/SK-A-4084"), commons: "Gezicht op Istanbul vanuit de Nederlandse ambassade in Pera, SK-A-4084.jpg", width: 1920 },
    { file: "interior-of-the-pantheon.jpg", title: "Interior of the Pantheon, Rome", titleTr: "Pantheon'un İçi, Roma", artist: "Giovanni Paolo Panini", year: "y. 1734",
      ...at(NGA, "https://purl.org/nga/collection/artobject/165"), commons: "Giovanni Paolo Panini, Interior of the Pantheon, Rome, c. 1734, NGA 165.jpg", width: 1920 },
    { file: "campo-santi-giovanni-e-paolo.jpg", title: "The Campo di SS. Giovanni e Paolo, Venice", titleTr: "Santi Giovanni e Paolo Meydanı, Venedik", artist: "Bernardo Bellotto", year: "1743–47",
      ...at(NGA, "https://www.nga.gov/artworks/1144-campo-di-ss-giovanni-e-paolo-venice"), commons: "Bernardo Bellotto, The Campo di SS. Giovanni e Paolo, Venice, 1743-1747, NGA 1144.jpg", width: 1920 },
    { file: "cannaregio-canal.jpg", title: "View on the Cannaregio Canal, Venice", titleTr: "Cannaregio Kanalı'ndan Görünüm, Venedik", artist: "Francesco Guardi", year: "y. 1775–80",
      ...at(NGA, "https://www.nga.gov/artworks/254-view-cannaregio-canal-venice"), commons: "Francesco Guardi, View on the Cannaregio Canal, Venice, c. 1775-1780, NGA 254.jpg", width: 1920 },
    { file: "square-of-saint-marks.jpg", title: "The Square of Saint Mark's, Venice", titleTr: "San Marco Meydanı, Venedik", artist: "Canaletto (Giovanni Antonio Canal)", year: "1742–44",
      ...at(NGA, "https://www.nga.gov/artworks/32588-square-saint-marks-venice"), commons: "Canaletto, The Square of Saint Mark's, Venice, 1742-1744, NGA 32588.jpg", width: 1920 },
  ],
  // Research room 8: Rococo France
  "room-15": [
    { file: "italian-comedians.jpg", title: "The Italian Comedians", titleTr: "İtalyan Komedyenler", artist: "Antoine Watteau", year: "muhtemelen 1720",
      ...at(NGA, "https://www.nga.gov/artworks/32687-italian-comedians"), commons: "Antoine Watteau, The Italian Comedians, probably 1720, NGA 32687.jpg", width: 1920 },
    { file: "misse-and-luttine.jpg", title: "Misse and Luttine", titleTr: "Misse ile Luttine", artist: "Jean-Baptiste Oudry", year: "1729",
      ...at(NGA, "https://www.nga.gov/artworks/89604-misse-and-luttine"), commons: "Jean-Baptiste Oudry, Misse and Luttine, 1729, NGA 89604.jpg", width: 1920 },
    { file: "madame-bergeret.jpg", title: "Madame Bergeret", titleTr: "Madam Bergeret", artist: "François Boucher", year: "belki 1766",
      ...at(NGA, "https://www.nga.gov/artworks/32681-madame-bergeret"), commons: "François Boucher, Madame Bergeret, possibly 1766, NGA 32681.jpg", width: 1920 },
    { file: "ponte-salario.jpg", title: "The Ponte Salario", titleTr: "Salario Köprüsü", artist: "Hubert Robert", year: "y. 1775",
      ...at(NGA, "https://www.nga.gov/artworks/41665-ponte-salario"), commons: "Hubert Robert, The Ponte Salario, c. 1775, NGA 41665.jpg", width: 1920 },
    { file: "young-girl-reading.jpg", title: "Young Girl Reading", titleTr: "Kitap Okuyan Genç Kız", artist: "Jean Honoré Fragonard", year: "y. 1769",
      ...at(NGA, "https://www.nga.gov/artworks/46303-young-girl-reading"), commons: "Jean Honoré Fragonard, Young Girl Reading, c. 1769, NGA 46303.jpg", width: 1920 },
  ],
  // Research room 7: The Dutch Golden Age: Vermeer's Generation
  "room-16": [
    { file: "maas-at-dordrecht.jpg", title: "The Maas at Dordrecht", titleTr: "Dordrecht'te Maas Nehri", artist: "Aelbert Cuyp", year: "y. 1650",
      ...at(NGA, "https://www.nga.gov/artworks/576-maas-dordrecht"), commons: "Aelbert Cuyp, The Maas at Dordrecht, c. 1650, NGA 576.jpg", width: 1920 },
    { file: "threatened-swan.jpg", title: "The Threatened Swan", titleTr: "Tehdit Altındaki Kuğu", artist: "Jan Asselijn", year: "y. 1650",
      ...at(RIJKSMUSEUM, "https://www.rijksmuseum.nl/en/collection/SK-A-4"), commons: "De bedreigde zwaan Rijksmuseum SK-A-4.jpeg", width: 1920 },
    { file: "young-woman-with-a-water-pitcher.jpg", title: "Young Woman with a Water Pitcher", titleTr: "Su Sürahili Genç Kadın", artist: "Johannes Vermeer", year: "y. 1662",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/437881"), commons: "Young Woman with a Water Pitcher MET DP353257.jpg", width: 1920, crop: box(0.027, 0.025, 0.973, 0.975) },
    { file: "ruisdael-wheat-fields.jpg", title: "Wheat Fields", titleTr: "Buğday Tarlaları", artist: "Jacob van Ruisdael", year: "y. 1670",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/437549"), commons: "Wheat Fields MET DP145911.jpg", width: 1920, crop: box(0.011, 0.022, 0.985, 0.981) },
    { file: "the-little-street.jpg", title: "The Little Street (View of Houses in Delft)", titleTr: "Küçük Sokak (Delft'te Evler)", artist: "Johannes Vermeer", year: "y. 1658",
      ...at(RIJKSMUSEUM, "https://www.rijksmuseum.nl/en/collection/SK-A-2860"), commons: "Gezicht op huizen in Delft, bekend als ‘Het straatje’ Gezicht op huizen in Delft, bekend als 'Het straatje', SK-A-2860.jpg", width: 1920, crop: box(0.006, 0.007, 0.988, 0.99) },
  ],
  // Research room 6: Baroque Europe: Holland, Flanders and France
  "room-17": [
    { file: "brueghel-river-landscape.jpg", title: "River Landscape", titleTr: "Nehir Manzarası", artist: "Jan Brueghel the Elder", year: "1607",
      ...at(NGA, "https://www.nga.gov/artworks/111124-river-landscape"), commons: "Jan Brueghel the Elder, River Landscape, 1607, NGA 111124.jpg", width: 1920 },
    { file: "banquet-piece-with-mince-pie.jpg", title: "Banquet Piece with Mince Pie", titleTr: "Kıymalı Turtalı Ziyafet Sofrası", artist: "Willem Claesz Heda", year: "1635",
      ...at(NGA, "https://www.nga.gov/artworks/72869-banquet-piece-mince-pie"), commons: "Willem Claesz Heda, Banquet Piece with Mince Pie, 1635, NGA 72869.jpg", width: 1920 },
    { file: "andries-stilte-as-a-standard-bearer.jpg", title: "Andries Stilte as a Standard Bearer", titleTr: "Sancaktar Andries Stilte", artist: "Johannes Cornelisz Verspronck", year: "1640",
      ...at(NGA, "https://www.nga.gov/artworks/104729-andries-stilte-standard-bearer"), commons: "Johannes Cornelisz Verspronck, Andries Stilte as a Standard Bearer, 1640, NGA 104729.jpg", width: 1920 },
    { file: "trojan-women-setting-fire-to-their-fleet.jpg", title: "The Trojan Women Setting Fire to Their Fleet", titleTr: "Troyalı Kadınlar Gemilerini Ateşe Veriyor", artist: "Claude Lorrain (Claude Gellée)", year: "y. 1643",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/435908"), commons: "The Trojan Women Setting Fire to Their Fleet MET DP226145.jpg", width: 1920, crop: box(0.016, 0.02, 0.986, 0.974) },
    { file: "leyster-self-portrait.jpg", title: "Self-Portrait", titleTr: "Otoportre", artist: "Judith Leyster", year: "y. 1630",
      ...at(NGA, "https://www.nga.gov/artworks/37003-self-portrait"), commons: "Judith Leyster, Self-Portrait, c. 1630, NGA 37003.jpg", width: 1920 },
  ],
  // Research room 5: Persian and Mughal Courts: The Age of the Miniature
  "room-18": [
    { file: "feast-of-sada.jpg", title: "The Feast of Sada (from the Shahnama of Shah Tahmasp)", titleTr: "Sede Bayramı (Şah Tahmasb Şehnamesi'nden)", artist: "Attributed to Sultan Muhammad (Tabriz, Iran)", year: "y. 1525",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/452111"), commons: "\"The Feast of Sada\", Folio 22v from the Shahnama (Book of Kings) of Shah Tahmasp MET DP107689.jpg", crop: box(0.09, 0.07, 0.75, 0.643) },
    { file: "concourse-of-the-birds.jpg", title: "The Concourse of the Birds", titleTr: "Kuşların Meclisi (Mantıku't-Tayr'dan)", artist: "Habiballah of Sava", year: "y. 1600",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/451725"), commons: "\"The Concourse of the Birds\", Folio 11r from a Mantiq al-tair (Language of the Birds) MET DT462.jpg", crop: box(0.343, 0.243, 0.841, 0.778) },
    { file: "study-of-a-nilgai.jpg", title: "Study of a Nilgai (Blue Bull)", titleTr: "Nilgay (Mavi Boğa) Etüdü", artist: "Mansur", year: "y. 1620",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/451261"), commons: "\"Study of a Nilgai (Blue Bull)\", Folio from the Shah Jahan Album MET DP240655.jpg", width: 3840, crop: box(0.034, 0.03, 0.974, 0.96) },
    { file: "jahangir-and-akbar.jpg", title: "Jahangir and His Father, Akbar", titleTr: "Cihangir ve Babası Ekber", artist: "Balchand", year: "y. 1630",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/451264"), commons: "\"Jahangir and his Father, Akbar\", Folio from the Shah Jahan Album MET DP240813.jpg", width: 1920, crop: box(0.037, 0.026, 0.961, 0.97) },
    { file: "shah-jahan-on-horseback.jpg", title: "Shah Jahan on Horseback", titleTr: "At Üstünde Şah Cihan", artist: "Payag", year: "y. 1630",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/451267"), commons: "\"Shah Jahan on Horseback\", Folio from the Shah Jahan Album MET DT1030.jpg", width: 1280, crop: box(0.034, 0.021, 0.964, 0.978) },
  ],
  // Research room 4: Renaissance Europe: Holbein to Arcimboldo
  "room-19": [
    { file: "cariani-a-concert.jpg", title: "A Concert", titleTr: "Konser", artist: "Cariani (Giovanni Busi)", year: "y. 1518–20",
      ...at(NGA, "https://purl.org/nga/collection/artobject/90793"), commons: "Cariani, A Concert, c. 1518-1520, NGA 90793.jpg", width: 1920 },
    { file: "edward-vi-as-a-child.jpg", title: "Edward VI as a Child", titleTr: "Çocukken VI. Edward", artist: "Hans Holbein the Younger", year: "muhtemelen 1538",
      ...at(NGA, "https://purl.org/nga/collection/artobject/71"), commons: "Hans Holbein the Younger, Edward VI as a Child, probably 1538, NGA 71.jpg", width: 1920 },
    { file: "the-librarian.jpg", title: "The Librarian", titleTr: "Kütüphaneci", artist: "Giuseppe Arcimboldo", year: "y. 1562–66",
      ...at(SKOKLOSTER, "https://samlingar.shm.se/object/465F670E-FE9D-4AF7-9C45-7DE92E39EB9A"), download: "https://media.samlingar.shm.se/item/6374F08D-58CA-4752-A9A6-04F3D4ED3343", crop: box(0.013, 0.01, 0.988, 0.99) },
    { file: "marketplace-in-bergen-op-zoom.jpg", title: "The Marketplace in Bergen op Zoom", titleTr: "Bergen op Zoom'da Pazar Yeri", artist: "Attributed to Abel Grimmer", year: "muhtemelen 1590 ve 1597",
      ...at(NGA, "https://purl.org/nga/collection/artobject/57418"), commons: "Attributed to Abel Grimmer, The Marketplace in Bergen op Zoom, probably 1590 and 1597, NGA 57418.jpg", width: 1920, crop: box(0.004, 0.004, 0.996, 0.996) },
    { file: "vertumnus.jpg", title: "Vertumnus (Emperor Rudolf II as Vertumnus)", titleTr: "Vertumnus (Vertumnus Olarak İmparator II. Rudolf)", artist: "Giuseppe Arcimboldo", year: "1591",
      ...at(SKOKLOSTER, "https://samlingar.shm.se/object/BD10473C-4D1B-4D89-82D9-4ADD890B7941"), download: "https://media.samlingar.shm.se/item/3A2122A1-3181-45E6-B2B5-B8B2C0259EE9", crop: box(0.005, 0.008, 0.995, 0.995) },
  ],
  // Research room 3: Flanders and Italy: The Fifteenth Century
  "room-20": [
    { file: "francesco-d-este.jpg", title: "Francesco d'Este", titleTr: "Francesco d'Este", artist: "Rogier van der Weyden", year: "y. 1460",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/437487"), commons: "Francesco d'Este (born about 1430, died after 1475) MET 83.jpg", crop: box(0.2, 0.15, 0.805, 0.823) },
    { file: "neroccio-portrait-of-a-lady.jpg", title: "Portrait of a Lady", titleTr: "Bir Hanımın Portresi", artist: "Neroccio de' Landi", year: "y. 1485",
      ...at(NGA, "https://purl.org/nga/collection/artobject/1186"), commons: "Neroccio de' Landi, Portrait of a Lady, c. 1485, NGA 1186.jpg", width: 1920, crop: box(0.178, 0.128, 0.822, 0.87) },
    { file: "francesco-sassetti-and-his-son.jpg", title: "Francesco Sassetti and His Son Teodoro", titleTr: "Francesco Sassetti ve Oğlu Teodoro", artist: "Domenico Ghirlandaio", year: "y. 1488",
      ...at(MET, "https://www.metmuseum.org/art/collection/search/436489"), commons: "Francesco Sassetti (1421–1490) and His Son Teodoro MET DT1369.jpg", width: 1920, crop: box(0.096, 0.11, 0.903, 0.938) },
    { file: "hunting-on-the-lagoon.jpg", title: "Hunting on the Lagoon", titleTr: "Lagünde Av", artist: "Vittore Carpaccio", year: "y. 1490–95",
      ...at(GETTY, "https://www.getty.edu/art/collection/object/103REK"), download: "https://media.getty.edu/iiif/image/420e73de-34a0-47cf-9d21-475f8daf6c80/full/!1920,1920/0/default.jpg", crop: box(0, 0.012, 1, 1) },
    { file: "giuliano-de-medici.jpg", title: "Giuliano de' Medici", titleTr: "Giuliano de' Medici", artist: "Sandro Botticelli", year: "y. 1478–80",
      ...at(NGA, "https://purl.org/nga/collection/artobject/41671"), commons: "Sandro Botticelli, Giuliano de' Medici, c. 1478-1480, NGA 41671.jpg", width: 1280, crop: box(0.016, 0.016, 0.985, 1) },
  ],
};

/** A painting whose picture could not be fetched or made; the message names it and what went wrong. */
class FetchFailure extends Error {}

/** Width and height of a JPEG, read from its frame header; absent when the bytes are not a JPEG. */
function jpegSize(bytes) {
  if (bytes[0] !== 0xff || bytes[1] !== 0xd8) return undefined;
  for (let i = 2; i < bytes.length - 9;) {
    if (bytes[i] !== 0xff) { i++; continue; }
    const marker = bytes[i + 1];
    const isFrame = marker >= 0xc0 && marker <= 0xcf && marker !== 0xc4 && marker !== 0xc8 && marker !== 0xcc;
    if (isFrame) return { width: bytes.readUInt16BE(i + 7), height: bytes.readUInt16BE(i + 5) };
    i += 2 + bytes.readUInt16BE(i + 2);
  }
  return undefined;
}

const pause = milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds));

function sourceAddress(painting) {
  if (painting.download) return painting.download;
  const size = painting.width ? `?width=${painting.width}` : "";
  return `https://commons.wikimedia.org/wiki/Special:FilePath/${encodeURIComponent(painting.commons)}${size}`;
}

function sourcePage(painting) {
  return painting.download ?? `https://commons.wikimedia.org/wiki/File:${painting.commons.replace(/ /g, "_")}`;
}

/** The painting's download, fetched unless an earlier run kept it. */
async function downloaded(room, painting) {
  const target = path.join(DOWNLOADS, room, painting.file);
  if (fs.existsSync(target)) return target;
  let response;
  try {
    response = await fetch(sourceAddress(painting), { headers: { "User-Agent": USER_AGENT } });
  } catch (failure) {
    throw new FetchFailure(`${room}/${painting.file}: no answer from ${sourceAddress(painting)}`, { cause: failure });
  }
  const type = response.headers.get("content-type") ?? "";
  const bytes = Buffer.from(await response.arrayBuffer());
  await pause(PAUSE_MS);
  if (!response.ok) throw new FetchFailure(`${room}/${painting.file}: ${sourceAddress(painting)} answered ${response.status}`);
  if (!type.startsWith("image/")) throw new FetchFailure(`${room}/${painting.file}: ${sourceAddress(painting)} answered with ${type}, not a picture`);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, bytes);
  return target;
}

function prepare(download, picture, crop) {
  const edges = [crop.left, crop.top, crop.right, crop.bottom].map(String);
  try {
    execFileSync(PYTHON, ["-I", PREPARE, download, picture, ...edges, String(LONGER_SIDE)], { stdio: ["ignore", "ignore", "pipe"] });
  } catch (failure) {
    fs.rmSync(picture, { force: true });
    throw new FetchFailure(`${picture}: the picture could not be made from ${download}: ${failure.stderr ?? failure.message}`, { cause: failure });
  }
}

async function fetchPainting(room, folder, painting) {
  const picture = path.join(folder, painting.file);
  if (!fs.existsSync(picture)) prepare(await downloaded(room, painting), picture, painting.crop ?? WHOLE);
  const bytes = fs.readFileSync(picture);
  const size = jpegSize(bytes);
  if (!size) throw new FetchFailure(`${room}/${painting.file}: the picture is not a JPEG`);
  console.log(`${room}/${painting.file}: ${size.width}x${size.height}, ${(bytes.length / 1048576).toFixed(2)} MB`);
  return {
    file: painting.file, title: painting.title, titleTr: painting.titleTr, artist: painting.artist, year: painting.year,
    width: size.width, height: size.height,
    museum: painting.museum, museumPage: painting.museumPage,
    fetchedFrom: sourcePage(painting),
    ...(painting.crop ? { crop: painting.crop } : {}),
    licence: painting.licence,
  };
}

async function main() {
  const failures = [];
  for (const [room, paintings] of Object.entries(ROOMS)) {
    const pictures = path.join("unity", "Assets", "Resources", "Paintings", room);
    const records = path.join("art", "paintings", room);
    fs.mkdirSync(pictures, { recursive: true });
    fs.mkdirSync(records, { recursive: true });
    const licences = [];
    for (const painting of paintings) {
      try {
        licences.push(await fetchPainting(room, pictures, painting));
      } catch (failure) {
        if (!(failure instanceof FetchFailure)) throw failure;
        failures.push(failure.message);
        console.error(failure.message);
      }
    }
    // A room with a picture missing keeps its earlier licence list: a list never names fewer paintings than the room holds.
    if (licences.length === paintings.length) {
      fs.writeFileSync(path.join(records, "licences.json"), JSON.stringify({ room, paintings: licences }, null, 2) + "\n");
    }
  }
  if (failures.length > 0) {
    console.error(`\n${failures.length} painting(s) not fetched:\n${failures.join("\n")}`);
    process.exitCode = 1;
  }
}

main().catch(failure => {
  console.error(failure.message);
  process.exitCode = 1;
});
