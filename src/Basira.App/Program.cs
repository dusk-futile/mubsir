using Basira.App.Abstractions;
using Basira.App.Diagnostics;
using Basira.App.Host;
using Basira.App.Orchestration;
using Basira.Core.Common;

namespace Basira.App;

internal static class Program
{
    /// <summary>
    /// <c>Basira</c> runs the assistant; <c>--watch</c> supervises it; <c>--set-key &lt;anthropic|gemini&gt; [value]</c>
    /// and <c>--clear-key &lt;name&gt;</c> manage API keys; <c>--dev</c> enables the dev pipe; <c>--version</c> prints the version.
    /// </summary>
    [STAThread]
    private static int Main(string[] args)
    {
        long startMs = Environment.TickCount64;
        CommandLine cmd = CommandLine.Parse(args);
        switch (cmd.Mode)
        {
            case RunMode.Version:
                ConsoleIo.Attach();
                ConsoleIo.WriteLine("Basira " + SystemFacts.Version());
                return 0;
            case RunMode.SetKey:
                return KeyCommands.SetKey(cmd.KeyName!, cmd.KeyValue);
            case RunMode.ClearKey:
                return KeyCommands.ClearKey(cmd.KeyName!);
            case RunMode.Watch:
                return Supervisor.Run(cmd.ChildArgs ?? []);
            case RunMode.Invalid:
                ConsoleIo.Attach();
                ConsoleIo.WriteLine(cmd.Error ?? "invalid arguments");
                return 2;
        }

        using SingleInstance? single = SingleInstance.TryAcquire();
        if (single is null)
        {
            ConsoleIo.Attach();
            ConsoleIo.WriteLine("already running");
            EmergencyVoice.Speak(AppText.Get(AppPhrase.AlreadyRunning, Lang.Arabic), Lang.Arabic, NullEventLog.Instance, TimeSpan.FromSeconds(5));
            return 0;
        }

        AppPaths.EnsureCreated();
        return BasiraHost.Run(cmd, startMs);
    }
}
