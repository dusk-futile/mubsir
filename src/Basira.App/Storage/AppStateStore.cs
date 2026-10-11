using System.Text.Json;
using System.Text.Json.Nodes;
using Basira.App.Abstractions;
using Basira.App.Diagnostics;
using Basira.Core.Logging;

namespace Basira.App.Storage;

/// <summary>
/// Small bits of app state that are not user settings (e.g. the day the "no AI key" notice was last spoken),
/// in plain <c>state.json</c>. Never holds content. Thread-safe.
/// </summary>
internal sealed class AppStateStore
{
    private readonly object _gate = new();
    private readonly IEventLog _log;
    private readonly string _path;
    private JsonObject _state;

    public AppStateStore(IEventLog log, string? path = null)
    {
        _log = log;
        _path = path ?? Path.Combine(AppPaths.DataDir, "state.json");
        _state = Load();
    }

    public string? GetString(string key)
    {
        lock (_gate)
        {
            return _state.TryGetPropertyValue(key, out JsonNode? node) && node is JsonValue v && v.TryGetValue(out string? s) ? s : null;
        }
    }

    public void SetString(string key, string? value)
    {
        lock (_gate)
        {
            _state[key] = value is null ? null : JsonValue.Create(value);
            try
            {
                AtomicFile.WriteAllText(_path, _state.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _log.Failure("state_write", e);
            }
        }
    }

    private JsonObject Load()
    {
        try
        {
            if (File.Exists(_path) && JsonNode.Parse(File.ReadAllText(_path)) is JsonObject obj)
            {
                return obj;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            _log.Failure("state_read", e);
        }

        return new JsonObject();
    }
}
