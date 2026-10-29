using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DashTray;

internal sealed class KeyboardHook : IDisposable
{
    private readonly Func<AppSettings> _getSettings;
    private readonly Func<bool> _isPaused;
    private readonly Action _togglePause;
    private readonly Action _onPauseChanged;
    private readonly ReplacementEngine _engine;
    private readonly QuoteTracker _quotes = new();
    private readonly NativeMethods.LowLevelKeyboardProc _callback;
    private IntPtr _hookHandle;

    private bool _minusIsDown;
    private bool _suppressCurrentMinusKeyUp;
    private long _firstMinusTimestamp;
    private IntPtr _firstMinusWindow;
    private bool _awaitingEmDashUndo;
    private long _emDashTimestamp;
    private IntPtr _emDashWindow;
    private int _emDashLength = 1;
    private IntPtr _bufferWindow;

    public KeyboardHook(
        Func<AppSettings> getSettings,
        Func<bool> isPaused,
        Action togglePause,
        Action onPauseChanged,
        ReplacementEngine engine)
    {
        _getSettings = getSettings;
        _isPaused = isPaused;
        _togglePause = togglePause;
        _onPauseChanged = onPauseChanged;
        _engine = engine;
        _callback = HookCallback;

        var moduleHandle = NativeMethods.GetModuleHandle(null);
        _hookHandle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhKeyboardLl,
            _callback,
            moduleHandle,
            0);

