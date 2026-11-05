using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mubsir.Core.Logging;

namespace Mubsir.App.Storage;

internal sealed class SecretStore
{
    private readonly object _gate = new();
    private readonly string _path;
    private readonly IEventLog _log;
    private Dictionary<string, string> _values;

    public SecretStore(IEventLog log, string? path = null)
    {
        _log = log;
        _path = path ?? AppPaths.SecretsFile;
        _values = Load();
    }

    public string? Get(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        lock (_gate)
        {
            return _values.TryGetValue(Normalize(name), out string? v) && !string.IsNullOrWhiteSpace(v) ? v : null;
        }
    }

    public void Set(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A secret needs a name.", nameof(name));
        }

        lock (_gate)
        {
            var next = new Dictionary<string, string>(_values, StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(value))
            {
                next.Remove(Normalize(name));
            }
            else
            {
                next[Normalize(name)] = value.Trim();
            }

            Dpapi.WriteFile(_path, JsonSerializer.Serialize(next, StorageJson.Default.DictionaryStringString));
            _values = next;
        }
    }

    public bool Has(string name) => Get(name) is not null;

    private static string Normalize(string name) => name.Trim().ToLowerInvariant();

    private Dictionary<string, string> Load()
    {
        try
        {
            string? json = Dpapi.ReadFile(_path);
            if (json is null)
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            var parsed = JsonSerializer.Deserialize(json, StorageJson.Default.DictionaryStringString);
            return parsed is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(parsed.Where(kv => !string.IsNullOrWhiteSpace(kv.Key)).ToDictionary(kv => Normalize(kv.Key), kv => kv.Value), StringComparer.Ordinal);
        }
        catch (Exception e) when (e is CryptographicException or JsonException or IOException or UnauthorizedAccessException)
        {
            _log.Failure("secrets_load", e);
            AtomicFile.QuarantineQuietly(_path);
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class StorageJson : JsonSerializerContext;
