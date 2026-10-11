namespace Basira.App.Orchestration;

/// <summary>
/// TODO(M2): the spoken setup interview (name, reply style, speed, studies and work, apps, top-10 tasks, talk key by
/// pressing it, privacy summary and consent) and the short repeatable lessons ("teach me ..."). This is the entry
/// point; for now it only says that it is coming.
/// </summary>
internal sealed class Onboarding
{
    private readonly AppServices _app;

    public Onboarding(AppServices app)
    {
        _app = app;
    }

    /// <summary>Whether the interview has run for this profile. Always false until the interview exists.</summary>
    public bool Completed => false;

    /// <summary>Runs the interview. Placeholder: announces that it is coming in a later version.</summary>
    public Task RunAsync(CancellationToken ct)
    {
        _app.Say(AppPhrase.OnboardingComingSoon);
        return Task.CompletedTask;
    }
}
