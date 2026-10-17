"""Bakes the town screen's backdrop. Run from the project folder: python Tools/bake_town.py  (needs Pillow and numpy)

Into Assets/Art/UI/Town/ (1920x1080 unless noted; the Town scene layers them, TownScenery moves them):
  TownBackground.png       a night village under the stars (hills, houses with dark windows, a watchtower and the
                           Shaman's spire with its crystal, the cracked ground), lit like the dungeon: dark and cold,
                           warm light around the windows, violet from the spire and the cracks, a warm light on each
                           banner; a vignette
  TownWindows.png          the window lights alone (flicker)
  TownCracks.png           the cracks' crystal light alone (pulses)
  Banner_<leader>.png      400x640: each leader's banner (cloth in their color, their portrait faintly on it), lit the
                           same way; Banner_<leader>_Gone.png: grey, for a wiped-out faction

The leaders (names, colors) and the banner spacing are copied from TownConfig / TownSceneBuilder below; the window and
landmark spots are fixed (seeded), so a re-bake gives the same village.
"""
import math
import os
import random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = "Assets/Art/UI/Town"
PORTRAITS = "Assets/Art/NPCs"
W, H = 1920, 1080
LEADERS = [("Guard", (1.0, 0.8, 0.3)), ("Shaman", (0.65, 0.45, 0.95)), ("Elder", (0.85, 0.65, 0.35))]  # TownConfig
BANNER_X = [W / 2 + (i - 1) * 560 for i in range(3)]  # over the leader buttons (520 wide, 40 apart)
BANNER_W, BANNER_H = 400, 640
GROUND = 880
WINDOW_CORE, WINDOW_GLOW = (255, 215, 140), (255, 160, 60)
CRACK_CORE, CRACK_GLOW = (205, 170, 255), (160, 90, 255)


def gradient(stops):
    t = np.linspace(0, 1, H, dtype=np.float32)
    column = np.stack([np.interp(t, [p for p, _ in stops], [c[i] for _, c in stops]) for i in range(3)], axis=1)
    return Image.fromarray(np.broadcast_to(column[:, None, :], (H, W, 3)).astype(np.uint8), "RGB").convert("RGBA")


def blurred(draw_shapes, blur):
    layer = Image.new("RGBA", (W, H))
    draw_shapes(ImageDraw.Draw(layer))
    return layer.filter(ImageFilter.GaussianBlur(blur))


def village():
    """The unlit backdrop (dark windows, no crack light), and the window rects and crack lines for the other layers."""
    rng = random.Random(4)
    image = gradient([(0, (8, 7, 20)), (0.45, (24, 18, 48)), (0.68, (58, 34, 86)), (0.75, (40, 24, 62)), (1, (12, 9, 18))])
    draw = ImageDraw.Draw(image)
    for _ in range(170):  # the stars that stay put (TownScenery adds twinkling ones), fewer toward the horizon
        x, y = rng.random() * W, (rng.random() ** 1.6) * H * 0.62
        v = rng.randint(90, 230)
        r = rng.choice((0.8, 1, 1, 1.4, 2))
        draw.ellipse((x - r, y - r, x + r, y + r), fill=(v, v, min(255, v + 25), 255))

    def ridge(base, height, roughness, color, seed):
        r = random.Random(seed)
        points, x, y = [(0, H)], 0, base
        while x <= W + 40:
            points.append((x, y))
            x += r.randint(40, 110)
            y = min(base, max(base - height, y + r.uniform(-roughness, roughness)))
        points.append((W, H))
        draw.polygon(points, fill=color)

    ridge(770, 160, 60, (34, 22, 52, 255), 1)  # far mountains
    ridge(800, 90, 40, (24, 16, 38, 255), 2)   # nearer hills

    house = (13, 9, 20, 255)
    windows = []
    x = -20
    while x < W:  # houses with gable roofs and some chimneys
        w, h = rng.randint(90, 170), rng.randint(70, 130)
        top = GROUND - h
        draw.rectangle((x, top, x + w, GROUND), fill=house)
        draw.polygon([(x - 10, top), (x + w / 2, top - rng.randint(45, 80)), (x + w + 10, top)], fill=house)
        if rng.random() < 0.5:
            cx = x + w * rng.uniform(0.2, 0.7)
            draw.rectangle((cx, top - 55, cx + 14, top), fill=house)
        for _ in range(rng.randint(1, 3)):
            wx, wy = x + rng.uniform(15, w - 30), top + rng.uniform(18, h - 35)
            windows.append((wx, wy, wx + 14, wy + 18))
        x += w + rng.randint(-15, 25)
    # the watchtower and the Shaman's spire, standing between the banners
    draw.rectangle((645, 640, 715, GROUND), fill=house)
    draw.polygon([(630, 640), (680, 575), (730, 640)], fill=house)
    windows.append((671, 665, 689, 690))
    draw.rectangle((1215, 680, 1265, GROUND), fill=house)
    draw.polygon([(1203, 680), (1240, 580), (1277, 680)], fill=house)
    windows.append((1232, 715, 1248, 740))
    draw.rectangle((0, GROUND, W, H), fill=(10, 7, 15, 255))
    for rect in windows:
        draw.rectangle(rect, fill=(70, 42, 24, 255))  # dark glass under the window lights

    cracks = []
    for start in (250, 760, 1350, 1700):
        points, cx, cy = [], start, GROUND + 8
        while cy < H:
            points.append((cx, cy))
            cx += rng.uniform(-40, 40)
            cy += rng.uniform(25, 60)
        points.append((cx, H))
        cracks.append(points)
    for crack in cracks:
        draw.line(crack, fill=(40, 26, 60, 255), width=3)  # the cracks, dark (their light is TownCracks)
    return image, windows, cracks


