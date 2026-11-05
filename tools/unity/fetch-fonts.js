// Fetches the design's three typefaces (docs/GAME_DESIGN.md section 22: Gabarito, Paytone One, Young Serif) from
// Google Fonts into the game (unity/Assets/Art/Fonts/) with their licence texts, and writes where each file came
// from to art/fonts/licences.json. All three are under the SIL Open Font License 1.1.
//
// The three draw Latin letters only. The game's other scripts are drawn by fallback typefaces, one font file a
// script (or a written form of Chinese characters) and a kind of type, whose letters stand in where a design
// typeface has none: Korean in Noto Sans KR (behind the sans faces) and Noto Serif KR (behind the serif); Russian
// in Noto Sans and Noto Serif; Japanese in Noto Sans JP and Noto Serif JP; Simplified Chinese in Noto Sans SC and
// Noto Serif SC; Traditional Chinese in Noto Sans TC and Noto Serif TC; all under the same licence. A fallback
// font is whole, with every letter of its script, so a new word never needs a new file; the Editor's
// LetteringBuilder makes its typeface with only the letters the tables of words use.
//
// The Korean pair came from Google's font service; every fallback since comes from the Noto project's own
// releases on GitHub (notofonts/noto-cjk, notofonts/latin-greek-cyrillic), each a zip archive with every weight
// and format of a family, of which one static file is read by itself (release-member.js) with the archive's own
// licence text. The Japanese and Chinese files are the region's subset of Noto Sans/Serif CJK, OpenType with
// PostScript outlines (.otf), which TextMesh Pro reads as it reads TrueType; their heaviest weights are Bold
// (700) and Black (900), and Bold is taken behind the sans faces, where Korean has 800: a Chinese character set
// in Black at a small size closes up.
//
// Gabarito is published as one variable font, of which TextMesh Pro reads the default weight alone. Google's font
// service hands a client that names no browser one static TrueType file per weight, and those are what is fetched:
// the design sets Gabarito in two weights, 700 and 800. The licence texts come from Google's own font repository.
//
// One file is not kept as it is fetched: Young Serif's figures are made its lining ones (lining-figures.js says
// why). The licence allows a changed copy that stays under the same licence, and under the font's name too where
// its authors reserved none; the record states the change and both files' bytes.
//
// usage (from the repository root): node tools/unity/fetch-fonts.js
// A font that is already there with the bytes its record names is not fetched again. After a font file has
// changed, run ProjectSetup.Run: the Editor's LetteringBuilder makes a typeface anew when the file names other
// drawings for its letters, and is the judge of whether the font has every letter the game writes. A change that
// redraws letters and keeps their numbers it cannot see: delete the font's "<name> Lettering.asset" then.
const crypto = require("crypto");
const fs = require("fs");
const path = require("path");
const { withLiningFigures } = require("./lining-figures");
const { fileOf } = require("./release-member");

const FONTS_FOLDER = path.join("unity", "Assets", "Art", "Fonts");
const RECORD = path.join("art", "fonts", "licences.json");
const FONT_SERVICE = "https://fonts.googleapis.com/css2";
const LICENCE_TEXTS = "https://raw.githubusercontent.com/google/fonts/main/ofl";
const LICENCE = "SIL Open Font License, Version 1.1";

/** What the record says of a file whose figures were made the font's lining ones. */
const LINING_FIGURES = "The figures 0 to 9 are the font's own lining forms (its 'lnum' feature) by default: tools/unity/lining-figures.js";

/** A font that lacks one of these was cut down to another alphabet; the whole set of the game's letters is the Editor's to check. */
const TURKISH_LETTERS = "ÇĞİÖŞÜçğıöşü";

/** The letters from one code point to another, both included. */
const lettersFromTo = (first, last) => Array.from({ length: last - first + 1 }, (_, index) => String.fromCodePoint(first + index)).join("");

/** Every Hangul syllable, U+AC00 to U+D7A3: a Korean fallback font that lacks one could not write a word some day. */
const HANGUL_SYLLABLES = lettersFromTo(0xac00, 0xd7a3);

/** The Russian alphabet, А to я with Ё and ё. */
const RUSSIAN_LETTERS = lettersFromTo(0x0410, 0x044f) + "Ёё";

