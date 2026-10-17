"""Bakes the Quake Hammer's ground cracks. Run from the project folder: python Tools/bake_cracks.py  (needs Pillow)

Into Assets/Art/Effects/QuakeCracks_0..3.png (512x512, transparent): four crack patterns. Each has CRACKS jagged cracks
running from the middle out to REACH px from it (glowing orange, a dark split, a hot core), ending exactly at
0, 40, 80 ... degrees (GroundCracks puts dust there), and a faint ring at that reach. Then Jam > Build Game Scenes.
"""
import math
import random
from PIL import Image, ImageDraw, ImageFilter

OUT = "Assets/Art/Effects"
SIZE = 512
REACH = 240        # px from the middle to a crack's end (GroundCracks.Reach = REACH / (SIZE / 2))
CRACKS = 9         # GroundCracks.Cracks
VARIANTS = 4
GLOW, SPLIT, CORE = (255, 150, 60), (30, 20, 16), (255, 200, 120)


def path(rng, angle):
    """A jagged crack from the middle to REACH, ending exactly on `angle` (radians)."""
    points, dist = [(0.0, 0.0)], 0.0
    while True:
        dist = min(1.0, dist + rng.uniform(0.08, 0.16))
        jag = 0.0 if dist >= 1.0 else rng.uniform(-0.12, 0.12)
        points.append((math.cos(angle + jag) * dist, math.sin(angle + jag) * dist))
        if dist >= 1.0:
            return points


def layer(paths, width, color, alpha, blur=0, ring=None):
    image = Image.new("RGBA", (SIZE, SIZE))
    draw = ImageDraw.Draw(image)
    c = SIZE / 2
    for points in paths:
        draw.line([(c + x * REACH, c - y * REACH) for x, y in points], fill=color + (alpha,), width=width, joint="curve")
    if ring:
        draw.ellipse((c - REACH, c - REACH, c + REACH, c + REACH), outline=color + (alpha,), width=ring)
    return image.filter(ImageFilter.GaussianBlur(blur)) if blur else image


def bake(variant):
    rng = random.Random(100 + variant)
    paths = [path(rng, i / CRACKS * 2 * math.pi) for i in range(CRACKS)]
    image = layer([], 0, (200, 170, 140), 110, blur=7, ring=12)          # the faint ring at the reach
    image.alpha_composite(layer(paths, 20, GLOW, 170, blur=10))          # glow
    image.alpha_composite(layer(paths, 7, SPLIT, 255))                   # the split
    image.alpha_composite(layer(paths, 3, CORE, 230))                    # hot core
    image.save(f"{OUT}/QuakeCracks_{variant}.png")


if __name__ == "__main__":
    for v in range(VARIANTS):
        bake(v)
    print("baked", VARIANTS, "crack patterns into", OUT)
