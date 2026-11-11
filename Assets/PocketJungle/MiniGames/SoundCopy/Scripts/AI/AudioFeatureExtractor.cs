using System;
using UnityEngine;

namespace SoundCopy.AI
{
    public static class AudioFeatureExtractor
    {
        public const int SampleRate = 16000;
        public const int FrameLength = 400;
        public const int HopLength = 160;
        public const int FftSize = 512;
        public const int MelBands = 64;
        public const int PatchFrames = 96;
        private const float MelMinHz = 125f;
        private const float MelMaxHz = 7500f;
        private const float LogOffset = 0.001f;

        public static float[] ReadMono16K(AudioClip clip)
        {
            if (clip == null) return Array.Empty<float>();
            float[] interleaved = new float[clip.samples * clip.channels];
            if (!clip.GetData(interleaved, 0)) return Array.Empty<float>();
            float[] mono = new float[clip.samples];
            for (int i = 0; i < clip.samples; i++)
            {
                float sum = 0f;
                for (int channel = 0; channel < clip.channels; channel++) sum += interleaved[i * clip.channels + channel];
                mono[i] = sum / clip.channels;
            }
            if (clip.frequency == SampleRate) return mono;
            int outputLength = Mathf.Max(1, Mathf.RoundToInt(mono.Length * (SampleRate / (float)clip.frequency)));
            float[] output = new float[outputLength];
            float ratio = clip.frequency / (float)SampleRate;
            for (int i = 0; i < outputLength; i++)
            {
                float position = i * ratio;
                int left = Mathf.Min((int)position, mono.Length - 1);
                int right = Mathf.Min(left + 1, mono.Length - 1);
                output[i] = Mathf.Lerp(mono[left], mono[right], position - left);
            }
            return output;
        }

        public static float[] CreateLogMelPatch(float[] samples, int startFrame)
        {
            float[] patch = new float[PatchFrames * MelBands];
            float[] real = new float[FftSize];
            float[] imaginary = new float[FftSize];
            float[] power = new float[FftSize / 2 + 1];
            for (int frame = 0; frame < PatchFrames; frame++)
            {
                Array.Clear(real, 0, real.Length);
                Array.Clear(imaginary, 0, imaginary.Length);
                int sampleStart = (startFrame + frame) * HopLength;
                for (int i = 0; i < FrameLength; i++)
                {
                    int sampleIndex = sampleStart + i;
                    float sample = sampleIndex < samples.Length ? samples[sampleIndex] : 0f;
                    float window = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * i / FrameLength);
                    real[i] = sample * window;
                }
                FFT(real, imaginary);
                for (int bin = 0; bin < power.Length; bin++) power[bin] = real[bin] * real[bin] + imaginary[bin] * imaginary[bin];
                for (int mel = 0; mel < MelBands; mel++)
                {
                    float left = MelToHz(HzToMel(MelMinHz) + (HzToMel(MelMaxHz) - HzToMel(MelMinHz)) * mel / (MelBands + 1f));
                    float center = MelToHz(HzToMel(MelMinHz) + (HzToMel(MelMaxHz) - HzToMel(MelMinHz)) * (mel + 1f) / (MelBands + 1f));
                    float right = MelToHz(HzToMel(MelMinHz) + (HzToMel(MelMaxHz) - HzToMel(MelMinHz)) * (mel + 2f) / (MelBands + 1f));
                    float energy = 0f;
                    for (int bin = 0; bin < power.Length; bin++)
                    {
                        float hz = bin * SampleRate / (float)FftSize;
                        float weight = hz < center ? (hz - left) / Mathf.Max(center - left, 0.0001f) : (right - hz) / Mathf.Max(right - center, 0.0001f);
                        if (weight > 0f) energy += power[bin] * weight;
                    }
                    patch[frame * MelBands + mel] = Mathf.Log(energy + LogOffset);
                }
            }
            return patch;
        }

        public static int PatchCount(float[] samples)
        {
            int frames = Mathf.Max(1, 1 + Mathf.Max(0, samples.Length - FrameLength) / HopLength);
            return Mathf.Max(1, 1 + Mathf.Max(0, frames - PatchFrames) / 48);
        }

