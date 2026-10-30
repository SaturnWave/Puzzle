"""Draws the app icon proposals as 1024x1024 SVG pages and renders them with headless Chrome.

usage: icons.py OUT_DIR
No people and no mascots: a gilt frame, a jigsaw piece, the game's own gems and its public-domain paintings.
"""
import base64
import io
import shutil
import subprocess
import sys
from pathlib import Path

from PIL import Image

REPO = Path("C:/Users/arcan/puzzle")
CHROME = "C:/Program Files/Google/Chrome/Application/chrome.exe"
SIZE = 1024

GILT = """<linearGradient id="gilt" x1="0" y1="0" x2="0" y2="1">
<stop offset="0" stop-color="#FFF4C4"/><stop offset=".22" stop-color="#FFDB6E"/><stop offset=".58" stop-color="#F6B93C"/><stop offset="1" stop-color="#C98418"/></linearGradient>
<linearGradient id="gilt-side" x1="0" y1="0" x2="1" y2="0">
<stop offset="0" stop-color="#FFE892"/><stop offset=".5" stop-color="#F6B93C"/><stop offset="1" stop-color="#D38D1A"/></linearGradient>"""

BACKGROUND = """<radialGradient id="wall" cx=".5" cy=".42" r=".75">
<stop offset="0" stop-color="#34A7AB"/><stop offset=".55" stop-color="#1D7C84"/><stop offset="1" stop-color="#0D4A53"/></radialGradient>
<pattern id="damask" width="144" height="216" patternUnits="userSpaceOnUse">
<path d="M72 0C108 36 144 72 144 108C144 144 108 180 72 216C36 180 0 144 0 108C0 72 36 36 72 0Z" fill="none" stroke="#fff" stroke-opacity=".07" stroke-width="4"/>
<path d="M72 66C85 84 85 99 72 117C59 99 59 84 72 66Z" fill="#fff" fill-opacity=".06"/></pattern>
<radialGradient id="glow" cx=".5" cy=".45" r=".5"><stop offset="0" stop-color="#FFF1CC" stop-opacity=".35"/><stop offset="1" stop-color="#FFF1CC" stop-opacity="0"/></radialGradient>
<filter id="shadow" x="-30%" y="-30%" width="160%" height="160%"><feDropShadow dx="0" dy="18" stdDeviation="22" flood-color="#021C22" flood-opacity=".55"/></filter>
<filter id="soft" x="-30%" y="-30%" width="160%" height="160%"><feDropShadow dx="0" dy="10" stdDeviation="12" flood-color="#021C22" flood-opacity=".5"/></filter>"""


def data_uri(path, max_side=1400):
    image = Image.open(path).convert("RGBA" if path.suffix == ".png" else "RGB")
    image.thumbnail((max_side, max_side), Image.LANCZOS)
    buffer = io.BytesIO()
    kind = "png" if path.suffix == ".png" else "jpeg"
    image.save(buffer, format=kind.upper(), quality=90)
    return f"data:image/{kind};base64," + base64.b64encode(buffer.getvalue()).decode()


def piece_path(x, y, side, knobs=(1, 1, -1, -1)):
    """A jigsaw piece on the square at (x, y): knobs for the top, right, bottom and left edges, 1 out, -1 in."""
    shape = [((0.34, 0), (0.40, 0), (0.42, 0.02), (0.40, 0.08)),
             ((0.40, 0.08), (0.36, 0.18), (0.42, 0.27), (0.50, 0.27)),
             ((0.50, 0.27), (0.58, 0.27), (0.64, 0.18), (0.60, 0.08)),
             ((0.60, 0.08), (0.58, 0.02), (0.60, 0), (0.66, 0))]
    corners = [(x, y), (x + side, y), (x + side, y + side), (x, y + side)]
    normals = [(0, -1), (1, 0), (0, 1), (-1, 0)]
    parts = [f"M{x:.1f} {y:.1f}"]
    for edge in range(4):
        (ax, ay), (bx, by) = corners[edge], corners[(edge + 1) % 4]
        nx, ny = normals[edge]
        sign = knobs[edge]

        def at(u, v):
            return (ax + u * (bx - ax) + v * sign * side * nx, ay + u * (by - ay) + v * sign * side * ny)

        parts.append("L{:.1f} {:.1f}".format(*at(0.34, 0)))
        for _, c1, c2, end in shape:
            p1, p2, p3 = at(*c1), at(*c2), at(*end)
            parts.append(f"C{p1[0]:.1f} {p1[1]:.1f} {p2[0]:.1f} {p2[1]:.1f} {p3[0]:.1f} {p3[1]:.1f}")
        parts.append("L{:.1f} {:.1f}".format(*at(1, 0)))
    return " ".join(parts) + "Z"


