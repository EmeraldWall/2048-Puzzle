"""Synthesizes the game's sound effects from scratch (no samples), as 44.1 kHz 16-bit mono WAV files.

Usage: python3 tools/generate_sounds.py src/Puzzle2048.App/Resources/Raw/sfx
"""
import math
import random
import struct
import sys
import wave

RATE = 44100
OUT = sys.argv[1]


def note(name):
    names = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}
    semis = names[name[0]] + (1 if "#" in name else 0)
    octave = int(name[-1])
    return 440.0 * 2 ** ((semis - 9) / 12 + (octave - 4))


def silence(seconds):
    return [0.0] * int(seconds * RATE)


def mix(base, layer, at=0.0, gain=1.0):
    start = int(at * RATE)
    if len(base) < start + len(layer):
        base.extend([0.0] * (start + len(layer) - len(base)))
    for i, v in enumerate(layer):
        base[start + i] += v * gain
    return base


def env(i, n, attack, release_tau):
    t = i / RATE
    a = min(1.0, t / attack) if attack > 0 else 1.0
    return a * math.exp(-t / release_tau)


def bubble_pop(freq, length=0.22):
    """A round, bubbly pop: quick upward pitch glide, soft harmonics, fast decay."""
    n = int(length * RATE)
    out, phase = [], 0.0
    for i in range(n):
        t = i / RATE
        glide = freq * (0.72 + 0.28 * (1 - math.exp(-t / 0.018)))
        phase += 2 * math.pi * glide / RATE
        e = env(i, n, 0.002, 0.07)
        v = math.sin(phase) + 0.28 * math.sin(2 * phase) + 0.08 * math.sin(3 * phase)
        out.append(v * e)
    return out


def bell(freq, length=0.6, brightness=1.6, decay=0.22):
    """Soft FM bell, used for chimes and fanfares."""
    n = int(length * RATE)
    out = []
    for i in range(n):
        t = i / RATE
        index = brightness * math.exp(-t / 0.12)
        mod = math.sin(2 * math.pi * freq * 2.0 * t)
        v = math.sin(2 * math.pi * freq * t + index * mod)
        out.append(v * env(i, n, 0.003, decay))
    return out


def soft_tone(freq, length, decay, shape="tri"):
    n = int(length * RATE)
    out = []
    for i in range(n):
        t = i / RATE
        p = (freq * t) % 1.0
        v = 4 * abs(p - 0.5) - 1 if shape == "tri" else math.sin(2 * math.pi * freq * t)
        out.append(v * env(i, n, 0.006, decay))
    return out


def noise_swish(length, low=0.15, gain=1.0):
    random.seed(7)
    n = int(length * RATE)
    out, prev = [], 0.0
    for i in range(n):
        x = random.uniform(-1, 1)
        prev += low * (x - prev)          # one-pole low-pass
        t = i / n
        shape = math.sin(math.pi * t) ** 2
        out.append(prev * shape * gain)
    return out


def write(name, samples, peak=0.85):
    top = max(1e-9, max(abs(s) for s in samples))
    scale = peak / top
    fade = int(0.004 * RATE)
    data = bytearray()
    for i, s in enumerate(samples):
        if i >= len(samples) - fade:
            s *= (len(samples) - i) / fade
        data += struct.pack("<h", int(max(-1, min(1, s * scale)) * 32767))
    with wave.open(f"{OUT}/{name}.wav", "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(bytes(data))


# Merge pops climb a major pentatonic scale as tiles grow (4, 8, 16 ... 4096+)
scale = ["C5", "D5", "E5", "G5", "A5", "C6", "D6", "E6", "G6", "A6", "C7", "D7"]
for i, name in enumerate(scale, start=1):
    write(f"pop_{i:02d}", bubble_pop(note(name)), peak=0.7)

# Soft move tick
write("move", [v * 0.6 for v in bubble_pop(note("G6"), 0.06)], peak=0.28)

# UI tap
tap = mix(bubble_pop(note("E6"), 0.08), noise_swish(0.02, 0.5), gain=0.3)
write("tap", tap, peak=0.45)

# Combo chime: quick rising arpeggio
combo = silence(0.0)
for k, name in enumerate(["E6", "G6", "C7"]):
    mix(combo, bell(note(name), 0.45, 1.2, 0.16), at=k * 0.06)
write("combo", combo, peak=0.6)

# Milestone tile: shimmering chord with a sparkle on top
big = silence(0.0)
for name, g in (("C6", 1.0), ("E6", 0.8), ("G6", 0.7), ("C7", 0.5)):
    mix(big, bell(note(name), 0.9, 1.8, 0.3), at=0.0, gain=g)
for k in range(6):
    mix(big, bell(note("E7") * (1 + 0.06 * k), 0.15, 0.6, 0.05), at=0.1 + k * 0.05, gain=0.25)
write("milestone", big, peak=0.65)

# Goal / win fanfare
fanfare = silence(0.0)
for k, name in enumerate(["C5", "E5", "G5", "C6"]):
    mix(fanfare, bell(note(name), 0.7, 1.4, 0.25), at=k * 0.11)
    mix(fanfare, soft_tone(note(name) / 2, 0.5, 0.2), at=k * 0.11, gain=0.35)
mix(fanfare, bell(note("E6"), 1.0, 1.0, 0.45), at=0.5, gain=0.7)
mix(fanfare, bell(note("G6"), 1.0, 1.0, 0.45), at=0.5, gain=0.6)
write("win", fanfare, peak=0.7)

# Level up: fast rising run with a sparkle
level = silence(0.0)
for k, name in enumerate(["C5", "E5", "G5", "C6", "E6", "G6"]):
    mix(level, bell(note(name), 0.35, 1.1, 0.12), at=k * 0.055)
mix(level, bell(note("C7"), 0.8, 1.6, 0.3), at=0.34, gain=0.8)
write("levelup", level, peak=0.65)

# Game over: gentle, falling, never harsh
lose = silence(0.0)
for k, name in enumerate(["G4", "E4", "C4"]):
    mix(lose, soft_tone(note(name), 0.5, 0.22), at=k * 0.16)
write("lose", lose, peak=0.5)

# New game whoosh
write("whoosh", noise_swish(0.35, 0.08), peak=0.35)
print("sounds written")
