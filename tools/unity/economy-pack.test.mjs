// Checks the pack of the economy settings that the Unity client plays by (tools/unity/economy-pack.js): the
// settings file is read with its comments and trailing commas, as the simulator's loader reads it; the shipped
// file packs to exactly the pinned pack the C# tests read (tests/fixtures/economy-pack.json); and a file that
// leaves out something the game reads stops with the field's name, because Unity's reader would take it for 0.
//
// usage (from the repository root):  node --test tools/unity/economy-pack.test.mjs

import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import pack from "./economy-pack.js";

const { PINNED_PACK, SETTINGS, packedEconomy, parsedSettings } = pack;

const shippedText = () => readFileSync(SETTINGS, "utf8");

/** The shipped settings as a plain document with `change` applied: a whole file a test has taken something out of. */
function shippedWith(change) {
  const settings = parsedSettings(shippedText(), SETTINGS);
  change(settings);
  return JSON.stringify(settings);
}

test("settings_commentsAndTrailingCommas_areReadAsTheSimulatorReadsThem", () => {
  const text = `{
    // a line comment with "quotes" and an apostrophe's mark
    "a": 1, /* a block comment, with a comma, */
    "list": [1, 2, ],
    "inner": { "b": 2, },
  }`;

  assert.deepEqual(parsedSettings(text, "test.json"), { a: 1, list: [1, 2], inner: { b: 2 } });
});

test("settings_whatStandsInsideAString_isNoCommentAndNoTrailingComma", () => {
  const text = String.raw`{
    "address": "https://example.org/a//b", // the two slashes inside the string stay
    "block": "not /* a comment */ at all",
    "quoted": "she said \"// no comment\", and left",
    "comma": "a list ends with ,]",
    "backslash": "ends with a backslash \\"
  }`;

  assert.deepEqual(parsedSettings(text, "test.json"), {
    address: "https://example.org/a//b",
    block: "not /* a comment */ at all",
    quoted: 'she said "// no comment", and left',
    comma: "a list ends with ,]",
    backslash: "ends with a backslash \\",
  });
});

test("settings_thatAreNotJson_areRefusedByTheFilesName", () => {
  assert.throws(() => parsedSettings(`{ "a": "never closed }`, "broken.json"), /broken\.json is not a settings file: a string is never closed/);
  assert.throws(() => parsedSettings(`{ "a": 1 /* never closed }`, "broken.json"), /broken\.json is not a settings file: a comment/);
  assert.throws(() => parsedSettings(`{ "a": 1,, }`, "broken.json"), /broken\.json is not a settings file/);
  assert.throws(() => parsedSettings(`[1, 2]`, "list.json"), /list\.json is not a settings file: a JSON object is expected/);
});

test("pack_ofTheShippedSettings_isThePinnedPack", () => {
  const pinned = JSON.parse(readFileSync(PINNED_PACK, "utf8"));

  // When this fails after a change of the settings: node tools/unity/economy-pack.js --pin
  assert.deepEqual(packedEconomy(shippedText(), SETTINGS), pinned);
});

test("pack_keepsTheGamesSectionsOnly_andListsWhatTheSettingsKeyByName", () => {
  const packed = packedEconomy(shippedText(), SETTINGS);
  const settings = parsedSettings(shippedText(), SETTINGS);

  assert.deepEqual(Object.keys(packed), ["formatVersion", "lives", "coins", "rewards", "extraMovesOffer", "prices", "start", "boosterUnlocks", "startBoosters", "shop", "build"]);
  for (const simulatorsOwn of ["name", "players", "habit", "player", "measurement", "report", "observed"]) {
    assert.ok(Object.hasOwn(settings, simulatorsOwn), `the settings have a section ${simulatorsOwn}`);
    assert.ok(!Object.hasOwn(packed, simulatorsOwn), `the pack has no section ${simulatorsOwn}`);
  }

  // A named object becomes a list in the file's order; nothing is added, left out or renamed on the way.
  assert.deepEqual(packed.prices.boosters.map((price) => price.booster), Object.keys(settings.prices.boosters));
  assert.deepEqual(packed.prices.boosters.map((price) => price.coins), Object.values(settings.prices.boosters));
  assert.deepEqual(packed.start.boosters.map((held) => [held.booster, held.count]), Object.entries(settings.start.boosters));
  assert.deepEqual(packed.boosterUnlocks.levels.map((opens) => [opens.booster, opens.level]), Object.entries(settings.boosterUnlocks.levels));
  assert.deepEqual(packed.boosterUnlocks.gift.map((given) => [given.booster, given.count]), Object.entries(settings.boosterUnlocks.gift));
  assert.deepEqual(packed.shop.coinPacks.map((coinPack) => coinPack.name), Object.keys(settings.shop.coinPacks));
  assert.deepEqual(packed.shop.boosterSets.map((set) => set.name), Object.keys(settings.shop.boosterSets));
  const [firstSet] = packed.shop.boosterSets;
  assert.deepEqual(firstSet.boosters.map((held) => [held.booster, held.count]), Object.entries(settings.shop.boosterSets[firstSet.name].boosters));
  assert.equal(firstSet.priceCoins, settings.shop.boosterSets[firstSet.name].priceCoins);
});

test("pack_ofSettingsWithAMissingPrice_stopsByTheFieldsName", () => {
  const withoutThePrice = shippedWith((settings) => delete settings.extraMovesOffer.priceCoins);
  const withoutASetsPrice = shippedWith((settings) => delete settings.shop.boosterSets.starter.priceCoins);
  const withoutTheShop = shippedWith((settings) => delete settings.shop);
  const withoutTheLevels = shippedWith((settings) => delete settings.boosterUnlocks.levels);

  assert.throws(() => packedEconomy(withoutThePrice, "changed.json"), /changed\.json: 'extraMovesOffer\.priceCoins' is missing/);
  assert.throws(() => packedEconomy(withoutASetsPrice, "changed.json"), /changed\.json: 'shop\.boosterSets\.starter\.priceCoins' is missing/);
  assert.throws(() => packedEconomy(withoutTheShop, "changed.json"), /changed\.json: 'shop' is missing/);
  assert.throws(() => packedEconomy(withoutTheLevels, "changed.json"), /changed\.json: 'boosterUnlocks\.levels' is missing/);
});

test("pack_ofSettingsWithAMisshapenValue_stopsByTheFieldsName", () => {
  const withAFraction = shippedWith((settings) => (settings.coins.perMoveLeftInFinale = 4.5));
  const withAWordForANumber = shippedWith((settings) => (settings.prices.boosters.shuffle = "300"));
  const withANumberForAWord = shippedWith((settings) => (settings.build.ads = 1));
  const withAListForAnObject = shippedWith((settings) => (settings.lives = [5, 30]));

  assert.throws(() => packedEconomy(withAFraction, "changed.json"), /'coins\.perMoveLeftInFinale' must be a whole number, not 4\.5/);
  assert.throws(() => packedEconomy(withAWordForANumber, "changed.json"), /'prices\.boosters\.shuffle' must be a whole number, not "300"/);
  assert.throws(() => packedEconomy(withANumberForAWord, "changed.json"), /'build\.ads' must be a word in quotes, not 1/);
  assert.throws(() => packedEconomy(withAListForAnObject, "changed.json"), /'lives' must be an object/);
});

test("pack_ofAVariantFile_isRefused_becauseAVariantStatesOnlyWhatItChanges", () => {
  const variant = readFileSync("economy/one-life.json", "utf8");

  assert.throws(() => packedEconomy(variant, "economy/one-life.json"), /economy\/one-life\.json: '.+' is missing/);
});
