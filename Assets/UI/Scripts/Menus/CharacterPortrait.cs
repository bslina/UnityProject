using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    // Shows an animated 3D character inside a UI RawImage (like a video call window).
    // At runtime it builds a tiny hidden "photo studio" far below the level: the character,
    // a light and a camera that films it into a texture with a see-through background.
    // Put it on a RawImage; move / resize the RawImage to place the character on screen.
    [RequireComponent(typeof(RawImage))]
    public class CharacterPortrait : MonoBehaviour
    {
        [Tooltip("The character to show (a prefab with an Animator that already plays the right animation).")]
        public GameObject characterPrefab;
        [Tooltip("Texture size. Bigger = sharper but slower.")]
        [SerializeField] int resolution = 768;
        [Tooltip("How much of the character is framed: 1 = whole body, 0.6 = from the knees up.")]
        [SerializeField, Range(0.3f, 1f)] float framing = 1f;
        [Tooltip("Turns the character left/right (0 = looking straight at the player).")]
        [SerializeField] float turnAngle = 0f;
        [Tooltip("Animation speed: 1 = normal, 0.7 = calmer, 0.5 = half speed. Can be changed while playing.")]
        [SerializeField, Range(0.2f, 1.5f)] float animationSpeed = 0.7f;
        [Header("Brightness")]
        [SerializeField] Color lightColor = new Color(1f, 0.96f, 0.9f);
        [Tooltip("Strength of the light in front of the character.")]
        [SerializeField, Range(0f, 4f)] float lightIntensity = 1.6f;
        [Tooltip("Lights the character evenly from inside, like a cartoon. 0 = only the studio lights.")]
        [SerializeField, Range(0f, 1f)] float selfLight = 0.45f;
        [Tooltip("Multiplies the character's colours (1 = as in the material, higher = punchier).")]
        [SerializeField, Range(0.5f, 2f)] float colorBoost = 1.2f;

        static int studioCount;

        RawImage image;
        RenderTexture texture;
        GameObject studio;
        Animator[] animators = new Animator[0];

        // Fires an Animator trigger on the character (e.g. "React" when the player taps it).
        void Update()
        {
            foreach (var a in animators)
                if (a != null) a.speed = animationSpeed;
        }

        public void Trigger(string trigger)
        {
            foreach (var a in animators)
                if (a != null && a.runtimeAnimatorController != null) a.SetTrigger(trigger);
        }

        void Awake()
        {
            image = GetComponent<RawImage>();
        }

        void OnEnable()
        {
            if (studio == null) Build();
            if (studio != null) studio.SetActive(true);
        }

        void OnDisable()
        {
            if (studio != null) studio.SetActive(false);
        }

        void OnDestroy()
        {
            if (studio != null) Destroy(studio);
            if (texture != null) texture.Release();
        }

        // Swap the character at runtime (e.g. the Win screen picks the last minigame's character).
        public void Show(GameObject prefab)
        {
            characterPrefab = prefab;
            if (studio != null) Destroy(studio);
            studio = null;
            if (isActiveAndEnabled) Build();
        }

        void Build()
        {
            if (characterPrefab == null) { image.enabled = false; return; }
            image.enabled = true;

            // Each portrait gets its own spot, far from the level and from each other.
            Vector3 spot = new Vector3(studioCount++ * 40f, -2000f, 0f);
            studio = new GameObject("CharacterPortraitStudio (" + name + ")");
            studio.transform.position = spot;

            GameObject character = Instantiate(characterPrefab, spot, Quaternion.Euler(0f, 180f + turnAngle, 0f), studio.transform);
            animators = character.GetComponentsInChildren<Animator>();
            foreach (var anim in animators)
            {
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                anim.updateMode = AnimatorUpdateMode.UnscaledTime;   // keeps moving in pause / result menus
            }
            foreach (var r in character.GetComponentsInChildren<SkinnedMeshRenderer>())
                r.updateWhenOffscreen = true;
            Brighten(character);

            // Frame the character from its size (models come in at very different scales).
            Bounds bounds = new Bounds(spot + Vector3.up, Vector3.one * 0.1f);
            bool first = true;
            foreach (var r in character.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            float height = Mathf.Max(0.1f, bounds.size.y);
            float shown = height * framing * 1.15f;                      // a bit of room for waving arms
            Vector3 lookAt = new Vector3(bounds.center.x, bounds.max.y - shown * 0.5f + height * 0.04f, bounds.center.z);

            float aspect = 1f;
            var rt = (RectTransform)transform;
            if (rt.rect.height > 1f) aspect = Mathf.Clamp(rt.rect.width / rt.rect.height, 0.25f, 4f);

            const float fov = 30f;
            float distance = shown * 0.5f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            if (aspect < 1f) distance /= aspect;                         // tall-and-thin window: back up

            var camGO = new GameObject("Camera");
            camGO.transform.SetParent(studio.transform, false);
            camGO.transform.position = lookAt + Vector3.back * distance;
            camGO.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            var cam = camGO.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = Mathf.Max(0.01f, distance - height * 2f);
            cam.farClipPlane = distance + height * 2f;                   // sees only the studio
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);             // see-through background
            cam.allowHDR = false;
            cam.allowMSAA = true;
            cam.depth = -10;

            int w = Mathf.RoundToInt(resolution * Mathf.Min(1f, aspect));
            int h = Mathf.RoundToInt(resolution * Mathf.Min(1f, 1f / aspect));
            texture = new RenderTexture(Mathf.Max(64, w), Mathf.Max(64, h), 24, RenderTextureFormat.ARGB32);
            texture.antiAliasing = 2;
            texture.name = "CharacterPortrait";
            cam.targetTexture = texture;
            cam.aspect = (float)texture.width / texture.height;
            image.texture = texture;
            image.color = Color.white;

            // Soft key light in front + a fill from the other side (lights only reach the studio).
            AddLight(lookAt, new Vector3(-height, height * 0.8f, -height * 1.5f), lightIntensity);
            AddLight(lookAt, new Vector3(height * 1.2f, height * 0.2f, -height), lightIntensity * 0.5f);
            AddLight(lookAt, new Vector3(0f, height * 0.6f, height * 1.2f), lightIntensity * 0.6f);   // rim light from behind
        }

        // Point lights fade with distance squared, so scale them to arrive at the character with 'intensity'.
        void AddLight(Vector3 lookAt, Vector3 offset, float intensity)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(studio.transform, false);
            go.transform.position = lookAt + offset;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = lightColor;
            light.intensity = intensity * offset.sqrMagnitude;
            light.range = offset.magnitude * 3f;
            light.shadows = LightShadows.None;
        }

        // Copies of the materials (the game's character is untouched): punchier colours + a bit of
        // emission using the character's own texture, so it never looks grey whatever the scene lighting.
        void Brighten(GameObject character)
        {
            foreach (var r in character.GetComponentsInChildren<Renderer>())
            {
                var mats = r.materials;                       // instances
                foreach (var m in mats)
                {
                    if (m.HasProperty("_BaseColor"))
                    {
                        Color c = m.GetColor("_BaseColor");
                        m.SetColor("_BaseColor", new Color(Mathf.Min(1f, c.r * colorBoost), Mathf.Min(1f, c.g * colorBoost), Mathf.Min(1f, c.b * colorBoost), c.a));
                    }
                    if (selfLight > 0f && m.HasProperty("_EmissionColor"))
                    {
                        Texture baseMap = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                        if (baseMap != null && m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", baseMap);
                        m.SetColor("_EmissionColor", Color.white * selfLight);
                        m.EnableKeyword("_EMISSION");
                        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    }
                }
                r.materials = mats;
            }
        }
    }
}
