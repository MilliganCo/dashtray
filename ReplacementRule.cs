namespace DashTray;

internal sealed class ReplacementRule
{
    public string Trigger { get; set; } = string.Empty;
    public string Replacement { get; set; } = string.Empty;
    public bool IgnoreCase { get; set; }

    public ReplacementRule Clone() => new()
    {
        Trigger = Trigger,
        Replacement = Replacement,
        IgnoreCase = IgnoreCase
    };
}