        if (_hookHandle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось установить глобальный перехват клавиатуры.");
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        var message = wParam.ToInt32();
        var isKeyDown = message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown;
        var isKeyUp = message is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp;

        if (!isKeyDown && !isKeyUp)
        {
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        var data = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);
        if ((data.Flags & NativeMethods.KbdLlFlags.Injected) != 0)
        {
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        var settings = _getSettings();
        var isMinus = data.VkCode == NativeMethods.VkOemMinus
            || (settings.IncludeNumpadMinus && data.VkCode == NativeMethods.VkSubtract);

        if (isKeyUp && isMinus)
        {
            _minusIsDown = false;
            if (_suppressCurrentMinusKeyUp)
            {
                _suppressCurrentMinusKeyUp = false;
                return new IntPtr(1);
            }

            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        if (!isKeyDown)
        {
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        if (isMinus && IsPauseHotkey())
        {
            ResetAllSequences();
            _togglePause();
            _onPauseChanged();
            _suppressCurrentMinusKeyUp = true;
            return new IntPtr(1);
        }

        if (_isPaused() || !settings.Enabled)
        {
            ResetAllSequences();
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        var foregroundWindow = NativeMethods.GetForegroundWindow();
        ForegroundTracker.Observe(foregroundWindow);
        EnsureWindowContext(foregroundWindow);

        if (foregroundWindow == IntPtr.Zero || IsExcludedProcess(foregroundWindow, settings))
        {
            ResetAllSequences();
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        if (IsNavigationOrEditKey(data.VkCode))
        {
            HandleEditKey(data.VkCode);
            ResetDashState(clearUndo: true);
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        if (AnyCommandModifierIsDown())
        {
            ResetAllSequences();
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        // Shift+minus is underscore (or another layout glyph) — do not treat as dash double-press.
        if (isMinus && !IsDown(NativeMethods.VkShift))
        {
            return HandleMinusKey(nCode, wParam, lParam, settings, foregroundWindow);
        }

        var text = KeyConverter.ToText(data.VkCode, data.ScanCode);
        if (string.IsNullOrEmpty(text))
        {
            if (!IsModifierKey(data.VkCode))
            {
                ResetDashState(clearUndo: true);
            }

            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        ResetDashState(clearUndo: false);

        if (settings.SmartQuotesEnabled && KeyConverter.IsStraightDoubleQuote(text))
        {
            return HandleSmartQuote(nCode, wParam, lParam, foregroundWindow);
        }

        if (_engine.TryMatch(text, out var alreadyTyped, out var replacement, out _))
        {
            try
            {
                InputSender.ReplaceCharacters(alreadyTyped, replacement);
                _engine.CommitReplacement(alreadyTyped, replacement);
                _awaitingEmDashUndo = false;
                return new IntPtr(1);
            }
            catch
            {
                _engine.Append(text);
                return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
            }
        }

        _engine.Append(text);
        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private IntPtr HandleMinusKey(int nCode, IntPtr wParam, IntPtr lParam, AppSettings settings, IntPtr foregroundWindow)
    {
        if (_minusIsDown)
        {
            ResetDashState(clearUndo: true);
            _engine.Reset();
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        _minusIsDown = true;
        var now = Environment.TickCount64;

        if (_awaitingEmDashUndo
            && foregroundWindow == _emDashWindow
            && now - _emDashTimestamp >= 0
            && now - _emDashTimestamp <= settings.DoublePressIntervalMs)
        {
            try
            {
                InputSender.ReplaceCharacters(_emDashLength, "--");
                _engine.Reset();
                _engine.Append("--");
                _awaitingEmDashUndo = false;
                _suppressCurrentMinusKeyUp = true;
                ResetPendingMinus();
                return new IntPtr(1);
            }
            catch
            {
                _awaitingEmDashUndo = false;
                return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
            }
        }

        const string minusText = "-";
        if (_engine.TryMatch(minusText, out var alreadyTyped, out var replacement, out _))
        {
            try
            {
                InputSender.ReplaceCharacters(alreadyTyped, replacement);
                _engine.CommitReplacement(alreadyTyped, replacement);
                _awaitingEmDashUndo = false;
                _suppressCurrentMinusKeyUp = true;
                ResetPendingMinus();
                return new IntPtr(1);
            }
            catch
            {
                _engine.Append(minusText);
                return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
            }
        }

        var elapsed = now - _firstMinusTimestamp;
        var isSecondPress = _firstMinusTimestamp != 0
            && elapsed >= 0
            && elapsed <= settings.DoublePressIntervalMs
            && foregroundWindow == _firstMinusWindow;

        if (!isSecondPress)
        {
            _firstMinusTimestamp = now;
            _firstMinusWindow = foregroundWindow;
            _awaitingEmDashUndo = false;
            _engine.Append(minusText);
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        ResetPendingMinus();

        try
        {
            InputSender.ReplaceCharacters(1, settings.ReplacementText);
            if (_engine.Buffer.EndsWith('-'))
            {
                _engine.TrimLast();
            }

            _engine.Append(settings.ReplacementText);
            _awaitingEmDashUndo = true;
            _emDashTimestamp = now;
            _emDashWindow = foregroundWindow;
            _emDashLength = Math.Max(1, settings.ReplacementText.Length);
            _suppressCurrentMinusKeyUp = true;
            return new IntPtr(1);
        }
        catch
        {
            _engine.Append(minusText);
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }
    }

    private IntPtr HandleSmartQuote(int nCode, IntPtr wParam, IntPtr lParam, IntPtr foregroundWindow)
    {
        try
        {
            var quote = _quotes.NextQuote(foregroundWindow, _engine.Buffer).ToString();
            InputSender.ReplaceCharacters(0, quote);
            _engine.Append(quote);
            _awaitingEmDashUndo = false;
            return new IntPtr(1);
        }
        catch
        {
            _engine.Append("\"");
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }
    }

    private void EnsureWindowContext(IntPtr foregroundWindow)
    {
        if (foregroundWindow == _bufferWindow)
        {
            return;
        }

        _bufferWindow = foregroundWindow;
        _engine.Reset();
        _quotes.Reset();
        ResetDashState(clearUndo: true);
    }

    private void HandleEditKey(uint virtualKey)
    {
        if (virtualKey == NativeMethods.VkBack)
        {
            _engine.TrimLast();
            return;
        }

        _engine.Reset();
        _quotes.Reset();
    }

    private void ResetAllSequences()
    {
        _engine.Reset();
        _quotes.Reset();
        ResetDashState(clearUndo: true);
        _minusIsDown = false;
    }

    private void ResetDashState(bool clearUndo)
    {
        ResetPendingMinus();
        if (clearUndo)
        {
            _awaitingEmDashUndo = false;
        }
    }

    private void ResetPendingMinus()
    {
        _firstMinusTimestamp = 0;
        _firstMinusWindow = IntPtr.Zero;
    }

    private static bool IsPauseHotkey()
    {
        return IsDown(NativeMethods.VkControl)
            && IsDown(NativeMethods.VkMenu)
            && !IsDown(NativeMethods.VkShift)
            && !IsDown(NativeMethods.VkLwin)
            && !IsDown(NativeMethods.VkRwin);
    }

    private static bool IsNavigationOrEditKey(uint virtualKey)
    {
        return virtualKey is NativeMethods.VkBack
            or NativeMethods.VkTab
            or NativeMethods.VkReturn
            or NativeMethods.VkEscape
            or NativeMethods.VkDelete
            or NativeMethods.VkLeft
            or NativeMethods.VkUp
            or NativeMethods.VkRight
            or NativeMethods.VkDown;
    }

    private static bool IsModifierKey(uint virtualKey)
    {
        return virtualKey is NativeMethods.VkShift
            or NativeMethods.VkControl
            or NativeMethods.VkMenu
            or NativeMethods.VkLwin
            or NativeMethods.VkRwin;
    }

    private static bool AnyCommandModifierIsDown()
    {
        // Shift alone is allowed (capitals / symbols). Ctrl/Alt/Win cancel sequences.
        return IsDown(NativeMethods.VkControl)
            || IsDown(NativeMethods.VkMenu)
            || IsDown(NativeMethods.VkLwin)
            || IsDown(NativeMethods.VkRwin);
    }

    private static bool IsDown(uint virtualKey)
    {
        return (NativeMethods.GetAsyncKeyState((int)virtualKey) & 0x8000) != 0;
    }

    private static bool IsExcludedProcess(IntPtr foregroundWindow, AppSettings settings)
    {
        var exclusions = settings.GetExcludedProcessSet();
        if (exclusions.Count == 0)
        {
            return false;
        }

        try
        {
            NativeMethods.GetWindowThreadProcessId(foregroundWindow, out var processId);
            if (processId == 0)
            {
                return false;
            }

            using var process = Process.GetProcessById((int)processId);
            return exclusions.Contains(process.ProcessName);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
        GC.SuppressFinalize(this);
    }
}
