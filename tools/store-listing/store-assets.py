"""Makes the Google Play store listing's pictures for Museum Puzzle, outside the repository, and checks them.

Everything comes from what the game already has. The app icon is art/icon/app-icon-1024.png, made smaller. The
feature graphic is laid out from the gallery's drawings as the game reads them (unity/Assets/Art/Gallery, Pieces),
one of its public-domain paintings (unity/Assets/Resources/Paintings), the title's typefaces (Young Serif and
Gabarito, SIL OFL 1.1, unity/Assets/Art/Fonts), the jigsaw piece's outline of the icon proposals
(art/icon/tools/icons.py) and the painter's approved render (art/avatar, credits there); its words are the game's own
(words/<code>.json: EntryTitle, EntrySubtitle). The screenshots are renders of the game's own picture tools
(ScreenShots and BoosterShots in unity/Assets/Editor, written to out/unity/shots), copied as 24-bit PNG. Nothing is
drawn by hand here: no person, mascot or character.

usage: py tools/store-listing/store-assets.py [icon | feature | screenshots | text | check | all] [--out FOLDER]
    icon         icon-512.png
    feature      feature-graphic-<locale>.png for English and Turkish
    screenshots  phone-<locale>/NN-*.png and tablet-<locale>/NN-*.png from the renders (run the picture tools first:
                 docs/PLAY_RELEASE.md, section 8.3)
    text         text/<locale>.txt: each language's name and descriptions from release/store-listing, to copy from
    check        every picture in the folder against Google Play's rules; exits with 1 when one breaks them
    all          all of the above, check last (the default)
FOLDER defaults to D:/puzzle-cache/play/listing. Needs Pillow and Google Chrome. The words themselves are checked by
the tests (StoreListingTests).
"""
from __future__ import annotations

import argparse
import base64
import importlib.util
import io
import json
import shutil
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from enum import Enum
from pathlib import Path

from PIL import Image

# Loading the icon proposals' drawings (art/icon/tools/icons.py) must leave no __pycache__ beside them in the repository.
sys.dont_write_bytecode = True

REPO = Path(__file__).resolve().parents[2]
SHOTS = REPO / "out" / "unity" / "shots"
DEFAULT_OUT = Path("D:/puzzle-cache/play/listing")
CHROME = Path("C:/Program Files/Google/Chrome/Application/chrome.exe")
GALLERY = REPO / "unity" / "Assets" / "Art" / "Gallery"
PIECES = REPO / "unity" / "Assets" / "Art" / "Pieces"
FONTS = REPO / "unity" / "Assets" / "Art" / "Fonts"
PAINTINGS = REPO / "unity" / "Assets" / "Resources" / "Paintings"

KIB = 1024
MIB = 1024 * KIB


class Language(Enum):
    """A language the listing has pictures in: its Play locale, its words table, and how the picture tools name its renders."""

    ENGLISH = ("en-US", "en", "-en", "booster-en")
    TURKISH = ("tr-TR", "tr", "", "booster")

    def __init__(self, locale: str, words: str, shot_ending: str, booster_folder: str) -> None:
        self.locale = locale
        self.words = words
        self.shot_ending = shot_ending
        self.booster_folder = booster_folder

    def says(self, word_id: str) -> str:
        """What the game says for a word of its table in this language."""
        table = json.loads((REPO / "words" / f"{self.words}.json").read_text(encoding="utf-8"))
        for word in table["words"]:
            if word["id"] == word_id:
                return word["text"]
        raise KeyError(f"words/{self.words}.json has no word {word_id}")


class AssetKind(Enum):
    """What Google Play takes a picture as, with its rules (Play Console Help, "Add preview assets", read 2026-10-28)."""

    ICON = "icon"
    FEATURE = "feature graphic"
    PHONE = "phone screenshot"
    TABLET = "tablet screenshot"


@dataclass(frozen=True)
class Screen:
    """One screenshot: where it goes in the listing's order and the render it is copied from."""

    name: str
    render: str

    def source(self, language: Language) -> Path:
        return SHOTS / self.render.format(ending=language.shot_ending, boosters=language.booster_folder)


