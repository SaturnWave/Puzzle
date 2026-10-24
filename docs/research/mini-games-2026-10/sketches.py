"""Idea sketches ("fikir çizimi") of the first mini-game picks, built only from the game's own art.

Usage: py -I sketches.py <repo root> <output jpg>

Reads the gem and special sprites (unity/Assets/Art/Pieces), gallery parts (unity/Assets/Art/Gallery),
paintings (unity/Assets/Resources/Paintings), the game's fonts (unity/Assets/Art/Fonts) and the colours of
the app icon (art/icon/tools/palette.py). Nothing here is the final look of a mini-game.
"""
import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(sys.argv[1])
OUT = Path(sys.argv[2])
PIECES = ROOT / "unity/Assets/Art/Pieces"
GALLERY = ROOT / "unity/Assets/Art/Gallery"
PAINTINGS = ROOT / "unity/Assets/Resources/Paintings"
FONTS = ROOT / "unity/Assets/Art/Fonts"

# Colours of the app icon (art/icon/tools/palette.py).
TEAL_LIGHT = (63, 178, 180)
TEAL = (31, 128, 136)
TEAL_DARK = (13, 74, 83)
CREAM = (255, 248, 230)
CREAM2 = (246, 228, 188)
SAND = (217, 183, 122)
WOOD = (201, 136, 69)
WOOD_DARK = (142, 84, 36)
WOOD_EDGE = (122, 67, 24)
BROWN = (78, 42, 14)
INK = (94, 51, 5)
RED = (168, 30, 34)
GOLD = (246, 185, 60)
GOLD_DARK = (168, 106, 18)

STAGES = 6  # stages per event in docs/research/mini-games.md


def font(name, size):
    return ImageFont.truetype(str(FONTS / name), size)


TITLE = font("YoungSerif-Regular.ttf", 50)
SUB = font("Gabarito-Bold.ttf", 27)
BODY = font("Gabarito-Bold.ttf", 27)
BODY_X = font("Gabarito-ExtraBold.ttf", 28)
SMALL = font("Gabarito-Bold.ttf", 23)
STAMP = font("YoungSerif-Regular.ttf", 25)
NUMBER = font("PaytoneOne-Regular.ttf", 46)
BUTTON = font("PaytoneOne-Regular.ttf", 30)
HEAD = font("YoungSerif-Regular.ttf", 66)
HEAD_SUB = font("Gabarito-Bold.ttf", 31)
WEEK = font("YoungSerif-Regular.ttf", 34)

_cache = {}


def sprite(folder, name):
    key = (folder, name)
    if key not in _cache:
        _cache[key] = Image.open(folder / f"{name}.png").convert("RGBA")
    return _cache[key]


def gem(colour, size):
    return sprite(PIECES, f"gem-{colour}").resize((int(size), int(size)), Image.LANCZOS)


def gallery(name, size=None):
    image = sprite(GALLERY, name)
    return image.resize((int(size[0]), int(size[1])), Image.LANCZOS) if size else image


def nine(name, width, height, border, target, trim=False):
    """Nine-slice a gallery sprite to width x height; corners keep their shape.

    trim cuts the sprite to its visible part first (the plank has an empty band above it).
    """
    src = sprite(GALLERY, name)
    if trim:
        src = src.crop(src.getchannel("A").getbbox())
    w0, h0 = src.size
    b, t = border, target
    width, height = int(width), int(height)
    out = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    xs = [(0, b, 0, t), (b, w0 - b, t, width - t), (w0 - b, w0, width - t, width)]
    ys = [(0, b, 0, t), (b, h0 - b, t, height - t), (h0 - b, h0, height - t, height)]
    for sx0, sx1, dx0, dx1 in xs:
        for sy0, sy1, dy0, dy1 in ys:
            if dx1 <= dx0 or dy1 <= dy0:
                continue
            part = src.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0), Image.LANCZOS)
            out.paste(part, (dx0, dy0), part)
    return out


