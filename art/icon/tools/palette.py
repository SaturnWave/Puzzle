"""The painter's-palette app icons: a wooden palette whose paint blobs are the game's gems, and a brush.

usage: palette.py OUT_DIR
"""
import shutil
import sys
from pathlib import Path

from icons import REPO, SIZE, data_uri, render

PALETTE = ("M168 548C140 372 300 214 522 206C770 198 914 330 892 528C874 700 726 812 556 800"
           "C462 794 430 734 462 690C494 646 462 600 400 614C322 632 292 716 222 698C182 688 174 640 168 548Z")

WOOD = """<linearGradient id="wood" x1="0" y1="0" x2="1" y2="1">
<stop offset="0" stop-color="#E9B377"/><stop offset=".45" stop-color="#C98845"/><stop offset="1" stop-color="#8E5424"/></linearGradient>
<linearGradient id="wood-edge" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#7A4318"/><stop offset="1" stop-color="#4E2A0E"/></linearGradient>
<radialGradient id="wood-light" cx=".38" cy=".3" r=".6"><stop offset="0" stop-color="#FFF4DC" stop-opacity=".55"/><stop offset="1" stop-color="#FFF4DC" stop-opacity="0"/></radialGradient>
<linearGradient id="handle" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#E0473A"/><stop offset=".5" stop-color="#A81E22"/><stop offset="1" stop-color="#6A0F14"/></linearGradient>
<linearGradient id="ferrule" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FFF4C4"/><stop offset=".4" stop-color="#F6B93C"/><stop offset="1" stop-color="#A86A12"/></linearGradient>
<linearGradient id="bristle" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#F1D9A8"/><stop offset=".55" stop-color="#C99A5C"/><stop offset="1" stop-color="#2F6FD8"/></linearGradient>
<filter id="lift" x="-30%" y="-30%" width="160%" height="160%"><feDropShadow dx="0" dy="22" stdDeviation="24" flood-color="#1A0C02" flood-opacity=".5"/></filter>
<filter id="gem-shadow" x="-30%" y="-30%" width="160%" height="160%"><feDropShadow dx="0" dy="8" stdDeviation="8" flood-color="#3A1A04" flood-opacity=".45"/></filter>"""

GRAIN = "".join(
    f'<path d="M{200 + i * 9} {300 + i * 37}C{380 + i * 6} {250 + i * 30} {620 + i * 4} {260 + i * 28} {860 - i * 8} {340 + i * 33}" '
    f'fill="none" stroke="#7A4318" stroke-opacity=".14" stroke-width="{3 + i % 3}"/>' for i in range(11))


def background(kind):
    if kind == "teal":
        return """<radialGradient id="bg" cx=".5" cy=".42" r=".75"><stop offset="0" stop-color="#3FB2B4"/><stop offset=".55" stop-color="#1F8088"/><stop offset="1" stop-color="#0D4A53"/></radialGradient>"""
    if kind == "cream":
        return """<radialGradient id="bg" cx=".5" cy=".4" r=".8"><stop offset="0" stop-color="#FFF8E6"/><stop offset=".6" stop-color="#F6E4BC"/><stop offset="1" stop-color="#D9B77A"/></radialGradient>"""
    return """<radialGradient id="bg" cx=".5" cy=".38" r=".8"><stop offset="0" stop-color="#5B4FD6"/><stop offset=".55" stop-color="#2E2A8C"/><stop offset="1" stop-color="#15134A"/></radialGradient>"""


def brush(x, y, angle, scale=1.0):
    """A long brush lying at an angle; its blue-wet tip at (x, y)."""
    return f"""<g transform="translate({x} {y}) rotate({angle}) scale({scale})" filter="url(#lift)">
<path d="M0 0C30 -24 92 -30 150 -22L150 22C92 30 30 24 0 0Z" fill="url(#bristle)" stroke="#5E3305" stroke-width="5" stroke-linejoin="round"/>
<path d="M2 0C24 -6 40 -6 58 -3" fill="none" stroke="#9CC6FF" stroke-width="5" stroke-linecap="round" stroke-opacity=".8"/>
<rect x="146" y="-27" width="74" height="54" rx="10" fill="url(#ferrule)" stroke="#5E3305" stroke-width="5"/>
<path d="M160 -27V27M176 -27V27" stroke="#FFF6D2" stroke-width="4" stroke-opacity=".7"/>
<path d="M216 -22L560 -14C580 -13 590 0 580 12L560 16L216 22Z" fill="url(#handle)" stroke="#3E0A0C" stroke-width="5" stroke-linejoin="round"/>
<path d="M232 -12L540 -6" stroke="#FFB4A8" stroke-width="6" stroke-linecap="round" stroke-opacity=".6"/></g>"""


