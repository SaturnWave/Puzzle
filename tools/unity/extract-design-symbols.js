// Takes the symbols the Unity client draws out of the design canvas and writes each as a stand-alone drawing in
// art/unity, where tools/unity/export-gallery-art.js finds it. The canvas' artboards (out/design/next/project/
// *.dc.html, a local copy of the owner's canvas) are not in git; the files written here are, so the game's art can
// be exported again without the canvas.
//
// A symbol of the canvas is a <symbol> in an artboard's hidden sprite, painted with gradients and cut by clip
// paths that stand in the same artboard, and sometimes built from other symbols. The drawing written here is the
// symbol as it is, with exactly the paints, clip paths and symbols it uses, in a picture of the symbol's own box.
//
// usage (from the repository root): node tools/unity/extract-design-symbols.js [--from out/design/next/project] [--out art/unity]
// After it: node tools/unity/export-gallery-art.js, then ProjectSetup.Run in the Editor's batch mode.
const fs = require("fs");
const path = require("path");

/** Headless Chrome cannot photograph a picture narrower than this (see export-gallery-art.js), so every drawing is this wide. */
const PICTURE_WIDTH = 512;
const COMMENT_WIDTH = 116;

/**
 * The drawings to write: the file in art/unity, the canvas' symbol, the artboard it is read from (a symbol that
 * several artboards carry is the same in each), and what the drawing is, for the head of its file.
 */
const DRAWINGS = [
  { file: "icon-coin", symbol: "i-coin", from: "HomeTR", is: "A gold coin with a drop of paint struck on it: the mark of the player's coins, on the wallet, on a price and on what a level pays." },
  { file: "icon-heart", symbol: "i-heart", from: "HomeTR", is: "The red heart of the player's lives." },
  { file: "icon-gear", symbol: "i-gear", from: "HomeTR", is: "The ivory gear on the round button that opens the settings." },
  { file: "icon-shop", symbol: "n-shop", from: "HomeTR", is: "The shop's mark on the navigation bar: a market stall under a striped awning." },
  { file: "picture-gift", symbol: "w-gift", from: "HomeTR", is: "The picture in the small hung frame of the free gift: a parcel in a red ribbon on velvet. It fills its whole box, corners too; the frame is drawn over it." },
  { file: "icon-back", symbol: "i-back", from: "JigsawTR", is: "The ivory arrow to the left on the round button that leads back from a screen." },
  { file: "icon-pause", symbol: "i-pause", from: "BoardTR", is: "The two white bars on the shelf's round button that stops a level." },
  { file: "icon-spatula", symbol: "i-spatula", from: "BoardTR", is: "The painter's spatula: the booster that takes one piece off the board." },
  { file: "icon-shuffle", symbol: "i-shuffle", from: "BoardTR", is: "A palette between two turning arrows: the booster that shuffles the board." },
  { file: "icon-moves-3", symbol: "i-moves3", from: "LevelStartTR", is: "The gilt token that says +3: the booster of three more moves, chosen before a level." },
  { file: "icon-moves-5", symbol: "i-moves5", from: "LoseTR", is: "The gilt token that says +5: the offer of five more moves when a level's moves have run out." },
  { file: "coins-pile", symbol: "sh-pile", from: "ShopTR", is: "A handful of coins: the shop's smallest pack, and the mark of the coins an advertisement gives." },
  { file: "coins-pouch", symbol: "sh-pouch", from: "ShopTR", is: "A red pouch of coins: the shop's middle pack." },
  { file: "coins-chest", symbol: "sh-chest", from: "ShopTR", is: "An open chest of coins: the shop's largest pack." },
  { file: "icon-sound", symbol: "st-sound", from: "SettingsTR", is: "A loudspeaker: the mark of the settings' row for sound." },
  { file: "icon-music", symbol: "st-music", from: "SettingsTR", is: "Two notes: the mark of the settings' row for music." },
  { file: "icon-buzz", symbol: "st-buzz", from: "SettingsTR", is: "A phone that shakes: the mark of the settings' row for vibration." },
  { file: "icon-globe", symbol: "st-globe", from: "SettingsTR", is: "A globe: the mark of the settings' row for the language." },
  { file: "icon-shield", symbol: "st-shield", from: "SettingsTR", is: "A shield with a tick: the mark of the settings' row for privacy and data." },
  { file: "icon-help", symbol: "st-help", from: "SettingsTR", is: "A question mark: the mark of the settings' row for support." },
];

