"""Bakes the town's rain. Run from the project folder: python Tools/bake_rain.py  (needs Pillow)

Into Assets/Art/UI/Town/Rain.png (512x512, tiling): thin pale streaks slanting down to the left on transparent, a
light drizzle's worth; streaks crossing an edge continue on the other side so it repeats seamlessly. TownRain slides
three layers of it (far small and slow, near big and fast). Then Jam > Build Game Scenes.
"""
import random
from PIL import Image, ImageDraw

SIZE = 512
DROPS = 24
SLANT = 0.18  # sideways per fall (TownRain moves it the same way)
OUT = "Assets/Art/UI/Town/Rain.png"


def main():
    rng = random.Random(8)
    image = Image.new("RGBA", (SIZE, SIZE))
    draw = ImageDraw.Draw(image)
    for _ in range(DROPS):
        x, y = rng.uniform(0, SIZE), rng.uniform(0, SIZE)
        length = rng.uniform(16, 34)
        alpha = rng.randint(90, 170)
        for dx in (-SIZE, 0, SIZE):  # the wrapped copies, for a seamless tile
            for dy in (-SIZE, 0, SIZE):
                top = (x + dx, y + dy)
                bottom = (x + dx - length * SLANT, y + dy + length)
                draw.line([top, bottom], fill=(200, 215, 240, alpha), width=2)
    image.save(OUT)
    print("baked", OUT)


if __name__ == "__main__":
    main()
