using Mubsir.Core.Common;

namespace Mubsir.Core.Text;

public sealed record TextRun(string Text, Lang Lang, bool SpellOut = false);
