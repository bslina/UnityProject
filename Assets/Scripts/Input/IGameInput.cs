// One interface for all control schemes (Touch = Easy, Tilt = Medium, Voice = Hard).
// The game never cares which one is plugged in.
public interface IGameInput
{
    float Steer { get; }      // -1 (left) to 1 (right)
    bool Accelerate { get; }
    bool Brake { get; }
    float AimX { get; }       // -1 to 1
    float AimY { get; }       // -1 to 1
    bool Fire { get; }
    bool Special { get; }     // big bomb button (held; the game detects the press)
}

// Extra commands for the voice rounds (rounds 4 and 5). The input says "the player spoke N times";
// the gun and the bomb ability decide what that does.
public interface IVoiceControls
{
    bool VoiceMode { get; }       // true in the voice rounds (no gas, brake, fire or bomb buttons)
    int ConsumeShots();           // number of "shoot" words since the last call
    int ConsumeBombs();           // number of "bomb" words since the last call
}
