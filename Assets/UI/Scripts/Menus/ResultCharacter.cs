using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    // WinMenu / LoseMenu: the character of the minigame just played cheers the player on,
    // with a speech bubble ("It's okay! I'm sure we can do it next time...").
    // The character and the lines are set per minigame in the MiniGameRegistry
    // (Result Character, Win Line, Lose Line). Minigames without a character hide this object.
    public class ResultCharacter : MonoBehaviour
    {
        [Tooltip("Ticked in the WinMenu scene, unticked in the LoseMenu scene (picks the line).")]
        [SerializeField] bool isWinScreen = true;
        [SerializeField] CharacterPortrait portrait;
        [Tooltip("The speech bubble text (optional).")]
        [SerializeField] Text lineText;
        [Tooltip("Shown/hidden together with the character (e.g. the speech bubble). Empty = this object.")]
        [SerializeField] GameObject root;
        [Tooltip("Show this character when the scene is opened directly in the Editor (for placing it).")]
        [SerializeField] GameObject editorPreview;

        void Start()
        {
            if (root == null) root = gameObject;
            var flow = GameFlow.Instance;
            var game = flow != null && flow.Registry != null && flow.LastResult != null
                ? flow.Registry.Get(flow.LastResult.GameIndex) : null;

            GameObject character = game != null ? game.resultCharacter : null;
#if UNITY_EDITOR
            if (game == null && character == null) character = editorPreview;
#endif
            if (character == null) { root.SetActive(false); return; }

            root.SetActive(true);
            if (portrait != null) portrait.Show(character);

            if (lineText != null && game != null)
            {
                string line = isWinScreen ? game.winLine : game.loseLine;
                if (!string.IsNullOrEmpty(line)) lineText.text = line;
            }
        }
    }
}
