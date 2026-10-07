// The board of the player page: the lab's board (game-view.js) in the game's own frame. The stepped gilt frame with
// its ring of beads, the woven tray, the varnish slabs under the tiles and the exits under the columns are drawn
// here from the level's own mask; the cells, the tiles and all that moves stay the shared board's.

import { svgElement } from "./dom.js";
import { createBoard, symbolArt } from "./game-view.js";
import { lengthOf, movedOut, outlineLoops, pathOf } from "./board-outline.js";

// The chosen design's frame, in px whatever the size of the cells. One path, `FRAME_INSET` inside the cells'
// outline, is stroked many times over; the strokes reach `FRAME_REACH` past the cells, and the tray fills the rest.
const FRAME_INSET = 6;
const FRAME_REACH = 10.6;
const FRAME_ROOM = 22; // around the board for the frame and its shadow: the drawing's margin

/** The gilt rails along the frame path, the outermost first: a paint, a width, and a nudge for the two edge lights. */
const RAILS = [
  { stroke: "#5E3305", width: 33.2 },
  { stroke: "#B87412", width: 30.8 },
  { stroke: "#FFF8D6", width: 28.8, nudge: "translate(-.8 -1)" },
  { stroke: "#9C5C0B", width: 28.8, nudge: "translate(.8 1)" },
  { stroke: "url(#board-gilt)", width: 28.8 },
  { stroke: "url(#board-sheen)", width: 28.8, opacity: 0.55 },
  { stroke: "#9C5C0B", width: 19 },
  { stroke: "url(#board-gilt-light)", width: 17.4 },
  { stroke: "#6B3E08", width: 14 },
];

/**
 * The frame's shadow on the wall: the design blurs one dark stroke; here faint strokes of growing width are laid
 * over each other instead, which costs no SVG filter while tiles move above it. They are many and faint enough
 * (each a twentieth as dark as black) that no single one shows as a band under the frame.
 */
const SHADOW = { colour: "#031E24", drop: 9, narrowest: 22, widest: 46, strokes: 10, opacity: 0.05 };

/**
 * The tray is the frame path filled, with a stroke that carries the fill out to the cells' edge. Its ground is the
 * dark teal the owner chose for the rich pieces to stand on (2026-10-07); the stylesheet makes every second cell a
 * shade darker still. The weave's threads and the shade the frame throws on the tray are faint on that ground, and
 * darker than it: the teal they had on the light tray would not show.
 */
const TRAY_STROKE = 2 * FRAME_INSET;
const TRAY_GROUND = "#124652";
const TRAY_SHADE = { colour: "#03161C", opacity: 0.42, width: 21, drop: 2.5 };
const WEAVE = { light: { colour: "#fff", opacity: 0.04 }, dark: { colour: "#03161C", opacity: 0.13 } };
const BEADS = { ring: 5.6, pitch: 5.41, dot: 0.01, shade: "#9C5C0B", light: "#FFF6CC" };

/** Tiles are drawn a little larger than their cell, so the edge they are cut off at lies this far outside the cells. */
const TILE_OVERHANG = 2.5;

/** An exit hangs under its column, clear of the frame; drawn narrower than a cell, it also clears the frame's next step. */
const EXIT = { gap: FRAME_REACH + 1.4, size: 0.72 };

const SLAB_VARIANTS = 3;
const CORNERS = [
  { name: "open-tl", dx: -1, dy: -1 },
  { name: "open-tr", dx: 1, dy: -1 },
  { name: "open-br", dx: 1, dy: 1 },
  { name: "open-bl", dx: -1, dy: 1 },
];

/**
 * Where the wall's two sconces hang: in the board's top corners when both are cut away that many cells wide, else
 * above the frame; either way only when that much bare wall, in px, lies above the board's cells. A lamp above the
 * frame reaches 80px above the cells; the rest of its room keeps its bowl clear of the shadow under the HUD.
 */
const SCONCE_CORNER_CELLS = 2;
const SCONCE_ROOM = { corners: 62, above: 86 };

const stop = (offset, colour, opacity = 1) => svgElement("stop", { offset, "stop-color": colour, "stop-opacity": opacity });

function gradient(id, { x1 = 0, y1, x2 = 0, y2 }, stops) {
  const element = svgElement("linearGradient", { id, gradientUnits: "userSpaceOnUse", x1, y1, x2, y2 });
  element.append(...stops);
  return element;
}

function weave() {
  const pattern = svgElement("pattern", { id: "board-weave", width: 5, height: 5, patternUnits: "userSpaceOnUse" });
  pattern.append(
    svgElement("path", { d: "M0 1.2H5", stroke: WEAVE.light.colour, "stroke-opacity": WEAVE.light.opacity, "stroke-width": 1 }),
    svgElement("path", { d: "M1.2 0V5", stroke: WEAVE.dark.colour, "stroke-opacity": WEAVE.dark.opacity, "stroke-width": 1 }),
  );
  return pattern;
}

