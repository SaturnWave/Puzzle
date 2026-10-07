// Checks the rich palette of the gem pieces (tools/web-theme/gem-palette.js): the rule as it is written down, and
// the colours of the panel the owner chose (panel 4 of out/colour/study2.png), which the study's own code gave.
//
// usage (from the repository root, Node 22 or later):  node --test tools/web-theme/gem-palette.test.mjs

import assert from "node:assert/strict";
import { test } from "node:test";
import palette from "./gem-palette.js";

const { DARK_UNDER, HUE_LED_TOWARDS, NEUTRAL_UNDER, inRichPalette, oklchOf, paintsNotRead, richColour } = palette;

/** A colour is written with eight bits a channel, so what is read back from it is this near to what was meant. */
const ROUNDING = { lightness: 0.004, chroma: 0.004, hue: 1.5 };

const hueApart = (one, other) => Math.abs(((((one - other) % 360) + 540) % 360) - 180);

function assertNear(actual, expected, room, what) {
  assert.ok(Math.abs(actual - expected) <= room, `${what}: ${actual} is not within ${room} of ${expected}`);
}

test("the colours of the chosen panel", () => {
  const chosen = [
    ["#F4443F", "red", "#FD2129"], ["#5F0A1B", "red", "#590116"],
    ["#46C24F", "green", "#27C211"], ["#0B4B23", "green", "#06460E"],
    ["#2F7FE8", "blue", "#0079F7"],
    ["#FFCB21", "yellow", "#FCC705"], ["#C6D0F8", "yellow", "#C0CCFE"],
    ["#9B5DE0", "purple", "#A043F7"],
    ["#F28C30", "orange", "#F4850E"],
    ["#0FAFDF", "cyan", "#08ADD5"], ["#054263", "cyan", "#023E53"],
    ["#F072AC", "pink", "#FD5BAB"],
  ];
  for (const [drawn, gem, rich] of chosen) assert.equal(richColour(drawn, gem), rich, `${drawn} of the ${gem} piece`);
});

test("a white, a grey and a black stay as they are written", () => {
  for (const neutral of ["#fff", "#FFFFFF", "#000", "#808080", "#7A7F80"]) {
    assert.equal(richColour(neutral, "yellow"), neutral);
  }
});

test("the daisy's white petals and the ladybug's eyes are neutrals", () => {
  for (const [light, gem] of [["#E4EAFF", "yellow"], ["#DDD6EA", "yellow"], ["#FFF4E0", "red"]]) {
    assert.ok(oklchOf(light).chroma < NEUTRAL_UNDER);
    assert.equal(richColour(light, gem), light);
  }
});

test("a light colour that a screen can show gets 1.3 times its chroma and gives 0.01 of its lightness", () => {
  const drawn = oklchOf("#9B5DE0");
  const rich = oklchOf(richColour("#9B5DE0", "purple"));
  assert.ok(drawn.lightness >= DARK_UNDER);
  assertNear(rich.chroma, 1.3 * drawn.chroma, ROUNDING.chroma, "chroma");
  assertNear(rich.lightness, drawn.lightness - 0.01, ROUNDING.lightness, "lightness");
  assertNear(hueApart(rich.hue, drawn.hue), 0, ROUNDING.hue, "hue");
});

test("a dark colour gets 1.15 times its chroma and gives 0.02 of its lightness", () => {
  const drawn = oklchOf("#2A1D5E"); // the ink of the ladybug's dots
  const rich = oklchOf(richColour("#2A1D5E", "red"));
  assert.ok(drawn.lightness < DARK_UNDER);
  assertNear(rich.chroma, 1.15 * drawn.chroma, ROUNDING.chroma, "chroma");
  assertNear(rich.lightness, drawn.lightness - 0.02, ROUNDING.lightness, "lightness");
  assertNear(hueApart(rich.hue, drawn.hue), 0, ROUNDING.hue, "hue");
});

test("a colour no screen shows gives up chroma, and neither lightness nor hue", () => {
  const drawn = oklchOf("#F4443F");
  const rich = oklchOf(richColour("#F4443F", "red"));
  assert.ok(rich.chroma > drawn.chroma && rich.chroma < 1.3 * drawn.chroma - ROUNDING.chroma);
  assertNear(rich.lightness, drawn.lightness - 0.01, ROUNDING.lightness, "lightness");
  assertNear(hueApart(rich.hue, drawn.hue), 0, ROUNDING.hue, "hue");
});

test("the green and the cyan piece are led 60 % of the way to their hue; the same colour in another piece keeps its own", () => {
  for (const [drawnHex, gem] of [["#147033", "green"], ["#B4F28A", "green"], ["#0679B2", "cyan"], ["#93EDFF", "cyan"]]) {
    const drawn = oklchOf(drawnHex);
    const led = oklchOf(richColour(drawnHex, gem));
    const before = hueApart(drawn.hue, HUE_LED_TOWARDS[gem]);
    assertNear(hueApart(led.hue, HUE_LED_TOWARDS[gem]), 0.4 * before, ROUNDING.hue, `${drawnHex} of the ${gem} piece`);
    const kept = oklchOf(richColour(drawnHex, "pink"));
    assertNear(hueApart(kept.hue, drawn.hue), 0, ROUNDING.hue, `${drawnHex} of another piece`);
  }
});

test("only paints are repainted: ids, pointers and numbers that look like colours stay", () => {
  const drawn = '<linearGradient id="fade"><stop offset=".5" stop-color="#F4443F"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></linearGradient>'
    + '<path d="M0 0" fill="url(#fade)" stroke="#5F0A1B" stroke-width="2"/><use href="#bead"/><path fill="none" stroke="#abc"/>';
  const rich = inRichPalette(drawn, "red");
  assert.equal(rich, drawn.replace("#F4443F", "#FD2129").replace("#5F0A1B", "#590116").replace('"#abc"', `"${richColour("#abc", "red")}"`));
  assert.match(richColour("#abc", "red"), /^#[0-9A-F]{6}$/);
});

test("a paint that is no hex colour is named, since it would keep its old colour", () => {
  const drawn = '<path fill="rgb(244, 68, 63)" stroke="none"/><path fill="url(#f-red)" stroke="crimson"/><stop stop-color="#F4443F"/>';
  assert.deepEqual(paintsNotRead(drawn), ["rgb(244, 68, 63)", "crimson"]);
});
