"""Bakes the tremor's art. Run from the project folder: python Tools/bake_tremor.py  (needs Pillow and numpy)

Into Assets/Art/Effects/ (then Jam > Build Game Scenes):
  CaveRock_0..2.png      256x256: the rock rain's rocks, lumps cut from the cave's own wall (one CaveWall_A/B/C tile
                         stretched over it), darker underneath, a dark outline
  TremorCracks_0..2.png  1024x1024: dark cracks wandering out from the middle (a tremor's start, and small under a
                         landed rock), reaching CRACK_REACH px
  Ripple.png             1024x1024: a thin soft ring at the edge (a tremor's ripples across the floor)
  DustHaze.png           256x144: white, see-through in the middle, solid toward the edges (the tremor's dusty haze)
"""
import math
import random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = "Assets/Art/Effects"
CAVE = "Assets/Art/Cave"
CRACK_SIZE, CRACK_REACH = 1024, 480  # TremorEffect / FallingRock: PatternReach = CRACK_REACH / (CRACK_SIZE / 2)
SPLIT, EDGE = (18, 12, 14, 255), (95, 82, 78, 255)


def rocks():
    for i, variant in enumerate("ABC"):
        # One wall tile over the whole rock (its bricks drawn big, so it reads as a lump of stone, not masonry)
        size = 256
        stone = Image.open(f"{CAVE}/CaveWall_{variant}_0.png").convert("RGBA").resize((size, size), Image.LANCZOS)
        rng = random.Random(10 + i)
        c, r = size / 2, size * 0.42
        points = [(c + math.cos(a) * r * rng.uniform(0.75, 1.0), c + math.sin(a) * r * rng.uniform(0.7, 0.95))
                  for a in [k / 10 * 2 * math.pi for k in range(10)]]
        mask = Image.new("L", (size, size))
        ImageDraw.Draw(mask).polygon(points, fill=255)
        shade = Image.new("RGBA", (size, size))
        ImageDraw.Draw(shade).ellipse((-size * 0.2, size * 0.45, size * 1.2, size * 1.4), fill=(0, 0, 0, 110))  # darker underneath
        stone.alpha_composite(shade)
        pixels = np.asarray(stone, np.float32).copy()
        pixels[..., :3] = np.clip(pixels[..., :3] * 1.25, 0, 255)
        stone = Image.fromarray(pixels.astype(np.uint8), "RGBA")
        stone.putalpha(mask)
        image = Image.new("RGBA", (size, size), (15, 10, 14, 0))
        image.putalpha(mask.filter(ImageFilter.MaxFilter(11)))  # the dark outline, 5 px
        image.alpha_composite(stone)
        image.save(f"{OUT}/CaveRock_{i}.png")


def cracks():
    for v in range(3):
        rng = random.Random(30 + v)
        image = Image.new("RGBA", (CRACK_SIZE, CRACK_SIZE))
        d = ImageDraw.Draw(image)
        c = CRACK_SIZE / 2
        paths = []
        for i in range(7):
            angle = i / 7 * 2 * math.pi + rng.uniform(-0.3, 0.3)
            points, dist = [(c, c)], 0.0
            while dist < CRACK_REACH:
                dist += rng.uniform(30, 60)
                angle += rng.uniform(-0.35, 0.35)
                points.append((c + math.cos(angle) * dist, c + math.sin(angle) * dist))
            paths.append(points)
        for points in paths:
            d.line(points, fill=SPLIT, width=7, joint="curve")
        for points in paths:
            d.line(points, fill=EDGE, width=2)
        image.save(f"{OUT}/TremorCracks_{v}.png")


def ripple():
    size = 1024
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    r = np.sqrt((xx - size / 2 + 0.5) ** 2 + (yy - size / 2 + 0.5) ** 2) / (size / 2)
    alpha = np.clip(1 - np.abs(r - 0.975) / 0.012, 0, 1) ** 1.5  # a thin soft line just inside the edge
    image = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    image.putalpha(Image.fromarray((alpha * 255).astype(np.uint8), "L"))
    image.save(f"{OUT}/Ripple.png")


def haze():
    w, h = 256, 144
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.sqrt(((xx - w / 2 + 0.5) / (w / 2)) ** 2 + ((yy - h / 2 + 0.5) / (h / 2)) ** 2) / math.sqrt(2)
    alpha = np.clip((d - 0.3) / 0.7, 0, 1) ** 1.4
    image = Image.new("RGBA", (w, h), (255, 255, 255, 0))
    image.putalpha(Image.fromarray((alpha * 255).astype(np.uint8), "L"))
    image.save(f"{OUT}/DustHaze.png")


if __name__ == "__main__":
    rocks()
    cracks()
    ripple()
    haze()
    print("baked the tremor's art into", OUT)
