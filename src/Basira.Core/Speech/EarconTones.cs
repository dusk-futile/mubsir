using System.Buffers.Binary;
using System.Collections.Concurrent;
using Basira.Core.Common;

namespace Basira.Core.Speech;

/// <summary>
/// Synthesizes Basira's earcons as small in-memory WAV files (PCM16 mono): short, soft, and distinct,
/// with 5 ms fades so they never click. Peaks stay at or below half of full scale.
/// </summary>
public static class EarconTones
{
    private const double FadeMs = 5;

    private static readonly ConcurrentDictionary<(Earcon Earcon, int SampleRate), byte[]> Cache = new();

    /// <param name="Frequency">Fundamental in Hz.</param>
    /// <param name="Ms">Tone length.</param>
    /// <param name="GapMs">Silence after the tone.</param>
    /// <param name="Partial">Optional second pitch sounding with the first (a chime).</param>
    /// <param name="Decay">Exponential decay rate per second (0 = sustained).</param>
    /// <param name="Buzz">Odd-harmonic content for a buzzier, still soft timbre (0–1).</param>
    private readonly record struct Note(double Frequency, double Ms, double GapMs = 0, double Partial = 0, double Decay = 0, double Buzz = 0);

    private readonly record struct Design(double Peak, Note[] Notes);

    /// <summary>
    /// A complete RIFF WAV for <paramref name="earcon"/>. Results are cached and shared: callers must not modify the array.
    /// </summary>
    public static byte[] Wav(Earcon earcon, int sampleRate = 22050)
    {
        if (!Enum.IsDefined(earcon))
        {
            throw new ArgumentOutOfRangeException(nameof(earcon));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, 8000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sampleRate, 96000);
        return Cache.GetOrAdd((earcon, sampleRate), static key => Render(DesignOf(key.Earcon), key.SampleRate));
    }

    private static Design DesignOf(Earcon earcon) => earcon switch
    {
        // Rising fourth: "I'm listening".
        Earcon.ListeningStart => new(0.45, [new(659.25, 70, 15), new(880.00, 90)]),

        // Falling fourth: "got it".
        Earcon.ListeningEnd => new(0.45, [new(880.00, 70, 15), new(659.25, 90)]),

        // A soft, very short tick.
        Earcon.Thinking => new(0.22, [new(1200, 60, Decay: 35)]),

        // Gentle major third up.
        Earcon.Done => new(0.42, [new(523.25, 90, 10), new(659.25, 140, Decay: 6)]),

        // Low double buzz, kept soft.
        Earcon.Error => new(0.40, [new(220, 100, 50, Buzz: 0.35), new(220, 100, Buzz: 0.35)]),

        // Two equal mid notes: "press Enter to confirm".
        Earcon.NeedsConfirmation => new(0.42, [new(660, 90, 70), new(660, 90)]),

        Earcon.LayerOpen => new(0.35, [new(1318.5, 70)]),
        Earcon.LayerClose => new(0.35, [new(987.77, 70)]),

        // A bell-like chime marks AI-generated content.
        Earcon.AiContent => new(0.38, [new(1046.5, 300, Partial: 1568.0, Decay: 9)]),

        // Low–high–low.
        Earcon.BankMode => new(0.42, [new(392.00, 70, 15), new(587.33, 70, 15), new(392.00, 90)]),

        // Three ascending notes (C–E–G).
        Earcon.Ready => new(0.42, [new(523.25, 70, 15), new(659.25, 70, 15), new(783.99, 110)]),

        // Two soft descending notes.
        Earcon.Paused => new(0.30, [new(783.99, 90, 20), new(587.33, 110)]),

        _ => throw new ArgumentOutOfRangeException(nameof(earcon)),
    };

    private static byte[] Render(Design design, int sampleRate)
    {
        int total = 0;
        foreach (var note in design.Notes)
        {
            total += Samples(note.Ms, sampleRate) + Samples(note.GapMs, sampleRate);
        }

        var buffer = new double[total];
        int pos = 0;
        int fade = Math.Max(1, Samples(FadeMs, sampleRate));
        foreach (var note in design.Notes)
        {
            int n = Samples(note.Ms, sampleRate);
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / sampleRate;
                double w = 2 * Math.PI * note.Frequency * t;
                double v = Math.Sin(w) + (0.12 * Math.Sin(2 * w));
                if (note.Buzz > 0)
                {
                    v += (note.Buzz * Math.Sin(3 * w)) + (note.Buzz * note.Buzz * Math.Sin(5 * w));
                }

                if (note.Partial > 0)
                {
                    v += 0.6 * Math.Sin(2 * Math.PI * note.Partial * t);
                }

                double envelope = Math.Exp(-note.Decay * t);
                int edge = Math.Min(i, n - 1 - i);
                if (edge < fade)
                {
                    envelope *= 0.5 - (0.5 * Math.Cos(Math.PI * edge / fade));
                }

                buffer[pos + i] = v * envelope;
            }

            pos += n + Samples(note.GapMs, sampleRate);
        }

        double max = 0;
        foreach (double v in buffer)
        {
            max = Math.Max(max, Math.Abs(v));
        }

        double scale = max > 0 ? design.Peak / max : 0;
        var wav = new byte[44 + (total * 2)];
        WriteHeader(wav, sampleRate, total * 2);
        for (int i = 0; i < total; i++)
        {
            short sample = (short)Math.Round(buffer[i] * scale * short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(44 + (i * 2)), sample);
        }

        return wav;
    }

    private static int Samples(double ms, int sampleRate) => (int)Math.Round(ms * sampleRate / 1000.0);

    private static void WriteHeader(Span<byte> wav, int sampleRate, int dataBytes)
    {
        const short channels = 1, bitsPerSample = 16;
        const short blockAlign = channels * bitsPerSample / 8;
        "RIFF"u8.CopyTo(wav);
        BinaryPrimitives.WriteInt32LittleEndian(wav[4..], 36 + dataBytes);
        "WAVE"u8.CopyTo(wav[8..]);
        "fmt "u8.CopyTo(wav[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(wav[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(wav[20..], 1); // PCM
        BinaryPrimitives.WriteInt16LittleEndian(wav[22..], channels);
        BinaryPrimitives.WriteInt32LittleEndian(wav[24..], sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(wav[28..], sampleRate * blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(wav[32..], blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(wav[34..], bitsPerSample);
        "data"u8.CopyTo(wav[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(wav[40..], dataBytes);
    }
}
