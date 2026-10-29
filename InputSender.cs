using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DashTray;

internal static class InputSender
{
    public static void ReplaceCharacters(int backspaceCount, string replacement)
    {
        if (backspaceCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(backspaceCount));
        }

        var inputs = new List<NativeMethods.Input>(backspaceCount * 2 + replacement.Length * 2);

        for (var i = 0; i < backspaceCount; i++)
        {
            inputs.Add(KeyInput((ushort)NativeMethods.VkBack, 0, 0));
            inputs.Add(KeyInput((ushort)NativeMethods.VkBack, 0, NativeMethods.KeyeventfKeyup));
        }

        foreach (var character in replacement)
        {
            inputs.Add(KeyInput(0, (ushort)character, NativeMethods.KeyeventfUnicode));
            inputs.Add(KeyInput(0, (ushort)character, NativeMethods.KeyeventfUnicode | NativeMethods.KeyeventfKeyup));
        }

        if (inputs.Count == 0)
        {
            return;
        }

        var inputArray = inputs.ToArray();
        var sent = NativeMethods.SendInput(
            (uint)inputArray.Length,
            inputArray,
            Marshal.SizeOf<NativeMethods.Input>());

        if (sent != (uint)inputArray.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private static NativeMethods.Input KeyInput(ushort virtualKey, ushort scanCode, uint flags)
    {
        return new NativeMethods.Input
        {
            Type = NativeMethods.InputKeyboard,
            Data = new NativeMethods.InputUnion
            {
                Keyboard = new NativeMethods.KeyboardInput
                {
                    VirtualKey = virtualKey,
                    ScanCode = scanCode,
                    Flags = flags,
                    Time = 0,
                    ExtraInfo = UIntPtr.Zero
                }
            }
        };
    }
}
