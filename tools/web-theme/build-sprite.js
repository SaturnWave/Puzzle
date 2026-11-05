// Builds the art of the player page (src/Puzzle.Web/wwwroot/player) from the design's sources, so that a redrawn
// piece reaches the page by running this again and never by hand:
//   pieces.svg   the sprite: every tile, cover, goal icon and the wall's sconce as one <symbol>, under the name the
//                page asks for it by
//   wall.svg     one repeat of the wall's damask, which the stylesheet lays as a background
//   icon.svg     one piece as a picture of its own: the page's icon in a browser's tab
//
// Two things are not shown as the design's sources draw them, by the owner's choice of 2026-10-07:
//   the palette step  puts every gem colour's piece, its goal icon and the kit's gradients only it paints with into
//                     the rich palette (gem-palette.js holds the rule). The page's icon is a gem piece and goes
//                     through the same step.
//   the tray step     deepens the ground shadow under every piece, which was drawn for a light tray, so that it
//                     shows on the dark one.
//
// usage (from the repository root):
//   node tools/web-theme/build-sprite.js [--design <folder>] [--check]
// With --check nothing is written: the three files are built in memory, both steps included, and compared with the
// ones in the page's folder, and the run fails, naming them, when a source, the palette or this script was changed
// without building again.
// Reads  <design>/chosen/Board.dc.html       the kit: the shared gradients, the first symbols and the wall
//         <design>/next/sym/final/<id>.svg    the later symbols: one <symbol> per file, its own definitions in front
//         src/Puzzle.Web/wwwroot/tiles.svg    the lab's sprite: the shape of a gem colour that has no piece yet
// <design> is out/design unless given. The page's Content-Security-Policy drops inline styles and scripts without a
// word, and a reference to a missing gradient paints nothing, so the build refuses to write a file that has either.
"use strict";

const fs = require("node:fs");
const path = require("node:path");
const { inRichPalette, paintsNotRead } = require("./gem-palette.js");

const REPOSITORY = path.join(__dirname, "..", "..");
const WEB_ROOT = path.join(REPOSITORY, "src", "Puzzle.Web", "wwwroot");
const PLAYER_PAGE = path.join(WEB_ROOT, "player");
const LAB_SPRITE = path.join(WEB_ROOT, "tiles.svg");
const DEFAULT_DESIGN = path.join(REPOSITORY, "out", "design");

/** The piece that draws each gem colour of the engine (GemColor). The board asks for a gem as "gem-<colour>". */
const GEM_ART = {
  red: "p-ladybug",
  green: "p-green",
  blue: "p-blue",
  yellow: "p-daisy",
  purple: "p-purple",
  orange: "p-orange",
  cyan: "p-cyan",
  pink: "p-pink",
};

/** Every other symbol the page asks for, and the design's symbol that draws it. */
const OTHER_ART = {
  "sp-rocket-h": "sp-brush-h",
  "sp-rocket-v": "sp-brush-v",
  "sp-bomb": "sp-splat",
  "sp-bigbomb": "sp-bigsplat",
  "sp-seeker": "sp-butterfly",
  "sp-colorbomb": "sp-rainbow",
  "sp-box-1": "o-crate-1",
  "sp-box-2": "o-crate-2",
  "sp-box-3": "o-crate-3",
  "item": "o-item",
  "ov-chain-1": "o-tape-1",
  "ov-chain-2": "o-tape-2",
  "slab-1-0": "v-0",
  "slab-1-1": "v-1",
  "slab-1-2": "v-2",
  "slab-2-0": "v2-0",
  "slab-2-1": "v2-1",
  "slab-2-2": "v2-2",
  "exit": "o-exit",
  "goal-boxes": "i-crate",
  "goal-jelly": "i-varnish",
  "goal-chains": "i-tape",
  "mark-star": "i-star",
};

/** Names of the page that show a symbol the sprite already has under another name. */
const SAME_AS = { "goal-items": "item" };

/**
 * The shadow a piece throws on its ground: the kit's gradient of that name, and what the build paints it with. The
 * kit draws it for the light tray of the first canvas, a teal shade at .42 and .2 that cannot be seen on the dark tray
 * the owner chose. Its three stops keep their places and take this colour, each at its own strength, from the middle
 * outwards. The goal icons on the cream plaques throw the same shadow and were looked at with it.
 */
