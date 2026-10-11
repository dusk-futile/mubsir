using Basira.Core.Common;
using Basira.Core.Text;

namespace Basira.Core.Tests.Text;

/// <summary>Compact rendering and invariants for language runs.</summary>
internal static class RunAssert
{
    /// <summary>Renders runs as "ar:…|en*:…" where * marks spelled-out runs.</summary>
    public static string Describe(IReadOnlyList<TextRun> runs) =>
        string.Join("|", runs.Select(r => $"{r.Lang.Tag()}{(r.SpellOut ? "*" : "")}:{r.Text}"));

    /// <summary>Run texts concatenate to the input; no run is empty; neighbours differ in language or spelling.</summary>
    public static void Lossless(string input, IReadOnlyList<TextRun> runs)
    {
        Assert.Equal(input, string.Concat(runs.Select(r => r.Text)));
        Assert.All(runs, r => Assert.NotEqual("", r.Text));
        for (int i = 1; i < runs.Count; i++)
        {
            Assert.False(
                runs[i].Lang == runs[i - 1].Lang && runs[i].SpellOut == runs[i - 1].SpellOut,
                $"runs {i - 1} and {i} should have been merged: {Describe(runs)}");
        }
    }

    public static string TextIn(IReadOnlyList<TextRun> runs, Lang lang) =>
        string.Concat(runs.Where(r => r.Lang == lang).Select(r => r.Text));
}