function clipOf(id, d) {
  const clip = svgElement("clipPath", { id, clipPathUnits: "userSpaceOnUse" });
  clip.appendChild(svgElement("path", { d }));
  return clip;
}

/** The paints that are sized to the board, and the two clips: the tray's own edge, and the edge tiles are cut off at. */
function definitions({ width, height }, paths) {
  const defs = svgElement("defs", {});
  defs.append(
    gradient("board-gilt", { y1: -10, y2: height + 10 }, [stop(0, "#FFE892"), stop(0.45, "#F6B93C"), stop(1, "#DE9A22")]),
    gradient("board-gilt-light", { y1: -10, y2: height + 10 }, [stop(0, "#FFF7D2"), stop(0.5, "#FFDF7C"), stop(1, "#F3BC48")]),
    gradient("board-sheen", { y1: 0, x2: width, y2: height }, [
      stop(0.1, "#fff", 0), stop(0.2, "#fff", 0.65), stop(0.3, "#fff", 0),
      stop(0.62, "#fff", 0), stop(0.7, "#fff", 0.4), stop(0.78, "#fff", 0),
    ]),
    weave(),
    clipOf("board-tray-edge", paths.outline),
    clipOf("board-tile-edge", paths.tileEdge),
  );
  return defs;
}

const stroked = (d, attributes) => svgElement("path", { d, fill: "none", "stroke-linejoin": "round", ...attributes });

function shadow(d) {
  const step = (SHADOW.widest - SHADOW.narrowest) / (SHADOW.strokes - 1);
  return Array.from({ length: SHADOW.strokes }, (_, stroke) => stroked(d, {
    stroke: SHADOW.colour,
    "stroke-opacity": SHADOW.opacity,
    "stroke-width": (SHADOW.narrowest + stroke * step).toFixed(2),
    transform: `translate(0 ${SHADOW.drop})`,
  }));
}

function rails(d) {
  return RAILS.map(({ stroke, width, nudge, opacity }) => {
    const rail = stroked(d, { stroke, "stroke-width": width });
    if (nudge !== undefined) rail.setAttribute("transform", nudge);
    if (opacity !== undefined) rail.setAttribute("stroke-opacity", opacity);
    return rail;
  });
}

function tray(paths) {
  const shade = svgElement("g", { "clip-path": "url(#board-tray-edge)" });
  shade.appendChild(stroked(paths.frame, {
    stroke: TRAY_SHADE.colour, "stroke-opacity": TRAY_SHADE.opacity, "stroke-width": TRAY_SHADE.width, transform: `translate(0 ${TRAY_SHADE.drop})`,
  }));
  return [
    svgElement("path", { d: paths.frame, fill: TRAY_GROUND, stroke: TRAY_GROUND, "stroke-width": TRAY_STROKE, "stroke-linejoin": "round" }),
    svgElement("path", { d: paths.outline, fill: "url(#board-weave)" }),
    shade,
  ];
}

/**
 * The beads of one ring: the dots of a dashed line, spaced so that a whole number of them closes the ring, with a
 * darker dot under each as its shade.
 */
function beadRing(points) {
  const length = lengthOf(points);
  const pitch = length / Math.max(1, Math.round(length / BEADS.pitch));
  const dots = { "stroke-linecap": "round", "stroke-dasharray": `${BEADS.dot} ${(pitch - BEADS.dot).toFixed(4)}` };
  const d = pathOf([points]);
  return [
    stroked(d, { ...dots, stroke: BEADS.shade, "stroke-opacity": 0.8, "stroke-width": 2.5, transform: "translate(.5 .9)" }),
    stroked(d, { ...dots, stroke: BEADS.light, "stroke-width": 2.3 }),
  ];
}

/** The bottom playable cell of every column that has one: where a drop item leaves the board. */
function exitCells({ width, height, playable }) {
  const exits = [];
  for (let x = 0; x < width; x++) {
    for (let y = height - 1; y >= 0; y--) {
      if (!playable[y * width + x]) continue;
      exits.push({ x, y });
      break;
    }
  }
  return exits;
}

function exitMarks(state, cellSize) {
  const size = EXIT.size * cellSize;
  return exitCells(state).map(({ x, y }) => svgElement("use", {
    href: "#exit", x: (x + 0.5) * cellSize - size / 2, y: (y + 1) * cellSize + EXIT.gap, width: size, height: size,
  }));
}

const hasDropGoal = (state) => state.goals.some((goal) => goal.type === "dropItems");

/**
 * What the frame's <svg> holds for a board at a cell size: its `box` in px, with the cells' top left corner at the
 * origin, and its `parts` from the shadow on the wall up to the beads.
 */
