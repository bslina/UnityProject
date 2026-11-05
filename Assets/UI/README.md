# Game UI: menus for the whole game

Everything to do with the shared menus is in `Assets/UI`. It covers the main menu, the level menu, settings, pause and the win/lose screen. Each minigame (Egg Run, and later your colleagues' games) only reports "won" or "lost". The menus handle everything else.

## 1. First-time setup (one click)

1. Let Unity finish compiling the new scripts.
2. Menu bar: **Tools > Game UI > Build Menus (create missing)**.
   This creates:
   - `Resources/MiniGameRegistry.asset`: the list of minigames shown in the level menu
   - `Resources/GameUI.prefab`: the pause button shown over minigames, the scene fade, and the music
   - `Prefabs/LevelButton.prefab`: one card in the level menu
   - **One scene per menu** in `Scenes/`:

     | Scene | What it is | How it opens |
     |---|---|---|
     | `MainMenu` | title screen: Play (opens the level menu), Settings, Quit | replaces the current scene |
     | `LevelSelect` | one card per minigame | replaces the current scene |
     | `SettingsMenu` | sliders and toggles | **on top of** the main menu, level menu or pause menu |
     | `PauseMenu` | Resume, Restart, Settings, Levels, Main Menu | **on top of** the paused minigame, so the game keeps its state |
     | `WinMenu` | stars, score, Next / Retry / Levels / Menu | replaces the minigame |
     | `LoseMenu` | Retry / Levels / Menu | replaces the minigame |

   - Build Profiles scene list: **MainMenu first**, then the other menus and every minigame scene
3. Open `Scenes/MainMenu` and press Play.

You can press Play in **any** scene. `GameUI.prefab` spawns itself before the first scene loads, so EggRun on its own still gets the pause button and the win/lose scenes. `SettingsMenu` and `PauseMenu` have no camera on purpose (they are drawn over another scene), so opened alone they show over an empty background.

## 2. Folder layout

```
Assets/UI/
  Art/
    Sprites/   <- buttons, panels, cards, background, logo, slider/toggle parts
    Icons/     <- pause, back, settings, lock, stars, win, lose
    Fonts/     <- .ttf / .otf
  Audio/       <- menu music, click, win, lose
  Prefabs/     LevelButton.prefab
  Resources/   GameUI.prefab, MiniGameRegistry.asset  (names must not change)
  Scenes/      MainMenu, LevelSelect, SettingsMenu, PauseMenu, WinMenu, LoseMenu
  Scripts/
    Core/      GameFlow, MiniGameBridge, MiniGameRegistry, GameSettings, SaveData, AudioManager
    Menus/     UIPanel, MainMenuUI, LevelSelectUI, LevelButton, PauseMenuUI, SettingsUI, ResultUI, GameOverlayUI, SafeArea, ButtonClickSound, StarStyle, FitToScreen
    Editor/    GameUIBuilder (the Tools > Game UI menu)
```

## 3. Styling: every element is set by hand

There is no theme: each button, panel, text and image keeps exactly what you set on it. Open the scene (or prefab) and change the element's **Image** (sprite, colour, 9-slice) or **Text** (font, size, colour) directly.

| What | Where to edit it |
|---|---|
| Main menu, level menu, settings, pause, win, lose | the matching scene in `Assets/UI/Scenes` |
| Level cards | `Assets/UI/Prefabs/LevelButton.prefab` |
| Pause button shown over minigames, scene fade | `Assets/UI/Resources/GameUI.prefab` |
| Menu music, button click, win and lose sounds | `GameUI.prefab`, root object, **Audio Manager** component |
| Star look (earned / not earned) | **Star Style** on `LevelButton.prefab` and on the WinMenu's **Result UI** |
| Card names, descriptions, pictures | `MiniGameRegistry.asset` |

- Icons (pause, back, settings, lock) and the main-menu **Logo** are empty Images that start switched off; their text label (`II`, `<`, `Locked`, "GAME TITLE") shows instead. Give the Image a sprite, tick it on, and hide the label.
- Image colours multiply with the sprite: **if your sprites are already coloured, set the Image colour to white.**
- For stretchable buttons and panels, set 9-slice borders in the Sprite Editor and set the Image Type to *Sliced*.
- To restyle many elements at once, select them together in the Hierarchy and change the shared property.
- **Free placement:** the menus have no automatic layouts, so drag any button, text or image wherever you want (Rect Tool, `T`). Elements are anchored to the centre of their parent; change the anchors if you want something pinned to a screen edge. Only the level cards row in LevelSelect keeps its layout, because the cards are created at runtime. Menus made with an older version: run **Tools > Game UI > Unlock Layouts** once.
- Because nothing reflows any more, hidden elements leave a gap: e.g. the Win screen's Score text is hidden when the score is 0.
- The texts are legacy `UnityEngine.UI.Text`, like EggRun. You can swap individual texts to TextMeshPro, but then their scripts' `Text` slots need updating.
- A few values are written by scripts at runtime and can't be styled by text: the level cards' name/description (registry), the win/lose Detail, Score and Game Name texts, the Next/Finish label and the version number.

Everything is laid out for **1920 × 1080 landscape**. The Canvas Scaler matches 0.5, the same as EggRun.

## 4. Adding a colleague's minigame

1. Put their game in its own folder, e.g. `Assets/<GameName>/`, with its scene.
2. **Tools > Game UI > Select MiniGame Registry**. Egg Run, Sound Copy (`Main` scene) and Photo Challenge (`MainScene`) are already in it. In the "Mini Game 4" entry:
   - `Id`: a short unique key, e.g. `cooking`. Don't change it later, because saves use it.
   - `Display Name`, `Description`, `Icon` (card picture), `Music` (optional)
   - `Scene Name`: the exact scene file name without `.unity`
   - untick **Coming Soon**
   - **Orientation**: *Landscape* for all current games (like the menus); *Portrait* is there for a future game held upright
   - **Hide Pause Button**: tick if the game has its own pause menu (Sound Copy)
3. **Tools > Game UI > Sync Build Scene List**
4. In their game code, report the end of a round:

```csharp
using GameUI;

MiniGameBridge.Win("12.4 s left", stars: 3, score: 124); // stars 0-3, score optional
MiniGameBridge.Lose("Time's up!");
```

That's the whole integration. Optional extras:

- `MiniGameBridge.SetPauseButtonVisible(false)` hides the pause button, e.g. during an intro.
- `MiniGameBridge.PauseChanged += paused => ...` lets you react to pause. `Time.timeScale` is already 0 while paused.
- `GameSettings.Vibration`, `GameSettings.InvertTilt`, `GameSettings.TiltSensitivity`, `GameSettings.MusicVolume`, `GameSettings.SfxVolume`, `GameSettings.ShowDebug` are the player's settings.

Things to tell colleagues:

- **Don't** give their scene its own "Retry / Main menu" end screen. Call the bridge instead.
- Use `Time.deltaTime` for gameplay so pause works. Coroutines that use `WaitForSecondsRealtime` keep running while paused.
- Put scripts in a namespace (e.g. `namespace Cooking { ... }`). EggRun already uses the global names `GameManager`, `Bird`, `Egg`..., and two classes with the same name won't compile.
- Each scene keeps its own EventSystem (with *Input System UI Input Module*). If a scene has none, the Game UI creates one.

## 5. How the flow works

```
MainMenu --PLAY--> LevelSelect --card----> that game (any game, nothing is locked)
         --SETTINGS--> SettingsMenu (on top)

Minigame --pause button / Android back--> PauseMenu (on top): Resume, Restart, Settings, Levels, Main Menu
         --MiniGameBridge.Win-----------> WinMenu:  Next, Retry, Levels, Menu
         --MiniGameBridge.Lose----------> LoseMenu: Retry, Levels, Menu
```

- **Locking**: off. The player can pick any minigame. To lock games until the previous one is won, tick *Lock Until Previous Won* in the registry. "Coming soon" entries can't be played.
- **Scene names**: the registry's *Menu scenes* section holds the six menu scene names. If you rename a menu scene, rename it there too.
- **Saves**: best stars and score per game are kept in PlayerPrefs. To clear them, use **Tools > Game UI > Clear Saved Progress and Settings** or the *Reset progress* button in Settings.
- **Android back button**: closes Settings, then the pause menu, then pauses in-game. In the other menus it goes back to the main menu, and from the main menu it quits.

## 6. Rebuilding

- **Build Menus (create missing)** never overwrites. Use it after deleting one of the generated files.
- **Rebuild Menus (overwrite)** regenerates the two prefabs and the six menu scenes, and **loses manual edits** to them. The Registry is always kept. Once you start editing the generated prefabs or scene by hand, stop using Rebuild.

## 7. What changed in Egg Run

- `GameManager`: on win/lose it calls `MiniGameBridge.Win/Lose`. Stars come from time left: ≥30 s gives 3, ≥15 s gives 2, otherwise 1. The score is the time left × 10. The old EndPanel is only used if the Game UI is missing. The debug text shows only when *Show debug info* is on.
- `Haptics`: does nothing when Vibration is off in Settings.
- `TiltInput`: applies the *Invert tilt* and *Tilt sensitivity* settings on top of the Inspector values.
- `SensorCheck`: the sensor panel shows only when *Show debug info* is on (default **off**). Turn it on in Settings to test sensors on the phone.
- **Sun events** (`SunGlare` + `LightCoverDetector`, added to the GameManager object automatically at runtime):
  - Every 9-15 s the sun comes out: a warning (sun in the corner, two buzzes), then 5-8 s of blinding glare that heats the egg.
  - Covering the **front camera** with a thumb shades the egg: the glare fades and the egg cools. If the **EGG HEAT** bar fills, the round is lost with "The egg got cooked!".
  - The camera is the main signal because many phones (e.g. Galaxy A17) only have "virtual" light/proximity sensors that don't react to a hand. It runs at low resolution while the start panel and the round are on screen; only the picture's average brightness and contrast are measured, nothing is shown or saved. Android asks for camera permission once.
  - The light and proximity sensors are backups, used only when the camera can't run.
  - In the Editor, hold **L** to cover (or tick *Use Camera In Editor* on Light Cover Detector to use your webcam). Without permission or any usable sensor, an on-screen **SHADE** hold-button appears instead.
  - To tune it or add art, add the *Sun Glare* component to the GameManager object yourself: timings, heat speed, glare strength, sun/flare/shade sprites and sounds are all in the Inspector. *Light Cover Detector* holds the sensor thresholds.
  - Can be switched off with *Sun events (Egg Run)* in Settings. Existing SettingsMenu scenes only get this toggle after **Rebuild Menus**.
- `SpoonController`: walking is harder. Bigger wobble and sway, a slow **drift** (a lean that lasts seconds and rolls the egg off unless you correct it), and a random **stumble** every 2.5-5 s of walking. *Walk Difficulty* (0-2) scales all of it at once.
