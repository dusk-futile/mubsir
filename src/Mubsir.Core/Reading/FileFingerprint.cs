using System.Security.Cryptography;

namespace Mubsir.Core.Reading;

/// <summary>
/// Identifies a file by its contents, so a bookmark survives a rename or a move. Hashes the length, the
/// first megabyte and the last megabyte: fast on a 300 MB book, and two different books don't collide.
/// </summary>
public static class FileFingerprint
{
    private const int EdgeBytes = 1024 * 1024;

    /// <summary>The fingerprint, or null if the file can't be read.</summary>
    public static string? Of(string path)
    {
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return Of(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    public static string Of(Stream file)
    {
        ArgumentNullException.ThrowIfNull(file);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(BitConverter.GetBytes(file.Length));
        var buffer = new byte[EdgeBytes];
        Append(hash, file, buffer);
        if (file.Length > EdgeBytes)
        {
            file.Seek(Math.Max(EdgeBytes, file.Length - EdgeBytes), SeekOrigin.Begin);
            Append(hash, file, buffer);
        }

        return "sha256:" + Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void Append(IncrementalHash hash, Stream file, byte[] buffer)
    {
        int read = file.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
        hash.AppendData(buffer, 0, read);
    }
}
