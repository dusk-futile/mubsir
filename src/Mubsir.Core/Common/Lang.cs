namespace Mubsir.Core.Common;

public enum Lang
{
    Arabic,
    English,
}

public static class LangExtensions
{
    public static string Tag(this Lang lang) => lang == Lang.Arabic ? "ar" : "en";

    public static Lang Other(this Lang lang) => lang == Lang.Arabic ? Lang.English : Lang.Arabic;
}
