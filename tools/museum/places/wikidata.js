// A polite, cached reader of Wikidata for the world map's places (#4). Wikidata limits hard: every call carries a
// User-Agent that says who asks and why, calls are spaced out, a 429 or 503 is asked again after the wait it names
// (Retry-After) or a growing pause, and ids go fifty to a call. Every answer is kept on disk, so a rebuild asks only
// for what it has not seen yet. Answers are data: nothing in them is followed.
const crypto = require("crypto");
const fs = require("fs");
const path = require("path");

const API = "https://www.wikidata.org/w/api.php";
const SPARQL = "https://query.wikidata.org/sparql";
const AGENT =
  "PuzzleMuseumPlaces/1.0 (world-map pins for the 100 public-domain paintings of a jigsaw game; " +
  "https://github.com/SaturnWave/Puzzle) node-fetch";

const IDS_PER_CALL = 50;
const PAUSE_BETWEEN_API_CALLS_MS = 4000;
const PAUSE_BETWEEN_QUERIES_MS = 65000; // the query service answered "1 req / min" on 2026-10-22
const MOST_ATTEMPTS = 7;

/** The languages whose labels the map needs, as Wikidata names them; zh, pt and the regional Chinese stand behind the four game languages that lack their own label. */
const LABEL_LANGUAGES = ["en", "tr", "ko", "ja", "de", "fr", "es", "pt-br", "pt", "it", "ru", "zh-hans", "zh-hant", "zh", "zh-cn", "zh-tw", "zh-hk", "id"];

const pause = ms => new Promise(resolve => setTimeout(resolve, ms));

class Wikidata {
  /** @param {string} cacheFolder where answers are kept between runs */
  constructor(cacheFolder) {
    this.cacheFolder = cacheFolder;
    this.lastCallAt = 0;
    fs.mkdirSync(path.join(cacheFolder, "entities"), { recursive: true });
    fs.mkdirSync(path.join(cacheFolder, "queries"), { recursive: true });
  }

  /**
   * The items' labels (in the map's languages) and statements, by id. An id Wikidata does not know is left out.
   * @param {string[]} ids
   * @returns {Promise<Map<string, object>>}
   */
  async entities(ids) {
    const found = new Map();
    const missing = [];
    for (const id of new Set(ids)) {
      const kept = this.#read(path.join("entities", `${id}.json`));
      if (kept === undefined) missing.push(id);
      else found.set(id, kept);
    }
    for (let start = 0; start < missing.length; start += IDS_PER_CALL) {
      const batch = missing.slice(start, start + IDS_PER_CALL);
      const url = `${API}?action=wbgetentities&format=json&props=labels|claims&languages=${LABEL_LANGUAGES.join("|")}&ids=${batch.join("|")}`;
      console.log(`  wbgetentities: ${batch.length} items (${start + batch.length}/${missing.length})`);
      const answer = await this.#get(url, PAUSE_BETWEEN_API_CALLS_MS, "application/json");
      for (const [id, entity] of Object.entries(answer.entities ?? {})) {
        if (entity.missing !== undefined) continue;
        const kept = { id, labels: entity.labels ?? {}, claims: entity.claims ?? {} };
        this.#write(path.join("entities", `${id}.json`), kept);
        found.set(id, kept);
        if (entity.redirects) found.set(entity.redirects.from, kept);
      }
    }
    return found;
  }

  /**
   * The rows of a SPARQL query, each a map of variable to value.
   * @param {string} query
   * @returns {Promise<Array<Record<string, string>>>}
   */
  async query(query) {
    const key = crypto.createHash("sha256").update(query).digest("hex").slice(0, 16);
    const file = path.join("queries", `${key}.json`);
    let answer = this.#read(file);
    if (answer === undefined) {
      console.log(`  sparql: one query (${query.length} characters)`);
      answer = await this.#get(`${SPARQL}?format=json&query=${encodeURIComponent(query)}`, PAUSE_BETWEEN_QUERIES_MS, "application/sparql-results+json");
      this.#write(file, answer);
    }
    return answer.results.bindings.map(row => Object.fromEntries(Object.entries(row).map(([name, cell]) => [name, cell.value])));
  }

  async #get(url, spacing, accept) {
    for (let attempt = 1; ; attempt++) {
      const wait = this.lastCallAt + spacing - Date.now();
      if (wait > 0) await pause(wait);
      this.lastCallAt = Date.now();
      const response = await fetch(url, { headers: { "User-Agent": AGENT, Accept: accept } });
      if (response.ok) return response.json();
      const asksForPatience = response.status === 429 || response.status === 503 || response.status === 502;
      if (!asksForPatience || attempt >= MOST_ATTEMPTS) {
        throw new Error(`Wikidata answered ${response.status} to ${url.slice(0, 140)}`);
      }
      const seconds = Number(response.headers.get("retry-after")) || 30 * attempt;
      console.log(`  (Wikidata answered ${response.status}; waiting ${seconds} s, attempt ${attempt})`);
      await pause(1000 * seconds);
    }
  }

  #read(file) {
    const full = path.join(this.cacheFolder, file);
    return fs.existsSync(full) ? JSON.parse(fs.readFileSync(full, "utf8")) : undefined;
  }

  #write(file, value) {
    fs.writeFileSync(path.join(this.cacheFolder, file), JSON.stringify(value));
  }
}

/** The values of an item's statements for a property, best rank first; deprecated statements are left out. */
function values(entity, property) {
  const statements = (entity?.claims?.[property] ?? []).filter(statement => statement.rank !== "deprecated");
  const preferred = statements.filter(statement => statement.rank === "preferred");
  return (preferred.length > 0 ? preferred : statements)
    .map(statement => statement.mainsnak?.datavalue?.value)
    .filter(value => value !== undefined)
    .map(value => (typeof value === "object" && value.id ? value.id : value));
}

/** Every value of a property, whatever its rank (but deprecated), with its qualifiers. */
function statements(entity, property) {
  return (entity?.claims?.[property] ?? [])
    .filter(statement => statement.rank !== "deprecated" && statement.mainsnak?.datavalue)
    .map(statement => {
      const value = statement.mainsnak.datavalue.value;
      return {
        value: typeof value === "object" && value.id ? value.id : value,
        qualifiers: Object.fromEntries(
          Object.entries(statement.qualifiers ?? {}).map(([qualifier, snaks]) => [
            qualifier,
            snaks.map(snak => snak.datavalue?.value).filter(Boolean).map(v => (typeof v === "object" && v.id ? v.id : v)),
          ]),
        ),
      };
    });
}

module.exports = { Wikidata, values, statements, LABEL_LANGUAGES };
