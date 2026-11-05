// Writes the world map's data (#4) from the hand-made table and the Wikidata facts, without asking Wikidata anything:
//   museum/places.json            a pin for each painting of the catalogue, in its order
//   museum/places/<code>.json     the labels of the pins' places, notes and museums in each of the game's languages
//
// A label is put together from its key's parts, "Place, Region, Country": a part is a Wikidata item, named by its
// label in the language, or a term of table.json (a word with no item, or a shorter name than Wikidata's). A label
// written whole in table.json ("labels") is taken as it is. Nothing is guessed: a part with no name in a language, or
// a letter the game cannot draw, stops the build with the list of what is missing.
//
// usage (from the repository root): node tools/museum/places/build.js
const fs = require("fs");
const path = require("path");

const HERE = __dirname;
const CATALOGUE = "museum/catalogue.json";
const PLACES = "museum/places.json";
const LABELS_FOLDER = "museum/places";
const TABLE = path.join(HERE, "table.json");
const FACTS = path.join(HERE, "facts.json");

const PRECISIONS = ["exact", "city", "region"];

/** The game's languages, with the Wikidata label codes that stand for each, best first. */
const LANGUAGES = {
  en: ["en"],
  tr: ["tr"],
  ko: ["ko"],
  ja: ["ja"],
  de: ["de"],
  fr: ["fr"],
  es: ["es"],
  "pt-BR": ["pt-br", "pt"],
  it: ["it"],
  ru: ["ru"],
  "zh-Hans": ["zh-hans", "zh-cn"],
  "zh-Hant": ["zh-hant", "zh-tw", "zh-hk"],
  id: ["id"],
};

/** The languages written in Latin letters, which keep a foreign place's own name when they have none of their own. */
const LATIN_LANGUAGES = ["en", "tr", "de", "fr", "es", "pt-BR", "it", "id"];

/** How the parts of a label are joined: full-width marks for Japanese and Chinese, as the game's other tables write them. */
const SEPARATOR = { ja: "、", "zh-Hans": "，", "zh-Hant": "，" };
const PLAIN_SEPARATOR = ", ";

/** Pins that would fall on one spot are set around it on a ring this wide (in degrees of latitude, about 500 m). */
const SPREAD_RADIUS = 0.005;

// The letters the game's typefaces carry (unity/Assets/Scripts/Art/LetterSet.cs), and the scripts its fallback typefaces add.
const LETTER_SET = (() => {
  const letters = [];
  for (let code = 0x20; code <= 0x7e; code++) letters.push(String.fromCharCode(code));
  for (let code = 0xc0; code <= 0xff; code++) if (code !== 0xd7 && code !== 0xf7) letters.push(String.fromCharCode(code));
  return new Set([...letters, ..."ĞİŞğış", ..."¡¿œŒ«»", " ", ..."·…’“”–×∞"]);
})();
const FALLBACK_SCRIPTS = {
  ru: /[Ѐ-ӿ]/u,
  ja: /[　-ヿ一-鿿＀-￯]/u,
  "zh-Hans": /[　-〿一-鿿＀-￯]/u,
  "zh-Hant": /[　-〿一-鿿＀-￯]/u,
  ko: /[ᄀ-ᇿ㄰-㆏가-힯　-〿]/u,
};

/** What is wrong with the table or the facts, listed for a person to put right: the only failure reported without a stack. */
class TableProblems extends Error {}

const readJson = file => JSON.parse(fs.readFileSync(file, "utf8"));

/**
 * The keys written twice within one section of table.json (" "section": {" at one space, its keys at two). JSON.parse
 * keeps the last of two equal keys without a word, so a term written twice would quietly lose its first words.
 */
