// The player page's art: the game's own pieces as one sprite (pieces.svg, beside this module), built from the
// design's symbols by tools/web-theme/build-sprite.js under the names the board asks for. The lab keeps its own
// sprite (tiles.svg); the two are never in one page, because their gradients share names.

import { TileArtError } from "./game-view.js";

const ART_FILE = "pieces.svg";
const ART_TIMEOUT_MS = 30_000;

/**
 * Puts the sprite into the page; every tile, cover, goal icon and lamp is then one of its symbols, shown by <use>.
 * It is inlined rather than referenced as a file: the gradients and clip paths of a sprite that stays outside the
 * page do not resolve in every browser.
 */
export async function installPieceArt() {
  const sprite = new DOMParser().parseFromString(await fetchPieceArt(), "image/svg+xml").documentElement;
  if (sprite.localName !== "svg") throw new TileArtError(`${ART_FILE} is not an SVG document`);
  document.body.prepend(document.importNode(sprite, true));
}

async function fetchPieceArt() {
  let response;
  try {
    response = await fetch(new URL(ART_FILE, import.meta.url), { signal: AbortSignal.timeout?.(ART_TIMEOUT_MS) });
  } catch (cause) {
    throw new TileArtError(`${ART_FILE} could not be fetched`, { cause });
  }
  if (!response.ok) throw new TileArtError(`${ART_FILE} answered ${response.status}`);
  return response.text();
}
