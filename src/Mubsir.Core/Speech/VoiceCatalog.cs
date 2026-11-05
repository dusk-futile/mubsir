namespace Mubsir.Core.Speech;

public sealed record FishVoiceChoice(int Number, string Id, string Name, string Sample);

/// <summary>
/// The Arabic voices a person can pick by number. English has one free voice on the PC. Each has a public sample, so listening needs no key and costs nothing.
/// Voices named after real people or characters are left out on purpose.
/// </summary>
public static class VoiceCatalog
{
    private const string Samples = "https://platform.r2.fish.audio/";

    /// <summary>The voice used until the person picks one.</summary>
    public static FishVoiceChoice Default => Arabic[0];

    public static FishVoiceChoice? Get(int number) => Arabic.FirstOrDefault(v => v.Number == number);

    public static IReadOnlyList<FishVoiceChoice> Arabic { get; } = [
        new(1, "c3e5d81d807f4cbc9a0c2872a4dea9ea", "راوي عربي", Samples + "tasks/2026-07-11/5cbfcb90d232407f88ff615351cb63b1.mp3"),
        new(2, "0b14f34a13a94ed88fe6113193749bb0", "فاطمة", Samples + "tasks/2026-08-24/93406e1b905a4c4994306e3421243390.mp3"),
        new(3, "05bf47f4aad948e1a7167a56e1d21c22", "حنان", Samples + "task/86d9c88488ae4406a73fd780db3f65b9.mp3"),
        new(4, "14f1000b77d547eeb5f03b474dd29e0f", "أسماء", Samples + "task/7181ceaaf4214b00be3c8302f6b49f56.mp3"),
        new(5, "10c5c2a37a284a81bb0cf3c53955d795", "شاب سعودي", Samples + "task/214291316a0a4d9084bbb15532049f5e.mp3"),
        new(6, "ccbfe8a2690e4ca99e91a059c38f30dc", "مصري", Samples + "task/0ac9f317345a4db3a8d4a7f9d38408e8.mp3"),
    ];
}
