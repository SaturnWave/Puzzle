// The game as both pages draw it, the lab's and the public mode's player page: the tile art, the HUD's numbers, and
// the board, which plays the engine's events as animation and reports the player's moves. The server runs the real
// engine; a page sends the moves it is told of and hands the answers back. After every move the board is reconciled
// with the authoritative state, so a missed animation can never leave the view wrong.

import { make, svgElement } from "./dom.js";

const SPECIALS = new Set(["rocketH", "rocketV", "bomb", "bigBomb", "seeker", "colorBomb"]);
const SWAPPABLE = new Set([...SPECIALS, "gem", "item"]);
const STEP_MS = { swap: 170, hit: 230, fall: 260 };
const CELL_PX = { min: 22, max: 64 };
const ART_TIMEOUT_MS = 30_000;

export const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");

// ---------- tile art ----------
/** The tile art could not be fetched or is not the sprite; `cause` says which. */
export class TileArtError extends Error {
  constructor(message, options) {
    super(message, options);
    this.name = "TileArtError";
  }
}

/**
 * Puts the tile sprite (tiles.svg, beside this module) into the page. Every tile, special and obstacle is one of its
 * symbols, shown by <use>. It is inlined rather than referenced as a file: the gradients and clip paths of a sprite
 * that stays outside the page do not resolve in every browser.
 */
export async function installTileArt() {
  const sprite = new DOMParser().parseFromString(await fetchTileArt(), "image/svg+xml").documentElement;
  if (sprite.localName !== "svg") throw new TileArtError("tiles.svg is not an SVG document");
  document.body.prepend(document.importNode(sprite, true));
}

async function fetchTileArt() {
  let response;
  try {
    response = await fetch(new URL("tiles.svg", import.meta.url), { signal: AbortSignal.timeout?.(ART_TIMEOUT_MS) });
  } catch (cause) {
    throw new TileArtError("tiles.svg could not be fetched", { cause });
  }
  if (!response.ok) throw new TileArtError(`tiles.svg answered ${response.status}`);
  return response.text();
}

/** An inline SVG that shows one symbol of the tile sprite. */
export function symbolArt(symbol, className = "art") {
  const svg = svgElement("svg", { class: className, viewBox: "0 0 100 100", "aria-hidden": "true" });
  svg.appendChild(svgElement("use", { href: `#${symbol}` }));
  return svg;
}

// ---------- HUD ----------
const GOAL_NAMES = { breakBoxes: "Kutu kır", clearJelly: "Jöle temizle", breakChains: "Zincir kır", dropItems: "Taç indir" };
const COLOR_NAMES = { red: "kırmızı", green: "yeşil", blue: "mavi", yellow: "sarı", purple: "mor", orange: "turuncu", cyan: "turkuaz", pink: "pembe" };
const LOW_MOVES = 3;

function goalIcon(goal) {
  const icon = make("span", "goal-icon");
  switch (goal.type) {
    case "collectColor": icon.appendChild(symbolArt(`gem-${goal.color.toLowerCase()}`)); break;
    case "breakBoxes": icon.appendChild(symbolArt("sp-box-1")); break;
    case "dropItems": icon.appendChild(symbolArt("item")); break;
    case "breakChains": icon.classList.add("on-cell"); icon.appendChild(symbolArt("ov-chain-1")); break;
    case "clearJelly": icon.classList.add("jelly-icon"); break;
    default: icon.textContent = "?"; break;
  }
  return icon;
}

function goalName(goal) {
  if (goal.type === "collectColor") return `${COLOR_NAMES[goal.color.toLowerCase()] ?? goal.color} taş topla`;
  return GOAL_NAMES[goal.type] ?? goal.type;
}

/** One goal as a chip: its icon and what is left of it, or a check mark once it is met. */
function goalChip(goal) {
  const done = goal.done >= goal.target;
  const chip = make("div", `goal${done ? " done" : ""}`);
  chip.title = `${goalName(goal)}: ${Math.min(goal.done, goal.target)} / ${goal.target}`;
  chip.append(goalIcon(goal), done ? make("span", "done-mark", "✓") : make("span", "", String(goal.target - goal.done)));
  return chip;
}

/**
 * Shows a state's numbers in a page's HUD: the moves left in `movesLeft`, with `moves` marked when few remain, and a
 * chip per goal in `goals`.
 */
export function showHud({ moves, movesLeft, goals }, state) {
  const left = Math.max(0, state.moveLimit - state.movesUsed);
  movesLeft.textContent = left;
  moves.classList.toggle("low", state.status === "Playing" && left <= LOW_MOVES);
  goals.replaceChildren(...state.goals.map(goalChip));
}

