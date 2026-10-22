// Brings the Unity project up to date with the engine, the rules around it and the content:
//   1. builds Puzzle.Bot, Puzzle.Museum and Puzzle.Meta (netstandard2.1; each builds the Puzzle.Core it depends
//      on) and copies the four assemblies into unity/Assets/Generated/Engine;
//   2. packs levels/generated/level_*.json into one document, unity/Assets/Generated/Resources/Levels.json;
//   3. copies the museum's catalogue, museum/catalogue.json, to unity/Assets/Generated/Resources/Museum.json;
//   4. packs the game's part of the economy settings, economy/default.json, into
//      unity/Assets/Generated/Resources/Economy.json (tools/unity/economy-pack.js says how);
//   5. packs each language's table of words, words/<code>.json, into unity/Assets/Generated/Resources/Words/<code>.json
//      without the translators' notes, and each language's museum words, museum/titles/<code>.json, into
//      unity/Assets/Generated/Resources/Titles/<code>.json. The game offers a language whose tables are complete;
//   6. copies the world map's places, museum/places.json, to unity/Assets/Generated/Resources/Places.json, and packs
//      each language's labels for them, museum/places/<code>.json, into Resources/PlaceLabels/<code>.json with each
//      kind of label as a list of {key, text}: Unity's JSON reader has no dictionaries.
// The engine, the museum's rules and the rules of lives, coins and boosters go in as compiled assemblies so that
// the game runs the very code the levels were calibrated with and the tools and tests check.
// Everything written here is derived from the repository, so unity/Assets/Generated is not committed: run this
// script after cloning and whenever the engine, the rules, the levels, the catalogue, the economy settings or a
// language's tables change.
//
// usage (from the repository root): node tools/unity/sync-engine.js
const fs = require("fs");
const path = require("path");
const { execFileSync } = require("child_process");
const { SETTINGS: ECONOMY, packedEconomy } = require("./economy-pack");

const ENGINE = "unity/Assets/Generated/Engine";
const LEVELS = "levels/generated";
const LEVEL_PACK = "unity/Assets/Generated/Resources/Levels.json";
const CATALOGUE = "museum/catalogue.json";
const CATALOGUE_COPY = "unity/Assets/Generated/Resources/Museum.json";
const ECONOMY_PACK = "unity/Assets/Generated/Resources/Economy.json";
const WORDS = "words";
const WORDS_PACK = "unity/Assets/Generated/Resources/Words";
const TITLES = "museum/titles";
const TITLES_PACK = "unity/Assets/Generated/Resources/Titles";
const PLACES = "museum/places.json";
const PLACES_COPY = "unity/Assets/Generated/Resources/Places.json";
const PLACE_LABELS = "museum/places";
const PLACE_LABELS_PACK = "unity/Assets/Generated/Resources/PlaceLabels";
// Each project with the assemblies taken from its output folder. The core lands in both; it is taken once.
const PROJECTS = [
  { project: "src/Puzzle.Bot", assemblies: ["Puzzle.Core.dll", "Puzzle.Bot.dll"] },
  { project: "src/Puzzle.Museum", assemblies: ["Puzzle.Museum.dll"] },
  { project: "src/Puzzle.Meta", assemblies: ["Puzzle.Meta.dll"] },
];

function main() {
  fs.mkdirSync(ENGINE, { recursive: true });
  for (const { project, assemblies } of PROJECTS) {
    execFileSync("dotnet", ["build", project, "-c", "Release", "--nologo", "-v", "quiet"], { stdio: "inherit" });
    for (const assembly of assemblies) {
      fs.copyFileSync(path.join(project, "bin/Release/netstandard2.1", assembly), path.join(ENGINE, assembly));
    }
  }
  console.log(`engine, museum and meta assemblies -> ${ENGINE}`);

  const files = fs.readdirSync(LEVELS).filter(name => /^level_\d{4}\.json$/.test(name)).sort();
  if (files.length === 0) throw new Error(`no level file in ${LEVELS}`);
  const levels = files.map(name => JSON.parse(fs.readFileSync(path.join(LEVELS, name), "utf8")));
  fs.mkdirSync(path.dirname(LEVEL_PACK), { recursive: true });
  fs.writeFileSync(LEVEL_PACK, JSON.stringify({ levels }));
  console.log(`${levels.length} levels -> ${LEVEL_PACK} (${(fs.statSync(LEVEL_PACK).size / 1048576).toFixed(1)} MB)`);

  // Parsed and written again, not copied: a catalogue that is not JSON stops here, by name, and not inside the game.
  const catalogue = JSON.parse(fs.readFileSync(CATALOGUE, "utf8"));
  fs.writeFileSync(CATALOGUE_COPY, JSON.stringify(catalogue));
  const chosen = catalogue.rooms.reduce((count, room) => count + room.paintings.length, 0);
  console.log(`${catalogue.rooms.length} rooms, ${chosen} paintings chosen -> ${CATALOGUE_COPY}`);

  // A settings file that leaves out a field the game reads stops here, by the field's name, and not inside the game.
  const economy = packedEconomy(fs.readFileSync(ECONOMY, "utf8"), ECONOMY);
  fs.writeFileSync(ECONOMY_PACK, JSON.stringify(economy));
  console.log(`${ECONOMY}: ${economy.prices.boosters.length} booster prices, ${economy.shop.boosterSets.length} sets, ${economy.shop.coinPacks.length} coin packs -> ${ECONOMY_PACK}`);

  // A table that is not JSON, or is named for another language than it says, stops here by name.
  const words = packTables(WORDS, WORDS_PACK, table => ({
    ...table,
    words: table.words.map(({ note, ...entry }) => entry),
  }));
  console.log(`${words.length} tables of words (${words.join(", ")}) -> ${WORDS_PACK}`);
  const titles = packTables(TITLES, TITLES_PACK, table => table);
  console.log(`${titles.length} tables of the museum's words (${titles.join(", ")}) -> ${TITLES_PACK}`);

  const places = JSON.parse(fs.readFileSync(PLACES, "utf8"));
  fs.writeFileSync(PLACES_COPY, JSON.stringify(places));
  console.log(`${places.paintings.length} painting places -> ${PLACES_COPY}`);
  const asEntries = labels => Object.entries(labels ?? {}).map(([key, text]) => ({ key, text }));
  const labels = packTables(PLACE_LABELS, PLACE_LABELS_PACK, table => ({
    language: table.language,
    review: table.review,
    places: asEntries(table.places),
    notes: asEntries(table.notes),
    museums: asEntries(table.museums),
  }));
  console.log(`${labels.length} tables of the map's labels (${labels.join(", ")}) -> ${PLACE_LABELS_PACK}`);
}

/**
 * Writes each <code>.json of a folder, parsed and made by a step, into the pack's folder, which holds nothing else
 * afterwards: a language whose file is gone from the repository is gone from the game too.
 * @returns the codes of the languages packed.
 */
function packTables(folder, pack, make) {
  fs.rmSync(pack, { recursive: true, force: true });
  fs.mkdirSync(pack, { recursive: true });
  const codes = [];
  for (const name of fs.readdirSync(folder).filter(name => name.endsWith(".json")).sort()) {
    const code = name.slice(0, -".json".length);
    const table = JSON.parse(fs.readFileSync(path.join(folder, name), "utf8"));
    if (table.language !== code) throw new Error(`${path.join(folder, name)} says it is of "${table.language}"`);
    fs.writeFileSync(path.join(pack, name), JSON.stringify(make(table)));
    codes.push(code);
  }
  return codes;
}

main();
