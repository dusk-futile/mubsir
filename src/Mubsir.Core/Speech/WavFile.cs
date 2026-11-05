namespace Mubsir.Core.Speech;

/// <summary>Turns the local voice's samples into something the Windows player can play.</summary>
public static class WavFile
{
    private const int HeaderBytes = 44;

    public static byte[] FromSamples(ReadOnlySpan<float> samples, int sampleRate)
    {
        byte[] file = new byte[HeaderBytes + (samples.Length * 2)];
        using var stream = new MemoryStream(file);
        using var w = new BinaryWriter(stream);
        w.Write("RIFF"u8);
        w.Write(file.Length - 8);
        w.Write("WAVEfmt "u8);
        w.Write(16);
        w.Write((short)1);
        w.Write((short)1);
        w.Write(sampleRate);
        w.Write(sampleRate * 2);
        w.Write((short)2);
        w.Write((short)16);
        w.Write("data"u8);
        w.Write(samples.Length * 2);
        foreach (float sample in samples)
        {
            w.Write((short)(Math.Clamp(sample, -1f, 1f) * short.MaxValue));
        }

        return file;
    }
}
