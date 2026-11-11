# Pocket Jungle: project layout

```
Assets/
├─ PocketJungle/                 everything made for the game
│  ├─ Shared/                    the "big game": menus, players, turns, Random Match, audio
│  │   ├─ Scenes/                MainMenu, Players, LevelSelect, RandomMatch, TurnHandover,
│  │   │                         SettingsMenu, PauseMenu, WinMenu (results), LoseMenu (unused)
│  │   ├─ Scripts/Core/          GameFlow (+ .Players / .Match), MiniGameBridge, MiniGameRegistry,
│  │   │                         AudioManager, GameAudio, GameSettings, SaveData
│  │   ├─ Scripts/Menus/         one script per menu screen
│  │   ├─ Scripts/Editor/        Tools > Game UI builders
│  │   ├─ Prefabs/               LevelButton (level card)
│  │   ├─ Resources/             GameUI prefab, MiniGameRegistry, Audio/ (all music + sounds)
│  │   └─ Art/                   fonts, logo, backgrounds, icons
│  └─ MiniGames/                 one folder per minigame, each with its own Scenes/Scripts/art
│      ├─ EggRun/                Scenes/EggRun.unity, Scripts, Editor (Tools > Egg Run), Character, Models (spoons)
│      ├─ SoundCopy/             Scenes/Main.unity, Scripts, AI, Media (videos), Art, Fonts
│      ├─ PhotoChallenge/        Scenes/MainScene.unity, Scripts, Models (AI), ReferenceImages, JungleBackgrounds
│      ├─ ReactionBattle/        ReactionBattle.unity + ReactionBattle3D.cs
│      └─ DuoRoad/               DuoRoad.unity, Scripts, Resources (PrimLit material), VOICE_SETUP.md
├─ ThirdParty/                   downloaded packs (2D Casual UI, Low Poly Trees & Nature Pack)
├─ _Archive/                     old scenes kept just in case (not in the game)
└─ Settings/, TextMesh Pro/, TutorialInfo/, Resources/, InputSystem_Actions
                                 left where Unity / its packages expect them
```

Adding a minigame: put it in `PocketJungle/MiniGames/<Name>/`, add its scene to the build list and an
entry in `Shared/Resources/MiniGameRegistry.asset`; talk to the menus only through `MiniGameBridge`
(`ReportDuel`, `Win`, `Lose`, `PlayerName`...).

Moving files: always move them **inside Unity** (or together with their `.meta` file), so every
reference keeps working. Paths written in scripts (editor tools) must then be updated by hand.
