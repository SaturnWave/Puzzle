// Makes the lining figures of a TrueType font the figures it draws by default.
//
// Young Serif draws old-style figures unless it is asked otherwise: 0, 1 and 2 as high as a small letter, so that
// "10" reads as the Turkish letters "ıo", and 3, 4, 5, 7 and 9 hanging under the line. Figures as high as its
// capitals are in the file too, behind its OpenType feature 'lnum', and the design canvas asks for them
// (font-variant-numeric: lining-nums). TextMesh Pro draws the glyphs a font's character map names and cannot turn
// such a feature on. So the game's copy of the font answers the ten figures with the glyphs its own 'lnum' feature
// replaces them by: one change to the character map; every drawing, every width and every spacing of a pair stays
// as its designers made it.
//
// usage: const { withLiningFigures } = require("./lining-figures"); fetch-fonts.js applies it to the fonts that ask for it.

const TRUETYPE = 0x00010000;
const FIRST_FIGURE = 0x30;
const LAST_FIGURE = 0x39;
const LINING_FIGURES = "lnum";

/** GSUB lookup types: one glyph for one glyph, and the wrapper that holds any other type at a 32-bit offset. */
const SINGLE_SUBSTITUTION = 1;
const EXTENSION = 7;

/** The character map of the common kind: 16-bit codes in segments. */
const SEGMENTED_MAP = 4;
const LAST_CODE = 0xffff;

/** A font file's sum of its own 32-bit words comes to this once its head table states the right adjustment. */
const SUM_OF_A_WHOLE_FONT = 0xb1b0afba;
const ADJUSTMENT_IN_HEAD = 8;

/**
 * The font with its figures 0 to 9 mapped to their lining forms.
 * @param {Buffer} font A static TrueType font.
 * @returns {Buffer} The same font with another character map.
 * @throws {Error} The bytes are not such a font, it has no 'lnum' feature that replaces all ten figures one for
 *   one, or its character map is of a kind this script does not read.
 */
function withLiningFigures(font) {
  const tables = readTables(font);
  const gsub = tables.find(table => table.tag === "GSUB");
  const cmap = tables.find(table => table.tag === "cmap");
  if (!gsub || !cmap) throw new Error("the font has no GSUB or no cmap table: it names no lining figures");

  cmap.bytes = characterMapThrough(cmap.bytes, liningFormsIn(gsub.bytes));
  return writeFont(tables);
}

/** The tables of a font in the order of their records, each with its own copy of its bytes. */
function readTables(font) {
  if (font.length < 12 || font.readUInt32BE(0) !== TRUETYPE) throw new Error("the bytes are not a TrueType font");
  const tables = [];
  for (let record = 0; record < font.readUInt16BE(4); record++) {
    const at = 12 + 16 * record;
    const offset = font.readUInt32BE(at + 8);
    tables.push({ tag: font.toString("latin1", at, at + 4), offset, bytes: Buffer.from(font.subarray(offset, offset + font.readUInt32BE(at + 12))) });
  }
  return tables;
}

/** What the font's 'lnum' feature replaces each glyph by: glyph number to glyph number. */
function liningFormsIn(gsub) {
  const features = gsub.readUInt16BE(6);
  const lookups = gsub.readUInt16BE(8);
  const forms = new Map();
  for (let record = 0; record < gsub.readUInt16BE(features); record++) {
    const at = features + 2 + 6 * record;
    if (gsub.toString("latin1", at, at + 4) !== LINING_FIGURES) continue;
    const feature = features + gsub.readUInt16BE(at + 4);
    for (let index = 0; index < gsub.readUInt16BE(feature + 2); index++) {
      const lookup = lookups + gsub.readUInt16BE(lookups + 2 + 2 * gsub.readUInt16BE(feature + 4 + 2 * index));
      for (const [glyph, form] of singleSubstitutionsOf(gsub, lookup)) forms.set(glyph, form);
    }
  }
  if (forms.size === 0) throw new Error("the font has no 'lnum' feature that replaces one glyph by another");
  return forms;
}

