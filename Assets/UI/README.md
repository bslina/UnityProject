# Game UI: menus for the whole game

Everything to do with the shared menus is in `Assets/UI`. It covers the main menu, the level menu, settings, pause and the win/lose screen. Each minigame (Egg Run, and later your colleagues' games) only reports "won" or "lost". The menus handle everything else.

## 1. First-time setup (one click)

1. Let Unity finish compiling the new scripts.
2. Menu bar: **Tools > Game UI > Build Menus (create missing)**.
   This creates:
   - `Resources/UITheme.asset`: the skin, with all the drag-and-drop slots
   - `Resources/MiniGameRegistry.asset`: the list of minigames shown in the level menu
   - `Resources/GameUI.prefab`: pause button, pause, settings and win/lose popups, and the fade
   - `Prefabs/LevelButton.prefab`: one card in the level menu
   - `Scenes/MainMenu.unity`: the title screen and level menu
   - Build Profiles scene list: **MainMenu first**, then every minigame scene
3. Open `Scenes/MainMenu` and press Play.

You can press Play in **any** scene. `GameUI.prefab` spawns itself before the first scene loads, so EggRun on its own still gets the pause button and the win/lose screen.

## 2. Folder layout

```
Assets/UI/
  Art/
    Sprites/   <- buttons, panels, cards, background, logo, slider/toggle parts
    Icons/     <- pause, back, settings, lock, stars, win, lose
    Fonts/     <- .ttf / .otf
  Audio/       <- menu music, click, win, lose
  Prefabs/     LevelButton.prefab
  Resources/   GameUI.prefab, UITheme.asset, MiniGameRegistry.asset  (names must not change)
  Scenes/      MainMenu.unity
  Scripts/
    Core/      GameFlow, MiniGameBridge, MiniGameRegistry, GameSettings, SaveData, AudioManager
    Menus/     UIPanel, MainMenuUI, LevelSelectUI, LevelButton, PauseMenuUI, SettingsUI, ResultUI, GameOverlayUI, SafeArea, ButtonClickSound
    Theme/     UITheme, ThemedImage, ThemedText
    Editor/    GameUIBuilder (the Tools > Game UI menu)
```

## 3. Skinning: dragging in your GUI assets

**Tools > Game UI > Select Theme (skins)** opens `UITheme.asset`. Drag your assets into its slots:

| Section | Slots |
|---|---|
| Fonts | Body Font, Title Font |
| Backgrounds & panels | Menu Background, Logo, Panel, Card |
| Buttons | Primary Button (Play/Resume/Retry/Next), Secondary Button, Icon Button (pause/back) |
| Icons | Pause, Back, Settings, Lock, Star Full, Star Empty, Win, Lose |
| Sliders & toggles | Slider Background/Fill/Handle, Toggle Background/Checkmark |
| Colours | tint for each of the above, plus text colours |
| Sounds | Menu Music, Button Click, Win Sound, Lose Sound |

- Every element with a **ThemedImage** or **ThemedText** component updates right away, in the Editor as well as at runtime.
- An empty slot keeps Unity's default rounded sprite. An empty icon slot shows a text label instead (`II`, `<`, `Locked`). An empty Logo slot shows the "GAME TITLE" text.
- Colours multiply with the sprite. **If your sprites are already coloured, set the matching colour to white.**
- For buttons and panels, set 9-slice borders in the Sprite Editor. ThemedImage switches the Image to *Sliced* automatically when the sprite has borders.
- To style **one** element differently from the theme, select it, untick *Use Theme Sprite* / *Use Theme Colour* on its ThemedImage, and set the Image yourself.
- Want TextMeshPro instead of the legacy Text? The menus use `UnityEngine.UI.Text`, like EggRun, so no TMP import is needed. You can swap individual texts later.

Everything is laid out for **1920 × 1080 landscape**. The Canvas Scaler matches 0.5, the same as EggRun.

## 4. Adding a colleague's minigame

1. Put their game in its own folder, e.g. `Assets/<GameName>/`, with its scene.
2. **Tools > Game UI > Select MiniGame Registry**. In one of the "Mini Game N" entries:
   - `Id`: a short unique key, e.g. `cooking`. Don't change it later, because saves use it.
   - `Display Name`, `Description`, `Icon` (card picture), `Music` (optional)
   - `Scene Name`: the exact scene file name without `.unity`
   - untick **Coming Soon**
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
MainMenu --PLAY------------------------> first unlocked game not yet won
         --LEVELS--> level menu --card--> that game
         --SETTINGS--> settings popup

Minigame --pause button / Android back--> Pause: Resume, Restart, Settings, Levels, Main Menu
         --MiniGameBridge.Win/Lose------> Result: Next (win only), Retry, Levels, Menu
```

- **Locking**: with *Lock Until Previous Won* on in the registry, a game unlocks once the one before it is won. "Coming soon" entries are skipped.
- **Saves**: best stars and score per game are kept in PlayerPrefs. To clear them, use **Tools > Game UI > Clear Saved Progress and Settings** or the *Reset progress* button in Settings.
- **Android back button**: closes the top popup, then pauses in-game. In the menus it goes from the level menu back to the main menu, and from the main menu it quits.

## 6. Rebuilding

- **Build Menus (create missing)** never overwrites. Use it after deleting one of the generated files.
- **Rebuild Menus (overwrite)** regenerates the two prefabs and the MainMenu scene, and **loses manual edits** to them. The Theme and Registry are always kept. Once you start editing the generated prefabs or scene by hand, stop using Rebuild.

## 7. What changed in Egg Run

- `GameManager`: on win/lose it calls `MiniGameBridge.Win/Lose`. Stars come from time left: ≥30 s gives 3, ≥15 s gives 2, otherwise 1. The score is the time left × 10. The old EndPanel is only used if the Game UI is missing. The debug text shows only when *Show debug info* is on.
- `Haptics`: does nothing when Vibration is off in Settings.
- `TiltInput`: applies the *Invert tilt* and *Tilt sensitivity* settings on top of the Inspector values.
- `SensorCheck`: the sensor panel shows only when *Show debug info* is on (default **off**). Turn it on in Settings to test sensors on the phone.