def frame(x, y, w, h, band, inner):
    """A stepped gilt frame drawn round an inner window; returns the markup."""
    return f"""
<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="14" fill="url(#gilt)" stroke="#5E3305" stroke-width="7" filter="url(#shadow)"/>
<rect x="{x + 14}" y="{y + 14}" width="{w - 28}" height="{h - 28}" rx="9" fill="none" stroke="#FFF6D2" stroke-opacity=".75" stroke-width="5"/>
<rect x="{x + band - 12}" y="{y + band - 12}" width="{w - 2 * band + 24}" height="{h - 2 * band + 24}" rx="6" fill="url(#gilt-side)" stroke="#8A5512" stroke-width="4"/>
<rect x="{x + band - 12}" y="{y + band - 12}" width="{w - 2 * band + 24}" height="{h - 2 * band + 24}" rx="6" fill="none" stroke="#FFF1B4" stroke-width="5" stroke-dasharray="2 12" stroke-linecap="round"/>
{inner}
<rect x="{x + band}" y="{y + band}" width="{w - 2 * band}" height="{h - 2 * band}" fill="none" stroke="#3A1E06" stroke-width="6"/>"""


def page(body):
    return f"""<!doctype html><html><head><meta charset="utf-8"><style>html,body{{margin:0;background:#000}}svg{{display:block}}</style></head><body>
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {SIZE} {SIZE}" width="{SIZE}" height="{SIZE}"><defs>{GILT}{BACKGROUND}</defs>
<rect width="{SIZE}" height="{SIZE}" fill="url(#wall)"/><rect width="{SIZE}" height="{SIZE}" fill="url(#damask)"/><ellipse cx="512" cy="470" rx="470" ry="420" fill="url(#glow)"/>
{body}</svg></body></html>"""


def wave_in_frame(painting, crop, piece_side=210):
    """A: the painting in a gilt frame with one jigsaw piece lifted out of it."""
    fx, fy, fw, fh, band = 168, 150, 688, 688, 74
    wx, wy, ww, wh = fx + band, fy + band, fw - 2 * band, fh - 2 * band
    px, py = wx + 120, wy + wh - piece_side - 36
    hole = piece_path(px, py, piece_side)
    cx, cy, cw = crop
    image = f'<image href="{painting}" x="{wx - cx}" y="{wy - cy}" width="{cw}" preserveAspectRatio="xMidYMid slice"/>'
    inner = f"""<clipPath id="window"><rect x="{wx}" y="{wy}" width="{ww}" height="{wh}"/></clipPath>
<g clip-path="url(#window)">{image}<path d="{hole}" fill="#24140A"/><path d="{hole}" fill="none" stroke="#000" stroke-opacity=".55" stroke-width="10"/></g>"""
    lifted = f"""<clipPath id="piece"><path d="{hole}"/></clipPath>
<g transform="translate(250 70) rotate(13 {px + piece_side / 2} {py + piece_side / 2})" filter="url(#shadow)">
<path d="{hole}" fill="#FFF8E7" stroke="#FFF8E7" stroke-width="22" stroke-linejoin="round"/>
<g clip-path="url(#piece)">{image}</g>
<path d="{hole}" fill="none" stroke="#5E3305" stroke-width="5" stroke-linejoin="round"/></g>"""
    return page(frame(fx, fy, fw, fh, band, inner) + lifted)