/** The pairs of a lookup that replaces one glyph by one glyph. */
function* singleSubstitutionsOf(gsub, lookup) {
  const type = gsub.readUInt16BE(lookup);
  for (let index = 0; index < gsub.readUInt16BE(lookup + 4); index++) {
    let subtable = lookup + gsub.readUInt16BE(lookup + 6 + 2 * index);
    let subtableType = type;
    if (subtableType === EXTENSION) {
      subtableType = gsub.readUInt16BE(subtable + 2);
      subtable += gsub.readUInt32BE(subtable + 4);
    }
    if (subtableType !== SINGLE_SUBSTITUTION) throw new Error(`the 'lnum' feature uses a lookup of type ${subtableType}, not one glyph for one glyph`);

    const covered = coveredGlyphs(gsub, subtable + gsub.readUInt16BE(subtable + 2));
    const format = gsub.readUInt16BE(subtable);
    for (let place = 0; place < covered.length; place++) {
      // Format 1 moves every covered glyph by one number; format 2 lists a glyph for each.
      yield [covered[place], format === 1 ? (covered[place] + gsub.readInt16BE(subtable + 4)) & 0xffff : gsub.readUInt16BE(subtable + 6 + 2 * place)];
    }
  }
}

/** The glyphs a coverage table names, in its order: a list of glyphs, or ranges of them. */
function coveredGlyphs(gsub, coverage) {
  const glyphs = [];
  const count = gsub.readUInt16BE(coverage + 2);
  if (gsub.readUInt16BE(coverage) === 1) {
    for (let index = 0; index < count; index++) glyphs.push(gsub.readUInt16BE(coverage + 4 + 2 * index));
    return glyphs;
  }
  for (let range = 0; range < count; range++) {
    const at = coverage + 4 + 6 * range;
    for (let glyph = gsub.readUInt16BE(at); glyph <= gsub.readUInt16BE(at + 2); glyph++) glyphs.push(glyph);
  }
  return glyphs;
}

/** The character map with the figures' glyphs replaced by their forms; every other code keeps its glyph. */
function characterMapThrough(cmap, forms) {
  const records = [];
  const written = new Map();
  for (let record = 0; record < cmap.readUInt16BE(2); record++) {
    const at = 4 + 8 * record;
    const glyphs = glyphsBySegments(cmap, cmap.readUInt32BE(at + 4));
    for (let code = FIRST_FIGURE; code <= LAST_FIGURE; code++) {
      const form = forms.get(glyphs.get(code));
      if (form === undefined) throw new Error(`the font has no lining form for the figure ${String.fromCharCode(code)}`);
      glyphs.set(code, form);
    }
    const subtable = segmentedMapOf(glyphs, cmap.readUInt16BE(cmap.readUInt32BE(at + 4) + 4));
    const key = subtable.toString("latin1");
    if (!written.has(key)) written.set(key, subtable);
    records.push({ platform: cmap.readUInt16BE(at), encoding: cmap.readUInt16BE(at + 2), key });
  }

  // Maps that say the same are written once and named by each of their records, as the font had them.
  const header = Buffer.alloc(4 + 8 * records.length);
  header.writeUInt16BE(cmap.readUInt16BE(0), 0);
  header.writeUInt16BE(records.length, 2);
  const offsets = new Map();
  let offset = header.length;
  for (const [key, subtable] of written) {
    offsets.set(key, offset);
    offset += subtable.length;
  }
  records.forEach((record, index) => {
    header.writeUInt16BE(record.platform, 4 + 8 * index);
    header.writeUInt16BE(record.encoding, 4 + 8 * index + 2);
    header.writeUInt32BE(offsets.get(record.key), 4 + 8 * index + 4);
  });
  return Buffer.concat([header, ...written.values()]);
}

/** The glyph of every code of a segmented character map. */
function glyphsBySegments(cmap, subtable) {
  if (cmap.readUInt16BE(subtable) !== SEGMENTED_MAP) {
    throw new Error(`the font has a character map of format ${cmap.readUInt16BE(subtable)}; only format ${SEGMENTED_MAP} is read`);
  }
  const segments = cmap.readUInt16BE(subtable + 6) / 2;
  const ends = subtable + 14;
  const starts = ends + 2 * segments + 2;
  const deltas = starts + 2 * segments;
  const rangeOffsets = deltas + 2 * segments;
  const glyphs = new Map();
  for (let segment = 0; segment < segments; segment++) {
    const start = cmap.readUInt16BE(starts + 2 * segment);
    const end = cmap.readUInt16BE(ends + 2 * segment);
    const delta = cmap.readUInt16BE(deltas + 2 * segment);
    const rangeOffset = cmap.readUInt16BE(rangeOffsets + 2 * segment);
    for (let code = start; code <= end && code < LAST_CODE; code++) {
      // A segment either moves its codes by one number, or names a list of glyphs that lies after the segments,
      // where a zero says that the code has no glyph.
      const listed = rangeOffset === 0 ? code : cmap.readUInt16BE(rangeOffsets + 2 * segment + rangeOffset + 2 * (code - start));
      const glyph = rangeOffset !== 0 && listed === 0 ? 0 : (listed + delta) & 0xffff;
      if (glyph !== 0) glyphs.set(code, glyph);
    }
  }
  return glyphs;
}

