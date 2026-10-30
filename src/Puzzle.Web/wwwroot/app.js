// Puzzle Test Lab client. The server runs the real engine; this file is the lab around the game: it starts sessions,
// sends the moves, runs the bots and the solver, and shows the recorded attempts. The game itself — the board, its
// animation and the HUD — is drawn by game-view.js, which the public mode's player page shares.

import { make, svgElement, svgText } from "./dom.js";
import { createBoard, endTitle, installTileArt, reducedMotion, showHud } from "./game-view.js";

const $ = (id) => document.getElementById(id);
const REQUEST_TIMEOUT_MS = 120_000;
const TABS = ["recordings", "analysis", "log"];

const percent0 = new Intl.NumberFormat("tr-TR", { style: "percent", maximumFractionDigits: 0 });
const percent1 = new Intl.NumberFormat("tr-TR", { style: "percent", maximumFractionDigits: 1 });
const decimal1 = new Intl.NumberFormat("tr-TR", { maximumFractionDigits: 1 });

const ui = {
  session: null,
  state: null,
  levels: [],
  busy: false,
  autoplay: false,
  scope: "device",  // whose recordings the panel shows: "device" or "all"
  comparing: false,
  comparison: null, // the last comparison with the bots
  chartRow: null,   // the comparison row the chart shows; null for all levels together
  simulation: null, // the last simulation, redrawn when its chart's width changes
};

const formatRate = (rate) => (rate === null || rate === undefined ? "–" : percent0.format(rate));
const formatMoves = (moves) => (moves === null || moves === undefined ? "–" : decimal1.format(moves));

/**
 * The width a chart is drawn at: its box's own, so one SVG unit is one pixel and labels keep their size, within
 * bounds that keep the plot readable. A hidden box (another tab) has no width; the chart is redrawn when shown.
 */
const chartWidth = (box) => Math.round(Math.min(560, Math.max(260, box.clientWidth || 300)));

// ---------- per-viewer memory ----------
// Only conveniences live here (the open tab, the scope); storage may be blocked, and then nothing is remembered.
function recall(key) {
  try {
    return localStorage.getItem(key) ?? undefined;
  } catch (error) {
    if (!(error instanceof DOMException)) throw error;
    return undefined;
  }
}

function remember(key, value) {
  try {
    localStorage.setItem(key, value);
  } catch (error) {
    if (!(error instanceof DOMException)) throw error;
  }
}

// ---------- API ----------
// Anonymous id of this browser for the attempt records (docs/GAME_DESIGN.md §20): random, nothing personal. Where it
// is kept matters: only an id kept across page loads and tabs ("device") lets the server number every attempt as the
// device's next retry. With site data blocked it lasts a tab ("tab") or a page load ("page"), the numbering restarts
// with it, and the server records those attempts out of sequence, left out of the comparison with the bots.
const DEVICE = loadDevice();

function loadDevice() {
  const kept = keptId(() => localStorage);
  if (kept) return { id: kept, kept: "device" };
  const forTab = keptId(() => sessionStorage);
  if (forTab) return { id: forTab, kept: "tab" };
  return { id: randomId(), kept: "page" };
}

/** The id kept in the storage `open` returns, stored now if there was none; absent when the browser blocks that storage. */
function keptId(open) {
  const key = "puzzle.deviceId";
  try {
    const storage = open();
    const stored = storage.getItem(key);
    if (stored && /^[A-Za-z0-9_-]{8,64}$/.test(stored)) return stored;
    const fresh = randomId();
    storage.setItem(key, fresh);
    return fresh;
  } catch (error) {
    if (!(error instanceof DOMException)) throw error;
    return undefined;
  }
}

function randomId() {
  // getRandomValues also works on a plain-http LAN address, where crypto.randomUUID does not exist.
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  return Array.from(bytes, (b) => b.toString(16).padStart(2, "0")).join("");
}

/** A refusal or failure the lab server answered with; `status` is the HTTP status. */
class ApiError extends Error {
  constructor(status, message) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

async function api(path, body) {
  const headers = { "X-Device-Id": DEVICE.id };
  const init = body === undefined
    ? { headers }
    : { method: "POST", headers: { ...headers, "Content-Type": "application/json" }, body: JSON.stringify(body) };
  if (typeof AbortSignal.timeout === "function") init.signal = AbortSignal.timeout(REQUEST_TIMEOUT_MS);
  const response = await fetch(path, init);
  if (!response.ok) throw new ApiError(response.status, await errorMessage(response));
  return response.json();
}

/** The server's reason: the lab answers with a JSON string; anything else is shown as it came. */
async function errorMessage(response) {
  const text = await response.text();
  try {
    const parsed = JSON.parse(text);
    if (typeof parsed === "string") return parsed;
    if (parsed && typeof parsed.title === "string") return parsed.title;
  } catch (error) {
    if (!(error instanceof SyntaxError)) throw error;
  }
  return text || `${response.status} ${response.statusText}`;
}

let toastTimer = 0;

function reportError(error) {
  const toast = $("toast");
  toast.textContent = `Hata: ${error instanceof Error ? error.message : String(error)}`;
  toast.hidden = false;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => { toast.hidden = true; }, 6000);
}

// ---------- board and HUD ----------
const board = createBoard($("board"), {
  onMove: sendMove,
  mayPlay: () => !ui.busy && !ui.autoplay,
  space: boardSpace,
});

/** The room the board may take: the board area's width, and the window's height less the lab's own rows. */
function boardSpace() {
  const narrow = window.innerWidth <= 760;
  const reserved = narrow ? 200 : 270; // top bar, attempt bar, HUD and the action buttons
  return {
    width: Math.min(document.querySelector(".board-area").clientWidth - 12, 720),
    height: Math.max(240, window.innerHeight - reserved),
  };
}

function renderBoard(state) {
  ui.state = state;
  board.render(state);
  updateHud(state);
}

/** Makes the view match the server state exactly, without animation. */
function reconcile(state) {
  ui.state = state;
  board.reconcile(state);
  updateHud(state);
}

function updateHud(state) {
  showHud({ moves: $("moves"), movesLeft: $("moves-left"), goals: $("goals") }, state);
  showBanner(state);
}

function showBanner(state) {
  const banner = $("banner");
  if (state.status === "Playing") {
    banner.classList.add("hidden");
    banner.replaceChildren();
    return;
  }
  const won = state.status === "Won";
  const card = make("div", "banner-card");
  const next = make("button", "btn primary", "Tekrar dene");
  next.type = "button";
  next.title = "Yeni desen, aynı zorluk";
  next.onclick = () => newGame({ kind: "next" });
  const repeat = make("button", "btn", "Aynı seed");
  repeat.type = "button";
  repeat.onclick = () => newGame({ kind: "repeat" });
  const actions = make("div", "banner-actions");
  actions.append(next, repeat);
  card.append(
    make("div", `banner-title ${won ? "won" : "lost"}`, endTitle(state.status)),
    make("div", "banner-meta", `${state.movesUsed} hamle · ${describeAttempt(ui.session)}`),
    make("div", "banner-rec", recordingView(ui.session).summary),
    actions,
  );
  banner.replaceChildren(card);
  banner.classList.remove("hidden");
}

