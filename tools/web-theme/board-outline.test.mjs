// Checks the player page's board outline (src/Puzzle.Web/wwwroot/player/board-outline.js) against the frame of the
// chosen design: drawn around the design's own board, the outline must come out as the three paths the design's
// frame is stroked along, character for character. It also pins what the design has no example of: a board with a
// hole, and cells that touch only at a corner.
//
// usage (from the repository root, Node 22.7 or later):  node --test tools/web-theme/board-outline.test.mjs

import assert from "node:assert/strict";
import { test } from "node:test";
import { lengthOf, movedOut, outlineLoops, pathOf } from "../../src/Puzzle.Web/wwwroot/player/board-outline.js";

/** A board from rows of "#" (a cell) and "." (none), as the state gives it. */
function boardOf(rows) {
  return { width: rows[0].length, height: rows.length, playable: rows.flatMap((row) => [...row].map((cell) => cell === "#")) };
}

const pathAt = (board, cellSize, distance) => pathOf(outlineLoops(board).map((corners) => movedOut(corners, cellSize, distance)));

// The chosen board (out/design/chosen/Board.dc.html): 9 by 11 cells of 40 px, and the paths of its frame.
const CHOSEN_BOARD = boardOf([
  "...###...",
  "..#####..",
  ".#######.",
  "#########",
  "#########",
  "#########",
  "#########",
  "#########",
  ".#######.",
  "..#####..",
  "...###...",
]);
const CHOSEN_CELL_PX = 40;
const CHOSEN_OUTLINE = "M120 0L240 0L240 40L280 40L280 80L320 80L320 120L360 120L360 320L320 320L320 360L280 360L280 400L240 400L240 440L120 440L120 400L80 400L80 360L40 360L40 320L0 320L0 120L40 120L40 80L80 80L80 40L120 40Z";
const CHOSEN_FRAME = "M126 6L234 6L234 46L274 46L274 86L314 86L314 126L354 126L354 314L314 314L314 354L274 354L274 394L234 394L234 434L126 434L126 394L86 394L86 354L46 354L46 314L6 314L6 126L46 126L46 86L86 86L86 46L126 46Z";
const CHOSEN_BEADS = "M114.4 -5.6L245.6 -5.6L245.6 34.4L285.6 34.4L285.6 74.4L325.6 74.4L325.6 114.4L365.6 114.4L365.6 325.6L325.6 325.6L325.6 365.6L285.6 365.6L285.6 405.6L245.6 405.6L245.6 445.6L114.4 445.6L114.4 405.6L74.4 405.6L74.4 365.6L34.4 365.6L34.4 325.6L-5.6 325.6L-5.6 114.4L34.4 114.4L34.4 74.4L74.4 74.4L74.4 34.4L114.4 34.4Z";

test("outline_chosenBoard_isTheChosenFramesThreePaths", () => {
  assert.equal(pathAt(CHOSEN_BOARD, CHOSEN_CELL_PX, 0), CHOSEN_OUTLINE);
  assert.equal(pathAt(CHOSEN_BOARD, CHOSEN_CELL_PX, -6), CHOSEN_FRAME);
  assert.equal(pathAt(CHOSEN_BOARD, CHOSEN_CELL_PX, 5.6), CHOSEN_BEADS);
});

test("outline_plainRectangle_isOneLoopOfFourCorners", () => {
  const loops = outlineLoops(boardOf(["###", "###"]));

  assert.deepEqual(loops, [[{ x: 0, y: 0 }, { x: 3, y: 0 }, { x: 3, y: 2 }, { x: 0, y: 2 }]]);
  assert.equal(lengthOf(movedOut(loops[0], 10, 0)), 100);
});

test("outline_boardWithAHole_ringsTheHoleAndMovesItsEdgeIntoTheHole", () => {
  const board = boardOf(["###", "#.#", "###"]);

  const [around, hole] = outlineLoops(board);

  assert.equal(pathOf([movedOut(around, 10, 2)]), "M-2 -2L32 -2L32 32L-2 32Z");
  assert.equal(pathOf([movedOut(hole, 10, 2)]), "M12 18L18 18L18 12L12 12Z"); // out of the cells is into the hole
});

test("outline_cellsTouchingAtACorner_getALoopEach", () => {
  const loops = outlineLoops(boardOf(["#.", ".#"]));

  assert.equal(pathOf(loops.map((corners) => movedOut(corners, 10, 0))), "M0 0L10 0L10 10L0 10ZM10 10L20 10L20 20L10 20Z");
});

test("outline_noCells_hasNoLoop", () => {
  assert.deepEqual(outlineLoops(boardOf(["..", ".."])), []);
});