const END_TITLES = { Won: "Kazandın!", LostOutOfMoves: "Hamle bitti", LostNoMovesPossible: "Oynanacak hamle kalmadı" };

/** What a result banner says for the status a game ended with. */
export const endTitle = (status) => END_TITLES[status] ?? status;

// ---------- tiles ----------
const isAdjacent = (a, b) => Math.abs(a.x - b.x) + Math.abs(a.y - b.y) === 1;

function tileSignature(tile) {
  return tile ? `${tile.kind}|${tile.color ?? ""}|${tile.hp}` : "";
}

/** The sprite symbol that draws a tile: every gem colour has its own shape. */
function symbolFor(tile) {
  switch (tile.kind) {
    case "gem": return `gem-${tile.color.toLowerCase()}`;
    case "rocketH": return "sp-rocket-h";
    case "rocketV": return "sp-rocket-v";
    case "bomb": return "sp-bomb";
    case "bigBomb": return "sp-bigbomb";
    case "seeker": return "sp-seeker";
    case "colorBomb": return "sp-colorbomb";
    case "box": return `sp-box-${Math.min(3, Math.max(1, tile.hp))}`;
    case "item": return "item";
    default: return "unknown";
  }
}

function tileClass(tile) {
  if (tile.kind === "gem") return `tile k-gem gem c-${tile.color.toLowerCase()}`;
  if (tile.kind === "box") return `tile k-box hp-${tile.hp}`;
  return `tile k-${tile.kind}`;
}

function paintFace(face, tile) {
  face.replaceChildren(symbolArt(symbolFor(tile)));
  if (tile.kind === "box" && tile.hp > 1) face.appendChild(make("span", "hp", String(tile.hp)));
}

function placeAt(el, p) {
  el.style.setProperty("--x", p.x);
  el.style.setProperty("--y", p.y);
}

/** Restarts a one-shot animation class on an element. */
function flashClass(el, className, ms) {
  el.classList.remove(className);
  el.getBoundingClientRect();
  el.classList.add(className);
  setTimeout(() => el.classList.remove(className), ms);
}

/** Which animation step an engine event belongs to; a marker only ends the step before it. */
function category(type) {
  switch (type) {
    case "swapped": return "swap";
    case "tileMoved":
    case "tileSpawned": return "fall";
    case "roundStarted":
    case "shuffled": return "marker";
    default: return "hit";
  }
}

/**
 * How much longer than a one-row fall a fall of `rows` takes. Under gravity the time grows with the square root of
 * the distance; this keeps a short drop snappy and lets a long refill visibly accelerate.
 */
const fallFactor = (rows) => 0.75 + 0.25 * Math.sqrt(Math.max(1, rows));

// ---------- board ----------
/**
 * The board drawn in `element`. `onMove(move)` is told every move the player makes on it (a swap by dragging or by
 * touching two neighbours in turn, a tap on a special); `mayPlay()` says whether the player's input counts right now;
 * `space()` gives the room the board may take, as `{ width, height }` in pixels.
 */
