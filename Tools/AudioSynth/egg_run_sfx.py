# Original procedural Egg Run sound effects (no samples, no third-party audio).
# Reuses the instruments of synth.py.
# Usage: python Tools/AudioSynth/egg_run_sfx.py Assets/PocketJungle/Shared/Resources/Audio
#   footstep_1..4  soft steps on a dirt path (one random step per footfall)
#   bird_land      happy "tweet-tweet" when the bird lands on the spoon
#   shoo           the player shooing: breathy "shhh-oo!" + hand swish
#   egg_cooked     quick sharp oven "ting!" when the egg cooks in the sun
import os, sys, math, random

here = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(here, 'synth.py'), encoding='utf-8').read()
exec(src[:src.index("write('music_menu'")])   # instruments only, writes nothing


def lowpass(data, k):
    y, out = 0.0, []
    for v in data:
        y += k * (v - y)
        out.append(y)
    return out


# ---- footsteps: crunchy dirt/grass step, bright enough for phone speakers
# (a phone speaker can't play the deep thump of a real step, so the "crunch" carries it)
def bandpass(data, lo, hi):
    a_lo = math.exp(-TAU * lo / SR); a_hi = 1 - math.exp(-TAU * hi / SR)
    y1 = y2 = prev = 0.0; out = []
    for v in data:
        y1 += a_hi * (v - y1)            # low-pass at hi
        hp = y1 - prev * a_lo; prev = y1  # rough high-pass at lo
        y2 += 0.5 * (hp - y2)
        out.append(y2)
    return out

for i in range(4):
    rnd = random.Random(100 + i)
    dur = 0.18
    n = int(dur * SR)
    step = [0.0] * n
    # heel: short knock in the range phones can play
    f0 = rnd.uniform(190, 260)
    add(step, 0.0, [math.sin(TAU * f0 * (k / SR) * (1 - 0.35 * k / n)) * math.exp(-k / SR * 45) * 0.8 for k in range(n)])
    # crunch: a few grains of gravel/grass, band-passed noise bursts
    t = 0.0
    for g in range(rnd.randint(3, 5)):
        gl = rnd.uniform(0.012, 0.03)
        grain = [(rnd.random() * 2 - 1) * math.exp(-k / SR * rnd.uniform(60, 120)) for k in range(int(gl * SR))]
        add(step, t, [v * rnd.uniform(0.6, 1.0) for v in bandpass(grain, rnd.uniform(700, 1100), rnd.uniform(2600, 4200))])
        t += rnd.uniform(0.006, 0.025)
    write('footstep_%d' % (i + 1), step, 0.95)


# ---- bird lands: two quick rising chirps + a small trill
def chirp(f0, f1, dur, vol):
    out, ph = [], 0.0
    for k in range(int(dur * SR)):
        t = k / SR
        x = t / dur
        f = f0 + (f1 - f0) * x * x
        ph += TAU * f / SR
        env = math.sin(math.pi * x) ** 0.7
        out.append((math.sin(ph) + 0.2 * math.sin(2 * ph)) * env * vol)
    return out

land = buf(0.55)
add(land, 0.00, chirp(2300, 3900, 0.07, 0.9))
add(land, 0.11, chirp(2500, 4200, 0.08, 1.0))
for j in range(4):
    add(land, 0.25 + j * 0.045, chirp(3400, 3000, 0.035, 0.5))
write('bird_land', land, 0.95)


# ---- shoo: the PLAYER shooing the bird: a breathy "shhh-OO!" + a quick hand swish
rnd = random.Random(7)

def breath(dur, lo, hi, shape, vol, seed):
    r = random.Random(seed)
    a_hi = 1 - math.exp(-TAU * hi / SR); a_lo = math.exp(-TAU * lo / SR)
    y = prev = 0.0; out = []
    n = int(dur * SR)
    for k in range(n):
        x = k / n
        y += a_hi * ((r.random() * 2 - 1) - y)
        hp = y - prev * a_lo; prev = y
        out.append(hp * shape(x) * vol)
    return out

# "shh": hissy noise around 2-6 kHz, swelling in
shh = breath(0.22, 2000, 6500, lambda x: min(1, x / 0.3) * (1 - 0.3 * x), 1.0, 11)
# "OO!": voiced and breathy, a short low hoot gliding down, like a mouth saying "oo"
oo, ph = [], 0.0
odur = 0.2
for k in range(int(odur * SR)):
    t = k / SR; x = t / odur
    f = 330 - 90 * x
    ph += TAU * f / SR
    voiced = math.sin(ph) + 0.35 * math.sin(2 * ph) + 0.12 * math.sin(3 * ph)
    airy = (rnd.random() * 2 - 1) * 0.35
    oo.append((voiced * 0.6 + airy * 0.4) * min(1, t / 0.015) * (1 - x) ** 1.5)
# hand swish: fast filtered-noise sweep
swish = breath(0.16, 600, 3500, lambda x: math.sin(math.pi * x) ** 2, 0.6, 12)

shoo = buf(0.5)
add(shoo, 0.0, shh)
add(shoo, 0.17, oo)
add(shoo, 0.12, swish)
write('shoo', shoo, 0.95)


# ---- egg cooked: quick, sharp oven "ting!" (small bright bell, short ring)
def ting(f, dur, vol):
    partials = [(1.0, 1.0, 7.0), (2.76, 0.5, 14.0), (5.4, 0.3, 24.0), (8.93, 0.15, 36.0)]
    out = []
    for k in range(int(dur * SR)):
        t = k / SR
        v = sum(a * math.sin(TAU * f * r * t) * math.exp(-t * d) for r, a, d in partials)
        out.append(v * min(1, t / 0.0008) * vol)
    return out

ding = buf(0.7)
add(ding, 0.0, ting(2637, 0.7, 1.0))                              # E7: high and sharp
add(ding, 0.0, noise_burst(0.004, 900, 0.5, lp=1.0, seed=3))      # the metal "t" of the strike
write('egg_cooked', ding, 0.95)
