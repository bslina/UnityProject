# Sound Copy : changements pour l'intégration (Pocket Jungle)

**Important : après avoir récupéré ces changements, ouvrir Unity, attendre zéro erreur rouge, puis lancer `SoundCopy > Build Scenes`.** Cela régénère `Assets/SoundCopy/Scenes/Main.unity` avec la nouvelle interface.

## Nouveau parcours
```
Accueil -> Nouvelle partie (prénoms + manches + catégorie, 1 seul écran)
-> Manche (scores + jokers) -> Vidéo -> « Passe le téléphone à J1 » -> Imitation J1
-> « Passe le téléphone à J2 » -> Imitation J2 -> Analyse IA -> Verdict de la manche
-> ... -> Résultats finaux (vainqueur, tableau des manches) -> Continuer / Revanche
```

## Mécaniques ajoutées
- **Joker (secousse)** : une fois par partie, chaque joueur touche sa carte Joker sur l'écran de manche puis **secoue le téléphone 3 fois** (accéléromètre, `ShakeDetector`). S'il gagne cette manche, ses points comptent **double**. Touche **S** dans l'éditeur pour simuler une secousse.
- **Score** : 1 point par manche gagnée (2 avec joker). Les pourcentages IA de chaque manche sont gardés : moyenne de précision par joueur et tableau final.
- **Fin de match** : écran de résultats, puis « Continuer » ouvre l'écran de victoire du jeu principal (`MiniGameBridge.Win`). Les étoiles dépendent de la précision moyenne du vainqueur (≥70 % : 3, ≥45 % : 2, sinon 1).

## Interface mobile
- Textes ≥ 34 px, boutons ≥ 140 px de haut, action principale toujours en bas (zone du pouce).
- Imitation : compte à rebours 3-2-1, **vu-mètre en direct** (suit la voix), barre de progression, **réécouter**, **refaire** (1 fois).
- Écran « Passe le téléphone » plein écran à la couleur du joueur.
- Verdict : cartes joueur avec barres animées, points gagnés, jokers.
- Pause plus grande, avec « Menu des jeux » pour revenir au jeu principal.

## Fichiers
- Nouveaux : `UI/MobileUI.cs`, `SetupPanel.cs`, `HandOverPanel.cs`, `VerdictPanel.cs`, `ResultsPanel.cs`, `Core/RoundRecord.cs`.
- Réécrits : `GameManager.cs`, `GameState.cs`, `RoundPanel.cs`, `VideoPanel.cs`, `RecordingPanel.cs`, `AIAnalysisPanel.cs`, `PauseOverlay.cs`, `Editor/SceneBuilder.cs`.
- Supprimés (remplacés par `SetupPanel` / `MobileUI`) : `NamesPanel.cs`, `MatchSetupPanel.cs`, `CategoryPanel.cs`, `ScreenScaffold.cs`.
- **Inchangés** : tout le juge IA (`AI/`), l'audio, l'accueil « Battle Poster » (juste un bouton retour ajouté en haut à gauche dans le jeu principal).

## Dans le jeu principal
- `MiniGameRegistry` : Sound Copy est en **Portrait** et cache le bouton pause partagé (elle a sa propre pause).
