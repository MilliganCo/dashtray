using System.Text.Json;

namespace DashTray;

internal static class SettingsStore
{
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DashTray");

    private static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(FilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            Normalize(settings, applyRuleDefaultsIfMissing: true);
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        Normalize(settings, applyRuleDefaultsIfMissing: false);
        Directory.CreateDirectory(DirectoryPath);

        var temporaryPath = FilePath + ".tmp";
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, FilePath, overwrite: true);
    }

    private static void Normalize(AppSettings settings, bool applyRuleDefaultsIfMissing)
    {
        settings.DoublePressIntervalMs = Math.Clamp(settings.DoublePressIntervalMs, 100, 1500);
        settings.ReplacementText = string.IsNullOrEmpty(settings.ReplacementText) ? "—" : settings.ReplacementText;
        settings.ExcludedProcesses ??= string.Empty;

        if (applyRuleDefaultsIfMissing)
        {
            settings.TypographyRules ??= AppSettings.CreateDefaultTypographyRules();
            settings.TemplateRules ??= AppSettings.CreateDefaultTemplateRules();
        }
        else
        {
            settings.TypographyRules ??= [];
            settings.TemplateRules ??= [];
        }

        settings.TypographyRules = SanitizeRules(settings.TypographyRules);
        settings.TemplateRules = SanitizeRules(settings.TemplateRules);
    }

    private static List<ReplacementRule> SanitizeRules(IEnumerable<ReplacementRule> rules)
    {
        return rules
            .Where(rule => rule is not null)
            .Select(rule => new ReplacementRule
            {
                Trigger = (rule.Trigger ?? string.Empty).Trim(),
                Replacement = rule.Replacement ?? string.Empty,
                IgnoreCase = rule.IgnoreCase
            })
            .Where(rule => rule.Trigger.Length > 0)
            .ToList();
    }
}
