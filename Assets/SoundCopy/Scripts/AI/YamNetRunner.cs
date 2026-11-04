using System;
using Unity.InferenceEngine;
using UnityEngine;

namespace SoundCopy.AI
{
    public sealed class YamNetRunner : IDisposable
    {
        private readonly Worker worker;

        public YamNetRunner(ModelAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            Model model = ModelLoader.Load(asset);
            worker = new Worker(model, BackendType.CPU);
        }

        public float[] Run(float[] logMelPatch)
        {
            using Tensor<float> input = new Tensor<float>(new TensorShape(1, 1, AudioFeatureExtractor.PatchFrames, AudioFeatureExtractor.MelBands), logMelPatch);
            worker.Schedule(input);
            Tensor<float> output = worker.PeekOutput("class_scores") as Tensor<float>;
            if (output == null) throw new InvalidOperationException("YAMNet output 'class_scores' was not found.");
            using Tensor<float> cpu = output.ReadbackAndClone();
            return cpu.DownloadToArray();
        }

        public void Dispose() => worker?.Dispose();
    }
}
