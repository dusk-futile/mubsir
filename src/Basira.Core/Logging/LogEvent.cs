namespace Basira.Core.Logging;

public enum LogKind
{
    Startup,
    Shutdown,
    Hotkey,
    Talk,
    Transcribe,
    BrainRequest,
    ToolCall,
    Confirmation,
    Policy,
    Speech,
    Error,
    Update,
    Budget,
    Metric,
}

public enum LogOutcome
{
    Ok,
    Cancelled,
    Blocked,
    Declined,
    Failed,
    TimedOut,
}

/// <summary>
/// One metadata-only log entry. By design there is no free-text field: <see cref="Tool"/>,
/// <see cref="ControlType"/>, <see cref="ErrorCode"/> and <see cref="Metric"/> accept only short
/// identifiers (see <see cref="LogIdentifier"/>), and anything else is stored as "invalid".
/// No transcripts, screen text, URLs or file names can reach the log through this type.
/// </summary>
public sealed record LogEvent(
    DateTimeOffset At,
    LogKind Kind,
    LogOutcome Outcome = LogOutcome.Ok,
    string? Tool = null,
    string? ControlType = null,
    long? LatencyMs = null,
    long? InputTokens = null,
    long? OutputTokens = null,
    decimal? CostUsd = null,
    string? ErrorCode = null,
    string? Metric = null,
    double? Value = null)
{
    private readonly string? _tool = LogIdentifier.Sanitize(Tool);
    private readonly string? _controlType = LogIdentifier.Sanitize(ControlType);
    private readonly string? _errorCode = LogIdentifier.Sanitize(ErrorCode);
    private readonly string? _metric = LogIdentifier.Sanitize(Metric);
    private readonly double? _value = Finite(Value);

    public string? Tool { get => _tool; init => _tool = LogIdentifier.Sanitize(value); }

    public string? ControlType { get => _controlType; init => _controlType = LogIdentifier.Sanitize(value); }

    public string? ErrorCode { get => _errorCode; init => _errorCode = LogIdentifier.Sanitize(value); }

    public string? Metric { get => _metric; init => _metric = LogIdentifier.Sanitize(value); }

    /// <summary>Metric value; NaN and infinities are dropped (JSON cannot hold them).</summary>
    public double? Value { get => _value; init => _value = Finite(value); }

    private static double? Finite(double? v) => v is { } d && double.IsFinite(d) ? d : null;
}

/// <summary>Validation for the identifier fields of <see cref="LogEvent"/>.</summary>
public static class LogIdentifier
{
    public const string Invalid = "invalid";
    public const int MaxLength = 64;

    /// <summary>Things that look like web addresses even without a scheme ("bank.example.com").</summary>
    private static readonly HashSet<string> DomainEndings = new(StringComparer.OrdinalIgnoreCase)
    {
        "com", "net", "org", "edu", "gov", "info", "io", "ai", "co", "me", "tv", "app", "dev", "uk", "eg", "sa",
        "ly", "ae", "ma", "dz", "tn", "jo", "lb", "sy", "iq", "kw", "qa", "bh", "om", "ye", "sd", "ps",
    };

    /// <summary>
    /// Matches <c>^[A-Za-z0-9_.:-]{1,64}$</c>, and is not a bare domain name or "www." address.
    /// </summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength)
        {
            return false;
        }

        foreach (char c in value)
        {
            bool ok = c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '.' or ':' or '-';
            if (!ok)
            {
                return false;
            }
        }

        if (value.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int lastDot = value.LastIndexOf('.');
        return lastDot < 0 || !DomainEndings.Contains(value[(lastDot + 1)..]);
    }

    /// <summary>Null stays null; a valid identifier is kept; anything else becomes <see cref="Invalid"/>.</summary>
    public static string? Sanitize(string? value) => value is null ? null : IsValid(value) ? value : Invalid;
}
