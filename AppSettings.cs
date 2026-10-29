namespace DashTray;

internal sealed class AppSettings
{
    public bool Enabled { get; set; } = true;
    public int DoublePressIntervalMs { get; set; } = 350;
    public bool IncludeNumpadMinus { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public bool SmartQuotesEnabled { get; set; } = true;
    public string ReplacementText { get; set; } = "—";
    public string ExcludedProcesses { get; set; } = string.Empty;
    public List<ReplacementRule> TypographyRules { get; set; } = CreateDefaultTypographyRules();
    public List<ReplacementRule> TemplateRules { get; set; } = CreateDefaultTemplateRules();

    public AppSettings Clone() => new()
    {
        Enabled = Enabled,
        DoublePressIntervalMs = DoublePressIntervalMs,
        IncludeNumpadMinus = IncludeNumpadMinus,
        StartWithWindows = StartWithWindows,
        SmartQuotesEnabled = SmartQuotesEnabled,
        ReplacementText = ReplacementText,
        ExcludedProcesses = ExcludedProcesses,
        TypographyRules = TypographyRules.Select(rule => rule.Clone()).ToList(),
        TemplateRules = TemplateRules.Select(rule => rule.Clone()).ToList()
    };

    public HashSet<string> GetExcludedProcessSet()
    {
        return ExcludedProcesses
            .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeProcessName)
            .Where(name => name.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static List<ReplacementRule> CreateDefaultTypographyRules() =>
    [
        new() { Trigger = "...", Replacement = "…" },
        new() { Trigger = "(c)", Replacement = "©", IgnoreCase = true },
        new() { Trigger = "(r)", Replacement = "®", IgnoreCase = true },
        new() { Trigger = "+-", Replacement = "±" },
        new() { Trigger = "1/2", Replacement = "½" }
    ];

    public static List<ReplacementRule> CreateDefaultTemplateRules() =>
    [
        new() { Trigger = ";mail", Replacement = "user@example.com" },
        new() { Trigger = ";company", Replacement = "Example Company" }
    ];

    public static string NormalizeProcessName(string value)
    {
        var name = Path.GetFileName(value.Trim());
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? name[..^4]
            : name;
    }
}
