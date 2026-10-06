// The public mode's player page: sign in, play the level the server deals, see the result, go on. The server owns the
// game — which level, which attempt, every tile — and records it; this page only shows it and sends the moves.

import { make } from "./dom.js";
import { starAlcove } from "./alcove.js";
import { TileArtError } from "./game-view.js";
import { createFramedBoard } from "./framed-board.js";
import { showHud, showLevel } from "./hud.js";
import { installPieceArt } from "./piece-art.js";
import { resultPanel } from "./result-panel.js";

const $ = (id) => document.getElementById(id);
const REQUEST_TIMEOUT_MS = 20_000;
const TOAST_MS = 6000;
const BOARD_PX = { min: 220, max: 720 };
const VIEWS = { loading: "loading-view", signIn: "sign-in-view", game: "game-view", finished: "finished-view" };

/** What lies behind a result panel: still drawn, in the dark, but out of reach of a touch, the keyboard or a screen reader. */
const BEHIND_THE_PANEL = ["hud", "board", "help"];

/** The design's three typefaces, as Google Fonts serves them; gallery.css names the fallbacks for each. */
const DESIGN_FONTS = "https://fonts.googleapis.com/css2?family=Gabarito:wght@600;700;800;900&family=Paytone+One&family=Young+Serif&display=swap";

/** Refusals of a move that mean the game is not where this page thought: another tab, or a request sent twice, moved it on. */
const PAGE_IS_BEHIND = new Set(["attemptChanged", "noLiveAttempt", "attemptOver"]);
const SESSION_NOT_KEPT = "Giriş yapıldı ama tarayıcın oturumu saklamadı. Sayfayı https:// ile başlayan adresinden aç ve çerezlere izin ver.";

const page = {
  game: undefined,   // { progress, attempt } as the server last told it; absent while nobody is signed in
  busy: false,       // a request that changes the game, or the animation of its answer, is under way
  outOfSync: false,  // a request failed without saying what became of the game: ask the server before the next move
};

// ---------- API ----------
/** The server refused: `code` is its stable error code, `message` a Turkish sentence the page shows as it is. */
class Refusal extends Error {
  constructor(status, answer) {
    super(answer?.message ?? `Sunucu beklenmeyen bir yanıt verdi (${status}).`);
    this.name = "Refusal";
    this.status = status;
    this.code = answer?.error ?? "unknown";
    this.retryAfterSeconds = answer?.retryAfterSeconds;
  }
}

/** The server could not be reached, or did not answer in time: nothing is known of what the request did. */
class Unreachable extends Error {
  constructor(cause) {
    super("Sunucuya ulaşılamadı. Bağlantını kontrol edip tekrar dene.", { cause });
    this.name = "Unreachable";
  }
}

/** A request to the player API. The header marks it as this page's own; the server refuses changes without it. */
async function api(path, { method = "GET", body } = {}) {
  const init = { method, headers: { "X-Puzzle-Client": "player" }, signal: AbortSignal.timeout?.(REQUEST_TIMEOUT_MS) };
  if (body !== undefined) {
    init.headers["Content-Type"] = "application/json";
    init.body = JSON.stringify(body);
  }
  let response;
  let answer;
  try {
    response = await fetch(path, init);
    answer = await readJson(response);
  } catch (error) {
    if (!isConnectionFailure(error)) throw error;
    throw new Unreachable(error);
  }
  if (!response.ok) throw new Refusal(response.status, answer);
  return answer;
}

/** fetch fails with a TypeError when the connection is lost or never made, and with a timeout error when the answer takes too long. */
const isConnectionFailure = (error) => error instanceof TypeError || isTimeout(error);
const isTimeout = (error) => error instanceof DOMException && ["TimeoutError", "AbortError"].includes(error.name);

/** The JSON of an answer; absent when it has none (a sign-out) or is not JSON (an error page of the tunnel in between). */
async function readJson(response) {
  const text = await response.text();
  if (text === "") return undefined;
  try {
    return JSON.parse(text);
  } catch (error) {
    if (!(error instanceof SyntaxError)) throw error;
    return undefined;
  }
}

const currentGame = () => api("/api/player/game");

/** Asks for the next attempt, naming the one this page shows: the server never ends an attempt being played that the page did not see. */
const startAttempt = () => api("/api/player/game/attempts", { method: "POST", body: { leaves: page.game?.attempt?.id } });