const GROUND_SHADOW = { id: "g-shadow", colour: "#02141A", strengths: [".6", ".32", "0"] };

/** The gem colour whose piece stands for the game in a browser's tab. */
const ICON_GEM = "red";

/** The lab's symbol for a tile kind the page does not know; the page keeps it as it is. */
const UNKNOWN_TILE = "unknown";

/** What marks the ids taken from the lab's sprite: its gradients have the names of the kit's. */
const LAB_PREFIX = "lab-";

/**
 * The lab draws a gem from edge to edge of its box; a piece of the design leaves room around itself and is shown a
 * tenth larger than its cell. A lab gem that stands in for a piece is therefore drawn this much smaller, in place.
 */
const LAB_GEM_FIT = "translate(10 6) scale(.8)";

const SVG_NAMESPACE = "http://www.w3.org/2000/svg";

/** A source is missing or not built the way this script reads it; the message names the part. */
class ArtSourceError extends Error {
  constructor(message, options) {
    super(message, options);
    this.name = "ArtSourceError";
  }
}

/** The file about to be written would not draw in the page; the message lists every reason. */
class ArtOutputError extends Error {
  constructor(file, problems) {
    super(`${file} was not written:\n  ${problems.join("\n  ")}`);
    this.name = "ArtOutputError";
  }
}

/** The page's folder holds art that is no longer what the sources give; the message names the files. */
class ArtStaleError extends Error {
  constructor(files) {
    super(`Not what the design's sources give now: ${files.join(", ")}. Build again, without --check.`);
    this.name = "ArtStaleError";
  }
}

// ---------- reading ----------
function readSource(file) {
  try {
    return fs.readFileSync(file, "utf8").replaceAll("\r\n", "\n");
  } catch (cause) {
    if (cause.code !== "ENOENT") throw cause;
    throw new ArtSourceError(`${file} is missing.`, { cause });
  }
}

/** The text from `from` to the first `to` after it, both included; `what` names it when it is not there. */
function cut(text, from, to, what) {
  const start = text.indexOf(from);
  const end = start < 0 ? -1 : text.indexOf(to, start + from.length);
  if (end < 0) throw new ArtSourceError(`${what} was not found.`);
  return text.slice(start, end + to.length);
}

/**
 * The element of `markup` that carries the id, whole. It may be self-closing; when it is not, no element of its own
 * kind may sit inside it, since the first closing tag is taken for its end.
 */
function elementWithId(markup, id, what) {
  const opening = new RegExp(`<([A-Za-z]+) id="${id}"[^>]*>`).exec(markup);
  if (!opening) throw new ArtSourceError(`${what} has no element with the id "${id}".`);
  if (opening[0].endsWith("/>")) return opening[0];
  const closing = `</${opening[1]}>`;
  const element = cut(markup.slice(opening.index), opening[0], closing, `the end of #${id} in ${what}`);
  const nested = element.slice(opening[0].length).includes(`<${opening[1]}`);
  if (nested) throw new ArtSourceError(`#${id} in ${what} holds another <${opening[1]}>: its end cannot be told.`);
  return element;
}

/** The ids `markup` points at: every href="#id" and url(#id). */
function referencedIds(markup) {
  return new Set([...markup.matchAll(/href="#([^"]+)"|url\(#([^)]+)\)/g)].map((match) => match[1] ?? match[2]));
}

const definedIds = (markup) => [...markup.matchAll(/\sid="([^"]+)"/g)].map((match) => match[1]);

/** The gradients of a block of definitions, by id. */
function gradientsOf(defs) {
  const gradients = new Map();
  for (const match of defs.matchAll(/<(linearGradient|radialGradient) id="([^"]+)"[^>]*>[\s\S]*?<\/\1>/g)) {
    gradients.set(match[2], match[0]);
  }
  return gradients;
}

/** The chosen board's hidden symbol sheet: `{ gradients, symbols }`, both by id. */
function readKit(board) {
  const sheet = cut(board, '<svg width="0" height="0"', "</svg>", "the kit's symbol sheet");
  const symbols = new Map();
  for (const match of sheet.matchAll(/<symbol id="([^"]+)"[\s\S]*?<\/symbol>/g)) symbols.set(match[1], match[0]);
  if (symbols.size === 0) throw new ArtSourceError("The kit's symbol sheet holds no symbol.");
  return { gradients: gradientsOf(cut(sheet, "<defs>", "</defs>", "the kit's gradients")), symbols };
}