def light(windows, cracks):
    """The dungeon-like lighting: a cold dim ambient plus warm / violet light pools (multiplies the colors)."""
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    field = np.zeros((H, W, 3), np.float32) + np.array([0.30, 0.32, 0.45], np.float32)

    def add(x, y, radius, rgb, strength):
        d = np.sqrt((xx - x) ** 2 + (yy - y) ** 2) / radius
        field[...] += (np.clip(1 - d, 0, 1) ** 1.6 * strength)[..., None] * np.array(rgb, np.float32)

    for a, b, c, e in windows:
        add((a + c) / 2, (b + e) / 2, 140, (1.0, 0.65, 0.3), 0.7)
    add(1240, 580, 420, (0.7, 0.45, 1.0), 1.0)  # the spire's crystal
    for crack in cracks:
        for px, py in crack[::2]:
            add(px, py, 170, (0.6, 0.35, 1.0), 0.45)
    for x in BANNER_X:
        add(x, 180, 420, (1.0, 0.75, 0.45), 0.9)  # the banners
    return field


def vignette_factor(strength=0.5):
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    d = np.sqrt(((xx - W / 2) / (W / 2)) ** 2 + ((yy - H / 2) / (H / 2)) ** 2) / math.sqrt(2)
    return 1 - strength * np.clip((d - 0.35) / 0.65, 0, 1) ** 1.5


def shade(image, field, k):
    """`image` (W x H, RGBA) lit by `field` and darkened by the vignette `k`."""
    pixels = np.asarray(image, np.float32).copy()
    pixels[..., :3] = np.clip(pixels[..., :3] * field * k[..., None], 0, 255)
    return Image.fromarray(pixels.astype(np.uint8), "RGBA")


def fade_alpha(image, k):
    pixels = np.asarray(image, np.float32).copy()
    pixels[..., 3] *= k
    return Image.fromarray(pixels.astype(np.uint8), "RGBA")


