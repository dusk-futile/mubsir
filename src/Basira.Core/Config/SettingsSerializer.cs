using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Basira.Core.Common;
using Basira.Core.Input;

namespace Basira.Core.Config;

/// <summary>
/// JSON for <see cref="BasiraSettings"/> and <see cref="UserProfile"/> (source-generated, AOT-safe).
/// Reading is tolerant: unknown fields are ignored, missing fields keep their defaults and
/// out-of-range values are brought back into range. Malformed JSON throws <see cref="JsonException"/>.
/// </summary>
public static class SettingsSerializer
{
    private static readonly ConfigJsonContext Context = new(new JsonSerializerOptions(ConfigJsonContext.Default.Options)
    {
        // Arabic names and tasks stay readable in the file.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });

    public static string Serialize(BasiraSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return JsonSerializer.Serialize(settings, Context.BasiraSettings);
    }

    public static string Serialize(UserProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return JsonSerializer.Serialize(profile, Context.UserProfile);
    }

    /// <summary>Reads settings; empty input gives the defaults.</summary>
    public static BasiraSettings DeserializeSettings(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new BasiraSettings();
        }

        BasiraSettings? settings = JsonSerializer.Deserialize(json, Context.BasiraSettings);
        return Normalize(settings ?? new BasiraSettings());
    }

    /// <summary>Reads a profile; empty input gives an empty profile.</summary>
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

    /// <summary>
    /// Reads settings without throwing: false (and the defaults) when <paramref name="json"/> is
    /// malformed. Missing or empty input is not an error.
    /// </summary>
    public static bool TryDeserializeSettings(string? json, out BasiraSettings settings)
    {
        try
        {
            settings = DeserializeSettings(json ?? string.Empty);
            return true;
        }
        catch (JsonException)
        {
            settings = new BasiraSettings();
            return false;
        }
    }

    /// <summary>Reads a profile without throwing: false (and an empty profile) when <paramref name="json"/> is malformed.</summary>
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

    /// <summary>Replaces nulls and out-of-range values with safe ones.</summary>
    public static BasiraSettings Normalize(BasiraSettings s)
    {
        ArgumentNullException.ThrowIfNull(s);
        return s with
        {
            BrainModel = string.IsNullOrWhiteSpace(s.BrainModel) ? BasiraSettings.DefaultBrainModel : s.BrainModel.Trim(),
            EscalationModel = string.IsNullOrWhiteSpace(s.EscalationModel) ? null : s.EscalationModel.Trim(),
            EarsModel = string.IsNullOrWhiteSpace(s.EarsModel) ? BasiraSettings.DefaultEarsModel : s.EarsModel.Trim(),
            SpeechRate = Math.Clamp(s.SpeechRate, 0, 100),
            Verbosity = Enum.IsDefined(s.Verbosity) ? s.Verbosity : Verbosity.Brief,
            TalkKey = s.TalkKey.IsEmpty ? KeyIdentity.RightCtrl : s.TalkKey,
            BudgetSoftCapUsd = Math.Max(0m, s.BudgetSoftCapUsd),
            BankDomains = s.BankDomains is null ? BankDomainDefaults.Create() : CleanDomains(s.BankDomains),
            NvdaLangTagStyle = string.IsNullOrWhiteSpace(s.NvdaLangTagStyle) ? BasiraSettings.DefaultNvdaLangTagStyle : s.NvdaLangTagStyle.Trim(),
            AgentMaxSteps = s.AgentMaxSteps > 0 ? s.AgentMaxSteps : BasiraSettings.DefaultAgentMaxSteps,
            AgentMaxSeconds = s.AgentMaxSeconds > 0 ? s.AgentMaxSeconds : BasiraSettings.DefaultAgentMaxSeconds,
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
[JsonSerializable(typeof(BasiraSettings))]
[JsonSerializable(typeof(UserProfile))]
internal sealed partial class ConfigJsonContext : JsonSerializerContext;
