using UnityEngine;

// Shapes the spoon's bowl: the egg rolls on 9 thin plates (Bowl 0 ... Bowl 8) laid out in a curve.
//   Bowl Size     1 = the original spoon, 0.7 = 30% narrower AND shallower (the egg falls off sooner)
//   Rim Plates    0 = full bowl; 1 = the two raised edge plates removed; 2 = four removed (flatter, very hard)
// Move the sliders in the Inspector and watch the spoon change in the Scene view.
// The Handle stays attached to the bowl and the bird lands on the new rim automatically.
// Put it on the Spoon (Tools > Egg Run > Spoon > Add Bowl Shape Controls).
[DisallowMultipleComponent]
public class SpoonShape : MonoBehaviour
{
    [Tooltip("1 = original size. Smaller = narrower and shallower bowl: the egg falls off more easily.")]
    [Range(0.5f, 1.2f)] public float bowlSize = 0.8f;
    [Tooltip("Removes the raised edge plates, pair by pair (0 = full bowl).")]
    [Range(0, 3)] public int rimPlatesRemoved = 0;

    // The original spoon (bowl size 1): x, y and tilt of each plate, from left (0) to right (8).
    static readonly float[] PlateX = { -0.258f, -0.199f, -0.135f, -0.068f, 0f, 0.068f, 0.135f, 0.199f, 0.258f };
    static readonly float[] PlateY = { 0.081f, 0.046f, 0.021f, 0.005f, 0f, 0.005f, 0.021f, 0.046f, 0.081f };
    static readonly float[] PlateTilt = { -35f, -26.3f, -17.5f, -8.8f, 0f, 8.8f, 17.5f, 26.3f, 35f };
    const float PlateLength = 0.076f;
    const float HandleLength = 0.4f;   // Handle's x scale
    const float HandleY = 0.07f;

    // Used by the bird to sit on the rim of the current bowl.
    public float RimX => OuterPlateX() * bowlSize;
    public float RimY => PlateY[OuterIndex()] * bowlSize;

    void Awake() => Apply();

#if UNITY_EDITOR
    void OnValidate()
    {
        // Changing the hierarchy inside OnValidate isn't allowed: do it right after.
        UnityEditor.EditorApplication.delayCall += () => { if (this != null) Apply(); };
    }
#endif

    int OuterIndex() => Mathf.Clamp(rimPlatesRemoved, 0, 3);
    float OuterPlateX() => PlateX[8 - OuterIndex()];

    public void Apply()
    {
        int removed = Mathf.Clamp(rimPlatesRemoved, 0, 3);
        for (int i = 0; i < 9; i++)
        {
            Transform plate = transform.Find("Bowl " + i);
            if (plate == null) continue;
            plate.localPosition = new Vector3(PlateX[i] * bowlSize, PlateY[i] * bowlSize, plate.localPosition.z);
            plate.localRotation = Quaternion.Euler(0f, 0f, PlateTilt[i]);
            Vector3 s = plate.localScale;
            plate.localScale = new Vector3(PlateLength * bowlSize, s.y, s.z);
            bool keep = i >= removed && i <= 8 - removed;
            if (plate.gameObject.activeSelf != keep) plate.gameObject.SetActive(keep);
        }

        // Keep the handle touching the left end of the bowl.
        Transform handle = transform.Find("Handle");
        if (handle != null)
        {
            float leftEdge = -OuterPlateX() * bowlSize - PlateLength * bowlSize * 0.5f;
            handle.localPosition = new Vector3(leftEdge - HandleLength * 0.5f + PlateLength * 0.5f,
                                               PlateY[OuterIndex()] * bowlSize + (HandleY - PlateY[0]), handle.localPosition.z);
        }
    }
}