def painting(room, name, width, height):
    image = Image.open(PAINTINGS / room / f"{name}.jpg").convert("RGB")
    w0, h0 = image.size
    want = width / height
    if w0 / h0 > want:
        crop = int(h0 * want)
        image = image.crop(((w0 - crop) // 2, 0, (w0 - crop) // 2 + crop, h0))
    else:
        crop = int(w0 / want)
        image = image.crop((0, (h0 - crop) // 2, w0, (h0 - crop) // 2 + crop))
    return image.resize((int(width), int(height)), Image.LANCZOS)


def put(canvas, image, x, y):
    canvas.paste(image, (int(x), int(y)), image if image.mode == "RGBA" else None)


def centred(draw, text, cx, y, face, fill):
    box = draw.textbbox((0, 0), text, font=face)
    draw.text((cx - (box[2] - box[0]) / 2 - box[0], y), text, font=face, fill=fill)


def wrapped(draw, text, x, y, width, face, fill, gap=8):
    words, line, lines = text.split(), "", []
    for word in words:
        trial = f"{line} {word}".strip()
        if draw.textlength(trial, font=face) <= width:
            line = trial
        else:
            lines.append(line)
            line = word
    lines.append(line)
    height = face.size + gap
    for i, part in enumerate(lines):
        draw.text((x, y + i * height), part, font=face, fill=fill)
    return y + len(lines) * height


def arrow(draw, points, colour=GOLD, edge=INK, width=12, head=30):
    draw.line(points, fill=edge, width=width + 6, joint="curve")
    draw.line(points, fill=colour, width=width, joint="curve")
    (x0, y0), (x1, y1) = points[-2], points[-1]
    angle = math.atan2(y1 - y0, x1 - x0)
    tip = (x1 + math.cos(angle) * head * 0.6, y1 + math.sin(angle) * head * 0.6)
    left = (x1 + math.cos(angle + 2.5) * head, y1 + math.sin(angle + 2.5) * head)
    right = (x1 + math.cos(angle - 2.5) * head, y1 + math.sin(angle - 2.5) * head)
    draw.polygon([tip, left, right], fill=colour, outline=edge, width=3)


def curve(p0, p1, p2, steps=24):
    return [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t ** 2 * p2[0],
             (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t ** 2 * p2[1])
            for t in (i / steps for i in range(steps + 1))]


def plaque(canvas, x, y, width, height):
    """A cream plaque with a gilt rim and a soft shadow, drawn in the icon's colours."""
    shadow = Image.new("RGBA", (int(width) + 40, int(height) + 40), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle((20, 26, width + 20, height + 26), radius=30, fill=(5, 30, 34, 120))
    shadow = shadow.filter(ImageFilter.GaussianBlur(9))
    put(canvas, shadow, x - 20, y - 20)
    draw = ImageDraw.Draw(canvas)
    draw.rounded_rectangle((x, y, x + width, y + height), radius=30, fill=GOLD_DARK)
    draw.rounded_rectangle((x + 5, y + 5, x + width - 5, y + height - 5), radius=26, fill=GOLD)
    draw.rounded_rectangle((x + 13, y + 13, x + width - 13, y + height - 13), radius=20, fill=CREAM, outline=SAND, width=2)


def pips(canvas, x, y, won, total=STAGES, size=(40, 30), gap=42):
    for i in range(total):
        put(canvas, gallery("pip-on" if i < won else "pip-off", size), x + i * gap, y)


PW, PH = 740, 1400
CAPTION_TOP = PH - 252


def panel(title, subtitle, caption):
    """A phone-shaped panel: teal velvet wall, the idea stamp, a nameplate title and a caption plaque."""
    velvet = sprite(GALLERY, "velvet")
    damask = sprite(GALLERY, "damask")
    canvas = Image.new("RGBA", (PW, PH), TEAL)
    for x in range(0, PW, velvet.width):
        for y in range(0, PH, velvet.height):
            canvas.paste(velvet, (x, y))
    faint = damask.resize((512, 512), Image.LANCZOS)
    faint.putalpha(faint.getchannel("A").point(lambda a: int(a * 0.35)))
    for x in range(0, PW, 512):
        for y in range(0, PH, 512):
            put(canvas, faint, x, y)
    shade = Image.new("RGBA", (PW, PH), (0, 0, 0, 0))
    ImageDraw.Draw(shade).rectangle((0, PH - 290, PW, PH), fill=(5, 30, 34, 90))
    canvas.alpha_composite(shade)
    draw = ImageDraw.Draw(canvas)

    put(canvas, nine("pill", 236, 56, 70, 26), 24, 20)
    centred(draw, "fikir çizimi", 24 + 118, 33, STAMP, RED)

    put(canvas, nine("nameplate", 660, 112, 80, 44), (PW - 660) / 2, 90)
    centred(draw, title, PW / 2, 113, TITLE, INK)
    centred(draw, subtitle, PW / 2, 214, SUB, CREAM)

    plaque(canvas, 30, CAPTION_TOP, PW - 60, 222)
    draw = ImageDraw.Draw(canvas)
    wrapped(draw, caption, 62, CAPTION_TOP + 34, PW - 124, BODY, INK, gap=9)
    return canvas, draw


def tube(canvas, draw, x, top, content, tw=104, th=330, size=74, step=76):
    glass = Image.new("RGBA", (tw, th), (0, 0, 0, 0))
    g = ImageDraw.Draw(glass)
    g.rounded_rectangle((4, 18, tw - 4, th - 4), radius=46, fill=(255, 248, 230, 70), outline=(255, 248, 230, 230), width=6)
    g.rounded_rectangle((17, 44, 31, th - 64), radius=7, fill=(255, 255, 255, 90))
    put(canvas, glass, x, top)
    draw.rounded_rectangle((x + 10, top, x + tw - 10, top + 26), radius=8, fill=WOOD, outline=WOOD_EDGE, width=3)
    for level, colour in enumerate(content):
        put(canvas, gem(colour, size), x + (tw - size) / 2, top + th - 16 - step * (level + 1))


def tube_sorting():
    canvas, draw = panel(
        "Boya Tüpleri", "tüp sıralama · Magic Sort! türü",
        "Üstteki taşı aynı renkli taşın üstüne ya da boş tüpe taşı. Her tüp tek renk olunca aşama biter. "
        "Süre yok, kayıp yok, geri almak serbest.")
    # Bottom to top. Every colour has four units; tube 3's red is in the hand.
    row1 = [["green", "red", "yellow", "blue"], ["yellow", "purple", "red"], ["blue", "green", "purple"], ["red"]]
    row2 = [["blue", "yellow", "green", "purple"], ["green", "blue"], ["yellow", "purple"]]
    tw = 104
    spots = []
    for top, row in ((330, row1), (700, row2)):
        gap = (PW - len(row) * tw) / (len(row) + 1)
        for i, content in enumerate(row):
            x = gap + i * (tw + gap)
            spots.append((x, top))
            tube(canvas, draw, x, top, content)
    (x3, y3), (x4, y4) = spots[2], spots[3]
    put(canvas, gem("red", 82), x3 + 11, y3 - 76)
    arrow(draw, curve((x3 + 92, y3 - 40), ((x3 + x4) / 2 + 40, y3 - 96), (x4 + 50, y4 - 6)), width=9, head=24)
    put(canvas, nine("plank", PW - 100, 84, 100, 26, trim=True), 50, 1050)
    put(canvas, gallery("disc-teal", (66, 66)), 70, 1058)
    draw.arc((88, 1076, 118, 1106), 200, 520, fill=CREAM, width=5)
    draw.text((146, 1070), "geri al", font=BODY_X, fill=CREAM)
    draw.text((286, 1070), "aşama 3 / 6", font=BODY_X, fill=CREAM)
    pips(canvas, 444, 1074, 2, size=(34, 26), gap=36)
    return canvas


def pigment_recipe():
    canvas, draw = panel(
        "Pigment Tarifi", "renk sırası · Magic Cauldron türü",
        "Ustanın dört pigmentlik sırasını bul. Altın nokta: doğru renk, doğru yer. Mavi nokta: doğru renk, "
        "yanlış yer. Tarif herkese aynı; deneme sınırı yok.")
    left = (PW - 470) / 2
    put(canvas, nine("brass-plate", 470, 118, 90, 30), left, 268)
    for i in range(4):
        x = left + 26 + i * 108
        draw.rounded_rectangle((x, 286, x + 90, 368), radius=16, fill=CREAM, outline=INK, width=4)
        centred(draw, "?", x + 45, 292, NUMBER, INK)
    draw.text((60, 408), "4. deneme", font=BODY_X, fill=CREAM)
    # Secret: red, blue, green, yellow. Marks: (right place, right colour in the wrong place).
    guesses = [(["yellow", "red", "purple", "blue"], 0, 3), (["red", "yellow", "blue", "orange"], 1, 2),
               (["red", "blue", "yellow", "green"], 2, 2)]
    for row, (colours, exact, near) in enumerate(guesses):
        y = 450 + row * 112
        plaque(canvas, 60, y, 620, 98)
        for i, colour in enumerate(colours):
            put(canvas, gem(colour, 72), 90 + i * 96, y + 13)
        marks = ["exact"] * exact + ["near"] * near
        marks += ["none"] * (4 - len(marks))
        for i, mark in enumerate(marks):
            cx, cy = 528 + (i % 2) * 52, y + 16 + (i // 2) * 36
            if mark == "none":
                draw.ellipse((cx + 9, cy + 9, cx + 21, cy + 21), fill=SAND)
            else:
                fill = GOLD if mark == "exact" else TEAL_LIGHT
                draw.ellipse((cx, cy, cx + 30, cy + 30), fill=fill, outline=INK, width=4)
    y = 450 + 3 * 112
    plaque(canvas, 60, y, 620, 98)
    for i, colour in enumerate(["red", "blue", None, None]):
        x = 90 + i * 96
        if colour:
            put(canvas, gem(colour, 72), x, y + 13)
        else:
            draw.rounded_rectangle((x + 2, y + 16, x + 72, y + 82), radius=14, outline=GOLD_DARK, width=4)
    put(canvas, nine("button", 150, 74, 150, 34), 512, y + 12)
    centred(draw, "Dene", 587, y + 26, BUTTON, CREAM)
    put(canvas, nine("tray", 640, 128, 130, 40), 50, 952)
    for i, colour in enumerate(["red", "orange", "yellow", "green", "blue", "purple"]):
        put(canvas, gem(colour, 78), 88 + i * 96, 978)
    arrow(draw, curve((330, 968), (330, 920), (300, 905)), width=8, head=22)
    return canvas


def storeroom_jam():
    canvas, draw = panel(
        "Depo Sıkışıklığı", "kayan sandıklar · Rush Hour kuralı",
        "Sandıklar yalnız kendi yönünde kayar. Tablonun sandığını sağdaki kapıdan çıkar. "
        "En kısa çözümü bilgisayar önceden bulur; bu örnek 5 hamlede biter.")
    cell, n = 96, 6
    x0, y0 = (PW - cell * n) / 2, 300
    draw.rounded_rectangle((x0 - 18, y0 - 18, x0 + cell * n + 18, y0 + cell * n + 18), radius=22, fill=WOOD_EDGE, outline=BROWN, width=5)
    draw.rectangle((x0, y0, x0 + cell * n, y0 + cell * n), fill=WOOD_DARK)
    for i in range(n + 1):
        draw.line((x0 + i * cell, y0, x0 + i * cell, y0 + cell * n), fill=BROWN, width=2)
        draw.line((x0, y0 + i * cell, x0 + cell * n, y0 + i * cell), fill=BROWN, width=2)
    door_y = y0 + 2 * cell
    draw.rectangle((x0 + cell * n - 2, door_y + 8, x0 + cell * n + 20, door_y + cell - 8), fill=TEAL_DARK)
    arrow(draw, [(x0 + cell * n + 6, door_y + cell / 2), (x0 + cell * n + 52, door_y + cell / 2)], width=10, head=24)

    def crate(col, row, length, horizontal, art=None):
        """A crate drawn to fill its cells: wood face, grain along its length, brass studs."""
        w = cell * (length if horizontal else 1) - 12
        h = cell * (1 if horizontal else length) - 12
        x, y = x0 + col * cell + 6, y0 + row * cell + 6
        draw.rounded_rectangle((x + 3, y + 6, x + w + 3, y + h + 6), radius=14, fill=BROWN)
        draw.rounded_rectangle((x, y, x + w, y + h), radius=14, fill=WOOD_EDGE)
        draw.rounded_rectangle((x + 6, y + 6, x + w - 6, y + h - 6), radius=10, fill=WOOD)
        if horizontal:
            for k in (1, 2):
                gy = y + 6 + (h - 12) * k / 3
                draw.line((x + 14, gy, x + w - 14, gy), fill=WOOD_DARK, width=3)
        else:
            for k in (1, 2):
                gx = x + 6 + (w - 12) * k / 3
                draw.line((gx, y + 14, gx, y + h - 14), fill=WOOD_DARK, width=3)
        draw.rounded_rectangle((x + 6, y + 6, x + w - 6, y + 16), radius=6, fill=(233, 179, 119))
        stud = gallery("stud", (18, 18))
        if art is not None:
            draw.rectangle((x + 20, y + 14, x + w - 20, y + h - 14), fill=GOLD, outline=GOLD_DARK, width=3)
            pic = painting(*art, w - 56, h - 44)
            put(canvas, pic.convert("RGBA"), x + 28, y + 22)
        for sx, sy in ((x + 8, y + 8), (x + w - 26, y + 8), (x + 8, y + h - 26), (x + w - 26, y + h - 26)):
            put(canvas, stud, sx, sy)

    crate(0, 2, 2, True, art=("room-11", "red-fuji"))
    crate(2, 0, 3, False)
    crate(1, 5, 3, True)
    crate(4, 2, 2, False)
    crate(3, 0, 2, True)
    crate(5, 3, 2, False)
    crate(0, 3, 2, True)
    arrow(draw, [(x0 + 2.5 * cell, y0 + 5.5 * cell), (x0 + 4.2 * cell, y0 + 5.5 * cell)], colour=CREAM, width=7, head=20)
    draw.text((x0, y0 + cell * n + 34), "1. hamle: alttaki sandık sağa", font=BODY_X, fill=CREAM)
    draw.text((x0, y0 + cell * n + 74), "aşama 2 / 6", font=BODY_X, fill=CREAM)
    pips(canvas, x0 + 170, y0 + cell * n + 78, 1, size=(34, 26), gap=36)
    return canvas


def swap_jigsaw():
    canvas, draw = panel(
        "Takas Yapbozu", "kart takası · Jigsolitaire türü",
        "Tablo kartlara bölünüp karışır. İki karta dokun, yer değiştirsinler. Doğru yere oturan kart "
        "altın kenarla kilitlenir. Kendi tablolarımız; yeni çizim yok.")
    cols, rows, cell = 5, 3, 132
    pic = painting("room-11", "great-wave", cols * cell, rows * cell)
    perm = list(range(cols * rows))
    for a, b in ((1, 7), (3, 12), (5, 9)):
        perm[a], perm[b] = perm[b], perm[a]
    x0, y0 = (PW - cols * cell) / 2, 320
    draw.rectangle((x0 - 14, y0 - 14, x0 + cols * cell + 14, y0 + rows * cell + 14), fill=WOOD_EDGE, outline=GOLD_DARK, width=4)
    for i, piece in enumerate(perm):
        sx, sy = (piece % cols) * cell, (piece // cols) * cell
        tile = pic.crop((sx, sy, sx + cell, sy + cell)).convert("RGBA")
        x, y = x0 + (i % cols) * cell, y0 + (i // cols) * cell
        if piece == i:
            canvas.paste(tile, (int(x), int(y)))
            draw.rectangle((x, y, x + cell - 1, y + cell - 1), outline=GOLD, width=3)
        else:
            small = tile.resize((cell - 14, cell - 14), Image.LANCZOS)
            draw.rectangle((x + 3, y + 3, x + cell - 3, y + cell - 3), fill=TEAL_DARK)
            canvas.paste(small, (int(x + 7), int(y + 7)))
            draw.rectangle((x + 6, y + 6, x + cell - 8, y + cell - 8), outline=CREAM, width=3)
    for i in (1, 7):
        x, y = x0 + (i % cols) * cell, y0 + (i // cols) * cell
        draw.rectangle((x + 2, y + 2, x + cell - 3, y + cell - 3), outline=GOLD, width=9)
    a = (x0 + 1.5 * cell + 20, y0 + 0.75 * cell)
    b = (x0 + 2.5 * cell - 10, y0 + 1.35 * cell)
    arrow(draw, curve(a, (x0 + 2.6 * cell, y0 + 0.55 * cell), b), width=9, head=24)
    y = y0 + rows * cell + 46
    draw.text((x0, y), "doğru yerde: 9 / 15", font=BODY_X, fill=CREAM)
    draw.text((x0 + 330, y), "aşama 4 / 6", font=BODY_X, fill=CREAM)
    pips(canvas, x0 + 330, y + 40, 3, size=(34, 26), gap=36)
    y += 96
    sample = pic.crop((0, 0, cell, cell)).resize((86, 86)).convert("RGBA")
    canvas.paste(sample, (int(x0), int(y)))
    draw.rectangle((x0, y, x0 + 85, y + 85), outline=GOLD, width=4)
    draw.text((x0 + 104, y + 26), "altın kenar: yerinde, kilitli", font=SMALL, fill=CREAM)
    canvas.paste(sample, (int(x0), int(y + 106)))
    draw.rectangle((x0 + 3, y + 109, x0 + 82, y + 188), outline=CREAM, width=3)
    draw.text((x0 + 104, y + 112), "krem kenar: henüz yerinde değil", font=SMALL, fill=CREAM)
    draw.text((x0 + 104, y + 146), "kartlar aşamadan aşamaya çoğalır", font=SMALL, fill=CREAM2)
    return canvas


def screw(canvas, colour, x, y, size):
    """A screw head: the brass stud with the gem's own shape on it, so colour is never the only clue."""
    put(canvas, gallery("stud", (size, size)), x, y)
    put(canvas, gem(colour, size * 0.72), x + size * 0.14, y + size * 0.12)


def frame_screws():
    canvas, draw = panel(
        "Çerçeve Vidaları", "vida sıralama · Screwdom türü",
        "Vidaya dokun: kendi renginin kutusuna gider, kutu yoksa bekleme yerine. Son tahta düşünce ödünç "
        "gelen tablo görünür. Bekleme yeri dolarsa aşama baştan; jeton gitmez.")
    boxes = [("red", 2), ("blue", 1), ("yellow", 0)]
    for i, (colour, filled) in enumerate(boxes):
        x = 52 + i * 216
        put(canvas, nine("tray", 204, 104, 130, 34), x, 262)
        put(canvas, gem(colour, 40), x + 82, 252)
        for slot in range(3):
            sx = x + 22 + slot * 56
            draw.ellipse((sx, 300, sx + 46, 346), fill=TEAL_DARK, outline=CREAM2, width=2)
            if slot < filled:
                screw(canvas, colour, sx + 1, 301, 44)
    fx, fy, fw, fh = 92, 400, 556, 520
    put(canvas, nine("picture-frame", fw, fh, 90, 50), fx, fy)
    pic = painting("room-16", "the-little-street", fw - 104, fh - 104)
    put(canvas, pic.convert("RGBA"), fx + 52, fy + 52)
    for top, screws in ((fy + 70, [("green", 0.12), ("blue", 0.88)]),
                        (fy + 230, [("purple", 0.12), ("red", 0.88)]),
                        (fy + 380, [("yellow", 0.12), ("green", 0.88)])):
        put(canvas, nine("plank", fw - 70, 92, 100, 26, trim=True), fx + 35, top)
        for colour, at in screws:
            screw(canvas, colour, fx + 35 + (fw - 70) * at - 26, top + 20, 52)
    diagonal = nine("plank", 560, 88, 100, 26, trim=True).rotate(28, expand=True, resample=Image.BICUBIC)
    put(canvas, diagonal, fx + 10, fy + 120)
    cx, cy = fx + 10 + diagonal.width / 2, fy + 120 + diagonal.height / 2
    for t in (-0.36, 0.36):
        screw(canvas, "red" if t < 0 else "yellow", cx + t * 560 * math.cos(math.radians(28)) - 26,
              cy - t * 560 * math.sin(math.radians(28)) - 26, 52)
    start = (cx - 0.36 * 494 + 4, cy + 0.36 * 263 - 34)
    arrow(draw, curve(start, (60, 560), (162, 362)), width=8, head=22)
    draw.text((60, 960), "bekleme yeri", font=BODY_X, fill=CREAM)
    for slot in range(5):
        sx = 238 + slot * 86
        draw.rounded_rectangle((sx, 944, sx + 70, 1008), radius=14, fill=TEAL_DARK, outline=CREAM2, width=3)
        if slot < 2:
            screw(canvas, ["green", "purple"][slot], sx + 9, 950, 52)
    draw.text((60, 1046), "aşama 5 / 6", font=BODY_X, fill=CREAM)
    pips(canvas, 230, 1050, 4, size=(34, 26), gap=36)
    return canvas


def common_frame():
    canvas, draw = panel(
        "Ortak çerçeve", "etkinlik kartı ve salondaki çerçeve",
        "Her mini oyun aynı kartı, aynı aşama sırasını ve aynı sonuç kartını kullanır. Kazanılan bölüm jeton "
        "verir, jeton aşama açar. Ödüller herkese aynı ve baştan belli.")
    plaque(canvas, 50, 262, 640, 560)
    draw = ImageDraw.Draw(canvas)
    put(canvas, nine("nameplate", 520, 84, 80, 34), 110, 280)
    centred(draw, "Atölye Haftası", PW / 2, 294, BODY_X, INK)
    put(canvas, gem("blue", 54), 92, 384)
    draw.text((156, 392), "Boya Tüpleri", font=BODY_X, fill=INK)
    draw.text((370, 396), "sırada: Takas Yapbozu", font=SMALL, fill=WOOD_EDGE)
    draw.text((92, 450), "Aşamalar ve ödülleri (herkese aynı):", font=SMALL, fill=INK)
    rewards = [("sp-rocket-h", "booster"), ("coins-pile", "altın"), ("sp-colorbomb", "15 dk"),
               ("sp-bomb", "booster"), ("coins-pile", "altın"), ("icon-piece", "parça")]
    for i, (icon, label) in enumerate(rewards):
        x = 96 + i * 92
        put(canvas, gallery("pip-on" if i < 2 else "pip-off", (52, 39)), x + 8, 490)
        folder = GALLERY if icon in ("coins-pile", "icon-piece") else PIECES
        size = (60, 60)
        put(canvas, sprite(folder, icon).resize(size, Image.LANCZOS), x + 4 + (60 - size[0]) / 2, 540)
        centred(draw, label, x + 34, 606, SMALL, INK)
    draw.text((92, 650), "Jeton: 5   ·   aşama başına: 3", font=BODY, fill=INK)
    draw.text((92, 690), "Bitişe: 2 gün 5 saat", font=BODY, fill=INK)
    put(canvas, nine("button", 220, 84, 150, 38), 260, 730)
    centred(draw, "Oyna", PW / 2, 748, BUTTON, CREAM)
    draw.text((80, 840), "Miktarları simülatör belirler.", font=SMALL, fill=CREAM2)

    put(canvas, gallery("workbench", (300, 251)), 400, 870)
    draw.rectangle((180, 912, 270, 1036), fill=CREAM2)
    put(canvas, gem("blue", 60), 195, 944)
    put(canvas, nine("frame-small", 116, 150, 150, 26), 167, 902)
    put(canvas, gallery("badge-red", (34, 34)), 262, 892)
    put(canvas, nine("pill", 180, 48, 70, 22), 135, 1060)
    centred(draw, "2 gün kaldı", 225, 1068, SMALL, INK)
    draw.text((52, 880), "salonda:", font=BODY_X, fill=CREAM)
    return canvas


def mark(panel_image, box):
    """A small mark of a kind for the calendar strip: its play area, scaled down."""
    crop = panel_image.crop(box).convert("RGBA")
    crop.thumbnail((110, 110), Image.LANCZOS)
    tile = Image.new("RGBA", (116, 116), TEAL_DARK + (255,))
    tile.paste(crop, ((116 - crop.width) // 2, (116 - crop.height) // 2), crop)
    ImageDraw.Draw(tile).rounded_rectangle((0, 0, 115, 115), radius=16, outline=GOLD, width=4)
    rounded = Image.new("L", (116, 116), 0)
    ImageDraw.Draw(rounded).rounded_rectangle((0, 0, 115, 115), radius=16, fill=255)
    tile.putalpha(rounded)
    return tile


def month_strip(width, marks):
    """The sample month of docs/research/mini-games.md §4: two workshop weeks, two quiet weeks."""
    height = 560
    strip = Image.new("RGBA", (width, height), CREAM2 + (255,))
    draw = ImageDraw.Draw(strip)
    draw.text((0, 0), "Örnek ay: Kasım 2026 (fikir çizimi)", font=WEEK, fill=INK)
    draw.text((0, 50), "Hafta, pazartesisinin düştüğü aya sayılır; saatler telefonun yerel saati. 5. hafta (30 Kasım) "
              "sessiz ya da mevsim etkinliği. Çerçeve Vidaları beşinci tür olarak sonra döngüye girer.",
              font=SMALL, fill=WOOD_EDGE)
    weeks = [
        ("1. hafta · 2–8 Kasım", "Atölye Haftası",
         [("Pzt–Per", ["tube", "swap"]), ("Cum–Paz", ["pigment", "jam"])]),
        ("2. hafta · 9–15 Kasım", "Sessiz hafta", None),
        ("3. hafta · 16–22 Kasım", "Atölye Haftası",
         [("Pzt–Per", ["swap", "tube"]), ("Cum–Paz", ["jam", "pigment"])]),
        ("4. hafta · 23–29 Kasım", "Sessiz hafta", None),
    ]
    names = {"tube": "Boya Tüpleri", "swap": "Takas Yapbozu", "pigment": "Pigment Tarifi", "jam": "Depo Sıkışıklığı"}
    gutter = 30
    card_w = (width - 3 * gutter) / 4
    for i, (when, kind, chains) in enumerate(weeks):
        x = i * (card_w + gutter)
        y = 100
        busy = chains is not None
        draw.rounded_rectangle((x, y, x + card_w, y + 440), radius=26,
                               fill=TEAL if busy else CREAM, outline=GOLD_DARK, width=4)
        draw.text((x + 22, y + 18), when, font=BODY_X, fill=CREAM if busy else INK)
        draw.text((x + 22, y + 56), kind, font=WEEK, fill=GOLD if busy else WOOD_EDGE)
        if not busy:
            wrapped(draw, "Mini oyun yok. Yalnız her gün açık olanlar: günün hediyesi, ileride sezon pasosu "
                    "ve günün sınavı.", x + 22, y + 120, card_w - 44, BODY, INK)
            continue
        for row, (days, kinds) in enumerate(chains):
            ry = y + 118 + row * 158
            draw.text((x + 22, ry + 44), days, font=BODY_X, fill=CREAM)
            for k, key in enumerate(kinds):
                mx = x + 166 + k * 202
                put(strip, marks[key], mx, ry)
                centred(draw, names[key], mx + 58, ry + 122, SMALL, CREAM)
            arrow(draw, [(x + 292, ry + 58), (x + 356, ry + 58)], width=7, head=18)
    return strip


def sheet():
    tube_panel, pigment_panel, jam_panel = tube_sorting(), pigment_recipe(), storeroom_jam()
    swap_panel, screw_panel, frame_panel = swap_jigsaw(), frame_screws(), common_frame()
    panels = [tube_panel, pigment_panel, swap_panel, jam_panel, screw_panel, frame_panel]
    marks = {
        "tube": mark(tube_panel, (40, 250, 700, 1040)),
        "pigment": mark(pigment_panel, (50, 440, 690, 900)),
        "swap": mark(swap_panel, (45, 305, 695, 735)),
        "jam": mark(jam_panel, (70, 280, 670, 900)),
    }
    margin, gutter, header, footer = 50, 40, 200, 70
    width = margin * 2 + 3 * PW + 2 * gutter
    strip = month_strip(width - 2 * margin, marks)
    height = header + 2 * PH + gutter + 60 + strip.height + footer
    out = Image.new("RGB", (width, height), CREAM2)
    draw = ImageDraw.Draw(out)
    draw.text((margin, 36), "Mini oyunlar: ilk seçim (fikir çizimi)", font=HEAD, fill=INK)
    draw.text((margin, 124), "Yalnız oyunun kendi taşları, galeri parçaları, tabloları, yazı tipleri ve simge paleti. "
              "Oyundaki son görünüş değildir: tüp, vida kutusu ve sandık çizimleri yeni yapılacak.",
              font=HEAD_SUB, fill=WOOD_EDGE)
    for i, image in enumerate(panels):
        x = margin + (i % 3) * (PW + gutter)
        y = header + (i // 3) * (PH + gutter)
        mask = Image.new("L", (PW, PH), 0)
        ImageDraw.Draw(mask).rounded_rectangle((0, 0, PW - 1, PH - 1), radius=48, fill=255)
        shadow = Image.new("RGBA", (PW + 60, PH + 60), (0, 0, 0, 0))
        ImageDraw.Draw(shadow).rounded_rectangle((30, 40, PW + 30, PH + 40), radius=48, fill=(60, 30, 5, 110))
        shadow = shadow.filter(ImageFilter.GaussianBlur(16))
        out.paste(shadow, (x - 30, y - 30), shadow)
        out.paste(image.convert("RGB"), (x, y), mask)
        draw.rounded_rectangle((x, y, x + PW - 1, y + PH - 1), radius=48, outline=GOLD_DARK, width=4)
    top = header + 2 * PH + gutter + 60
    out.paste(strip.convert("RGB"), (margin, top))
    draw.text((margin, height - 52), "out/research/mini-games-2026-10/sketches.jpg · 2026-10-24 · "
              "docs/research/mini-games.md §4 ve §6", font=SMALL, fill=WOOD_EDGE)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    out.save(OUT, quality=88, optimize=True)


if __name__ == "__main__":
    sheet()
