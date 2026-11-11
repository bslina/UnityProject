#if UNITY_EDITOR
using SoundCopy.UI;
using SoundCopy.AI;
using System.Collections.Generic;
using System.IO;
using TMPro;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace SoundCopy.Editor
{
    public static class SceneBuilder
    {
        private const string SceneFolder = "Assets/PocketJungle/MiniGames/SoundCopy/Scenes";
        private const string ScenePath = SceneFolder + "/Main.unity";

        [MenuItem("SoundCopy/Build Scenes")]
        public static void BuildScenes()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Sound Copy: Exit Play Mode before building scenes.");
                return;
            }

            EnsureFolder("Assets", "SoundCopy");
            EnsureFolder("Assets/PocketJungle/MiniGames/SoundCopy", "Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Main";

            GameObject managerObject = new GameObject("GameManager");
            GameManager manager = managerObject.AddComponent<GameManager>();
            OnDeviceAIJudge aiJudge = managerObject.AddComponent<OnDeviceAIJudge>();
            ModelAsset yamNet = AssetDatabase.LoadAssetAtPath<ModelAsset>("Assets/PocketJungle/MiniGames/SoundCopy/AI/Models/YamNet.onnx");
            LoadReferenceAudio(out AudioClip[] referenceClips, out string[] referenceIds);
            aiJudge.Initialize(yamNet, referenceClips, referenceIds);
            if (yamNet == null) Debug.LogError("Sound Copy: YamNet.onnx not found in Assets/PocketJungle/MiniGames/SoundCopy/AI/Models/.");

            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // Landscape, like the rest of the game: always 1080 px tall, 1920+ px wide.
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            GameObject safeArea = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
            safeArea.transform.SetParent(canvasObject.transform, false);
            RectTransform safeRect = safeArea.GetComponent<RectTransform>();
            safeRect.anchorMin = Vector2.zero;
            safeRect.anchorMax = Vector2.one;
            safeRect.offsetMin = Vector2.zero;
            safeRect.offsetMax = Vector2.zero;

            LoadStyle(out TMP_FontAsset displayFont, out TMP_FontAsset bodyFont);
            Sprite homePoster = EnsureSprite("Assets/PocketJungle/MiniGames/SoundCopy/Art/Home/battle_poster_background.png");

            ShakeDetector shake = null; // the shake joker was replaced by spinning the phone

            GameObject menuObject = CreateFullScreenPanel("MenuPanel", safeArea.transform);
            MenuPanel menu = menuObject.AddComponent<MenuPanel>();
            menu.Initialize(manager, displayFont, bodyFont, homePoster);
            menu.BuildUI();

            SetupPanel setup = CreateFullScreenPanel("SetupPanel", safeArea.transform).AddComponent<SetupPanel>();
            setup.Initialize(manager);
            setup.BuildUI();

            RoundPanel round = CreateFullScreenPanel("RoundPanel", safeArea.transform).AddComponent<RoundPanel>();
            round.Initialize(manager);
            round.BuildUI();

            VideoPanel video = CreateFullScreenPanel("VideoPanel", safeArea.transform).AddComponent<VideoPanel>();
            VideoClip[] internationalClips = LoadVideoClips("Assets/PocketJungle/MiniGames/SoundCopy/Media/Videos/International");
            VideoClip[] tunisianClips = LoadVideoClips("Assets/PocketJungle/MiniGames/SoundCopy/Media/Videos/Tunisian");
            video.Initialize(manager, internationalClips, tunisianClips);
            video.BuildUI();

            HandOverPanel handOver = CreateFullScreenPanel("HandOverPanel", safeArea.transform).AddComponent<HandOverPanel>();
            handOver.Initialize(manager);
            handOver.BuildUI();

            RecordingPanel recording = CreateFullScreenPanel("RecordingPanel", safeArea.transform).AddComponent<RecordingPanel>();
            recording.Initialize(manager);
            recording.BuildUI();

            AIAnalysisPanel analysis = CreateFullScreenPanel("AIAnalysisPanel", safeArea.transform).AddComponent<AIAnalysisPanel>();
            analysis.Initialize(manager);
            analysis.BuildUI();

            VerdictPanel verdict = CreateFullScreenPanel("VerdictPanel", safeArea.transform).AddComponent<VerdictPanel>();
            verdict.Initialize(manager);
            verdict.BuildUI();

            ResultsPanel results = CreateFullScreenPanel("ResultsPanel", safeArea.transform).AddComponent<ResultsPanel>();
            results.Initialize(manager);
            results.BuildUI();

            SpinPanel spin = CreateFullScreenPanel("SpinPanel", safeArea.transform).AddComponent<SpinPanel>();
            spin.Initialize(manager);
            spin.BuildUI();

            PauseOverlay pause = CreateFullScreenPanel("PauseOverlay", safeArea.transform).AddComponent<PauseOverlay>();
            pause.Initialize(manager);
            pause.BuildUI();
            pause.SetAvailable(false);

            manager.Configure(menu, setup, round, video, handOver, recording, analysis, verdict, results, pause, aiJudge, shake);
            manager.ConfigureSpin(spin);
            foreach (Component panel in new Component[] { setup, round, video, handOver, recording, analysis, verdict, results, spin })
                panel.gameObject.SetActive(false);
            menuObject.SetActive(true);

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = menuObject;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Sound Copy: Main scene built successfully at " + ScenePath);
        }

        // Fonts + pack sprites used by every generated screen.
        private static void LoadStyle(out TMP_FontAsset displayFont, out TMP_FontAsset bodyFont)
        {
            // Same fonts as the Pocket Jungle menus (Starborn titles, Cute Dino text). Ons's Barlow is kept as a
            // fallback so any letter missing from them still shows.
            TMP_FontAsset barlowFont = EnsureFontAsset("Assets/PocketJungle/MiniGames/SoundCopy/Fonts/BarlowCondensed-Bold.ttf", "Assets/PocketJungle/MiniGames/SoundCopy/Fonts/BarlowCondensed-UI.asset");
            displayFont = EnsureFontAsset("Assets/PocketJungle/Shared/Art/Fonts/starborn/Starborn.ttf", "Assets/PocketJungle/MiniGames/SoundCopy/Fonts/Starborn-Display.asset", barlowFont)
                ?? EnsureFontAsset("Assets/PocketJungle/MiniGames/SoundCopy/Fonts/Anton-Regular.ttf", "Assets/PocketJungle/MiniGames/SoundCopy/Fonts/Anton-Display.asset");
            bodyFont = EnsureFontAsset("Assets/PocketJungle/Shared/Art/Fonts/cute_dino_2/Cute Dino.otf", "Assets/PocketJungle/MiniGames/SoundCopy/Fonts/CuteDino-UI.asset", barlowFont)
                ?? barlowFont;
            LoadSkin();
            MobileUI.DisplayFont = displayFont;
            MobileUI.BodyFont = bodyFont;
        }

        // Texts that are replaced only if they still read exactly like the old generated text.
        private static readonly string[][] TextUpdates =
        {
            new[] { "LISTEN TO THE SOUND  >", "SPIN THE PHONE  >", "RoundPanel" },
            new[] { "Each player has <color=#FFF1B8>1 x2 joker</color>.\nYou choose the round to play it in!",
                    "Before each round, <color=#FFF1B8>spin the phone</color>:\nit picks who goes first, and a fast spin makes it a x2 round!", null },
            new[] { "Best one wins. Joker: shake for x2!", "Best one wins. Spin fast for a x2 round!", null },
            new[] { "JOKER ON  \u00B7  x2", "x2 ROUND", null },
            new[] { "Tap the mic when you're ready", "Raise the phone to your mouth to start\n(or tap the mic)", null },
        };

        [MenuItem("SoundCopy/Add Spin Screen (keeps your edits)")]
        public static void AddSpinScreen()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Sound Copy: Exit Play Mode first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameManager manager = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            SafeAreaFitter safeArea = Object.FindAnyObjectByType<SafeAreaFitter>(FindObjectsInactive.Include);
            if (manager == null || safeArea == null)
            {
                Debug.LogError("Sound Copy: GameManager or SafeArea not found in " + ScenePath + ".");
                return;
            }

            var log = new List<string>();
            SpinPanel spin = Object.FindAnyObjectByType<SpinPanel>(FindObjectsInactive.Include);
            if (spin == null)
            {
                LoadStyle(out _, out _);
                GameObject panel = CreateFullScreenPanel("SpinPanel", safeArea.transform);
                // Below the pause overlay, so the pause window stays on top.
                Transform pause = safeArea.transform.Find("PauseOverlay");
                if (pause != null) panel.transform.SetSiblingIndex(pause.GetSiblingIndex());
                spin = panel.AddComponent<SpinPanel>();
                spin.Initialize(manager);
                spin.BuildUI();
                panel.SetActive(false);
                Undo.RegisterCreatedObjectUndo(panel, "Add Spin Screen");
                log.Add("added SpinPanel (under Canvas > SafeArea, hidden; turn it on to style it)");
            }
            Undo.RecordObject(manager, "Add Spin Screen");
            manager.ConfigureSpin(spin);
            EditorUtility.SetDirty(manager);

            // The old joker cards on the round screen: hidden, not deleted.
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if ((t.name == "Jokers" || t.name == "JokerLabel" || t.name == "JokerHint") && t.GetComponentInParent<RoundPanel>(true) != null && t.gameObject.activeSelf)
                {
                    Undo.RecordObject(t.gameObject, "Hide joker");
                    t.gameObject.SetActive(false);
                    log.Add("hid " + t.name);
                }
            }

            foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
            {
                foreach (string[] update in TextUpdates)
                {
                    if (text.text != update[0]) continue;
                    if (update[2] == "RoundPanel" && text.GetComponentInParent<RoundPanel>(true) == null) continue;
                    Undo.RecordObject(text, "Update text");
                    text.text = update[1];
                    EditorUtility.SetDirty(text);
                    log.Add("text \"" + update[0].Replace("\n", " ") + "\" -> \"" + update[1].Replace("\n", " ") + "\"");
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Sound Copy: spin screen ready. " + (log.Count == 0 ? "Nothing to change." : string.Join("; ", log)));
        }

        private static VideoClip[] LoadVideoClips(string folder)
        {
            List<VideoClip> clips = new List<VideoClip>();
            string[] guids = AssetDatabase.FindAssets("t:VideoClip", new[] { folder });
            foreach (string guid in guids)
            {
                VideoClip clip = AssetDatabase.LoadAssetAtPath<VideoClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip != null) clips.Add(clip);
            }
            clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            if (clips.Count == 0) Debug.LogWarning("Sound Copy: no videos found in " + folder);
            return clips.ToArray();
        }

        private static void LoadReferenceAudio(out AudioClip[] clips, out string[] ids)
        {
            const string folder = "Assets/PocketJungle/MiniGames/SoundCopy/AI/ReferenceAudio";
            List<AudioClip> clipList = new List<AudioClip>();
            List<string> idList = new List<string>();
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer != null)
                {
                    AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                    bool changed = settings.loadType != AudioClipLoadType.DecompressOnLoad || settings.compressionFormat != AudioCompressionFormat.PCM || !settings.preloadAudioData;
                    settings.loadType = AudioClipLoadType.DecompressOnLoad;
                    settings.compressionFormat = AudioCompressionFormat.PCM;
                    settings.preloadAudioData = true;
                    importer.defaultSampleSettings = settings;
                    if (changed) importer.SaveAndReimport();
                }
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) { clipList.Add(clip); idList.Add(Path.GetFileNameWithoutExtension(path)); }
            }
            clips = clipList.ToArray();
            ids = idList.ToArray();
            Debug.Log("Sound Copy: loaded " + clips.Length + " AI audio references.");
        }

        private static GameObject CreateFullScreenPanel(string name, Transform parent)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            return panel;
        }

        private static Sprite EnsureSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("Sound Copy: poster background not found at " + path);
                return null;
            }
            bool changed = importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single || importer.mipmapEnabled;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 4096;
            if (changed) importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Sprites from the menus' 2D Casual UI pack (same art as the Pocket Jungle menus).
        private const string PackSheet = "Assets/ThirdParty/2D Casual UI/Sprite/GUI.png";
        private const string PageBackgroundPath = "Assets/PocketJungle/Shared/Art/background/download (13).jpg";

        private static void LoadSkin()
        {
            var pack = new Dictionary<string, Sprite>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(PackSheet))
                if (asset is Sprite sprite) pack[sprite.name] = sprite;
            Sprite Pick(string name)
            {
                if (pack.TryGetValue(name, out Sprite sprite)) return sprite;
                Debug.LogWarning("Sound Copy: sprite " + name + " not found in " + PackSheet + " (a plain shape is used instead).");
                return null;
            }

            SoundCopySkin.Rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            SoundCopySkin.Circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            SoundCopySkin.PageBackground = AssetDatabase.LoadAssetAtPath<Sprite>(PageBackgroundPath);
            SoundCopySkin.Panel = Pick("GUI_28");        // framed window
            SoundCopySkin.Button = Pick("GUI_11");       // green button, 9-sliced on all sides
            SoundCopySkin.RoundButton = Pick("GUI_7");   // round green button
            SoundCopySkin.Ribbon = Pick("GUI_68");       // title ribbon
            SoundCopySkin.IconBack = Pick("GUI_55");     // arrow (flipped to point back)
            SoundCopySkin.IconPause = Pick("GUI_34");
            SoundCopySkin.IconTrophy = Pick("GUI_62");
            SoundCopySkin.BarTrack = Pick("GUI_13");     // dark bar
            if (SoundCopySkin.PageBackground == null) Debug.LogWarning("Sound Copy: background not found at " + PageBackgroundPath + " (import it as Sprite).");
        }

        private static TMP_FontAsset EnsureFontAsset(string fontPath, string assetPath, TMP_FontAsset fallback = null)
        {
            TMP_FontAsset result = CreateOrLoadFontAsset(fontPath, assetPath);
            if (result != null && fallback != null && result != fallback)
            {
                if (result.fallbackFontAssetTable == null) result.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (!result.fallbackFontAssetTable.Contains(fallback)) result.fallbackFontAssetTable.Add(fallback);
                EditorUtility.SetDirty(result);
                AssetDatabase.SaveAssets();
            }
            return result;
        }

        private static TMP_FontAsset CreateOrLoadFontAsset(string fontPath, string assetPath)
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null)
            {
                existing.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                return existing;
            }
            TrueTypeFontImporter fontImporter = AssetImporter.GetAtPath(fontPath) as TrueTypeFontImporter;
            if (fontImporter != null && !fontImporter.includeFontData)
            {
                fontImporter.includeFontData = true;
                fontImporter.SaveAndReimport();
            }
            Font source = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (source == null)
            {
                Debug.LogError("Sound Copy: font file not found at " + fontPath);
                return null;
            }
            TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(source);
            created.name = Path.GetFileNameWithoutExtension(assetPath);
            created.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            AssetDatabase.CreateAsset(created, assetPath);
            if (created.atlasTextures != null)
            {
                foreach (Texture2D atlas in created.atlasTextures)
                {
                    if (atlas != null && !AssetDatabase.Contains(atlas))
                        AssetDatabase.AddObjectToAsset(atlas, created);
                }
            }
            if (created.material != null && !AssetDatabase.Contains(created.material))
                AssetDatabase.AddObjectToAsset(created.material, created);
            EditorUtility.SetDirty(created);
            AssetDatabase.SaveAssets();
            return created;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
