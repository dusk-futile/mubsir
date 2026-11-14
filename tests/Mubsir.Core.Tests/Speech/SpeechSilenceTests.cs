using Mubsir.Core.Speech;

namespace Mubsir.Core.Tests.Speech;

/// <summary>Sentences follow each other with the same short breath, and cutting never clicks.</summary>
public sealed class SpeechSilenceTests
{
    private const int Rate = 22050;

    private static float[] Sentence(double silenceBeforeMs, double soundMs, double silenceAfterMs)
    {
        int before = (int)(silenceBeforeMs * Rate / 1000);
        int sound = (int)(soundMs * Rate / 1000);
        int after = (int)(silenceAfterMs * Rate / 1000);
        var samples = new float[before + sound + after];
        for (int i = 0; i < sound; i++)
        {
            samples[before + i] = (0.5f * MathF.Sin(i * 0.2f)) + (i % 2 == 0 ? 0.1f : -0.1f);
        }

        return samples;
    }

    [Theory]
    [InlineData(400, 900)]
    [InlineData(10, 30)]
    [InlineData(1500, 0)]
    public void Every_sentence_gets_the_same_short_pause(double before, double after)
    {
        float[] trimmed = SpeechSilence.Trim(Sentence(before, 1000, after), Rate);

        double ms = trimmed.Length * 1000.0 / Rate;
        double expected = 1000 + SpeechSilence.LeadMs + SpeechSilence.TailMs;
        Assert.InRange(ms, expected - 2, expected + 2);
    }

    [Fact]
    public void The_edges_fade_so_there_is_no_click()
    {
        float[] trimmed = SpeechSilence.Trim(Sentence(0, 500, 0), Rate);

        Assert.Equal(0f, trimmed[0]);
        Assert.Equal(0f, trimmed[^1]);
        Assert.True(Math.Abs(trimmed[1]) < 0.2f);
    }

    [Fact]
    public void All_silence_gives_nothing()
    {
        Assert.Empty(SpeechSilence.Trim(new float[Rate], Rate));
    }

    [Fact]
    public void The_spoken_part_is_untouched()
    {
        float[] original = Sentence(500, 1000, 500);
        float[] trimmed = SpeechSilence.Trim(original, Rate);
        int lead = (int)Math.Round(SpeechSilence.LeadMs * Rate / 1000);
        int start = (int)(500.0 * Rate / 1000);

        for (int i = 200; i < 1000; i++)
        {
            Assert.Equal(original[start + i], trimmed[lead + i]);
        }
    }
}
