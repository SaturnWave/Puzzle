// Exports the Unity client's own drawings of the gallery (art/unity/*.svg: the plaque, the medallion, the shelf, the
// wall's damask, the wall lamp, the seal of a met goal, and the result card with its nameplate, alcove, button,
// sparkle and count badge) as transparent PNG files. Unity does not read SVG. Each
// source says how large its picture is in its root tag's width and height; one unit of its viewBox is one point of
// the design. export-sprites.js does the same for the pieces, from the web test page's sprite.
//
// usage (from the repository root): node tools/unity/export-gallery-art.js [--out unity/Assets/Art/Gallery] [--sheet file.png]
//   --sheet also writes every exported picture side by side on the gallery wall's colour, to look at the result.
// Requires Google Chrome or Microsoft Edge (set PUZZLE_BROWSER to the executable when neither is in its usual place).
const fs = require("fs");
const os = require("os");
const path = require("path");
const { execFileSync } = require("child_process");
const { pathToFileURL } = require("url");

const SOURCE_FOLDER = "art/unity";

/** Headless Chrome on Windows cannot open a window narrower than about 500 px: a narrower picture would come out padded. */
const NARROWEST_PICTURE = 512;
const SHEET_CELL = 300;
const WALL_COLOUR = "#17666f";
const BROWSERS = [
  process.env.PUZZLE_BROWSER,
  "C:/Program Files/Google/Chrome/Application/chrome.exe",
  "C:/Program Files (x86)/Google/Chrome/Application/chrome.exe",
  "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  "C:/Program Files/Microsoft/Edge/Application/msedge.exe",
];

function main() {
  const options = readOptions(process.argv.slice(2));
  const drawings = fs.readdirSync(SOURCE_FOLDER).filter(file => file.endsWith(".svg")).sort().map(readDrawing);
  if (drawings.length === 0) throw new Error(`${SOURCE_FOLDER} holds no .svg`);

  const browser = BROWSERS.find(candidate => candidate && fs.existsSync(candidate));
  if (!browser) throw new Error("no Chrome or Edge found; set PUZZLE_BROWSER to the browser executable");

  fs.mkdirSync(options.out, { recursive: true });
  const work = fs.mkdtempSync(path.join(os.tmpdir(), "puzzle-gallery-art-"));
  try {
    for (const drawing of drawings) {
      screenshot(browser, work, drawingPage(drawing), path.join(options.out, `${drawing.name}.png`), drawing.width, drawing.height);
    }
    console.log(`${drawings.length} pictures -> ${options.out}`);
    if (options.sheet) {
      screenshot(browser, work, sheetPage(drawings, options.out), options.sheet, drawings.length * SHEET_CELL, SHEET_CELL);
      console.log(`contact sheet -> ${options.sheet}`);
    }
  } finally {
    fs.rmSync(work, { recursive: true, force: true });
  }
}

function readOptions(args) {
  const options = { out: "unity/Assets/Art/Gallery", sheet: undefined };
  for (let i = 0; i < args.length; i += 2) {
    const name = args[i];
    const value = args[i + 1];
    if ((name !== "--out" && name !== "--sheet") || value === undefined) {
      throw new Error("usage: node tools/unity/export-gallery-art.js [--out <folder>] [--sheet <file.png>]");
    }
    options[name.slice(2)] = value;
  }
  return options;
}

/** A source with the size of its picture in px, as its root tag gives it. */
function readDrawing(file) {
  const svg = fs.readFileSync(path.join(SOURCE_FOLDER, file), "utf8");
  const root = /<svg\b[^>]*>/.exec(svg)?.[0];
  const width = Number(/\bwidth="(\d+)"/.exec(root ?? "")?.[1]);
  const height = Number(/\bheight="(\d+)"/.exec(root ?? "")?.[1]);
  if (!Number.isInteger(width) || !Number.isInteger(height)) {
    throw new Error(`${file}: the root tag must give the picture's width and height in whole px`);
  }
  if (width < NARROWEST_PICTURE) {
    throw new Error(`${file}: a picture must be at least ${NARROWEST_PICTURE} px wide (the browser's narrowest window), not ${width}`);
  }
  return { name: path.basename(file, ".svg"), svg, width, height };
}

/** The drawing alone on a page of its own size; inlined, so that the styled box inside one is laid out by the page. */
function drawingPage(drawing) {
  return `<!doctype html><meta charset="utf-8">
<style>html, body { margin: 0; background: transparent; } svg { display: block; }</style>
${drawing.svg}`;
}

function sheetPage(drawings, folder) {
  const cells = drawings
    .map(drawing => `<figure><img src="${pathToFileURL(path.resolve(folder, `${drawing.name}.png`))}"><figcaption>${drawing.name} ${drawing.width}x${drawing.height}</figcaption></figure>`)
    .join("");
  return `<!doctype html><meta charset="utf-8">
<style>
  html, body { margin: 0; background: ${WALL_COLOUR}; }
  body { display: grid; grid-template-columns: repeat(${drawings.length}, ${SHEET_CELL}px); }
  figure { margin: 0; height: ${SHEET_CELL}px; display: grid; justify-items: center; align-content: center; }
  img { max-width: ${SHEET_CELL - 20}px; max-height: ${SHEET_CELL - 40}px; }
  figcaption { font: 12px sans-serif; color: #f4ead2; }
</style>${cells}`;
}

/** Writes the page to a file and has the headless browser photograph it on a transparent ground. */
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
