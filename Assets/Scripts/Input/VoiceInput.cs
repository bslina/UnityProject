using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

// Listens to the player's voice and turns three spoken words into game commands:
//   "brake"  ->  Command.Brake      "shoot"  ->  Command.Shoot      "bomb"  ->  Command.Bomb
// Every word you say is ONE command (say "shoot shoot shoot" = 3 commands).
//
// Backends:
//   Android  : the phone's own speech recogniser (android.speech.SpeechRecognizer), called from C#,
//              no plugin needed. It is restarted automatically after every phrase.
//   Windows  : UnityEngine.Windows.Speech.KeywordRecognizer (Editor + standalone).
//   Others   : no recogniser (CurrentStatus = Unavailable). TouchInput still maps the keyboard
//              (S = brake, Space = shoot, E = bomb) so you can test anywhere.
//
// See VOICE_SETUP.md for the Android manifest lines (microphone permission + "queries").
public class VoiceInput : MonoBehaviour
{
    public enum Command { Brake = 0, Shoot = 1, Bomb = 2 }
    public enum Status { Off, Starting, Listening, NoPermission, Unavailable }

    [Header("Android")]
    [Tooltip("Language of the recogniser, e.g. en-US or fr-FR. Empty = the phone's language.")]
    public string languageTag = "";
    [Tooltip("Mute the system 'listening' beep, which would otherwise play after every phrase.")]
    public bool muteBeep = true;

    // Fired on Unity's main thread, once per recognised word
    public event Action<Command> CommandHeard;

    public Status CurrentStatus { get { lock (gate) return status; } }
    public string StatusDetail { get { lock (gate) return detail; } }

    // Rough microphone loudness 0..1 (Android only), for the little level meter
    public float Level { get; private set; }

    // Words that count as each command (lower case). Includes common mis-hearings and French.
    static readonly Dictionary<string, Command> Words = new Dictionary<string, Command>
    {
        { "brake", Command.Brake }, { "brakes", Command.Brake }, { "braking", Command.Brake },
        { "break", Command.Brake }, { "frein", Command.Brake }, { "freine", Command.Brake },
        { "freiner", Command.Brake },

        { "shoot", Command.Shoot }, { "shoots", Command.Shoot }, { "shot", Command.Shoot },
        { "tire", Command.Shoot }, { "tir", Command.Shoot }, { "tirer", Command.Shoot },

        { "bomb", Command.Bomb }, { "bombs", Command.Bomb }, { "bom", Command.Bomb },
        { "bombe", Command.Bomb },
    };

    readonly object gate = new object();
    readonly Queue<Command> incoming = new Queue<Command>();
    Status status = Status.Off;
    string detail = "";
    bool wanted;

    // ------------------------------------------------------------------ public API

    public void StartListening()
    {
        if (wanted && (CurrentStatus == Status.Listening || CurrentStatus == Status.Starting)) return;

        wanted = true;
        SetStatus(Status.Starting, "");
        PlatformStart();
    }

    public void StopListening()
    {
        if (!wanted) return;

        wanted = false;
        PlatformStop();
        lock (gate) incoming.Clear();
        SetStatus(Status.Off, "");
        Level = 0f;
    }

    // ------------------------------------------------------------------ shared

    void SetStatus(Status s, string text)
    {
        lock (gate)
        {
            status = s;
            detail = text ?? "";
        }
    }

    // Safe from any thread
    void Enqueue(Command c)
    {
        lock (gate) incoming.Enqueue(c);
    }

    static void Count(string text, int[] counts)
    {
        if (string.IsNullOrEmpty(text)) return;

        var parts = text.ToLowerInvariant().Split(
            new[] { ' ', ',', '.', '!', '?', ';', ':', '-', '\'', '"' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var w in parts)
        {
            Command c;
            if (Words.TryGetValue(w, out c)) counts[(int)c]++;
        }
    }

    void Update()
    {
        // Hand the words heard (possibly on another thread) over to the game, on the main thread
        while (true)
        {
            Command c;
            lock (gate)
            {
                if (incoming.Count == 0) break;
                c = incoming.Dequeue();
            }
            if (wanted && CommandHeard != null) CommandHeard(c);
        }

        PlatformUpdate();
    }

    void OnDestroy()
    {
        wanted = false;
        PlatformStop();
        PlatformDestroy();
    }

    void OnApplicationPause(bool paused)
    {
        PlatformPause(paused);
    }

    // =================================================================== ANDROID
#if UNITY_ANDROID && !UNITY_EDITOR

    AndroidJavaObject activity, recognizer, intent, audio;
    AndroidJavaProxy listener;

    bool permAsked, permGranted, permDenied;
    bool beepMuted;
    float nextStartAt = -1f;       // main thread only
    float pendingDelay = -1f;      // set by the listener thread (under gate), read in Update
    volatile float rms;
    readonly int[] lastCounts = new int[3];   // words already counted in the current phrase (listener thread)

    void PlatformStart()
    {
        // Referencing Microphone makes Unity add RECORD_AUDIO to the manifest.
        // (We never open it: the speech recogniser needs the microphone for itself.)
        if (Microphone.devices == null) return;

        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            if (!permAsked)
            {
                permAsked = true;
                var cb = new PermissionCallbacks();
                cb.PermissionGranted += s => permGranted = true;
                cb.PermissionDenied += s => permDenied = true;
                cb.PermissionDeniedAndDontAskAgain += s => permDenied = true;
                Permission.RequestUserPermission(Permission.Microphone, cb);
            }
            return;   // Update() continues once the answer is in
        }

        BeginAndroid();
    }

