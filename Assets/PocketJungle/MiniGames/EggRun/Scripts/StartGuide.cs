using GameUI;
using UnityEngine;
using UnityEngine.UI;

// Start panel guide: the character waves at the player and explains how to play in a speech
// bubble, one tip at a time, typed out letter by letter.
//  - Tap the bubble: next tip (or finish typing). Each new tip pops the bubble in with a sound.
//  - Dots under the bubble show which tip you're on.
//  - Tap the character: it jumps for joy (Excited), then goes back to waving.
// Edit the tips in the Inspector.
public class StartGuide : MonoBehaviour
{
    [SerializeField] Text bubbleText;
    [Tooltip("Small \"tap\" hint under the text (optional). Hidden while a tip is being typed.")]
    [SerializeField] GameObject tapHint;
    [Tooltip("Tapping this goes to the next tip. Empty = a Button on this object.")]
    [SerializeField] Button nextButton;
    [TextArea(2, 4)]
    [SerializeField] string[] tips =
    {
        "Hi there! Help me carry this egg to the flag!",
        "Tilt your phone to keep the egg on the spoon.",
        "Hold the WALK button to move. Careful, I wobble!",
        "Shake the phone to scare the bird away.",
        "Sun too hot? Cover the front camera to make shade.",
        "Reach the flag before time runs out. Tap START!",
    };
    [SerializeField] float lettersPerSecond = 40f;
    [Tooltip("Goes to the next tip by itself after this many seconds (0 = only when tapped).")]
    [SerializeField] float autoNext = 4f;

    [Header("Pop-in")]
    [Tooltip("What pops in for each new tip. Empty = this object.")]
    [SerializeField] RectTransform popTarget;
    [SerializeField] float popDuration = 0.35f;
    [Tooltip("Size the bubble starts from when the panel opens (it grows out of its tail).")]
    [SerializeField, Range(0f, 1f)] float firstPopFrom = 0.2f;
    [Tooltip("Size it starts from for each next tip.")]
    [SerializeField, Range(0f, 1f)] float nextPopFrom = 0.75f;
    [SerializeField] string popSound = "bubble_pop";

    [Header("Progress dots")]
    [Tooltip("Row the dots are created in (one per tip). Empty = no dots.")]
    [SerializeField] RectTransform dotsParent;
    [SerializeField] Sprite dotSprite;
    [SerializeField] float dotSize = 26f;
    [SerializeField] Color dotOn = Color.white;
    [SerializeField] Color dotOff = new Color(1f, 1f, 1f, 0.35f);

    [Header("Tap the character")]
    [Tooltip("Button on the character picture.")]
    [SerializeField] Button characterButton;
    [SerializeField] CharacterPortrait portrait;
    [SerializeField] string reactTrigger = "React";
    [SerializeField] string reactSound = "character_cheer";
    [Tooltip("The little \"Tap me!\" text next to the character (gently pulses). Optional.")]
    [SerializeField] RectTransform tapMeLabel;

    [Header("Babble voice (cute mumbling while a tip is typed)")]
    [Tooltip("Sounds named <prefix>1 ... <prefix><count> in Assets/PocketJungle/Shared/Resources/Audio.")]
    [SerializeField] string babblePrefix = "babble_";
    [SerializeField] int babbleCount = 8;
    [Tooltip("One syllable every this many letters (2 = chatty, 4 = calm).")]
    [SerializeField, Range(1, 6)] int lettersPerSyllable = 2;
    [Tooltip("Voice pitch: 1 = as recorded, higher = smaller/cuter.")]
    [SerializeField, Range(0.5f, 2f)] float voicePitch = 1.1f;
    [Tooltip("Random pitch change per syllable, so it sounds like talking.")]
    [SerializeField, Range(0f, 0.5f)] float pitchVariation = 0.18f;
    [SerializeField, Range(0f, 1f)] float voiceVolume = 0.55f;

    AudioSource voice;
    AudioClip[] syllables = new AudioClip[0];
    int lettersSinceSyllable;
    int lastCount;

    int index;
    float shown;
    float waitTimer;
    float popTimer = 999f;
    float popFrom = 1f;
    Image[] dots = new Image[0];

    void Awake()
    {
        if (nextButton == null) nextButton = GetComponent<Button>();
        if (nextButton != null) nextButton.onClick.AddListener(Tap);
        if (popTarget == null) popTarget = transform as RectTransform;
        if (characterButton != null) characterButton.onClick.AddListener(React);
        BuildDots();

        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;
        voice.ignoreListenerPause = true;
        var list = new System.Collections.Generic.List<AudioClip>();
        for (int i = 1; i <= babbleCount; i++)
        {
            var clip = GameAudio.Get(babblePrefix + i);
            if (clip != null) list.Add(clip);
        }
        syllables = list.ToArray();
    }

