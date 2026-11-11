using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    // RandomMatch scene: 2 players, 3 random minigames in a row, 1 point per round won.
    // The same scene is shown before, between and after the rounds; it picks its panel from GameFlow.Match:
    //   Setup      names of the 2 players, then DRAW
    //   Draw       slot machine picking the 3 games
    //   Board      last round's winner + score + next game (or "pass the phone" for a solo game's 2nd turn)
    //   Final      the champion, the recap, Rematch / Main menu
    // Built by Tools > Game UI > Build Random Match; every element can be moved and restyled by hand.
    public class RandomMatchUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] GameObject setupPanel;
        [SerializeField] GameObject drawPanel;
        [SerializeField] GameObject boardPanel;
        [SerializeField] GameObject finalPanel;

        [Header("Setup")]
        [SerializeField] InputField player1Input;
        [SerializeField] InputField player2Input;
        [SerializeField] Button drawButton;
        [SerializeField] Button backButton;

        [Header("Draw")]
        [Tooltip("One text per round card: shows the game's name.")]
        [SerializeField] Text[] slotTexts = new Text[3];
        [SerializeField] Button startButton;
        [SerializeField] float spinSeconds = 1.2f;
        [Tooltip("Extra spin for each next card, so they stop one after the other.")]
        [SerializeField] float spinStagger = 0.6f;

        [Header("Board (between rounds)")]
        [SerializeField] Text boardTitle;
        [SerializeField] Text resultText;
        [SerializeField] Text player1NameText;
        [SerializeField] Text player2NameText;
        [SerializeField] Text player1PointsText;
        [SerializeField] Text player2PointsText;
        [SerializeField] Text nextText;
        [SerializeField] Text historyText;
        [SerializeField] Button playButton;
        [SerializeField] Button quitButton;

        [Header("Final")]
        [SerializeField] Text winnerText;
        [SerializeField] Text finalScoreText;
        [SerializeField] Text recapText;
        [SerializeField] Button rematchButton;
        [SerializeField] Button menuButton;

        GameFlow Flow => GameFlow.Instance;
        RandomMatchState Match => Flow != null ? Flow.Match : null;

        void Awake()
        {
            if (drawButton != null) drawButton.onClick.AddListener(OnDraw);
            if (backButton != null) backButton.onClick.AddListener(() => Flow.LoadMainMenu());
            if (startButton != null) startButton.onClick.AddListener(() => Flow.PlayMatchRound());
            if (playButton != null) playButton.onClick.AddListener(() => Flow.PlayMatchRound());
            if (quitButton != null) quitButton.onClick.AddListener(() => Flow.LoadMainMenu());
            if (rematchButton != null) rematchButton.onClick.AddListener(OnRematch);
            if (menuButton != null) menuButton.onClick.AddListener(() => Flow.LoadMainMenu());
        }

        void Start()
        {
            var m = Match;
            if (m == null) ShowSetup();
            else if (m.Finished) ShowFinal();
            else if (!m.Drawn) ShowDraw();
            else ShowBoard();
        }

        void Show(GameObject panel)
        {
            foreach (var p in new[] { setupPanel, drawPanel, boardPanel, finalPanel })
                if (p != null) p.SetActive(p == panel);
        }

        // ---------------------------------------------------------------- setup

        void ShowSetup()
        {
            Show(setupPanel);
            if (player1Input != null) player1Input.text = Flow != null ? Flow.PlayerName(1) : "";
            if (player2Input != null) player2Input.text = Flow != null ? Flow.PlayerName(2) : "";
        }

        void OnDraw()
        {
            string n1 = player1Input != null ? player1Input.text : "";
            string n2 = player2Input != null ? player2Input.text : "";
            Flow.SetPlayers(n1, n2);
            Flow.StartRandomMatch(Flow.PlayerName(1), Flow.PlayerName(2));
            ShowDraw();
        }

        void OnRematch()
        {
            var m = Match;
            Flow.StartRandomMatch(m != null ? m.Names[0] : "", m != null ? m.Names[1] : "");
            ShowDraw();
        }

        // ---------------------------------------------------------------- draw

        void ShowDraw()
        {
            Show(drawPanel);
            var m = Match;
            if (m == null) { ShowSetup(); return; }
            m.Drawn = true;
            StopAllCoroutines();
            StartCoroutine(SpinRoutine(m));
        }

        IEnumerator SpinRoutine(RandomMatchState m)
        {
            if (startButton != null) startButton.gameObject.SetActive(false);
            var registry = Flow.Registry;
            int count = Mathf.Min(slotTexts.Length, m.Games.Count);
            float start = Time.unscaledTime;
            var stopped = new bool[count];
            int done = 0;
            float tick = 0f;

            while (done < count)
            {
                tick -= Time.unscaledDeltaTime;
                bool ticked = tick <= 0f;
                if (ticked) tick = 0.07f;
                for (int i = 0; i < count; i++)
                {
                    if (stopped[i] || slotTexts[i] == null) continue;
                    if (Time.unscaledTime - start >= spinSeconds + spinStagger * i)
                    {
                        stopped[i] = true;
                        done++;
                        slotTexts[i].text = GameName(m.Games[i]);
                        slotTexts[i].transform.localScale = Vector3.one * 1.15f;
                        GameAudio.Play("point");
                    }
                    else if (ticked)
                        slotTexts[i].text = GameName(Random.Range(0, registry != null ? registry.Count : 1));
                }
                if (ticked) GameAudio.Play("click", 0.35f);
                for (int i = 0; i < count; i++)
                    if (stopped[i] && slotTexts[i] != null)
                        slotTexts[i].transform.localScale = Vector3.Lerp(slotTexts[i].transform.localScale, Vector3.one, Time.unscaledDeltaTime * 8f);
                yield return null;
            }
            for (int i = count; i < slotTexts.Length; i++) if (slotTexts[i] != null) slotTexts[i].text = "-";
            yield return new WaitForSecondsRealtime(0.4f);
            for (int i = 0; i < count; i++) if (slotTexts[i] != null) slotTexts[i].transform.localScale = Vector3.one;
            if (startButton != null)
            {
                startButton.gameObject.SetActive(true);
                SetLabel(startButton, "PLAY ROUND 1");
            }
        }

        // ---------------------------------------------------------------- board

        void ShowBoard()
        {
            Show(boardPanel);
            var m = Match;
            var game = Flow.Registry != null ? Flow.Registry.Get(m.CurrentGame) : null;

            if (player1NameText != null) player1NameText.text = m.Names[0];
            if (player2NameText != null) player2NameText.text = m.Names[1];
            if (player1PointsText != null) player1PointsText.text = m.Points[0].ToString();
            if (player2PointsText != null) player2PointsText.text = m.Points[1].ToString();
            if (historyText != null) historyText.text = History(m);

            if (m.SoloTurn == 1)
            {
                // Solo game, first turn done: hand the phone over.
                SetText(boardTitle, GameName(m.CurrentGame) + ": " + m.Names[0] + " is done!");
                SetText(resultText, m.Names[0] + ": " + m.SoloDetails[0]);
                SetText(nextText, "Pass the phone to " + m.Names[1] + "!\nSame game, beat " + m.Names[0] + "'s score.");
                SetLabel(playButton, m.Names[1].ToUpperInvariant() + ", I'M READY!");
                return;
            }

            var last = m.Results.Count > 0 ? m.Results[m.Results.Count - 1] : null;
            if (last != null)
            {
                SetText(boardTitle, "ROUND " + m.Results.Count + ": " + GameName(last.GameIndex));
                SetText(resultText, (last.Winner == 0 ? "Draw! No point this round." : m.Name(last.Winner) + " wins the round!") +
                                    (string.IsNullOrEmpty(last.Detail) ? "" : "\n" + last.Detail));
                GameAudio.Play(last.Winner == 0 ? "click" : "point");
            }
            else
            {
                SetText(boardTitle, "LET'S GO!");
                SetText(resultText, "");
            }

            string round = m.IsTiebreaker ? "TIEBREAKER" : "ROUND " + (m.Round + 1);
            string who = game != null && !game.twoPlayer
                ? m.Names[0] + " plays first, then " + m.Names[1] + "."
                : "Player 1 = " + m.Names[0] + "   •   Player 2 = " + m.Names[1];
            SetText(nextText, (m.IsTiebreaker ? "It's a tie! One more game decides:\n" : "Next: ") +
                              round + " - " + GameName(m.CurrentGame) + "\n" + who);
            SetLabel(playButton, "PLAY " + round);
        }

        // ---------------------------------------------------------------- final

        void ShowFinal()
        {
            Show(finalPanel);
            var m = Match;
            int w = m.Winner;
            SetText(winnerText, w == 0 ? "IT'S A DRAW!" : m.Name(w).ToUpperInvariant() + " WINS THE MATCH!");
            SetText(finalScoreText, m.Names[0] + "  " + m.Points[0] + " - " + m.Points[1] + "  " + m.Names[1]);
            SetText(recapText, History(m));
            if (Flow.Audio != null) Flow.Audio.PlayResult(w != 0);
        }

        // ---------------------------------------------------------------- helpers

        string History(RandomMatchState m)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < m.Results.Count; i++)
            {
                var r = m.Results[i];
                sb.Append(i < RandomMatchState.Rounds ? "Round " + (i + 1) : "Tiebreaker").Append(": ")
                  .Append(GameName(r.GameIndex)).Append(" - ")
                  .Append(r.Winner == 0 ? "draw" : m.Name(r.Winner)).Append('\n');
            }
            return sb.ToString().TrimEnd();
        }

        string GameName(int index)
        {
            var g = Flow != null && Flow.Registry != null ? Flow.Registry.Get(index) : null;
            return g != null ? g.displayName : "?";
        }

        static void SetText(Text t, string s) { if (t != null) t.text = s; }

        static void SetLabel(Button b, string s)
        {
            if (b == null) return;
            var t = b.GetComponentInChildren<Text>(true);
            if (t != null) t.text = s;
        }
    }
}