/** Plays `move` on the attempt this page shows. The server takes it only while that is the live attempt, at that move. */
const playMove = (attempt, move) => api("/api/player/game/moves", {
  method: "POST",
  body: { attempt: attempt.id, movesUsed: attempt.state.movesUsed, ...move },
});

/** The game as the page opens it: the live attempt, or a new one when there is none and levels remain. */
async function openGame() {
  const game = await currentGame();
  const nothingToStart = game.attempt !== undefined || game.progress.finished;
  return nothingToStart ? game : startAttempt();
}

// ---------- messages ----------
/** A request that failed in a way the page expects; anything else it catches is a bug, and is thrown on. */
const isFailedRequest = (error) => error instanceof Refusal || error instanceof Unreachable || error instanceof TileArtError;
const isSignInRequired = (error) => error instanceof Refusal && error.code === "signInRequired";

/** What the player reads when a request failed. */
function failureText(error) {
  if (!isFailedRequest(error)) throw error;
  if (error instanceof TileArtError) return "Oyunun görselleri yüklenemedi. Sayfayı yenile.";
  if (error.code === "lockedOut" && error.retryAfterSeconds) {
    return `Çok fazla hatalı deneme. ${Math.ceil(error.retryAfterSeconds / 60)} dakika sonra tekrar dene.`;
  }
  if (error.code === "tooManyAttempts" && error.retryAfterSeconds) {
    return `Kısa sürede çok fazla yeni deneme başlattın. ${Math.ceil(error.retryAfterSeconds / 60)} dakika sonra tekrar dene.`;
  }
  return error.message;
}

let toastTimer = 0;

/** Tells the player something for a few seconds: a failure, or with the tone "note" a plain notice. */
function showToast(text, tone = "error") {
  const toast = $("toast");
  toast.textContent = text;
  toast.dataset.tone = tone;
  toast.hidden = false;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => { toast.hidden = true; }, TOAST_MS);
}

// ---------- views ----------
function showView(name) {
  for (const [view, id] of Object.entries(VIEWS)) $(id).hidden = view !== name;
  $("shelf").hidden = name === "loading" || name === "signIn";
  if (name !== "game") leaveGameOnShelf();
}

/** The shelf carries two things that tell of the attempt on the board: the progress line, and the dimming under a result panel. */
function leaveGameOnShelf() {
  $("progress-line").textContent = "";
  $("shelf").classList.remove("subdued");
}

/** The sign-in form, with `note` in its error line when there is something to say first. */
function showSignIn(note) {
  page.game = undefined;
  page.outOfSync = false;
  showView("signIn");
  showSignInError(note);
}

/**
 * Back to the sign-in form from a game. The control the player last used is gone with the game, so the keyboard's
 * and a screen reader's place moves to the form's title, from where the next Tab reaches its first field. A field
 * is not focused itself: on a phone that would raise the keyboard over the form unasked.
 */
function returnToSignIn(note) {
  showSignIn(note);
  $("sign-in-title").focus({ preventScroll: true });
}

function showSignInError(text) {
  const line = $("sign-in-error");
  line.textContent = text ?? "";
  line.hidden = !text;
}

/** Shows the game the server described: the attempt on the board, or the closing card once every level is won. */
function showGame(game) {
  page.game = game;
  page.outOfSync = false;
  if (!game.attempt) {
    showView("finished");
    $("finished-title").focus({ preventScroll: true }); // the button that led here is gone: the card's title takes its place
    return;
  }
  showView("game"); // before the board: it sizes itself to the rows around it
  showAttempt();
  board.render(game.attempt.state);
}

/** Everything around the board for the attempt on it: the level, the progress line, the HUD and the result. */
function showAttempt() {
  const { attempt, progress } = page.game;
  showLevel($("level-title"), attempt.levelNumber);
  $("progress-line").replaceChildren(
    make("span", "", `İlerleme ${attempt.levelNumber} / ${progress.levelCount}`),
    " · ",
    make("span", "", `bu bölümde ${attempt.attemptNumber}. deneme`),
  );
  showHud({ moves: $("moves"), movesLeft: $("moves-left"), goals: $("goals") }, attempt.state);
  showResult();
}

