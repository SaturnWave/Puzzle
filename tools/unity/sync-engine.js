// Brings the Unity project up to date with the engine and the levels:
//   1. builds Puzzle.Core and Puzzle.Bot (netstandard2.1) and copies the two assemblies into unity/Assets/Generated/Engine;
//   2. packs levels/generated/level_*.json into one document, unity/Assets/Generated/Resources/Levels.json.
// The engine goes in as compiled assemblies so that the game runs the very code the levels were calibrated with.
// Everything written here is derived from the repository, so unity/Assets/Generated is not committed: run this
// script after cloning and whenever the engine or the levels change.
//
// usage (from the repository root): node tools/unity/sync-engine.js
const fs = require("fs");
const path = require("path");
const { execFileSync } = require("child_process");

const ENGINE = "unity/Assets/Generated/Engine";
const LEVELS = "levels/generated";
const LEVEL_PACK = "unity/Assets/Generated/Resources/Levels.json";
const BOT_OUTPUT = "src/Puzzle.Bot/bin/Release/netstandard2.1";

function main() {
  // Building the bot builds the core it depends on; both assemblies land in the bot's output folder.
  execFileSync("dotnet", ["build", "src/Puzzle.Bot", "-c", "Release", "--nologo", "-v", "quiet"], { stdio: "inherit" });
  fs.mkdirSync(ENGINE, { recursive: true });
  for (const assembly of ["Puzzle.Core.dll", "Puzzle.Bot.dll"]) {
    fs.copyFileSync(path.join(BOT_OUTPUT, assembly), path.join(ENGINE, assembly));
  }
  console.log(`engine assemblies -> ${ENGINE}`);

  const files = fs.readdirSync(LEVELS).filter(name => /^level_\d{4}\.json$/.test(name)).sort();
  if (files.length === 0) throw new Error(`no level file in ${LEVELS}`);
  const levels = files.map(name => JSON.parse(fs.readFileSync(path.join(LEVELS, name), "utf8")));
  fs.mkdirSync(path.dirname(LEVEL_PACK), { recursive: true });
  fs.writeFileSync(LEVEL_PACK, JSON.stringify({ levels }));
  console.log(`${levels.length} levels -> ${LEVEL_PACK} (${(fs.statSync(LEVEL_PACK).size / 1048576).toFixed(1)} MB)`);
}

main();