// ---------- attempt and recording ----------
function describeAttempt(session) {
  const board = session.startBoard === "fixed" ? "tahta sabit" : "tahta her denemede farklı";
  const source = session.attempt === undefined || session.attempt === null
    ? "elle seçilen seed"
    : `deneme #${session.attempt + 1}${session.fromPool ? ` (eşit zorluk havuzu, ${session.level.poolSize} seed)` : " (havuz yok: zorluk değişebilir)"}`;
  const seeds = session.boardSeed === session.playSeed ? `seed ${session.playSeed}` : `tahta ${session.boardSeed} / oyun ${session.playSeed}`;
  return `${source} · ${board} · ${seeds}`;
}

function renderAttemptInfo(session) {
  const handPicked = session.attempt === undefined || session.attempt === null;
  const pool = session.fromPool ? `havuz · ${session.level.poolSize} seed` : "havuz yok";
  const seeds = session.boardSeed === session.playSeed ? `seed ${session.playSeed}` : `tahta ${session.boardSeed} / oyun ${session.playSeed}`;
  const parts = [
    make("span", "level-key", session.level.key),
    make("span", "attempt-num", handPicked ? "elle seçilen seed" : `deneme #${session.attempt + 1}`),
    make("span", "meta", `${handPicked ? "" : `${pool} · `}${session.startBoard === "fixed" ? "sabit tahta" : "değişken tahta"}`),
    make("span", "meta mono", seeds),
  ];
  if (session.assist > 0) parts.push(make("span", "meta", `DDA %${session.assist}`));
  const box = $("attempt-info");
  box.replaceChildren(...parts);
  box.title = describeAttempt(session);
}

/**
 * How the chip and the end banner describe the attempt's record: whether it is written (the server's `record`:
 * open, waiting, written or lost), and whether it will stand beside the bots (its `standing`).
 */
function recordingView(session) {
  const { standing, record } = session.recording;
  const counts = standingView(session, standing, session.recording.sequenceBreak);
  switch (record) {
    case "waiting":
      return {
        tone: "warn",
        icon: "!",
        label: "Yazılamadı · tekrar denenecek",
        summary: "Kayıt dosyası yazılamadı; sunucu tekrar deneyecek.",
        note: `Deneme bitti ama kayıt dosyasına yazılamadı (dosya başka bir programda açık ya da disk dolu olabilir). Kayıt sunucuda bekliyor ve dakikada bir yeniden denenir. Yazılınca: ${counts.written.summary}`,
      };
    case "lost":
      return {
        tone: "bad",
        icon: "✕",
        label: "Kaydedilemedi",
        summary: "Bu denemenin kaydı yazılamadı.",
        note: "Bu denemenin kaydı yazılamadı ve artık yazılmayacak; nedeni laboratuvar sunucusunun günlüğünde.",
      };
    case "written":
      return { ...counts.written, note: counts.note };
    default:
      return { ...counts.open, note: counts.note };
  }
}

/**
 * How the attempt stands in the comparison with the bots: the chip and banner while it is `open` and once it is
 * `written`, and the `note` that explains the standing.
 */
function standingView(session, standing, sequenceBreak) {
  switch (standing) {
    case "comparable":
      return {
        open: { tone: "accent", icon: "●", label: "Kaydediliyor", summary: "Kaydedilecek · botlarla karşılaştırmaya girer." },
        written: { tone: "good", icon: "✓", label: "Kaydedildi", summary: "Kaydedildi · botlarla karşılaştırmaya girer." },
        note: "Yardımsız ve sıradaki deneme: ilk hamleden sonra kaydedilir, bitince ya da yenisine geçince yazılır ve botlarla karşılaştırılır.",
      };
    case "assisted": {
      const help = session.assist > 0 ? `DDA yardımı %${session.assist} ile oynanıyor.` : "İpucu, çözücü ya da bot hamlesi kullanıldı.";
      const label = "Yardımlı · karşılaştırma dışı";
      return {
        open: { tone: "warn", icon: "◐", label, summary: "Yardımlı · kaydedilecek, botlarla karşılaştırılmaz." },
        written: { tone: "warn", icon: "◐", label, summary: "Yardımlı · kaydedildi, botlarla karşılaştırılmaz." },
        note: `${help} Deneme kaydedilir ama botlarla karşılaştırmaya girmez.`,
      };
    }
    case "chosenSeeds": {
      const { why, reason } = sequenceBreakView(session, sequenceBreak);
      const label = `Sayılmaz · ${why}`;
      return {
        open: { tone: "muted", icon: "○", label, summary: `Sayılmaz (${why}) · tekrar oynatmak için günlüğe yazılacak.` },
        written: { tone: "muted", icon: "○", label, summary: `Sayılmaz (${why}) · tekrar oynatmak için günlüğe yazıldı.` },
        note: `${reason} Deneme tekrar oynatılabilsin diye günlüğe yazılır ama botlarla karşılaştırmaya girmez.`,
      };
    }
    default: {
      const unknown = { tone: "muted", icon: "?", label: standing, summary: standing };
      return { open: unknown, written: unknown, note: "" };
    }
  }
}

/** Why an attempt is outside its device's sequence of retries: a short `why` for the chip, a sentence of `reason`. */
function sequenceBreakView(session, sequenceBreak) {
  switch (sequenceBreak) {
    case "startBoardOverride":
      return { why: "tahta ayarı farklı", reason: "Başlangıç tahtası bölüm ayarından farklı: bölümün kendisi bu oyunu vermez." };
    case "deviceNotKept":
      return { why: "cihaz hatırlanmıyor", reason: "Tarayıcı bu cihazın kimliğini saklayamıyor (site verisi engelli): deneme sırası her açılışta baştan başlar, oyun tekrar gelebilir." };
    case "poolReplay":
      return { why: "havuz tekrarı", reason: `Bölümün ${session.level.poolSize} seed'lik havuzu bitti: bu deneme daha önce oynanmış bir oyunu tekrarlar.` };
    default: {
      const handPicked = session.attempt === undefined || session.attempt === null;
      return handPicked
        ? { why: "elle seçilen seed", reason: "Seed elle seçildi: aynı oyun daha önce oynanmış olabilir." }
        : { why: "elle seçilen deneme", reason: "Deneme numarası elle seçildi: aynı oyun daha önce oynanmış olabilir." };
    }
  }
}

