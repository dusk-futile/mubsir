using System.Text.Json;
using System.Text.Json.Serialization;
using Basira.App.Abstractions;
using Basira.Core.Text;

namespace Basira.App.Actions;

/// <summary>Why a path may or may not go to the Recycle Bin.</summary>
internal enum RecycleEligibility
{
    Ok,
    NotFound,
    NotFixedDrive,
    TooLarge,
}

/// <summary>
/// File helpers for the agent's file tools: library-name resolution, never-overwrite naming, recycle eligibility,
/// name-only listing and search, and the undo journal. Pure parts are static and take their I/O as delegates.
/// </summary>
internal static class FileOps
{
    public const int MaxResults = 50;
    public const int MaxDepth = 4;
    public const long MaxRecycleBytes = 500L * 1024 * 1024;

    private static readonly (string[] Names, Func<string> Path)[] Libraries =
    [
        (["desktop", "سطح المكتب", "المكتب"], () => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)),
        (["documents", "my documents", "المستندات", "مستنداتي", "الوثائق"], () => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
        (["downloads", "التنزيلات", "التحميلات", "تنزيلات"], () => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")),
        (["pictures", "photos", "الصور", "صوري"], () => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)),
        (["videos", "الفيديو", "الفيديوهات", "الفيديوات"], () => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)),
        (["music", "الموسيقى", "الأغاني"], () => Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)),
    ];

    /// <summary>The user's library folders, the roots the file tools may touch.</summary>
    public static IReadOnlyList<string> LibraryRoots() =>
        Libraries.Select(l => SafePath(l.Path)).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>
    /// Expands a library name ("Desktop", "سطح المكتب", "Documents\\letters") to a full path; full paths are
    /// normalized with GetFullPath. Returns null for relative paths that do not start with a library name.
    /// </summary>
    public static string? ResolveUserPath(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var s = input.Trim().Trim('"').Trim();
        if (Path.IsPathFullyQualified(s))
        {
            try
            {
                return Path.GetFullPath(s);
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return null;
            }
        }

        var normalized = s.Replace('/', '\\');
        int slash = normalized.IndexOf('\\');
        var head = slash < 0 ? normalized : normalized[..slash];
        var rest = slash < 0 ? "" : normalized[(slash + 1)..];
        var folded = ArabicFolding.Fold(head);
        foreach (var (names, path) in Libraries)
        {
            if (names.Any(n => ArabicFolding.Fold(n) == folded) && SafePath(path) is { } root)
            {
                try
                {
                    return rest.Length == 0 ? root : Path.GetFullPath(Path.Combine(root, rest));
                }
                catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    return null;
                }
            }
        }

        return null;
    }

    /// <summary>Destination for move/copy: into <paramref name="to"/> when it is a folder, else <paramref name="to"/> itself.</summary>
    public static string? Destination(string from, string? to, Func<string, bool> isDirectory)
    {
        var resolved = ResolveUserPath(to);
        if (resolved is null)
        {
            return null;
        }

        bool intoFolder = isDirectory(resolved) || (to?.EndsWith('\\') ?? false) || (to?.EndsWith('/') ?? false);
        return intoFolder ? Path.Combine(resolved, Path.GetFileName(from.TrimEnd('\\', '/'))) : resolved;
    }

    /// <summary>"name.ext" if free, else "name (2).ext", "name (3).ext"…; never an existing path.</summary>
    public static string UniquePath(string desired, Func<string, bool> exists)
    {
        if (!exists(desired))
        {
            return desired;
        }

        var dir = Path.GetDirectoryName(desired) ?? "";
        var name = Path.GetFileNameWithoutExtension(desired);
        var ext = Path.GetExtension(desired);
        for (int i = 2; i < 10_000; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(dir, $"{name} ({Guid.NewGuid():N}){ext}");
    }

    /// <summary>
    /// The Recycle Bin only exists on fixed local drives; on USB sticks, network shares and for very large items Windows
    /// deletes permanently, so those are refused.
    /// </summary>
    public static RecycleEligibility CanRecycle(bool exists, DriveType? driveType, long sizeBytes)
    {
        if (!exists)
        {
            return RecycleEligibility.NotFound;
        }

        if (driveType != DriveType.Fixed)
        {
            return RecycleEligibility.NotFixedDrive;
        }

        return sizeBytes > MaxRecycleBytes ? RecycleEligibility.TooLarge : RecycleEligibility.Ok;
    }

    public static RecycleEligibility CheckRecycle(string path)
    {
        try
        {
            bool isFile = File.Exists(path);
            bool isDir = !isFile && Directory.Exists(path);
            var root = Path.GetPathRoot(path);
            DriveType? type = null;
            if (!string.IsNullOrEmpty(root) && !root.StartsWith(@"\\", StringComparison.Ordinal))
            {
                type = new DriveInfo(root).DriveType;
            }

            long size = isFile ? new FileInfo(path).Length : isDir ? FolderSize(path, MaxRecycleBytes + 1) : 0;
            return CanRecycle(isFile || isDir, type, size);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return RecycleEligibility.NotFound;
        }
    }

    /// <summary>Folder and file names (folders first, marked), at most <see cref="MaxResults"/>.</summary>
    public static (List<string> Lines, int Total) ListFolder(string path)
    {
        var dir = new DirectoryInfo(path);
        var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
        var folders = dir.EnumerateDirectories("*", options).Select(d => d.Name).Order(StringComparer.CurrentCultureIgnoreCase).ToList();
        var files = dir.EnumerateFiles("*", options).Select(f => f.Name).Order(StringComparer.CurrentCultureIgnoreCase).ToList();
        var lines = folders.Select(f => f + "\\ (folder)").Concat(files).Take(MaxResults).ToList();
        return (lines, folders.Count + files.Count);
    }

    /// <summary>Breadth-first name search (depth ≤ 4); every query word must appear in the folded file name.</summary>
    public static List<string> FindFiles(string query, IEnumerable<string> roots, CancellationToken ct, TimeSpan budget)
    {
        var words = FuzzyMatch.Fold(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var results = new List<string>();
        if (words.Length == 0)
        {
            return results;
        }

        long deadline = Environment.TickCount64 + (long)budget.TotalMilliseconds;
        var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint };
        var queue = new Queue<(string Dir, int Depth)>(roots.Where(Directory.Exists).Select(r => (r, 0)));
        while (queue.Count > 0 && results.Count < MaxResults && Environment.TickCount64 < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var (dir, depth) = queue.Dequeue();
            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(dir, "*", options))
                {
                    var name = Path.GetFileName(entry);
                    var folded = FuzzyMatch.Fold(name);
                    if (words.All(w => folded.Contains(w, StringComparison.Ordinal)))
                    {
                        results.Add(entry);
                        if (results.Count >= MaxResults)
                        {
                            break;
                        }
                    }

                    if (depth + 1 < MaxDepth && Directory.Exists(entry))
                    {
                        queue.Enqueue((entry, depth + 1));
                    }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }

        return results;
    }

    /// <summary>Appends one line to the undo journal (local file only; paths never go to logs or the cloud).</summary>
    public static void Journal(string op, string from, string? to)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            var entry = new UndoEntry(DateTimeOffset.UtcNow, op, from, to);
            var line = JsonSerializer.Serialize(entry, UndoJson.Default.UndoEntry);
            File.AppendAllText(AppPaths.UndoJournalFile, line + Environment.NewLine);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The operation itself succeeded; a missing journal line only loses undo for it.
        }
    }

    private static long FolderSize(string path, long stopAt)
    {
        long total = 0;
        var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true };
        foreach (var f in new DirectoryInfo(path).EnumerateFiles("*", options))
        {
            total += f.Length;
            if (total >= stopAt)
            {
                break;
            }
        }

        return total;
    }

    private static string? SafePath(Func<string> get)
    {
        try
        {
            var p = get();
            return string.IsNullOrWhiteSpace(p) ? null : p;
        }
        catch (Exception e) when (e is PlatformNotSupportedException or System.Security.SecurityException)
        {
            return null;
        }
    }
}

/// <summary>One undo journal line.</summary>
internal sealed record UndoEntry(DateTimeOffset At, string Op, string From, string? To);

[JsonSerializable(typeof(UndoEntry))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class UndoJson : JsonSerializerContext;
