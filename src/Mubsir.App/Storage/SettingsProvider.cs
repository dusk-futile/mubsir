using System.Security.Cryptography;
using System.Text.Json;
using Mubsir.Core.Config;
using Mubsir.Core.Logging;

namespace Mubsir.App.Storage;

internal sealed class SettingsProvider : IDisposable
{
    private readonly object _gate = new();
    private readonly IEventLog _log;
    private readonly string _settingsPath;
    private readonly string _profilePath;
    private MubsirSettings _settings;
    private UserProfile _profile;
    private string? _lastWrittenSettingsJson;
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;

    public SettingsProvider(IEventLog log, string? settingsPath = null, string? profilePath = null)
    {
        _log = log;
        _settingsPath = settingsPath ?? AppPaths.SettingsFile;
        _profilePath = profilePath ?? AppPaths.ProfileFile;
        _settings = LoadSettings();
        _profile = LoadProfile();
    }

    public event Action? Changed;

    public MubsirSettings Settings
    {
        get { lock (_gate) { return _settings; } }
    }

    public UserProfile Profile
    {
        get { lock (_gate) { return _profile; } }
    }

    public void Update(Func<MubsirSettings, MubsirSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        lock (_gate)
        {
            MubsirSettings next = SettingsSerializer.Normalize(change(_settings));
            if (next == _settings)
            {
                return;
            }

            _settings = next;
            WriteSettings(next);
        }

        RaiseChanged();
    }

    public void UpdateProfile(Func<UserProfile, UserProfile> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        lock (_gate)
        {
            UserProfile next = SettingsSerializer.Normalize(change(_profile));
            _profile = next;
            try
            {
                Dpapi.WriteFile(_profilePath, SettingsSerializer.Serialize(next));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or CryptographicException)
            {
                _log.Failure("profile_write", e);
            }
        }

        RaiseChanged();
    }

    public void StartWatching()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_settingsPath);
            if (dir is null)
            {
                return;
            }

            _debounce = new Timer(_ => ReloadFromDisk(), null, Timeout.Infinite, Timeout.Infinite);
            _watcher = new FileSystemWatcher(dir, Path.GetFileName(_settingsPath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            };
            _watcher.Changed += (_, _) => _debounce?.Change(500, Timeout.Infinite);
            _watcher.Created += (_, _) => _debounce?.Change(500, Timeout.Infinite);
            _watcher.Renamed += (_, _) => _debounce?.Change(500, Timeout.Infinite);
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception e) when (e is IOException or ArgumentException or PlatformNotSupportedException or UnauthorizedAccessException)
        {
            _log.Failure("settings_watch", e);
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce?.Dispose();
    }

    private void ReloadFromDisk()
    {
        try
        {
            string json = File.ReadAllText(_settingsPath);
            bool changed;
            lock (_gate)
            {
                if (json == _lastWrittenSettingsJson)
                {
                    return;
                }

                if (!SettingsSerializer.TryDeserializeSettings(json, out MubsirSettings loaded))
                {
                    _log.Error("settings_reload_malformed");
                    return;
                }

                changed = loaded != _settings;
                _settings = loaded;
                _lastWrittenSettingsJson = json;
            }

            if (changed)
            {
                _log.Event(LogKind.Startup, tool: "settings_reloaded");
                RaiseChanged();
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _log.Failure("settings_reload", e);
        }
    }

    private MubsirSettings LoadSettings()
    {
        string? json = null;
        try
        {
            json = File.Exists(_settingsPath) ? File.ReadAllText(_settingsPath) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _log.Failure("settings_read", e);
        }

        if (json is null)
        {
            var defaults = new MubsirSettings();
            WriteSettings(defaults);
            return defaults;
        }

        if (SettingsSerializer.TryDeserializeSettings(json, out MubsirSettings settings))
        {
            _lastWrittenSettingsJson = json;
            return settings;
        }

        _log.Error("settings_malformed");
        AtomicFile.QuarantineQuietly(_settingsPath);
        var fresh = new MubsirSettings();
        WriteSettings(fresh);
        return fresh;
    }

    private UserProfile LoadProfile()
    {
        try
        {
            string? json = Dpapi.ReadFile(_profilePath);
            if (json is null)
            {
                return new UserProfile();
            }

            if (SettingsSerializer.TryDeserializeProfile(json, out UserProfile profile))
            {
                return profile;
            }

            _log.Error("profile_malformed");
        }
        catch (Exception e) when (e is CryptographicException or IOException or UnauthorizedAccessException or JsonException)
        {
            _log.Failure("profile_read", e);
        }

        AtomicFile.QuarantineQuietly(_profilePath);
        return new UserProfile();
    }

    private void WriteSettings(MubsirSettings settings)
    {
        try
        {
            string json = SettingsSerializer.Serialize(settings);
            _lastWrittenSettingsJson = json;
            AtomicFile.WriteAllText(_settingsPath, json);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _log.Failure("settings_write", e);
        }
    }

    private void RaiseChanged()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.Failure("settings_changed_handler", e);
        }
    }
}
