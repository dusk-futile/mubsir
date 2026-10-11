using Mubsir.Core.Agent;
using Mubsir.Core.Common;
using Mubsir.Core.Config;

namespace Mubsir.App.Orchestration;

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

internal sealed class UnavailableToolExecutor : IToolExecutor
{
    public static readonly UnavailableToolExecutor Instance = new();

    public Task<PreparedAction> PrepareAsync(ToolCall call, Mubsir.Core.Policy.TaskContext task, CancellationToken ct) =>
        throw new NotSupportedException("tools_unavailable");

    public Task<ToolResult> ExecuteAsync(PreparedAction action, CancellationToken ct) =>
        Task.FromResult(new ToolResult(action.Call.Id, "error: tools are not available", IsError: true));

    public Task<ToolResult> HandOffAsync(PreparedAction action, CancellationToken ct) => ExecuteAsync(action, ct);

    public string Narration(PreparedAction action, Lang lang) => string.Empty;
}
