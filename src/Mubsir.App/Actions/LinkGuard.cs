namespace Mubsir.App.Actions;

internal static class LinkGuard
{
    public static bool PassesThroughLink(string path)
    {
        string? current;
        try
        {
            current = Path.GetFullPath(path);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return true;
        }

        while (!string.IsNullOrEmpty(current))
        {
            try
            {
                FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
                if (info.Exists && info.LinkTarget is not null)
                {
                    return true;
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return true;
            }

            current = Path.GetDirectoryName(current);
        }

        return false;
    }

    public static bool AnyLink(IEnumerable<string?> paths) => paths.Any(p => p is not null && PassesThroughLink(p));
}
