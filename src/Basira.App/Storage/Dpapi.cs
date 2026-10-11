using System.Security.Cryptography;
using System.Text;

namespace Basira.App.Storage;

/// <summary>
/// DPAPI for the current Windows user, with a fixed application entropy so other programs that call DPAPI
/// for the same user cannot decrypt Basira's files by accident.
/// </summary>
internal static class Dpapi
{
    private static readonly byte[] Entropy = "Basira/6f1c2d8e/user-data/v1"u8.ToArray();
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static byte[] Protect(string text) =>
        ProtectedData.Protect(Utf8NoBom.GetBytes(text), Entropy, DataProtectionScope.CurrentUser);

    /// <exception cref="CryptographicException">The data was encrypted by another user, another PC, or is corrupt.</exception>
    public static string Unprotect(byte[] data) =>
        Utf8NoBom.GetString(ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser));

    /// <summary>Reads and decrypts a file. Null when it does not exist; throws <see cref="CryptographicException"/> when unreadable.</summary>
    public static string? ReadFile(string path)
    {
        byte[]? data = AtomicFile.ReadAllBytesOrNull(path);
        return data is null || data.Length == 0 ? null : Unprotect(data);
    }

    public static void WriteFile(string path, string text) => AtomicFile.WriteAllBytes(path, Protect(text));
}
