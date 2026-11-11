# Original procedural music loops for the minigames added later (no samples, no third-party audio).
# Reuses the instruments and the music() composer of synth.py.
# Usage: python Tools/AudioSynth/minigame_music.py Assets/PocketJungle/Shared/Resources/Audio
#
# Every place in Pocket Jungle has its own tune:
#   music_menu              all menus (main menu, levels, settings, pause, win, lose)  - synth.py
#   music_egg_run           Egg Run while playing (128 bpm plucks + kick)              - synth.py
#   music_egg_run_intro     Egg Run start panel                                         - here
#   music_sound_copy        Sound Copy                                                  - synth.py
#   music_photo_challenge   Photo Challenge                                             - synth.py
#   music_reaction_battle   Reaction Battle                                             - here
#   music_duo_road          Duo Road                                                    - here
import os, sys

here = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(here, 'synth.py'), encoding='utf-8').read()
exec(src[:src.index("write('music_menu'")])   # instruments + music() only, writes nothing

# Egg Run start panel: gentle and playful, a soft music-box pluck up high with finger snaps,
# no drums (the menu tune is marimba + bongos, the gameplay tune is fast plucks + kick).
write('music_egg_run_intro',
      music('music_egg_run_intro', 72, 'major_penta', PROG_MAJOR, 84,
            lambda f: pluck(f, 0.5, 0.4, 1.0), {'snap': 1}, seed=33, swing=0.3),
      0.6)

# Reaction Battle: tense duel, minor key, busy kick + snaps, short staccato plucks.
write('music_reaction_battle',
      music('music_reaction_battle', 55, 'minor_penta', PROG_MINOR, 118,
            lambda f: pluck(f, 0.16, 0.5, 1.2), {'kick': 'busy', 'snap': 1}, seed=41),
      0.7)

# Duo Road: fast driving groove, low marimba lead, kick + shaker + bongos.
write('music_duo_road',
      music('music_duo_road', 58, 'major_penta', PROG_MAJOR, 140,
            lambda f: marimba(f, 0.3, 0.5), {'kick': 1, 'shaker': 1, 'bongo': 1}, seed=57),
      0.7)
