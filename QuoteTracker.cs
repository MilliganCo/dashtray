namespace DashTray;

internal sealed class QuoteTracker
{
    private IntPtr _window;
    private int _depth;

    public void Reset()
    {
        _window = IntPtr.Zero;
        _depth = 0;
    }

    public char NextQuote(IntPtr window, string bufferBeforeQuote)
    {
        if (window != _window)
        {
            _window = window;
            _depth = 0;
        }

        var opening = ShouldOpen(bufferBeforeQuote);
        if (opening)
        {
            var quote = _depth == 0 ? '«' : '„';
            _depth++;
            return quote;
        }

        var closing = _depth <= 1 ? '»' : '“';
        if (_depth > 0)
        {
            _depth--;
        }

        return closing;
    }

    private static bool ShouldOpen(string bufferBeforeQuote)
    {
        if (string.IsNullOrEmpty(bufferBeforeQuote))
        {
            return true;
        }

        var previous = bufferBeforeQuote[^1];
        return char.IsWhiteSpace(previous)
            || previous is '(' or '[' or '{' or '«' or '„' or '—' or '-' or '–';
    }
}
