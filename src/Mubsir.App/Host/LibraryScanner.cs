using Mubsir.App.Screen.Documents;
using Mubsir.Core.Library;
using Mubsir.Core.Logging;

namespace Mubsir.App.Host;

/// <summary>
/// Quietly makes a library card for every book on this PC, at the lowest priority, one file at a time. Reads only
/// the first pages and sends nothing anywhere. Looks again every few hours to pick up new books.
/// </summary>
internal sealed class LibraryScanner(BookLibrary library, DocumentReader documents, IEventLog log)
{
    private const long MinBookBytes = 20 * 1024;
    private const int SaveEvery = 25;
    private static readonly TimeSpan FirstWait = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan BetweenFiles = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan BetweenScans = TimeSpan.FromHours(6);
    private static readonly HashSet<string> SkippedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows", "Program Files", "Program Files (x86)", "ProgramData", "AppData", "$Recycle.Bin", "System Volume Information", "node_modules", ".git", "Recovery",
    };

    public void Start(CancellationToken stopping)
    {
        var thread = new Thread(() => Run(stopping)) { IsBackground = true, Priority = ThreadPriority.Lowest, Name = "library-scan" };
        thread.Start();
    }

    private void Run(CancellationToken stopping)
    {
        try
        {
            stopping.WaitHandle.WaitOne(FirstWait);
            while (!stopping.IsCancellationRequested)
            {
                ScanAll(stopping);
                stopping.WaitHandle.WaitOne(BetweenScans);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            log.Failure("library_scan", e);
        }
    }

    private void ScanAll(CancellationToken stopping)
    {
        int added = 0;
        foreach (string file in Roots().SelectMany(Files))
        {
            stopping.ThrowIfCancellationRequested();
            var info = new FileInfo(file);
            if (!info.Exists || info.Length < MinBookBytes || info.Length > DocumentReader.MaxFileBytes || library.IsCurrent(file, info.Length, info.LastWriteTimeUtc))
            {
                continue;
            }

            library.Put(documents.ReadCardAsync(file, stopping).GetAwaiter().GetResult());
            if (++added % SaveEvery == 0)
            {
                library.Save();
            }

            stopping.WaitHandle.WaitOne(BetweenFiles);
        }

        library.Forget(File.Exists);
        library.Save();
        log.Event(LogKind.Startup, tool: "library_scan");
    }

    /// <summary>The places books usually are first, then every other fixed drive.</summary>
    private static IEnumerable<string> Roots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] first =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Path.Combine(profile, "Downloads"),
            profile,
        ];
        IEnumerable<string> drives = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady).Select(d => d.RootDirectory.FullName);
        return first.Concat(drives).Where(r => !string.IsNullOrEmpty(r) && Directory.Exists(r) && seen.Add(r));
    }

    private static IEnumerable<string> Files(string root)
    {
        var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint };
        var folders = new Stack<string>();
        folders.Push(root);
        while (folders.Count > 0)
        {
            string folder = folders.Pop();
            foreach (string file in Directory.EnumerateFiles(folder, "*", options))
            {
                if (Path.GetExtension(file).ToLowerInvariant() is ".pdf" or ".docx")
                {
                    yield return file;
                }
            }

            foreach (string sub in Directory.EnumerateDirectories(folder, "*", options))
            {
                if (!SkippedFolders.Contains(Path.GetFileName(sub)))
                {
                    folders.Push(sub);
                }
            }
        }
    }
}