def palette(gems, places, tilt, gilt_rim=False):
    rim = '<path d="{0}" fill="none" stroke="url(#ferrule)" stroke-width="16"/>'.format(PALETTE) if gilt_rim else ""
    blobs = "".join(
        f'<image href="{gems[i]}" x="{x - s / 2}" y="{y - s / 2}" width="{s}" height="{s}" filter="url(#gem-shadow)"/>'
        for i, (x, y, s) in enumerate(places))
    return f"""<g transform="rotate({tilt} 520 520)">
<mask id="hole"><rect width="{SIZE}" height="{SIZE}" fill="#fff"/><ellipse cx="300" cy="520" rx="58" ry="48" fill="#000"/><ellipse cx="300" cy="536" rx="58" ry="48" fill="#000"/></mask>
<g mask="url(#hole)">
<g filter="url(#lift)"><path d="{PALETTE}" fill="url(#wood-edge)" transform="translate(0 16)"/></g>
<path d="{PALETTE}" fill="url(#wood)"/>
<g clip-path="url(#palette-clip)">{GRAIN}</g>
<path d="{PALETTE}" fill="url(#wood-light)"/>
<path d="{PALETTE}" fill="none" stroke="#5E3305" stroke-width="7"/>{rim}
</g>
<ellipse cx="300" cy="520" rx="58" ry="48" fill="none" stroke="#5E3305" stroke-width="7"/>
<path d="M248 512A58 48 0 0 1 352 512" fill="none" stroke="#3E1E06" stroke-opacity=".5" stroke-width="12"/>
{blobs}</g>"""


def page(kind, body):
    return f"""<!doctype html><html><head><meta charset="utf-8"><style>html,body{{margin:0;background:#000}}svg{{display:block}}</style></head><body>
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {SIZE} {SIZE}" width="{SIZE}" height="{SIZE}"><defs>{WOOD}{background(kind)}
<clipPath id="palette-clip"><path d="{PALETTE}"/></clipPath>
<radialGradient id="shine" cx=".5" cy=".42" r=".5"><stop offset="0" stop-color="#FFF6DA" stop-opacity=".35"/><stop offset="1" stop-color="#FFF6DA" stop-opacity="0"/></radialGradient></defs>
<rect width="{SIZE}" height="{SIZE}" fill="url(#bg)"/><ellipse cx="512" cy="470" rx="470" ry="420" fill="url(#shine)"/>
{body}</svg></body></html>"""


def main(out):
    out.mkdir(parents=True, exist_ok=True)
    gem = {c: data_uri(REPO / f"unity/Assets/Art/Pieces/gem-{c}.png", 512) for c in ("red", "yellow", "blue", "green", "purple")}
    four = [gem["red"], gem["yellow"], gem["blue"], gem["green"]]
    arc = [(430, 330, 180), (612, 312, 180), (775, 418, 175), (790, 600, 170)]
    three = [gem["red"], gem["yellow"], gem["blue"]]
    big = [(450, 340, 235), (680, 340, 235), (760, 560, 225)]
    # The brush lies across the palette's lower half, its wet tip by the gems, its handle out to the lower left.
    designs = {
        "E-palet-teal": page("teal", palette(four, arc, -10) + brush(620, 600, 146, 0.92)),
        "F-palet-krem": page("cream", palette(three, big, -8) + brush(610, 610, 146, 0.92)),
        "G-palet-gece": page("night", palette(four, arc, -12, gilt_rim=True) + brush(620, 600, 146, 0.92)),
    }
    profile = out / ".chrome-profile"
    try:
        for name, html in designs.items():
            path = out / f"{name}.html"
            path.write_text(html, encoding="utf-8")
            render(path, out / f"{name}.png", profile)
            print(name, "rendered")
    finally:
        shutil.rmtree(profile, ignore_errors=True)


if __name__ == "__main__":
    main(Path(sys.argv[1]).resolve())