function renderRecordingChip() {
  const chip = $("recording-chip");
  if (!ui.session) {
    chip.hidden = true;
    return;
  }
  const view = recordingView(ui.session);
  chip.hidden = false;
  chip.dataset.tone = view.tone;
  chip.replaceChildren(make("span", "rec-icon", view.icon), make("span", "", view.label));
  chip.title = view.note;
  $("recording-note").textContent = view.note;
  const bannerLine = document.querySelector("#banner .banner-rec"); // the end banner says the same, should the record move on
  if (bannerLine) bannerLine.textContent = view.summary;
}

function toggleRecordingNote() {
  const chip = $("recording-chip");
  const open = chip.getAttribute("aria-expanded") !== "true";
  chip.setAttribute("aria-expanded", String(open));
  $("recording-note").hidden = !open;
}

// The server tries a waiting record again once a minute: ask again a little after that, so the chip catches up.
const RECHECK_WAITING_MS = 65_000;
let recheckTimer = 0;

function setRecording(recording) {
  if (!ui.session || !recording) return;
  ui.session.recording = recording;
  renderRecordingChip();
  clearTimeout(recheckTimer);
  if (recording.record === "waiting") {
    recheckTimer = setTimeout(() => refreshRecording().catch(reportError), RECHECK_WAITING_MS);
  }
}

/** A hint, the solver or a waiting write changes what the chip says; the server decides, so ask it. */
async function refreshRecording() {
  if (!ui.session) return;
  const asked = ui.session.sessionId;
  const session = await api(`/api/sessions/${asked}`);
  if (ui.session.sessionId === asked) setRecording(session.recording); // the player may have moved on meanwhile
}

// ---------- moves ----------
async function sendMove(move) {
  if (ui.busy || !ui.session || ui.state.status !== "Playing") return null;
  ui.busy = true;
  board.clearSelection();
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
  setRecording(result.recording);
  if (!result.accepted) {
    await board.showRefused(result.move);
    return;
  }
  await board.playEvents(result.events);
  reconcile(result.state);
  logMove(result);
  if (result.state.status !== "Playing") refreshRecordings();
}

function describeMove(move) {
  return move.kind === "tap" ? `dokun (${move.from.x},${move.from.y})` : `(${move.from.x},${move.from.y})→(${move.to.x},${move.to.y})`;
}

function logMove(result) {
  const s = result.stats;
  const text = `${describeMove(result.move)} · ${s.gemsCleared} taş, +${s.specialsCreated} özel, ${s.specialsActivated} patlama, ${s.cascades} kaskad${s.shuffles ? ", karıştırıldı" : ""}`;
  $("last-move").textContent = text;
  const log = $("event-log");
  log.prepend(make("li", "", `#${result.state.movesUsed} ${text}`));
  while (log.children.length > 60) log.lastChild.remove();
}

// ---------- game flow ----------
/** mode: {kind: "next"} next retry · {kind: "repeat"} exact same seeds · {kind: "seed", playSeed} explicit seed */
async function newGame(mode) {
  const levelKey = $("level-select").value;
  const assist = Number($("assist-input").value);
  const body = { levelKey, assist, startBoard: $("start-board-select").value };
  if (ui.session) body.replaces = ui.session.sessionId; // the server records the attempt being left
  if (DEVICE.kept !== "device") body.transientDeviceId = true;
  if (mode.kind === "repeat" && ui.session) {
    body.boardSeed = ui.session.boardSeed;
    body.playSeed = ui.session.playSeed;
  } else if (mode.kind === "seed") {
    body.playSeed = mode.playSeed;
  } // otherwise the server numbers this device's next retry, so a reload or a second tab never replays one
  try {
    stopAutoplay();
    const session = await api("/api/sessions", body);
    ui.session = session;
    renderAttemptInfo(session);
    renderRecordingChip();
    $("event-log").replaceChildren();
    $("last-move").textContent = "—";
    $("solve-result").textContent = "Şu anki konumdan, gelecek taşları bilerek en kısa kazanma yolunu arar. Henüz çalıştırılmadı.";
    renderBoard(session.state);
    refreshRecordings();
  } catch (error) {
    reportError(error);
  }
}

async function botStep() {
  if (ui.busy || !ui.session || ui.state.status !== "Playing") return false;
  ui.busy = true;
  board.clearSelection();
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
  const button = $("autoplay-btn");
  button.textContent = "Durdur ■";
  button.classList.add("is-on");
  while (ui.autoplay && await botStep()) {
    await board.wait(250);
  }
  stopAutoplay();
}

function stopAutoplay() {
  ui.autoplay = false;
  const button = $("autoplay-btn");
  button.textContent = "Bot oynasın ▶";
  button.classList.remove("is-on");
}

async function showHint() {
  if (!ui.session || ui.state.status !== "Playing") return;
  try {
    const move = await api(`/api/sessions/${ui.session.sessionId}/hint?bot=expert`);
    // A ring marks the hinted tiles; it pulses with motion, and without it stays a little longer instead.
    board.flashTiles([move.from, move.to], "hint", reducedMotion.matches ? 3000 : 1900);
    await refreshRecording();
  } catch (error) {
    reportError(error);
  }
}

async function solve() {
  if (!ui.session) return;
  const box = $("solve-result");
  box.classList.remove("muted");
  box.textContent = "Aranıyor…";
  const counted = ui.state.status === "Playing"
    ? "Çözücüyü açmak denemeyi yardımlı yapar."
    : "Deneme bittiği için bu bakış yardım sayılmaz.";
  try {
    const result = await api(`/api/sessions/${ui.session.sessionId}/solve`, { beam: Number($("beam-select").value) });
    const summary = make("p", "", result.solved
      ? `Gelecek taşları bilen oyuncu kalan ${result.movesLeft} hamlenin ${result.movesNeeded} tanesiyle kazanıyor.`
      : `Kalan ${result.movesLeft} hamleyle kazanan bir yol bulunamadı.`);
    const meta = make("p", "small muted", `${result.positionsExplored} pozisyon, ${result.elapsedMs} ms. Not: çözücü yukarıdan düşecek taşları bilir; gerçek oyuncu bilemez. ${counted}`);
    box.replaceChildren(summary, meta);
    if (result.solved) {
      const play = make("button", "btn", "Çözümü oynat");
      play.type = "button";
      play.onclick = () => playPath(result.path);
      box.appendChild(play);
    }
    await refreshRecording();
  } catch (error) {
    box.textContent = `Hata: ${error.message}`;
  }
}

async function playPath(path) {
  for (const move of path) {
    const result = await sendMove(move);
    if (!result || !result.accepted) break;
    await board.wait(150);
  }
}