    void OnEnable()
    {
        index = 0;
        Begin(true, firstPopFrom);
    }

    void Update()
    {
        // "Tap me!" gently pulses and wiggles so the player notices it.
        if (tapMeLabel != null && tapMeLabel.gameObject.activeInHierarchy)
        {
            float t = Time.unscaledTime;
            tapMeLabel.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(t * 4f));
            tapMeLabel.localRotation = Quaternion.Euler(0f, 0f, -8f + 4f * Mathf.Sin(t * 2.3f));
        }

        // Pop: quick overshoot from 85% to 100% size.
        if (popTarget != null && popTimer < popDuration)
        {
            popTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(popTimer / popDuration);
            // "back out" ease: overshoots a little past full size, then settles = springy pop
            const float c1 = 2.2f, c3 = c1 + 1f;
            float e = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
            popTarget.localScale = Vector3.one * Mathf.LerpUnclamped(popFrom, 1f, e);
            if (t >= 1f) popTarget.localScale = Vector3.one;
        }

        if (bubbleText == null || tips == null || tips.Length == 0) return;
        string tip = tips[index];
        if (shown < tip.Length)
        {
            shown += Time.unscaledDeltaTime * lettersPerSecond;
            int count = Mathf.Min(tip.Length, Mathf.FloorToInt(shown));
            for (int c = lastCount; c < count; c++) Babble(tip[c]);
            lastCount = count;
            bubbleText.text = tip.Substring(0, count);
            if (count >= tip.Length && tapHint != null) tapHint.SetActive(true);
        }
        else if (autoNext > 0f && index < tips.Length - 1)
        {
            waitTimer += Time.unscaledDeltaTime;
            if (waitTimer >= autoNext) Next();
        }
    }

    void Tap()
    {
        if (tips == null || tips.Length == 0) return;
        if (shown < tips[index].Length)
        {
            shown = tips[index].Length;            // finish typing right away
            bubbleText.text = tips[index];
            if (tapHint != null) tapHint.SetActive(true);
        }
        else Next();
    }

    void Next()
    {
        index = (index + 1) % tips.Length;
        Begin(true, nextPopFrom);
    }

    // One "syllable" every few letters; spaces and punctuation are little pauses.
    void Babble(char letter)
    {
        if (!char.IsLetterOrDigit(letter)) { lettersSinceSyllable = lettersPerSyllable - 1; return; }
        if (++lettersSinceSyllable < lettersPerSyllable) return;
        lettersSinceSyllable = 0;
        if (voice == null || syllables.Length == 0) return;
        voice.pitch = voicePitch * (1f + Random.Range(-pitchVariation, pitchVariation));
        voice.PlayOneShot(syllables[Random.Range(0, syllables.Length)], voiceVolume * GameSettings.SfxVolume);
    }

    void Begin(bool pop, float from)
    {
        shown = 0f;
        lastCount = 0;
        lettersSinceSyllable = lettersPerSyllable - 1;   // the first letter speaks
        waitTimer = 0f;
        if (bubbleText != null) bubbleText.text = "";
        if (tapHint != null) tapHint.SetActive(false);
        for (int i = 0; i < dots.Length; i++)
            if (dots[i] != null) dots[i].color = i == index ? dotOn : dotOff;
        if (pop)
        {
            popTimer = 0f;
            popFrom = from;
            if (popTarget != null) popTarget.localScale = Vector3.one * from;
            GameAudio.Play(popSound, 0.9f);
        }
    }

    void React()
    {
        if (portrait != null) portrait.Trigger(reactTrigger);
        GameAudio.Play(reactSound);
        Haptics.Pulse(40, 120);
    }

    void BuildDots()
    {
        if (dotsParent == null || tips == null) return;
        for (int i = dotsParent.childCount - 1; i >= 0; i--) Destroy(dotsParent.GetChild(i).gameObject);
        if (dotsParent.GetComponent<HorizontalLayoutGroup>() == null)
        {
            var row = dotsParent.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = dotSize * 0.6f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = false;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
        }
        dots = new Image[tips.Length];
        for (int i = 0; i < tips.Length; i++)
        {
            var go = new GameObject("Dot" + (i + 1), typeof(RectTransform), typeof(Image));
            go.transform.SetParent(dotsParent, false);
            ((RectTransform)go.transform).sizeDelta = Vector2.one * dotSize;
            var img = go.GetComponent<Image>();
            img.sprite = dotSprite;
            img.raycastTarget = false;
            img.color = dotOff;
            dots[i] = img;
        }
    }
}
