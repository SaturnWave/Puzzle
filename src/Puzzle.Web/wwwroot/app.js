// Puzzle Test Lab client. The server runs the real engine; this file only draws the board and animates
// the engine's events. After every move the board is reconciled with the authoritative state, so a
// missed animation can never leave the view wrong.

const $ = (id) => document.getElementById(id);
const SPECIALS = new Set(["rocketH", "rocketV", "bomb", "bigBomb", "seeker", "colorBomb"]);
const SWAPPABLE = new Set([...SPECIALS, "gem", "item"]);
const GLYPHS = { rocketH: "⇆", rocketV: "⇅", bomb: "✸", bigBomb: "✹", seeker: "✈", colorBomb: "", item: "★" };
const STEP_MS = { swap: 170, hit: 230, fall: 260 };

const ui = {
  session: null,
  state: null,
  levels: [],
  tiles: new Map(), // cell index -> tile element
  chains: new Map(), // cell index -> chain overlay element
  cells: [],        // cell index -> cell element (or null for gaps)
  selected: null,   // {x, y}
  attempts: new Map(), // level key -> next retry number
  busy: false,
  autoplay: false,
  speed: 1,
};

// ---------- API ----------
async function api(path, body) {
  const response = await fetch(path, body === undefined
    ? {}
    : { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
  if (!response.ok) {
    const text = await response.text();
    throw new Error(text.replace(/^"|"$/g, "") || `${response.status} ${response.statusText}`);
  }
  return response.json();
}

function reportError(error) {
  $("status-line").textContent = `Hata: ${error.message}`;
}

// ---------- geometry ----------
const indexOf = (p) => p.y * ui.state.width + p.x;
const posOf = (i) => ({ x: i % ui.state.width, y: Math.floor(i / ui.state.width) });
const wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms * ui.speed));
const isAdjacent = (a, b) => Math.abs(a.x - b.x) + Math.abs(a.y - b.y) === 1;

function layoutBoard() {
  if (!ui.state) return;
  const area = document.querySelector(".board-area");
  const available = Math.min(area.clientWidth - 8, 720);
  const byHeight = Math.max(260, window.innerHeight - 230);
  const size = Math.max(24, Math.floor(Math.min(available / ui.state.width, byHeight / ui.state.height, 64)));
  const board = $("board");
  board.style.setProperty("--cell-size", `${size}px`);
  board.style.setProperty("--cols", ui.state.width);
  board.style.setProperty("--rows", ui.state.height);
}

function setSpeed(speed) {
  ui.speed = speed;
  document.documentElement.style.setProperty("--move-ms", `${Math.round(STEP_MS.fall * speed * 0.9)}ms`);
  document.documentElement.style.setProperty("--fade-ms", `${Math.round(STEP_MS.hit * speed * 0.8)}ms`);
}

// ---------- rendering ----------
function tileSignature(tile) {
  return tile ? `${tile.kind}|${tile.color ?? ""}|${tile.hp}` : "";
}

function createTile(tile, p) {
  const el = document.createElement("div");
  el.className = `tile k-${tile.kind}`;
  if (tile.kind === "gem") el.classList.add("gem", `c-${tile.color.toLowerCase()}`);
  if (tile.kind === "box") el.classList.add(`hp-${tile.hp}`);
  el.dataset.sig = tileSignature(tile);
  const face = document.createElement("div");
  face.className = "face";
  face.textContent = tile.kind === "box" ? String(tile.hp) : (GLYPHS[tile.kind] ?? "");
  el.appendChild(face);
  placeTile(el, p);
  $("board").appendChild(el);
  return el;
}

function placeTile(el, p) {
  el.style.setProperty("--x", p.x);
  el.style.setProperty("--y", p.y);
}

function removeTile(el, animated) {
  if (!animated) {
    el.remove();
    return;
  }
  el.classList.add("dying");
  setTimeout(() => el.remove(), STEP_MS.hit * ui.speed + 60);
}

function renderBoard(state) {
  ui.state = state;
  const board = $("board");
  board.innerHTML = "";
  ui.tiles.clear();
  ui.chains.clear();
  ui.cells = [];
  layoutBoard();
  for (let i = 0; i < state.width * state.height; i++) {
    const p = posOf(i);
    if (!state.playable[i]) {
      ui.cells.push(null);
      continue;
    }
    const cell = document.createElement("div");
    cell.className = `cell${(p.x + p.y) % 2 ? " alt" : ""}`;
    cell.style.setProperty("--x", p.x);
    cell.style.setProperty("--y", p.y);
    board.appendChild(cell);
    ui.cells.push(cell);
    if (state.tiles[i]) ui.tiles.set(i, createTile(state.tiles[i], p));
  }
  updateOverlays(state);
  updateHud(state);
}

