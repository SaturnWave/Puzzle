// The plaque hung in front of the darkened room once an attempt is decided. Its nameplate says what happened; a lit
// alcove of the gallery shows what the game left behind (the star of a level won, or the goals a lost one did not
// reach); under it stand the level and the attempt, and the one green button that leads on.

import { leftoverAlcove, starAlcove } from "./alcove.js";
import { make } from "./dom.js";
import { openGoalFigures } from "./hud.js";

/** The page's markup names the panel's title by this id (the dialog's label). */
const TITLE_ID = "result-title";

/** What the nameplate says for the status a game ended with. */
const TITLES = { Won: "Bölüm tamamlandı!", LostOutOfMoves: "Hamle bitti!", LostNoMovesPossible: "Oynanacak hamle kalmadı!" };

/** Where the green button leads, by how the attempt came out: to the same level again, to the next one, or to the closing card. */
const GO_ON_LABELS = { lost: "Tekrar dene", won: "Sonraki bölüm", wonLastLevel: "Devam" };

/** How a decided attempt came out, as the panel tells the outcomes apart: "lost", "won" or "wonLastLevel". */
function outcomeOf(state, everyLevelWon) {
  if (state.status !== "Won") return "lost";
  return everyLevelWon ? "wonLastLevel" : "won";
}

/** What the panel shows in its alcove: the goals a lost game did not reach, or the star of a level won. */
function exhibitOf(state, outcome) {
  return outcome === "lost" ? leftoverAlcove("Kalan hedefler", openGoalFigures(state)) : starAlcove();
}

/** The line under the alcove: the level, and the attempt a lost game was or the moves a won one took. */
function metaOf({ state, levelNumber, attemptNumber }, outcome) {
  return outcome === "lost" ? `Bölüm ${levelNumber} · ${attemptNumber}. deneme` : `Bölüm ${levelNumber} · ${state.movesUsed} hamlede`;
}

/** The green button of the panel; pressed, it hands itself to `onGoOn`. */
function goOnButton(text, onGoOn) {
  const button = make("button", "btn primary");
  button.type = "button";
  button.appendChild(make("span", "btn-label", text));
  button.addEventListener("click", () => onGoOn(button));
  return button;
}

/**
 * The panel of a decided attempt, as `{ card, goOn }`: the plaque and its button. `attempt` is the attempt as the
 * server told it, `everyLevelWon` whether no level is left to play after it, and `onGoOn(button)` is called when the
 * button is pressed.
 */
export function resultPanel(attempt, { everyLevelWon, onGoOn }) {
  const { state } = attempt;
  const outcome = outcomeOf(state, everyLevelWon);
  const title = make("h2", "nameplate", TITLES[state.status] ?? state.status);
  title.id = TITLE_ID;
  const goOn = goOnButton(GO_ON_LABELS[outcome], onGoOn);
  const actions = make("div", "banner-actions");
  actions.appendChild(goOn);
  const card = make("div", "plaque card hangs result-card");
  card.append(title, exhibitOf(state, outcome), make("p", "result-meta", metaOf(attempt, outcome)), make("div", "rule"), actions);
  return { card, goOn };
}
