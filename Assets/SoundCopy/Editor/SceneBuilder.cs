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
        private const string SceneFolder = "Assets/SoundCopy/Scenes";
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
            EnsureFolder("Assets/SoundCopy", "Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Main";

            GameObject managerObject = new GameObject("GameManager");
            GameManager manager = managerObject.AddComponent<GameManager>();
            OnDeviceAIJudge aiJudge = managerObject.AddComponent<OnDeviceAIJudge>();
            ModelAsset yamNet = AssetDatabase.LoadAssetAtPath<ModelAsset>("Assets/SoundCopy/AI/Models/YamNet.onnx");
            LoadReferenceAudio(out AudioClip[] referenceClips, out string[] referenceIds);
            aiJudge.Initialize(yamNet, referenceClips, referenceIds);
            if (yamNet == null) Debug.LogError("Sound Copy: YamNet.onnx not found in Assets/SoundCopy/AI/Models/.");

            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 2400f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject safeArea = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
            safeArea.transform.SetParent(canvasObject.transform, false);
            RectTransform safeRect = safeArea.GetComponent<RectTransform>();
            safeRect.anchorMin = Vector2.zero;
            safeRect.anchorMax = Vector2.one;
            safeRect.offsetMin = Vector2.zero;
            safeRect.offsetMax = Vector2.zero;

            TMP_FontAsset displayFont = EnsureFontAsset("Assets/SoundCopy/Fonts/Anton-Regular.ttf", "Assets/SoundCopy/Fonts/Anton-Display.asset");
            TMP_FontAsset bodyFont = EnsureFontAsset("Assets/SoundCopy/Fonts/BarlowCondensed-Bold.ttf", "Assets/SoundCopy/Fonts/BarlowCondensed-UI.asset");
            Sprite homePoster = EnsureSprite("Assets/SoundCopy/Art/Home/battle_poster_background.png");

            // Shake detection for the jokers (accelerometer; press S in the Editor).
            ShakeDetector shake = managerObject.AddComponent<ShakeDetector>();

            // Every screen uses these fonts (Anton for titles/buttons, Barlow Condensed for text).
            MobileUI.DisplayFont = displayFont;
            MobileUI.BodyFont = bodyFont;

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
            VideoClip[] internationalClips = LoadVideoClips("Assets/SoundCopy/Media/Videos/International");
            VideoClip[] tunisianClips = LoadVideoClips("Assets/SoundCopy/Media/Videos/Tunisian");
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

            PauseOverlay pause = CreateFullScreenPanel("PauseOverlay", safeArea.transform).AddComponent<PauseOverlay>();
            pause.Initialize(manager);
            pause.BuildUI();
            pause.SetAvailable(false);

            manager.Configure(menu, setup, round, video, handOver, recording, analysis, verdict, results, pause, aiJudge, shake);
            foreach (Component panel in new Component[] { setup, round, video, handOver, recording, analysis, verdict, results })
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
            const string folder = "Assets/SoundCopy/AI/ReferenceAudio";
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

        private static TMP_FontAsset EnsureFontAsset(string fontPath, string assetPath)
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