// ---------- simulation ----------
async function simulate() {
  const box = $("sim-result");
  box.classList.remove("muted");
  box.textContent = "Simüle ediliyor…";
  try {
    ui.simulation = await api("/api/simulate", {
      levelKey: $("level-select").value,
      bot: $("bot-select").value,
      runs: Number($("runs-select").value),
    });
    renderSimulation();
  } catch (error) {
    ui.simulation = null;
    box.textContent = `Hata: ${error.message}`;
  }
}

function renderSimulation() {
  const r = ui.simulation;
  if (!r) return;
  const box = $("sim-result");
  const moves = (v) => v ?? ">150";
  const rows = [
    ["Bot / oyun", `${r.bot} · ${r.runs}`],
    [`Limit (${r.moveLimit}) ile kazanma`, percent1.format(r.winAtLimit)],
    ["Near-miss (≥%80 hedef)", percent1.format(r.nearMissAtLimit)],
    ["Gereken hamle p10 / p50 / p90", `${moves(r.movesNeeded.p10)} / ${moves(r.movesNeeded.p50)} / ${moves(r.movesNeeded.p90)}`],
    ["Özel taş / oyun", decimal1.format(r.specialsPerRun)],
    ["Kaskad / hamle", r.cascadesPerMove.toFixed(2)],
    ["Karıştırma / oyun", r.averageShuffles.toFixed(2)],
    ["Süre", `${r.elapsedMs} ms`],
  ];
  const table = make("table", "stats");
  for (const [label, value] of rows) {
    const tr = table.insertRow();
    tr.insertCell().textContent = label;
    tr.insertCell().textContent = value;
  }
  box.replaceChildren(table, winRateChart(r.curve, r.moveLimit, chartWidth(box)));
}

/** Single-series line: win rate for every move limit, with the level's own limit marked. */
function winRateChart(curve, limit, width) {
  const W = width, H = Math.round(Math.min(220, Math.max(160, W * 0.55))), L = 34, R = 8, T = 18, B = 26;
  const maxMoves = curve[curve.length - 1].moves;
  const sx = (m) => L + ((m - 1) / Math.max(1, maxMoves - 1)) * (W - L - R);
  const sy = (v) => T + (1 - v) * (H - T - B);
  const svg = svgElement("svg", { viewBox: `0 0 ${W} ${H}`, width: W, height: H, class: "chart", role: "img", "aria-label": "Hamle limitine göre kazanma oranı" });
  svg.appendChild(svgText("Hamle limitine göre kazanma oranı", { x: L, y: 11 }));
  for (const v of [0, 0.5, 1]) {
    svg.appendChild(svgElement("line", { class: "grid", x1: L, x2: W - R, y1: sy(v), y2: sy(v) }));
    svg.appendChild(svgText(percent0.format(v), { x: L - 4, y: sy(v) + 4, "text-anchor": "end" }));
  }
  for (const m of [1, Math.round(maxMoves / 2), maxMoves]) {
    svg.appendChild(svgText(String(m), { x: sx(m), y: H - 8, "text-anchor": "middle" }));
  }
  if (limit <= maxMoves) {
    svg.appendChild(svgElement("line", { class: "limit", x1: sx(limit), x2: sx(limit), y1: T, y2: H - B }));
    svg.appendChild(svgText(`limit ${limit}`, { x: sx(limit) + 4, y: H - B - 6 })); // low, where the rising curve rarely is
  }
  const d = curve.map((p, i) => `${i ? "L" : "M"}${sx(p.moves).toFixed(1)},${sy(p.winRate).toFixed(1)}`).join("");
  svg.appendChild(svgElement("path", { class: "line", d }));
  const cross = svgElement("line", { class: "cross", y1: T, y2: H - B, visibility: "hidden" });
  const dot = svgElement("circle", { class: "dot", r: 4, visibility: "hidden" });
  const hit = svgElement("rect", { x: L, y: T, width: W - L - R, height: H - T - B, fill: "transparent" });
  svg.append(cross, dot, hit);
  const tip = make("div", "chart-tip", "Grafiğin üzerine gel: her limit için kazanma oranı");
  const wrap = make("div", "chart-box");
  wrap.append(svg, tip);
  hit.addEventListener("pointermove", (event) => {
    const rect = svg.getBoundingClientRect();
    const x = ((event.clientX - rect.left) / rect.width) * W;
    const m = Math.min(maxMoves, Math.max(1, Math.round(1 + ((x - L) / (W - L - R)) * (maxMoves - 1))));
    const point = curve[m - 1];
    cross.setAttribute("x1", sx(m)); cross.setAttribute("x2", sx(m)); cross.setAttribute("visibility", "visible");
    dot.setAttribute("cx", sx(m)); dot.setAttribute("cy", sy(point.winRate)); dot.setAttribute("visibility", "visible");
    tip.textContent = `${m} hamle limiti → ${percent1.format(point.winRate)} kazanma`;
  });
  hit.addEventListener("pointerleave", () => {
    cross.setAttribute("visibility", "hidden");
    dot.setAttribute("visibility", "hidden");
  });
  return wrap;
}

// ---------- recordings panel ----------
let recordingsTicket = 0; // a newer request (another scope, another refresh) makes older answers stale

async function refreshRecordings() {
  if ($("tab-recordings").hidden) return;
  const ticket = ++recordingsTicket;
  const body = $("recordings-body");
  body.classList.add("is-stale");
  try {
    const data = await api(`/api/recordings?scope=${ui.scope}`);
    if (ticket === recordingsTicket) renderRecordings(data);
  } catch (error) {
    if (ticket === recordingsTicket) reportError(error);
  } finally {
    if (ticket === recordingsTicket) body.classList.remove("is-stale");
  }
}

function setScope(scope) {
  ui.scope = scope;
  remember("puzzle.recordingsScope", scope);
  for (const button of document.querySelectorAll(".segmented [data-scope]")) {
    button.setAttribute("aria-pressed", String(button.dataset.scope === scope));
  }
  ui.comparison = null;
  ui.chartRow = null;
  $("compare-result").replaceChildren();
  refreshRecordings();
}

/** How the panel names whoever played the scope's attempts, in the forms Turkish needs. */
const PEOPLE = {
  device: { title: "Sen", inline: "sen", dative: "Sana" },
  all: { title: "İnsanlar", inline: "insanlar", dative: "İnsanlara" },
};
const people = () => PEOPLE[ui.scope];

function kpi(label, value, sub) {
  const tile = make("div", "kpi");
  tile.append(make("div", "kpi-label", label), make("div", "kpi-value", value));
  if (sub) tile.appendChild(make("div", "kpi-sub", sub));
  return tile;
}