/** In front of the darkened room once the attempt is decided: what happened, and the one way on. */
function showResult() {
  const { attempt, progress } = page.game;
  const decided = attempt.state.status !== "Playing";
  const banner = $("banner");
  for (const id of BEHIND_THE_PANEL) $(id).inert = decided;
  $("game-view").classList.toggle("decided", decided);
  $("shelf").classList.toggle("subdued", decided);
  banner.classList.toggle("hidden", !decided);
  if (!decided) {
    banner.replaceChildren();
    return;
  }
  const { card, goOn } = resultPanel(attempt, { everyLevelWon: progress.finished, onGoOn: goOnToNextAttempt });
  banner.replaceChildren(card);
  goOn.focus(); // on a screen too short for the whole card the page scrolls its button into view
}

// ---------- board ----------
const board = createFramedBoard({ board: $("board"), frame: $("board-frame"), wrap: $("board-wrap") }, {
  onMove: sendMove,
  mayPlay: () => !page.busy && isPlaying(),
  space: boardSpace,
  roomAbove: wallAboveBoard,
});

const isPlaying = () => page.game?.attempt?.state.status === "Playing";

const px = (length) => parseFloat(length);

/** An element's height on the page with its margins: the room it takes in a column. */
function rowHeight(element) {
  const style = getComputedStyle(element);
  return element.offsetHeight + px(style.marginTop) + px(style.marginBottom);
}

/**
 * The room the board's cells may take: the play column's width and the window's height, less the frame's room
 * around the cells and every other row of the page.
 */
function boardSpace() {
  const column = $("game-view");
  const wrap = $("board-wrap");
  const columnStyle = getComputedStyle(column);
  const wrapStyle = getComputedStyle(wrap);
  const otherRows = [...column.children].filter((row) => row !== wrap);
  const beside = px(columnStyle.paddingLeft) + px(columnStyle.paddingRight) + px(wrapStyle.paddingLeft) + px(wrapStyle.paddingRight);
  const aboveAndBelow = px(columnStyle.paddingTop) + px(columnStyle.paddingBottom) + px(wrapStyle.paddingTop) + px(wrapStyle.paddingBottom)
    + otherRows.reduce((height, row) => height + row.offsetHeight, 0)
    + otherRows.length * px(columnStyle.rowGap)
    + rowHeight($("shelf"));
  return {
    width: Math.min(column.clientWidth - beside, BOARD_PX.max),
    height: Math.max(BOARD_PX.min, window.innerHeight - aboveAndBelow),
  };
}

/** The bare wall between the HUD and the top of the board's cells, in px: what a lamp beside the board may fill. */
function wallAboveBoard() {
  return $("board").getBoundingClientRect().top - $("hud").getBoundingClientRect().bottom;
}

let resizeFrame = 0;

function onResize() {
  cancelAnimationFrame(resizeFrame);
  resizeFrame = requestAnimationFrame(() => board.fit());
}

// ---------- playing ----------
async function sendMove(move) {
  if (page.busy || !isPlaying()) return;
  page.busy = true;
  board.clearSelection();
  try {
    if (page.outOfSync && !(await reloadKeptTheBoard())) return; // the board changed under the move: let the player look first
    await showMoveResult(move, await playMove(page.game.attempt, move));
  } catch (error) {
    await recoverFromFailedMove(error);
  } finally {
    page.busy = false;
  }
}

/** Shows the server's answer to a move: a refusal on the tiles, or the engine's events and then the state they led to. */
async function showMoveResult(move, result) {
  if (!result.accepted) {
    await board.showRefused(move);
    return;
  }
  await board.playEvents(result.events);
  page.game = { progress: result.progress, attempt: { ...page.game.attempt, state: result.state } };
  board.reconcile(result.state);
  showAttempt();
}

/**
 * Asks the server for the game after a request left it unknown. True when the server still has the board the player
 * was looking at, so the move they just made on it can go ahead; otherwise the player is told the board changed.
 */
async function reloadKeptTheBoard() {
  const shown = page.game.attempt;
  if (!(await loadGame(openGame))) return false;
  const kept = isSameBoard(shown, page.game?.attempt);
  if (!kept) showToast("Bağlantı geri geldi. Oyun sunucudaki son haline getirildi.", "note");
  return kept;
}

