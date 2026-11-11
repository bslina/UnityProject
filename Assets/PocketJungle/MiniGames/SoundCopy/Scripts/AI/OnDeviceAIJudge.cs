using System;
using System.Collections;
using Unity.InferenceEngine;
using UnityEngine;

namespace SoundCopy.AI
{
    public sealed class OnDeviceAIJudge : MonoBehaviour
    {
        [SerializeField] private ModelAsset yamNetModel;
        [SerializeField] private AudioClip[] referenceClips;
        [SerializeField] private string[] referenceIds;
        private YamNetRunner runner;

        public void Initialize(ModelAsset model, AudioClip[] clips, string[] ids)
        {
            yamNetModel = model;
            referenceClips = clips;
            referenceIds = ids;
        }

        public IEnumerator Judge(string referenceId, AudioClip player1, AudioClip player2, Action<string> progress, Action<AIJudgeResult> complete)
        {
            AIJudgeResult result = new AIJudgeResult();
            AudioClip original = FindReference(referenceId);
            if (yamNetModel == null || original == null || player1 == null || player2 == null)
            {
                result.Error = yamNetModel == null ? "YAMNet model missing." : original == null ? "No reference audio for " + referenceId + "." : "A player recording is missing.";
                complete?.Invoke(result);
                yield break;
            }

            if (runner == null)
            {
                progress?.Invoke("Starting YAMNet…");
                try { runner = new YamNetRunner(yamNetModel); }
                catch (Exception exception) { result.Error = exception.Message; complete?.Invoke(result); yield break; }
                yield return null;
            }

            float[] originalNeural = null, firstNeural = null, secondNeural = null;
            float[] originalSamples = AudioFeatureExtractor.ReadMono16K(original);
            float[] firstSamples = AudioFeatureExtractor.ReadMono16K(player1);
            float[] secondSamples = AudioFeatureExtractor.ReadMono16K(player2);

            yield return ExtractSignature(originalSamples, "Analysing the original sound…", progress, value => originalNeural = value);
            yield return ExtractSignature(firstSamples, "Analysing " + nameForPlayer(1) + "…", progress, value => firstNeural = value);
            yield return ExtractSignature(secondSamples, "Analysing " + nameForPlayer(2) + "…", progress, value => secondNeural = value);

            if (originalNeural == null || firstNeural == null || secondNeural == null)
            {
                result.Error = "The YAMNet analysis gave no result.";
                complete?.Invoke(result);
                yield break;
            }

            // How each imitation is scored (0-100):
            //   sound types   YAMNet: the original's main sound classes found in the imitation + overall closeness
            //   timbre        how the pitch bands evolve over time (mel spectrogram contour)
            //   loudness      the loudness curve over time (centred correlation: unrelated sounds score ~0)
            // ...multiplied by three checks that a click / snap / clap / silence can't pass:
            //   activity      something clearly audible was recorded
            //   duration      the sound lasts about as long as the original
            //   voice         if the original is voice-like (has a pitch), the imitation must be voiced too
            float[] originalTemporal = AudioFeatureExtractor.CreateTemporalMelSignature(originalSamples);
            float[] originalRms = AudioFeatureExtractor.FrameRms(originalSamples);
            float originalActive = AudioFeatureExtractor.ActiveFraction(originalRms);
            float originalVoiced = AudioFeatureExtractor.VoicedRatio(originalSamples, originalRms);

            Score(firstSamples, firstNeural, out float score1, out float activity1, out string metrics1);
            Score(secondSamples, secondNeural, out float score2, out float activity2, out string metrics2);

            void Score(float[] samples, float[] neural, out float score, out float activity, out string metrics)
            {
                float types = 0.5f * Mathf.InverseLerp(0.70f, 0.98f, AudioFeatureExtractor.Cosine(originalNeural, neural))
                            + 0.5f * Mathf.InverseLerp(0.25f, 0.85f, AudioFeatureExtractor.ClassOverlap(originalNeural, neural));
                float timbre = Mathf.InverseLerp(0.45f, 0.90f, AudioFeatureExtractor.Cosine(originalTemporal, AudioFeatureExtractor.CreateTemporalMelSignature(samples)));
                float[] rms = AudioFeatureExtractor.FrameRms(samples);
                float loudness = Mathf.InverseLerp(0.05f, 0.85f, AudioFeatureExtractor.EnvelopeCorrelation(originalRms, rms));
                activity = AudioFeatureExtractor.RecordingActivity(samples);

                float active = AudioFeatureExtractor.ActiveFraction(rms);
                float durationRatio = originalActive <= 1e-3f ? 1f : Mathf.Min(active, originalActive) / Mathf.Max(active, originalActive, 1e-3f);
                float duration = Mathf.Lerp(0.25f, 1f, Mathf.Pow(Mathf.Clamp01(durationRatio * 1.4f), 0.8f));

                float voiced = AudioFeatureExtractor.VoicedRatio(samples, rms);
                float voice = originalVoiced < 0.2f ? 1f   // the original isn't voice-like: no voice needed
                            : Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(0.05f, Mathf.Min(0.35f, originalVoiced), voiced));

                score = Mathf.Clamp(100f * (0.30f * types + 0.35f * timbre + 0.35f * loudness) * activity * duration * voice, 0f, 100f);
                metrics = $"types:{types:0.00} timbre:{timbre:0.00} loudness:{loudness:0.00} activity:{activity:0.00} " +
                          $"duration:{duration:0.00} (active {active:0.00} vs {originalActive:0.00}) voice:{voice:0.00} (voiced {voiced:0.00} vs {originalVoiced:0.00})";
            }

