// Brings the Unity project up to date with the engine, the museum's rules and the content:
//   1. builds Puzzle.Bot and Puzzle.Museum (netstandard2.1; each builds the Puzzle.Core it depends on) and copies
//      the three assemblies into unity/Assets/Generated/Engine;
//   2. packs levels/generated/level_*.json into one document, unity/Assets/Generated/Resources/Levels.json;
//   3. copies the museum's catalogue, museum/catalogue.json, to unity/Assets/Generated/Resources/Museum.json.
// The engine and the museum's rules go in as compiled assemblies so that the game runs the very code the levels
// were calibrated with and the tools and tests check.
// Everything written here is derived from the repository, so unity/Assets/Generated is not committed: run this
// script after cloning and whenever the engine, the museum's rules, the levels or the catalogue change.
//
// usage (from the repository root): node tools/unity/sync-engine.js
const fs = require("fs");
const path = require("path");
const { execFileSync } = require("child_process");

const ENGINE = "unity/Assets/Generated/Engine";
const LEVELS = "levels/generated";
const LEVEL_PACK = "unity/Assets/Generated/Resources/Levels.json";
const CATALOGUE = "museum/catalogue.json";
const CATALOGUE_COPY = "unity/Assets/Generated/Resources/Museum.json";
// Each project with the assemblies taken from its output folder. The core lands in both; it is taken once.
const PROJECTS = [
  { project: "src/Puzzle.Bot", assemblies: ["Puzzle.Core.dll", "Puzzle.Bot.dll"] },
  { project: "src/Puzzle.Museum", assemblies: ["Puzzle.Museum.dll"] },
];

function main() {
  fs.mkdirSync(ENGINE, { recursive: true });
  for (const { project, assemblies } of PROJECTS) {
    execFileSync("dotnet", ["build", project, "-c", "Release", "--nologo", "-v", "quiet"], { stdio: "inherit" });
    for (const assembly of assemblies) {
      fs.copyFileSync(path.join(project, "bin/Release/netstandard2.1", assembly), path.join(ENGINE, assembly));
    }
  }
  console.log(`engine and museum assemblies -> ${ENGINE}`);

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
}

main();