function renderRecordings(data) {
  const body = $("recordings-body");
  if (data.records === 0) {
    body.replaceChildren(make("p", "empty", ui.scope === "device"
      ? "Bu cihazın kaydı yok. Bir bölümde en az bir hamle oyna: deneme bitince ya da yenisine geçince kaydedilir."
      : "Henüz hiç kayıt yok."), ...playersSection(data.players));
    return;
  }
  const overall = data.overall;
  const counted = overall.finished + overall.abandoned;
  const leftOut = data.assisted + data.chosenSeeds;
  const kpis = make("div", "kpis");
  kpis.append(
    kpi("Sayılan deneme", String(counted), ui.scope === "all" ? `${overall.devices} cihaz` : `${data.levelCount} bölüm`),
    kpi("Limitte kazanma", formatRate(overall.winRate), overall.finished === 0 ? "biten deneme yok" : `alt sınır ${formatRate(overall.winRateLowerBound)}`),
    kpi("Yarıda bırakılan", String(overall.abandoned), "limit dolmadan"),
    kpi("Karşılaştırma dışı", String(leftOut), `${data.assisted} yardımlı · ${data.chosenSeeds} elle seed`),
  );
  const parts = [kpis, recordingsTable(data.levels)];
  const notes = ["Sayılan: yardımsız, sıradaki denemeler. Kaz.: limitte kazanılan; % biten denemelere göre. Dışı: yardımlı (ipucu, çözücü, bot hamlesi, DDA) ya da elle seçilen seed; kaydedilir ama botlarla karşılaştırılmaz. Satıra dokun: bölümü aç."];
  if (data.levelCount > data.levels.length) notes.push(`Son ${data.levels.length} bölüm gösteriliyor (toplam ${data.levelCount}).`);
  if (data.unreadableLines > 0) notes.push(`Kayıt dosyasında ${data.unreadableLines} satır okunamadı.`);
  parts.push(make("p", "table-note", notes.join(" ")), ...playersSection(data.players));
  body.replaceChildren(...parts);
}

const shortDateTime = new Intl.DateTimeFormat("tr-TR", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });

/**
 * The players of a public server by username, the one who played last first. Only a public server sends them, to its
 * owner and for everyone's scope; anywhere else there is nothing to show.
 */
function playersSection(players) {
  if (!players) return [];
  if (players.length === 0) return [make("h3", "", "Oyuncular"), make("p", "empty", "Henüz kayıtlı oyuncu yok.")];
  const table = make("table", "data-table");
  const head = table.createTHead().insertRow();
  for (const [label, title] of [["Oyuncu", ""], ["Geçti", "Kazandığı bölüm sayısı"], ["Say.", "Sayılan deneme"], ["Kaz.", "Limitte kazanılan"], ["%", "Limitte kazanma oranı (biten denemeler)"], ["Yarım", "Limit dolmadan bırakılan"], ["Son oyun", "Son denemenin bittiği an"]]) {
    const th = make("th", "", label);
    if (title) th.title = title;
    head.appendChild(th);
  }
  const body = table.createTBody();
  for (const player of players) {
    const row = body.insertRow();
    row.insertCell().append(make("span", "level-name", player.username), make("span", "level-sub", `${player.records} kayıt`));
    row.insertCell().textContent = String(player.levelsWon);
    row.insertCell().textContent = String(player.play.finished + player.play.abandoned);
    row.insertCell().textContent = String(player.play.wins);
    row.insertCell().textContent = formatRate(player.play.winRate);
    row.insertCell().textContent = String(player.play.abandoned);
    const last = row.insertCell();
    last.textContent = player.lastPlayedUtc ? shortDateTime.format(new Date(player.lastPlayedUtc)) : "–";
    if (!player.lastPlayedUtc) last.className = "dim";
  }
  const wrap = make("div", "table-wrap");
  wrap.appendChild(table);
  return [make("h3", "", "Oyuncular"), wrap, make("p", "table-note", "Kullanıcı adları yalnızca burada görünür; kayıtlarda oyuncunun rastgele kimliği durur.")];
}

function recordingsTable(levels) {
  const table = make("table", "data-table");
  const head = table.createTHead().insertRow();
  for (const [label, title] of [["Bölüm", ""], ["Say.", "Sayılan deneme"], ["Kaz.", "Limitte kazanılan"], ["%", "Limitte kazanma oranı (biten denemeler)"], ["Med.", "Kazanılanların medyan hamlesi"], ["Yarım", "Limit dolmadan bırakılan"], ["Dışı", "Karşılaştırma dışı: yardımlı ya da elle seçilen seed"]]) {
    const th = make("th", "", label);
    if (title) th.title = title;
    head.appendChild(th);
  }
  const body = table.createTBody();
  for (const level of levels) {
    const row = body.insertRow();
    const name = row.insertCell();
    name.append(make("span", "level-name", level.levelKey ?? `Bölüm ${level.levelId}`), make("span", "level-sub", `#${level.levelId} · ${level.moveLimit} hamle`));
    const counted = level.play.finished + level.play.abandoned;
    row.insertCell().textContent = String(counted);
    row.insertCell().textContent = String(level.play.wins);
    row.insertCell().textContent = formatRate(level.play.winRate);
    row.insertCell().textContent = formatMoves(level.medianWinMoves);
    row.insertCell().textContent = String(level.play.abandoned);
    const leftOut = level.assisted + level.chosenSeeds;
    const out = row.insertCell();
    out.textContent = String(leftOut);
    out.className = leftOut > 0 ? "left-out" : "dim";
    out.title = `${level.assisted} yardımlı, ${level.chosenSeeds} elle seçilen seed`;
    if (level.levelKey && ui.levels.some((l) => l.key === level.levelKey)) {
      row.className = "clickable";
      row.tabIndex = 0;
      row.title = "Bu bölümü aç";
      row.onclick = () => openLevel(level.levelKey);
      row.onkeydown = (event) => { if (event.key === "Enter" || event.key === " ") { event.preventDefault(); openLevel(level.levelKey); } };
    }
  }
  const wrap = make("div", "table-wrap");
  wrap.appendChild(table);
  return wrap;
}

function openLevel(key) {
  $("level-filter").value = "";
  renderLevelOptions("");
  $("level-select").value = key;
  $("level-select").dispatchEvent(new Event("change"));
  if (window.innerWidth <= 760) {
    document.querySelector(".board-area").scrollIntoView({ behavior: reducedMotion.matches ? "auto" : "smooth", block: "start" });
  }
}

