using Basira.Core.Common;

namespace Basira.Core.Text;

/// <summary>A word or phrase in both of Basira's languages.</summary>
internal readonly record struct Bilingual(string Ar, string En)
{
    public string For(Lang lang) => lang == Lang.Arabic ? Ar : En;
}
