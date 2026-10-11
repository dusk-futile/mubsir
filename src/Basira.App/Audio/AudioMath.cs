using System.Buffers.Binary;

namespace Basira.App.Audio;

/// <summary>Level statistics of a clip: milliseconds of likely speech and the peak (0–1).</summary>
internal readonly record struct AudioLevels(int SpeechMs, double Peak, double NoiseFloorDbfs, double ThresholdDbfs);

/// <summary>WAV writing and a simple energy voice-activity check for push-to-talk clips (pure, testable).</summary>
internal static class AudioMath
{
    public const int FrameMs = 20;
    public const double AbsoluteThresholdDbfs = -45;
    public const double AboveNoiseDb = 12;

    /// <summary>A threshold above this would make normal speech look like silence; only very noisy clips reach it.</summary>
    public const double MaxThresholdDbfs = -20;

    private const double SilenceDbfs = -120;

    /// <summary>A complete RIFF WAV (PCM16) around <paramref name="pcm"/>.</summary>
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
        BinaryPrimitives.WriteInt16LittleEndian(h[20..], 1); // PCM
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

    /// <summary>Duration of mono PCM16 audio in milliseconds.</summary>
    public static int DurationMs(int pcmBytes, int sampleRate) =>
        sampleRate <= 0 ? 0 : (int)((long)(pcmBytes / 2) * 1000 / sampleRate);

    /// <summary>
    /// Energy VAD over 20 ms frames of mono PCM16: a frame is speech when its RMS is above
    /// max(-45 dBFS, noise floor + 12 dB). The noise floor is the 10th percentile of frame levels, so the pauses
    /// between words set it even when the user talks through the whole clip.
    /// </summary>
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

        // Peak over the tail that does not fill a frame, too.
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
