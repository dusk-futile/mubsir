using System.Text;
using Basira.App.Native;

namespace Basira.App.Host;

/// <summary>
/// Console access for the command-line modes of a GUI-subsystem exe: attaches to the parent console (cmd or
/// PowerShell) when there is one. Secrets are read without echo and never printed.
/// </summary>
internal static class ConsoleIo
{
    private static bool s_attached;

    public static void Attach()
    {
        if (s_attached)
        {
            return;
        }

        // A GUI exe has no console of its own. Output redirected to a file or pipe works without attaching.
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
            // No console: nothing to print to.
        }
    }

    /// <summary>
    /// Reads one line without echo: key by key from an interactive console, or a line from redirected stdin
    /// (e.g. <c>type key.txt | Basira --set-key gemini</c>). Null when nothing could be read.
    /// </summary>
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
