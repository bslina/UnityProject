# Sound Copy : changements pour l'intégration (Pocket Jungle)

**Important : après avoir récupéré ces changements, ouvrir Unity, attendre zéro erreur rouge, puis lancer `SoundCopy > Build Scenes`.** Cela régénère `Assets/PocketJungle/MiniGames/SoundCopy/Scenes/Main.unity` avec la nouvelle interface.

## Nouveau parcours
```
Accueil -> Nouvelle partie (prénoms + catégorie, 1 seul écran ; une partie = toujours 3 manches)
-> Manche (scores + jokers) -> Vidéo -> « Passe le téléphone à J1 » -> Imitation J1
-> « Passe le téléphone à J2 » -> Imitation J2 -> Analyse IA -> Verdict de la manche
-> ... -> Résultats finaux (vainqueur, tableau des manches) -> Continuer / Revanche
```

## Mécaniques ajoutées
- **Partie en 3 manches fixes** (plus de choix 1/3/5).
- **Tourner le téléphone (gyroscope)** : avant chaque manche, le téléphone est posé à plat entre les joueurs (J1 côté bas de l'écran, J2 côté haut) et on le fait tourner comme une bouteille. Quand il s'arrête, le côté vers lequel pointe le haut de l'écran commence. Une rotation rapide = **manche x2** (points doublés). Éditeur : touche ESPACE ou bouton « Spin for me ». (Le joker par secousse est supprimé.)
- **Lever pour enregistrer (accéléromètre)** : l'enregistrement démarre quand le joueur lève le téléphone vers sa bouche comme un micro (le bouton micro marche toujours). Le baisser pendant le 3-2-1 annule la prise. Éditeur : maintenir R.
- **Écran Spin sans reconstruire la scène** : menu **SoundCopy > Add Spin Screen (keeps your edits)**. Il ajoute seulement le panneau SpinPanel, cache les anciennes cartes joker et ne change que les textes encore identiques aux textes générés.
- **Score** : 1 point par manche gagnée (2 sur une manche x2). Les pourcentages IA de chaque manche sont gardés : moyenne de précision par joueur et tableau final.
- **Fin de match** : écran de résultats, puis « Continuer » ouvre l'écran de victoire du jeu principal (`MiniGameBridge.Win`). Les étoiles dépendent de la précision moyenne du vainqueur (≥70 % : 3, ≥45 % : 2, sinon 1).

## Interface mobile (paysage)
- Le jeu est maintenant en **paysage**, comme le reste de Pocket Jungle : Canvas 1920×1080, adapté à la **hauteur** (toujours 1080 px de haut, 1920 à ~2400 px de large selon le téléphone).
- Écrans en deux colonnes ; l'action principale est toujours **en bas à droite** (sous le pouce droit).
- Textes ≥ 30 px, boutons ≥ 120 px de haut.
- Imitation : compte à rebours 3-2-1, **vu-mètre en direct** (suit la voix), barre de progression, **réécouter**, **refaire** (1 fois).
- Écran « Passe le téléphone » plein écran à la couleur du joueur.
- Verdict : cartes joueur avec barres animées, points gagnés, jokers.
- Pause plus grande, avec « Menu des jeux » pour revenir au jeu principal.

## Fichiers
- Nouveaux : `UI/MobileUI.cs`, `SetupPanel.cs`, `HandOverPanel.cs`, `VerdictPanel.cs`, `ResultsPanel.cs`, `Core/RoundRecord.cs`.
- Réécrits : `GameManager.cs`, `GameState.cs`, `RoundPanel.cs`, `VideoPanel.cs`, `RecordingPanel.cs`, `AIAnalysisPanel.cs`, `PauseOverlay.cs`, `Editor/SceneBuilder.cs`.
- Supprimés (remplacés par `SetupPanel` / `MobileUI`) : `NamesPanel.cs`, `MatchSetupPanel.cs`, `CategoryPanel.cs`, `ScreenScaffold.cs`.
- **Inchangés** : tout le juge IA (`AI/`) et l'audio. L'accueil « Battle Poster » garde son style mais est remis en page en paysage (titre à gauche, Play au centre, J1/J2 à droite).

## Dans le jeu principal
- `MiniGameRegistry` : Sound Copy est en **paysage** et cache le bouton pause partagé (elle a sa propre pause).
