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
                result.Error = yamNetModel == null ? "Modèle YAMNet absent." : original == null ? "Référence audio absente pour " + referenceId + "." : "Un enregistrement joueur est absent.";
                complete?.Invoke(result);
                yield break;
            }

            if (runner == null)
            {
                progress?.Invoke("Initialisation de YAMNet…");
                try { runner = new YamNetRunner(yamNetModel); }
                catch (Exception exception) { result.Error = exception.Message; complete?.Invoke(result); yield break; }
                yield return null;
            }

            float[] originalNeural = null, firstNeural = null, secondNeural = null;
            float[] originalSamples = AudioFeatureExtractor.ReadMono16K(original);
            float[] firstSamples = AudioFeatureExtractor.ReadMono16K(player1);
            float[] secondSamples = AudioFeatureExtractor.ReadMono16K(player2);

            yield return ExtractSignature(originalSamples, "Analyse du son original…", progress, value => originalNeural = value);
            yield return ExtractSignature(firstSamples, "Analyse de " + nameForPlayer(1) + "…", progress, value => firstNeural = value);
            yield return ExtractSignature(secondSamples, "Analyse de " + nameForPlayer(2) + "…", progress, value => secondNeural = value);

            if (originalNeural == null || firstNeural == null || secondNeural == null)
            {
                result.Error = "L’analyse YAMNet n’a pas produit de résultat.";
                complete?.Invoke(result);
                yield break;
            }

            float neural1 = Mathf.InverseLerp(0.70f, 0.98f, AudioFeatureExtractor.Cosine(originalNeural, firstNeural));
            float neural2 = Mathf.InverseLerp(0.70f, 0.98f, AudioFeatureExtractor.Cosine(originalNeural, secondNeural));
            float[] originalTemporal = AudioFeatureExtractor.CreateTemporalMelSignature(originalSamples);
            float temporal1 = Mathf.InverseLerp(0.45f, 0.90f, AudioFeatureExtractor.Cosine(originalTemporal, AudioFeatureExtractor.CreateTemporalMelSignature(firstSamples)));
            float temporal2 = Mathf.InverseLerp(0.45f, 0.90f, AudioFeatureExtractor.Cosine(originalTemporal, AudioFeatureExtractor.CreateTemporalMelSignature(secondSamples)));
            float acoustic1 = AudioFeatureExtractor.Cosine(AudioFeatureExtractor.AcousticSignature(originalSamples), AudioFeatureExtractor.AcousticSignature(firstSamples));
            float acoustic2 = AudioFeatureExtractor.Cosine(AudioFeatureExtractor.AcousticSignature(originalSamples), AudioFeatureExtractor.AcousticSignature(secondSamples));
            float activity1 = AudioFeatureExtractor.RecordingActivity(firstSamples);
            float activity2 = AudioFeatureExtractor.RecordingActivity(secondSamples);
            result.Player1TooQuiet = activity1 < 0.12f;
            result.Player2TooQuiet = activity2 < 0.12f;
            // YAMNet class scores are broad categories, so they remain supporting evidence.
            // Local acoustic detail carries the decision; the temporal contour checks progression.
            result.Player1Score = result.Player1TooQuiet ? 0f : Mathf.Clamp(100f * (0.15f * neural1 + 0.25f * temporal1 + 0.60f * acoustic1) * activity1, 0f, 100f);
            result.Player2Score = result.Player2TooQuiet ? 0f : Mathf.Clamp(100f * (0.15f * neural2 + 0.25f * temporal2 + 0.60f * acoustic2) * activity2, 0f, 100f);
            Debug.Log($"Sound Copy AI metrics — P1 neural:{neural1:0.000} temporal:{temporal1:0.000} acoustic:{acoustic1:0.000} activity:{activity1:0.000} | P2 neural:{neural2:0.000} temporal:{temporal2:0.000} acoustic:{acoustic2:0.000} activity:{activity2:0.000}");
            float difference = result.Player1Score - result.Player2Score;
            if (result.Player1TooQuiet && result.Player2TooQuiet) result.Winner = 0;
            else if (result.Player1TooQuiet) result.Winner = 2;
            else if (result.Player2TooQuiet) result.Winner = 1;
            else result.Winner = Mathf.Abs(difference) < 3f ? 0 : difference > 0f ? 1 : 2;
            progress?.Invoke("Verdict prêt !");
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

        private string nameForPlayer(int player) => player == 1 ? "Joueur 1" : "Joueur 2";
        private void OnDestroy() { runner?.Dispose(); runner = null; }
    }
}
