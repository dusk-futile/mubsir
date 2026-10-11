using System.Text;
using Mubsir.App.Native;

namespace Mubsir.App.Host;

internal static class ConsoleIo
{
    private static bool s_attached;

    public static void Attach()
    {
        if (s_attached)
        {
            return;
        }

        s_attached = Kernel32.AttachConsole(Win32.ATTACH_PARENT_PROCESS);
        if (s_attached && !Console.IsOutputRedirected)
        {
            var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
            Console.SetOut(stdout);
            Console.WriteLine();
        }
    }

    public static void WriteLine(string text)
    {
        try
        {
            Console.Out.WriteLine(text);
        }
        catch (IOException)
        {
        }
    }

    public static string? ReadSecret(string prompt)
    {
        try
        {
            if (Console.IsInputRedirected)
            {
                return Console.In.ReadLine()?.Trim();
            }

            Console.Out.Write(prompt);
            var sb = new StringBuilder();
            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    break;
                }

                if (key.Key == ConsoleKey.Escape)
                {
                    sb.Clear();
                    break;
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (sb.Length > 0)
                    {
                        sb.Length--;
                    }

                    continue;
                }

                if (!char.IsControl(key.KeyChar))
                {
                    sb.Append(key.KeyChar);
                }
            }

            Console.Out.WriteLine();
            string value = sb.ToString().Trim();
            sb.Clear();
            return value.Length > 0 ? value : null;
        }
        catch (Exception e) when (e is IOException or InvalidOperationException)
        {
            return null;
        }
    }
}
