using Anthropic;
using Basira.App.Abstractions;
using Basira.App.Screen;
using Basira.Core.Agent;
using Basira.Core.Config;
using Basira.Core.Logging;

namespace Basira.App.Brain;

/// <summary>
/// The Claude brain (official Anthropic C# SDK). The API key comes from the DPAPI secret store on every session, so a
/// key added later works without a restart; one client is kept per key.
/// </summary>
internal sealed class ClaudeBrain(ISecretStore secrets, ISettingsProvider settings, IEventLog log) : IBrain
{
    /// <summary>Per-request HTTP timeout; streaming answers to short spoken requests finish well within it.</summary>
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(90);

    private readonly object _gate = new();
    private AnthropicClient? _client;
    private string? _clientKey;

    public IBrainSession StartSession(BrainSessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        string model;
        try
        {
            model = settings.Settings.BrainModel;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            model = BasiraSettings.DefaultBrainModel;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            model = BasiraSettings.DefaultBrainModel;
        }

        return new ClaudeSession(this, options, model.Trim(), log ?? NullEventLog.Instance);
    }

    /// <summary>The client for the current key, or null when no key is stored.</summary>
    internal AnthropicClient? Client()
    {
        string? key;
        try
        {
            key = secrets.Get(SecretNames.Anthropic);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            log.Fail(LogKind.Error, "secrets." + MetaLog.Code(e));
            key = null;
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        key = key.Trim();
        lock (_gate)
        {
            if (_client is null || !string.Equals(_clientKey, key, StringComparison.Ordinal))
            {
                // Sessions holding the old client finish with it; it is simply dropped afterwards.
                _client = new AnthropicClient
                {
                    ApiKey = key,
                    MaxRetries = 2,
                    Timeout = RequestTimeout,
                };
                _clientKey = key;
            }

            return _client;
        }
    }
}
