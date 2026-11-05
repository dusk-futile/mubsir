using Mubsir.Core.Common;

namespace Mubsir.Core.Speech;

public sealed record FishVoiceChoice(int Number, Lang Lang, string Id, string Name, string Sample);

/// <summary>
/// The voices a person can pick by number. Each has a public sample, so listening needs no key and costs nothing.
/// Voices named after real people or characters are left out on purpose.
/// </summary>
public static class VoiceCatalog
{
    private const string Samples = "https://platform.r2.fish.audio/";

    public static IReadOnlyList<FishVoiceChoice> For(Lang lang) => lang == Lang.Arabic ? Arabic : English;

    /// <summary>The voice used until the person picks one.</summary>
    public static FishVoiceChoice Default(Lang lang) => For(lang)[0];

    public static FishVoiceChoice? Get(Lang lang, int number) => For(lang).FirstOrDefault(v => v.Number == number);

    private static readonly FishVoiceChoice[] English = [
        new(1, Lang.English, "933563129e564b19a115bedd57b7406a", "Sarah", Samples + "task/4e204945670c460c86fdab57512cb308.mp3"),
        new(2, Lang.English, "b347db033a6549378b48d00acb0d06cd", "Selene", Samples + "task/fcf629b4a0b343558f461338ba1b9da4.mp3"),
        new(3, Lang.English, "536d3a5e000945adb7038665781a4aca", "Ethan", Samples + "task/bdd677ed767744fc91f166468786264b.mp3"),
    ];

    private static readonly FishVoiceChoice[] Arabic = [
        new(1, Lang.Arabic, "0b14f34a13a94ed88fe6113193749bb0", "فاطمة", Samples + "tasks/2026-08-24/93406e1b905a4c4994306e3421243390.mp3"),
        new(2, Lang.Arabic, "05bf47f4aad948e1a7167a56e1d21c22", "حنان", Samples + "task/86d9c88488ae4406a73fd780db3f65b9.mp3"),
        new(3, Lang.Arabic, "14f1000b77d547eeb5f03b474dd29e0f", "أسماء", Samples + "task/7181ceaaf4214b00be3c8302f6b49f56.mp3"),
        new(4, Lang.Arabic, "10c5c2a37a284a81bb0cf3c53955d795", "شاب سعودي", Samples + "task/214291316a0a4d9084bbb15532049f5e.mp3"),
    ];
}