# The phone screenshots, in the order the listing shows them: play first, then what a win leads to. 1080 x 1920 is
# 9:16, the shape Play asks of a game's portrait screenshots for its promotions (1080 x 2424 would be too long).
PHONE_SCREENS = (
    Screen("01-play-row-brush", "{boosters}/at-work-hit-row-0.30s-1080x1920.png"),
    Screen("02-jigsaw", "screen-jigsaw-p10-carried-won96-placed94-1080x1920{ending}.png"),
    Screen("03-museum-hall", "screen-home-won99-placed97-1080x1920{ending}.png"),
    Screen("04-collection", "screen-collection-won99-placed97-1080x1920{ending}.png"),
    Screen("05-painting-up-close", "screen-viewer-p06-won99-placed97-1080x1920{ending}.png"),
    Screen("06-world-map", "screen-map-won99-placed97-1080x1920{ending}.png"),
    Screen("07-room-restored", "screen-restored-room02-light-won100-placed100-1080x1920{ending}.png"),
    Screen("08-level-280", "screen-entrance-all-won279-placed458-1080x1920{ending}.png"),
)

# The tablet screenshots: the same game held sideways on a 16:9 tablet (the layout turns on a tablet). One set serves
# both the 7-inch and the 10-inch slots. BoosterShots pictures a booster at work on its first screen only, so the
# level's board stands in for it.
TABLET_SCREENS = (
    Screen("01-museum-hall", "screen-home-won99-placed97-2560x1440{ending}.png"),
    Screen("02-jigsaw", "screen-jigsaw-p10-carried-won96-placed94-2560x1440{ending}.png"),
    Screen("03-level-280", "screen-entrance-all-won279-placed458-2560x1440{ending}.png"),
    Screen("04-collection", "screen-collection-won99-placed97-2560x1440{ending}.png"),
    Screen("05-world-map", "screen-map-won99-placed97-2560x1440{ending}.png"),
    Screen("06-room-restored", "screen-restored-room02-light-won100-placed100-2560x1440{ending}.png"),
)


def problems_of(path: Path, kind: AssetKind) -> list[str]:
    """What keeps a picture from being taken as this kind of asset; nothing when Play takes it."""
    with Image.open(path) as image:
        width, height = image.size
        mode, kind_of_file = image.mode, image.format
    size = path.stat().st_size
    found = []
    if kind is AssetKind.ICON:
        if (width, height) != (512, 512):
            found.append(f"is {width}x{height}, not 512x512")
        if kind_of_file != "PNG" or mode != "RGBA":
            found.append(f"is {kind_of_file} {mode}, not a 32-bit PNG with alpha")
        if size > 1024 * KIB:
            found.append(f"has {size} bytes, more than 1024 KB")
        return found

    if kind_of_file not in ("PNG", "JPEG") or mode != "RGB":
        found.append(f"is {kind_of_file} {mode}, not a JPEG or a 24-bit PNG without alpha")
    if kind is AssetKind.FEATURE:
        if (width, height) != (1024, 500):
            found.append(f"is {width}x{height}, not 1024x500")
        if size > 15 * MIB:
            found.append(f"has {size} bytes, more than 15 MB")
        return found

    short, long = sorted((width, height))
    if short < 320 or long > 3840:
        found.append(f"is {width}x{height}: each side must be 320 to 3,840 px")
    if long > 2 * short:
        found.append(f"is {width}x{height}: the long side is more than twice the short one")
    if size > 8 * MIB:
        found.append(f"has {size} bytes, more than 8 MB")
    if kind is AssetKind.PHONE and (short, long) < (1080, 1920):
        found.append(f"is {width}x{height}: below 1080x1920, so Play does not use it in its promotions")
    if kind is AssetKind.TABLET and (short < 1080 or long * 9 != short * 16):
        found.append(f"is {width}x{height}: a tablet screenshot is 16:9 or 9:16 and at least 1080 px on its short side")
    return found


def make_icon(out: Path) -> Path:
    """The 512 x 512 icon: the game's own icon made smaller, kept as a 32-bit PNG."""
    with Image.open(REPO / "art" / "icon" / "app-icon-1024.png") as source:
        icon = source.convert("RGBA").resize((512, 512), Image.LANCZOS)
    path = out / "icon-512.png"
    icon.save(path, optimize=True)
    return path


def data_uri(path: Path, kind: str, longest: int | None = None) -> str:
    """A file as a data URI; a picture made no longer than the longest side asked, so the page stays small."""
    if longest is None:
        return f"data:{kind};base64," + base64.b64encode(path.read_bytes()).decode()
    with Image.open(path) as source:
        picture = source.copy()
    picture.thumbnail((longest, longest), Image.LANCZOS)
    buffer = io.BytesIO()
    if kind == "image/jpeg":
        picture.convert("RGB").save(buffer, format="JPEG", quality=92)
    else:
        picture.save(buffer, format="PNG")
    return f"data:{kind};base64," + base64.b64encode(buffer.getvalue()).decode()


