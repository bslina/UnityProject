# Photo Challenge : changements pour l'intégration (Pocket Jungle)

Fichier modifié : `Assets/PocketJungle/MiniGames/PhotoChallenge/Scripts/PhotoChallengeGame.cs` (la scène `Assets/PocketJungle/MiniGames/PhotoChallenge/Scenes/MainScene.unity` et les réglages du GameManager ne changent pas).

## Paysage
- Le jeu est en **paysage**, comme le reste de Pocket Jungle : Canvas 1920×1080, adapté à la hauteur.
- Écran de jeu : mission + image de référence à gauche, **grande caméra au centre**, chrono + bouton photo **en bas à droite** (sous le pouce).
- Accueil, joueurs, règles et résultats remis en page en deux colonnes. Les fonds jungle couvrent l'écran sans être déformés (voile sombre léger pour la lisibilité).

## Corrections
- **Rotation caméra** : l'aperçu tient compte de `videoRotationAngle` et `videoVerticallyMirrored`, garde les bonnes proportions, et la photo envoyée à l'IA est remise à l'endroit.
- **Permission caméra Android** demandée explicitement (avant : seulement iOS/WebGL).
- **Chrono** : le texte affiche la vraie durée (`Seconds Per Turn`, 15 s) au lieu de « 10 seconds » écrit en dur.
- **Passage du téléphone** : chaque tour commence par « Passe le téléphone à X » ; le chrono ne démarre que quand le joueur appuie sur **JE SUIS PRÊT**.
- **Pause** : le chrono utilise le temps du jeu, donc la pause de Pocket Jungle l'arrête.
- **Quitter** : dans Pocket Jungle, « RETOUR AUX JEUX » revient au menu des niveaux (avant : fermait toute l'appli).
- **Fin de partie** : nouveau bouton **CONTINUER** qui ouvre l'écran de victoire de Pocket Jungle (`MiniGameBridge.Win`, étoiles = score du gagnant sur 3).
- Textes en **français** (les clés de mission « A BOTTLE »… restent en anglais dans l'Inspector ; l'écran affiche « UNE BOUTEILLE »…).

## Inchangé
- Toute la comparaison IA (MobileCLIP, seuil 0.45, images de référence), les règles (6 tours, 1 point par photo validée), les couleurs orange/bleu.
- `Assets/PocketJungle/MiniGames/PhotoChallenge/Models/yolo11n.onnx` et `classes.txt` ne sont toujours pas utilisés (5 Mo dans le build) : à supprimer ou à utiliser plus tard.
