using Mubsir.Core.Setup;

namespace Mubsir.Core.Tests.Onboarding;

/// <summary>Every service is optional: the person picks one, and only that one needs a key.</summary>
public sealed class ProvidersTests
{
    private static readonly KeysHeld None = new(Claude: false, Fish: false, Gemini: false);
    private static readonly KeysHeld GeminiOnly = new(Claude: false, Fish: false, Gemini: true);
    private static readonly KeysHeld ClaudeAndFish = new(Claude: true, Fish: true, Gemini: false);
    private static readonly KeysHeld All = new(Claude: true, Fish: true, Gemini: true);

    [Fact]
    public void A_free_gemini_key_alone_is_enough_to_start()
    {
        Assert.True(Providers.CanStart(null, null, GeminiOnly));
        Assert.True(Providers.CanStart(BrainChoice.Gemini, EarsChoice.Gemini, GeminiOnly));
        Assert.False(Providers.CanStart(null, null, None));
    }

    [Fact]
    public void Claude_and_fish_work_without_any_google_key()
    {
        Assert.True(Providers.CanStart(BrainChoice.Claude, EarsChoice.Fish, ClaudeAndFish));
        Assert.True(Providers.CanStart(null, null, ClaudeAndFish));
        Assert.Equal(BrainChoice.Claude, Providers.Brain(null, ClaudeAndFish));
        Assert.Equal([EarsChoice.Fish], Providers.Ears(null, ClaudeAndFish));
    }

    [Fact]
    public void Choosing_a_service_needs_that_service_key()
    {
        Assert.False(Providers.CanStart(BrainChoice.Claude, EarsChoice.Gemini, GeminiOnly));
        Assert.False(Providers.CanStart(BrainChoice.Gemini, EarsChoice.Fish, GeminiOnly));
    }

    [Fact]
    public void The_choice_wins_when_every_key_is_there()
    {
        Assert.Equal(BrainChoice.Gemini, Providers.Brain(BrainChoice.Gemini, All));
        Assert.Equal(BrainChoice.Claude, Providers.Brain(BrainChoice.Claude, All));
        Assert.Equal([EarsChoice.Fish, EarsChoice.Gemini], Providers.Ears(EarsChoice.Fish, All));
        Assert.Equal([EarsChoice.Gemini, EarsChoice.Fish], Providers.Ears(EarsChoice.Gemini, All));
    }

    [Fact]
    public void A_missing_key_falls_back_to_the_other_service_so_it_keeps_working()
    {
        Assert.Equal(BrainChoice.Gemini, Providers.Brain(BrainChoice.Claude, GeminiOnly));
        Assert.Equal([EarsChoice.Gemini], Providers.Ears(EarsChoice.Fish, GeminiOnly));
        Assert.Null(Providers.Brain(BrainChoice.Gemini, None));
        Assert.Empty(Providers.Ears(EarsChoice.Gemini, None));
    }

    [Fact]
    public void Someone_who_never_chose_keeps_what_their_keys_gave_them()
    {
        Assert.Equal(BrainChoice.Claude, Providers.Chosen((BrainChoice?)null, All));
        Assert.Equal(BrainChoice.Gemini, Providers.Chosen((BrainChoice?)null, GeminiOnly));
        Assert.Equal(EarsChoice.Gemini, Providers.Chosen((EarsChoice?)null, All));
    }

    [Fact]
    public void Each_choice_names_its_key()
    {
        Assert.Equal(KeyKind.Anthropic, Providers.KeyFor(BrainChoice.Claude));
        Assert.Equal(KeyKind.Gemini, Providers.KeyFor(BrainChoice.Gemini));
        Assert.Equal(KeyKind.FishAudio, Providers.KeyFor(EarsChoice.Fish));
        Assert.Equal(KeyKind.Gemini, Providers.KeyFor(EarsChoice.Gemini));
    }

    [Fact]
    public void A_custom_brain_needs_no_brain_key_but_hearing_still_needs_its_own()
    {
        Assert.Equal(BrainChoice.Custom, Providers.Brain(BrainChoice.Custom, None));
        Assert.True(Providers.CanStart(BrainChoice.Custom, EarsChoice.Fish, ClaudeAndFish));
        Assert.False(Providers.CanStart(BrainChoice.Custom, null, None));
    }

    [Theory]
    [InlineData(3900L, 4, true)]
    [InlineData(8100L, 2, true)]
    [InlineData(8100L, 4, false)]
    [InlineData(null, 8, false)]
    public void A_pc_with_little_memory_or_two_cores_starts_light(long? memoryMb, int cores, bool light) =>
        Assert.Equal(light, PcCheck.IsLight(memoryMb, cores));
}
