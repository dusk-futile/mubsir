using System.Text.Json;
using System.Text.Json.Serialization;
using Mubsir.Core.Common;
using Mubsir.Core.Text;

namespace Mubsir.App.Actions;

internal enum RecycleEligibility
{
    Ok,
    NotFound,
    NotFixedDrive,
    TooLarge,
}

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

    public static IReadOnlyList<string> LibraryRoots() =>
    Libraries.Select(l => SafePath(l.Path)).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();

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

    public static (List<string> Lines, int Total) ListFolder(string path)
    {
        var dir = new DirectoryInfo(path);
        var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
        var folders = dir.EnumerateDirectories("*", options).Select(d => d.Name).Order(StringComparer.CurrentCultureIgnoreCase).ToList();
        var files = dir.EnumerateFiles("*", options).Select(f => f.Name).Order(StringComparer.CurrentCultureIgnoreCase).ToList();
        var lines = folders.Select(f => f + "\\ (folder)").Concat(files).Take(MaxResults).ToList();
        return (lines, folders.Count + files.Count);
    }

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
        }
    }

    public static (bool Success, string Message) UndoLast(Lang lang)
    {
        try
        {
            if (!File.Exists(AppPaths.UndoJournalFile))
            {
                return (false, lang == Lang.Arabic ? "لا يوجد ما يمكن التراجع عنه." : "Nothing to undo.");
            }

            string[] lines = File.ReadAllLines(AppPaths.UndoJournalFile);
            if (lines.Length == 0)
            {
                return (false, lang == Lang.Arabic ? "لا يوجد ما يمكن التراجع عنه." : "Nothing to undo.");
            }

            int targetIndex = -1;
            UndoEntry? target = null;
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i].Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                try
                {
                    var entry = JsonSerializer.Deserialize(line, UndoJson.Default.UndoEntry);
                    if (entry is not null && entry.Op is "move" or "rename" or "copy")
                    {
                        targetIndex = i;
                        target = entry;
                        break;
                    }
                }
                catch (JsonException)
                {
                }
            }

            if (targetIndex < 0 || target is null)
            {
                return (false, lang == Lang.Arabic ? "لا يوجد ما يمكن التراجع عنه." : "Nothing to undo.");
            }

            string op = target.Op;
            string from = target.From;
            string? to = target.To;

            if (op is "move" or "rename")
            {
                if (string.IsNullOrWhiteSpace(to) || (!File.Exists(to) && !Directory.Exists(to)))
                {
                    return (false, lang == Lang.Arabic ? "الملف لم يعد موجوداً في موقعه الجديد للتراجع." : "The file is no longer at the new location to undo.");
                }

                if (File.Exists(from) || Directory.Exists(from))
                {
                    return (false, lang == Lang.Arabic ? "يوجد ملف بالفعل في الموقع الأصلي، لا يمكن الكتابة فوقه." : "A file already exists at the original location; cannot overwrite it.");
                }

                if (File.Exists(to))
                {
                    File.Move(to, from, overwrite: false);
                }
                else
                {
                    Directory.Move(to, from);
                }
            }
            else if (op == "copy")
            {
                if (string.IsNullOrWhiteSpace(to) || (!File.Exists(to) && !Directory.Exists(to)))
                {
                    return (false, lang == Lang.Arabic ? "النسخة لم تعد موجودة للتراجع عنها." : "The copied file no longer exists to undo.");
                }

                // The copy may have been changed since, so it goes to the Recycle Bin where it can be got back.
                if (SystemTools.Recycle([to]) is not null)
                {
                    return (false, lang == Lang.Arabic ? "فشل التراجع عن العملية." : "Undo failed.");
                }
            }

            var remaining = lines.Where((_, idx) => idx != targetIndex).ToArray();
            File.WriteAllLines(AppPaths.UndoJournalFile, remaining);

            string name = Path.GetFileName(op == "copy" ? to! : from);
            string msg = lang == Lang.Arabic
                ? (op == "move" ? $"تم التراجع عن نقل {name}." : op == "rename" ? $"تم التراجع عن إعادة تسمية {name}." : $"تم التراجع عن نسخ {name}.")
                : (op == "move" ? $"Undid moving {name}." : op == "rename" ? $"Undid renaming {name}." : $"Undid copying {name}.");

            return (true, msg);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return (false, lang == Lang.Arabic ? "فشل التراجع عن العملية." : "Undo failed.");
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

internal sealed record UndoEntry(DateTimeOffset At, string Op, string From, string? To);

[JsonSerializable(typeof(UndoEntry))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class UndoJson : JsonSerializerContext;
