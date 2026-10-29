namespace DashTray;

internal sealed class ReplacementEngine
{
    private const int MaxBufferLength = 32;

    private CompiledRule[] _rules = [];
    private string _buffer = string.Empty;

    public string Buffer => _buffer;

    public void UpdateRules(AppSettings settings)
    {
        _rules = settings.TypographyRules
            .Concat(settings.TemplateRules)
            .Where(rule => !string.IsNullOrEmpty(rule.Trigger))
            .Select(rule => new CompiledRule(
                rule.Trigger,
                rule.Replacement ?? string.Empty,
                rule.IgnoreCase))
            .OrderByDescending(rule => rule.Trigger.Length)
            .ToArray();
    }

    public void Reset() => _buffer = string.Empty;

    public void TrimLast()
    {
        if (_buffer.Length > 0)
        {
            _buffer = _buffer[..^1];
        }
    }

    public void Append(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        _buffer += text;
        if (_buffer.Length > MaxBufferLength)
        {
            _buffer = _buffer[^MaxBufferLength..];
        }
    }

    public bool TryMatch(string nextText, out int alreadyTypedCount, out string replacement, out int triggerLength)
    {
        alreadyTypedCount = 0;
        replacement = string.Empty;
        triggerLength = 0;

        if (string.IsNullOrEmpty(nextText) || _rules.Length == 0)
        {
            return false;
        }

        var candidate = _buffer + nextText;
        if (candidate.Length > MaxBufferLength)
        {
            candidate = candidate[^MaxBufferLength..];
        }

        foreach (var rule in _rules)
        {
            if (candidate.Length < rule.Trigger.Length || nextText.Length > rule.Trigger.Length)
            {
                continue;
            }

            var suffix = candidate[^rule.Trigger.Length..];
            var matched = rule.IgnoreCase
                ? suffix.Equals(rule.Trigger, StringComparison.OrdinalIgnoreCase)
                : suffix.Equals(rule.Trigger, StringComparison.Ordinal);

            if (!matched)
            {
                continue;
            }

            alreadyTypedCount = rule.Trigger.Length - nextText.Length;
            if (alreadyTypedCount < 0 || alreadyTypedCount > _buffer.Length)
            {
                continue;
            }

            replacement = rule.Replacement;
            triggerLength = rule.Trigger.Length;
            return true;
        }

        return false;
    }

    public void CommitReplacement(int alreadyTypedCount, string replacement)
    {
        if (alreadyTypedCount > _buffer.Length)
        {
            alreadyTypedCount = _buffer.Length;
        }

        _buffer = _buffer[..^alreadyTypedCount] + replacement;
        if (_buffer.Length > MaxBufferLength)
        {
            _buffer = _buffer[^MaxBufferLength..];
        }
    }

    private readonly record struct CompiledRule(string Trigger, string Replacement, bool IgnoreCase);
}
