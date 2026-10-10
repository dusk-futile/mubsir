using Basira.Core.Common;

namespace Basira.Core.Text;

/// <summary>A span of text spoken by one language's voice.</summary>
/// <param name="Text">The exact characters of the span.</param>
/// <param name="Lang">The voice that should speak it.</param>
/// <param name="SpellOut">Read letter by letter (short acronyms such as USB inside Arabic text).</param>
public sealed record TextRun(string Text, Lang Lang, bool SpellOut = false);
