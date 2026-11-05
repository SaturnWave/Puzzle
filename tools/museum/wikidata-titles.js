// Looks up, on Wikidata's public API (no sign-in), the title each painting of the museum is known by in a language:
// the painting's item is found among the works of its painter by its original title, and its label in that
// language is the established title. What is found is written to out/museum-titles/wikidata-<code>.json, one
// entry a painting: the item it matched (with how well), and the label when the item has one. The titles files
// (museum/titles/<code>.json) are written by hand from it: a label a person has checked against the painting
// becomes an "established" title with the item as its reference; a painting without one gets a translation.
//
// Nothing here is trusted as it arrives: a match is by words, and only a reader decides that the item is the
// painting. The answers are data, kept apart in their own folder.
//
// usage (from the repository root): node tools/museum/wikidata-titles.js ko
const fs = require("fs");
const path = require("path");

const CATALOGUE = "museum/catalogue.json";
const OUT_FOLDER = path.join("out", "museum-titles");
const API = "https://www.wikidata.org/w/api.php";
const SPARQL = "https://query.wikidata.org/sparql";
const AGENT = "PuzzleMuseumTitles/1.0 (one-off lookup of established painting titles; no sign-in)";

/** The painter as Wikidata's search knows them, where the catalogue writes the name with Turkish words around it or in another spelling. */
const PAINTER_AS_SEARCHED = {
  "Frederick Kemmelmeyer'e atfen": "Frederick Kemmelmeyer",
  "Yaşlı Jan Brueghel": "Jan Brueghel the Elder",
  "Genç Hans Holbein": "Hans Holbein the Younger",
  "Savalı Habiballah": "Habiballah of Sava",
  "Sultan Muhammad'a atfen": "Sultan Muhammad",
  "Abel Grimmer'e atfen": "Abel Grimmer",
  "Joseph Mallord William Turner": "J. M. W. Turner",
  "Sir Thomas Lawrence": "Thomas Lawrence",
  "Sir Joshua Reynolds": "Joshua Reynolds",
  "Toshusai Sharaku": "Sharaku",
  "Cariani": "Giovanni Cariani",
  "Mansur": "Ustad Mansur",
  "Paul Joseph Constantin Gabriël": "Paul Gabriël",
  "Jean Honoré Fragonard": "Jean-Honoré Fragonard",
};

const pause = ms => new Promise(resolve => setTimeout(resolve, ms));

/** A JSON answer; a service that asks for patience (429, 503) is asked again after the wait it names, a few times. */
async function json(url) {
  for (let attempt = 1; ; attempt++) {
    const response = await fetch(url, { headers: { "User-Agent": AGENT, Accept: "application/sparql-results+json, application/json" } });
    if (response.ok) return response.json();
    const asksForPatience = response.status === 429 || response.status === 503;
    if (attempt >= 6 || !asksForPatience) throw new Error(`${url.slice(0, 120)} answered ${response.status}`);
    const seconds = Number(response.headers.get("retry-after")) || 20 * attempt;
    console.log(`  (asked to wait ${seconds} s)`);
    await pause(1000 * seconds);
  }
}

/** The painter's item: the first search result that Wikidata describes as a maker of pictures. */
async function painterItem(name) {
  const url = `${API}?action=wbsearchentities&format=json&language=en&type=item&limit=10&search=${encodeURIComponent(name)}`;
  const found = (await json(url)).search ?? [];
  const maker = /painter|artist|printmaker|draughtsman|draftsman|illustrator|miniaturist|engraver|designer/i;
  const chosen = found.find(item => maker.test(item.description ?? "")) ?? found[0];
  return chosen ? { id: chosen.id, label: chosen.label, description: chosen.description ?? "" } : undefined;
}

/** The painter's works with their English label and, where they have one, their label in the language. */
async function worksOf(painterId, code) {
  const query = `SELECT ?item ?en ?own WHERE {
    ?item wdt:P170 wd:${painterId} .
    OPTIONAL { ?item rdfs:label ?en FILTER(LANG(?en) = "en") }
    OPTIONAL { ?item rdfs:label ?own FILTER(LANG(?own) = "${code}") }
  }`;
  const answer = await json(`${SPARQL}?format=json&query=${encodeURIComponent(query)}`);
  return answer.results.bindings.map(row => ({
    id: row.item.value.replace("http://www.wikidata.org/entity/", ""),
    en: row.en?.value ?? "",
    own: row.own?.value,
  }));
}

/** The words of a title that tell it apart, in small letters without accents or punctuation. */
function wordsOf(title) {
  const quiet = new Set(["the", "a", "an", "of", "from", "and", "in", "at", "on", "with", "la", "le", "de", "des", "en", "pour"]);
  return title.normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase().replace(/[^a-z0-9 ]+/g, " ")
    .split(/\s+/).filter(word => word && !quiet.has(word));
}

/** How alike two titles are: the share of the shorter one's words that the other has. */
function likeness(a, b) {
  const one = new Set(wordsOf(a));
  const other = new Set(wordsOf(b));
  if (one.size === 0 || other.size === 0) return 0;
  let shared = 0;
  for (const word of one) if (other.has(word)) shared++;
  return shared / Math.min(one.size, other.size);
}

async function main() {
  const code = process.argv[2];
  if (!code) throw new Error("name the language: node tools/museum/wikidata-titles.js ko");
  const catalogue = JSON.parse(fs.readFileSync(CATALOGUE, "utf8"));
  const painters = new Map();
  const results = [];
  for (const room of catalogue.rooms) {
    for (const painting of room.paintings) {
      const searched = PAINTER_AS_SEARCHED[painting.artist] ?? painting.artist;
      if (!painters.has(searched)) {
        const item = await painterItem(searched);
        const works = item ? await worksOf(item.id, code) : [];
        painters.set(searched, { item, works });
        await pause(2000);
      }

      const { item, works } = painters.get(searched);
      const ranked = works
        .map(work => ({ ...work, likeness: Math.max(likeness(painting.originalTitle, work.en), work.own ? likeness(painting.originalTitle, work.own) : 0) }))
        .sort((one, other) => other.likeness - one.likeness || (other.own ? 1 : 0) - (one.own ? 1 : 0));
      const best = ranked[0];
      results.push({
        id: painting.id, originalTitle: painting.originalTitle, painter: searched,
        painterItem: item ? `${item.id} ${item.label} (${item.description})` : null,
        works: works.length,
        match: best && best.likeness > 0 ? { item: best.id, en: best.en, label: best.own ?? null, likeness: Number(best.likeness.toFixed(2)) } : null,
        alternatives: ranked.slice(1, 4).filter(work => work.likeness > 0.3).map(work => ({ item: work.id, en: work.en, label: work.own ?? null })),
      });
      console.log(`${painting.id}: ${best ? `${best.id} "${best.en}" -> ${best.own ?? "(no label)"} [${best.likeness.toFixed(2)}]` : "no work found"}`);
    }
  }

  fs.mkdirSync(OUT_FOLDER, { recursive: true });
  const file = path.join(OUT_FOLDER, `wikidata-${code}.json`);
  fs.writeFileSync(file, JSON.stringify({ language: code, fetched: new Date().toISOString(), paintings: results }, null, 2) + "\n");
  const labelled = results.filter(result => result.match?.label && result.match.likeness >= 0.6).length;
  console.log(`${results.length} paintings, ${labelled} with a label in ${code} on a likely match -> ${file}`);
}

main().catch(failure => {
  console.error(failure.message);
  process.exitCode = 1;
});
