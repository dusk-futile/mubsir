using Mubsir.Core.Common;

namespace Mubsir.Core.Text;

internal readonly record struct Bilingual(string Ar, string En)
{
    public string For(Lang lang) => lang == Lang.Arabic ? Ar : En;
}
