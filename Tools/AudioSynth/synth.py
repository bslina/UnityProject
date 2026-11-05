# Original procedural sound effects and music loops for Pocket Jungle (no samples, no third-party audio).
# Usage: python Tools/AudioSynth/synth.py Assets/UI/Resources/Audio
import math, random, struct, sys, wave, os

SR = 22050
OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
TAU = 2 * math.pi

def buf(seconds):
    return [0.0] * int(seconds * SR)

def write(name, data, peak=0.85):
    m = max(1e-9, max(abs(v) for v in data))
    g = peak / m
    with wave.open(os.path.join(OUT, name + '.wav'), 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(b''.join(struct.pack('<h', int(max(-1, min(1, v * g)) * 32767)) for v in data))
    print('wrote', name, round(len(data) / SR, 2), 's')

def note_hz(n):  # MIDI number -> Hz
    return 440.0 * 2 ** ((n - 69) / 12.0)

def add(b, start, samples, wrap=False):
    i0 = int(start * SR)
    n = len(b)
    for k, v in enumerate(samples):
        i = i0 + k
        if i >= n:
            if not wrap: break
            i %= n
        b[i] += v

# ---------------------------------------------------------------- instruments (return sample lists)
def env_exp(t, rate): return math.exp(-t * rate)

def marimba(f, dur=0.6, vol=1.0):
    out = []
    for k in range(int(dur * SR)):
        t = k / SR
        a = min(1.0, t / 0.003)
        out.append(vol * a * (math.sin(TAU * f * t) * env_exp(t, 6) + 0.35 * math.sin(TAU * f * 4 * t) * env_exp(t, 16) + 0.15 * math.sin(TAU * f * 10 * t) * env_exp(t, 40)))
    return out

def pluck(f, dur=0.35, vol=1.0, bright=1.0):
    out = []
    for k in range(int(dur * SR)):
        t = k / SR
        a = min(1.0, t / 0.002)
        s = sum(math.sin(TAU * f * h * t) / h * (bright ** (h - 1)) for h in (1, 3, 5, 7))
        out.append(vol * a * s * env_exp(t, 7))
    return out

def bass(f, dur=0.4, vol=1.0):
    out = []
    for k in range(int(dur * SR)):
        t = k / SR
        ph = (f * t) % 1.0
        tri = 4 * abs(ph - 0.5) - 1
        a = min(1.0, t / 0.01) * (1 if t < dur - 0.04 else max(0.0, (dur - t) / 0.04))
        out.append(vol * a * (0.8 * tri + 0.4 * math.sin(TAU * f * t)) * env_exp(t, 2.5))
    return out

def kick(vol=1.0):
    out, ph = [], 0.0
    for k in range(int(0.25 * SR)):
        t = k / SR
        f = 45 + 110 * env_exp(t, 30)
        ph += TAU * f / SR
        out.append(vol * math.sin(ph) * env_exp(t, 14))
    return out

def noise_burst(dur, rate, vol=1.0, hp=True, lp=1.0, seed=None):
    rnd = random.Random(seed)
    out, prev, y = [], 0.0, 0.0
    for k in range(int(dur * SR)):
        t = k / SR
        x = rnd.uniform(-1, 1)
        if hp: x, prev = x - prev, x
        y += lp * (x - y)
        out.append(vol * y * env_exp(t, rate))
    return out

def shaker(vol=1.0): return noise_burst(0.08, 55, vol, hp=True, lp=0.9)
def snap(vol=1.0): return noise_burst(0.12, 35, vol, hp=True, lp=0.6)

def bongo(f, vol=1.0):
    out, ph = [], 0.0
    for k in range(int(0.25 * SR)):
        t = k / SR
        ph += TAU * (f * (1 + 0.5 * env_exp(t, 40))) / SR
        out.append(vol * math.sin(ph) * env_exp(t, 18))
    return out

def sweep(f0, f1, dur, vol=1.0, shape='sine', decay=3.0):
    out, ph = [], 0.0
    for k in range(int(dur * SR)):
        t = k / SR
        f = f0 + (f1 - f0) * (t / dur)
        ph += TAU * f / SR
        s = math.sin(ph) if shape == 'sine' else (1.0 if math.sin(ph) >= 0 else -1.0) * 0.6
        a = min(1.0, t / 0.005) * min(1.0, (dur - t) / 0.03)
        out.append(vol * a * s * env_exp(t, decay))
    return out

def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p): out[i] += v
    return out