    void BeginAndroid()
    {
        try
        {
            if (activity == null)
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            }
            RunOnUi(CreateAndStart);
        }
        catch (Exception e)
        {
            SetStatus(Status.Unavailable, e.Message);
        }
    }

    // SpeechRecognizer may only be used from the Android UI thread
    void RunOnUi(Action action)
    {
        activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            try { action(); }
            catch (Exception e) { SetStatus(Status.Unavailable, e.Message); }
        }));
    }

    void CreateAndStart()
    {
        if (!wanted) return;

        if (recognizer == null)
        {
            using (var cls = new AndroidJavaClass("android.speech.SpeechRecognizer"))
            {
                if (!cls.CallStatic<bool>("isRecognitionAvailable", activity))
                {
                    SetStatus(Status.Unavailable, "No speech service (check VOICE_SETUP.md)");
                    return;
                }
                recognizer = cls.CallStatic<AndroidJavaObject>("createSpeechRecognizer", activity);
            }

            listener = new Listener(this);
            recognizer.Call("setRecognitionListener", listener);

            intent = new AndroidJavaObject("android.content.Intent", "android.speech.action.RECOGNIZE_SPEECH");
            intent.Call<AndroidJavaObject>("putExtra", "android.speech.extra.LANGUAGE_MODEL", "free_form");
            intent.Call<AndroidJavaObject>("putExtra", "android.speech.extra.PARTIAL_RESULTS", true);
            intent.Call<AndroidJavaObject>("putExtra", "android.speech.extra.PREFER_OFFLINE", true);
            intent.Call<AndroidJavaObject>("putExtra", "android.speech.extra.MAX_RESULTS", 1);
            if (!string.IsNullOrEmpty(languageTag))
                intent.Call<AndroidJavaObject>("putExtra", "android.speech.extra.LANGUAGE", languageTag);
        }

        MuteBeep(true);
        Array.Clear(lastCounts, 0, lastCounts.Length);
        recognizer.Call("startListening", intent);
    }

    void PlatformUpdate()
    {
        if (!wanted) return;

        if (permGranted)
        {
            permGranted = false;
            BeginAndroid();
        }
        if (permDenied)
        {
            permDenied = false;
            permAsked = false;
            SetStatus(Status.NoPermission, "Microphone permission denied");
        }

        lock (gate)
        {
            if (pendingDelay >= 0f)
            {
                nextStartAt = Time.unscaledTime + pendingDelay;
                pendingDelay = -1f;
            }
        }

        if (nextStartAt >= 0f && Time.unscaledTime >= nextStartAt && recognizer != null)
        {
            nextStartAt = -1f;
            RunOnUi(() =>
            {
                if (!wanted) return;
                Array.Clear(lastCounts, 0, lastCounts.Length);
                recognizer.Call("startListening", intent);
            });
        }

        // Smooth loudness for the HUD meter
        float target = Mathf.Clamp01((rms + 2f) / 12f);
        Level = Mathf.Lerp(Level, target, 12f * Time.unscaledDeltaTime);
    }

    void PlatformStop()
    {
        nextStartAt = -1f;
        lock (gate) pendingDelay = -1f;

        if (recognizer != null && activity != null)
        {
            try { RunOnUi(() => recognizer.Call("cancel")); } catch (Exception) { }
        }
        MuteBeep(false);
    }

    void PlatformPause(bool paused)
    {
        if (!wanted || recognizer == null) return;

        if (paused)
        {
            nextStartAt = -1f;
            lock (gate) pendingDelay = -1f;
            try { RunOnUi(() => recognizer.Call("cancel")); } catch (Exception) { }
            MuteBeep(false);
        }
        else
        {
            MuteBeep(true);
            nextStartAt = Time.unscaledTime + 0.3f;
        }
    }

    void PlatformDestroy()
    {
        if (recognizer == null || activity == null) return;

        var r = recognizer;
        recognizer = null;
        try { activity.Call("runOnUiThread", new AndroidJavaRunnable(() => { try { r.Call("destroy"); } catch (Exception) { } })); }
        catch (Exception) { }
    }

    void MuteBeep(bool mute)
    {
        if (mute && !muteBeep) return;
        if (mute == beepMuted) return;

        try
        {
            if (audio == null) audio = activity.Call<AndroidJavaObject>("getSystemService", "audio");
            int direction = mute ? -100 : 100;          // ADJUST_MUTE / ADJUST_UNMUTE
            audio.Call("adjustStreamVolume", 5, direction, 0);   // STREAM_NOTIFICATION
            audio.Call("adjustStreamVolume", 1, direction, 0);   // STREAM_SYSTEM
            beepMuted = mute;
        }
        catch (Exception) { /* Do-Not-Disturb can refuse this: the beep is only a nuisance */ }
    }

    // ---- results (these arrive on the Android UI thread, not Unity's main thread)

    void OnReady()
    {
        SetStatus(Status.Listening, "");
    }

    void OnAndroidText(AndroidJavaObject bundle, bool final)
    {
        try
        {
            string best = FirstResult(bundle);
            if (!string.IsNullOrEmpty(best))
            {
                var counts = new int[3];
                Count(best, counts);

                // Partial results grow while you speak ("brake" -> "brake brake"):
                // only the NEW words are commands.
                for (int k = 0; k < 3; k++)
                {
                    for (int n = lastCounts[k]; n < counts[k]; n++) Enqueue((Command)k);
                    if (counts[k] > lastCounts[k]) lastCounts[k] = counts[k];
                }
            }
        }
        catch (Exception) { }

        if (final)
        {
            Array.Clear(lastCounts, 0, lastCounts.Length);
            ScheduleRestart(0.02f);
        }
    }

    static string FirstResult(AndroidJavaObject bundle)
    {
        if (bundle == null) return null;

        using (var list = bundle.Call<AndroidJavaObject>("getStringArrayList", "results_recognition"))
        {
            if (list == null || list.Call<int>("size") == 0) return null;
            using (var first = list.Call<AndroidJavaObject>("get", 0))
                return first != null ? first.Call<string>("toString") : null;
        }
    }

    void OnAndroidError(int error)
    {
        Array.Clear(lastCounts, 0, lastCounts.Length);

        switch (error)
        {
            case 9:                       // ERROR_INSUFFICIENT_PERMISSIONS
                SetStatus(Status.NoPermission, "Microphone permission denied");
                return;
            case 12:                      // ERROR_LANGUAGE_NOT_SUPPORTED
            case 13:                      // ERROR_LANGUAGE_UNAVAILABLE
                SetStatus(Status.Unavailable, "Language not available: try another languageTag");
                return;
            case 6:                       // ERROR_SPEECH_TIMEOUT (you were silent)
            case 7:                       // ERROR_NO_MATCH
                ScheduleRestart(0.02f);
                break;
            case 5:                       // ERROR_CLIENT
            case 8:                       // ERROR_RECOGNIZER_BUSY
                ScheduleRestart(0.4f);
                break;
            default:                      // network / server / audio problems
                ScheduleRestart(0.8f);
                break;
        }
    }

    void ScheduleRestart(float delay)
    {
        lock (gate) pendingDelay = delay;
    }

    class Listener : AndroidJavaProxy
    {
        readonly VoiceInput owner;

        public Listener(VoiceInput owner) : base("android.speech.RecognitionListener")
        {
            this.owner = owner;
        }

        public void onReadyForSpeech(AndroidJavaObject parameters) { owner.OnReady(); }
        public void onBeginningOfSpeech() { }
        public void onRmsChanged(float value) { owner.rms = value; }
        public void onBufferReceived(byte[] buffer) { }
        public void onEndOfSpeech() { }
        public void onError(int error) { owner.OnAndroidError(error); }
        public void onResults(AndroidJavaObject results) { owner.OnAndroidText(results, true); }
        public void onPartialResults(AndroidJavaObject partial) { owner.OnAndroidText(partial, false); }
        public void onEvent(int eventType, AndroidJavaObject parameters) { }
    }

    // =================================================================== WINDOWS
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN

    KeywordRecognizer recognizer;

    void PlatformStart()
    {
        try
        {
            if (recognizer == null)
            {
                var keywords = new string[Words.Count];
                Words.Keys.CopyTo(keywords, 0);

                recognizer = new KeywordRecognizer(keywords, ConfidenceLevel.Medium);
                recognizer.OnPhraseRecognized += OnPhrase;
            }
            if (!recognizer.IsRunning) recognizer.Start();
            SetStatus(Status.Listening, "");
        }
        catch (Exception e)
        {
            SetStatus(Status.Unavailable, "Windows speech: " + e.Message);
        }
    }

    void OnPhrase(PhraseRecognizedEventArgs args)
    {
        Command c;
        if (Words.TryGetValue(args.text.ToLowerInvariant(), out c)) Enqueue(c);
    }

    void PlatformUpdate() { }

    void PlatformStop()
    {
        if (recognizer != null && recognizer.IsRunning) recognizer.Stop();
    }

    void PlatformPause(bool paused) { }

    void PlatformDestroy()
    {
        if (recognizer == null) return;
        recognizer.OnPhraseRecognized -= OnPhrase;
        recognizer.Dispose();
        recognizer = null;
    }

    // =================================================================== OTHER (iOS, Mac editor, WebGL...)
#else

    void PlatformStart()
    {
        SetStatus(Status.Unavailable, "No speech recogniser on this platform (keyboard: S / Space / E)");
    }

    void PlatformUpdate() { }
    void PlatformStop() { }
    void PlatformPause(bool paused) { }
    void PlatformDestroy() { }

#endif
}
