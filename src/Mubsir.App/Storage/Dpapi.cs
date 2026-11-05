using System.Security.Cryptography;
using System.Text;

namespace Mubsir.App.Storage;

internal static class Dpapi
{
    private static readonly byte[] Entropy = "Mubsir/6f1c2d8e/user-data/v1"u8.ToArray();
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static byte[] Protect(string text) =>
        ProtectedData.Protect(Utf8NoBom.GetBytes(text), Entropy, DataProtectionScope.CurrentUser);

    public static string Unprotect(byte[] data) =>
    Utf8NoBom.GetString(ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser));

    public static string? ReadFile(string path)
    {
        byte[]? data = AtomicFile.ReadAllBytesOrNull(path);
        return data is null || data.Length == 0 ? null : Unprotect(data);
    }

    public static void WriteFile(string path, string text) => AtomicFile.WriteAllBytes(path, Protect(text));
}