        public static float[] AcousticSignature(float[] samples)
        {
            const int bins = 24;
            float[] result = new float[bins + 3];
            if (samples.Length == 0) return result;
            double rms = 0; int crossings = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                rms += samples[i] * samples[i];
                if (i > 0 && (samples[i] >= 0) != (samples[i - 1] >= 0)) crossings++;
            }
            result[bins] = Mathf.Sqrt((float)(rms / samples.Length));
            result[bins + 1] = crossings / (float)samples.Length;
            int block = Mathf.Max(1, samples.Length / bins);
            float previous = 0f;
            for (int b = 0; b < bins; b++)
            {
                float energy = 0f;
                int start = b * block;
                int end = Mathf.Min(samples.Length, start + block);
                for (int i = start; i < end; i++) energy += Mathf.Abs(samples[i]);
                energy /= Mathf.Max(1, end - start);
                result[b] = energy;
                if (b > 0) result[bins + 2] += Mathf.Max(0f, energy - previous);
                previous = energy;
            }
            Normalize(result);
            return result;
        }

        public static float[] CreateTemporalMelSignature(float[] samples)
        {
            const int timeBins = 24;
            const int frequencyGroups = 16;
            float[] signature = new float[timeBins * frequencyGroups];
            float[] real = new float[FftSize];
            float[] imaginary = new float[FftSize];
            float[] power = new float[FftSize / 2 + 1];
            float melMinimum = HzToMel(MelMinHz);
            float melRange = HzToMel(MelMaxHz) - melMinimum;

            for (int time = 0; time < timeBins; time++)
            {
                Array.Clear(real, 0, real.Length);
                Array.Clear(imaginary, 0, imaginary.Length);
                int center = Mathf.RoundToInt((time + 0.5f) * Mathf.Max(0, samples.Length - FrameLength) / timeBins);
                for (int i = 0; i < FrameLength; i++)
                {
                    float window = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * i / FrameLength);
                    real[i] = samples[Mathf.Min(center + i, samples.Length - 1)] * window;
                }
                FFT(real, imaginary);
                for (int bin = 0; bin < power.Length; bin++) power[bin] = real[bin] * real[bin] + imaginary[bin] * imaginary[bin];

                for (int group = 0; group < frequencyGroups; group++)
                {
                    float energy = 0f;
                    int firstMel = group * MelBands / frequencyGroups;
                    int lastMel = (group + 1) * MelBands / frequencyGroups;
                    for (int mel = firstMel; mel < lastMel; mel++)
                    {
                        float left = MelToHz(melMinimum + melRange * mel / (MelBands + 1f));
                        float centerHz = MelToHz(melMinimum + melRange * (mel + 1f) / (MelBands + 1f));
                        float right = MelToHz(melMinimum + melRange * (mel + 2f) / (MelBands + 1f));
                        for (int bin = 0; bin < power.Length; bin++)
                        {
                            float hz = bin * SampleRate / (float)FftSize;
                            float weight = hz < centerHz ? (hz - left) / Mathf.Max(centerHz - left, 0.0001f) : (right - hz) / Mathf.Max(right - centerHz, 0.0001f);
                            if (weight > 0f) energy += power[bin] * weight;
                        }
                    }
                    signature[time * frequencyGroups + group] = Mathf.Log(energy + LogOffset);
                }
            }

            float mean = 0f;
            for (int i = 0; i < signature.Length; i++) mean += signature[i];
            mean /= signature.Length;
            for (int i = 0; i < signature.Length; i++) signature[i] -= mean;
            Normalize(signature);
            return signature;
        }

        public static float RecordingActivity(float[] samples)
        {
            if (samples == null || samples.Length == 0) return 0f;
            double sum = 0;
            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float absolute = Mathf.Abs(samples[i]);
                sum += samples[i] * samples[i];
                if (absolute > peak) peak = absolute;
            }
            float rms = Mathf.Sqrt((float)(sum / samples.Length));
            // Both sustained energy and at least one clear peak are required.
            // This prevents room noise or an untouched microphone from scoring highly.
            float rmsFactor = Mathf.InverseLerp(0.006f, 0.030f, rms);
            float peakFactor = Mathf.InverseLerp(0.025f, 0.15f, peak);
            return Mathf.Clamp01(rmsFactor * peakFactor);
        }

        // ---------------------------------------------------------------- imitation checks
        // (added so a click, a snap or a clap can't pass for an imitation of a voice-like sound)

        const int AnalysisFrame = 512;   // 32 ms at 16 kHz
        const int AnalysisHop = 256;

        // Loudness (RMS) of each 32 ms frame.
        public static float[] FrameRms(float[] samples)
        {
            int frames = samples.Length < AnalysisFrame ? 1 : 1 + (samples.Length - AnalysisFrame) / AnalysisHop;
            float[] rms = new float[frames];
            for (int f = 0; f < frames; f++)
            {
                double sum = 0; int start = f * AnalysisHop;
                int end = Mathf.Min(samples.Length, start + AnalysisFrame);
                for (int i = start; i < end; i++) sum += samples[i] * samples[i];
                rms[f] = Mathf.Sqrt((float)(sum / Mathf.Max(1, end - start)));
            }
            return rms;
        }

        // Share of the clip where something is actually heard (frames louder than 15% of the loudest,
        // and above the room noise). A snap or a clap is a few frames; a sustained sound is many.
        public static float ActiveFraction(float[] rms)
        {
            if (rms.Length == 0) return 0f;
            float peak = 0f;
            foreach (float r in rms) peak = Mathf.Max(peak, r);
            if (peak < 1e-4f) return 0f;
            float threshold = Mathf.Max(0.008f, peak * 0.15f);
            int active = 0;
            foreach (float r in rms) if (r >= threshold) active++;
            return active / (float)rms.Length;
        }

        // Share of the loud frames that are VOICED (have a pitch, like a voice, a hum or an animal call):
        // normalised autocorrelation peak between 70 Hz and 1000 Hz. Snaps, claps and taps have no pitch.
        public static float VoicedRatio(float[] samples, float[] rms)
        {
            float peak = 0f;
            foreach (float r in rms) peak = Mathf.Max(peak, r);
            if (peak < 1e-4f) return 0f;
            float threshold = Mathf.Max(0.008f, peak * 0.15f);
            int minLag = SampleRate / 1000, maxLag = SampleRate / 70;
            int loud = 0, voiced = 0;
            for (int f = 0; f < rms.Length; f++)
            {
                if (rms[f] < threshold) continue;
                loud++;
                int start = f * AnalysisHop;
                if (start + AnalysisFrame + maxLag > samples.Length) continue;
                double energy0 = 0;
                for (int i = 0; i < AnalysisFrame; i++) energy0 += samples[start + i] * samples[start + i];
                float best = 0f;
                for (int lag = minLag; lag <= maxLag; lag += 2)
                {
                    double dot = 0, energy1 = 0;
                    for (int i = 0; i < AnalysisFrame; i += 2)
                    {
                        float a = samples[start + i], b = samples[start + i + lag];
                        dot += a * b; energy1 += b * b;
                    }
                    double norm = System.Math.Sqrt(energy0 * 0.5 * energy1);
                    if (norm > 1e-9) best = Mathf.Max(best, (float)(dot / norm));
                }
                if (best > 0.5f) voiced++;
            }
            return loud == 0 ? 0f : voiced / (float)loud;
        }

        // How alike two loudness curves are (-1..1), allowing the imitation to start a little early or late.
        // Curves are resampled to 48 points, put on a log scale and centred, so unrelated sounds score ~0
        // (a plain cosine of positive values always looks "similar", which was the old flaw).
        public static float EnvelopeCorrelation(float[] rmsA, float[] rmsB)
        {
            const int points = 48;
            float[] a = Envelope(rmsA, points), b = Envelope(rmsB, points);
            float best = -1f;
            for (int lag = -4; lag <= 4; lag++)
            {
                double dot = 0, aa = 0, bb = 0;
                for (int i = 0; i < points; i++)
                {
                    int j = i + lag;
                    if (j < 0 || j >= points) continue;
                    dot += a[i] * b[j]; aa += a[i] * a[i]; bb += b[j] * b[j];
                }
                if (aa > 1e-9 && bb > 1e-9) best = Mathf.Max(best, (float)(dot / System.Math.Sqrt(aa * bb)));
            }
            return best;
        }

        static float[] Envelope(float[] rms, int points)
        {
            float[] e = new float[points];
            if (rms.Length == 0) return e;
            for (int i = 0; i < points; i++)
            {
                int from = i * rms.Length / points, to = Mathf.Max(from + 1, (i + 1) * rms.Length / points);
                float sum = 0f;
                for (int k = from; k < Mathf.Min(to, rms.Length); k++) sum += rms[k];
                e[i] = Mathf.Log(1e-3f + sum / (to - from));
            }
            float mean = 0f;
            foreach (float v in e) mean += v;
            mean /= points;
            for (int i = 0; i < points; i++) e[i] -= mean;
            return e;
        }

        // YAMNet: how much of the original's main sound types (its 10 strongest classes) the imitation has.
        public static float ClassOverlap(float[] reference, float[] imitation)
        {
            int n = Mathf.Min(reference.Length, imitation.Length);
            if (n == 0) return 0f;
            var top = new System.Collections.Generic.List<int>();
            for (int i = 0; i < n; i++) top.Add(i);
            top.Sort((x, y) => reference[y].CompareTo(reference[x]));
            float shared = 0f, total = 0f;
            for (int k = 0; k < Mathf.Min(10, top.Count); k++)
            {
                int c = top[k];
                total += reference[c];
                shared += Mathf.Min(reference[c], imitation[c]);
            }
            return total > 1e-6f ? Mathf.Clamp01(shared / total) : 0f;
        }

        public static float Cosine(float[] a, float[] b)
        {
            int count = Mathf.Min(a.Length, b.Length);
            double dot = 0, aa = 0, bb = 0;
            for (int i = 0; i < count; i++) { dot += a[i] * b[i]; aa += a[i] * a[i]; bb += b[i] * b[i]; }
            if (aa <= 1e-12 || bb <= 1e-12) return 0f;
            return Mathf.Clamp01((float)(dot / Math.Sqrt(aa * bb)));
        }

        private static void Normalize(float[] values)
        {
            double sum = 0;
            for (int i = 0; i < values.Length; i++) sum += values[i] * values[i];
            float length = (float)Math.Sqrt(sum);
            if (length < 1e-8f) return;
            for (int i = 0; i < values.Length; i++) values[i] /= length;
        }

        private static float HzToMel(float hz) => 2595f * Mathf.Log10(1f + hz / 700f);
        private static float MelToHz(float mel) => 700f * (Mathf.Pow(10f, mel / 2595f) - 1f);

        private static void FFT(float[] real, float[] imaginary)
        {
            int n = real.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) { (real[i], real[j]) = (real[j], real[i]); (imaginary[i], imaginary[j]) = (imaginary[j], imaginary[i]); }
            }
            for (int length = 2; length <= n; length <<= 1)
            {
                float angle = -2f * Mathf.PI / length;
                float wLengthReal = Mathf.Cos(angle), wLengthImaginary = Mathf.Sin(angle);
                for (int i = 0; i < n; i += length)
                {
                    float wr = 1f, wi = 0f;
                    for (int j = 0; j < length / 2; j++)
                    {
                        int even = i + j, odd = even + length / 2;
                        float oddReal = real[odd] * wr - imaginary[odd] * wi;
                        float oddImaginary = real[odd] * wi + imaginary[odd] * wr;
                        real[odd] = real[even] - oddReal; imaginary[odd] = imaginary[even] - oddImaginary;
                        real[even] += oddReal; imaginary[even] += oddImaginary;
                        float nextWr = wr * wLengthReal - wi * wLengthImaginary;
                        wi = wr * wLengthImaginary + wi * wLengthReal; wr = nextWr;
                    }
                }
            }
        }
    }
}