function keysWrittenTwice(text) {
  const twice = [];
  let section = "";
  let seen = new Set();
  for (const line of text.split("\n")) {
    const opening = line.match(/^ "(\w+)": [[{]/);
    if (opening) [section, seen] = [opening[1], new Set()];
    const key = line.match(/^ {2}"([^"]+)": /)?.[1];
    if (key === undefined) continue;
    if (seen.has(key)) twice.push(`${section}.${key}`);
    seen.add(key);
  }
  return twice;
}
const writeJson = (file, value) => fs.writeFileSync(file, `${JSON.stringify(value, null, 1)}\n`);
const isItem = part => /^Q\d+$/.test(part);

/** The letters of some words that the game cannot draw in a language. */
function lettersOutside(words, language) {
  const fallback = FALLBACK_SCRIPTS[language];
  return [...new Set([...words].filter(letter => !LETTER_SET.has(letter) && !(fallback && fallback.test(letter))))].join("");
}

/** The first letter of a name made a capital, as a name standing first in a label is written (Wikidata keeps "jardin du Luxembourg"). */
const capitalised = (name, language) => name.charAt(0).toLocaleUpperCase(language) + name.slice(1);

class LabelMaker {
  constructor(facts, terms) {
    this.facts = facts;
    this.terms = terms;
    this.problems = [];
    /** For each language, how many label parts came from Wikidata and how many from the table's own words. */
    this.tally = Object.fromEntries(Object.keys(LANGUAGES).map(language => [language, { wikidata: 0, table: 0 }]));
  }

  /** Words written whole in the table (a note), checked for letters the game cannot draw. */
  written(key, words, language) {
    return this.#checked(words, key, language);
  }

  /** A whole label: the one written in the table for the language, or its parts joined. */
  label(key, entry, language) {
    const written = entry.labels?.[language];
    if (written !== undefined) {
      this.tally[language].table++;
      return this.#checked(written, key, language);
    }
    const names = entry.parts.map(part => this.#partName(part, key, language));
    if (names.includes(undefined)) return undefined;
    return this.#checked(names.join(SEPARATOR[language] ?? PLAIN_SEPARATOR), key, language);
  }

  /**
   * A part's name in a language: the term's words for the language, else the item's Wikidata label, else (in a
   * language written in Latin letters) the term's "latin" spelling, the name as every such language keeps it.
   */
  #partName(part, key, language) {
    const term = this.terms[part];
    const labels = isItem(part) ? this.facts.items[part]?.labels : undefined;
    const code = labels && LANGUAGES[language].find(candidate => labels[candidate] !== undefined);
    const fromTable = term?.[language] ?? (code === undefined && LATIN_LANGUAGES.includes(language) ? term?.latin : undefined);
    if (fromTable !== undefined) {
      this.tally[language].table++;
      return fromTable;
    }
    if (code !== undefined) {
      this.tally[language].wikidata++;
      return capitalised(labels[code], language);
    }
    if (isItem(part) && labels === undefined) this.problems.push(`${key}: ${part} is not in facts.json (run fetch-facts.js)`);
    else this.problems.push(`${key}: ${part} (${labels?.en ?? "a term"}) has no ${language} name; add it to terms`);
    return undefined;
  }

  #checked(words, key, language) {
    const outside = lettersOutside(words, language);
    if (outside) this.problems.push(`${key} (${language}): "${words}" has letters the game cannot draw: ${outside}`);
    return words;
  }
}

/** The pin's latitude and longitude: the coordinates of its item, or those written in the table. */
function coordinates(painting, facts) {
  if (Array.isArray(painting.pin)) return painting.pin;
  const found = facts.items[painting.pin]?.coordinates;
  if (!found) throw new TableProblems(`${painting.id}: the pin ${painting.pin} has no coordinates in facts.json (run fetch-facts.js)`);
  return found;
}

/** Pins on one spot set around it on a small ring, in catalogue order, so each can be touched on the map. */
function spreadApart(pins) {
  const bySpot = new Map();
  for (const pin of pins) {
    const spot = `${pin.lat},${pin.lon}`;
    bySpot.set(spot, [...(bySpot.get(spot) ?? []), pin]);
  }
  for (const together of bySpot.values()) {
    if (together.length < 2) continue;
    together.forEach((pin, index) => {
      const angle = (2 * Math.PI * index) / together.length;
      const longitudeScale = Math.cos((pin.lat * Math.PI) / 180);
      pin.lat = round(pin.lat + SPREAD_RADIUS * Math.cos(angle));
      pin.lon = round(pin.lon + (SPREAD_RADIUS * Math.sin(angle)) / longitudeScale);
    });
  }
}

const round = degrees => Math.round(degrees * 10000) / 10000;

