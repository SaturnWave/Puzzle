// Fetches the paintings of a museum room from Wikimedia Commons into the game (unity/Assets/Resources/Paintings/<room>/)
// and writes their licence list to art/paintings/<room>/licences.json (docs/GAME_DESIGN.md section 18: every image's
// source and licence is recorded).
// The files chosen are the Art Institute of Chicago's own open-access photographs as mirrored on Commons; the
// museum's image server answers scripts with a browser challenge, Commons serves them to a named client.
//
// usage (from the repository root): node tools/unity/fetch-paintings.js
// Add a room by adding its paintings to ROOMS. A file that is already there is not fetched again.
const fs = require("fs");
const path = require("path");

const USER_AGENT = "PuzzleGameDev/1.0 (museum-themed game; fetches a handful of public-domain paintings)";
/** The licence note of a picture that is the museum's own open-access photograph; other pictures name theirs. */
const MUSEUM_PHOTOGRAPH_LICENCE = "Public domain work; the museum's photograph is released under CC0 (Art Institute of Chicago open access)";
const ROOMS = {
  "room-01": [
    { file: "beach-at-sainte-adresse.jpg", title: "The Beach at Sainte-Adresse", titleTr: "Sainte-Adresse Plajı", artist: "Claude Monet", year: "1867",
      museumId: 14598, commons: "Claude Monet - The Beach at Sainte-Adresse - 1933.439 - Art Institute of Chicago.jpg", width: 1920 },
    { file: "two-sisters-on-the-terrace.jpg", title: "Two Sisters (On the Terrace)", titleTr: "Kız Kardeş (Terasta)", artist: "Pierre-Auguste Renoir", year: "1881",
      museumId: 14655, commons: "Pierre-Auguste Renoir - Two Sisters (On the Terrace) - 1933.455 - Art Institute of Chicago.jpg", width: 1280 },
    { file: "paris-street-rainy-day.jpg", title: "Paris Street; Rainy Day", titleTr: "Yağmurlu Gün", artist: "Gustave Caillebotte", year: "1877",
      museumId: 20684, commons: "Gustave Caillebotte - Paris Street, Rainy Day - 1964.336 - Art Institute of Chicago.jpg", width: 1920 },
    { file: "water-lilies.jpg", title: "Water Lilies", titleTr: "Nilüferler", artist: "Claude Monet", year: "1906",
      museumId: 16568, commons: "Claude Monet - Water Lilies - 1933.1157 - Art Institute of Chicago.jpg", width: 1920 },
    { file: "stacks-of-wheat-end-of-summer.jpg", title: "Stacks of Wheat (End of Summer)", titleTr: "Buğday Yığınları (Yaz Sonu)", artist: "Claude Monet", year: "1890–91",
      museumId: 64818, commons: "Claude Monet - Stacks of Wheat (End of Summer) - 1985.1103 - Art Institute of Chicago.jpg", width: 1920 },
  ],
  "room-02": [
    { file: "van-gogh-self-portrait.jpg", title: "Self-Portrait", titleTr: "Otoportre", artist: "Vincent van Gogh", year: "1887",
      museumId: 80607, commons: "Vincent van Gogh - Self-Portrait - 1954.326 - Art Institute of Chicago.jpg", width: 1280 },
    { file: "basket-of-apples.jpg", title: "The Basket of Apples", titleTr: "Elma Sepeti", artist: "Paul Cézanne", year: "y. 1893",
      museumId: 111436, commons: "Paul Cézanne - The Basket of Apples - 1926.252 - Art Institute of Chicago.jpg", width: 1920 },
    { file: "at-the-moulin-rouge.jpg", title: "At the Moulin Rouge", titleTr: "Moulin Rouge'da", artist: "Henri de Toulouse-Lautrec", year: "1892–95",
      museumId: 61128, commons: "Henri de Toulouse-Lautrec - At the Moulin Rouge - 1928.610 - Art Institute of Chicago.jpg", width: 1920 },
    { file: "the-bedroom.jpg", title: "The Bedroom", titleTr: "Yatak Odası", artist: "Vincent van Gogh", year: "1889",
      museumId: 28560, commons: "Vincent van Gogh - The Bedroom - 1926.417 - Art Institute of Chicago.jpg", width: 1920 },
    { file: "sunday-on-la-grande-jatte.jpg", title: "A Sunday on La Grande Jatte — 1884", titleTr: "Grande Jatte Adası'nda Bir Pazar Öğleden Sonrası", artist: "Georges Seurat", year: "1884–86",
      museumId: 27992, commons: "Georges Seurat - A Sunday on La Grande Jatte -- 1884 - Google Art Project.jpg", width: 1920,
      licence: "Public domain work; the photograph is Google Arts & Culture's faithful reproduction of it (public domain), taken from Wikimedia Commons." },
  ],
};

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

async function fetchPainting(folder, painting) {
  const target = path.join(folder, painting.file);
  if (!fs.existsSync(target)) {
    const url = `https://commons.wikimedia.org/wiki/Special:FilePath/${encodeURIComponent(painting.commons)}?width=${painting.width}`;
    const response = await fetch(url, { headers: { "User-Agent": USER_AGENT } });
    if (!response.ok) throw new Error(`${painting.file}: Commons answered ${response.status}`);
    fs.writeFileSync(target, Buffer.from(await response.arrayBuffer()));
  }
  const bytes = fs.readFileSync(target);
  const size = jpegSize(bytes);
  if (!size) throw new Error(`${painting.file}: what arrived is not a JPEG`);
  console.log(`${painting.file}: ${size.width}x${size.height}, ${(bytes.length / 1048576).toFixed(2)} MB`);
  return {
    file: painting.file, title: painting.title, titleTr: painting.titleTr, artist: painting.artist, year: painting.year,
    width: size.width, height: size.height,
    museum: "Art Institute of Chicago", museumPage: `https://www.artic.edu/artworks/${painting.museumId}`,
    fetchedFrom: `https://commons.wikimedia.org/wiki/File:${painting.commons.replace(/ /g, "_")}`,
    licence: painting.licence ?? MUSEUM_PHOTOGRAPH_LICENCE,
  };
}

async function main() {
  for (const [room, paintings] of Object.entries(ROOMS)) {
    const pictures = path.join("unity", "Assets", "Resources", "Paintings", room);
    const records = path.join("art", "paintings", room);
    fs.mkdirSync(pictures, { recursive: true });
    fs.mkdirSync(records, { recursive: true });
    const licences = [];
    for (const painting of paintings) licences.push(await fetchPainting(pictures, painting));
    fs.writeFileSync(path.join(records, "licences.json"), JSON.stringify({ room, paintings: licences }, null, 2) + "\n");
  }
}

main().catch(failure => {
  console.error(failure.message);
  process.exitCode = 1;
});