/** The punctuation Japanese and Chinese write in full width: what a Japanese or Chinese fallback must have beside its characters. */
const FULL_WIDTH_MARKS = "、。！？：；（）";

/** Every Hiragana and Katakana letter (U+3041 to U+3096, U+30A1 to U+30FA), the long vowel mark and the middle dot. */
const KANA = lettersFromTo(0x3041, 0x3096) + lettersFromTo(0x30a1, 0x30fa) + "ー・";

/** A release of the Noto project's Chinese, Japanese and Korean fonts on GitHub; its archives lay their files out by region. */
const NOTO_CJK_SANS = "https://github.com/notofonts/noto-cjk/releases/download/Sans2.004";
const NOTO_CJK_SERIF = "https://github.com/notofonts/noto-cjk/releases/download/Serif2.003";

/** A release of the Noto project's Latin, Greek and Cyrillic fonts on GitHub. */
const NOTO_LGC = "https://github.com/notofonts/latin-greek-cyrillic/releases/download";

/**
 * A file per weight the design uses; `asks` is what the font service is asked for and `repository` the family's
 * folder in Google's font repository, or `release` the archive of a GitHub release, the file in it and its licence
 * text in it; `change` is what is done to the file before it is kept (nothing for most), `letters` what it must
 * have to be kept, and `fallback` the script a fallback typeface draws (none for the design's own).
 */
const FONTS = [
  { file: "Gabarito-Bold.ttf", family: "Gabarito", weight: 700, asks: "Gabarito:wght@700", repository: "gabarito", letters: TURKISH_LETTERS },
  { file: "Gabarito-ExtraBold.ttf", family: "Gabarito", weight: 800, asks: "Gabarito:wght@800", repository: "gabarito", letters: TURKISH_LETTERS },
  { file: "PaytoneOne-Regular.ttf", family: "Paytone One", weight: 400, asks: "Paytone+One", repository: "paytoneone", letters: TURKISH_LETTERS },
  {
    file: "YoungSerif-Regular.ttf", family: "Young Serif", weight: 400, asks: "Young+Serif", repository: "youngserif", letters: TURKISH_LETTERS,
    change: { says: LINING_FIGURES, make: withLiningFigures },
  },
  {
    file: "NotoSansKR-ExtraBold.ttf", family: "Noto Sans KR", weight: 800, asks: "Noto+Sans+KR:wght@800", repository: "notosanskr",
    letters: HANGUL_SYLLABLES, fallback: "Korean, behind Gabarito and Paytone One",
  },
  {
    file: "NotoSerifKR-Bold.ttf", family: "Noto Serif KR", weight: 700, asks: "Noto+Serif+KR:wght@700", repository: "notoserifkr",
    letters: HANGUL_SYLLABLES, fallback: "Korean, behind Young Serif",
  },
  {
    file: "NotoSans-ExtraBold.ttf", family: "Noto Sans", weight: 800,
    release: { archive: `${NOTO_LGC}/NotoSans-v2.015/NotoSans-v2.015.zip`, file: "NotoSans/unhinted/ttf/NotoSans-ExtraBold.ttf", licence: "OFL.txt" },
    letters: RUSSIAN_LETTERS, fallback: "Russian (Cyrillic), behind Gabarito and Paytone One",
  },
  {
    file: "NotoSerif-Bold.ttf", family: "Noto Serif", weight: 700,
    release: { archive: `${NOTO_LGC}/NotoSerif-v2.015/NotoSerif-v2.015.zip`, file: "NotoSerif/unhinted/ttf/NotoSerif-Bold.ttf", licence: "OFL.txt" },
    letters: RUSSIAN_LETTERS, fallback: "Russian (Cyrillic), behind Young Serif",
  },
  {
    file: "NotoSansJP-Bold.otf", family: "Noto Sans JP", weight: 700,
    release: { archive: `${NOTO_CJK_SANS}/16_NotoSansJP.zip`, file: "NotoSansJP-Bold.otf", licence: "LICENSE" },
    letters: KANA + FULL_WIDTH_MARKS + "日本語", fallback: "Japanese (kana and kanji in their Japanese forms), behind Gabarito and Paytone One",
  },
  {
    file: "NotoSerifJP-Bold.otf", family: "Noto Serif JP", weight: 700,
    release: { archive: `${NOTO_CJK_SERIF}/12_NotoSerifJP.zip`, file: "SubsetOTF/JP/NotoSerifJP-Bold.otf", licence: "LICENSE" },
    letters: KANA + FULL_WIDTH_MARKS + "日本語", fallback: "Japanese (kana and kanji in their Japanese forms), behind Young Serif",
  },
  {
    file: "NotoSansSC-Bold.otf", family: "Noto Sans SC", weight: 700,
    release: { archive: `${NOTO_CJK_SANS}/18_NotoSansSC.zip`, file: "NotoSansSC-Bold.otf", licence: "LICENSE" },
    letters: FULL_WIDTH_MARKS + "，《》简体中文", fallback: "Simplified Chinese (characters in their mainland forms), behind Gabarito and Paytone One",
  },
  {
    file: "NotoSerifSC-Bold.otf", family: "Noto Serif SC", weight: 700,
    release: { archive: `${NOTO_CJK_SERIF}/14_NotoSerifSC.zip`, file: "SubsetOTF/SC/NotoSerifSC-Bold.otf", licence: "LICENSE" },
    letters: FULL_WIDTH_MARKS + "，《》简体中文", fallback: "Simplified Chinese (characters in their mainland forms), behind Young Serif",
  },
  {
    file: "NotoSansTC-Bold.otf", family: "Noto Sans TC", weight: 700,
    release: { archive: `${NOTO_CJK_SANS}/19_NotoSansTC.zip`, file: "NotoSansTC-Bold.otf", licence: "LICENSE" },
    letters: FULL_WIDTH_MARKS + "，《》繁體中文", fallback: "Traditional Chinese (characters in their Taiwan forms), behind Gabarito and Paytone One",
  },
  {
    file: "NotoSerifTC-Bold.otf", family: "Noto Serif TC", weight: 700,
    release: { archive: `${NOTO_CJK_SERIF}/15_NotoSerifTC.zip`, file: "SubsetOTF/TC/NotoSerifTC-Bold.otf", licence: "LICENSE" },
    letters: FULL_WIDTH_MARKS + "，《》繁體中文", fallback: "Traditional Chinese (characters in their Taiwan forms), behind Young Serif",
  },
];

