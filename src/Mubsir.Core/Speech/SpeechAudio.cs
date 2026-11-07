using System.Buffers.Binary;

namespace Mubsir.Core.Speech;

/// <summary>Mono speech samples between -1 and 1, at one sample rate.</summary>
public sealed record SpeechClip(float[] Samples, int SampleRate)
{
    public double Seconds => (double)Samples.Length / SampleRate;
}

/// <summary>
/// Turns the pieces of one sentence into one clip that sounds like one person reading: the same loudness
/// whichever voice spoke a piece, a word-sized gap where the language changes, and no click anywhere.
/// </summary>
public static class SpeechAudio
{
    /// <summary>The gap where one language's piece ends and the next begins, like the space between two words.</summary>
    public const double SwitchGapMs = 50;

    /// <summary>Kept at each edge of a piece, so a soft first or last sound isn't cut.</summary>
    public const double PieceEdgeMs = 15;

    /// <summary>Target loudness (RMS of the speech, about -20 dBFS).</summary>
    public const float TargetRms = 0.1f;

    /// <summary>Quiet voices are lifted at most this much, so noise is never blown up.</summary>
    public const float MaxGain = 4f;

    /// <summary>Peaks are kept under this after lifting.</summary>
    public const float PeakLimit = 0.98f;

    /// <summary>Reads a 16-bit PCM WAV file, mixing stereo down to mono. Null if it isn't one.</summary>
    public static SpeechClip? ReadWav(ReadOnlySpan<byte> wav)
    {
        if (wav.Length < 12 || !wav[..4].SequenceEqual("RIFF"u8) || !wav[8..12].SequenceEqual("WAVE"u8))
        {
            return null;
        }

        int channels = 0, rate = 0, bits = 0, format = 0;
        int pos = 12;
        while (pos + 8 <= wav.Length)
        {
            ReadOnlySpan<byte> id = wav.Slice(pos, 4);
            int size = BinaryPrimitives.ReadInt32LittleEndian(wav.Slice(pos + 4, 4));
            int body = pos + 8;
            if (size < 0)
            {
                return null;
            }

            if (id.SequenceEqual("fmt "u8) && size >= 16 && body + 16 <= wav.Length)
            {
                format = BinaryPrimitives.ReadInt16LittleEndian(wav.Slice(body, 2));
                channels = BinaryPrimitives.ReadInt16LittleEndian(wav.Slice(body + 2, 2));
                rate = BinaryPrimitives.ReadInt32LittleEndian(wav.Slice(body + 4, 4));
                bits = BinaryPrimitives.ReadInt16LittleEndian(wav.Slice(body + 14, 2));
            }
            else if (id.SequenceEqual("data"u8))
            {
                // PCM, or WAVE_FORMAT_EXTENSIBLE carrying PCM, 16-bit.
                if (format is not (1 or -2) || bits != 16 || channels is < 1 or > 2 || rate <= 0)
                {
                    return null;
                }

                int length = Math.Min(size, wav.Length - body);
                ReadOnlySpan<byte> data = wav.Slice(body, length);
                int frames = data.Length / (2 * channels);
                var samples = new float[frames];
                for (int f = 0; f < frames; f++)
                {
                    float sum = 0;
                    for (int c = 0; c < channels; c++)
                    {
                        sum += BinaryPrimitives.ReadInt16LittleEndian(data.Slice(((f * channels) + c) * 2, 2)) / 32768f;
                    }

                    samples[f] = sum / channels;
                }

                return new SpeechClip(samples, rate);
            }

            pos = body + size + (size & 1);
        }

        return null;
    }

    /// <summary>Brings speech to the target loudness, lifting at most <see cref="MaxGain"/> times and never clipping.</summary>
    public static float[] EvenLoudness(float[] samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        double sum = 0;
        int counted = 0;
        foreach (float s in samples)
        {
            // Only the speech counts, not the pauses inside it.
            if (Math.Abs(s) >= SpeechSilence.Threshold)
            {
                sum += s * s;
                counted++;
            }
        }

        if (counted == 0)
        {
            return samples;
        }

        float rms = (float)Math.Sqrt(sum / counted);
        float gain = Math.Min(TargetRms / rms, MaxGain);
        float peak = 0;
        foreach (float s in samples)
        {
            peak = Math.Max(peak, Math.Abs(s));
        }

        if (peak * gain > PeakLimit)
        {
            gain = PeakLimit / peak;
        }

        var result = new float[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            result[i] = samples[i] * gain;
        }

        return result;
    }

    /// <summary>
    /// Joins the pieces of one sentence, each spoken by the voice of its language, into one clip: each piece is
    /// trimmed of its own padding and brought to the same loudness, and a word-sized gap goes between them.
    /// </summary>
    public static SpeechClip Join(IReadOnlyList<SpeechClip> pieces)
    {
        ArgumentNullException.ThrowIfNull(pieces);
        if (pieces.Count == 0)
        {
            return new SpeechClip([], 16000);
        }

        int rate = pieces.Max(p => p.SampleRate);
        int gap = (int)Math.Round(SwitchGapMs * rate / 1000);
        var joined = new List<float>();
        foreach (SpeechClip piece in pieces)
        {
            float[] samples = SpeechSilence.Trim(Resample(piece, rate), rate, PieceEdgeMs, PieceEdgeMs);
            if (samples.Length == 0)
            {
                continue;
            }

            if (joined.Count > 0)
            {
                joined.AddRange(new float[gap]);
            }

            joined.AddRange(EvenLoudness(samples));
        }

        return new SpeechClip(SpeechSilence.Trim(joined.ToArray(), rate), rate);
    }

    /// <summary>Changes the sample rate by straight-line interpolation, which is plenty for speech.</summary>
    public static float[] Resample(SpeechClip clip, int rate)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rate);
        if (clip.SampleRate == rate || clip.Samples.Length == 0)
        {
            return clip.Samples;
        }

        int length = (int)Math.Round((long)clip.Samples.Length * rate / (double)clip.SampleRate);
        var result = new float[length];
        double step = (double)clip.SampleRate / rate;
        int last = clip.Samples.Length - 1;
        for (int i = 0; i < length; i++)
        {
            double at = i * step;
            int left = Math.Min((int)at, last);
            int right = Math.Min(left + 1, last);
            float t = (float)(at - left);
            result[i] = (clip.Samples[left] * (1 - t)) + (clip.Samples[right] * t);
        }

        return result;
    }
}
