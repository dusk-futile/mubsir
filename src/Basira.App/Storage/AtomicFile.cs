using System.Security.Cryptography;
using System.Text;

namespace Basira.App.Storage;

/// <summary>Crash-safe file writes: write a temp file next to the target, flush it, then replace the target.</summary>
internal static class AtomicFile
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static void WriteAllBytes(string path, ReadOnlySpan<byte> data)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
        Directory.CreateDirectory(dir);
        string temp = Path.Combine(dir, "." + Path.GetFileName(path) + "." + Convert.ToHexString(RandomNumberGenerator.GetBytes(4)) + ".tmp");
        try
        {
            using (var fs = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                fs.Write(data);
                fs.Flush(flushToDisk: true);
            }

            // MoveFileEx(MOVEFILE_REPLACE_EXISTING): the reader sees the old file or the new one, never half of one.
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    public static void WriteAllText(string path, string text) => WriteAllBytes(path, Utf8NoBom.GetBytes(text));

    /// <summary>The file's bytes, or null when it does not exist.</summary>
    public static byte[]? ReadAllBytesOrNull(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    /// <summary>Moves an unreadable file aside (path.bad-yyyyMMddHHmmss) so it is not overwritten blindly.</summary>
    public static void QuarantineQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Move(path, path + ".bad-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture), overwrite: true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
