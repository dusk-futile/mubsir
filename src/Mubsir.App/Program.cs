using Mubsir.App.Host;
using Mubsir.Core.Common;

namespace Mubsir.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        long startMs = Environment.TickCount64;
        CommandLine cmd = CommandLine.Parse(args);
        switch (cmd.Mode)
        {
            case RunMode.Version:
                ConsoleIo.Attach();
                ConsoleIo.WriteLine("Mubsir " + SystemFacts.Version());
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
        return MubsirHost.Run(cmd, startMs);
    }
}
