using System;
using System.IO;
using UnityEngine;

namespace SoundCopy.Audio
{
    public static class WavUtility
    {
        public static void Save(AudioClip clip, string path)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            using BinaryWriter writer = new BinaryWriter(stream);
            int dataSize = samples.Length * 2;
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)clip.channels);
            writer.Write(clip.frequency);
            writer.Write(clip.frequency * clip.channels * 2);
            writer.Write((short)(clip.channels * 2));
            writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);
            foreach (float sample in samples)
            {
                short pcm = (short)Mathf.Clamp(Mathf.RoundToInt(sample * 32767f), short.MinValue, short.MaxValue);
                writer.Write(pcm);
            }
        }
    }
}
