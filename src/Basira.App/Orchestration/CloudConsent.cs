using Basira.App.Diagnostics;
using Basira.Core.Agent;
using Basira.Core.Logging;

namespace Basira.App.Orchestration;

/// <summary>
/// First-run privacy consent: a short spoken summary of what goes to which provider and what never leaves the PC,
/// then Enter to agree or Escape to keep the AI off. Nothing is sent to the cloud before consent; the answer can only
/// be given with a key, since a spoken "yes" would itself need the cloud.
/// </summary>
internal sealed class CloudConsent
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private readonly AppServices _app;
    private readonly Confirmer _confirmer;
    private int _asking;

    public CloudConsent(AppServices app, Confirmer confirmer)
    {
        _app = app;
        _confirmer = confirmer;
    }

    public bool Given => _app.Settings.Profile.CloudConsentGiven;

    public bool IsAsking => Volatile.Read(ref _asking) != 0;

    /// <summary>Asks unless consent was already given. Returns whether it is given now.</summary>
    public async Task<bool> EnsureAsync(CancellationToken ct)
    {
        if (Given)
        {
            return true;
        }

        if (Interlocked.Exchange(ref _asking, 1) != 0)
        {
            return false; // already asking
        }

        try
        {
            string summary = AppText.Get(AppPhrase.ConsentSummary, _app.UiLang);
            ConfirmOutcome outcome = await _confirmer.ConfirmAsync(summary, Timeout, allowVoice: false, sayTimedOut: false, ct).ConfigureAwait(false);
            if (outcome == ConfirmOutcome.Accepted)
            {
                DateTimeOffset now = _app.Clock.UtcNow;
                _app.Settings.UpdateProfile(p => p with { CloudConsentGiven = true, CloudConsentAt = now });
                _app.Log.Event(LogKind.Confirmation, LogOutcome.Ok, tool: "cloud_consent");
                _app.SayNow(AppPhrase.ConsentGiven);
                return true;
            }

            _app.Log.Event(LogKind.Confirmation, outcome == ConfirmOutcome.TimedOut ? LogOutcome.TimedOut : LogOutcome.Declined, tool: "cloud_consent");
            _app.SayNow(AppPhrase.ConsentDeclined);
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            Volatile.Write(ref _asking, 0);
        }
    }
}
