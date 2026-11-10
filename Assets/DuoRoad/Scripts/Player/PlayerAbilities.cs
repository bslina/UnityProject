using UnityEngine;
namespace DuoRoad
{

// The big bomb special. Bomb potions add a charge (max 3); pressing the BOMB button
// (or E on the keyboard) uses one and blows up every enemy, enemy bomb and traffic car on screen.
// In the voice rounds there is no button: saying "bomb" does the same thing (if you have a potion).
public class PlayerAbilities : MonoBehaviour
{
    public IGameInput controls;
    public EnemySpawner enemies;
    public ObstacleSpawner traffic;
    public CarController car;
    public GameManager game;

    public int maxCharges = 3;
    public int trafficCarScore = 25;
    public bool controlsEnabled = true;

    public int Charges { get; private set; }

    bool wasPressed;

    public void AddCharge()
    {
        Charges = Mathf.Min(maxCharges, Charges + 1);
    }

    public void ResetAbilities()
    {
        Charges = 0;
        wasPressed = false;
        controlsEnabled = true;
    }

    void Update()
    {
        // Let the on-screen BOMB button show the remaining charges
        var display = controls as TouchInput;
        if (display != null) display.SpecialCharges = Charges;

        var voice = controls as IVoiceControls;
        if (voice != null && voice.VoiceMode)
        {
            int words = voice.ConsumeBombs();    // always empty the queue, even when controls are off
            wasPressed = false;
            if (!controlsEnabled) return;

            for (int i = 0; i < words; i++)
            {
                if (Charges > 0)
                {
                    Detonate();
                }
                else
                {
                    game.Say("NO BOMB POTION");
                    break;
                }
            }
            return;
        }

        bool pressed = controlsEnabled && controls != null && controls.Special;
        if (pressed && !wasPressed && Charges > 0) Detonate();
        wasPressed = pressed;
    }

    void Detonate()
    {
        Charges--;

        int cars = traffic.DestroyAll();
        enemies.DestroyAll();
        game.AddScore(trafficCarScore * cars);

        car.AddShake(0.9f);
        Effects.Explosion(new Vector3(0f, 0f, 30f), 26f, new Color(1f, 0.85f, 0.35f));
        game.Say("BIG BOMB!");
    }
}
}
