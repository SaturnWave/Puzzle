// The lit alcove of a card: a piece of the gallery wall in a beaded gilt frame, with a wooden shelf that the card's
// exhibits stand on. The wall, the shelf and the gilt band with its two edge lines are the stylesheet's; the row of
// beads is drawn here, because only a stroked line carries round beads around the frame's corners.

import { make, svgElement } from "./dom.js";
import { symbolArt } from "./game-view.js";

/**
 * The frame's beads as the design draws them: the dots of a dashed line along the middle of the gilt band (a box
 * with rounded corners), each over a darker dot as its shade. `pitch` is the design's distance between two beads;
 * the row keeps as close to it as a whole number of beads around the frame allows.
 */
const BEADS = {
  pitch: 4.69,
  dot: 0.01,
  cornerRadius: 14.5,
  shade: { stroke: "#9C5C0B", "stroke-width": 3, transform: "translate(.5 .8)" },
  light: { stroke: "#FFF6CC", "stroke-width": 2.6 },
};

/** The sparkles around a star; the stylesheet places each one. */
const SPARKLES = 5;

/** The length of the line the beads lie on, for a box of this size: its straight sides and its four quarter turns. */
function beadLineLength({ width, height }) {
  const straight = 2 * (width + height) - 8 * BEADS.cornerRadius;
  return straight + 2 * Math.PI * BEADS.cornerRadius;
}

/** One line of dots around the frame; it has no dots until `spaceBeads` has measured the frame. */
function beadRow(look) {
  return svgElement("rect", { width: "100%", height: "100%", rx: BEADS.cornerRadius, fill: "none", "stroke-linecap": "round", ...look });
}

/** Spaces the dots of `rows` so that a whole number of beads closes the row around a frame of `size`. */
function spaceBeads(rows, size) {
  const length = beadLineLength(size);
  const pitch = length / Math.max(1, Math.round(length / BEADS.pitch));
  for (const row of rows) row.setAttribute("stroke-dasharray", `${BEADS.dot} ${(pitch - BEADS.dot).toFixed(4)}`);
}

/**
 * The frame's row of beads, as an <svg> over the middle line of the band. An alcove is as wide as its card and, on
 * a result panel, as tall as the board behind it leaves it, so the beads are spaced again whenever its size
 * changes. The watch ends when the alcove has left the page.
 */
function beads() {
  const frame = svgElement("svg", { class: "alcove-beads", "aria-hidden": "true" });
  const rows = [beadRow(BEADS.shade), beadRow(BEADS.light)];
  const sizes = new ResizeObserver(([latest]) => {
    if (!frame.isConnected) {
      sizes.disconnect();
      return;
    }
    const { width, height } = latest.contentRect;
    const isShown = width > 0 && height > 0; // a card that is put away has no size, and needs no beads
    if (isShown) spaceBeads(rows, { width, height });
  });
  frame.append(...rows);
  sizes.observe(frame);
  return frame;
}

/** An alcove lit and laid out as the stylesheet does for `kind` ("won" or "lost"), with its exhibits. */
function alcove(kind, exhibits) {
  const niche = make("div", `alcove alcove-${kind}`);
  niche.append(beads(), ...exhibits);
  return niche;
}

/** The star of a won level on the shelf, among its sparkles. It adorns what the card says; a screen reader skips it. */
export function starAlcove() {
  const sparkles = Array.from({ length: SPARKLES }, () => make("span", "sparkle"));
  const niche = alcove("won", [...sparkles, symbolArt("mark-star", "alcove-star")]);
  niche.setAttribute("aria-hidden", "true");
  return niche;
}

/** What a lost game left, standing side by side on the shelf; a screen reader hears the exhibits as a group named `label`. */
export function leftoverAlcove(label, exhibits) {
  const niche = alcove("lost", exhibits);
  niche.setAttribute("role", "group");
  niche.setAttribute("aria-label", label);
  return niche;
}
