"""Renders the chosen app icon (F, the palette on cream; the owner's choice of 2026-10-23, after G) and its Android adaptive layers.

usage: final_g.py OUT_DIR
- app-icon-1024.png: the whole icon, opaque (iOS, Android legacy and round icons, the stores).
- adaptive-background-1024.png: the cream background alone, opaque.
- adaptive-foreground-1024.png: the palette and brush alone on transparency, shrunk into the middle 66 %
  (Android's adaptive-icon safe zone: launchers may cut up to a sixth from every side).
"""
import shutil
import subprocess
import sys
from pathlib import Path

from icons import CHROME, REPO, SIZE, data_uri
from palette import WOOD, background, brush, palette

SAFE = 0.66


def svg(body, defs_extra="", with_background=True):
    back = '<rect width="1024" height="1024" fill="url(#bg)"/><ellipse cx="512" cy="470" rx="470" ry="420" fill="url(#shine)"/>' if with_background else ""
    return f"""<!doctype html><html><head><meta charset="utf-8"><style>html,body{{margin:0;background:transparent}}svg{{display:block}}</style></head><body>
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {SIZE} {SIZE}" width="{SIZE}" height="{SIZE}"><defs>{WOOD}{background("cream")}
<clipPath id="palette-clip"><path d="M168 548C140 372 300 214 522 206C770 198 914 330 892 528C874 700 726 812 556 800C462 794 430 734 462 690C494 646 462 600 400 614C322 632 292 716 222 698C182 688 174 640 168 548Z"/></clipPath>
<radialGradient id="shine" cx=".5" cy=".42" r=".5"><stop offset="0" stop-color="#FFF6DA" stop-opacity=".35"/><stop offset="1" stop-color="#FFF6DA" stop-opacity="0"/></radialGradient>{defs_extra}</defs>
{back}{body}</svg></body></html>"""


def shoot(html, png, profile, transparent):
    html_path = png.with_suffix(".html")
    html_path.write_text(html, encoding="utf-8")
    command = [CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", f"--user-data-dir={profile}",
               f"--window-size={SIZE},{SIZE}", f"--screenshot={png}", "--virtual-time-budget=3000"]
    if transparent:
        command.append("--default-background-color=00000000")
    subprocess.run(command + [html_path.as_uri()], check=True, capture_output=True, timeout=120)
    html_path.unlink()


def main(out):
    out.mkdir(parents=True, exist_ok=True)
    gem = {c: data_uri(REPO / f"unity/Assets/Art/Pieces/gem-{c}.png", 512) for c in ("red", "yellow", "blue")}
    three = [gem["red"], gem["yellow"], gem["blue"]]
    big = [(450, 340, 235), (680, 340, 235), (760, 560, 225)]
    art = palette(three, big, -8) + brush(610, 610, 146, 0.92)
    shrink = (1 - SAFE) / 2 * SIZE
    foreground = f'<g transform="translate({shrink:.1f} {shrink:.1f}) scale({SAFE})">{art}</g>'
    profile = out / ".chrome-profile"
    try:
        shoot(svg(art), out / "app-icon-1024.png", profile, transparent=False)
        shoot(svg(""), out / "adaptive-background-1024.png", profile, transparent=False)
        shoot(svg(foreground, with_background=False), out / "adaptive-foreground-1024.png", profile, transparent=True)
    finally:
        shutil.rmtree(profile, ignore_errors=True)
    print("rendered", sorted(p.name for p in out.glob("*.png")))


if __name__ == "__main__":
    main(Path(sys.argv[1]).resolve())
