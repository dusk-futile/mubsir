using Mubsir.Core.Common;
using Mubsir.Core.Config;
using Mubsir.Core.Policy;

namespace Mubsir.App.Orchestration;

internal static class TaskContexts
{
    public static TaskContext Create(string utterance, Lang lang, MubsirSettings settings) => new()
    {
        UserUtterance = utterance,
        Lang = lang,
        UserNamedDomains = RequestDomains.Extract(utterance),
        LibraryRoots = LibraryFolders.Roots,
        BankDomains = settings.BankDomains ?? [],
        PrivateMode = settings.PrivateMode,
    };
}

internal static class LibraryFolders
{
    private static readonly Lazy<IReadOnlyList<string>> LazyRoots = new(Resolve);

    public static IReadOnlyList<string> Roots => LazyRoots.Value;

    private static IReadOnlyList<string> Resolve()
    {
        var roots = new List<string>(6);
        void Add(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !roots.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                roots.Add(Path.TrimEndingDirectorySeparator(path));
            }
        }

        Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (profile.Length > 0)
        {
            Add(Path.Combine(profile, "Downloads"));
        }

        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
        return roots;
    }
}