function frameDrawing(state, cellSize) {
  const loops = outlineLoops(state);
  const at = (distance) => loops.map((corners) => movedOut(corners, cellSize, distance));
  const paths = { outline: pathOf(at(0)), frame: pathOf(at(-FRAME_INSET)), tileEdge: pathOf(at(TILE_OVERHANG)) };
  const cells = { width: state.width * cellSize, height: state.height * cellSize };
  return {
    box: { x: -FRAME_ROOM, y: -FRAME_ROOM, width: cells.width + 2 * FRAME_ROOM, height: cells.height + 2 * FRAME_ROOM },
    parts: [
      definitions(cells, paths),
      ...shadow(paths.frame),
      ...rails(paths.frame),
      ...tray(paths),
      ...at(BEADS.ring).flatMap(beadRing),
      ...(hasDropGoal(state) ? exitMarks(state, cellSize) : []),
    ],
  };
}

/**
 * The board of game-view.js in the game's frame. It is that board, with all it can do, except that `render` and
 * `fit` also draw what lies around the cells. `board` is the element the cells go into, `frame` an <svg> lying
 * under it, and `wrap` the box around both, which is told what hangs on the wall beside this board. `roomAbove()`
 * gives the bare wall above the board's cells in px; the other options are createBoard's own.
 */
export function createFramedBoard({ board, frame, wrap }, { roomAbove, ...boardOptions }) {
  const view = createBoard(board, boardOptions);
  let state;          // the state last rendered: its mask and goals; absent until the first board
  let drawnCellSize;  // the cell size the frame was last drawn at

  const cellSize = () => parseFloat(board.style.getPropertyValue("--cell-size"));
  const isCell = (x, y) => x >= 0 && y >= 0 && x < state.width && y < state.height && state.playable[y * state.width + x];

  function drawFrame() {
    drawnCellSize = cellSize();
    const { box, parts } = frameDrawing(state, drawnCellSize);
    frame.setAttribute("viewBox", `${box.x} ${box.y} ${box.width} ${box.height}`);
    frame.setAttribute("width", String(box.width));
    frame.setAttribute("height", String(box.height));
    frame.replaceChildren(...parts);
    // What falls in from above a column is cut off at the tray's edge. The clip is named from the element's own
    // style, so that the address is read as the page's and not the stylesheet's.
    board.style.setProperty("clip-path", "url(#board-tile-edge)");
  }

  /** A corner of a cell is open when the board has no cell beside it either way: the tray is rounded there. */
  function markOpenCorners(cell, x, y) {
    for (const corner of CORNERS) {
      cell.classList.toggle(corner.name, !isCell(x + corner.dx, y) && !isCell(x, y + corner.dy));
    }
  }

  /** The slab of each varnish layer the cell has, the thick one over the thin; the board's own classes show the right one. */
  function laySlabs(cell, x, y) {
    const layers = state.jelly[y * state.width + x];
    const variant = (((x - y) % SLAB_VARIANTS) + SLAB_VARIANTS) % SLAB_VARIANTS;
    for (let layer = 1; layer <= layers; layer++) {
      cell.appendChild(symbolArt(`slab-${layer}-${variant}`, `slab slab-${layer}`));
    }
  }

  function dressCells() {
    for (const cell of board.querySelectorAll(":scope > .cell")) {
      const x = Number(cell.style.getPropertyValue("--x"));
      const y = Number(cell.style.getPropertyValue("--y"));
      markOpenCorners(cell, x, y);
      laySlabs(cell, x, y);
    }
  }

  /** Where the sconces fit beside this board: "corners", "above" or, on a wall with too little room, "none". */
  function sconcePlace() {
    const cornerIsCut = (fromX) => Array.from({ length: SCONCE_CORNER_CELLS }, (_, step) => fromX + step).every((x) => !isCell(x, 0));
    const place = cornerIsCut(0) && cornerIsCut(state.width - SCONCE_CORNER_CELLS) ? "corners" : "above";
    return roomAbove() >= SCONCE_ROOM[place] ? place : "none";
  }

  /** Draws `next` from nothing, in its frame: a new game, or the one the page came back to. */
  function render(next) {
    state = next;
    wrap.classList.toggle("has-exits", hasDropGoal(state)); // before the board measures its room: exits hang under the frame
    view.render(state);
    drawFrame();
    dressCells();
    wrap.dataset.sconces = sconcePlace();
  }

  /** Sizes the cells to the room the page gives the board; the frame keeps its px while the cells change theirs. */
  function fit() {
    view.fit();
    if (state === undefined) return;
    if (cellSize() !== drawnCellSize) drawFrame();
    wrap.dataset.sconces = sconcePlace();
  }

  return { ...view, render, fit };
}