// ---------- comparison with the bots ----------
async function compareWithBots() {
  if (ui.comparing) return;
  ui.comparing = true;
  const button = $("compare-btn");
  const status = $("compare-status");
  const result = $("compare-result");
  const started = performance.now();
  const tick = () => { status.textContent = `Botlar oynuyor… ${Math.floor((performance.now() - started) / 1000)} sn`; };
  button.disabled = true;
  button.classList.add("is-busy");
  status.hidden = false;
  tick();
  const timer = setInterval(tick, 1000);
  result.classList.add("is-stale");
  try {
    ui.comparison = await api("/api/recordings/compare", { scope: ui.scope, runs: Number($("compare-runs").value) });
    ui.chartRow = null;
    renderComparison();
  } catch (error) {
    reportError(error instanceof ApiError && error.status === 429 ? new Error("Başka bir karşılaştırma sürüyor; o bitince tekrar dene.") : error);
  } finally {
    clearInterval(timer);
    status.hidden = true;
    button.disabled = false;
    button.classList.remove("is-busy");
    result.classList.remove("is-stale");
    ui.comparing = false;
  }
}

function levelLabel(row) {
  return row.levelKey ?? `Bölüm ${row.levelId}`;
}

function renderComparison() {
  const c = ui.comparison;
  const box = $("compare-result");
  if (!c) {
    box.replaceChildren();
    return;
  }
  const tally = c.records;
  const leftOut = `Dışarıda: ${tally.assisted} yardımlı, ${tally.chosenSeeds} elle seed${tally.unknownLevel ? `, ${tally.unknownLevel} artık olmayan bölüm` : ""}.`;
  if (tally.compared === 0) {
    box.replaceChildren(make("p", "empty", `Karşılaştırılacak sayılan deneme yok: yardımsız, sıradaki denemelerle oyna. ${leftOut}`));
    return;
  }
  const row = ui.chartRow ?? c.overall;
  const title = make("div", "compare-title");
  title.append(make("strong", "", row === c.overall ? "Tüm bölümler" : levelLabel(row)), make("span", "", `${row.play.finished + row.play.abandoned} deneme`));
  const parts = [title, closestCallout(row), botChart(row, chartWidth(box)), comparisonTable(c)];
  const meta = [`Her deneme için ${c.botRuns} oyun × ${c.bots.length} bot profili · ${decimal1.format(c.elapsedMs / 1000)} sn.`];
  if (c.comparable > tally.compared) meta.push(`En son ${tally.compared} sayılan deneme karşılaştırıldı (toplam ${c.comparable}).`);
  meta.push(leftOut);
  parts.push(make("p", "compare-meta", meta.join(" ")));
  box.replaceChildren(...parts);
}

/**
 * The comparisons a row shows, each a human rate set beside the bots on the games it covers: the people's rate on the
 * finished attempts beside the bots on those games, then — when attempts were abandoned — the lower bound beside the
 * bots on every attempt's game. The first one is the main one.
 */
function comparisons(row) {
  const shown = [];
  if (row.play.finished > 0) {
    shown.push({
      name: people().inline,
      legend: people().inline,
      human: row.play.winRate,
      closest: row.closestBot,
      rateOf: (bot) => bot.winRateOnFinished,
      games: "biten denemelerin oyunlarında",
      short: "biten denemelerde",
    });
  }
  if (row.play.abandoned > 0) {
    shown.push({
      name: "alt sınır",
      legend: "alt sınır: yarıda bırakılanlar kayıp",
      human: row.play.winRateLowerBound,
      closest: row.closestBotAtLowerBound,
      rateOf: (bot) => bot.winRateOnAll,
      games: "tüm denemelerin oyunlarında",
      short: "tüm denemelerde",
    });
  }
  return shown;
}

const strongRate = (rate) => make("strong", "", formatRate(rate));

/** The win rate of `comparison`'s closest bot, on the games that comparison covers. */
function closestRate(row, comparison) {
  const bot = row.bots.find((b) => b.bot === comparison.closest);
  return bot ? comparison.rateOf(bot) : undefined;
}

/** The comparison's answer, first: the bot whose win rate is nearest the people's, and the two rates side by side. */
function closestCallout(row) {
  const who = people();
  const box = make("div", "closest");
  const [main, alternative] = comparisons(row);
  if (!main || !main.closest) {
    box.classList.add("none");
    box.appendChild(make("div", "closest-sub", "Henüz biten ya da yarıda bırakılan deneme yok."));
    return box;
  }
  const text = make("div", "closest-text");
  text.append(make("div", "closest-label", `${who.dative} en yakın bot`), make("div", "closest-bot", main.closest));
  const sub = make("div", "closest-sub");
  if (row.play.finished > 0) {
    sub.append(`${who.inline} `, strongRate(main.human), ` · ${main.closest} `, strongRate(closestRate(row, main)), ` (limitte kazanma, ${main.games})`);
  } else {
    sub.append("Biten deneme yok, yarıda bırakılanlar kayıp sayıldı: alt sınır ", strongRate(main.human), ` · ${main.closest} `, strongRate(closestRate(row, main)), ` (${main.games})`);
  }
  text.appendChild(sub);
  if (alternative && row.abandonsDecideClosest) {
    const alt = make("div", "closest-sub", "Yarıda bırakılanlar kayıp sayılırsa (");
    alt.append(strongRate(alternative.human), "): ", make("strong", "", alternative.closest), ` ${formatRate(closestRate(row, alternative))} (${alternative.games})`);
    text.appendChild(alt);
  }
  box.appendChild(text);
  return box;
}

/**
 * Bots as bars (weakest first) beside the people's rate as a line, each pair on the same games. Thick bars are the bots
 * on the finished attempts' games, beside the people's rate (a solid line); when attempts were abandoned, thin bars
 * are the bots on every attempt's game, beside the lower bound (a dashed line), and the closest bot of each is
 * emphasised. Drawn `width` pixels wide. Screen readers get each bot's rates from its labelled row.
 */
