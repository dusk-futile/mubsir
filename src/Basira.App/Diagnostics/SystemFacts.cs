using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using Basira.App.Native;

namespace Basira.App.Diagnostics;

/// <summary>Short machine facts for the diagnostics report: no user, PC or file names, no content.</summary>
internal static class SystemFacts
{
    public static IReadOnlyList<KeyValuePair<string, string>> Collect(Func<bool> nvdaRunning, Func<bool> hasAnthropicKey, Func<bool> hasGeminiKey, Func<bool> privateMode)
    {
        var facts = new List<KeyValuePair<string, string>>();
        void Add(string key, Func<string> value)
        {
            try
            {
                facts.Add(new(key, value()));
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                facts.Add(new(key, "error:" + e.GetType().Name));
            }
        }

        Add("basira_version", Version);
        Add("os", () => RuntimeInformation.OSDescription);
        Add("os_arch", () => RuntimeInformation.OSArchitecture.ToString());
        Add("process_arch", () => RuntimeInformation.ProcessArchitecture.ToString());
        Add("dotnet", () => RuntimeInformation.FrameworkDescription);
        Add("cpu_count", () => Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture));
        Add("ram_total_mb", () => NativeQueries.TotalMemoryMb()?.ToString(CultureInfo.InvariantCulture) ?? "unknown");
        Add("working_set_mb", () => (Environment.WorkingSet / (1024 * 1024)).ToString(CultureInfo.InvariantCulture));
        Add("uptime_min", () => (Environment.TickCount64 / 60_000).ToString(CultureInfo.InvariantCulture));
        Add("ui_culture", () => CultureInfo.CurrentUICulture.Name);
        Add("nvda_running", () => YesNo(nvdaRunning()));
        Add("anthropic_key_present", () => YesNo(hasAnthropicKey()));
        Add("gemini_key_present", () => YesNo(hasGeminiKey()));
        Add("private_mode", () => YesNo(privateMode()));
        return facts;
    }

    public static string Version() =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        ?? "0.0.0";

    private static string YesNo(bool value) => value ? "yes" : "no";
}