            result.Player1TooQuiet = activity1 < 0.12f;
            result.Player2TooQuiet = activity2 < 0.12f;
            result.Player1Score = result.Player1TooQuiet ? 0f : score1;
            result.Player2Score = result.Player2TooQuiet ? 0f : score2;
            Debug.Log($"Sound Copy AI - P1 {result.Player1Score:0} [{metrics1}] | P2 {result.Player2Score:0} [{metrics2}]");
            float difference = result.Player1Score - result.Player2Score;
            if (result.Player1TooQuiet && result.Player2TooQuiet) result.Winner = 0;
            else if (result.Player1TooQuiet) result.Winner = 2;
            else if (result.Player2TooQuiet) result.Winner = 1;
            else result.Winner = Mathf.Abs(difference) < 3f ? 0 : difference > 0f ? 1 : 2;
            progress?.Invoke("Verdict ready!");
            complete?.Invoke(result);
        }

        private IEnumerator ExtractSignature(float[] samples, string message, Action<string> progress, Action<float[]> complete)
        {
            progress?.Invoke(message);
            int totalPatches = AudioFeatureExtractor.PatchCount(samples);
            int patchCount = Mathf.Min(totalPatches, 12);
            float[] average = new float[521];
            for (int patch = 0; patch < patchCount; patch++)
            {
                // For long clips, sample patches across the whole sound instead of only its beginning.
                int sourcePatch = patchCount <= 1 ? 0 : Mathf.RoundToInt(patch * (totalPatches - 1f) / (patchCount - 1f));
                int startFrame = sourcePatch * 48;
                float[] input = AudioFeatureExtractor.CreateLogMelPatch(samples, startFrame);
                float[] output;
                try { output = runner.Run(input); }
                catch (Exception exception) { Debug.LogError("Sound Copy YAMNet: " + exception); complete(null); yield break; }
                for (int i = 0; i < Mathf.Min(average.Length, output.Length); i++) average[i] += output[i] / patchCount;
                yield return null;
            }
            complete(average);
        }

        private AudioClip FindReference(string id)
        {
            if (referenceClips == null || referenceIds == null) return null;
            for (int i = 0; i < Mathf.Min(referenceClips.Length, referenceIds.Length); i++)
                if (string.Equals(referenceIds[i], id, StringComparison.OrdinalIgnoreCase)) return referenceClips[i];
            return null;
        }

        // The players' names (Players scene); "Player 1 / 2" when Sound Copy runs on its own.
        private string nameForPlayer(int player) => GameUI.MiniGameBridge.PlayerName(player);
        private void OnDestroy() { runner?.Dispose(); runner = null; }
    }
}