# ---------------------------------------------------------------- music loops
SCALES = {'major_penta': [0, 2, 4, 7, 9], 'minor_penta': [0, 3, 5, 7, 10]}
PROG_MAJOR = [0, 9, 5, 7]     # I vi IV V (semitones from root)
PROG_MINOR = [0, 8, 3, 10]    # i VI III VII

def music(name, root, scale, prog, bpm, lead, drums, seed, bars=8, swing=0.0):
    rnd = random.Random(seed)
    beat = 60.0 / bpm
    length = bars * 4 * beat
    b = [0.0] * int(length * SR)
    steps = SCALES[scale]
    # Melody: a 2-bar motif, varied every 2 bars, staying on the scale.
    motif = []
    deg = 2
    for i in range(8 * 2):  # 8th notes over 2 bars
        if rnd.random() < 0.28 and i % 2 == 1:
            motif.append(None); continue
        deg = max(0, min(9, deg + rnd.choice([-2, -1, -1, 0, 1, 1, 2])))
        motif.append(deg)
    for bar in range(bars):
        chord = prog[(bar // 1) % len(prog)] if bars <= 4 else prog[(bar // 2) % len(prog)]
        t0 = bar * 4 * beat
        # bass: root on 1 and 3, fifth on the and of 4
        add(b, t0, bass(note_hz(root - 24 + chord), beat * 1.6, 0.55), True)
        add(b, t0 + 2 * beat, bass(note_hz(root - 24 + chord), beat * 1.4, 0.45), True)
        add(b, t0 + 3.5 * beat, bass(note_hz(root - 24 + chord + 7), beat * 0.45, 0.35), True)
        # chord stabs (soft)
        for off in (1, 3):
            for iv in (0, 4 if scale == 'major_penta' else 3, 7):
                add(b, t0 + off * beat, pluck(note_hz(root + chord + iv), 0.25, 0.10, 0.4), True)
        # melody
        variation = (bar // 2) % 2
        for i in range(8):
            d = motif[(bar % 2) * 8 + i]
            if d is None: continue
            if variation and i == 7: d = min(9, d + 2)
            octave, idx = divmod(d, len(steps))
            n = root + 12 * octave + steps[idx]
            st = t0 + i * beat / 2 + (swing * beat / 2 if i % 2 else 0)
            add(b, st, lead(note_hz(n)), True)
        # drums
        for q in range(8):
            st = t0 + q * beat / 2
            if 'kick' in drums and q in (0, 4) or ('kick' in drums and drums['kick'] == 'busy' and q in (3, 6)):
                add(b, st, kick(0.9), True)
            if 'shaker' in drums: add(b, st + (swing * beat / 2 if q % 2 else 0), shaker(0.25 if q % 2 else 0.15), True)
            if 'snap' in drums and q in (2, 6): add(b, st, snap(0.35), True)
            if 'bongo' in drums and q in (1, 3, 5, 6): add(b, st, bongo(260 if q % 2 else 190, 0.4), True)
    return b

write('music_menu', music('music_menu', 60, 'major_penta', PROG_MAJOR, 100, lambda f: marimba(f, 0.55, 0.55), {'shaker': 1, 'bongo': 1}, seed=7, swing=0.15), 0.7)
write('music_egg_run', music('music_egg_run', 65, 'major_penta', PROG_MAJOR, 128, lambda f: pluck(f, 0.28, 0.5, 0.6), {'kick': 1, 'shaker': 1}, seed=21), 0.7)
write('music_sound_copy', music('music_sound_copy', 57, 'minor_penta', PROG_MINOR, 96, lambda f: pluck(f, 0.4, 0.45, 0.8), {'kick': 'busy', 'snap': 1, 'shaker': 1}, seed=5, swing=0.2), 0.7)
write('music_photo_challenge', music('music_photo_challenge', 67, 'major_penta', PROG_MAJOR, 112, lambda f: marimba(f * 2, 0.4, 0.5), {'kick': 1, 'bongo': 1, 'shaker': 1}, seed=13), 0.7)

# ---------------------------------------------------------------- shared UI
write('click', mix(sweep(1900, 1500, 0.045, 0.8, decay=60), noise_burst(0.03, 120, 0.25, lp=0.5, seed=1)))
win = buf(1.4)
for i, n in enumerate([72, 76, 79, 84]):
    add(win, i * 0.09, marimba(note_hz(n), 0.9, 0.8))
for n in (72, 76, 79, 84):
    add(win, 0.4, pluck(note_hz(n), 1.0, 0.25, 0.5))
write('win', win)
lose = buf(1.8)
for i, (n, d) in enumerate([(67, 0.3), (66, 0.3), (65, 0.3), (64, 0.8)]):
    seg = []
    for k in range(int(d * SR)):
        t = k / SR
        f = note_hz(n) * (1 + 0.015 * math.sin(TAU * 6 * t) * (t / d))
        ph = (f * t) % 1.0
        seg.append((4 * abs(ph - 0.5) - 1) * min(1, t / 0.02) * min(1, (d - t) / 0.05) * 0.7)
    add(lose, i * 0.32, seg)
write('lose', lose)
write('point', mix(*[[0.0] * int(i * 0.07 * SR) + marimba(note_hz(n), 0.5, 0.8) for i, n in enumerate([79, 83, 86])]))

# ---------------------------------------------------------------- Egg Run
crack = buf(0.6)
add(crack, 0.0, noise_burst(0.05, 60, 0.9, lp=0.7, seed=2))
add(crack, 0.06, noise_burst(0.04, 70, 0.7, lp=0.8, seed=3))
add(crack, 0.11, noise_burst(0.12, 30, 0.6, lp=0.4, seed=4))
add(crack, 0.1, [math.sin(TAU * 90 * k / SR) * env_exp(k / SR, 18) * 0.8 for k in range(int(0.3 * SR))])
write('egg_crack', crack)
chime = buf(1.2)
for st, f in ((0.0, 1320), (0.25, 1760)):
    add(chime, st, [(math.sin(TAU * f * k / SR) + 0.3 * math.sin(TAU * f * 2.7 * k / SR)) * env_exp(k / SR, 3.5) * 0.6 for k in range(int(0.9 * SR))])
write('sun_warning', chime)
# Sizzle: crackly hiss, built to loop (pops wrap around the end).
siz = [0.0] * int(1.6 * SR)
rnd = random.Random(9); y = 0.0; prev = 0.0
for i in range(len(siz)):
    x = rnd.uniform(-1, 1); hp = x - prev; prev = x
    y += 0.35 * (hp - y)
    siz[i] = y * 0.25
for _ in range(40):
    add(siz, rnd.uniform(0, 1.6), noise_burst(0.02, 150, rnd.uniform(0.3, 0.8), lp=0.9, seed=rnd.randint(0, 9999)), True)
write('sizzle', siz, 0.6)
write('stumble', mix(noise_burst(0.35, 9, 0.6, hp=False, lp=0.08, seed=11), sweep(220, 110, 0.3, 0.4, decay=8)))
chirp = buf(0.45)
add(chirp, 0.0, sweep(2600, 3600, 0.08, 0.6, decay=10))
add(chirp, 0.14, sweep(2800, 4000, 0.1, 0.6, decay=10))
write('bird_chirp', chirp)
flap = buf(0.5)
for i in range(4): add(flap, i * 0.1, noise_burst(0.07, 35, 0.7, hp=False, lp=0.15, seed=20 + i))
write('bird_flap', flap)

# ---------------------------------------------------------------- Sound Copy
write('spin_result', mix(marimba(note_hz(76), 0.7, 0.8), [0.0] * int(0.12 * SR) + marimba(note_hz(83), 0.8, 0.8)))
x2 = buf(1.2)
add(x2, 0.0, sweep(400, 1600, 0.35, 0.5, decay=1.5))
for i, n in enumerate([84, 88, 91, 96]): add(x2, 0.33 + i * 0.07, marimba(note_hz(n), 0.6, 0.6))
write('spin_x2', x2)

# ---------------------------------------------------------------- Photo Challenge
shutter = buf(0.35)
add(shutter, 0.0, noise_burst(0.03, 100, 0.9, lp=0.9, seed=31))
add(shutter, 0.09, noise_burst(0.05, 70, 0.7, lp=0.6, seed=32))
write('shutter', shutter)
write('photo_match', mix(marimba(note_hz(88), 0.6, 0.8), [0.0] * int(0.1 * SR) + marimba(note_hz(93), 0.8, 0.8)))
write('photo_miss', mix(sweep(160, 120, 0.35, 0.7, 'square', decay=4), sweep(165, 125, 0.35, 0.5, 'square', decay=4)))
write('tick', sweep(2000, 1900, 0.03, 0.8, decay=80))
write('reveal', mix(noise_burst(0.4, 5, 0.4, hp=True, lp=0.3, seed=41), sweep(300, 900, 0.35, 0.3, decay=4)))
