// Packs the game's economy settings for the Unity client: the sections of economy/default.json that the game plays
// by (lives, coins, rewards, the extra-moves offer, the booster prices, the start, when the boosters used during play
// open, the shop and what the build can do), without the sections that only describe simulated players, and with a list of named entries wherever the
// settings have an object keyed by a name, because Unity's JSON reader knows no such object.
// tools/unity/sync-engine.js writes the pack to unity/Assets/Generated/Resources/Economy.json; the game reads it
// through Puzzle.Meta's EconomyFileMapper, which checks what the numbers mean. This script checks only that every
// field the game reads is stated: Unity's reader would take a missing number for 0 and say nothing.
//
// The pack of the shipped settings is pinned in tests/fixtures/economy-pack.json, so that the tests see the file
// the game sees. After a change of economy/default.json, pin it again (from the repository root):
//   node tools/unity/economy-pack.js --pin
const fs = require("fs");

const SETTINGS = "economy/default.json";
const PINNED_PACK = "tests/fixtures/economy-pack.json";

// The layout of the pack; Puzzle.Meta's EconomyFile.CurrentFormatVersion is the reader's side of it.
const FORMAT_VERSION = 2;

const TRAILING_COMMA_CLOSES = /\s*[}\]]/y;

/** Where the string that opens at `start` ends: the place after its closing quote. */
function endOfString(text, start) {
  for (let at = start + 1; at < text.length; at++) {
    if (text[at] === "\\") at++;
    else if (text[at] === '"') return at + 1;
  }
  throw new SyntaxError("a string is never closed");
}

// The text without its comments: those from two slashes to the end of the line, and those between slash-star and
// star-slash. What stands inside a string is no comment.
function withoutComments(text) {
  let plain = "";
  let at = 0;
  while (at < text.length) {
    if (text[at] === '"') {
      const end = endOfString(text, at);
      plain += text.slice(at, end);
      at = end;
    } else if (text.startsWith("//", at)) {
      while (at < text.length && text[at] !== "\n" && text[at] !== "\r") at++;
    } else if (text.startsWith("/*", at)) {
      const close = text.indexOf("*/", at + 2);
      if (close < 0) throw new SyntaxError("a comment that opens with /* is never closed");
      at = close + 2;
    } else {
      plain += text[at];
      at++;
    }
  }
  return plain;
}

/** The comment-free text without the commas that stand before a closing brace or bracket. A comma inside a string stays. */
function withoutTrailingCommas(text) {
  let plain = "";
  let at = 0;
  while (at < text.length) {
    if (text[at] === '"') {
      const end = endOfString(text, at);
      plain += text.slice(at, end);
      at = end;
      continue;
    }

    const isTrailingComma = text[at] === "," && closesAt(text, at + 1);
    if (!isTrailingComma) plain += text[at];
    at++;
  }
  return plain;
}

/** True when nothing but space stands between `at` and a closing brace or bracket. */
function closesAt(text, at) {
  TRAILING_COMMA_CLOSES.lastIndex = at;
  return TRAILING_COMMA_CLOSES.test(text);
}

/** A settings file as a value: JSON in which comments and trailing commas are allowed, as the simulator's loader allows them. */
function parsedSettings(text, fileName) {
  let settings;
  try {
    settings = JSON.parse(withoutTrailingCommas(withoutComments(text)));
  } catch (error) {
    if (!(error instanceof SyntaxError)) throw error;
    throw new Error(`${fileName} is not a settings file: ${error.message}`, { cause: error });
  }

  if (settings === null || typeof settings !== "object" || Array.isArray(settings)) {
    throw new Error(`${fileName} is not a settings file: a JSON object is expected`);
  }
  return settings;
}

/** One object of the settings, which knows its path so that a missing or misshapen field is refused by name. */
class Section {
  constructor(fields, fileName, path) {
    this.fields = fields;
    this.fileName = fileName;
    this.path = path;
  }

  pathOf(name) {
    return this.path === "" ? name : `${this.path}.${name}`;
  }

  refuse(name, problem) {
    return new Error(`${this.fileName}: '${this.pathOf(name)}' ${problem}`);
  }

  stated(name) {
    if (!Object.hasOwn(this.fields, name)) throw this.refuse(name, "is missing; the game reads it, and every field must be stated");
    return this.fields[name];
  }

  section(name) {
    const fields = this.stated(name);
    if (fields === null || typeof fields !== "object" || Array.isArray(fields)) throw this.refuse(name, "must be an object");
    return new Section(fields, this.fileName, this.pathOf(name));
  }

