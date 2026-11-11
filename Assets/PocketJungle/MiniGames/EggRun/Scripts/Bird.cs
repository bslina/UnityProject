using UnityEngine;
using UnityEngine.UI;

// A bird that flies in at set points on the track and lands on one rim of the spoon.
// Its weight tips the spoon toward it (more the longer it stays) and it pecks the egg.
// Shaking the phone scares it away. The phone buzzes when it lands, while it sits, and when it leaves.
// All the logic lives here; the look is the "visual" child, so it can be swapped for any model.
public class Bird : MonoBehaviour
{
    enum State { Away, FlyingIn, Perched, FlyingOff }

    [SerializeField] PlayerWalker walker;
    [SerializeField] SpoonController spoon;
    [SerializeField] Rigidbody egg;
    [Tooltip("The bird's model. Shown only while the bird is around. Should face +X (right).")]
    [SerializeField] GameObject visual;
    [Tooltip("Optional wing transforms to flap while flying.")]
    [SerializeField] Transform wingLeft;
    [SerializeField] Transform wingRight;
    [Tooltip("Optional \"SHAKE!\" hint shown while the bird sits on the spoon.")]
    [SerializeField] Text hintText;

    [Header("When")]
    [Tooltip("The bird comes in when the player passes each of these X positions.")]
    [SerializeField] float[] landAtX = { 12f, 28f, 44f };
    [SerializeField] float flightTime = 1.2f;
    [Tooltip("One thing at a time: if the sun is out, the bird waits until it has been gone this many seconds.")]
    [SerializeField] float waitAfterSun = 2f;

    [Header("On the spoon")]
    [Tooltip("Where the bird sits, relative to the spoon. X is mirrored for the left rim.")]
    [SerializeField] Vector3 perchOffset = new Vector3(0.26f, 0.14f, 0f);
    [Tooltip("How much its weight tips the spoon when it lands, in degrees.")]
    [SerializeField] float weightTilt = 6f;
    [Tooltip("Extra tip per second while it stays, so waiting gets worse.")]
    [SerializeField] float weightGrowth = 3f;
    [SerializeField] float maxWeightTilt = 20f;
    [SerializeField] float peckInterval = 1.5f;
    [Tooltip("How hard each peck knocks the egg.")]
    [SerializeField] float peckImpulse = 0.03f;

    State state = State.Away;
    int nextLanding;
    float side;          // +1 = right rim, -1 = left rim
    float timer;
    float peckTimer;
    float buzzTimer;
    float weight;
    Vector3 flyFrom;
    Vector3 baseScale;
    SunGlare sun;
    float sunQuietFor = 999f;

    // True from the moment it starts flying in until it has flown away (the sun waits meanwhile).
    public bool IsAround => state != State.Away;

    void Start()
    {
        baseScale = transform.localScale;
        sun = FindAnyObjectByType<SunGlare>();
        if (ShakeDetector.Instance != null) ShakeDetector.Instance.Shaken += OnShaken;
        Show(false);
    }

    void OnDestroy()
    {
        if (ShakeDetector.Instance != null) ShakeDetector.Instance.Shaken -= OnShaken;
    }

    void Update()
    {
        Flap();

        switch (state)
        {
            case State.Away:
                // One thing at a time: wait for the sun to be gone (and a short calm moment after it).
                sunQuietFor = sun != null && sun.IsSunOut ? 0f : sunQuietFor + Time.deltaTime;
                if (walker.CanWalk && nextLanding < landAtX.Length && walker.transform.position.x >= landAtX[nextLanding]
                    && sunQuietFor >= waitAfterSun)
                {
                    nextLanding++;
                    FlyIn();
                }
                break;

            case State.FlyingIn:
                timer += Time.deltaTime;
                transform.position = Arc(flyFrom, PerchPoint(), timer / flightTime);
                if (!walker.CanWalk) FlyOff();
                else if (timer >= flightTime) Land();
                break;

            case State.Perched:
                transform.position = PerchPoint();
                weight = Mathf.Min(weight + weightGrowth * Time.deltaTime, maxWeightTilt);
                spoon.ExtraTilt = side * weight;
                Pecking();
                if (hintText != null)
                    hintText.transform.localScale = Vector3.one * (1f + 0.1f * Mathf.Sin(Time.time * 10f));
                if (!walker.CanWalk) FlyOff();   // round over
                break;

            case State.FlyingOff:
                timer += Time.deltaTime;
                transform.position = Arc(flyFrom, flyFrom + new Vector3(side * 4f, 4f, 0f), timer / flightTime);
                // Let the spoon level out gently instead of snapping back.
                spoon.ExtraTilt = Mathf.MoveTowards(spoon.ExtraTilt, 0f, 40f * Time.deltaTime);
                if (timer >= flightTime)
                {
                    spoon.ExtraTilt = 0f;
                    state = State.Away;
                    Show(false);
                }
                break;
        }
    }