function checkTable(table, catalogued) {
  const problems = [];
  const order = table.paintings.map(painting => painting.id).join(",");
  if (order !== catalogued.map(painting => painting.id).join(",")) problems.push("table.json does not list the catalogue's paintings in the catalogue's order");
  for (const painting of table.paintings) {
    if (!PRECISIONS.includes(painting.precision)) problems.push(`${painting.id}: precision "${painting.precision}" is not one of ${PRECISIONS.join(", ")}`);
    if (typeof painting.verified !== "boolean") problems.push(`${painting.id}: verified must be true or false`);
    if (!table.places[painting.place]) problems.push(`${painting.id}: no place "${painting.place}"`);
    if (painting.note !== null && !table.notes[painting.note]) problems.push(`${painting.id}: no note "${painting.note}"`);
    if (!painting.source) problems.push(`${painting.id}: no source`);
  }
  for (const [key, note] of Object.entries(table.notes)) {
    for (const language of Object.keys(LANGUAGES)) if (!note[language]) problems.push(`note ${key}: no ${language} words`);
  }
  return problems;
}

function main() {
  const catalogue = readJson(CATALOGUE);
  const twice = keysWrittenTwice(fs.readFileSync(TABLE, "utf8"));
  if (twice.length > 0) throw new TableProblems(`table.json writes these keys twice: ${twice.join(", ")}`);
  const table = readJson(TABLE);
  const facts = readJson(FACTS);
  const catalogued = catalogue.rooms.flatMap(room => room.paintings);
  const sourceOf = new Map(catalogued.map(painting => [painting.id, painting.source]));

  const problems = checkTable(table, catalogued);
  const museumOf = id => table.catalogueMuseums[sourceOf.get(id).split(",")[0]];
  for (const painting of table.paintings) if (!museumOf(painting.id)) problems.push(`${painting.id}: its museum is not in catalogueMuseums`);
  if (problems.length > 0) throw new TableProblems(problems.join("\n"));

  const pins = table.paintings.map(painting => {
    const [lat, lon] = coordinates(painting, facts);
    return {
      id: painting.id,
      lat: round(lat),
      lon: round(lon),
      place: painting.place,
      precision: painting.precision,
      verified: painting.verified,
      museum: museumOf(painting.id),
      note: painting.note,
      source: painting.source,
    };
  });
  spreadApart(pins);

  const labels = new LabelMaker(facts, table.terms);
  const tables = Object.keys(LANGUAGES).map(language => ({
    language,
    review: "machine",
    places: Object.fromEntries(Object.entries(table.places).map(([key, entry]) => [key, labels.label(key, entry, language)])),
    notes: Object.fromEntries(Object.entries(table.notes).map(([key, words]) => [key, labels.written(key, words[language], language)])),
    museums: Object.fromEntries(Object.entries(table.museums).map(([key, entry]) => [key, labels.label(key, entry, language)])),
  }));
  if (labels.problems.length > 0) throw new TableProblems(labels.problems.join("\n"));

  writeJson(PLACES, {
    formatVersion: 1,
    about:
      "Where each painting was made or what place it shows, for the world map (#4, docs/GAME_DESIGN.md §19). lat/lon in degrees (WGS84). " +
      "place, note and museum are keys into museum/places/<language>.json. precision: exact | city | region. " +
      "verified: the place is named by Wikidata's statements on the painting or by the museum's own page; source says which, or why the pin is unverified. " +
      "Pins that would fall on one spot are set a few hundred metres apart. Built by tools/museum/places/build.js from tools/museum/places/table.json.",
    paintings: pins,
  });
  for (const written of tables) writeJson(path.join(LABELS_FOLDER, `${written.language}.json`), written);

  const verified = pins.filter(pin => pin.verified);
  const count = precision => verified.filter(pin => pin.precision === precision).length;
  console.log(`${pins.length} pins, ${verified.length} verified (exact ${count("exact")}, city ${count("city")}, region ${count("region")})`);
  console.log("place and museum label parts from Wikidata / from the table's words:");
  for (const [language, { wikidata, table: own }] of Object.entries(labels.tally)) console.log(`  ${language.padEnd(8)} ${wikidata} / ${own}`);
}

try {
  main();
} catch (error) {
  if (!(error instanceof TableProblems)) throw error;
  console.error(error.message);
  process.exitCode = 1;
}