export function createBoard(element, { onMove, mayPlay, space }) {
  const tiles = new Map();  // cell index -> tile element
  const chains = new Map(); // cell index -> chain overlay element
  let cells = [];           // cell index -> cell element (or null for gaps)
  let state = null;         // the server state the board shows
  let selected = null;      // {x, y}
  let pointerStart = null;
  let speed = 1;

  const indexOf = (p) => p.y * state.width + p.x;
  const posOf = (i) => ({ x: i % state.width, y: Math.floor(i / state.width) });

  /** Steps still take turns without motion, only quicker, so a reduced-motion player can follow them. */
  const motionScale = () => (reducedMotion.matches ? 0.5 : 1);
  const wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms * speed * motionScale()));

  /** Sizes the cells to the room the page gives the board. */
  function fit() {
    if (!state) return;
    const room = space();
    const size = Math.max(CELL_PX.min, Math.floor(Math.min(room.width / state.width, room.height / state.height, CELL_PX.max)));
    element.classList.add("still"); // a new size moves every tile at once: no slide across the board
    element.style.setProperty("--cell-size", `${size}px`);
    element.style.setProperty("--cols", state.width);
    element.style.setProperty("--rows", state.height);
    element.getBoundingClientRect();
    requestAnimationFrame(() => element.classList.remove("still"));
  }

  /** How long the steps take: 1 is the normal pace, less is faster. */
  function setSpeed(next) {
    speed = next;
    element.style.setProperty("--move-ms", `${Math.round(STEP_MS.fall * speed * 0.9)}ms`);
    element.style.setProperty("--swap-ms", `${Math.round(STEP_MS.swap * speed)}ms`);
    element.style.setProperty("--fade-ms", `${Math.round(STEP_MS.hit * speed * 0.8)}ms`);
  }

  // ---------- tiles and overlays ----------
  function createTile(tile, p) {
    const el = make("div", tileClass(tile));
    el.dataset.sig = tileSignature(tile);
    const face = make("div", "face");
    paintFace(face, tile);
    el.appendChild(face);
    placeAt(el, p);
    element.appendChild(el);
    return el;
  }

  function removeTile(el, animated) {
    if (!animated) {
      el.remove();
      return;
    }
    el.classList.add("dying");
    setTimeout(() => el.remove(), STEP_MS.hit * speed + 60);
  }

  /** Draws `next` from nothing: a new game, or the one the page came back to. */
  function render(next) {
    state = next;
    element.replaceChildren();
    tiles.clear();
    chains.clear();
    cells = [];
    selected = null;
    fit();
    for (let i = 0; i < state.width * state.height; i++) {
      const p = posOf(i);
      if (!state.playable[i]) {
        cells.push(null);
        continue;
      }
      const cell = make("div", `cell${(p.x + p.y) % 2 ? " alt" : ""}`);
      placeAt(cell, p);
      element.appendChild(cell);
      cells.push(cell);
      if (state.tiles[i]) tiles.set(i, createTile(state.tiles[i], p));
    }
    updateOverlays();
  }

  function updateOverlays() {
    cells.forEach((cell, i) => {
      if (!cell) return;
      setJelly(cell, state.jelly[i]);
      setChain(i, state.chains[i]);
    });
  }

  /** Shows `layers` of jelly on a cell; `animated` when a hit took a layer off (it squishes, the last one melts away). */
  function setJelly(cell, layers, animated = false) {
    const had = cell.classList.contains("jelly-1") || cell.classList.contains("jelly-2");
    cell.classList.toggle("jelly-1", layers === 1);
    cell.classList.toggle("jelly-2", layers >= 2);
    if (animated && had) flashClass(cell, layers > 0 ? "jelly-hit" : "jelly-clear", STEP_MS.hit * speed + 60);
  }

  function setChain(i, hp) {
    let el = chains.get(i);
    if (hp > 0) {
      if (!el) {
        el = make("div", "chain");
        placeAt(el, posOf(i));
        element.appendChild(el);
        chains.set(i, el);
      }
      if (el.dataset.hp !== String(hp)) {
        el.dataset.hp = String(hp);
        el.className = `chain hp-${hp}`;
        el.replaceChildren(symbolArt(`ov-chain-${Math.min(hp, 2)}`));
      }
    } else if (el) {
      chains.delete(i);
      el.classList.add("breaking");
      setTimeout(() => el.remove(), STEP_MS.hit * speed + 60);
    }
  }

  /** Makes the board match the server state `next` exactly, without animation. */
  function reconcile(next) {
    state = next;
    let corrected = 0;
    for (let i = 0; i < state.width * state.height; i++) {
      if (!state.playable[i]) continue;
      const expected = state.tiles[i];
      const el = tiles.get(i);
      if (!expected) {
        if (el) { removeTile(el, false); tiles.delete(i); corrected++; }
      } else if (!el || el.dataset.sig !== tileSignature(expected)) {
        if (el) removeTile(el, false);
        corrected++;
        const fresh = createTile(expected, posOf(i));
        fresh.classList.add("instant");
        tiles.set(i, fresh);
      } else {
        placeAt(el, posOf(i));
      }
    }
    updateOverlays();
    if (corrected > 0) console.warn(`reconcile corrected ${corrected} cell(s): the animation missed an event`);
  }

  // ---------- animation ----------
  /** Plays the engine's events of one move, step by step; resolves when the last step is over. */
  async function playEvents(events) {
    let step = [];
    let stepKind = null;
    for (const event of events) {
      const kind = category(event.type);
      if (kind === "marker" || (stepKind && kind !== stepKind)) {
        await runStep(stepKind, step);
        step = [];
        stepKind = null;
      }
      if (kind !== "marker") {
        step.push(event);
        stepKind = kind;
      }
    }
    await runStep(stepKind, step);
  }

  async function runStep(kind, events) {
    if (!kind || events.length === 0) return;
    element.dataset.phase = kind;
    let duration = STEP_MS[kind];
    if (kind === "fall") {
      const spawned = new Map(); // column -> spawns so far in this step
      let slowest = 1;
      for (const e of events) {
        const factor = e.type === "tileMoved" ? moveTile(indexOf(e.from), indexOf(e.to)) : spawnTile(e, spawned);
        slowest = Math.max(slowest, factor);
      }
      if (!reducedMotion.matches) duration *= slowest; // without motion nothing travels, so nothing needs the time
    } else {
      for (const e of events) applyHit(e);
    }
    await wait(duration);
  }

  /** Slides a tile to its new cell; returns its fall factor. */
  function moveTile(from, to) {
    const el = tiles.get(from);
    if (!el) return 1;
    tiles.delete(from);
    tiles.set(to, el);
    const a = posOf(from);
    const b = posOf(to);
    const factor = fallFactor(Math.hypot(b.x - a.x, b.y - a.y));
    el.style.setProperty("--fall", factor.toFixed(3));
    placeAt(el, b);
    return factor;
  }

  /** Drops a new tile in from above its column's top cell; returns its fall factor. */
  function spawnTile(e, spawned) {
    const column = e.at.x;
    const count = (spawned.get(column) ?? 0) + 1;
    spawned.set(column, count);
    const start = { x: column, y: topRow(column) - count };
    const el = createTile(e.tile, start);
    el.classList.add("instant");
    el.getBoundingClientRect(); // commit the start position before animating
    el.classList.remove("instant");
    tiles.set(indexOf(e.at), el);
    const factor = fallFactor(e.at.y - start.y);
    el.style.setProperty("--fall", factor.toFixed(3));
    placeAt(el, e.at);
    return factor;
  }

  function topRow(column) {
    for (let y = 0; y < state.height; y++) {
      if (state.playable[y * state.width + column]) return y;
    }
    return 0;
  }

  function applyHit(e) {
    switch (e.type) {
      case "swapped": {
        const a = indexOf(e.from);
        const b = indexOf(e.to);
        const elA = tiles.get(a);
        const elB = tiles.get(b);
        tiles.delete(a);
        tiles.delete(b);
        if (elA) { tiles.set(b, elA); placeAt(elA, e.to); }
        if (elB) { tiles.set(a, elB); placeAt(elB, e.from); }
        break;
      }
      case "tileDestroyed":
      case "itemCollected": {
        const i = indexOf(e.at);
        const el = tiles.get(i);
        if (el) { tiles.delete(i); removeTile(el, true); }
        break;
      }
      case "boxDamaged": {
        const i = indexOf(e.at);
        const el = tiles.get(i);
        if (!el) break;
        if (e.hp === 0) {
          tiles.delete(i);
          removeTile(el, true);
        } else {
          const box = { kind: "box", hp: e.hp };
          el.className = tileClass(box);
          paintFace(el.querySelector(".face"), box);
          el.dataset.sig = tileSignature(box);
          flashClass(el, "bump", STEP_MS.hit * speed + 60);
        }
        break;
      }
      case "chainDamaged":
        setChain(indexOf(e.at), e.hp);
        break;
      case "jellyDamaged": {
        const cell = cells[indexOf(e.at)];
        if (cell) setJelly(cell, e.hp, true);
        break;
      }
      case "specialCreated": {
        const i = indexOf(e.at);
        const old = tiles.get(i);
        if (old) removeTile(old, false);
        const el = createTile(e.tile, e.at);
        el.classList.add("pop");
        tiles.set(i, el);
        break;
      }
      case "effectTriggered":
        showEffect(e);
        break;
      case "seekerLaunched":
        showSeeker(e);
        break;
      default:
        break; // matchCleared: the destroyed tiles already animate
    }
  }

  function addFx(kind, x, y, w, h) {
    const fx = make("div", `fx ${kind}`);
    fx.style.left = `calc(${x} * var(--cell-size))`;
    fx.style.top = `calc(${y} * var(--cell-size))`;
    fx.style.width = `calc(${w} * var(--cell-size))`;
    fx.style.height = `calc(${h} * var(--cell-size))`;
    element.appendChild(fx);
    setTimeout(() => fx.remove(), 2 * STEP_MS.hit * speed + 100);
  }

  function showEffect(e) {
    const { width, height } = state;
    const { x, y } = e.at;
    const r = e.radius ?? 0;
    switch (e.kind) {
      case "clearRow": addFx("beam-h", 0, y, width, 1); break;
      case "clearColumn": addFx("beam-v", x, 0, 1, height); break;
      case "explode": addFx("burst", x - r - 0.5, y - r - 0.5, 2 * r + 2, 2 * r + 2); break;
      case "cross": addFx("beam-h", 0, y - r, width, 2 * r + 1); addFx("beam-v", x - r, 0, 2 * r + 1, height); break;
      case "clearBoard":
      case "clearColor":
      case "convertColor": addFx("wash", 0, 0, width, height); break;
      default: addFx("burst", x - 1.5, y - 1.5, 4, 4); break;
    }
  }

  function showSeeker(e) {
    const fx = make("div", "fx seeker");
    fx.appendChild(symbolArt("sp-seeker"));
    fx.style.transform = `translate(calc(${e.from.x} * var(--cell-size)), calc(${e.from.y} * var(--cell-size)))`;
    element.appendChild(fx);
    fx.getBoundingClientRect();
    fx.style.transform = `translate(calc(${e.to.x} * var(--cell-size)), calc(${e.to.y} * var(--cell-size)))`;
    setTimeout(() => fx.remove(), STEP_MS.fall * speed + 120);
  }

  /** The tiles now on the cells `positions`; a cell without a tile gives none. */
  const tilesAt = (positions) => positions.filter(Boolean).map((p) => tiles.get(indexOf(p))).filter(Boolean);

  /**
   * Shows that the engine refused `move`. A refused swap trades the two tiles and slides them back, as in the game; a
   * refused tap shakes. Without motion the tiles only dim for a moment instead.
   */
  async function showRefused(move) {
    if (!move) return;
    const refused = tilesAt([move.from, move.to]);
    if (reducedMotion.matches) {
      for (const el of refused) flashClass(el, "refused", 400);
      return;
    }
    if (move.kind !== "swap" || refused.length !== 2) {
      for (const el of refused) flashClass(el, "shake", 340);
      return;
    }
    const [a, b] = refused;
    element.dataset.phase = "swap";
    placeAt(a, move.to);
    placeAt(b, move.from);
    await wait(STEP_MS.swap);
    placeAt(a, move.from);
    placeAt(b, move.to);
    await wait(STEP_MS.swap);
  }

  /** Marks the tiles on `positions` with a one-shot class for `ms` milliseconds: a hint's ring, say. */
  function flashTiles(positions, className, ms) {
    for (const el of tilesAt(positions)) flashClass(el, className, ms);
  }

  // ---------- input ----------
  function cellFromEvent(event) {
    const rect = element.getBoundingClientRect();
    const size = rect.width / state.width;
    const x = Math.floor((event.clientX - rect.left) / size);
    const y = Math.floor((event.clientY - rect.top) / size);
    return { x, y, size };
  }

  const isOnBoard = (p) => p.x >= 0 && p.y >= 0 && p.x < state.width && p.y < state.height;

  function onPointerDown(event) {
    if (!state || !mayPlay()) return;
    const cell = cellFromEvent(event);
    if (!isOnBoard(cell)) return;
    pointerStart = { ...cell, clientX: event.clientX, clientY: event.clientY };
    element.setPointerCapture(event.pointerId);
  }

  function onPointerUp(event) {
    if (!pointerStart) return;
    const start = pointerStart;
    pointerStart = null;
    const dx = event.clientX - start.clientX;
    const dy = event.clientY - start.clientY;
    const threshold = start.size * 0.3;
    if (Math.max(Math.abs(dx), Math.abs(dy)) < threshold) {
      onTap({ x: start.x, y: start.y });
      return;
    }
    const to = Math.abs(dx) > Math.abs(dy)
      ? { x: start.x + Math.sign(dx), y: start.y }
      : { x: start.x, y: start.y + Math.sign(dy) };
    if (isOnBoard(to)) onMove({ kind: "swap", from: { x: start.x, y: start.y }, to }); // a drag off the edge is no move
  }

  function onTap(p) {
    const tile = state.tiles[indexOf(p)];
    if (selected) {
      const from = selected;
      if (from.x === p.x && from.y === p.y) { clearSelection(); return; }
      if (isAdjacent(from, p)) { onMove({ kind: "swap", from, to: p }); return; }
    }
    if (tile && SPECIALS.has(tile.kind) && state.chains[indexOf(p)] === 0) {
      onMove({ kind: "tap", from: p });
      return;
    }
    if (tile && SWAPPABLE.has(tile.kind)) select(p); else clearSelection();
  }

  function select(p) {
    clearSelection();
    selected = p;
    tiles.get(indexOf(p))?.classList.add("selected");
  }

  function clearSelection() {
    if (selected) tiles.get(indexOf(selected))?.classList.remove("selected");
    selected = null;
  }

  element.addEventListener("pointerdown", onPointerDown);
  element.addEventListener("pointerup", onPointerUp);
  element.addEventListener("pointercancel", () => { pointerStart = null; });

  return { render, reconcile, playEvents, showRefused, flashTiles, clearSelection, fit, setSpeed, wait };
}
