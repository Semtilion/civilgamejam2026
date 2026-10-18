"""Bakes the app icon. Run from the project folder: python Tools/bake_icon.py  (needs Pillow)

Into Assets/Art/UI/Icon.png (256x256): the artist's crystal (Assets/Art/Dungeon/Crystal.png) glowing violet on a dark
rounded square, like the title screen. Jam > Build Windows Player sets it as the game's icon.
"""
from PIL import Image, ImageDraw, ImageFilter

SIZE = 256
OUT = "Assets/Art/UI/Icon.png"


def main():
    image = Image.new("RGBA", (SIZE, SIZE))
    mask = Image.new("L", (SIZE, SIZE))
    ImageDraw.Draw(mask).rounded_rectangle((8, 8, SIZE - 8, SIZE - 8), radius=44, fill=255)

    # The square: dark violet, lighter in the middle
    square = Image.new("RGBA", (SIZE, SIZE), (18, 12, 30, 255))
    glow = Image.new("RGBA", (SIZE, SIZE))
    ImageDraw.Draw(glow).ellipse((40, 40, SIZE - 40, SIZE - 40), fill=(120, 70, 200, 150))
    square.alpha_composite(glow.filter(ImageFilter.GaussianBlur(34)))
    image.paste(square, (0, 0), mask)

    # The crystal with a soft violet halo behind it
    crystal = Image.open("Assets/Art/Dungeon/Crystal.png").convert("RGBA")
    height = 196
    crystal = crystal.resize((round(crystal.width * height / crystal.height), height), Image.LANCZOS)
    x, y = (SIZE - crystal.width) // 2, (SIZE - crystal.height) // 2
    halo = Image.new("RGBA", (SIZE, SIZE))
    halo.paste((190, 140, 255, 255), (x, y), crystal.split()[3])
    image.alpha_composite(halo.filter(ImageFilter.GaussianBlur(14)))
    image.alpha_composite(crystal, (x, y))

    # A thin light rim on the square
    ImageDraw.Draw(image).rounded_rectangle((8, 8, SIZE - 8, SIZE - 8), radius=44, outline=(150, 110, 220, 255), width=3)
    image.save(OUT)
    print("baked", OUT)


if __name__ == "__main__":
    main()
