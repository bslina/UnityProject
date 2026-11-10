# RoadDuo rounds 4 and 5: voice setup

New file: `Scripts/Input/VoiceInput.cs` (add `GameBootstrap` as before, it creates everything).

## Android (needed once)
Use a custom main manifest (Player Settings > Publishing Settings > Custom Main Manifest) and make sure it has:

```xml
<uses-permission android:name="android.permission.RECORD_AUDIO" />

<queries>
  <intent>
    <action android:name="android.speech.RecognitionService" />
  </intent>
</queries>
```
(`queries` is required on Android 11+, otherwise the phone reports "no speech service".)
The game asks for the microphone permission on the Round 4 intro screen.

## Testing
- Windows editor: uses Windows speech recognition (turn it on in Windows settings).
- Any editor, no microphone: press S = "brake", Space = "shoot", E = "bomb".
- Tilt still steers and aims in the voice rounds.

## Tuning (TouchInput inspector / VoiceInput)
- `brakePerWord` 0.35 s of braking per "brake", `maxBrakeTime` 1.5 s cap.
- `GunController.voiceShotRate` max shots per second when you say "shoot" fast.
- `VoiceInput.languageTag` e.g. `fr-FR`; French words (frein, tire, bombe) are already accepted.

## Known limit
Android's built-in recogniser listens in short phrases and restarts after each one, so a word said in
that tiny gap can be missed. Windows is smoother. For a fully continuous phone version, swap the backend for an
offline engine (Vosk / Whisper): only the Android block in VoiceInput.cs changes.