const sha256 = bytes => crypto.createHash("sha256").update(bytes).digest("hex");

async function answerOf(url) {
  // No client is named: the font service answers a browser with compressed, cut-down files and anyone else with whole TrueType ones.
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${url} answered ${response.status}`);
  return Buffer.from(await response.arrayBuffer());
}

/** What an OpenType file begins with: TrueType outlines, or PostScript ones ("OTTO"). */
const TRUETYPE_OUTLINES = 0x00010000;
const POSTSCRIPT_OUTLINES = 0x4f54544f;

/** The tables of an OpenType file by their four-letter names; absent when the bytes are not a single OpenType font. */
function tablesOf(bytes) {
  if (bytes.length < 12 || ![TRUETYPE_OUTLINES, POSTSCRIPT_OUTLINES].includes(bytes.readUInt32BE(0))) return undefined;
  const tables = new Map();
  for (let entry = 0; entry < bytes.readUInt16BE(4); entry++) {
    const at = 12 + 16 * entry;
    tables.set(bytes.toString("latin1", at, at + 4), { offset: bytes.readUInt32BE(at + 8), length: bytes.readUInt32BE(at + 12) });
  }
  return tables;
}

/** The code points a font has a glyph for, read from its Unicode character map of the common kind (format 4). */
function lettersOf(bytes, tables) {
  const cmap = tables.get("cmap").offset;
  const letters = new Set();
  for (let record = 0; record < bytes.readUInt16BE(cmap + 2); record++) {
    const subtable = cmap + bytes.readUInt32BE(cmap + 4 + 8 * record + 4);
    if (bytes.readUInt16BE(subtable) !== 4) continue;
    const segments = bytes.readUInt16BE(subtable + 6) / 2;
    const ends = subtable + 14;
    const starts = ends + 2 * segments + 2;
    for (let segment = 0; segment < segments; segment++) {
      const end = bytes.readUInt16BE(ends + 2 * segment);
      for (let code = bytes.readUInt16BE(starts + 2 * segment); code <= end && code < 0xffff; code++) letters.add(code);
    }
  }
  return letters;
}

/** Refuses what is not a whole, static OpenType font with every letter it is fetched for. */
function check(font, bytes) {
  const tables = tablesOf(bytes);
  if (!tables) throw new Error(`${font.file}: what arrived is not a single OpenType font`);
  if (tables.has("fvar")) throw new Error(`${font.file}: a variable font arrived; TextMesh Pro would read its default weight alone`);
  const letters = lettersOf(bytes, tables);
  const missing = [...font.letters].filter(letter => !letters.has(letter.codePointAt(0)));
  if (missing.length > 0) throw new Error(`${font.file}: the font has no ${missing.slice(0, 20).join(" ")}${missing.length > 20 ? ` and ${missing.length - 20} more` : ""}`);
}

/**
 * A font as the font service hands it out, with where it came from. The file that is there is taken where it is
 * still the one its record names as fetched; anything else is fetched anew.
 */
async function asFetched(font, kept) {
  const target = path.join(FONTS_FOLDER, font.file);
  const there = fs.existsSync(target) ? fs.readFileSync(target) : undefined;
  if (kept && there && sha256(there) === kept.sha256) {
    return { bytes: there, askedOf: kept.askedOf, fetchedFrom: kept.fetchedFrom, isNew: false };
  }

  if (font.release) {
    const bytes = await fileOf(font.release.archive, font.release.file);
    check(font, bytes);
    return { bytes, askedOf: font.release.archive, fetchedFrom: `${font.release.archive}!/${font.release.file}`, isNew: true };
  }

  const asked = `${FONT_SERVICE}?family=${font.asks}`;
  const stylesheet = (await answerOf(asked)).toString("utf8");
  const source = /url\((https:\/\/fonts\.gstatic\.com\/[^)]+\.ttf)\) format\('truetype'\)/.exec(stylesheet);
  if (!source) throw new Error(`${font.file}: ${asked} named no TrueType file`);
  const bytes = await answerOf(source[1]);
  check(font, bytes);
  return { bytes, askedOf: asked, fetchedFrom: source[1], isNew: true };
}

/** The copyright notice a font names of itself (its naming table's record 0), in Windows' Unicode records; empty where it names none. */
function copyrightNamedIn(bytes) {
  const naming = tablesOf(bytes).get("name");
  if (!naming) return "";
  const at = naming.offset;
  const strings = at + bytes.readUInt16BE(at + 4);
  for (let record = 0; record < bytes.readUInt16BE(at + 2); record++) {
    const entry = at + 6 + 12 * record;
    const isWindowsUnicode = bytes.readUInt16BE(entry) === 3 && bytes.readUInt16BE(entry + 2) === 1;
    if (isWindowsUnicode && bytes.readUInt16BE(entry + 6) === 0) {
      const start = strings + bytes.readUInt16BE(entry + 10);
      const big = bytes.subarray(start, start + bytes.readUInt16BE(entry + 8));
      return Buffer.from(big).swap16().toString("utf16le").trim();
    }
  }
  return "";
}

/**
 * A family's licence text, from where it lies beside the fonts or else from where the font came from (its
 * release's archive, or Google's font repository).
 */
async function licenceTextOf(font) {
  const file = `${font.family.replace(/ /g, "")}-OFL.txt`;
  const from = font.release ? `${font.release.archive}!/${font.release.licence}` : `${LICENCE_TEXTS}/${font.repository}/OFL.txt`;
  const target = path.join(FONTS_FOLDER, file);
  const fetchText = async () => (font.release ? await fileOf(font.release.archive, font.release.licence) : await answerOf(from)).toString("utf8");
  const text = fs.existsSync(target) ? fs.readFileSync(target, "utf8") : await fetchText();
  if (!text.includes("SIL OPEN FONT LICENSE Version 1.1")) throw new Error(`${from} is not the text of the ${LICENCE}`);
  if (!fs.existsSync(target)) fs.writeFileSync(target, text);
  return { file, from, text };
}

/**
 * What a licence text says of a font: who holds its copyright and the names they reserve. The notice is the text's
 * first paragraph; where that names no copyright (the Noto CJK releases' text begins with the licence itself), it
 * is the one the font file names of itself.
 */
function licenceOf(licenceText, fontBytes) {
  const firstParagraph = licenceText.text.split(/\r?\n\s*\r?\n/)[0].split(/\r?\n/).map(line => line.trim()).join(" ");
  const notice = /copyright|©/i.test(firstParagraph) ? firstParagraph : copyrightNamedIn(fontBytes);
  const reserved = /Reserved Font Names? (.+?)\.?$/.exec(notice);
  return {
    file: licenceText.file, from: licenceText.from, notice,
    reservedFontNames: reserved ? [...reserved[1].matchAll(/["'‘“]([^"'’”]+)["'’”]/g)].map(name => name[1]) : [],
  };
}

/** True when the file that is there is the one the record names as kept, changed the way the font is to be changed now. */
function isThereAsRecorded(font, kept) {
  const target = path.join(FONTS_FOLDER, font.file);
  if (!kept || !fs.existsSync(target)) return false;
  const changeAsked = font.change ? font.change.says : undefined;
  return kept.changed === changeAsked && sha256(fs.readFileSync(target)) === (kept.sha256AsKept ?? kept.sha256);
}

/**
 * What the record says of a font.
 * @param source Where the file came from and what arrived: askedOf, fetchedFrom, bytes, sha256.
 * @param change What was done to the file before it was kept (changed, bytesAsKept, sha256AsKept); undefined for a file kept as it arrived.
 */
function recordOf(font, licence, source, change) {
  const record = {
    file: font.file, family: font.family, weight: font.weight,
    ...(font.fallback ? { fallback: font.fallback } : {}),
    copyright: licence.notice, reservedFontNames: licence.reservedFontNames,
    licence: LICENCE, licenceText: licence.file, licenceTextFrom: licence.from,
    askedOf: source.askedOf, fetchedFrom: source.fetchedFrom, bytes: source.bytes, sha256: source.sha256,
  };
  return change ? { ...record, changed: change.changed, bytesAsKept: change.bytesAsKept, sha256AsKept: change.sha256AsKept } : record;
}

async function fetchFont(font, earlier) {
  const kept = earlier.find(record => record.file === font.file);
  const licenceText = await licenceTextOf(font);
  if (isThereAsRecorded(font, kept)) {
    console.log(`${font.file}: already there`);
    return recordOf(font, licenceOf(licenceText, fs.readFileSync(path.join(FONTS_FOLDER, font.file))), kept, kept.changed ? kept : undefined);
  }

  const fetched = await asFetched(font, kept);
  const licence = licenceOf(licenceText, fetched.bytes);
  if (font.change && licence.reservedFontNames.length > 0) {
    throw new Error(`${font.file}: its authors reserve the name ${licence.reservedFontNames.join(", ")}; a changed copy may not be kept under it`);
  }

  const bytes = font.change ? font.change.make(fetched.bytes) : fetched.bytes;
  check(font, bytes);
  fs.writeFileSync(path.join(FONTS_FOLDER, font.file), bytes);
  console.log(`${font.file}: ${(bytes.length / 1024).toFixed(0)} KB ${fetched.isNew ? `from ${fetched.fetchedFrom}` : "from the file that was there as fetched"}${font.change ? ", changed" : ""}`);

  return recordOf(
    font, licence,
    { askedOf: fetched.askedOf, fetchedFrom: fetched.fetchedFrom, bytes: fetched.bytes.length, sha256: sha256(fetched.bytes) },
    font.change ? { changed: font.change.says, bytesAsKept: bytes.length, sha256AsKept: sha256(bytes) } : undefined);
}

async function main() {
  fs.mkdirSync(FONTS_FOLDER, { recursive: true });
  fs.mkdirSync(path.dirname(RECORD), { recursive: true });
  const earlier = fs.existsSync(RECORD) ? JSON.parse(fs.readFileSync(RECORD, "utf8")).fonts : [];
  const fonts = [];
  for (const font of FONTS) fonts.push(await fetchFont(font, earlier));
  fs.writeFileSync(RECORD, JSON.stringify({ fonts }, null, 2) + "\n");
  console.log(`${fonts.length} fonts, ${(fonts.reduce((sum, font) => sum + (font.bytesAsKept ?? font.bytes), 0) / 1024).toFixed(0)} KB in all; record in ${RECORD}`);
}

main().catch(failure => {
  console.error(failure.message);
  process.exitCode = 1;
});
