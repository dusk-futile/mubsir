using Mubsir.Core.Common;
using Mubsir.Core.Logging;

namespace Mubsir.App.Host;

internal static class KeyCommands
{
    public static int SetKey(string name, string? value)
    {
        ConsoleIo.Attach();
        value ??= ConsoleIo.ReadSecret("Paste the " + name + " key and press Enter (it will not be shown): ");
        if (string.IsNullOrWhiteSpace(value))
        {
            ConsoleIo.WriteLine("No key given; nothing saved.");
            return 2;
        }

        using var log = OpenLog();
        try
        {
            new SecretStore(log).Set(name, value.Trim());
            log.Event(LogKind.Startup, tool: "set_key_" + name);
            ConsoleIo.WriteLine("saved");
            return 0;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            log.Failure("set_key", e);
            ConsoleIo.WriteLine("Could not save the key (" + e.GetType().Name + ").");
            return 1;
        }
    }

    public static int ClearKey(string name)
    {
        ConsoleIo.Attach();
        using var log = OpenLog();
        try
        {
            new SecretStore(log).Set(name, null);
            log.Event(LogKind.Startup, tool: "clear_key_" + name);
            ConsoleIo.WriteLine("cleared");
            return 0;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            log.Failure("clear_key", e);
            ConsoleIo.WriteLine("Could not clear the key (" + e.GetType().Name + ").");
            return 1;
        }
    }

    private static FileEventLog OpenLog()
    {
        AppPaths.EnsureCreated();
        return new FileEventLog(AppPaths.LogsDir, SystemClock.Instance);
    }
}
