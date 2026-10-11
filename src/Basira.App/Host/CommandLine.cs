namespace Basira.App.Host;

internal enum RunMode
{
    Run,
    Watch,
    SetKey,
    ClearKey,
    Version,
    Invalid,
}

/// <summary>Parsed command line (pure, testable).</summary>
/// <param name="KeyName">For --set-key / --clear-key: "anthropic" or "gemini".</param>
/// <param name="KeyValue">For --set-key: the value when given on the command line (otherwise read from stdin).</param>
/// <param name="Dev">--dev: enable the dev pipe.</param>
/// <param name="Recovered">--recovered: started by the supervisor after a crash.</param>
/// <param name="ChildArgs">For --watch: the arguments passed on to the app.</param>
internal sealed record CommandLine(
    RunMode Mode,
    string? KeyName = null,
    string? KeyValue = null,
    bool Dev = false,
    bool Recovered = false,
    IReadOnlyList<string>? ChildArgs = null,
    string? Error = null)
{
    public const string WatchFlag = "--watch";
    public const string DevFlag = "--dev";
    public const string RecoveredFlag = "--recovered";

    public static readonly IReadOnlySet<string> KeyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "anthropic", "gemini" };

    public static CommandLine Parse(IReadOnlyList<string> args)
    {
        bool dev = args.Any(a => Is(a, DevFlag));
        bool recovered = args.Any(a => Is(a, RecoveredFlag));
        if (args.Any(a => Is(a, "--version")))
        {
            return new CommandLine(RunMode.Version);
        }

        if (args.Any(a => Is(a, WatchFlag)))
        {
            string[] child = args.Where(a => !Is(a, WatchFlag) && !Is(a, RecoveredFlag)).ToArray();
            return new CommandLine(RunMode.Watch, Dev: dev, ChildArgs: child);
        }

        int set = IndexOf(args, "--set-key");
        if (set >= 0)
        {
            string? name = set + 1 < args.Count ? args[set + 1] : null;
            if (name is null || !KeyNames.Contains(name))
            {
                return new CommandLine(RunMode.Invalid, Error: "usage: --set-key <anthropic|gemini> [value]");
            }

            string? value = set + 2 < args.Count && !args[set + 2].StartsWith("--", StringComparison.Ordinal) ? args[set + 2] : null;
            return new CommandLine(RunMode.SetKey, KeyName: name.ToLowerInvariant(), KeyValue: value);
        }

        int clear = IndexOf(args, "--clear-key");
        if (clear >= 0)
        {
            string? name = clear + 1 < args.Count ? args[clear + 1] : null;
            if (name is null || !KeyNames.Contains(name))
            {
                return new CommandLine(RunMode.Invalid, Error: "usage: --clear-key <anthropic|gemini>");
            }

            return new CommandLine(RunMode.ClearKey, KeyName: name.ToLowerInvariant());
        }

        string? unknown = args.FirstOrDefault(a => !Is(a, DevFlag) && !Is(a, RecoveredFlag));
        return unknown is null
            ? new CommandLine(RunMode.Run, Dev: dev, Recovered: recovered)
            : new CommandLine(RunMode.Invalid, Error: "unknown argument");
    }

    /// <summary>Never prints <see cref="KeyValue"/>.</summary>
    public override string ToString() => "CommandLine { Mode = " + Mode + ", Dev = " + Dev + ", Recovered = " + Recovered + " }";

    private static bool Is(string arg, string flag) => string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase);

    private static int IndexOf(IReadOnlyList<string> args, string flag)
    {
        for (int i = 0; i < args.Count; i++)
        {
            if (Is(args[i], flag))
            {
                return i;
            }
        }

        return -1;
    }
}
