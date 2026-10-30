// The outline of a board, worked out from the level's mask of playable cells. The gilt frame, its ring of beads, the
// tray and the edge the falling tiles are cut off at are all this one outline, moved out or in by a few pixels.

/**
 * The loops of corners around the playable cells, in cell units, as `[{ x, y }, ...]` each. A loop runs clockwise
 * around a piece of the board and the other way around a hole in it, so "outwards" (see `movedOut`) always points
 * away from the cells. `width`, `height` and `playable` are the state's own (row-major mask).
 */
export function outlineLoops({ width, height, playable }) {
  const isCell = (x, y) => x >= 0 && y >= 0 && x < width && y < height && playable[y * width + x];
  const edgesFrom = new Map(); // "x,y" of a corner -> the edges that start there
  const topEdges = [];         // every loop has one: the walk of a loop starts on the first it meets

  function addEdge(fromX, fromY, toX, toY) {
    const edge = { fromX, fromY, toX, toY, walked: false };
    const key = `${fromX},${fromY}`;
    if (!edgesFrom.has(key)) edgesFrom.set(key, []);
    edgesFrom.get(key).push(edge);
    return edge;
  }

  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      if (!isCell(x, y)) continue;
      if (!isCell(x, y - 1)) topEdges.push(addEdge(x, y, x + 1, y));
      if (!isCell(x + 1, y)) addEdge(x + 1, y, x + 1, y + 1);
      if (!isCell(x, y + 1)) addEdge(x + 1, y + 1, x, y + 1);
      if (!isCell(x - 1, y)) addEdge(x, y + 1, x, y);
    }
  }

  /** Positive when `next` turns right off `edge`, zero when it goes straight on. */
  const turn = (edge, next) => (edge.toX - edge.fromX) * (next.toY - next.fromY) - (edge.toY - edge.fromY) * (next.toX - next.fromX);

  /** Where two cells touch only at a corner, the sharpest right turn keeps each loop on its own cell. */
  function edgeAfter(edge) {
    const onward = (edgesFrom.get(`${edge.toX},${edge.toY}`) ?? []).filter((candidate) => !candidate.walked);
    return onward.sort((a, b) => turn(edge, b) - turn(edge, a))[0]; // sorts the filter's own list, not the map's
  }

  function walkLoop(first) {
    const corners = [];
    let previous;
    for (let edge = first; edge !== undefined && !edge.walked; edge = edgeAfter(edge)) {
      edge.walked = true;
      const goesStraightOn = previous !== undefined && turn(previous, edge) === 0;
      if (!goesStraightOn) corners.push({ x: edge.fromX, y: edge.fromY });
      previous = edge;
    }
    return corners;
  }

  const loops = [];
  for (const edge of topEdges) {
    if (!edge.walked) loops.push(walkLoop(edge)); // an earlier loop may have walked this edge already
  }
  return loops;
}

/** Which way is out for the side that runs from one corner to the next: a unit step away from the cells. */
const outward = (from, to) => ({ x: Math.sign(to.y - from.y), y: -Math.sign(to.x - from.x) });

/**
 * A loop as points in px at `cellSize` px a cell, every side moved `distance` px outwards from the cells (inwards
 * when negative).
 */
export function movedOut(corners, cellSize, distance) {
  return corners.map((corner, index) => {
    const before = outward(corners[(index + corners.length - 1) % corners.length], corner);
    const after = outward(corner, corners[(index + 1) % corners.length]);
    return { x: corner.x * cellSize + distance * (before.x + after.x), y: corner.y * cellSize + distance * (before.y + after.y) };
  });
}

/** A length in px as a path writes it: two decimals at most. */
const px = (value) => String(Math.round(value * 100) / 100);

/** The path data of closed loops of points. */
export function pathOf(loops) {
  return loops.map((points) => `M${points.map((point) => `${px(point.x)} ${px(point.y)}`).join("L")}Z`).join("");
}

/** How long a closed loop of points is, in px. */
export function lengthOf(points) {
  return points.reduce((length, point, index) => {
    const next = points[(index + 1) % points.length];
    return length + Math.hypot(next.x - point.x, next.y - point.y);
  }, 0);
}