function updateOverlays(state) {
  ui.cells.forEach((cell, i) => {
    if (!cell) return;
    cell.classList.toggle("jelly-1", state.jelly[i] === 1);
    cell.classList.toggle("jelly-2", state.jelly[i] >= 2);
    setChain(i, state.chains[i]);
  });
}

function setChain(i, hp) {
  let el = ui.chains.get(i);
  if (hp > 0) {
    if (!el) {
      el = document.createElement("div");
      const p = posOf(i);
      el.style.setProperty("--x", p.x);
      el.style.setProperty("--y", p.y);
      $("board").appendChild(el);
      ui.chains.set(i, el);
    }
    el.className = `chain hp-${hp}`;
  } else if (el) {
    el.remove();
    ui.chains.delete(i);
  }
}

/** Makes the view match the server state exactly, without animation. */
function reconcile(state) {
  ui.state = state;
  let corrected = 0;
  for (let i = 0; i < state.width * state.height; i++) {
    if (!state.playable[i]) continue;
    const expected = state.tiles[i];
    const el = ui.tiles.get(i);
    if (!expected) {
      if (el) { removeTile(el, false); ui.tiles.delete(i); corrected++; }
    } else if (!el || el.dataset.sig !== tileSignature(expected)) {
      if (el) removeTile(el, false);
      corrected++;
      const fresh = createTile(expected, posOf(i));
      fresh.classList.add("instant");
      ui.tiles.set(i, fresh);
    } else {
      placeTile(el, posOf(i));
    }
  }
  updateOverlays(state);
  updateHud(state);
  if (corrected > 0) console.warn(`reconcile corrected ${corrected} cell(s): the animation missed an event`);
}

// ---------- HUD ----------
function goalIcon(goal) {
  const icon = document.createElement("div");
  icon.className = "icon";
  const kind = { breakBoxes: "box", dropItems: "item" }[goal.type];
  if (goal.type === "collectColor") {
    icon.innerHTML = `<div class="tile gem c-${goal.color.toLowerCase()}" style="--cell-size:22px;--x:0;--y:0"><div class="face"></div></div>`;
  } else if (kind) {
    icon.innerHTML = `<div class="tile k-${kind}" style="--cell-size:22px;--x:0;--y:0"><div class="face">${kind === "item" ? "★" : ""}</div></div>`;
  } else {
    const label = { clearJelly: "🟪", breakChains: "⛓" }[goal.type] ?? "?";
    icon.textContent = label;
  }
  return icon;
}

function updateHud(state) {
  $("moves-left").textContent = Math.max(0, state.moveLimit - state.movesUsed);
  const goals = $("goals");
  goals.innerHTML = "";
  for (const goal of state.goals) {
    const chip = document.createElement("div");
    const done = goal.done >= goal.target;
    chip.className = `goal${done ? " done" : ""}`;
    chip.appendChild(goalIcon(goal));
    const text = document.createElement("span");
    text.textContent = done ? "✓" : `${Math.max(0, goal.target - goal.done)}`;
    text.title = `${Math.min(goal.done, goal.target)} / ${goal.target}`;
    chip.appendChild(text);
    goals.appendChild(chip);
  }
  showBanner(state);
}

function showBanner(state) {
  const banner = $("banner");
  if (state.status === "Playing") {
    banner.classList.add("hidden");
    return;
  }
  const won = state.status === "Won";
  banner.innerHTML = `<div><strong>${won ? "Kazandın!" : "Hamle bitti"}</strong>
    <span>${state.movesUsed} hamle · ${describeAttempt(ui.session)}</span><br>
    <button id="banner-next">Tekrar dene (yeni desen, aynı zorluk)</button> <button id="banner-repeat">Aynı seed</button></div>`;
  banner.classList.remove("hidden");
  $("banner-next").onclick = () => newGame({ kind: "next" });
  $("banner-repeat").onclick = () => newGame({ kind: "repeat" });
}

