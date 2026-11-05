// The player page's HUD: the level's number on its plaque, the moves left in the medallion, and each goal as the
// piece's own art with what is left of it. The lab draws its own HUD (game-view.js); the names here are the game's.

import { make, svgElement } from "./dom.js";
import { symbolArt } from "./game-view.js";

const LOW_MOVES = 3;

/** What the pieces are called in the game, by the engine's colour. */
const PIECE_NAMES = {
  red: "Uğur böceği",
  green: "Kare",
  blue: "Damla",
  yellow: "Papatya",
  purple: "Koni",
  orange: "Hilal",
  cyan: "Elmas",
  pink: "Lale",
};

/** The goals that are not a colour to collect: the sprite's icon for each, and what the player is to do. */
const OBSTACLE_GOALS = {
  breakBoxes: { icon: "goal-boxes", name: "Sandık kır" },
  clearJelly: { icon: "goal-jelly", name: "Eski vernik temizle" },
  breakChains: { icon: "goal-chains", name: "Koruma bandı sök" },
  dropItems: { icon: "goal-items", name: "Çerçeve köşesi indir" },
};

/** A goal's icon in the sprite and its name; a goal type this page does not know shows the sprite's unknown tile. */
function describeGoal(goal) {
  if (goal.type === "collectColor") {
    const colour = goal.color.toLowerCase();
    return { icon: `goal-${colour}`, name: `${PIECE_NAMES[colour] ?? goal.color} topla` };
  }
  return OBSTACLE_GOALS[goal.type] ?? { icon: "unknown", name: goal.type };
}

const leftOf = (goal) => Math.max(0, goal.target - goal.done);

/** The mark of a goal that is met: a green seal with a tick. */
function doneMark() {
  const mark = svgElement("svg", { class: "goal-done", viewBox: "0 0 20 20", "aria-hidden": "true" });
  mark.append(
    svgElement("circle", { cx: 10, cy: 10, r: 9, fill: "#3FAE35", stroke: "#145A1B", "stroke-width": 1.6 }),
    svgElement("path", { d: "M5.6 10.4L8.8 13.4L14.4 7", fill: "none", stroke: "#fff", "stroke-width": 2.6, "stroke-linecap": "round", "stroke-linejoin": "round" }),
  );
  return mark;
}

/** One goal: its art above what is left of it, or above the seal once it is met. A screen reader hears it as one sentence. */
function goalFigure(goal) {
  const { icon, name } = describeGoal(goal);
  const left = leftOf(goal);
  const figure = make("div", left === 0 ? "goal done" : "goal");
  const count = make("span", "goal-count figure", String(left));
  count.setAttribute("aria-hidden", "true");
  figure.setAttribute("role", "img");
  figure.setAttribute("aria-label", left === 0 ? `${name}: tamam` : `${name}: ${left} kaldı`);
  figure.title = `${name}: ${Math.min(goal.done, goal.target)} / ${goal.target}`;
  figure.append(symbolArt(icon, "goal-art"), left === 0 ? doneMark() : count);
  return figure;
}

/** The goals a state has not met yet, each as its figure: what a lost game leaves to show. */
export function openGoalFigures(state) {
  return state.goals.filter((goal) => leftOf(goal) > 0).map(goalFigure);
}

/** The level plaque: a small label and the number as a figure, read together as "Bölüm 12". */
export function showLevel(title, levelNumber) {
  title.replaceChildren(make("span", "plaque-label", "Bölüm"), " ", make("span", "figure", String(levelNumber)));
}

/**
 * Shows a state's numbers: the moves left in `movesLeft`, with `moves` marked when few remain, and the goals in
 * `goals`, which is told how many there are and how wide their figures run so the plaque can size them.
 */
export function showHud({ moves, movesLeft, goals }, state) {
  const left = Math.max(0, state.moveLimit - state.movesUsed);
  movesLeft.textContent = String(left);
  moves.classList.toggle("low", state.status === "Playing" && left <= LOW_MOVES);
  goals.replaceChildren(...state.goals.map(goalFigure));
  goals.dataset.count = String(state.goals.length);
  goals.dataset.digits = String(Math.max(1, ...state.goals.map((goal) => String(leftOf(goal)).length))); // 1 for a state without goals
}
