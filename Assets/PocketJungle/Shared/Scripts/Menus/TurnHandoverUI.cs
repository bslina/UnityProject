using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    // TurnHandover scene: shown between the 2 turns of a solo minigame (e.g. Egg Run).
    // "Lina's turn is over!" + the minigame's character (Registry > Result Character, e.g. Egg Run's
    // cheering character) saying "Pass the phone to Sam!" in a speech bubble that pops in and types out
    // with the babble voice. OK plays the same game for player 2.
    // A minigame without a character shows the plain "Pass card" instead.
    public class TurnHandoverUI : MonoBehaviour
    {
        [SerializeField] Text titleText;
        [Tooltip("What player 1 did (e.g. \"23.4 s left\").")]
        [SerializeField] Text detailText;
        [Tooltip("Plain card used when the minigame has no character.")]
        [SerializeField] GameObject passCard;
        [SerializeField] Text passText;
        [SerializeField] Button readyButton;
        [SerializeField] Button quitButton;

        [Header("Character (optional)")]
        [Tooltip("Shown/hidden together: the character and its speech bubble.")]
        [SerializeField] GameObject characterRoot;
        [SerializeField] CharacterPortrait portrait;
        [SerializeField] RectTransform bubble;
        [SerializeField] Text bubbleText;
        [SerializeField] float lettersPerSecond = 35f;
        [SerializeField] string babblePrefix = "babble_";
        [SerializeField] int babbleCount = 8;
        [SerializeField, Range(0f, 1f)] float voiceVolume = 0.55f;

        AudioSource voice;

        void Awake()
        {
            if (readyButton != null) readyButton.onClick.AddListener(() => GameFlow.Instance.PlaySecondTurn());
            if (quitButton != null) quitButton.onClick.AddListener(() => GameFlow.Instance.LoadLevelMenu());
        }

        void Start()
        {
            var flow = GameFlow.Instance;
            if (flow == null) return;
            var game = flow.Registry != null ? flow.Registry.Get(flow.LastGameIndex) : null;
            string p1 = MiniGameBridge.PlayerName(1), p2 = MiniGameBridge.PlayerName(2);   // Random Match names too
            string line = "Pass the phone to " + p2 + "!\nBeat " + p1 + "'s score.";

            if (titleText != null) titleText.text = (game != null ? game.displayName + ": " : "") + p1 + "'s turn is over!";
            if (detailText != null) detailText.text = p1 + ": " + flow.FirstTurnDetail;
            if (readyButton != null)
            {
                var label = readyButton.GetComponentInChildren<Text>(true);
                if (label != null) label.text = "OK, " + p2.ToUpperInvariant() + " IS READY!";
            }

            GameObject character = game != null ? game.resultCharacter : null;
            bool withCharacter = character != null && characterRoot != null;
            if (characterRoot != null) characterRoot.SetActive(withCharacter);
            if (passCard != null) passCard.SetActive(!withCharacter);
            if (passText != null) passText.text = line;

            if (withCharacter)
            {
                if (portrait != null) portrait.Show(character);
                StartCoroutine(Say(line));
            }
            else GameAudio.Play("point");
        }

        // The bubble pops in (growing out of its tail), then the line types out with the babble voice.
        IEnumerator Say(string line)
        {
            if (bubbleText != null) bubbleText.text = "";
            GameAudio.Play("bubble_pop", 0.9f);
            if (bubble != null)
            {
                for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
                {
                    float x = t / 0.35f;
                    const float c1 = 2.2f, c3 = c1 + 1f;
                    float e = 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
                    bubble.localScale = Vector3.one * Mathf.LerpUnclamped(0.2f, 1f, e);
                    yield return null;
                }
                bubble.localScale = Vector3.one;
            }
            if (bubbleText == null) yield break;

            var clips = new System.Collections.Generic.List<AudioClip>();
            for (int i = 1; i <= babbleCount; i++) { var c = GameAudio.Get(babblePrefix + i); if (c != null) clips.Add(c); }
            if (clips.Count > 0 && voice == null)
            {
                voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 0f;
            }

            float shown = 0f;
            int last = 0, sinceSyllable = 1;
            while (last < line.Length)
            {
                shown += Time.unscaledDeltaTime * lettersPerSecond;
                int count = Mathf.Min(line.Length, Mathf.FloorToInt(shown));
                for (int c = last; c < count; c++)
                {
                    if (!char.IsLetterOrDigit(line[c])) { sinceSyllable = 1; continue; }
                    if (++sinceSyllable < 2 || clips.Count == 0) continue;
                    sinceSyllable = 0;
                    voice.pitch = 1.1f * (1f + Random.Range(-0.18f, 0.18f));
                    voice.PlayOneShot(clips[Random.Range(0, clips.Count)], voiceVolume * GameSettings.SfxVolume);
                }
                last = count;
                bubbleText.text = line.Substring(0, count);
                yield return null;
            }
        }
    }
}