def jigsaw_piece_path(x: float, y: float, side: float) -> str:
    """The outline of a jigsaw piece on a square, as the icon proposals draw it (art/icon/tools/icons.py)."""
    spec = importlib.util.spec_from_file_location("icon_drawings", REPO / "art" / "icon" / "tools" / "icons.py")
    if spec is None or spec.loader is None:
        raise FileNotFoundError("art/icon/tools/icons.py, which draws the jigsaw piece, is missing")
    drawings = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(drawings)
    return drawings.piece_path(x, y, side, (1, -1, 1, -1))


# The feature graphic in CSS pixels (1024 x 500): a gilt frame on the gallery's teal wall with one piece lifted out
# of its painting, the game's title in its gilt with its subtitle, a few of the board's pieces, and the painter
# cheering at the right. Play may cut the edges in some places, so the words stay well inside them.
FRAME_SCALE = 0.33          # picture-frame.png (710 px) shown at this scale: its gilt band is 94 px, so 31 px here
FRAME_SLICE = 260           # where the game's import slices the frame (GalleryArtImport.cs: 65 points of 4 px)
FRAME_BAND = 111            # where the frame's gilt begins, from each edge of picture-frame.png (its shadow outside)
FRAME_WINDOW = 205          # where the frame's window begins, from each edge of picture-frame.png
FRAME_BOX = (2, 96, 404, 300)  # left, top, width, height of the frame's whole picture, its shadow included
LIGHT_WIDTH = 236           # picture-light.png shown this wide; its brass bar ends 108 / 960 of that below its top
PIECE = (172, 84, 70)       # the hole's square in the frame's window: left, top, side
PIECE_FLIGHT = (96, 102, 17)  # where the lifted piece is from its hole: right, down, and its turn in degrees
TITLE_BOX = (384, 72, 344)  # the title's left, top and width; the subtitle and the pieces of the board follow below
PAINTER_BOX = (744, 70, 442)  # the painter's left, top and height; his feet go below the picture
BOARD_PIECES = (            # a piece of the board: its picture, left, top, width and turn in degrees
    ("gem-red", 432, 398, 62, -14),
    ("sp-colorbomb", 500, 410, 72, 8),
    ("gem-yellow", 580, 396, 60, -6),
    ("gem-blue", 648, 410, 56, 10),
)


