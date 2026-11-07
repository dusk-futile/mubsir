namespace Mubsir.Core.Speech;

/// <summary>
/// Evens out the silence around one spoken sentence, so sentences follow each other the way a person reads:
/// a short, steady breath between them instead of whatever silence the voice happened to add. The edges are
/// faded in and out over a few milliseconds, so cutting never leaves a click.
/// </summary>
public static class SpeechSilence
{
    /// <summary>Silence kept before the first sound.</summary>
    public const double LeadMs = 40;

    /// <summary>Silence kept after the last sound: with the next sentence's lead, a natural pause.</summary>
    public const double TailMs = 140;

    /// <summary>Length of the fade at each edge.</summary>
    public const double FadeMs = 6;

    /// <summary>Anything quieter than this (about -46 dBFS) counts as silence.</summary>
    public const float Threshold = 0.005f;

    public static float[] Trim(ReadOnlySpan<float> samples, int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        int first = 0;
        while (first < samples.Length && Math.Abs(samples[first]) < Threshold)
        {
            first++;
        }

        if (first == samples.Length)
        {
            return [];
        }

        int last = samples.Length - 1;
        while (last > first && Math.Abs(samples[last]) < Threshold)
        {
            last--;
        }

        int lead = Samples(LeadMs, sampleRate);
        int tail = Samples(TailMs, sampleRate);
        int start = Math.Max(0, first - lead);
        int end = Math.Min(samples.Length, last + 1 + tail);
        float[] result = samples[start..end].ToArray();

        int fade = Math.Min(Samples(FadeMs, sampleRate), result.Length / 2);
        for (int i = 0; i < fade; i++)
        {
            float gain = (float)i / fade;
            result[i] *= gain;
            result[^(i + 1)] *= gain;
        }

        return result;
    }

    private static int Samples(double ms, int sampleRate) => (int)Math.Round(ms * sampleRate / 1000);
}