def big_piece(painting, crop):
    """B: one big jigsaw piece cut from the painting, edged in gilt."""
    side = 560
    x, y = (SIZE - side) / 2, (SIZE - side) / 2 + 24
    path = piece_path(x, y, side, (1, 1, -1, -1))
    cx, cy, cw = crop
    image = f'<image href="{painting}" x="{x - cx}" y="{y - cy}" width="{cw}" preserveAspectRatio="xMidYMid slice"/>'
    return page(f"""<clipPath id="piece"><path d="{path}"/></clipPath>
<g filter="url(#shadow)"><path d="{path}" fill="url(#gilt)" stroke="url(#gilt)" stroke-width="44" stroke-linejoin="round"/></g>
<path d="{path}" fill="none" stroke="#5E3305" stroke-width="50" stroke-linejoin="round" stroke-opacity=".9"/>
<path d="{path}" fill="none" stroke="url(#gilt)" stroke-width="38" stroke-linejoin="round"/>
<path d="{path}" fill="none" stroke="#FFF1B4" stroke-width="6" stroke-linejoin="round" stroke-dasharray="2 14" stroke-linecap="round"/>
<g clip-path="url(#piece)">{image}</g>
<path d="{path}" fill="none" stroke="#3A1E06" stroke-width="7" stroke-linejoin="round"/>""")


def gems_in_frame(gems):
    """C: the game's three gems on a cream mat in a gilt frame."""
    fx, fy, fw, fh, band = 64, 196, 896, 632, 68
    wx, wy, ww, wh = fx + band, fy + band, fw - 2 * band, fh - 2 * band
    inner = f'<rect x="{wx}" y="{wy}" width="{ww}" height="{wh}" fill="#F6EBCD"/><rect x="{wx}" y="{wy}" width="{ww}" height="{wh}" fill="url(#glow)"/>'
    size = 318
    placed = []
    for index, gem in enumerate(gems):
        gx = wx + 8 + index * (size - 60)
        gy = wy + (wh - size) / 2 + (10 if index != 1 else -16)
        placed.append(f'<image href="{gem}" x="{gx}" y="{gy}" width="{size}" height="{size}" filter="url(#soft)"/>')
    sparkle = '<path d="M800 250l14 40 40 14-40 14-14 40-14-40-40-14 40-14z" fill="#FFF8E0"/><path d="M236 760l9 26 26 9-26 9-9 26-9-26-26-9 26-9z" fill="#FFF8E0" fill-opacity=".9"/>'
    return page(frame(fx, fy, fw, fh, band, inner + "".join(placed)) + sparkle)


def render(html_path, png_path, profile):
    subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", f"--user-data-dir={profile}",
                    f"--window-size={SIZE},{SIZE}", f"--screenshot={png_path}", "--virtual-time-budget=3000",
                    html_path.as_uri()], check=True, capture_output=True, timeout=120)


def main(out):
    out.mkdir(parents=True, exist_ok=True)
    paintings = REPO / "unity/Assets/Resources/Paintings"
    wave = data_uri(paintings / "room-11/great-wave.jpg")
    lilies = data_uri(paintings / "room-01/water-lilies.jpg")
    gems = [data_uri(REPO / f"unity/Assets/Art/Pieces/gem-{c}.png", 512) for c in ("red", "yellow", "blue")]
    designs = {
        "A-dalga-cerceve": wave_in_frame(wave, (240, 30, 1100)),
        "B-dalga-parca": big_piece(wave, (150, 40, 1300)),
        "C-taslar-cerceve": gems_in_frame(gems),
        "D-nilufer-cerceve": wave_in_frame(lilies, (60, 60, 700)),
    }
    profile = out / ".chrome-profile"
    try:
        for name, html in designs.items():
            html_path = out / f"{name}.html"
            html_path.write_text(html, encoding="utf-8")
            render(html_path, out / f"{name}.png", profile)
            print(name, "rendered")
    finally:
        shutil.rmtree(profile, ignore_errors=True)


if __name__ == "__main__":
    main(Path(sys.argv[1]).resolve())