def feature_page(language: Language) -> str:
    """The feature graphic as a page for Chrome, every picture and typeface inside it."""
    left, top, width, height = FRAME_BOX
    inset = FRAME_WINDOW * FRAME_SCALE
    wx, wy, ww, wh = left + inset, top + inset, width - 2 * inset, height - 2 * inset
    px, py, side = PIECE
    hole = jigsaw_piece_path(wx + px, wy + py, side)
    centre_x, centre_y = wx + px + side / 2, wy + py + side / 2
    right, down, turn = PIECE_FLIGHT
    wave = data_uri(PAINTINGS / "room-11" / "great-wave.jpg", "image/jpeg", 1200)
    painting = f'<image href="{wave}" x="{wx}" y="{wy}" width="{ww}" height="{wh}" preserveAspectRatio="xMidYMid slice"/>'
    light_top = top + FRAME_BAND * FRAME_SCALE - LIGHT_WIDTH * 108 / 960 + 2
    title_left, title_top, title_width = TITLE_BOX
    title = "".join(f'<div class="line"><span class="ink">{word}</span><span class="gilt">{word}</span></div>'
                    for word in language.says("EntryTitle").split(" ", 1))
    subtitle = language.says("EntrySubtitle").replace(", ", ",<br>", 1)
    board = "".join(
        f'<img class="gem" style="left:{x}px;top:{y}px;width:{w}px;transform:rotate({angle}deg)" '
        f'src="{data_uri(PIECES / f"{name}.png", "image/png", 256)}">'
        for name, x, y, w, angle in BOARD_PIECES)
    painter_left, painter_top, painter_height = PAINTER_BOX
    return f"""<!doctype html><html><head><meta charset="utf-8"><style>
@font-face {{ font-family: "Young Serif"; src: url({data_uri(FONTS / "YoungSerif-Regular.ttf", "font/ttf")}); }}
@font-face {{ font-family: "Gabarito"; font-weight: 700; src: url({data_uri(FONTS / "Gabarito-Bold.ttf", "font/ttf")}); }}
html, body {{ margin: 0; width: 1024px; height: 500px; overflow: hidden; background: #17666F; }}
.wall {{ position: absolute; inset: 0; background: radial-gradient(ellipse 70% 85% at 50% 40%, #2B8E95 0%, #1E777F 38%, #145A62 72%, #0D444B 100%); }}
.damask {{ position: absolute; inset: 0; background: url({data_uri(GALLERY / "damask.png", "image/png", 512)}) 0 0 / 236px 236px repeat; opacity: .9; }}
.shade {{ position: absolute; inset: 0; background: linear-gradient(180deg, rgba(5,30,34,0) 60%, rgba(5,30,34,.5) 100%); }}
.light {{ position: absolute; left: {left + width / 2 - LIGHT_WIDTH / 2}px; top: {light_top}px; width: {LIGHT_WIDTH}px; }}
.frame {{ position: absolute; left: {left}px; top: {top}px; width: {width}px; height: {height}px; box-sizing: border-box;
  border-style: solid; border-width: {FRAME_SLICE * FRAME_SCALE}px;
  border-image: url({data_uri(GALLERY / "picture-frame.png", "image/png")}) {FRAME_SLICE} fill / {FRAME_SLICE * FRAME_SCALE}px; }}
svg.layer {{ position: absolute; inset: 0; overflow: visible; }}
.title {{ position: absolute; left: {title_left}px; top: {title_top}px; width: {title_width}px; text-align: center;
  font-family: "Young Serif"; font-size: 84px; line-height: 92px; }}
.line {{ position: relative; white-space: nowrap; }}
.ink {{ position: absolute; inset: 0; color: #3A1E06; -webkit-text-stroke: 10px #3A1E06; text-shadow: 0 6px 0 #3A1E06, 0 14px 22px rgba(2,22,26,.55); }}
.gilt {{ position: relative; background: linear-gradient(180deg, #FFF6D2 0%, #FFE38E 34%, #F5B544 70%, #E8981F 100%);
  -webkit-background-clip: text; background-clip: text; color: transparent; }}
.subtitle {{ margin-top: 14px; font-family: "Gabarito"; font-weight: 700; font-size: 26px; line-height: 32px; color: #FFF3D6;
  text-shadow: 0 2px 0 rgba(6,34,38,.9), 0 0 14px rgba(6,34,38,.6); }}
.gem {{ position: absolute; filter: drop-shadow(0 6px 6px rgba(2,22,26,.45)); }}
.painter {{ position: absolute; left: {painter_left}px; top: {painter_top}px; height: {painter_height}px; filter: drop-shadow(0 10px 18px rgba(2,22,26,.5)); }}
</style></head><body>
<div class="wall"></div><div class="damask"></div><div class="shade"></div>
<svg class="layer" width="1024" height="500"><defs><filter id="inner" x="-20%" y="-20%" width="140%" height="140%"><feGaussianBlur stdDeviation="3"/></filter>
<clipPath id="hole"><path d="{hole}"/></clipPath></defs>
{painting}
<path d="{hole}" fill="#EADFC2"/><g clip-path="url(#hole)"><path d="{hole}" fill="none" stroke="#3A1E06" stroke-opacity=".5" stroke-width="10" filter="url(#inner)"/></g>
<path d="{hole}" fill="none" stroke="#9C8358" stroke-width="2" stroke-dasharray="5 4"/>
</svg>
<div class="frame"></div>
<img class="light" src="{data_uri(GALLERY / "picture-light.png", "image/png", 512)}">
<div class="title">{title}<div class="subtitle">{subtitle}</div></div>
{board}
<svg class="layer" width="1024" height="500"><defs>
<clipPath id="piece"><path d="{hole}"/></clipPath>
<filter id="lift" x="-40%" y="-40%" width="180%" height="180%"><feDropShadow dx="0" dy="12" stdDeviation="9" flood-color="#021C22" flood-opacity=".6"/></filter></defs>
<g transform="translate({right} {down}) rotate({turn} {centre_x} {centre_y})" filter="url(#lift)">
<path d="{hole}" fill="#FFF8E7" stroke="#FFF8E7" stroke-width="12" stroke-linejoin="round"/>
<g clip-path="url(#piece)">{painting}</g>
<path d="{hole}" fill="none" stroke="#5E3305" stroke-width="2.5" stroke-linejoin="round"/></g>
</svg>
<img class="painter" src="{data_uri(REPO / "art" / "avatar" / "ressam_ayakta-sevinc.png", "image/png", 1100)}">
</body></html>"""


def render_page(html: str, width: int, height: int) -> Image.Image:
    """The page as headless Chrome draws it at twice the size, made smaller again for clean edges."""
    if not CHROME.exists():
        raise FileNotFoundError(f"no Chrome at {CHROME}")
    with tempfile.TemporaryDirectory(prefix="store-assets-") as work:
        page = Path(work) / "page.html"
        shot = Path(work) / "page.png"
        page.write_text(html, encoding="utf-8")
        subprocess.run(
            [str(CHROME), "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=2",
             f"--user-data-dir={Path(work) / 'profile'}", f"--window-size={width},{height}", f"--screenshot={shot}",
             "--virtual-time-budget=5000", page.as_uri()],
            check=True, capture_output=True, timeout=180)
        with Image.open(shot) as drawn:
            return drawn.convert("RGB").resize((width, height), Image.LANCZOS)


