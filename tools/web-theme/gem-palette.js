// The rich palette of the gem pieces: the colours the owner chose on 2026-10-07 (panel 4 of the colour study,
// out/colour/study2.png) in place of the ones the pieces were first drawn in. It is a rule, not a table: a redrawn
// piece gets its rich colours by building the sprite again (build-sprite.js), whatever colours it was drawn with.
//
// The rule works in OKLCH, where lightness, chroma and hue can be changed one at a time without the others
// following, which is not so in RGB or HSL. For every colour of a piece:
//   - a colour with hardly any chroma (a white, a grey, a black) stays as it is, so a light stays a light;
//   - the hue stays, except in the green and the cyan piece, whose hues are gathered around one hue each;
//   - the chroma grows and the lightness gives a little, less so in a dark colour, whose chroma is small already;
//   - what no screen can show gives up chroma until it can be shown; lightness and hue are kept.
"use strict";

/**
 * The pieces whose hues are led towards one hue, in degrees. The green piece's shades are drawn bluer than its lights
 * (up to 151) and come out yellower; the cyan piece's shades are drawn nearly blue (up to 240) and come out nearer to
 * its lights. The tray's own teal lies at 217: the cyan piece stands out from it by its lightness, not by its hue.
 */
const HUE_LED_TOWARDS = { green: 140, cyan: 220 };

/** How far such a hue goes: this share of the way to where it is led. */
const SHARE_OF_THE_WAY = 0.6;

/** Under this chroma a colour is a neutral and stays as drawn. Pure white and black lie far under it. */
const NEUTRAL_UNDER = 0.03;

/** Under this lightness a colour is a dark one: an outline, a side, a shade. */
const DARK_UNDER = 0.4;

/** What a colour's chroma is multiplied by, and what is added to its lightness. */
const LIGHT_COLOURS = { chroma: 1.3, lightness: -0.01 };
const DARK_COLOURS = { chroma: 1.15, lightness: -0.02 };

/**
 * A colour no screen shows loses chroma in steps of this size until it is one; the count ends the search for a
 * colour that never becomes one. The steps are those of the study the palette was chosen from, so that the sprite
 * holds the very colours of the chosen panel. A channel may lie this far outside 0..1 and still be rounded into it.
 */
const CHROMA_STEP = 0.97;
const MOST_STEPS = 60;
const ROUNDING_ROOM = 0.0005;

/** The attributes of the sprite's markup that hold a paint. */
const PAINT = /(\s(?:fill|stroke|stop-color|flood-color|lighting-color|color)=")([^"]*)(")/g;
const HEX_COLOUR = /^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$/;

const toLinear = (channel) => (channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4);
const toGamma = (channel) => (channel <= 0.0031308 ? 12.92 * channel : 1.055 * channel ** (1 / 2.4) - 0.055);

/** A "#rgb" or "#rrggbb" colour as `{ lightness, chroma, hue }` in OKLCH, the hue in degrees. */
function oklchOf(hex) {
  const long = hex.length === 4 ? `#${[...hex.slice(1)].map((digit) => digit + digit).join("")}` : hex;
  const [red, green, blue] = [1, 3, 5].map((at) => toLinear(parseInt(long.slice(at, at + 2), 16) / 255));
  const l = Math.cbrt(0.4122214708 * red + 0.5363325363 * green + 0.0514459929 * blue);
  const m = Math.cbrt(0.2119034982 * red + 0.6806995451 * green + 0.1073969566 * blue);
  const s = Math.cbrt(0.0883024619 * red + 0.2817188376 * green + 0.6299787005 * blue);
  const a = 1.9779984951 * l - 2.428592205 * m + 0.4505937099 * s;
  const b = 0.0259040371 * l + 0.7827717662 * m - 0.808675766 * s;
  return {
    lightness: 0.2104542553 * l + 0.793617785 * m - 0.0040720468 * s,
    chroma: Math.hypot(a, b),
    hue: (Math.atan2(b, a) * 180) / Math.PI,
  };
}

/** An OKLCH colour as linear red, green and blue; outside 0..1 where no screen shows the colour. */
function linearRgbOf({ lightness, chroma, hue }) {
  const a = chroma * Math.cos((hue * Math.PI) / 180);
  const b = chroma * Math.sin((hue * Math.PI) / 180);
  const l = (lightness + 0.3963377774 * a + 0.2158037573 * b) ** 3;
  const m = (lightness - 0.1055613458 * a - 0.0638541728 * b) ** 3;
  const s = (lightness - 0.0894841775 * a - 1.291485548 * b) ** 3;
  return [
    4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
    -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
    -0.0041960863 * l - 0.7034186147 * m + 1.707614701 * s,
  ];
}

const canBeShown = (colour) => linearRgbOf(colour).every((channel) => channel >= -ROUNDING_ROOM && channel <= 1 + ROUNDING_ROOM);

/** An OKLCH colour as "#RRGGBB": the colour itself, or the nearest one of its lightness and hue a screen shows. */
function hexOf(colour) {
  let shown = colour;
  for (let step = 0; step < MOST_STEPS && !canBeShown(shown); step++) shown = { ...shown, chroma: shown.chroma * CHROMA_STEP };
  const channels = linearRgbOf(shown).map((channel) => Math.round(255 * toGamma(Math.min(1, Math.max(0, channel)))));
  return `#${channels.map((channel) => channel.toString(16).padStart(2, "0")).join("").toUpperCase()}`;
}

/** The hue `share` of the way from `hue` to `target`, the short way round the circle. */
function ledTowards(hue, target, share) {
  const shortWay = ((((target - hue) % 360) + 540) % 360) - 180;
  return hue + shortWay * share;
}

/** A colour of the piece of gem colour `gem`, as the rich palette has it. A neutral comes back as it was written. */
function richColour(hex, gem) {
  const drawn = oklchOf(hex);
  if (drawn.chroma < NEUTRAL_UNDER) return hex;
  const target = HUE_LED_TOWARDS[gem];
  const change = drawn.lightness < DARK_UNDER ? DARK_COLOURS : LIGHT_COLOURS;
  return hexOf({
    lightness: drawn.lightness + change.lightness,
    chroma: drawn.chroma * change.chroma,
    hue: target === undefined ? drawn.hue : ledTowards(drawn.hue, target, SHARE_OF_THE_WAY),
  });
}

/** The paints of `markup` the palette cannot read: neither a hex colour, nor none, nor a pointer to a gradient. */
function paintsNotRead(markup) {
  const isRead = (paint) => HEX_COLOUR.test(paint) || paint === "none" || paint.startsWith("url(#");
  return [...new Set([...markup.matchAll(PAINT)].map((match) => match[2]).filter((paint) => !isRead(paint)))];
}

/** `markup` of the piece of gem colour `gem` with every colour it is painted with in the rich palette. */
function inRichPalette(markup, gem) {
  return markup.replace(PAINT, (whole, before, paint, after) => (HEX_COLOUR.test(paint) ? before + richColour(paint, gem) + after : whole));
}

module.exports = { HUE_LED_TOWARDS, NEUTRAL_UNDER, DARK_UNDER, oklchOf, hexOf, richColour, paintsNotRead, inRichPalette };
