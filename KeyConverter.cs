using System.Text;

namespace DashTray;

internal static class KeyConverter
{
    private static readonly StringBuilder Buffer = new(8);
    private static readonly byte[] KeyState = new byte[256];

    public static string? ToText(uint virtualKey, uint scanCode)
    {
        if (!NativeMethods.GetKeyboardState(KeyState))
        {
            return null;
        }

        // LL-hook runs before the thread key state includes this key.
        KeyState[virtualKey & 0xFF] |= 0x80;
        SyncModifier(NativeMethods.VkShift, NativeMethods.VkLshift, NativeMethods.VkRshift);
        SyncModifier(NativeMethods.VkControl, NativeMethods.VkLcontrol, NativeMethods.VkRcontrol);
        SyncModifier(NativeMethods.VkMenu, NativeMethods.VkLmenu, NativeMethods.VkRmenu);
        SyncSingle(NativeMethods.VkLwin);
        SyncSingle(NativeMethods.VkRwin);

        // Avoid translating shortcuts into control characters.
        if ((KeyState[NativeMethods.VkControl] & 0x80) != 0
            || (KeyState[NativeMethods.VkMenu] & 0x80) != 0
            || (KeyState[NativeMethods.VkLwin] & 0x80) != 0
            || (KeyState[NativeMethods.VkRwin] & 0x80) != 0)
        {
            return null;
        }

        var layout = GetForegroundLayout();
        Buffer.Clear();
        var result = NativeMethods.ToUnicodeEx(
            virtualKey,
            scanCode,
            KeyState,
            Buffer,
            Buffer.Capacity,
            0,
            layout);

        if (result > 0)
        {
            return Buffer.ToString(0, result);
        }

        // Clear dead-key state left in the translation stack.
        if (result < 0)
        {
            Buffer.Clear();
            NativeMethods.ToUnicodeEx(
                virtualKey,
                scanCode,
                KeyState,
                Buffer,
                Buffer.Capacity,
                0,
                layout);
        }

        return null;
    }

    public static bool IsStraightDoubleQuote(string text) => text == "\"";

    private static IntPtr GetForegroundLayout()
    {
        var window = NativeMethods.GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return NativeMethods.GetKeyboardLayout(0);
        }

        var threadId = NativeMethods.GetWindowThreadProcessId(window, out _);
        return NativeMethods.GetKeyboardLayout(threadId);
    }

    private static void SyncModifier(uint genericVk, uint leftVk, uint rightVk)
    {
        var pressed = IsAsyncDown(genericVk) || IsAsyncDown(leftVk) || IsAsyncDown(rightVk);
        SetPressed(genericVk, pressed);
        SetPressed(leftVk, IsAsyncDown(leftVk));
        SetPressed(rightVk, IsAsyncDown(rightVk));
    }

    private static void SyncSingle(uint virtualKey)
    {
        SetPressed(virtualKey, IsAsyncDown(virtualKey));
    }

    private static void SetPressed(uint virtualKey, bool pressed)
    {
        if (pressed)
        {
            KeyState[virtualKey & 0xFF] |= 0x80;
        }
        else
        {
            KeyState[virtualKey & 0xFF] &= 0x7F;
        }
    }

    private static bool IsAsyncDown(uint virtualKey)
    {
        return (NativeMethods.GetAsyncKeyState((int)virtualKey) & 0x8000) != 0;
    }
}
