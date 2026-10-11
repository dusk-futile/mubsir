using Basira.Core.Agent;
using Basira.Core.Common;
using Basira.Core.Config;

namespace Basira.App.Orchestration;

/// <summary>The profile facts that go into the brain's cached profile block (pure, testable).</summary>
internal static class ProfileFields
{
    public static IReadOnlyDictionary<string, string> From(UserProfile profile)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        void Add(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                fields[key] = value.Trim();
            }
        }

        Add("name", profile.Name);
        Add("preferred_language", profile.PreferredLanguage == Lang.Arabic ? "Arabic" : "English");
        Add("dialect", profile.Dialect);
        Add("reply_style", profile.ReplyStyle == ReplyStyle.Dialect ? "dialect" : "simple MSA");
        Add("studies", profile.Studies);
        Add("work", profile.Work);
        Add("needs", profile.Needs);
        if (profile.TopTasks is { Count: > 0 } tasks)
        {
            Add("top_tasks", string.Join("; ", tasks.Where(t => !string.IsNullOrWhiteSpace(t))));
        }

        return fields;
    }
}

/// <summary>Stands in for the executor when the actions module is missing: quick answers never call tools.</summary>
internal sealed class UnavailableToolExecutor : IToolExecutor
{
    public static readonly UnavailableToolExecutor Instance = new();

    public Task<PreparedAction> PrepareAsync(ToolCall call, Basira.Core.Policy.TaskContext task, CancellationToken ct) =>
        throw new NotSupportedException("tools_unavailable");

    public Task<ToolResult> ExecuteAsync(PreparedAction action, CancellationToken ct) =>
        Task.FromResult(new ToolResult(action.Call.Id, "error: tools are not available", IsError: true));

    public Task<ToolResult> HandOffAsync(PreparedAction action, CancellationToken ct) => ExecuteAsync(action, ct);

    public string Narration(PreparedAction action, Lang lang) => string.Empty;
}
