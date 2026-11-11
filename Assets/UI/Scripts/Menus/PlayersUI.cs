using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    // Players scene: "Who's playing?" - the 2 names every minigame uses.
    // Recent duos are offered as one-tap buttons. Built by Tools > Game UI > Build Players + Turns;
    // every element can be moved and restyled by hand.
    public class PlayersUI : MonoBehaviour
    {
        [SerializeField] InputField player1Input;
        [SerializeField] InputField player2Input;
        [Tooltip("One button per recent duo (filled by the game; unused ones are hidden).")]
        [SerializeField] Button[] recentDuoButtons = new Button[0];
        [SerializeField] Button playButton;
        [SerializeField] Button backButton;
        [Tooltip("Optional: swaps the two names (who is Player 1).")]
        [SerializeField] Button swapButton;

        void Awake()
        {
            if (playButton != null) playButton.onClick.AddListener(OnPlay);
            if (backButton != null) backButton.onClick.AddListener(() => GameFlow.Instance.LoadMainMenu());
            if (swapButton != null) swapButton.onClick.AddListener(Swap);
        }

        void Start()
        {
            var flow = GameFlow.Instance;
            if (flow == null) return;
            string n1 = flow.PlayerName(1), n2 = flow.PlayerName(2);
            if (player1Input != null) player1Input.text = n1 == "Player 1" ? "" : n1;
            if (player2Input != null) player2Input.text = n2 == "Player 2" ? "" : n2;

            var duos = flow.RecentDuos();
            for (int i = 0; i < recentDuoButtons.Length; i++)
            {
                var b = recentDuoButtons[i];
                if (b == null) continue;
                bool used = i < duos.Count;
                b.gameObject.SetActive(used);
                if (!used) continue;
                var duo = duos[i];
                var label = b.GetComponentInChildren<Text>(true);
                if (label != null) label.text = duo.Item1 + " & " + duo.Item2;
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() =>
                {
                    if (player1Input != null) player1Input.text = duo.Item1;
                    if (player2Input != null) player2Input.text = duo.Item2;
                });
            }
        }

        void Swap()
        {
            if (player1Input == null || player2Input == null) return;
            (player1Input.text, player2Input.text) = (player2Input.text, player1Input.text);
        }

        void OnPlay()
        {
            GameFlow.Instance.SetPlayers(player1Input != null ? player1Input.text : "", player2Input != null ? player2Input.text : "");
            GameFlow.Instance.LoadLevelMenu();
        }
    }
}