def banner(name, color):
    """The cloth (with a notched tail, a trim and a hem in a lighter shade of the leader's color, rounder in the
    middle), the leader's portrait faintly on it."""
    cloth = Image.new("RGBA", (BANNER_W, BANNER_H))
    d = ImageDraw.Draw(cloth)
    base = tuple(int(15 + 70 * k) for k in color)
    d.polygon([(0, 0), (BANNER_W, 0), (BANNER_W, BANNER_H), (BANNER_W / 2, BANNER_H - 70), (0, BANNER_H)], fill=base + (255,))
    trim = tuple(int(40 + 120 * k) for k in color) + (255,)
    d.line([(10, 0), (10, BANNER_H - 8)], fill=trim, width=4)
    d.line([(BANNER_W - 10, 0), (BANNER_W - 10, BANNER_H - 8)], fill=trim, width=4)
    d.line([(10, BANNER_H - 8), (BANNER_W / 2, BANNER_H - 76)], fill=trim, width=4)
    d.line([(BANNER_W / 2, BANNER_H - 76), (BANNER_W - 10, BANNER_H - 8)], fill=trim, width=4)
    d.rectangle((0, 0, BANNER_W, 26), fill=trim)
    pixels = np.asarray(cloth, np.float32).copy()
    pixels[..., :3] *= (0.7 + 0.3 * np.sin(np.linspace(0, np.pi, BANNER_W, dtype=np.float32)))[None, :, None]
    cloth = Image.fromarray(pixels.astype(np.uint8), "RGBA")

    mask = cloth.getchannel("A")
    portrait = Image.open(f"{PORTRAITS}/Portrait_{name}.png").convert("RGBA")
    portrait = portrait.resize((360, round(portrait.height * 360 / portrait.width)), Image.LANCZOS)
    r, g, b, a = portrait.split()
    tint = [0.6 + 0.4 * k for k in color]
    r, g, b = (c.point(lambda v, t=t: int(v * t)) for c, t in zip((r, g, b), tint))
    a = a.point(lambda v: int(v * 0.33))
    cloth.alpha_composite(Image.merge("RGBA", (r, g, b, a)), (20, 40))
    cloth.putalpha(mask)
    return cloth


def grey(image):
    """Grey and dim (a wiped-out faction)."""
    pixels = np.asarray(image, np.float32).copy()
    level = (pixels[..., 0] * 0.299 + pixels[..., 1] * 0.587 + pixels[..., 2] * 0.114) * 0.6
    pixels[..., 0], pixels[..., 1], pixels[..., 2] = level, level, level * 1.04
    return Image.fromarray(np.clip(pixels, 0, 255).astype(np.uint8), "RGBA")


def main():
    os.makedirs(OUT, exist_ok=True)
    image, windows, cracks = village()
    field, k = light(windows, cracks), vignette_factor()

    background = shade(image, field, k)
    ImageDraw.Draw(background).polygon([(1240, 556), (1252, 580), (1240, 604), (1228, 580)], fill=(225, 200, 255, 255))
    background.alpha_composite(blurred(lambda d: d.ellipse((1205, 545, 1275, 615), fill=(170, 110, 255, 120)), 18))
    background.convert("RGB").save(f"{OUT}/TownBackground.png")

    lights = blurred(lambda d: [d.rectangle((a - 12, b - 12, c + 12, e + 12), fill=WINDOW_GLOW + (170,)) for a, b, c, e in windows], 12)
    cores = Image.new("RGBA", (W, H))
    for rect in windows:
        ImageDraw.Draw(cores).rectangle(rect, fill=WINDOW_CORE + (255,))
    lights.alpha_composite(cores)
    fade_alpha(lights, k).save(f"{OUT}/TownWindows.png")

    glow = blurred(lambda d: [d.line(c, fill=CRACK_GLOW + (200,), width=14) for c in cracks], 14)
    lines = Image.new("RGBA", (W, H))
    for crack in cracks:
        ImageDraw.Draw(lines).line(crack, fill=CRACK_CORE + (255,), width=3)
    glow.alpha_composite(lines)
    fade_alpha(glow, k).save(f"{OUT}/TownCracks.png")

    for (name, color), x in zip(LEADERS, BANNER_X):
        left = round(x - BANNER_W / 2)
        cloth = banner(name, color)
        pixels = np.asarray(cloth, np.float32).copy()  # lit like the rest, where it hangs
        pixels[..., :3] = np.clip(pixels[..., :3] * field[:BANNER_H, left:left + BANNER_W] * k[:BANNER_H, left:left + BANNER_W, None], 0, 255)
        lit = Image.fromarray(pixels.astype(np.uint8), "RGBA")
        lit.save(f"{OUT}/Banner_{name}.png")
        grey(lit).save(f"{OUT}/Banner_{name}_Gone.png")
    print("baked into", OUT)


if __name__ == "__main__":
    main()
