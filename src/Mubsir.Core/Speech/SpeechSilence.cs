namespace Mubsir.Core.Speech;

/// <summary>
/// Evens out the silence around one spoken sentence, so sentences follow each other the way a person reads:
/// a short, steady breath between them instead of whatever silence the voice happened to add. The edges are
/// faded in and out over a few milliseconds, so cutting never leaves a click.
/// </summary>
public static class SpeechSilence
{
    /// <summary>Silence before the first sound.</summary>
    public const double LeadMs = 40;

    /// <summary>Silence after the last sound: with the next sentence's lead, a natural pause.</summary>
    public const double TailMs = 140;

    /// <summary>Length of the fade at each edge.</summary>
    public const double FadeMs = 6;

    /// <summary>Anything quieter than this (about -46 dBFS) counts as silence.</summary>
    public const float Threshold = 0.005f;

    public static float[] Trim(ReadOnlySpan<float> samples, int sampleRate) => Trim(samples, sampleRate, LeadMs, TailMs);

    /// <summary>Keeps <paramref name="leadMs"/> of silence before the first sound and <paramref name="tailMs"/> after the last.</summary>
    public static float[] Trim(ReadOnlySpan<float> samples, int sampleRate, double leadMs, double tailMs)
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

        // Exactly this much silence on each side, whatever the voice added: taken from the clip, or added.
        int lead = Samples(leadMs, sampleRate);
        int tail = Samples(tailMs, sampleRate);
        int speech = last + 1 - first;
        var result = new float[lead + speech + tail];
        int from = Math.Max(0, first - lead);
        int to = Math.Min(samples.Length, last + 1 + tail);
        samples[from..to].CopyTo(result.AsSpan(lead - (first - from)));

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
