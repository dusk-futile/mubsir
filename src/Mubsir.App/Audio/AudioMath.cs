using System.Buffers.Binary;

namespace Mubsir.App.Audio;

internal readonly record struct AudioLevels(int SpeechMs, double Peak, double NoiseFloorDbfs, double ThresholdDbfs);

internal static class AudioMath
{
    public const int FrameMs = 20;
    public const double AbsoluteThresholdDbfs = -45;
    public const double AboveNoiseDb = 12;

    public const double MaxThresholdDbfs = -20;

    private const double SilenceDbfs = -120;

    public static byte[] ToWav(ReadOnlySpan<byte> pcm, int sampleRate, short channels = 1, short bitsPerSample = 16)
    {
        int blockAlign = channels * bitsPerSample / 8;
        int dataLength = pcm.Length - (pcm.Length % blockAlign);
        var wav = new byte[44 + dataLength];
        Span<byte> h = wav;
        "RIFF"u8.CopyTo(h);
        BinaryPrimitives.WriteInt32LittleEndian(h[4..], 36 + dataLength);
        "WAVE"u8.CopyTo(h[8..]);
        "fmt "u8.CopyTo(h[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(h[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(h[20..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(h[22..], channels);
        BinaryPrimitives.WriteInt32LittleEndian(h[24..], sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(h[28..], sampleRate * blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(h[32..], (short)blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(h[34..], bitsPerSample);
        "data"u8.CopyTo(h[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(h[40..], dataLength);
        pcm[..dataLength].CopyTo(h[44..]);
        return wav;
    }

    public static int DurationMs(int pcmBytes, int sampleRate) =>
    sampleRate <= 0 ? 0 : (int)((long)(pcmBytes / 2) * 1000 / sampleRate);

    public static AudioLevels Analyze(ReadOnlySpan<byte> pcm, int sampleRate)
    {
        int samplesPerFrame = Math.Max(1, sampleRate * FrameMs / 1000);
        int sampleCount = pcm.Length / 2;
        int frames = sampleCount / samplesPerFrame;
        int peak = 0;
        var levels = new double[frames];
        for (int f = 0; f < frames; f++)
        {
            double sum = 0;
            int start = f * samplesPerFrame;
            for (int i = 0; i < samplesPerFrame; i++)
            {
                int s = BinaryPrimitives.ReadInt16LittleEndian(pcm.Slice((start + i) * 2, 2));
                int a = Math.Abs(s);
                if (a > peak)
                {
                    peak = a;
                }

                sum += (double)s * s;
            }

            double rms = Math.Sqrt(sum / samplesPerFrame) / 32768.0;
            levels[f] = rms > 0 ? 20 * Math.Log10(rms) : SilenceDbfs;
        }

        for (int i = frames * samplesPerFrame; i < sampleCount; i++)
        {
            peak = Math.Max(peak, Math.Abs((int)BinaryPrimitives.ReadInt16LittleEndian(pcm.Slice(i * 2, 2))));
        }

        if (frames == 0)
        {
            return new AudioLevels(0, Math.Min(1.0, peak / 32768.0), SilenceDbfs, AbsoluteThresholdDbfs);
        }

        double[] sorted = (double[])levels.Clone();
        Array.Sort(sorted);
        double floor = sorted[(int)Math.Floor((sorted.Length - 1) * 0.10)];
        double threshold = Math.Min(MaxThresholdDbfs, Math.Max(AbsoluteThresholdDbfs, floor + AboveNoiseDb));
        int speechFrames = 0;
        foreach (double level in levels)
        {
            if (level > threshold)
            {
                speechFrames++;
            }
        }

        return new AudioLevels(speechFrames * FrameMs, Math.Min(1.0, peak / 32768.0), floor, threshold);
    }
}
