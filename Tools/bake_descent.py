"""Bakes the dive transition's rock. Run from the project folder: python Tools/bake_descent.py  (needs Pillow, numpy)

Into Assets/Art/UI/Descent.png (1920x1080): the cave's wall stone, very dark, with glowing violet crystal veins running
through it and a few crystals in it; the screen sinks through it between the town and the depths (SceneTransition).
Then Jam > Build Game Scenes.
"""
import random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

W, H, UNIT = 1920, 1080, 120
CAVE = "Assets/Art/Cave"
OUT = "Assets/Art/UI/Descent.png"


def main():
    rng = random.Random(6)
    tiles = [[Image.open(f"{CAVE}/CaveWall_{v}_{i}.png").convert("RGBA").resize((UNIT, UNIT), Image.LANCZOS) for i in range(4)]
             for v in "ABC"]
    image = Image.new("RGBA", (W + UNIT, H + UNIT))
    for row in range(H // UNIT + 1):
        for x in range(W // UNIT + 1):
            y = 20 - row
            variant = tiles[((x // 2) * 7 + (y // 2) * 3) % 3]
            image.paste(variant[(x % 2) + 2 * (y % 2)], (x * UNIT, row * UNIT))
    image = image.crop((0, 0, W, H))
    pixels = np.asarray(image, np.float32).copy()
    pixels[..., :3] *= np.array([0.22, 0.2, 0.28])  # deep underground: very dark, a little violet
    image = Image.fromarray(pixels.astype(np.uint8), "RGBA")

    veins = []
    for _ in range(9):
        x, y = rng.uniform(100, W - 100), rng.uniform(60, H - 60)
        points = [(x, y)]
        for _ in range(rng.randint(5, 9)):
            x += rng.uniform(-90, 90)
            y += rng.uniform(-60, 60)
            points.append((x, y))
        veins.append(points)
    glow = Image.new("RGBA", (W, H))
    for points in veins:
        ImageDraw.Draw(glow).line(points, fill=(150, 100, 255, 210), width=22)
    image.alpha_composite(glow.filter(ImageFilter.GaussianBlur(14)))
    for points in veins:
        ImageDraw.Draw(image).line(points, fill=(205, 180, 255, 255), width=4)

    crystal = Image.open("Assets/Art/Dungeon/Crystal.png").convert("RGBA")
    for _ in range(7):  # a few crystals stuck in the rock
        size = rng.uniform(50, 110)
        c = crystal.resize((round(84 * size / 136), round(size)), Image.LANCZOS).rotate(rng.uniform(-60, 60), expand=True, resample=Image.BICUBIC)
        x, y = rng.uniform(80, W - 80), rng.uniform(80, H - 80)
        halo = Image.new("RGBA", (W, H))
        ImageDraw.Draw(halo).ellipse((x - size, y - size, x + size, y + size), fill=(150, 100, 255, 90))
        image.alpha_composite(halo.filter(ImageFilter.GaussianBlur(20)))
        image.alpha_composite(c, (round(x - c.width / 2), round(y - c.height / 2)))
    image.convert("RGB").save(OUT)
    print("baked", OUT)


if __name__ == "__main__":
    main()
