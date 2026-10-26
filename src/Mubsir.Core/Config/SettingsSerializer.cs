using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mubsir.Core.Common;
using Mubsir.Core.Input;

namespace Mubsir.Core.Config;

public static class SettingsSerializer
{
    private static readonly ConfigJsonContext Context = new(new JsonSerializerOptions(ConfigJsonContext.Default.Options)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });

    public static string Serialize(MubsirSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return JsonSerializer.Serialize(settings, Context.MubsirSettings);
    }

    public static string Serialize(UserProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return JsonSerializer.Serialize(profile, Context.UserProfile);
    }

    public static MubsirSettings DeserializeSettings(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new MubsirSettings();
        }

        MubsirSettings? settings = JsonSerializer.Deserialize(json, Context.MubsirSettings);
        return Normalize(settings ?? new MubsirSettings());
    }

    public static UserProfile DeserializeProfile(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new UserProfile();
        }

        UserProfile? profile = JsonSerializer.Deserialize(json, Context.UserProfile);
        return Normalize(profile ?? new UserProfile());
    }

    public static bool TryDeserializeSettings(string? json, out MubsirSettings settings)
    {
        try
        {
            settings = DeserializeSettings(json ?? string.Empty);
            return true;
        }
        catch (JsonException)
        {
            settings = new MubsirSettings();
            return false;
        }
    }

    public static bool TryDeserializeProfile(string? json, out UserProfile profile)
    {
        try
        {
            profile = DeserializeProfile(json ?? string.Empty);
            return true;
        }
        catch (JsonException)
        {
            profile = new UserProfile();
            return false;
        }
    }

    public static MubsirSettings Normalize(MubsirSettings s)
    {
        ArgumentNullException.ThrowIfNull(s);
        return s with
        {
            BrainModel = string.IsNullOrWhiteSpace(s.BrainModel) ? MubsirSettings.DefaultBrainModel : s.BrainModel.Trim(),
            EscalationModel = string.IsNullOrWhiteSpace(s.EscalationModel) ? null : s.EscalationModel.Trim(),
            EarsModel = string.IsNullOrWhiteSpace(s.EarsModel) ? MubsirSettings.DefaultEarsModel : s.EarsModel.Trim(),
            SpeechRate = Math.Clamp(s.SpeechRate, 0, 100),
            Verbosity = Enum.IsDefined(s.Verbosity) ? s.Verbosity : Verbosity.Brief,
            TalkKey = s.TalkKey.IsEmpty ? KeyIdentity.RightCtrl : s.TalkKey,
            BudgetSoftCapUsd = Math.Max(0m, s.BudgetSoftCapUsd),
            BankDomains = s.BankDomains is null ? BankDomainDefaults.Create() : CleanDomains(s.BankDomains),
            NvdaLangTagStyle = string.IsNullOrWhiteSpace(s.NvdaLangTagStyle) ? MubsirSettings.DefaultNvdaLangTagStyle : s.NvdaLangTagStyle.Trim(),
            AgentMaxSteps = s.AgentMaxSteps > 0 ? s.AgentMaxSteps : MubsirSettings.DefaultAgentMaxSteps,
            AgentMaxSeconds = s.AgentMaxSeconds > 0 ? s.AgentMaxSeconds : MubsirSettings.DefaultAgentMaxSeconds,
        };
    }

    public static UserProfile Normalize(UserProfile p)
    {
        ArgumentNullException.ThrowIfNull(p);
        return p with
        {
            PreferredLanguage = Enum.IsDefined(p.PreferredLanguage) ? p.PreferredLanguage : Lang.Arabic,
            ReplyStyle = Enum.IsDefined(p.ReplyStyle) ? p.ReplyStyle : ReplyStyle.SimpleMsa,
            Apps = CleanList(p.Apps),
            TopTasks = CleanList(p.TopTasks),
        };
    }

    private static List<string> CleanList(List<string>? items) =>
        items is null ? [] : items.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).ToList();

    private static List<string> CleanDomains(List<string> domains) => domains
        .Where(d => !string.IsNullOrWhiteSpace(d))
        .Select(d => d.Trim().TrimEnd('.').ToLowerInvariant())
        .Distinct(StringComparer.Ordinal)
        .ToList();
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true,
    WriteIndented = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(MubsirSettings))]
[JsonSerializable(typeof(UserProfile))]
internal sealed partial class ConfigJsonContext : JsonSerializerContext;