/**
 * A later symbol, from its own file: `{ defs, symbol }`, where `defs` lists the blocks of definitions the symbol
 * brings along. None when the piece is not drawn yet.
 */
function readFragment(fragments, id) {
  const file = path.join(fragments, `${id}.svg`);
  if (!fs.existsSync(file)) return undefined;
  const text = readSource(file).trim();
  const at = text.indexOf("<symbol");
  const isOneSymbol = at >= 0 && text.indexOf("<symbol", at + 1) < 0 && text.includes(`<symbol id="${id}"`) && text.endsWith("</symbol>");
  if (!isOneSymbol) throw new ArtSourceError(`${file} must hold its definitions and then exactly one <symbol id="${id}">.`);
  const ownDefs = text.slice(0, at).trim();
  return { defs: ownDefs === "" ? [] : [ownDefs], symbol: text.slice(at) };
}

/** The design's symbol `id` with the definitions it brings along, from its own file or else from the kit. */
function designArt(sources, id) {
  const fragment = readFragment(sources.fragments, id);
  if (fragment) return fragment;
  const symbol = sources.kit.symbols.get(id);
  return symbol === undefined ? undefined : { defs: [], symbol };
}

// ---------- the lab's shapes ----------
/** The top-level definitions of the lab's sprite that `markup` needs, with all they need in turn, by id. */
function labDefinitionsFor(labSprite, markup) {
  const defs = cut(labSprite, "<defs>", "</defs>", "the lab sprite's definitions");
  const needed = new Map();
  const pending = [...referencedIds(markup)];
  while (pending.length > 0) {
    const id = pending.pop();
    if (needed.has(id)) continue;
    const definition = elementWithId(defs, id, "the lab sprite's definitions");
    needed.set(id, definition);
    pending.push(...referencedIds(definition));
  }
  return needed;
}

/** `markup` with the given ids, and every pointer to them, under the lab's prefix. */
function underLabPrefix(markup, ids) {
  let renamed = markup;
  for (const id of ids) {
    renamed = renamed
      .replaceAll(` id="${id}"`, ` id="${LAB_PREFIX}${id}"`)
      .replaceAll(`href="#${id}"`, `href="#${LAB_PREFIX}${id}"`)
      .replaceAll(`url(#${id})`, `url(#${LAB_PREFIX}${id})`);
  }
  return renamed;
}

/**
 * A symbol of the lab's sprite as the player's sprite carries it: its own id kept, all it draws with renamed. Two
 * lab symbols may bring the same definition; the sprite keeps one of each.
 */
function labArt(labSprite, id) {
  const symbol = elementWithId(labSprite, id, "the lab's sprite");
  const definitions = labDefinitionsFor(labSprite, symbol);
  const ids = [...definitions.keys()];
  return { defs: [...definitions.values()].map((definition) => underLabPrefix(definition, ids)), symbol: underLabPrefix(symbol, ids) };
}

// ---------- the wall ----------
/** One sconce of the chosen wall as a symbol in a box around its glow, with the wall's two gradients it paints with. */
function sconceArt(board) {
  const wall = cut(board, '<svg viewBox="0 0 390 844"', "</svg>", "the wall");
  const [first] = wall.slice(wall.indexOf("</defs>")).split(/(?=<circle [^>]*g-sconce-glow)/).slice(1);
  const glow = /^<circle cx="([\d.]+)" cy="([\d.]+)" r="([\d.]+)"/.exec(first ?? "");
  if (!glow) throw new ArtSourceError("The wall's first sconce does not begin with its glow.");
  const [centreX, centreY, radius] = glow.slice(1).map(Number);
  const gradients = gradientsOf(cut(wall, "<defs>", "</defs>", "the wall's definitions"));
  const defs = [...referencedIds(first)].map((id) => gradients.get(id)).filter((gradient) => gradient !== undefined);
  const viewBox = `${centreX - radius} ${centreY - radius} ${2 * radius} ${2 * radius}`;
  return { defs, symbol: `<symbol id="sconce" viewBox="${viewBox}">\n${first.replace("</svg>", "").trim()}\n</symbol>` };
}

