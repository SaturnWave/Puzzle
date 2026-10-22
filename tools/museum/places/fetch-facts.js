// Gathers from Wikidata what the world map's pins (#4) are checked against, into tools/museum/places/facts.json:
// for each painting's item its collection (P195), inventory number (P217), inception (P571), location of creation
// (P1071) and what it depicts (P180); for every place among those, and every item a place label or a museum label is
// built from (table.json's "parts"), and every item a pin is taken from (table.json's "pin"), its labels in the
// game's languages, its coordinates (P625), its country (P17) and what it lies in (P131). It also looks each painting
// up by the id its museum gives it (the catalogue's "source" page) and says where that disagrees with the item
// table.json names.
//
// The answers are kept in a cache folder, so a run after the first asks Wikidata only for what is new; facts.json is
// what tools/museum/places/build.js reads, so the map can be rebuilt without asking Wikidata at all.
//
// usage (from the repository root): node tools/museum/places/fetch-facts.js [cache folder]
const fs = require("fs");
const os = require("os");
const path = require("path");
const { Wikidata, values } = require("./wikidata");

const HERE = __dirname;
const CATALOGUE = "museum/catalogue.json";
const TABLE = path.join(HERE, "table.json");
const FACTS = path.join(HERE, "facts.json");

/** The property and value under which Wikidata keeps a museum's own id of a work, read from the catalogue's page address. */
const MUSEUM_IDS = [
  { pattern: /artic\.edu\/artworks\/(\d+)/, property: "P4610" }, // Art Institute of Chicago artwork ID
  { pattern: /metmuseum\.org\/.*\/(\d+)$/, property: "P3634" }, // The Met object ID
  { pattern: /nga\/collection\/artobject\/(\d+)/, property: "P4683" }, // National Gallery of Art artwork ID
  { pattern: /nga\.gov\/artworks\/(\d+)-/, property: "P4683" },
  { pattern: /nationalmuseum\.se\/en\/collection\/item\/(\d+)/, property: "P2539" }, // Nationalmuseum Sweden artwork ID
  { pattern: /rijksmuseum\.nl\/.*\/(?:object\/)?([A-Z]{2}-[A-Z]+(?:-[A-Z])?-[0-9-]+?)(?:--|$)/, property: "P217" }, // inventory number
  { pattern: /smk\.dk\/artwork\/image\/(\w+)/, property: "P217" },
  { pattern: /clevelandart\.org\/art\/([\d.]+)/, property: "P217" },
];

/** The museum's own id of a work, as Wikidata keeps it; none for a museum Wikidata keeps no such id of. */
function museumId(source) {
  for (const { pattern, property } of MUSEUM_IDS) {
    const found = source.match(pattern);
    if (found) return { property, id: found[1] };
  }
  return undefined;
}

const LABEL_CODES = ["en", "tr", "ko", "ja", "de", "fr", "es", "pt-br", "pt", "it", "ru", "zh-hans", "zh-hant", "zh", "zh-cn", "zh-tw", "zh-hk", "id"];

function placeFacts(entity) {
  const coordinates = values(entity, "P625")[0];
  return {
    labels: Object.fromEntries(LABEL_CODES.filter(code => entity.labels[code]).map(code => [code, entity.labels[code].value])),
    coordinates: coordinates ? [round(coordinates.latitude), round(coordinates.longitude)] : null,
    instanceOf: values(entity, "P31"),
    country: values(entity, "P17"),
    in: values(entity, "P131"),
  };
}

const round = degrees => Math.round(degrees * 10000) / 10000;

function inception(entity) {
  return values(entity, "P571").map(time => `${time.time.slice(0, 11).replace(/-00-00T.*|-01-01T.*/, "")} (precision ${time.precision})`);
}

async function main(cacheFolder) {
  const wikidata = new Wikidata(cacheFolder);
  const catalogue = JSON.parse(fs.readFileSync(CATALOGUE, "utf8"));
  const table = JSON.parse(fs.readFileSync(TABLE, "utf8"));
  const chosen = new Map(table.paintings.map(painting => [painting.id, painting.item]));
  const catalogued = catalogue.rooms.flatMap(room => room.paintings);

  console.log("Looking the paintings up by their museums' ids");
  const byMuseumId = new Map();
  const lookups = catalogued.map(painting => ({ painting, wanted: museumId(painting.source) })).filter(({ wanted }) => wanted);
  const rows = await wikidata.query(
    `SELECT ?key ?item WHERE { VALUES (?key ?property ?id) { ${lookups
      .map(({ painting, wanted }) => `("${painting.id}" wdt:${wanted.property} "${wanted.id}")`)
      .join(" ")} } ?item ?property ?id . }`,
  );
  for (const row of rows) (byMuseumId.get(row.key) ?? byMuseumId.set(row.key, new Set()).get(row.key)).add(row.item.replace(/.*\//, ""));

  console.log("Reading the paintings' items");
  const paintingItems = await wikidata.entities([...chosen.values()].filter(Boolean));
  const referenced = new Set();
  for (const item of chosen.values()) {
    const entity = paintingItems.get(item);
    for (const property of ["P1071", "P180", "P195"]) values(entity, property).forEach(value => referenced.add(value));
  }
  const isItem = value => typeof value === "string" && /^Q\d+$/.test(value);
  const parts = new Set(table.paintings.map(painting => painting.pin).filter(isItem));
  for (const group of [table.places, table.museums]) {
    for (const entry of Object.values(group)) entry.parts.filter(isItem).forEach(part => parts.add(part));
  }
  parts.forEach(part => referenced.add(part));

  console.log("Reading the places, collections and label parts");
  const items = await wikidata.entities([...referenced]);
  const isPlace = entity => values(entity, "P625").length > 0 || values(entity, "P17").length > 0;
  const geographic = id => items.has(id) && isPlace(items.get(id));

  const kept = {};
  const keep = (id, entity) => {
    if (entity && !kept[id]) kept[id] = placeFacts(entity);
  };
  const paintings = {};
  for (const painting of catalogued) {
    const item = chosen.get(painting.id) ?? null;
    const entity = item ? paintingItems.get(item) : undefined;
    const found = [...(byMuseumId.get(painting.id) ?? [])];
    if (item && found.length > 0 && !found.includes(item)) console.log(`  ! ${painting.id}: table names ${item}, the museum id finds ${found.join(", ")}`);
    paintings[painting.id] = {
      item,
      byMuseumId: found,
      label: entity?.labels.en?.value ?? null,
      collection: values(entity, "P195"),
      inventory: values(entity, "P217"),
      inception: entity ? inception(entity) : [],
      madeIn: values(entity, "P1071"),
      depictsPlaces: values(entity, "P180").filter(geographic),
    };
    for (const id of [...paintings[painting.id].madeIn, ...paintings[painting.id].depictsPlaces, ...paintings[painting.id].collection]) keep(id, items.get(id));
  }
  for (const id of parts) keep(id, items.get(id));

  const facts = { fetched: new Date().toISOString().slice(0, 10), source: "Wikidata (wbgetentities and the query service)", paintings, items: kept };
  fs.writeFileSync(FACTS, `${JSON.stringify(facts, null, 1)}\n`);
  console.log(`${Object.keys(paintings).length} paintings, ${Object.keys(kept).length} items written to ${path.relative(process.cwd(), FACTS)}`);
}

main(process.argv[2] ?? path.join(os.tmpdir(), "puzzle-museum-places-wikidata"));