def make_feature(out: Path, language: Language) -> Path:
    path = out / f"feature-graphic-{language.locale}.png"
    render_page(feature_page(language), 1024, 500).save(path, optimize=True)
    return path


def copy_screens(out: Path, language: Language) -> list[Path]:
    """The chosen renders, as 24-bit PNGs in the listing's order; a missing render stops it and names the tool to run."""
    written = []
    for folder, screens in ((f"phone-{language.locale}", PHONE_SCREENS), (f"tablet-{language.locale}", TABLET_SCREENS)):
        target = out / folder
        if target.exists():
            shutil.rmtree(target)
        target.mkdir(parents=True)
        for screen in screens:
            source = screen.source(language)
            if not source.exists():
                raise FileNotFoundError(f"{source} is missing: render it first (docs/PLAY_RELEASE.md, Mağaza sayfası)")
            with Image.open(source) as render:
                picture = render.convert("RGB")
            path = target / f"{screen.name}.png"
            picture.save(path, optimize=True)
            written.append(path)
    return written


def write_texts(out: Path) -> list[Path]:
    """Each language's listing as plain text, every field under its name and its length, to copy into Play Console."""
    target = out / "text"
    target.mkdir(parents=True, exist_ok=True)
    written = []
    for source in sorted((REPO / "release" / "store-listing").glob("*.json")):
        listing = json.loads(source.read_text(encoding="utf-8"))
        fields = (("App name", listing["title"], 30), ("Short description", listing["shortDescription"], 80),
                  ("Full description", listing["fullDescription"], 4000))
        blocks = [f"{listing['locale']} ({listing['review']}): {listing['reviewNote']}"]
        blocks += [f"===== {name} ({len(text)} / {limit}) =====\n{text}" for name, text, limit in fields]
        path = target / f"{listing['locale']}.txt"
        path.write_text("\n\n".join(blocks) + "\n", encoding="utf-8")
        written.append(path)
    return written


def kind_of(path: Path, out: Path) -> AssetKind | None:
    """What a file of the listing's folder is uploaded as; nothing for a file the listing does not upload."""
    relative = path.relative_to(out).parts
    if relative == ("icon-512.png",):
        return AssetKind.ICON
    if len(relative) == 1 and relative[0].startswith("feature-graphic-"):
        return AssetKind.FEATURE
    if len(relative) == 2 and relative[0].startswith("phone-"):
        return AssetKind.PHONE
    if len(relative) == 2 and relative[0].startswith("tablet-"):
        return AssetKind.TABLET
    return None


def check(out: Path) -> bool:
    """Prints every picture with its size and what Play would refuse in it; True when Play takes them all."""
    fine = True
    for folder in sorted({path.parent for path in out.rglob("*.png")}):
        count = len(list(folder.glob("*.png")))
        if folder != out and not 2 <= count <= 8:
            print(f"{folder.name}: {count} pictures; a set has 2 to 8")
            fine = False
    for path in sorted(out.rglob("*.png")):
        kind = kind_of(path, out)
        if kind is None:
            print(f"{path.relative_to(out)}: not a listing picture, left out")
            continue
        with Image.open(path) as image:
            width, height = image.size
        found = problems_of(path, kind)
        fine = fine and not found
        verdict = "ok" if not found else "; ".join(found)
        print(f"{str(path.relative_to(out)):48} {kind.value:18} {width}x{height} {path.stat().st_size:>10,} bytes  {verdict}")
    return fine


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n", 1)[0])
    parser.add_argument("step", nargs="?", default="all", choices=("icon", "feature", "screenshots", "text", "check", "all"))
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    arguments = parser.parse_args()
    out: Path = arguments.out
    out.mkdir(parents=True, exist_ok=True)
    if arguments.step in ("icon", "all"):
        print("wrote", make_icon(out))
    if arguments.step in ("feature", "all"):
        for language in Language:
            print("wrote", make_feature(out, language))
    if arguments.step in ("screenshots", "all"):
        for language in Language:
            print("wrote", len(copy_screens(out, language)), "screenshots in", language.locale)
    if arguments.step in ("text", "all"):
        print("wrote", len(write_texts(out)), "listings' words in", out / "text")
    if arguments.step in ("check", "all"):
        return 0 if check(out) else 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