/** One repeat of the wall's damask as a picture of its own. */
function wallTile(board) {
  const pattern = cut(board, '<pattern id="wall-pat"', "</pattern>", "the wall's damask");
  const size = /^<pattern id="wall-pat" width="(\d+)" height="(\d+)" patternUnits="userSpaceOnUse">/.exec(pattern);
  if (!size) throw new ArtSourceError("The wall's damask is not a pattern with a width and a height in px.");
  const drawing = pattern.slice(size[0].length, -"</pattern>".length).trim();
  return `<svg xmlns="${SVG_NAMESPACE}" width="${size[1]}" height="${size[2]}" viewBox="0 0 ${size[1]} ${size[2]}">\n${drawing}\n</svg>\n`;
}

// ---------- the palette step and the tray step ----------
/**
 * The palette step for one piece: the art of gem colour `colour`, its symbol and the definitions it brings along, in
 * the rich palette. A paint the palette cannot read would stay in the old colours beside the new ones, so it is
 * refused here by name.
 */
function inPaletteOf(colour, { defs, symbol }) {
  const notRead = paintsNotRead([...defs, symbol].join("\n"));
  if (notRead.length > 0) {
    throw new ArtSourceError(`The ${colour} piece is painted with ${notRead.join(", ")}: the palette reads only hex colours.`);
  }
  return { defs: defs.map((definitions) => inRichPalette(definitions, colour)), symbol: inRichPalette(symbol, colour) };
}

/**
 * The palette step for the kit's gradients. A gradient that the pieces of one gem colour alone paint with is that
 * colour's and goes into the palette with it; whatever else is painted with it (a drop of that colour on the colour
 * bomb's palette) follows. A gradient that pieces of several colours share is the kit's common shadow or light, and
 * one that no piece uses belongs to something else: both stay as drawn.
 */
function kitGradientsInPalette(sources) {
  const usedBy = new Map();
  for (const colour of Object.keys(GEM_ART)) {
    const drawn = [gemArt(sources, colour), gemGoalArt(sources, colour)].flatMap((art) => [...art.defs, art.symbol]).join("\n");
    for (const id of referencedIds(drawn)) usedBy.set(id, [...(usedBy.get(id) ?? []), colour]);
  }
  const inItsColour = ([id, gradient]) => {
    const colours = usedBy.get(id) ?? [];
    return [id, colours.length === 1 ? inRichPalette(gradient, colours[0]) : gradient];
  };
  return new Map([...sources.kit.gradients].map(inItsColour));
}

/** The tray step: the kit's gradients with the pieces' ground shadow made deep enough to show on the dark tray. */
function withShadowForTheDarkTray(gradients) {
  const drawn = gradients.get(GROUND_SHADOW.id);
  if (drawn === undefined) throw new ArtSourceError(`The kit has no gradient "${GROUND_SHADOW.id}", the pieces' ground shadow.`);
  const stops = [...drawn.matchAll(/<stop offset="([^"]+)"[^>]*>/g)];
  if (stops.length !== GROUND_SHADOW.strengths.length) {
    throw new ArtSourceError(`The kit's ground shadow has ${stops.length} stops; the build knows the strengths of ${GROUND_SHADOW.strengths.length}.`);
  }
  const deepStops = stops.map(([, offset], stop) =>
    `<stop offset="${offset}" stop-color="${GROUND_SHADOW.colour}" stop-opacity="${GROUND_SHADOW.strengths[stop]}"/>`);
  const [opening] = /^<radialGradient [^>]*>/.exec(drawn) ?? [];
  if (opening === undefined) throw new ArtSourceError("The kit's ground shadow is not a radial gradient.");
  return new Map(gradients).set(GROUND_SHADOW.id, `${opening}${deepStops.join("")}</radialGradient>`);
}

// ---------- the sprite ----------
const renamedSymbol = (symbol, from, to) => symbol.replace(`<symbol id="${from}"`, `<symbol id="${to}"`);
const sameAs = (name, shown) => `<symbol id="${name}" viewBox="0 0 100 100"><use href="#${shown}"/></symbol>`;

/** A symbol taken apart: its opening tag, and what it draws. */
function partsOf(symbol) {
  const [opening] = /^<symbol [^>]*>/.exec(symbol);
  return { opening, drawing: symbol.slice(opening.length, -"</symbol>".length).trim() };
}

