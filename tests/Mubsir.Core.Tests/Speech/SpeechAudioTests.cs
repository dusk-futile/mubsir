using Mubsir.Core.Speech;

namespace Mubsir.Core.Tests.Speech;

/// <summary>A mixed Arabic and English sentence becomes one clip that sounds like one reader.</summary>
public sealed class SpeechAudioTests
{
    private static float[] Tone(int rate, double ms, float amplitude, double silenceBeforeMs = 0, double silenceAfterMs = 0)
    {
        int before = (int)(silenceBeforeMs * rate / 1000);
        int sound = (int)(ms * rate / 1000);
        int after = (int)(silenceAfterMs * rate / 1000);
        var samples = new float[before + sound + after];
        for (int i = 0; i < sound; i++)
        {
            samples[before + i] = amplitude * MathF.Sin(2 * MathF.PI * 220 * i / rate);
        }

        return samples;
    }

    private static float Rms(ReadOnlySpan<float> samples)
    {
        double sum = 0;
        int n = 0;
        foreach (float s in samples)
        {
            if (Math.Abs(s) >= SpeechSilence.Threshold)
            {
                sum += s * s;
                n++;
            }
        }

        return n == 0 ? 0 : (float)Math.Sqrt(sum / n);
    }

    [Fact]
    public void A_wav_file_reads_back_as_the_same_samples()
    {
        float[] samples = Tone(16000, 200, 0.5f);
        SpeechClip? clip = SpeechAudio.ReadWav(WavFile.FromSamples(samples, 16000));

        Assert.NotNull(clip);
        Assert.Equal(16000, clip.SampleRate);
        Assert.Equal(samples.Length, clip.Samples.Length);
        for (int i = 0; i < samples.Length; i++)
        {
            Assert.InRange(clip.Samples[i] - samples[i], -0.0001f, 0.0001f);
        }
    }

    [Fact]
    public void Anything_that_is_not_a_wav_file_is_refused()
    {
        Assert.Null(SpeechAudio.ReadWav("ID3 this is an mp3"u8));
        Assert.Null(SpeechAudio.ReadWav([]));
    }

    [Fact]
    public void A_quiet_voice_and_a_loud_voice_end_up_equally_loud()
    {
        float quiet = Rms(SpeechAudio.EvenLoudness(Tone(16000, 300, 0.05f)));
        float loud = Rms(SpeechAudio.EvenLoudness(Tone(16000, 300, 0.6f)));

        Assert.InRange(quiet, SpeechAudio.TargetRms * 0.95f, SpeechAudio.TargetRms * 1.05f);
        Assert.InRange(loud, SpeechAudio.TargetRms * 0.95f, SpeechAudio.TargetRms * 1.05f);
    }

    [Fact]
    public void Evening_out_never_clips_or_blows_up_near_silence()
    {
        float[] peaky = Tone(16000, 300, 0.02f);
        peaky[100] = 0.9f;
        Assert.All(SpeechAudio.EvenLoudness(peaky), s => Assert.InRange(s, -SpeechAudio.PeakLimit, SpeechAudio.PeakLimit));

        float[] whisper = Tone(16000, 300, 0.006f);
        Assert.True(Rms(SpeechAudio.EvenLoudness(whisper)) <= 0.006f * SpeechAudio.MaxGain * 1.01f);
    }

    [Fact]
    public void Pieces_join_with_a_word_sized_gap_whatever_padding_each_voice_added()
    {
        // An Arabic piece from one voice with lots of padding, then an English piece from another voice.
        var arabic = new SpeechClip(Tone(16000, 500, 0.3f, silenceBeforeMs: 300, silenceAfterMs: 400), 16000);
        var english = new SpeechClip(Tone(16000, 400, 0.05f, silenceBeforeMs: 250, silenceAfterMs: 350), 16000);

        SpeechClip joined = SpeechAudio.Join([arabic, english]);

        double expected = SpeechSilence.LeadMs + 500 + SpeechAudio.PieceEdgeMs + SpeechAudio.SwitchGapMs
            + SpeechAudio.PieceEdgeMs + 400 + SpeechSilence.TailMs;
        Assert.InRange(joined.Seconds * 1000, expected - 5, expected + 5);
    }

    [Fact]
    public void Joined_pieces_have_the_same_loudness()
    {
        var a = new SpeechClip(Tone(16000, 500, 0.4f), 16000);
        var b = new SpeechClip(Tone(16000, 500, 0.04f), 16000);
        SpeechClip joined = SpeechAudio.Join([a, b]);
        int half = joined.Samples.Length / 2;

        float first = Rms(joined.Samples.AsSpan(0, half - 2000));
        float second = Rms(joined.Samples.AsSpan(half + 2000));
        Assert.InRange(first / second, 0.9f, 1.1f);
    }

    [Fact]
    public void Voices_with_different_sample_rates_join_at_the_higher_rate()
    {
        var a = new SpeechClip(Tone(16000, 300, 0.3f), 16000);
        var b = new SpeechClip(Tone(22050, 300, 0.3f), 22050);

        SpeechClip joined = SpeechAudio.Join([a, b]);

        Assert.Equal(22050, joined.SampleRate);
        double expected = SpeechSilence.LeadMs + 300 + (2 * SpeechAudio.PieceEdgeMs) + SpeechAudio.SwitchGapMs + 300 + SpeechSilence.TailMs;
        Assert.InRange(joined.Seconds * 1000, expected - 5, expected + 5);
    }

    [Fact]
    public void Silent_pieces_are_dropped_and_add_no_gap()
    {
        var speech = new SpeechClip(Tone(16000, 300, 0.3f), 16000);
        var silent = new SpeechClip(new float[1600], 16000);

        SpeechClip joined = SpeechAudio.Join([silent, speech, silent]);

        double expected = SpeechSilence.LeadMs + 300 + SpeechSilence.TailMs;
        Assert.InRange(joined.Seconds * 1000, expected - 3, expected + 3);
    }

    [Fact]
    public void Resampling_keeps_the_length_in_time()
    {
        var clip = new SpeechClip(Tone(16000, 1000, 0.3f), 16000);

        Assert.Equal(48000, SpeechAudio.Resample(clip, 48000).Length);
        Assert.Equal(8000, SpeechAudio.Resample(clip, 8000).Length);
    }
}