/** What a symbol can use by name: paints, clip paths and the like, and other symbols. */
const NAMED_ELEMENTS = /<(linearGradient|radialGradient|clipPath|filter|pattern|mask|symbol)\b[^>]*?\bid="([^"]+)"[^>]*?(?:\/>|>[\s\S]*?<\/\1>)/g;
const USES = /url\(#([^)]+)\)|\bhref="#([^"]+)"/g;

function main() {
  const options = readOptions(process.argv.slice(2));
  if (!fs.existsSync(options.from)) {
    throw new Error(`${options.from} is missing: the design canvas' artboards are a local copy that git does not keep. The drawings already in ${options.out} stay as they are.`);
  }

  fs.mkdirSync(options.out, { recursive: true });
  const artboards = new Map();
  for (const drawing of DRAWINGS) {
    if (!artboards.has(drawing.from)) artboards.set(drawing.from, readArtboard(options.from, drawing.from));
    fs.writeFileSync(path.join(options.out, `${drawing.file}.svg`), standAlone(drawing, artboards.get(drawing.from)));
  }
  console.log(`${DRAWINGS.length} symbols of ${artboards.size} artboards -> ${options.out}`);
}

function readOptions(args) {
  const options = { from: "out/design/next/project", out: "art/unity" };
  for (let i = 0; i < args.length; i += 2) {
    const name = args[i];
    const value = args[i + 1];
    if ((name !== "--from" && name !== "--out") || value === undefined) {
      throw new Error("usage: node tools/unity/extract-design-symbols.js [--from <folder of *.dc.html>] [--out <folder>]");
    }
    options[name.slice(2)] = value;
  }
  return options;
}

/** Everything an artboard defines under a name, by that name: the whole element as the artboard writes it. */
function readArtboard(folder, name) {
  const file = path.join(folder, `${name}.dc.html`);
  if (!fs.existsSync(file)) throw new Error(`${file} is missing`);
  const named = new Map();
  for (const [element, , id] of fs.readFileSync(file, "utf8").matchAll(NAMED_ELEMENTS)) {
    if (named.has(id)) throw new Error(`${file} defines ${id} twice`);
    named.set(id, element);
  }
  return { file, named };
}

/** The drawing's file: the symbol's own content in a picture of its box, after the definitions it uses. */
function standAlone(drawing, artboard) {
  const symbol = artboard.named.get(drawing.symbol);
  const opening = /^<symbol\b[^>]*>/.exec(symbol ?? "")?.[0];
  if (!opening) throw new Error(`${artboard.file} has no symbol ${drawing.symbol}`);

  const box = /\bviewBox="([-\d.]+) ([-\d.]+) ([-\d.]+) ([-\d.]+)"/.exec(opening)?.slice(1).map(Number);
  if (!box) throw new Error(`${artboard.file}: symbol ${drawing.symbol} states no box`);
  const height = PICTURE_WIDTH * box[3] / box[2];
  if (!Number.isInteger(height)) throw new Error(`${artboard.file}: symbol ${drawing.symbol} has a box of ${box[2]} x ${box[3]}, which is no whole number of px high at ${PICTURE_WIDTH} px wide`);

  const content = symbol.slice(opening.length, -"</symbol>".length).trim();
  const definitions = usedBy(content, artboard, drawing.symbol).map(id => artboard.named.get(id));
  const head = `${drawing.is} As the design canvas draws it (${drawing.from}): its symbol ${drawing.symbol} with the paints it uses, `
    + "written by tools/unity/extract-design-symbols.js. Change the canvas and run that again; do not change this file by hand. "
    + `One unit is one point where the symbol is drawn ${box[2]} points wide; the game draws it at the size a place needs.`;
  return [
    comment(head),
    `<svg xmlns="http://www.w3.org/2000/svg" width="${PICTURE_WIDTH}" height="${height}" viewBox="${box.join(" ")}">`,
    ...(definitions.length > 0 ? ["<defs>", ...definitions, "</defs>"] : []),
    content,
    "</svg>",
    "",
  ].join("\n");
}

/** The names a piece of the artboard uses, and those that these use in turn, in the order the artboard defines them. */
function usedBy(content, artboard, symbol) {
  const used = new Set();
  const toRead = [content];
  while (toRead.length > 0) {
    for (const [, paint, link] of toRead.pop().matchAll(USES)) {
      const id = paint ?? link;
      if (used.has(id)) continue;
      const element = artboard.named.get(id);
      if (!element) throw new Error(`${artboard.file}: symbol ${symbol} uses ${id}, which the artboard does not define`);
      used.add(id);
      toRead.push(element);
    }
  }
  return [...artboard.named.keys()].filter(id => used.has(id));
}

/** A comment as the hand-written drawings carry theirs: lines of words, the later ones set in under the first. */
function comment(text) {
  const lines = [];
  let line = "<!--";
  for (const word of text.split(" ")) {
    if (line.length + 1 + word.length > COMMENT_WIDTH) {
      lines.push(line);
      line = "    ";
    }
    line += ` ${word}`;
  }
  return [...lines, `${line} -->`].join("\n");
}

main();