function fittedLikeAPiece(symbol) {
  const { opening, drawing } = partsOf(symbol);
  return `${opening}<g transform="${LAB_GEM_FIT}">${drawing}</g></symbol>`;
}

/**
 * The art of one gem colour under its page name: the design's piece in the rich palette, or the lab's shape while
 * there is none. The lab's shape is a stand-in with paints of its own and stays as the lab has it.
 */
function gemArt(sources, colour) {
  const name = `gem-${colour}`;
  const piece = designArt(sources, GEM_ART[colour]);
  if (piece === undefined) {
    const shape = labArt(sources.labSprite, name);
    return { defs: shape.defs, symbol: fittedLikeAPiece(shape.symbol), fromLab: true };
  }
  const drawn = { defs: piece.defs, symbol: renamedSymbol(piece.symbol, GEM_ART[colour], name) };
  return { ...inPaletteOf(colour, drawn), fromLab: false };
}

/**
 * A colour's goal icon: the design's icon of the piece, in the piece's palette, where one is drawn; the piece itself
 * where not.
 */
function gemGoalArt(sources, colour) {
  const name = `goal-${colour}`;
  const iconId = GEM_ART[colour].replace(/^p-/, "i-");
  const icon = readFragment(sources.fragments, iconId);
  if (icon === undefined) return { defs: [], symbol: sameAs(name, `gem-${colour}`) };
  return inPaletteOf(colour, { defs: icon.defs, symbol: renamedSymbol(icon.symbol, iconId, name) });
}

function otherArt(sources, name) {
  const piece = designArt(sources, OTHER_ART[name]);
  if (piece === undefined) throw new ArtSourceError(`The design has no symbol "${OTHER_ART[name]}", which draws "${name}".`);
  return { defs: piece.defs, symbol: renamedSymbol(piece.symbol, OTHER_ART[name], name) };
}

/** A gradient of the kit stays only when something left in the sprite paints with it. */
function usedGradients(gradients, rest) {
  const used = referencedIds(rest);
  return [...gradients].filter(([id]) => used.has(id)).map(([, gradient]) => gradient);
}

/** The sprite's text and the colours that are still drawn with the lab's shape. */
function buildSprite(sources) {
  const colours = Object.keys(GEM_ART);
  const gems = colours.map((colour) => gemArt(sources, colour));
  const parts = [
    ...gems,
    ...Object.keys(OTHER_ART).map((name) => otherArt(sources, name)),
    ...colours.map((colour) => gemGoalArt(sources, colour)),
    ...Object.entries(SAME_AS).map(([name, shown]) => ({ defs: [], symbol: sameAs(name, shown) })),
    labArt(sources.labSprite, UNKNOWN_TILE),
    sconceArt(sources.board),
  ];
  const ownDefs = [...new Set(parts.flatMap((part) => part.defs))];
  const symbols = parts.map((part) => part.symbol);
  const shared = usedGradients(sources.kit.gradients, [...ownDefs, ...symbols].join("\n"));
  const text = [
    "<!-- The art of the player page. Built by tools/web-theme/build-sprite.js from the design's symbols: change a",
    "     source and build again, never this file. Every symbol is named as the page asks for it. -->",
    `<svg class="sprite" xmlns="${SVG_NAMESPACE}" aria-hidden="true" focusable="false">`,
    "<defs>",
    ...shared,
    ...ownDefs,
    "</defs>",
    ...symbols,
    "</svg>",
    "",
  ].join("\n");
  return { text, fromLab: colours.filter((_, index) => gems[index].fromLab) };
}

/** The piece of one gem colour as a picture of its own, as the sprite shows it, with the paints it needs. */
function gemPicture(sources, colour) {
  const piece = gemArt(sources, colour);
  const { opening, drawing } = partsOf(piece.symbol);
  const viewBox = / viewBox="[^"]+"/.exec(opening);
  if (!viewBox) throw new ArtSourceError(`The ${colour} piece has no viewBox: its picture would have no size.`);
  const shared = usedGradients(sources.kit.gradients, [...piece.defs, drawing].join("\n"));
  return [`<svg xmlns="${SVG_NAMESPACE}"${viewBox[0]}>`, "<defs>", ...shared, ...piece.defs, "</defs>", drawing, "</svg>", ""].join("\n");
}

