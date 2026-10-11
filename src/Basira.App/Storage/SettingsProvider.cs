using System.Security.Cryptography;
using System.Text.Json;
using Basira.App.Abstractions;
using Basira.App.Diagnostics;
using Basira.Core.Config;
using Basira.Core.Logging;

namespace Basira.App.Storage;

/// <summary>
/// Settings in plain <c>settings.json</c> (no secrets, so a sighted helper can edit it) and the profile in
/// DPAPI-encrypted <c>profile.dat</c>. Writes are atomic; edits made to settings.json while Basira runs are
/// picked up. Thread-safe.
/// </summary>
internal sealed class SettingsProvider : ISettingsProvider, IDisposable
{
    private readonly object _gate = new();
    private readonly IEventLog _log;
    private readonly string _settingsPath;
    private readonly string _profilePath;
    private BasiraSettings _settings;
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

    public BasiraSettings Settings
    {
        get { lock (_gate) { return _settings; } }
    }

    public UserProfile Profile
    {
        get { lock (_gate) { return _profile; } }
    }

    public void Update(Func<BasiraSettings, BasiraSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        lock (_gate)
        {
            BasiraSettings next = SettingsSerializer.Normalize(change(_settings));
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
                // The change still applies for this session.
                _log.Failure("profile_write", e);
            }
        }

        RaiseChanged();
    }

    /// <summary>Starts picking up edits a helper makes to settings.json (e.g. from the tray menu's "open settings").</summary>
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
                    return; // our own write
                }

                if (!SettingsSerializer.TryDeserializeSettings(json, out BasiraSettings loaded))
                {
                    // A half-typed edit: keep the current settings and wait for the next save.
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

    private BasiraSettings LoadSettings()
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
            var defaults = new BasiraSettings();
            WriteSettings(defaults);
            return defaults;
        }

        if (SettingsSerializer.TryDeserializeSettings(json, out BasiraSettings settings))
        {
            _lastWrittenSettingsJson = json;
            return settings;
        }

        _log.Error("settings_malformed");
        AtomicFile.QuarantineQuietly(_settingsPath);
        var fresh = new BasiraSettings();
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

    private void WriteSettings(BasiraSettings settings)
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