function botChart(row, width) {
  const shown = comparisons(row);
  const [main, alternative] = shown;
  const W = width, labelRow = 15, L = 64, R = 40, axis = 22;
  const top = 14 + labelRow * shown.length;
  const thick = alternative ? 12 : 14, thin = 5, gap = 3;
  const band = alternative ? 34 : 30;
  const bots = row.bots;
  const plotBottom = top + band * bots.length;
  const H = plotBottom + axis;
  const sx = (v) => L + v * (W - L - R);
  const svg = svgElement("svg", {
    viewBox: `0 0 ${W} ${H}`, width: W, height: H, class: "chart bot-chart", role: "group",
    "aria-label": `Botların kazanma oranları; ${shown.map((c) => `${c.name} ${formatRate(c.human)}`).join(", ")}`,
  });
  const decorative = { "aria-hidden": "true" };
  for (const v of [0, 0.25, 0.5, 0.75, 1]) {
    svg.appendChild(svgElement("line", { class: "grid", x1: sx(v), x2: sx(v), y1: top - 6, y2: plotBottom }));
  }
  for (const v of [0, 0.5, 1]) {
    svg.appendChild(svgText(percent0.format(v), { ...decorative, x: sx(v), y: H - 6, "text-anchor": "middle" }));
  }
  // Each human rate is a line with its label above it; the labels have a row each and the lines start below the
  // rows, so two rates close together never run through each other's label.
  shown.forEach((comparison, i) => {
    const x = sx(comparison.human);
    svg.appendChild(svgElement("line", { class: comparison === alternative ? "human-line dashed" : "human-line", x1: x, x2: x, y1: top - 8, y2: plotBottom }));
    svg.appendChild(svgText(`${comparison.name} ${formatRate(comparison.human)}`, {
      ...decorative, class: "human-label", x: Math.min(Math.max(x, L + 30), W - 30), y: labelRow * (i + 1), "text-anchor": "middle",
    }));
  });
  const box = make("div", "chart-box");
  const tooltip = make("div", "tooltip");
  tooltip.hidden = true;
  bots.forEach((bot, i) => {
    const rowTop = top + i * band;
    const y = rowTop + (band - (alternative ? thick + gap + thin : thick)) / 2;
    const group = svgElement("g", { class: "chart-row" });
    group.appendChild(svgText(bot.bot, { ...decorative, class: bot.bot === main.closest ? "bot-name strong" : "bot-name", x: L - 8, y: y + thick / 2 + 4, "text-anchor": "end" }));
    appendBar(group, sx, main.rateOf(bot), y, thick, emphasisFor(bot, main, "bar"), "value");
    if (alternative) appendBar(group, sx, alternative.rateOf(bot), y + thick + gap, thin, emphasisFor(bot, alternative, "bar thin"), "value minor");
    const hit = svgElement("rect", { class: "hit", x: 0, y: rowTop, width: W, height: band, tabindex: 0, role: "img", "aria-label": botRatesLabel(bot, shown) });
    group.appendChild(hit);
    const show = () => {
      group.classList.add("active");
      tooltip.replaceChildren(make("div", "tip-title", bot.bot), ...shown.map((comparison) => tooltipLine(bot, comparison, alternative)));
      const plot = svg.getBoundingClientRect();
      const scale = plot.width / W; // the SVG shrinks when its box is narrower than W
      tooltip.style.left = `${sx(Math.min(main.rateOf(bot), 0.62)) * scale}px`;
      tooltip.style.top = `${plot.top - box.getBoundingClientRect().top + rowTop * scale}px`;
      tooltip.hidden = false;
    };
    const hide = () => { group.classList.remove("active"); tooltip.hidden = true; };
    hit.addEventListener("pointerenter", show);
    hit.addEventListener("focus", show);
    hit.addEventListener("pointerleave", hide);
    hit.addEventListener("blur", hide);
    svg.appendChild(group);
  });
  box.append(svg, tooltip, chartLegend(shown));
  return box;
}

/** The bar class for `bot` in `comparison`: emphasised when it is that comparison's closest bot. */
const emphasisFor = (bot, comparison, barClass) => (bot.bot === comparison.closest ? `${barClass} emph` : barClass);

/** A bar from the baseline to `rate`, with the rate written after it; a rate too small to draw keeps only its text. */
function appendBar(group, sx, rate, y, height, barClass, valueClass) {
  const width = sx(rate) - sx(0);
  if (width > 0.5) group.appendChild(svgElement("path", { class: barClass, d: roundedBar(sx(0), y, width, height, 4) }));
  group.appendChild(svgText(formatRate(rate), { "aria-hidden": "true", class: valueClass, x: sx(rate) + 6, y: y + height / 2 + 4 }));
}

/** What a screen reader says for one bot: its rate in each comparison, and which comparison it is closest in. */
function botRatesLabel(bot, shown) {
  const rates = shown.map((comparison) => `${comparison.games} ${percent1.format(comparison.rateOf(bot))}${bot.bot === comparison.closest ? `, ${comparison.name} için en yakın bot` : ""}`);
  return `${bot.bot}: ${rates.join("; ")}`;
}

/** One comparison in a bot's tooltip: the mark's key and the bot's rate on those games, then how far the human rate is. */
function tooltipLine(bot, comparison, alternative) {
  const rate = comparison.rateOf(bot);
  const line = make("div", "tip-line");
  const key = `key${comparison === alternative ? " thin" : ""}${bot.bot === comparison.closest ? " emph" : ""}`;
  const gap = `${comparison.name} ${formatRate(comparison.human)} · fark ${decimal1.format(Math.abs(rate - comparison.human) * 100)} puan`;
  line.append(make("span", key), make("strong", "", percent1.format(rate)), ` ${comparison.short}`, make("div", "tip-detail", gap));
  return line;
}

/** What each mark of the bot chart stands for; the swatches mirror the marks (bars as boxes, the human rates as lines). */
function chartLegend(shown) {
  const [main, alternative] = shown;
  const legend = make("div", "chart-legend");
  const item = (swatch, text) => {
    const entry = make("span", "legend-item");
    entry.append(make("span", `swatch ${swatch}`), text);
    return entry;
  };
  legend.append(item("bar emph", "en yakın bot"), item("bar", `diğer botlar, ${main.short}`));
  if (alternative) legend.append(item("bar thin", `ince: botlar ${alternative.short}`));
  legend.append(item("line", main.legend));
  if (alternative) legend.append(item("line dashed", alternative.legend));
  return legend;
}

/** A bar from the baseline with a 4px rounded data end and a square base. */
function roundedBar(x, y, width, height, radius) {
  const r = Math.min(radius, width / 2, height / 2);
  return `M${x},${y} H${x + width - r} A${r},${r} 0 0 1 ${x + width},${y + r} V${y + height - r} A${r},${r} 0 0 1 ${x + width - r},${y + height} H${x} Z`;
}

function comparisonTable(c) {
  const table = make("table", "data-table");
  const head = table.createTHead().insertRow();
  const who = people();
  for (const [label, title] of [["Bölüm", ""], [who.title, "Biten denemelerde limitte kazanma"], ["Alt", "Yarıda bırakılanlar kayıp sayılırsa"], ["En yakın", "Kazanma oranı en yakın bot"]]) {
    const th = make("th", "", label);
    if (title) th.title = title;
    head.appendChild(th);
  }
  const body = table.createTBody();
  for (const row of [c.overall, ...c.levels]) {
    const tr = body.insertRow();
    const isOverall = row === c.overall;
    const name = tr.insertCell();
    name.append(make("span", "level-name", isOverall ? "Tümü" : levelLabel(row)));
    name.appendChild(make("span", "level-sub", isOverall
      ? `${row.play.finished + row.play.abandoned} deneme`
      : `${row.moveLimit} hamle · ${row.play.finished + row.play.abandoned} deneme`));
    tr.insertCell().textContent = formatRate(row.play.winRate);
    tr.insertCell().textContent = formatRate(row.play.winRateLowerBound);
    tr.insertCell().textContent = row.abandonsDecideClosest && row.closestBot && row.closestBotAtLowerBound
      ? `${row.closestBot} / ${row.closestBotAtLowerBound}`
      : (row.closestBot ?? row.closestBotAtLowerBound ?? "–");
    const shown = (ui.chartRow ?? c.overall) === row;
    tr.className = `clickable${shown ? " selected" : ""}`;
    tr.tabIndex = 0;
    tr.title = "Grafikte göster";
    const showRow = () => { ui.chartRow = isOverall ? null : row; renderComparison(); };
    tr.onclick = showRow;
    tr.onkeydown = (event) => { if (event.key === "Enter" || event.key === " ") { event.preventDefault(); showRow(); } };
  }
  const wrap = make("div", "table-wrap");
  wrap.appendChild(table);
  return wrap;
}

