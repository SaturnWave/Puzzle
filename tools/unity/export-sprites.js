// Exports the player page's sprite (src/Puzzle.Web/wwwroot/player/pieces.svg) as one transparent PNG per symbol, for
// the Unity client: Unity does not read SVG, and the web test and the game are to show the same pieces.
//
// usage (from the repository root): node tools/unity/export-sprites.js [--out unity/Assets/Art/Pieces] [--sheet file.png]
//   --sheet also writes a contact sheet of every exported sprite on the gallery wall colour, to look at the result.
// Requires Google Chrome or Microsoft Edge (set PUZZLE_BROWSER to the executable when neither is in its usual place).
// Headless Chrome on Windows cannot open a window narrower than about 500 px, hence the 512 px sprites.
const fs = require("fs");
const os = require("os");
const path = require("path");
const { execFileSync } = require("child_process");
const { pathToFileURL } = require("url");

const SPRITE_FILE = "src/Puzzle.Web/wwwroot/player/pieces.svg";
const SPRITE_SIZE = 512;
const SHEET_COLUMNS = 7;
const SHEET_CELL = 144;
const WALL_COLOUR = "#1f5f63";

/** Symbols the player page colours from its stylesheet (the wall lamp): alone they come out blank, so they are left out. */
const STYLED_BY_THE_PAGE = new Set(["sconce"]);
const BROWSERS = [
  process.env.PUZZLE_BROWSER,
  "C:/Program Files/Google/Chrome/Application/chrome.exe",
  "C:/Program Files (x86)/Google/Chrome/Application/chrome.exe",
  "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  "C:/Program Files/Microsoft/Edge/Application/msedge.exe",
];

function main() {
  const options = readOptions(process.argv.slice(2));
  const sprite = fs.readFileSync(SPRITE_FILE, "utf8");
  const symbols = symbolsOf(sprite).filter(symbol => !STYLED_BY_THE_PAGE.has(symbol.id));
  if (symbols.length === 0) throw new Error(`${SPRITE_FILE} has no <symbol>`);

  const browser = BROWSERS.find(candidate => candidate && fs.existsSync(candidate));
  if (!browser) throw new Error("no Chrome or Edge found; set PUZZLE_BROWSER to the browser executable");

  fs.mkdirSync(options.out, { recursive: true });
  const work = fs.mkdtempSync(path.join(os.tmpdir(), "puzzle-sprites-"));
  try {
    const shoot = (html, file, width, height) => screenshot(browser, work, html, file, width, height);
    for (const symbol of symbols) {
      shoot(spritePage(sprite, symbol), path.join(options.out, `${symbol.id}.png`), SPRITE_SIZE, SPRITE_SIZE);
    }
    console.log(`${symbols.length} sprites -> ${options.out}`);
    if (options.sheet) {
      const rows = Math.ceil(symbols.length / SHEET_COLUMNS);
      shoot(sheetPage(symbols, options.out), options.sheet, SHEET_COLUMNS * SHEET_CELL, rows * SHEET_CELL);
      console.log(`contact sheet -> ${options.sheet}`);
    }
  } finally {
    fs.rmSync(work, { recursive: true, force: true });
  }
}

function readOptions(args) {
  const options = { out: "unity/Assets/Art/Pieces", sheet: undefined };
  for (let i = 0; i < args.length; i += 2) {
    const name = args[i];
    const value = args[i + 1];
    if ((name !== "--out" && name !== "--sheet") || value === undefined) {
      throw new Error("usage: node tools/unity/export-sprites.js [--out <folder>] [--sheet <file.png>]");
    }
    options[name.slice(2)] = value;
  }
  return options;
}

/** Every symbol of the sprite with the box it is drawn in; a symbol without a viewBox cannot be sized. */
function symbolsOf(sprite) {
  return [...sprite.matchAll(/<symbol\b[^>]*>/g)].map(([tag]) => {
    const id = /\bid="([^"]+)"/.exec(tag)?.[1];
    const viewBox = /\bviewBox="([^"]+)"/.exec(tag)?.[1];
    if (!id || !viewBox) throw new Error(`a symbol of ${SPRITE_FILE} has no id or no viewBox: ${tag}`);
    return { id, viewBox };
  });
}

/** One symbol filling the page; the sprite itself is inlined because its gradients resolve only inside the page. */
function spritePage(sprite, symbol) {
  return `<!doctype html><meta charset="utf-8">
<style>html, body { margin: 0; background: transparent; } .hidden { position: absolute; width: 0; height: 0; } .shot { display: block; }</style>
<div class="hidden">${sprite}</div>
<svg class="shot" width="${SPRITE_SIZE}" height="${SPRITE_SIZE}" viewBox="${symbol.viewBox}"><use href="#${symbol.id}"/></svg>`;
}

function sheetPage(symbols, folder) {
  const cells = symbols
    .map(symbol => `<figure><img src="${pathToFileURL(path.resolve(folder, `${symbol.id}.png`))}"><figcaption>${symbol.id}</figcaption></figure>`)
    .join("");
  return `<!doctype html><meta charset="utf-8">
<style>
  html, body { margin: 0; background: ${WALL_COLOUR}; }
  body { display: grid; grid-template-columns: repeat(${SHEET_COLUMNS}, ${SHEET_CELL}px); }
  figure { margin: 0; height: ${SHEET_CELL}px; display: grid; justify-items: center; align-content: center; }
  img { width: ${SHEET_CELL - 40}px; height: ${SHEET_CELL - 40}px; }
  figcaption { font: 12px sans-serif; color: #f4ead2; }
</style>${cells}`;
}

/** Writes <paramref name="html"/> to a page file and has the headless browser photograph it on a transparent ground. */
function screenshot(browser, work, html, file, width, height) {
  const page = path.join(work, "page.html");
  fs.writeFileSync(page, html);
  fs.mkdirSync(path.dirname(path.resolve(file)), { recursive: true });
  execFileSync(browser, [
    "--headless=new",
    "--disable-gpu",
    "--hide-scrollbars",
    "--allow-file-access-from-files",
    "--default-background-color=00000000",
    `--user-data-dir=${path.join(work, "profile")}`,
    `--window-size=${width},${height}`,
    `--screenshot=${path.resolve(file)}`,
    pathToFileURL(page).href,
  ], { stdio: "ignore" });
  if (!fs.existsSync(file)) throw new Error(`the browser wrote no screenshot for ${file}`);
}

main();
