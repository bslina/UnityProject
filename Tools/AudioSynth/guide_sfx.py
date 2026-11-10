# Original procedural sounds for the Egg Run start-panel guide (no samples, no third-party audio).
# Usage: python Tools/AudioSynth/guide_sfx.py Assets/UI/Resources/Audio
import math, struct, sys, wave, os

SR = 22050
OUT = sys.argv[1]
TAU = 2 * math.pi


def write(name, data, peak=0.8):
    m = max(1e-9, max(abs(v) for v in data))
    g = peak / m
    with wave.open(os.path.join(OUT, name + '.wav'), 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(b''.join(struct.pack('<h', int(max(-1, min(1, v * g)) * 32767)) for v in data))
    print('wrote', name, round(len(data) / SR, 2), 's')


def bubble_pop():
    # cartoon "pop!": tiny click + pitch dropping fast 1100 -> 260 Hz, then a short airy tail
    import random
    rnd = random.Random(7)
    out, phase, dur = [], 0.0, 0.16
    for k in range(int(dur * SR)):
        t = k / SR
        f = 260 + 840 * math.exp(-t * 60)
        phase += TAU * f / SR
        body = math.sin(phase) * min(1.0, t / 0.002) * math.exp(-t * 28)
        click = (rnd.random() * 2 - 1) * math.exp(-t * 900) * 0.6
        air = (rnd.random() * 2 - 1) * math.exp(-t * 60) * 0.08
        out.append(body + click + air)
    return out


def character_cheer():
    # happy rising arpeggio (C E G C) with soft square-ish tone
    notes = [72, 76, 79, 84]
    step, total = 0.07, 0.07 * 3 + 0.3
    out = [0.0] * int(total * SR)
    for i, n in enumerate(notes):
        f = 440.0 * 2 ** ((n - 69) / 12.0)
        start = int(i * step * SR)
        dur = 0.3 if i == len(notes) - 1 else 0.11
        for k in range(int(dur * SR)):
            t = k / SR
            env = min(1.0, t / 0.005) * math.exp(-t * (6 if i == len(notes) - 1 else 18))
            v = math.sin(TAU * f * t) + 0.3 * math.sin(TAU * 2 * f * t) + 0.12 * math.sin(TAU * 3 * f * t)
            j = start + k
            if j < len(out): out[j] += v * env
    return out


write('bubble_pop', bubble_pop())
write('character_cheer', character_cheer())
