using Anthropic;
using Mubsir.Core.Agent;
using Mubsir.Core.Config;
using Mubsir.Core.Logging;

namespace Mubsir.App.Brain;

internal sealed class ClaudeBrain(SecretStore secrets, SettingsProvider settings, IEventLog log) : IBrain
{
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
            model = MubsirSettings.DefaultBrainModel;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            model = MubsirSettings.DefaultBrainModel;
        }

        return new ClaudeSession(this, options, model.Trim(), log ?? NullEventLog.Instance);
    }

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