// ---------- tabs ----------
function selectTab(name) {
  for (const tab of TABS) {
    const selected = tab === name;
    const button = $(`tab-btn-${tab}`);
    button.setAttribute("aria-selected", String(selected));
    button.tabIndex = selected ? 0 : -1;
    $(`tab-${tab}`).hidden = !selected;
  }
  remember("puzzle.tab", name);
  if (name === "recordings") refreshRecordings();
  redrawCharts(); // a chart drawn while its tab was hidden had no width to fit
}

// ---------- resizing ----------
let resizeFrame = 0;
let laidOutWidth = window.innerWidth;

/** Redraws the charts at their boxes' current widths. */
function redrawCharts() {
  if (ui.comparison && !$("tab-recordings").hidden) renderComparison();
  if (ui.simulation && !$("tab-analysis").hidden) renderSimulation();
}

function onResize() {
  cancelAnimationFrame(resizeFrame);
  resizeFrame = requestAnimationFrame(() => {
    board.fit();
    // A phone's toolbar showing or hiding changes only the height: the charts keep their width, and their focus.
    if (window.innerWidth !== laidOutWidth) {
      laidOutWidth = window.innerWidth;
      redrawCharts();
    }
  });
}

function onTabKey(event) {
  const step = { ArrowRight: 1, ArrowLeft: -1 }[event.key];
  if (!step) return;
  const current = TABS.findIndex((tab) => $(`tab-btn-${tab}`).getAttribute("aria-selected") === "true");
  const next = TABS[(current + step + TABS.length) % TABS.length];
  selectTab(next);
  $(`tab-btn-${next}`).focus();
}

// ---------- owner ----------
/** A public server serves the lab under /lab/, to its owner alone; in the lab mode the page is the site's root. */
const isOwnersLab = location.pathname.startsWith("/lab/");

/** Ends the owner's sign-in on this browser and goes back to the key form. */
async function signOutOwner() {
  try {
    const response = await fetch("/api/owner/sign-out", { method: "POST", headers: { "X-Device-Id": DEVICE.id } });
    if (!response.ok) throw new ApiError(response.status, await errorMessage(response));
    location.assign("/lab/sign-in");
  } catch (error) {
    reportError(error);
  }
}

// ---------- startup ----------
/** Lists the levels matching every word of the filter (number, key, tier or goal text). */
function renderLevelOptions(filter) {
  const words = filter.toLowerCase().split(/\s+/).filter(Boolean);
  const matches = ui.levels.filter((l) => {
    const text = `${l.key} ${l.id} ${l.difficulty} ${l.width}x${l.height} ${l.goals.join(" ")}`.toLowerCase();
    return words.every((w) => text.includes(w));
  });
  const select = $("level-select");
  const current = select.value;
  select.replaceChildren(...matches.map((l) => {
    const option = make("option", "", `${l.key} · ${l.difficulty} · ${l.width}x${l.height}`);
    option.value = l.key;
    option.selected = l.key === current;
    return option;
  }));
  return matches.length;
}

function describeLevel(level) {
  const retries = level.poolSize > 0 ? `${level.poolSize} eşit zorlukta seed` : "seed havuzu yok";
  const board = level.startBoard === "fixed" ? "sabit tahta" : "değişken tahta";
  return `${level.difficulty} · ${level.width}x${level.height} · ${level.colors} renk · ${level.moves} hamle · hedef: ${level.goals.join(", ")} · ${board}, ${retries}`;
}

/** Level count and this browser's device id; an id the browser cannot keep says so, since its attempts are not compared. */
function renderStatusLine(levelCount) {
  const line = $("status-line");
  const lifetime = { device: "", tab: " (yalnız bu sekme)", page: " (yalnız bu sayfa)" }[DEVICE.kept];
  line.textContent = `${levelCount} bölüm · cihaz ${DEVICE.id.slice(0, 6)}${lifetime}`;
  line.title = DEVICE.kept === "device"
    ? "Bu tarayıcının rastgele cihaz kimliği; kayıtlarda kişisel bilgi yok."
    : "Tarayıcı site verisini engelliyor: cihaz kimliği saklanamıyor, deneme sırası yeniden başlayabilir. Denemeler kaydedilir ama botlarla karşılaştırılmaz.";
}

async function init() {
  const tileArt = installTileArt();
  $("owner-sign-out").hidden = !isOwnersLab;
  $("owner-sign-out").onclick = signOutOwner;
  const storedScope = recall("puzzle.recordingsScope");
  if (storedScope === "device" || storedScope === "all") ui.scope = storedScope;
  for (const button of document.querySelectorAll(".segmented [data-scope]")) {
    button.setAttribute("aria-pressed", String(button.dataset.scope === ui.scope));
    button.onclick = () => setScope(button.dataset.scope);
  }
  const storedTab = recall("puzzle.tab");
  selectTab(TABS.includes(storedTab) ? storedTab : "recordings");
  try {
    const [levels, bots] = await Promise.all([api("/api/levels"), api("/api/bots"), tileArt]);
    ui.levels = levels;
    renderStatusLine(levels.length);
    renderLevelOptions("");
    $("bot-select").replaceChildren(...bots.map((b) => {
      const option = make("option", "", b);
      option.selected = b === "average";
      return option;
    }));
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
  $("speed-select").onchange = () => board.setSpeed(Number($("speed-select").value));
  $("hint-btn").onclick = showHint;
  $("bot-step").onclick = botStep;
  $("autoplay-btn").onclick = toggleAutoplay;
  $("simulate-btn").onclick = simulate;
  $("solve-btn").onclick = solve;
  $("recording-chip").onclick = toggleRecordingNote;
  $("recordings-refresh").onclick = refreshRecordings;
  $("compare-btn").onclick = compareWithBots;
  for (const tab of TABS) $(`tab-btn-${tab}`).onclick = () => selectTab(tab);
  document.querySelector(".tabs").addEventListener("keydown", onTabKey);
  window.addEventListener("resize", onResize);
  await newGame({ kind: "next" });
}

init();