/** A segmented character map of the codes given: a segment for every run of codes whose glyphs run with them. */
function segmentedMapOf(glyphs, language) {
  const runs = [];
  for (const code of [...glyphs.keys()].sort((a, b) => a - b)) {
    const run = runs[runs.length - 1];
    const delta = (glyphs.get(code) - code) & 0xffff;
    if (run && run.end + 1 === code && run.delta === delta) run.end = code;
    else runs.push({ start: code, end: code, delta });
  }
  runs.push({ start: LAST_CODE, end: LAST_CODE, delta: 1 }); // the segment that ends every such map

  const segments = runs.length;
  const length = 16 + 8 * segments;
  if (length > 0xffff) throw new Error("the character map has more segments than its format holds");
  const searchRange = 2 * 2 ** Math.floor(Math.log2(segments));
  const subtable = Buffer.alloc(length);
  subtable.writeUInt16BE(SEGMENTED_MAP, 0);
  subtable.writeUInt16BE(length, 2);
  subtable.writeUInt16BE(language, 4);
  subtable.writeUInt16BE(2 * segments, 6);
  subtable.writeUInt16BE(searchRange, 8);
  subtable.writeUInt16BE(Math.log2(searchRange / 2), 10);
  subtable.writeUInt16BE(2 * segments - searchRange, 12);
  runs.forEach((run, index) => {
    subtable.writeUInt16BE(run.end, 14 + 2 * index);
    subtable.writeUInt16BE(run.start, 16 + 2 * segments + 2 * index);
    subtable.writeUInt16BE(run.delta, 16 + 4 * segments + 2 * index);
    // The fourth list, of offsets into a list of glyphs, stays zero: every segment moves its codes by its delta.
  });
  return subtable;
}

/** A font file of the tables given: their records in the order they had, their bytes in the order they lay in the file. */
function writeFont(tables) {
  const directory = Buffer.alloc(12 + 16 * tables.length);
  const searchRange = 16 * 2 ** Math.floor(Math.log2(tables.length));
  directory.writeUInt32BE(TRUETYPE, 0);
  directory.writeUInt16BE(tables.length, 4);
  directory.writeUInt16BE(searchRange, 6);
  directory.writeUInt16BE(Math.log2(searchRange / 16), 8);
  directory.writeUInt16BE(16 * tables.length - searchRange, 10);

  const head = tables.find(table => table.tag === "head");
  if (!head) throw new Error("the font has no head table");
  head.bytes.writeUInt32BE(0, ADJUSTMENT_IN_HEAD);

  const parts = [directory];
  let offset = directory.length;
  const places = new Map();
  for (const table of [...tables].sort((a, b) => a.offset - b.offset)) {
    const padded = paddedToWords(table.bytes);
    places.set(table.tag, offset);
    parts.push(padded);
    offset += padded.length;
  }
  tables.forEach((table, index) => {
    const at = 12 + 16 * index;
    directory.write(table.tag, at, "latin1");
    directory.writeUInt32BE(sumOfWords(paddedToWords(table.bytes)), at + 4);
    directory.writeUInt32BE(places.get(table.tag), at + 8);
    directory.writeUInt32BE(table.bytes.length, at + 12);
  });

  const font = Buffer.concat(parts);
  font.writeUInt32BE((SUM_OF_A_WHOLE_FONT - sumOfWords(font)) >>> 0, places.get("head") + ADJUSTMENT_IN_HEAD);
  return font;
}

/** A table's bytes with zeros after them up to a whole number of 32-bit words, as it lies in a font file. */
function paddedToWords(bytes) {
  const padding = (4 - (bytes.length % 4)) % 4;
  return padding === 0 ? bytes : Buffer.concat([bytes, Buffer.alloc(padding)]);
}

function sumOfWords(bytes) {
  let sum = 0;
  for (let at = 0; at < bytes.length; at += 4) sum = (sum + bytes.readUInt32BE(at)) >>> 0;
  return sum;
}

module.exports = { withLiningFigures };
