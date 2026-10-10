using System.Buffers.Binary;
using System.Text;
using Basira.Core.Common;
using Basira.Core.Speech;

namespace Basira.Core.Tests.Speech;

public class EarconTonesTests
{
    public static readonly TheoryData<Earcon> AllEarcons = new(Enum.GetValues<Earcon>());

    private static short[] Samples(byte[] wav)
    {
        var samples = new short[(wav.Length - 44) / 2];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(44 + (i * 2)));
        }

        return samples;
    }

    private static double DurationMs(byte[] wav, int sampleRate) => (wav.Length - 44) / 2 * 1000.0 / sampleRate;

    /// <summary>Rough pitch from zero crossings of a stretch of samples.</summary>
    private static double Pitch(short[] samples, int start, int count, int sampleRate)
    {
        int crossings = 0;
        for (int i = start + 1; i < start + count; i++)
        {
            if ((samples[i - 1] < 0) != (samples[i] < 0))
            {
                crossings++;
            }
        }

        return crossings / 2.0 * sampleRate / count;
    }

    [Theory]
    [MemberData(nameof(AllEarcons))]
    public void IsAValidPcm16MonoRiffWav(Earcon earcon)
    {
        byte[] wav = EarconTones.Wav(earcon);
        Assert.True(wav.Length > 44);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal(wav.Length - 8, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(4)));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal("fmt ", Encoding.ASCII.GetString(wav, 12, 4));
        Assert.Equal(16, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(16)));
        Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(20))); // PCM
        Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(22))); // mono
        Assert.Equal(22050, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(24)));
        Assert.Equal(44100, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(28))); // byte rate
        Assert.Equal(2, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(32))); // block align
        Assert.Equal(16, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(34)));
        Assert.Equal("data", Encoding.ASCII.GetString(wav, 36, 4));
        Assert.Equal(wav.Length - 44, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(40)));
        Assert.Equal(0, (wav.Length - 44) % 2);
    }

    [Theory]
    [MemberData(nameof(AllEarcons))]
    public void IsShort(Earcon earcon) => Assert.InRange(DurationMs(EarconTones.Wav(earcon), 22050), 60, 350);

    [Theory]
    [MemberData(nameof(AllEarcons))]
    public void IsAudibleButModerate(Earcon earcon)
    {
        short[] samples = Samples(EarconTones.Wav(earcon));
        int peak = samples.Max(s => Math.Abs((int)s));
        Assert.InRange(peak, (int)(0.15 * short.MaxValue), (int)(0.5 * short.MaxValue) + 1);
    }

    /// <summary>Stretches of sound separated by at least 1 ms of exact silence (the gaps between notes).</summary>
    private static List<(int Start, int End)> SoundingStretches(short[] samples, int sampleRate)
    {
        int minGap = sampleRate / 1000;
        var stretches = new List<(int, int)>();
        int i = 0;
        while (i < samples.Length)
        {
            int start = i;
            int zeros = 0;
            while (i < samples.Length && zeros < minGap)
            {
                zeros = samples[i] == 0 ? zeros + 1 : 0;
                i++;
            }

            stretches.Add((start, i - zeros));
            while (i < samples.Length && samples[i] == 0)
            {
                i++;
            }
        }

        return stretches;
    }

    [Theory]
    [MemberData(nameof(AllEarcons))]
    public void EveryNoteFadesInAndOutWithoutClicks(Earcon earcon)
    {
        const int rate = 22050;
        short[] samples = Samples(EarconTones.Wav(earcon, rate));
        int peak = samples.Max(s => Math.Abs((int)s));
        Assert.Equal(0, samples[0]);
        Assert.Equal(0, samples[^1]);

        int edge = rate / 1000; // the first and last millisecond of every note stays quiet
        foreach (var (start, end) in SoundingStretches(samples, rate))
        {
            for (int i = start; i < Math.Min(end, start + edge); i++)
            {
                Assert.True(Math.Abs((int)samples[i]) < peak / 4, $"abrupt start at sample {i}");
            }

            for (int i = Math.Max(start, end - edge); i < end; i++)
            {
                Assert.True(Math.Abs((int)samples[i]) < peak / 4, $"abrupt end at sample {i}");
            }
        }
    }

    [Theory]
    [InlineData(Earcon.Error, 2)]
    [InlineData(Earcon.NeedsConfirmation, 2)]
    [InlineData(Earcon.ListeningStart, 2)]
    [InlineData(Earcon.BankMode, 3)]
    [InlineData(Earcon.Ready, 3)]
    [InlineData(Earcon.Thinking, 1)]
    [InlineData(Earcon.LayerOpen, 1)]
    [InlineData(Earcon.AiContent, 1)]
    public void HasTheDesignedNumberOfNotes(Earcon earcon, int notes) =>
        Assert.Equal(notes, SoundingStretches(Samples(EarconTones.Wav(earcon, 22050)), 22050).Count);

    [Fact]
    public void EveryEarconSoundsDifferent()
    {
        var earcons = Enum.GetValues<Earcon>();
        for (int a = 0; a < earcons.Length; a++)
        {
            for (int b = a + 1; b < earcons.Length; b++)
            {
                Assert.False(
                    EarconTones.Wav(earcons[a]).AsSpan().SequenceEqual(EarconTones.Wav(earcons[b])),
                    $"{earcons[a]} and {earcons[b]} are identical");
            }
        }
    }

    [Theory]
    [InlineData(Earcon.ListeningStart, true)]
    [InlineData(Earcon.ListeningEnd, false)]
    [InlineData(Earcon.Done, true)]
    [InlineData(Earcon.Ready, true)]
    [InlineData(Earcon.Paused, false)]
    public void MelodyGoesTheRightWay(Earcon earcon, bool rising)
    {
        const int rate = 22050;
        short[] samples = Samples(EarconTones.Wav(earcon, rate));
        int window = rate * 40 / 1000;
        double start = Pitch(samples, rate * 10 / 1000, window, rate);
        double end = Pitch(samples, samples.Length - window - (rate * 10 / 1000), window, rate);
        Assert.True(rising ? end > start * 1.15 : end < start / 1.15, $"start {start:F0} Hz, end {end:F0} Hz");
    }

    [Fact]
    public void LayerOpenIsHigherThanLayerClose()
    {
        const int rate = 22050;
        short[] open = Samples(EarconTones.Wav(Earcon.LayerOpen, rate));
        short[] close = Samples(EarconTones.Wav(Earcon.LayerClose, rate));
        Assert.True(Pitch(open, 0, open.Length, rate) > Pitch(close, 0, close.Length, rate));
    }

    [Fact]
    public void ResultsAreCached() => Assert.Same(EarconTones.Wav(Earcon.Ready), EarconTones.Wav(Earcon.Ready));

    [Theory]
    [InlineData(16000)]
    [InlineData(44100)]
    [InlineData(48000)]
    public void OtherSampleRatesKeepTheDuration(int sampleRate)
    {
        foreach (Earcon earcon in Enum.GetValues<Earcon>())
        {
            byte[] wav = EarconTones.Wav(earcon, sampleRate);
            Assert.Equal(sampleRate, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(24)));
            Assert.Equal(DurationMs(EarconTones.Wav(earcon), 22050), DurationMs(wav, sampleRate), 1.0);
        }
    }

    [Fact]
    public void InvalidArgumentsThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EarconTones.Wav((Earcon)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => EarconTones.Wav(Earcon.Ready, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => EarconTones.Wav(Earcon.Ready, 1_000_000));
    }
}
