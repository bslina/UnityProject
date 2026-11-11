using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Start panel guide: alternates calmly between breathing (Idle) and waving (Wave), with soft
// cross-fades. Tapping the character still plays React (Excited) from either state.
// Applied automatically once when Unity loads the scripts; menu to re-apply with new timings:
//   Tools > Egg Run > Update Guide Animations (idle + wave)
[InitializeOnLoad]
public static partial class EggRunCharacterSetup
{
    // Timings in seconds (at animation speed 1; the portrait's Animation Speed slider stretches them).
    const float GuideIdleSeconds = 3.5f;   // how long it breathes before waving again
    const float GuideWaveSeconds = 2.5f;   // minimum time it waves (always whole waves)
    const float GuideBlendSeconds = 0.6f;  // cross-fade between the two

    static EggRunCharacterSetup()
    {
        EditorApplication.delayCall += () => UpgradeGuideAnimations(false);
    }

    [MenuItem("Tools/Egg Run/Update Guide Animations (idle + wave)")]
    static void UpgradeGuideAnimationsMenu()
    {
        UpgradeGuideAnimations(true);
        EditorUtility.DisplayDialog("Egg Run", "The start panel character now alternates breathing and waving.\n" +
            "Change the timings at the top of EggRunGuideAnimations.cs, or select the transitions in EggRunWave.controller.", "OK");
    }

    static void UpgradeGuideAnimations(bool force)
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(WaveControllerPath);
        if (ctrl == null || ctrl.layers.Length == 0) return;
        var sm = ctrl.layers[0].stateMachine;
        bool hasIdle = sm.states.Any(s => s.state.name == "Idle");
        if (hasIdle && !force) return;

        var idleClip = Clip("Breathing Idle");
        var waveClip = Clip("Waving Gesture");
        if (idleClip == null || waveClip == null) return;

        AnimatorState wave = sm.states.Select(s => s.state).FirstOrDefault(s => s.motion == waveClip)
                             ?? sm.defaultState;
        if (wave == null) return;
        wave.name = "Wave";

        AnimatorState idle = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Idle")
                             ?? sm.AddState("Idle", new Vector3(300, -60));
        idle.motion = idleClip;
        AnimatorState react = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "React");

        // Start from clean transitions between these states.
        foreach (var t in wave.transitions.ToArray()) wave.RemoveTransition(t);
        foreach (var t in idle.transitions.ToArray()) idle.RemoveTransition(t);

        // Wave -> Idle after whole waves, Idle -> Wave after a calm breath.
        float waveLoops = Mathf.Max(1f, Mathf.Ceil(GuideWaveSeconds / Mathf.Max(0.1f, waveClip.length)));
        AddTimed(wave, idle, waveLoops - Mathf.Min(0.3f, GuideBlendSeconds / waveClip.length));
        AddTimed(idle, wave, Mathf.Max(0.3f, GuideIdleSeconds / Mathf.Max(0.1f, idleClip.length)));

        // Tap = React from either state, back to waving afterwards.
        if (react != null)
        {
            foreach (var from in new[] { wave, idle })
            {
                var t = from.AddTransition(react);
                t.AddCondition(AnimatorConditionMode.If, 0f, "React");
                t.hasExitTime = false;
                t.hasFixedDuration = true;
                t.duration = 0.15f;
            }
        }

        sm.defaultState = wave;   // says hi first
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        Debug.Log("[Egg Run] Guide animations: Wave (" + waveLoops + "x) <-> Idle (" + GuideIdleSeconds + " s), " +
                  GuideBlendSeconds + " s blends.");
    }

    static void AddTimed(AnimatorState from, AnimatorState to, float exitTime)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime = exitTime;              // in loops of the "from" animation (1.5 = one and a half)
        t.hasFixedDuration = true;
        t.duration = GuideBlendSeconds;
        t.interruptionSource = TransitionInterruptionSource.None;
    }
}
