namespace Basira.Core.Common;

/// <summary>The two languages Basira speaks and understands.</summary>
public enum Lang
{
    Arabic,
    English,
}

public static class LangExtensions
{
    /// <summary>BCP-47 tag used in SSML and for voice selection.</summary>
    public static string Tag(this Lang lang) => lang == Lang.Arabic ? "ar" : "en";

    public static Lang Other(this Lang lang) => lang == Lang.Arabic ? Lang.English : Lang.Arabic;
}
