"""Bakes the game's 4 synthesized sounds. Run from the project folder: python Tools/bake_sounds.py [out folder]
(needs NumPy; default out: Assets/Audio/Sounds)

  Click.wav       25 ms  a tiny tap: soft noise burst + a 1.2 kHz blip (every button)
  XpPickup.wav    90 ms  a tiny chime: 1320 + 1980 Hz bell (XP picked up)
  Hit.wav         22 ms  a thud tick: 420 Hz + soft noise (a monster is hit)
  FoodPickup.wav 160 ms  a warm pluck: 660 Hz then 880 Hz (food picked up)

Seeded, so a re-bake gives the same files. Each was picked by ear from three candidates.
"""
import os, sys, wave
import numpy as np

RATE = 44100
OUT = sys.argv[1] if len(sys.argv) > 1 else "Assets/Audio/Sounds"


def save(name, x, peak, fade_in):
    x = x / np.max(np.abs(x)) * peak
    x = x * np.minimum(1, np.arange(len(x)) / (fade_in * RATE))  # no hard edge at the start
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes((x * 32767).astype(np.int16).tobytes())
    print("baked", name, round(len(x) / RATE * 1000), "ms")


def t(ms):
    return np.arange(int(RATE * ms / 1000)) / RATE


def soft_noise(rng, n, smooth):
    kernel = np.exp(-np.arange(smooth * 4) / smooth)
    return np.convolve(rng.standard_normal(n), kernel / kernel.sum(), "same")


def main():
    # The click: a fingertip-on-wood tap
    c = t(25)
    rng = np.random.default_rng(3)
    noise = rng.standard_normal(len(c))
    kernel = np.exp(-np.arange(12) / 3.0)
    noise = np.convolve(noise, kernel / kernel.sum(), "same")
    save("Click.wav", noise * np.exp(-c * 260) + 0.6 * np.sin(2 * np.pi * 1200 * c) * np.exp(-c * 200), 0.45, 0.0015)

    # XP: a tiny bell
    b = t(90)
    save("XpPickup.wav", (np.sin(2 * np.pi * 1320 * b) + 0.5 * np.sin(2 * np.pi * 1980 * b)) * np.exp(-b * 45), 0.35, 0.001)

    # The hit: a low thud with a little body
    rng = np.random.default_rng(5)
    e = t(22)
    save("Hit.wav", (np.sin(2 * np.pi * 420 * e) + 0.6 * soft_noise(rng, len(e), 2)) * np.exp(-e * 260), 0.5, 0.001)

    # Food: a warm two-note pluck
    p = t(160)

    def pluck(f, delay):
        out = np.zeros(len(p))
        s = int(delay * RATE)
        u = p[: len(p) - s]
        out[s:] = (np.sin(2 * np.pi * f * u) + 0.3 * np.sin(2 * np.pi * 2 * f * u)) * np.exp(-u * 28)
        return out

    save("FoodPickup.wav", pluck(660, 0) + 0.8 * pluck(880, 0.06), 0.4, 0.001)


if __name__ == "__main__":
    main()