/** Whether the attempt the server has `now` is the one `shown`, at the same move and still being played. */
function isSameBoard(shown, now) {
  return now !== undefined
    && now.id === shown.id
    && now.state.movesUsed === shown.state.movesUsed
    && now.state.status === "Playing";
}

async function recoverFromFailedMove(error) {
  if (!(error instanceof Refusal && PAGE_IS_BEHIND.has(error.code))) {
    reportGameFailure(error);
    return;
  }
  // Nothing was played. Show the game as the server has it; an attempt that ended tells so itself, on its panel.
  const loaded = await loadGame(openGame);
  if (loaded && error.code !== "attemptOver") showToast("Oyun başka bir yerde ilerlemiş. Son hâli yüklendi.", "note");
}

/** "Sonraki bölüm" and "Tekrar dene": the next attempt on the level the player is now on. */
async function goOnToNextAttempt(button) {
  if (page.busy) return;
  page.busy = true;
  button.disabled = true;
  try {
    await loadGame(startAttempt);
  } finally {
    button.disabled = false; // it is gone with its panel when the attempt started, and usable again when it did not
    page.busy = false;
  }
}

/** Shows the game `request` answers with; false, with the failure told, when it could not be had. */
async function loadGame(request) {
  try {
    showGame(await request());
    return true;
  } catch (error) {
    reportGameFailure(error);
    return false;
  }
}

/** Tells a failed game request. A session the server no longer knows goes back to the sign-in form. */
function reportGameFailure(error) {
  if (isSignInRequired(error)) {
    returnToSignIn("Oturumun kapandı. Tekrar giriş yap.");
    return;
  }
  showToast(failureText(error));
  page.outOfSync = error instanceof Unreachable || error.status >= 500;
}

// ---------- account ----------
async function signIn(event) {
  event.preventDefault();
  const username = $("username").value.trim();
  const password = $("password").value;
  if (username === "" || password === "") {
    showSignInError("Kullanıcı adını ve şifreni yaz.");
    return;
  }
  const button = $("sign-in-submit");
  showSignInError(undefined);
  button.disabled = true;
  button.classList.add("is-busy");
  try {
    const session = await api("/api/player/sign-in", { method: "POST", body: { username, password } });
    $("password").value = "";
    await enter(session.username);
  } catch (error) {
    // The sign-in itself never answers "sign in first". The game asked for right after it does, when the browser did
    // not send back the cookie it was just given: the page is open over plain http, or cookies are blocked.
    showSignInError(isSignInRequired(error) ? SESSION_NOT_KEPT : failureText(error));
  } finally {
    button.disabled = false;
    button.classList.remove("is-busy");
  }
}

/** A signed-in player comes in: their name on the shelf, their game on the board. */
async function enter(username) {
  $("account-name").textContent = username;
  showGame(await openGame());
}

/** Signing out leaves the live attempt as it is on the server; signing in again goes on with it. */
async function signOut() {
  if (page.busy) return;
  try {
    await api("/api/player/sign-out", { method: "POST" });
  } catch (error) {
    showToast(failureText(error));
    return;
  }
  $("sign-in-form").reset(); // the next person at this phone starts from an empty form
  returnToSignIn();
}

// ---------- startup ----------
/**
 * Asks for the design's typefaces once the page is up. They are an extra: the page never waits for another site,
 * and where they do not arrive (no connection to it, or a server whose policy does not let them in) the fallbacks
 * of gallery.css stay. Type that arrives late changes the height of a row or two, so the board is fitted again.
 */
function requestDesignFonts() {
  const sheet = document.createElement("link");
  sheet.rel = "stylesheet";
  sheet.href = DESIGN_FONTS;
  document.head.appendChild(sheet);
  document.fonts?.addEventListener("loadingdone", onResize);
}

async function start() {
  $("finished-exhibit").replaceWith(starAlcove()); // the closing card shows the star the result panels show
  $("sign-in-form").addEventListener("submit", signIn);
  $("sign-out").addEventListener("click", signOut);
  window.addEventListener("resize", onResize);
  requestDesignFonts();
  try {
    const [session] = await Promise.all([api("/api/player/me"), installPieceArt()]);
    if (session.signedIn) await enter(session.username); else showSignIn();
  } catch (error) {
    if (!isFailedRequest(error)) throw error;
    $("loading-note").textContent = "Oyun açılamadı. Bağlantını kontrol edip sayfayı yenile.";
  }
}

start();