  number(name) {
    const value = this.stated(name);
    if (!Number.isSafeInteger(value)) throw this.refuse(name, `must be a whole number, not ${JSON.stringify(value)}`);
    return value;
  }

  word(name) {
    const value = this.stated(name);
    if (typeof value !== "string") throw this.refuse(name, `must be a word in quotes, not ${JSON.stringify(value)}`);
    return value;
  }

  words(name) {
    const list = this.stated(name);
    if (!Array.isArray(list) || list.some((value) => typeof value !== "string")) throw this.refuse(name, "must be a list of names in quotes");
    return list;
  }

  /** The numbers of the named fields, under their names. */
  numbers(...names) {
    return Object.fromEntries(names.map((name) => [name, this.number(name)]));
  }

  /** The names this object is keyed by, in the order the file states them. */
  names() {
    return Object.keys(this.fields);
  }
}

/** An object of numbers keyed by a name, as a list: `{ "hit-cell": 400 }` becomes `[{ booster: "hit-cell", coins: 400 }]`. */
function listed(section, nameField, valueField) {
  return section.names().map((name) => ({ [nameField]: name, [valueField]: section.number(name) }));
}

function rewardOf(reward, ...numbers) {
  return { ...reward.numbers(...numbers), boosterRotation: reward.words("boosterRotation") };
}

function shopOf(shop) {
  const coinPacks = shop.section("coinPacks");
  const boosterSets = shop.section("boosterSets");
  return {
    coinPacks: coinPacks.names().map((name) => ({ name, coins: coinPacks.section(name).number("coins") })),
    boosterSets: boosterSets.names().map((name) => {
      const set = boosterSets.section(name);
      return { name, priceCoins: set.number("priceCoins"), boosters: listed(set.section("boosters"), "booster", "count") };
    }),
    adForCoins: shop.section("adForCoins").numbers("coins", "perDay"),
  };
}

/**
 * The pack of a whole settings file (not of a variant, which states only what it changes).
 * Throws an Error that names the file and the field when the text is not such a file.
 */
function packedEconomy(settingsText, fileName) {
  const settings = new Section(parsedSettings(settingsText, fileName), fileName, "");
  const rewards = settings.section("rewards");
  const start = settings.section("start");
  const unlocks = settings.section("boosterUnlocks");
  const build = settings.section("build");
  return {
    formatVersion: FORMAT_VERSION,
    lives: settings.section("lives").numbers("max", "minutesPerLife"),
    coins: settings.section("coins").numbers("perMoveLeftInFinale", "perLevelWon"),
    rewards: {
      paintingCompleted: rewardOf(rewards.section("paintingCompleted"), "coins", "boostersEachTime"),
      daily: rewardOf(rewards.section("daily"), "coins", "everyDays", "boostersEachTime"),
    },
    extraMovesOffer: settings.section("extraMovesOffer").numbers("freeAdsPerDay", "priceCoins"),
    prices: { boosters: listed(settings.section("prices").section("boosters"), "booster", "coins") },
    start: { coins: start.number("coins"), boosters: listed(start.section("boosters"), "booster", "count") },
    boosterUnlocks: { levels: listed(unlocks.section("levels"), "booster", "level"), gift: listed(unlocks.section("gift"), "booster", "count") },
    startBoosters: settings.section("startBoosters").numbers("perAttempt"),
    shop: shopOf(settings.section("shop")),
    build: { ads: build.word("ads"), realMoneyPurchases: build.word("realMoneyPurchases") },
  };
}

/** A pack as the text of its file: laid out for a reader's eye, since a change of it is reviewed. */
function packText(pack) {
  return `${JSON.stringify(pack, null, 2)}\n`;
}

module.exports = { FORMAT_VERSION, PINNED_PACK, SETTINGS, packText, packedEconomy, parsedSettings };

if (require.main === module) {
  if (process.argv.length !== 3 || process.argv[2] !== "--pin") {
    console.error(`usage (from the repository root): node tools/unity/economy-pack.js --pin\n  writes the pack of ${SETTINGS} to ${PINNED_PACK}`);
    process.exit(2);
  }

  fs.writeFileSync(PINNED_PACK, packText(packedEconomy(fs.readFileSync(SETTINGS, "utf8"), SETTINGS)));
  console.log(`${SETTINGS} -> ${PINNED_PACK}`);
}