// ---------- checks ----------
/** What the page's policy drops, or what would make a tile slow to draw; none of it may be in a picture. */
const FORBIDDEN = [
  [/\sstyle=/, "a style attribute"],
  [/<style/, "a <style> element"],
  [/<script/, "a <script> element"],
  [/\son[a-z]+=/, "an event attribute"],
  [/data:/, "a data: address"],
  [/https?:/, "an address of another site"],
  [/<image|<foreignObject/, "an embedded picture or page"],
  [/<filter|\sfilter=/, "an SVG filter"],
];

/** Every reason `svg` would not draw in the page as meant; empty when there is none. */
function problemsOf(svg) {
  const body = svg.replace(` xmlns="${SVG_NAMESPACE}"`, "");
  const problems = FORBIDDEN.filter(([pattern]) => pattern.test(body)).map(([, what]) => `it holds ${what}`);
  const ids = definedIds(body);
  const twice = ids.filter((id, index) => ids.indexOf(id) !== index);
  if (twice.length > 0) problems.push(`ids defined more than once: ${[...new Set(twice)].join(", ")}`);
  const known = new Set(ids);
  const dangling = [...referencedIds(body)].filter((id) => !known.has(id));
  if (dangling.length > 0) problems.push(`nothing is defined for: ${dangling.join(", ")}`);
  return problems;
}

/** A file's path as the script's messages name it: from the repository's root. */
const nameOf = (file) => path.relative(REPOSITORY, file).replaceAll("\\", "/");

function writeChecked(file, svg) {
  const problems = problemsOf(svg);
  if (problems.length > 0) throw new ArtOutputError(file, problems);
  fs.writeFileSync(file, svg);
  console.log(`${nameOf(file)}  ${Buffer.byteLength(svg)} bytes`);
}

/** Whether the page's folder holds exactly `svg` as `file`; a file that is not there is not it. */
const isStored = (file, svg) => fs.existsSync(file) && readSource(file) === svg;

// ---------- main ----------
function designFolder(args) {
  const at = args.indexOf("--design");
  if (at < 0) return DEFAULT_DESIGN;
  if (args[at + 1] === undefined) throw new ArtSourceError("--design needs the folder that holds chosen/ and next/.");
  return path.resolve(args[at + 1]);
}

/**
 * The page's art as the design's sources give it now: `files`, each `{ file, svg }`, and `fromLab`, the colours that
 * are still drawn with the lab's shape.
 */
function buildArt(design) {
  const board = readSource(path.join(design, "chosen", "Board.dc.html"));
  const drawn = {
    board,
    kit: readKit(board),
    fragments: path.join(design, "next", "sym", "final"),
    labSprite: readSource(LAB_SPRITE),
  };
  const sources = { ...drawn, kit: { ...drawn.kit, gradients: withShadowForTheDarkTray(kitGradientsInPalette(drawn)) } };
  const sprite = buildSprite(sources);
  const files = [
    { file: path.join(PLAYER_PAGE, "pieces.svg"), svg: sprite.text },
    { file: path.join(PLAYER_PAGE, "wall.svg"), svg: wallTile(board) },
    { file: path.join(PLAYER_PAGE, "icon.svg"), svg: gemPicture(sources, ICON_GEM) },
  ];
  return { files, fromLab: sprite.fromLab };
}

function checkArt({ files }) {
  const stale = files.filter(({ file, svg }) => !isStored(file, svg)).map(({ file }) => nameOf(file));
  if (stale.length > 0) throw new ArtStaleError(stale);
  console.log(`Up to date: ${files.map(({ file }) => nameOf(file)).join(", ")}`);
}

function writeArt({ files, fromLab }) {
  for (const { file, svg } of files) writeChecked(file, svg);
  if (fromLab.length > 0) {
    const missing = fromLab.map((colour) => `${colour} (${GEM_ART[colour]}.svg)`).join(", ");
    console.log(`No piece is drawn yet for: ${missing}. These colours keep the lab's shape.`);
  }
}

function main() {
  const args = process.argv.slice(2);
  const art = buildArt(designFolder(args));
  if (args.includes("--check")) checkArt(art); else writeArt(art);
}

try {
  main();
} catch (error) {
  if (!(error instanceof ArtSourceError || error instanceof ArtOutputError || error instanceof ArtStaleError)) throw error;
  console.error(error.message);
  process.exitCode = 1;
}
