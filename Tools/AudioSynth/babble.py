# Original procedural "babble" syllables for the Egg Run guide character (cute mumbling voice,
# no real words, no samples). Each file is one short syllable; the game plays random ones at
# random pitches while a tip is typed out.
# Usage: python Tools/AudioSynth/babble.py Assets/PocketJungle/Shared/Resources/Audio
import math, random, struct, sys, wave, os

SR = 22050
OUT = sys.argv[1]
TAU = 2 * math.pi

# Vowel formants (Hz): a cartoon voice = a buzzy tone filtered through two "mouth" resonances.
VOWELS = {
    'a': (850, 1250),
    'e': (550, 1900),
    'i': (330, 2300),
    'o': (520, 900),
    'u': (380, 800),
}


def write(name, data, peak=0.75):
    m = max(1e-9, max(abs(v) for v in data))
    g = peak / m
    with wave.open(os.path.join(OUT, name + '.wav'), 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(b''.join(struct.pack('<h', int(max(-1, min(1, v * g)) * 32767)) for v in data))
    print('wrote', name, round(len(data) / SR, 3), 's')


def resonance(freq, f0, width=120.0):
    # gain of a harmonic at 'freq' through a formant centred on f0
    return 1.0 / (1.0 + ((freq - f0) / width) ** 2)


def syllable(vowel, pitch, bend, dur, seed):
    rnd = random.Random(seed)
    f1, f2 = VOWELS[vowel]
    out, phase = [], 0.0
    n = int(dur * SR)
    for k in range(n):
        t = k / SR
        x = t / dur
        f = pitch * (1 + bend * (x - 0.3)) * (1 + 0.015 * math.sin(TAU * 7 * t))  # slight wobble
        phase += TAU * f / SR
        v = 0.0
        for h in range(1, 14):                       # harmonics shaped by the two formants
            hf = f * h
            if hf > SR / 2: break
            amp = (resonance(hf, f1) + 0.6 * resonance(hf, f2, 180)) / h ** 0.6
            v += amp * math.sin(phase * h)
        env = min(1.0, t / 0.008) * (1 - x) ** 1.6     # soft start, quick fade = "bubbly"
        out.append(v * env)
    # tiny breathy consonant at the start
    for k in range(int(0.006 * SR)):
        out[k] += (rnd.random() * 2 - 1) * 0.15 * (1 - k / (0.006 * SR))
    return out


specs = [
    ('a', 520, 0.25, 0.085), ('e', 600, -0.15, 0.075), ('i', 680, 0.30, 0.070), ('o', 480, -0.20, 0.090),
    ('u', 500, 0.10, 0.080), ('a', 580, -0.25, 0.070), ('e', 640, 0.20, 0.085), ('o', 540, 0.35, 0.075),
]
for i, (v, p, b, d) in enumerate(specs):
    write('babble_%d' % (i + 1), syllable(v, p, b, d, i))
