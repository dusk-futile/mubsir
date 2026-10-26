namespace Mubsir.App.Host;

internal enum RunMode
{
    Run,
    Watch,
    SetKey,
    ClearKey,
    Version,
    Invalid,
}

internal sealed record CommandLine(
    RunMode Mode,
    string? KeyName = null,
    string? KeyValue = null,
    bool Recovered = false,
    IReadOnlyList<string>? ChildArgs = null,
    string? Error = null)
{
    public const string WatchFlag = "--watch";
    public const string RecoveredFlag = "--recovered";

    public static readonly IReadOnlySet<string> KeyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "anthropic", "gemini" };

    public static CommandLine Parse(IReadOnlyList<string> args)
    {
        bool recovered = args.Any(a => Is(a, RecoveredFlag));
        if (args.Any(a => Is(a, "--version")))
        {
            return new CommandLine(RunMode.Version);
        }

        if (args.Any(a => Is(a, WatchFlag)))
        {
            string[] child = args.Where(a => !Is(a, WatchFlag) && !Is(a, RecoveredFlag)).ToArray();
            return new CommandLine(RunMode.Watch, ChildArgs: child);
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

        string? unknown = args.FirstOrDefault(a => !Is(a, RecoveredFlag));
        return unknown is null
            ? new CommandLine(RunMode.Run, Recovered: recovered)
            : new CommandLine(RunMode.Invalid, Error: "unknown argument");
    }

    public override string ToString() => "CommandLine { Mode = " + Mode + ", Recovered = " + Recovered + " }";

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