    void FlyIn()
    {
        side = Random.value < 0.5f ? -1f : 1f;
        // Face the middle of the spoon (the model faces +X).
        transform.localScale = new Vector3(-side * Mathf.Abs(baseScale.x), baseScale.y, baseScale.z);
        flyFrom = PerchPoint() + new Vector3(side * 4f, 3f, 0f);
        transform.position = flyFrom;
        timer = 0f;
        state = State.FlyingIn;
        Show(true);
    }

    void Land()
    {
        state = State.Perched;
        weight = weightTilt;
        peckTimer = peckInterval;
        buzzTimer = 1f;
        if (hintText != null) hintText.gameObject.SetActive(true);
        Haptics.Pulse(150, 255);
        GameUI.GameAudio.Play("bird_land");
    }

    void Pecking()
    {
        buzzTimer -= Time.deltaTime;
        if (buzzTimer <= 0f)
        {
            buzzTimer = 1f;
            Haptics.Pulse(40, 90);   // light reminder that the bird is still there
        }

        peckTimer -= Time.deltaTime;
        if (peckTimer <= 0f)
        {
            peckTimer = peckInterval;
            if (egg != null && !egg.isKinematic)
                egg.AddForce(new Vector3(-side * 0.4f, 1f, 0f) * peckImpulse, ForceMode.Impulse);
            Haptics.Pulse(60, 180);
        }
    }

    void OnShaken()
    {
        if (state == State.Perched || state == State.FlyingIn)
        {
            Haptics.Pulse(80, 200);
            GameUI.GameAudio.Play("shoo");        // the player shooing
            if (PlayerAnimator.Instance != null) PlayerAnimator.Instance.Shoo();
            FlyOff();
        }
    }

    void FlyOff()
    {
        GameUI.GameAudio.Play("bird_land");      // the bird leaves: same "tweet-tweet" as when it arrives
        flyFrom = transform.position;
        timer = 0f;
        state = State.FlyingOff;
        if (hintText != null) hintText.gameObject.SetActive(false);
    }

    void Show(bool shown)
    {
        if (visual != null) visual.SetActive(shown);
        if (!shown && hintText != null) hintText.gameObject.SetActive(false);
    }

    Vector3 PerchPoint()
    {
        // perchOffset is for the original spoon; a SpoonShape (smaller bowl) moves the rim.
        Vector3 o = perchOffset;
        var shape = spoon.GetComponent<SpoonShape>();
        if (shape != null)
        {
            o.x += shape.RimX - 0.258f;
            o.y += shape.RimY - 0.081f;
        }
        return spoon.transform.TransformPoint(new Vector3(o.x * side, o.y, o.z));
    }

    static Vector3 Arc(Vector3 from, Vector3 to, float t)
    {
        t = Mathf.Clamp01(t);
        float eased = t * t * (3f - 2f * t);
        return Vector3.Lerp(from, to, eased) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.5f);
    }

    void Flap()
    {
        bool flying = state == State.FlyingIn || state == State.FlyingOff;
        float angle = flying ? Mathf.Sin(Time.time * 25f) * 45f : 0f;
        if (wingLeft != null) wingLeft.localRotation = Quaternion.Euler(angle, 0f, 0f);
        if (wingRight != null) wingRight.localRotation = Quaternion.Euler(-angle, 0f, 0f);
    }
}
