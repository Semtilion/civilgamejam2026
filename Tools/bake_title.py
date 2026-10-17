"""Bakes the title screen's art. Run from the project folder: python Tools/bake_title.py  (needs Pillow)

  Assets/Art/UI/Title.png            the game's name stacked (Depths / of the / CrystalFall) in IM Fell English SC
                                     (Assets/Fonts), thickened (the font has no bold) with a deep violet outline. Drawn
                                     at 2x: the Title scene shows it at half size, so it stays sharp on big screens.
  Assets/Art/UI/TitleBackground.png  dark violet, lighter toward the bottom, darker toward the corners (stretched over
                                     the screen; small, the gradient is smooth).

Then Jam > Build Game Scenes picks them up.
"""
import math
import random
from PIL import Image, ImageDraw, ImageFont

FONT = "Assets/Fonts/IMFellEnglishSC.ttf"
OUT = "Assets/Art/UI"
SCALE = 2                                                        # the title is drawn at 2x
LINES = [("Depths", 130), ("of the", 66), ("CrystalFall", 130)]  # (text, size at 1x)
GAPS = [18, 2]                                                   # between the lines, 1x
BOLD, OUTLINE = 2, 5                                             # stroke in the title color, then the outline, 1x
TITLE_COLOR = (209, 178, 255)                                    # GameBuilder.TitleColor
OUTLINE_COLOR = (80, 45, 135)
TOP_COLOR, BOTTOM_COLOR = (10, 8, 16), (38, 22, 60)              # the background
BACKGROUND_SIZE = (480, 270)


def round_up_4(value):
    return -(-value // 4) * 4  # texture compression likes multiples of 4


def title():
    fonts = [ImageFont.truetype(FONT, size * SCALE) for _, size in LINES]
    stroke = (BOLD + OUTLINE) * SCALE
    caps = [-font.getbbox("D", anchor="ls")[1] for font in fonts]  # the lines stack by their capitals
    width = max(font.getlength(text) for (text, _), font in zip(LINES, fonts)) + 2 * stroke + 40
    height = sum(caps) + sum(GAPS) * SCALE + 2 * stroke + 80
    outline, fill = Image.new("L", (int(width), int(height))), Image.new("L", (int(width), int(height)))
    baseline = stroke + 20
    for i, ((text, _), font) in enumerate(zip(LINES, fonts)):
        baseline += caps[i]
        for mask, thickness in ((outline, stroke), (fill, BOLD * SCALE)):
            ImageDraw.Draw(mask).text((width / 2, baseline), text, font=font, fill=255, anchor="ms", stroke_width=thickness, stroke_fill=255)
        if i < len(GAPS):
            baseline += GAPS[i] * SCALE
    image = Image.new("RGBA", outline.size, OUTLINE_COLOR + (0,))
    image.putalpha(outline)
    top = Image.new("RGBA", fill.size, TITLE_COLOR + (0,))
    top.putalpha(fill)
    image.alpha_composite(top)

    left, upper, right, lower = outline.getbbox()
    w, h = round_up_4(right - left + 8), round_up_4(lower - upper + 8)
    cx, cy = (left + right) / 2, (upper + lower) / 2
    image = image.crop((round(cx - w / 2), round(cy - h / 2), round(cx - w / 2) + w, round(cy - h / 2) + h))
    image.save(f"{OUT}/Title.png")
    print("Title.png", image.size)


def background():
    w, h = BACKGROUND_SIZE
    rng = random.Random(1)
    image = Image.new("RGB", (w, h))
    pixels = image.load()
    for y in range(h):
        t = (y / (h - 1)) ** 1.8
        base = [a + (b - a) * t for a, b in zip(TOP_COLOR, BOTTOM_COLOR)]
        for x in range(w):
            d = math.hypot((x - w / 2) / (w / 2), (y - h / 2) / (h / 2)) / math.sqrt(2)
            k = 1 - 0.5 * min(1.0, max(0.0, (d - 0.35) / 0.65)) ** 1.5  # vignette
            dither = rng.random() - 0.5  # no bands in the dark gradient
            pixels[x, y] = tuple(int(max(0, min(255, c * k + dither + 0.5))) for c in base)
    image.save(f"{OUT}/TitleBackground.png")
    print("TitleBackground.png", image.size)


if __name__ == "__main__":
    title()
    background()
