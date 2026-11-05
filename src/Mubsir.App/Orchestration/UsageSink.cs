using Mubsir.Core.Agent;
using Mubsir.Core.Common;
using Mubsir.Core.Localization;
using Mubsir.Core.Logging;

namespace Mubsir.App.Orchestration;

internal sealed class UsageSink : IUsageSink
{
    private readonly AppServices _app;

    public UsageSink(AppServices app)
    {
        _app = app;
    }

    public void Report(UsageRecord usage)
    {
        if (usage is null)
        {
            return;
        }

        try
        {
            decimal cost = _app.Ledger.Add(usage);
            _app.SaveCost();
            _app.Log.Write(new LogEvent(
                _app.Clock.UtcNow,
                LogKind.BrainRequest,
                Tool: ModelId(usage.Model),
                InputTokens: usage.InputTokens + usage.CacheReadTokens + usage.CacheWriteTokens + usage.AudioInputTokens,
                OutputTokens: usage.OutputTokens,
                CostUsd: cost));

            if (_app.Ledger.TryClaimWarningAnnouncement())
            {
                _app.SaveCost();
                _app.Say(StringKey.BudgetWarning80, _app.Ledger.MonthToDateUsd);
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _app.Log.Failure("usage_report", e);
        }
    }

    private static string ModelId(string? model) =>
    LogIdentifier.IsValid(model) ? model! : "model";
}