// ---------- animation ----------
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
  if (kind === "fall") {
    const spawned = new Map(); // column -> spawns so far in this step
    for (const e of events) {
      if (e.type === "tileMoved") moveTile(indexOf(e.from), indexOf(e.to));
      else spawnTile(e, spawned);
    }
  } else {
    for (const e of events) applyHit(e);
  }
  await wait(STEP_MS[kind]);
}

function moveTile(from, to) {
  const el = ui.tiles.get(from);
  if (!el) return;
  ui.tiles.delete(from);
  ui.tiles.set(to, el);
  placeTile(el, posOf(to));
}

function spawnTile(e, spawned) {
  const column = e.at.x;
  const count = (spawned.get(column) ?? 0) + 1;
  spawned.set(column, count);
  const top = topRow(column);
  const el = createTile(e.tile, { x: column, y: top - count });
  el.classList.add("instant");
  el.getBoundingClientRect(); // commit the start position before animating
  el.classList.remove("instant");
  ui.tiles.set(indexOf(e.at), el);
  placeTile(el, e.at);
}

function topRow(column) {
  for (let y = 0; y < ui.state.height; y++) {
    if (ui.state.playable[y * ui.state.width + column]) return y;
  }
  return 0;
}

function applyHit(e) {
  switch (e.type) {
    case "swapped": {
      const a = indexOf(e.from);
      const b = indexOf(e.to);
      const elA = ui.tiles.get(a);
      const elB = ui.tiles.get(b);
      ui.tiles.delete(a);
      ui.tiles.delete(b);
      if (elA) { ui.tiles.set(b, elA); placeTile(elA, e.to); }
      if (elB) { ui.tiles.set(a, elB); placeTile(elB, e.from); }
      break;
    }
    case "tileDestroyed":
    case "itemCollected": {
      const i = indexOf(e.at);
      const el = ui.tiles.get(i);
      if (el) { ui.tiles.delete(i); removeTile(el, true); }
      break;
    }
    case "boxDamaged": {
      const i = indexOf(e.at);
      const el = ui.tiles.get(i);
      if (!el) break;
      if (e.hp === 0) { ui.tiles.delete(i); removeTile(el, true); }
      else { el.className = `tile k-box hp-${e.hp}`; el.querySelector(".face").textContent = e.hp; el.dataset.sig = tileSignature({ kind: "box", hp: e.hp }); }
      break;
    }
    case "chainDamaged":
      setChain(indexOf(e.at), e.hp);
      break;
    case "jellyDamaged": {
      const cell = ui.cells[indexOf(e.at)];
      if (cell) { cell.classList.toggle("jelly-1", e.hp === 1); cell.classList.toggle("jelly-2", e.hp >= 2); }
      break;
    }
    case "specialCreated": {
      const i = indexOf(e.at);
      const old = ui.tiles.get(i);
      if (old) removeTile(old, false);
      const el = createTile(e.tile, e.at);
      el.classList.add("pop");
      ui.tiles.set(i, el);
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

function addFx(x, y, w, h) {
  const fx = document.createElement("div");
  fx.className = "fx";
  fx.style.left = `calc(${x} * var(--cell-size))`;
  fx.style.top = `calc(${y} * var(--cell-size))`;
  fx.style.width = `calc(${w} * var(--cell-size))`;
  fx.style.height = `calc(${h} * var(--cell-size))`;
  $("board").appendChild(fx);
  setTimeout(() => fx.remove(), 2 * STEP_MS.hit * ui.speed + 100);
}

function showEffect(e) {
  const { width, height } = ui.state;
  const { x, y } = e.at;
  const r = e.radius ?? 0;
  switch (e.kind) {
    case "clearRow": addFx(0, y, width, 1); break;
    case "clearColumn": addFx(x, 0, 1, height); break;
    case "explode": addFx(x - r, y - r, 2 * r + 1, 2 * r + 1); break;
    case "cross": addFx(0, y - r, width, 2 * r + 1); addFx(x - r, 0, 2 * r + 1, height); break;
    case "clearBoard":
    case "clearColor":
    case "convertColor": addFx(0, 0, width, height); break;
    default: addFx(x - 1, y - 1, 3, 3); break;
  }
}

function showSeeker(e) {
  const fx = document.createElement("div");
  fx.className = "fx seeker";
  fx.textContent = "✈";
  fx.style.width = fx.style.height = "var(--cell-size)";
  fx.style.transform = `translate(calc(${e.from.x} * var(--cell-size)), calc(${e.from.y} * var(--cell-size)))`;
  $("board").appendChild(fx);
  fx.getBoundingClientRect();
  fx.style.transform = `translate(calc(${e.to.x} * var(--cell-size)), calc(${e.to.y} * var(--cell-size)))`;
  setTimeout(() => fx.remove(), STEP_MS.fall * ui.speed + 120);
}

// ---------- moves ----------
async function sendMove(move) {
  if (ui.busy || !ui.session || ui.state.status !== "Playing") return null;
  ui.busy = true;
  clearSelection();
  try {
    const result = await api(`/api/sessions/${ui.session.sessionId}/moves`, move);
    await applyResult(result);
    return result;
  } catch (error) {
    reportError(error);
    return null;
  } finally {
    ui.busy = false;
  }
}

async function applyResult(result) {
  if (!result.accepted) {
    for (const p of [result.move.from, result.move.to].filter(Boolean)) {
      const el = ui.tiles.get(indexOf(p));
      if (el) { el.classList.add("shake"); setTimeout(() => el.classList.remove("shake"), 320); }
    }
    return;
  }
  await playEvents(result.events);
  reconcile(result.state);
  logMove(result);
}

function describeMove(move) {
  return move.kind === "tap" ? `dokun (${move.from.x},${move.from.y})` : `(${move.from.x},${move.from.y})→(${move.to.x},${move.to.y})`;
}

function logMove(result) {
  const s = result.stats;
  const text = `${describeMove(result.move)} · ${s.gemsCleared} taş, +${s.specialsCreated} özel, ${s.specialsActivated} patlama, ${s.cascades} kaskad${s.shuffles ? ", karıştırıldı" : ""}`;
  $("last-move").textContent = text;
  const item = document.createElement("li");
  item.textContent = `#${result.state.movesUsed} ${text}`;
  const log = $("event-log");
  log.prepend(item);
  while (log.children.length > 60) log.lastChild.remove();
}

// ---------- input ----------
let pointerStart = null;

function cellFromEvent(event) {
  const rect = $("board").getBoundingClientRect();
  const size = rect.width / ui.state.width;
  const x = Math.floor((event.clientX - rect.left) / size);
  const y = Math.floor((event.clientY - rect.top) / size);
  return { x, y, size };
}

function onPointerDown(event) {
  if (!ui.state || ui.busy || ui.autoplay) return;
  const cell = cellFromEvent(event);
  if (cell.x < 0 || cell.y < 0 || cell.x >= ui.state.width || cell.y >= ui.state.height) return;
  pointerStart = { ...cell, clientX: event.clientX, clientY: event.clientY };
  $("board").setPointerCapture(event.pointerId);
}

function onPointerUp(event) {
  if (!pointerStart) return;
  const start = pointerStart;
  pointerStart = null;
  const dx = event.clientX - start.clientX;
  const dy = event.clientY - start.clientY;
  const threshold = start.size * 0.3;
  if (Math.max(Math.abs(dx), Math.abs(dy)) >= threshold) {
    const to = Math.abs(dx) > Math.abs(dy)
      ? { x: start.x + Math.sign(dx), y: start.y }
      : { x: start.x, y: start.y + Math.sign(dy) };
    sendMove({ kind: "swap", from: { x: start.x, y: start.y }, to });
  } else {
    onTap({ x: start.x, y: start.y });
  }
}

function onTap(p) {
  const tile = ui.state.tiles[indexOf(p)];
  if (ui.selected) {
    const from = ui.selected;
    if (from.x === p.x && from.y === p.y) { clearSelection(); return; }
    if (isAdjacent(from, p)) { sendMove({ kind: "swap", from, to: p }); return; }
  }
  if (tile && SPECIALS.has(tile.kind) && ui.state.chains[indexOf(p)] === 0) {
    sendMove({ kind: "tap", from: p });
    return;
  }
  if (tile && SWAPPABLE.has(tile.kind)) select(p); else clearSelection();
}

function select(p) {
  clearSelection();
  ui.selected = p;
  ui.tiles.get(indexOf(p))?.classList.add("selected");
}

function clearSelection() {
  if (ui.selected) ui.tiles.get(indexOf(ui.selected))?.classList.remove("selected");
  ui.selected = null;
}

// ---------- game flow ----------
function describeAttempt(session) {
  const board = session.startBoard === "fixed" ? "tahta sabit" : "tahta her denemede farklı";
  const source = session.attempt === undefined || session.attempt === null
    ? "elle seçilen seed"
    : `deneme #${session.attempt + 1}${session.fromPool ? ` (eşit zorluk havuzu, ${session.level.poolSize} seed)` : " (havuz yok: zorluk değişebilir)"}`;
  const seeds = session.boardSeed === session.playSeed ? `seed ${session.playSeed}` : `tahta ${session.boardSeed} / oyun ${session.playSeed}`;
  return `${source} · ${board} · ${seeds}`;
}

/** mode: {kind: "next"} next retry · {kind: "repeat"} exact same seeds · {kind: "seed", playSeed} explicit seed */
async function newGame(mode) {
  const levelKey = $("level-select").value;
  const assist = Number($("assist-input").value);
  const body = { levelKey, assist, startBoard: $("start-board-select").value };
  if (mode.kind === "repeat" && ui.session) {
    body.boardSeed = ui.session.boardSeed;
    body.playSeed = ui.session.playSeed;
  } else if (mode.kind === "seed") {
    body.playSeed = mode.playSeed;
  } else {
    const next = ui.attempts.get(levelKey) ?? 0;
    ui.attempts.set(levelKey, next + 1);
    body.attempt = next;
  }
  try {
    stopAutoplay();
    const session = await api("/api/sessions", body);
    ui.session = session;
    $("status-line").textContent = `${session.level.key} · ${describeAttempt(session)}${assist ? ` · yardım %${assist}` : ""}`;
    $("event-log").innerHTML = "";
    $("last-move").textContent = "—";
    $("solve-result").textContent = "Henüz çalıştırılmadı.";
    renderBoard(session.state);
  } catch (error) {
    reportError(error);
  }
}

async function botStep() {
  if (ui.busy || !ui.session || ui.state.status !== "Playing") return false;
  ui.busy = true;
  clearSelection();
  try {
    const result = await api(`/api/sessions/${ui.session.sessionId}/bot`, { bot: $("bot-select").value });
    await applyResult(result);
    return result.state.status === "Playing";
  } catch (error) {
    reportError(error);
    return false;
  } finally {
    ui.busy = false;
  }
}

async function toggleAutoplay() {
  if (ui.autoplay) { stopAutoplay(); return; }
  ui.autoplay = true;
  $("autoplay-btn").textContent = "Durdur ■";
  while (ui.autoplay && await botStep()) {
    await wait(250);
  }
  stopAutoplay();
}

function stopAutoplay() {
  ui.autoplay = false;
  $("autoplay-btn").textContent = "Bot oynasın ▶";
}

async function showHint() {
  if (!ui.session || ui.state.status !== "Playing") return;
  try {
    const move = await api(`/api/sessions/${ui.session.sessionId}/hint?bot=expert`);
    for (const p of [move.from, move.to].filter(Boolean)) {
      const el = ui.tiles.get(indexOf(p));
      if (el) { el.classList.add("hint"); setTimeout(() => el.classList.remove("hint"), 1900); }
    }
  } catch (error) {
    reportError(error);
  }
}

async function solve() {
  if (!ui.session) return;
  const box = $("solve-result");
  box.textContent = "Aranıyor…";
  try {
    const result = await api(`/api/sessions/${ui.session.sessionId}/solve`, { beam: Number($("beam-select").value) });
    box.innerHTML = "";
    const summary = document.createElement("p");
    summary.textContent = result.solved
      ? `Gelecek taşları bilen oyuncu kalan ${result.movesLeft} hamlenin ${result.movesNeeded} tanesiyle kazanıyor.`
      : `Kalan ${result.movesLeft} hamleyle kazanan bir yol bulunamadı.`;
    const meta = document.createElement("p");
    meta.className = "small";
    meta.textContent = `${result.positionsExplored} pozisyon, ${result.elapsedMs} ms. Not: çözücü yukarıdan düşecek taşları bilir; gerçek oyuncu bilemez.`;
    box.append(summary, meta);
    if (result.solved) {
      const play = document.createElement("button");
      play.textContent = "Çözümü oynat";
      play.onclick = () => playPath(result.path);
      box.appendChild(play);
    }
  } catch (error) {
    box.textContent = `Hata: ${error.message}`;
  }
}

async function playPath(path) {
  for (const move of path) {
    const result = await sendMove(move);
    if (!result || !result.accepted) break;
    await wait(150);
  }
}

// ---------- simulation ----------
async function simulate() {
  const box = $("sim-result");
  box.textContent = "Simüle ediliyor…";
  try {
    const r = await api("/api/simulate", {
      levelKey: $("level-select").value,
      bot: $("bot-select").value,
      runs: Number($("runs-select").value),
    });
    const pct = (v) => `${(v * 100).toFixed(1)}%`;
    const moves = (v) => v ?? ">150";
    box.innerHTML = `
      <table class="stats">
        <tr><td>Bot / oyun</td><td>${r.bot} · ${r.runs}</td></tr>
        <tr><td>Limit (${r.moveLimit}) ile kazanma</td><td>${pct(r.winAtLimit)}</td></tr>
        <tr><td>Near-miss (≥%80 hedef)</td><td>${pct(r.nearMissAtLimit)}</td></tr>
        <tr><td>Gereken hamle p10 / p50 / p90</td><td>${moves(r.movesNeeded.p10)} / ${moves(r.movesNeeded.p50)} / ${moves(r.movesNeeded.p90)}</td></tr>
        <tr><td>Özel taş / oyun</td><td>${r.specialsPerRun.toFixed(1)}</td></tr>
        <tr><td>Kaskad / hamle</td><td>${r.cascadesPerMove.toFixed(2)}</td></tr>
        <tr><td>Karıştırma / oyun</td><td>${r.averageShuffles.toFixed(2)}</td></tr>
        <tr><td>Süre</td><td>${r.elapsedMs} ms</td></tr>
      </table>`;
    box.appendChild(winRateChart(r.curve, r.moveLimit));
  } catch (error) {
    box.textContent = `Hata: ${error.message}`;
  }
}

/** Single-series line: win rate for every move limit, with the level's own limit marked. */
function winRateChart(curve, limit) {
  const W = 300, H = 170, L = 34, R = 8, T = 18, B = 26;
  const maxMoves = curve[curve.length - 1].moves;
  const sx = (m) => L + ((m - 1) / Math.max(1, maxMoves - 1)) * (W - L - R);
  const sy = (v) => T + (1 - v) * (H - T - B);
  const ns = "http://www.w3.org/2000/svg";
  const wrap = document.createElement("div");
  const svg = document.createElementNS(ns, "svg");
  svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
  svg.setAttribute("class", "chart");
  svg.setAttribute("role", "img");
  svg.setAttribute("aria-label", "Hamle limitine göre kazanma oranı");
  const parts = [`<text x="${L}" y="11">Hamle limitine göre kazanma oranı</text>`];
  for (const v of [0, 0.5, 1]) {
    parts.push(`<line class="grid" x1="${L}" x2="${W - R}" y1="${sy(v)}" y2="${sy(v)}"/>`);
    parts.push(`<text x="${L - 4}" y="${sy(v) + 4}" text-anchor="end">${v * 100}%</text>`);
  }
  const ticks = [1, Math.round(maxMoves / 2), maxMoves];
  for (const m of ticks) parts.push(`<text x="${sx(m)}" y="${H - 8}" text-anchor="middle">${m}</text>`);
  if (limit <= maxMoves) {
    parts.push(`<line class="limit" x1="${sx(limit)}" x2="${sx(limit)}" y1="${T}" y2="${H - B}"/>`);
    parts.push(`<text x="${sx(limit) + 3}" y="${T + 10}">limit ${limit}</text>`);
  }
  const d = curve.map((p, i) => `${i ? "L" : "M"}${sx(p.moves).toFixed(1)},${sy(p.winRate).toFixed(1)}`).join("");
  parts.push(`<path class="line" d="${d}"/>`);
  parts.push(`<line class="cross" id="chart-cross" y1="${T}" y2="${H - B}" visibility="hidden"/>`);
  parts.push(`<circle class="dot" id="chart-dot" r="4" visibility="hidden"/>`);
  parts.push(`<rect x="${L}" y="${T}" width="${W - L - R}" height="${H - T - B}" fill="transparent" id="chart-hit"/>`);
  svg.innerHTML = parts.join("");
  const tip = document.createElement("div");
  tip.className = "chart-tip";
  tip.textContent = "Grafiğin üzerine gel: her limit için kazanma oranı";
  wrap.append(svg, tip);
  const hit = svg.querySelector("#chart-hit");
  hit.addEventListener("pointermove", (event) => {
    const rect = svg.getBoundingClientRect();
    const x = ((event.clientX - rect.left) / rect.width) * W;
    const m = Math.min(maxMoves, Math.max(1, Math.round(1 + ((x - L) / (W - L - R)) * (maxMoves - 1))));
    const point = curve[m - 1];
    const cross = svg.querySelector("#chart-cross");
    const dot = svg.querySelector("#chart-dot");
    cross.setAttribute("x1", sx(m)); cross.setAttribute("x2", sx(m)); cross.setAttribute("visibility", "visible");
    dot.setAttribute("cx", sx(m)); dot.setAttribute("cy", sy(point.winRate)); dot.setAttribute("visibility", "visible");
    tip.textContent = `${m} hamle limiti → %${(point.winRate * 100).toFixed(1)} kazanma`;
  });
  hit.addEventListener("pointerleave", () => {
    svg.querySelector("#chart-cross").setAttribute("visibility", "hidden");
    svg.querySelector("#chart-dot").setAttribute("visibility", "hidden");
  });
  return wrap;
}

// ---------- startup ----------
/** Lists the levels matching every word of the filter (number, key, tier or goal text). */
function renderLevelOptions(filter) {
  const words = filter.toLowerCase().split(/s+/).filter(Boolean);
  const matches = ui.levels.filter((l) => {
    const text = `${l.key} ${l.id} ${l.difficulty} ${l.width}x${l.height} ${l.goals.join(" ")}`.toLowerCase();
    return words.every((w) => text.includes(w));
  });
  const current = $("level-select").value;
  $("level-select").innerHTML = matches
    .map((l) => `<option value="${l.key}"${l.key === current ? " selected" : ""}>${l.key} · ${l.difficulty} · ${l.width}x${l.height}</option>`)
    .join("");
  return matches.length;
}

function describeLevel(level) {
  const retries = level.poolSize > 0 ? `${level.poolSize} eşit zorlukta seed` : "seed havuzu yok";
  const board = level.startBoard === "fixed" ? "sabit tahta" : "değişken tahta";
  return `${level.difficulty} · ${level.width}x${level.height} · ${level.colors} renk · ${level.moves} hamle · hedef: ${level.goals.join(", ")} · ${board}, ${retries}`;
}

async function init() {
  setSpeed(1);
  try {
    const [levels, bots] = await Promise.all([api("/api/levels"), api("/api/bots")]);
    ui.levels = levels;
    renderLevelOptions("");
    $("bot-select").innerHTML = bots.map((b) => `<option ${b === "average" ? "selected" : ""}>${b}</option>`).join("");
    const updateInfo = () => {
      const level = ui.levels.find((l) => l.key === $("level-select").value);
      $("level-info").textContent = level ? describeLevel(level) : "";
    };
    $("level-select").onchange = () => { updateInfo(); newGame({ kind: "next" }); };
    updateInfo();
  } catch (error) {
    reportError(error);
    return;
  }

  $("new-game").onclick = () => {
    const typed = $("seed-input").value;
    newGame(typed === "" ? { kind: "next" } : { kind: "seed", playSeed: Number(typed) });
  };
  $("replay-seed").onclick = () => ui.session && newGame({ kind: "repeat" });
  $("start-board-select").onchange = () => newGame({ kind: "next" });
  $("level-filter").oninput = () => {
    const before = $("level-select").value;
    if (renderLevelOptions($("level-filter").value) > 0 && $("level-select").value !== before) {
      $("level-select").dispatchEvent(new Event("change"));
    }
  };
  $("assist-input").oninput = () => { $("assist-out").textContent = $("assist-input").value; };
  $("speed-select").onchange = () => setSpeed(Number($("speed-select").value));
  $("hint-btn").onclick = showHint;
  $("bot-step").onclick = botStep;
  $("autoplay-btn").onclick = toggleAutoplay;
  $("simulate-btn").onclick = simulate;
  $("solve-btn").onclick = solve;
  const board = $("board");
  board.addEventListener("pointerdown", onPointerDown);
  board.addEventListener("pointerup", onPointerUp);
  board.addEventListener("pointercancel", () => { pointerStart = null; });
  window.addEventListener("resize", layoutBoard);
  await newGame({ kind: "next" });
}

init();
