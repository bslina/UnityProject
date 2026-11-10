using UnityEngine;

// Health of the player car. After a hit the car blinks and is briefly invulnerable.
public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public float invulnerableSeconds = 1.2f;

    public float Current { get; private set; }
    public float Fraction { get { return Current / maxHealth; } }
    public bool IsDead { get { return Current <= 0f; } }

    public event System.Action Died;
    public event System.Action<float> Damaged;

    float invulnTimer;
    Renderer[] renderers;

    void Awake()
    {
        Current = maxHealth;
        renderers = GetComponentsInChildren<Renderer>();
    }

    // Returns true if the damage was actually applied.
    public bool TakeDamage(float amount)
    {
        if (IsDead || invulnTimer > 0f) return false;

        Current = Mathf.Max(0f, Current - amount);
        if (Damaged != null) Damaged(amount);

        // Every damage source (traffic, bombs) goes through here, so the phone buzzes for all of them
        Haptics.Vibrate(IsDead ? 500 : 200);

        if (IsDead)
        {
            invulnTimer = 0f;
            SetVisible(true);
            if (Died != null) Died();
        }
        else
        {
            invulnTimer = invulnerableSeconds;
        }
        return true;
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        Current = Mathf.Min(maxHealth, Current + amount);
    }

    public void ResetHealth()
    {
        Current = maxHealth;
        invulnTimer = 0f;
        SetVisible(true);
    }

    void Update()
    {
        if (invulnTimer > 0f)
        {
            invulnTimer -= Time.deltaTime;
            bool visible = invulnTimer <= 0f || ((int)(invulnTimer * 10f) % 2 == 0);
            SetVisible(visible);
        }
    }

    void SetVisible(bool visible)
    {
        foreach (var r in renderers)
        {
            if (r != null) r.enabled = visible;
        }
    }
}
