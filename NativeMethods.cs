using System.Runtime.InteropServices;
using System.Text;

namespace DashTray;

internal static class NativeMethods
{
    internal const int WhKeyboardLl = 13;

    internal const int WmKeyDown = 0x0100;
    internal const int WmKeyUp = 0x0101;
    internal const int WmSysKeyDown = 0x0104;
    internal const int WmSysKeyUp = 0x0105;

    internal const uint VkBack = 0x08;
    internal const uint VkTab = 0x09;
    internal const uint VkReturn = 0x0D;
    internal const uint VkShift = 0x10;
    internal const uint VkControl = 0x11;
    internal const uint VkMenu = 0x12;
    internal const uint VkEscape = 0x1B;
    internal const uint VkSpace = 0x20;
    internal const uint VkLeft = 0x25;
    internal const uint VkUp = 0x26;
    internal const uint VkRight = 0x27;
    internal const uint VkDown = 0x28;
    internal const uint VkDelete = 0x2E;
    internal const uint VkLshift = 0xA0;
    internal const uint VkRshift = 0xA1;
    internal const uint VkLcontrol = 0xA2;
    internal const uint VkRcontrol = 0xA3;
    internal const uint VkLmenu = 0xA4;
    internal const uint VkRmenu = 0xA5;
    internal const uint VkLwin = 0x5B;
    internal const uint VkRwin = 0x5C;
    internal const uint VkOemMinus = 0xBD;
    internal const uint VkOem7 = 0xDE;
    internal const uint VkSubtract = 0x6D;
    internal const uint Vk2 = 0x32;

    internal const uint InputKeyboard = 1;
    internal const uint KeyeventfKeyup = 0x0002;
    internal const uint KeyeventfUnicode = 0x0004;

    [Flags]
    internal enum KbdLlFlags : uint
    {
        Extended = 0x01,
        LowerIlInjected = 0x02,
        Injected = 0x10,
        AltDown = 0x20,
        Up = 0x80
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KbdLlHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public KbdLlFlags Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;

        [FieldOffset(0)]
        public HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HardwareInput
    {
        public uint Message;
        public ushort ParameterLow;
        public ushort ParameterHigh;
    }

    internal delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelKeyboardProc callback,
        IntPtr moduleHandle,
        uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

    [DllImport("user32.dll")]
    internal static extern IntPtr CallNextHookEx(
        IntPtr hookHandle,
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(
        uint inputCount,
        [In] Input[] inputs,
        int inputSize);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetKeyboardState(byte[] keyState);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int ToUnicode(
        uint virtualKey,
        uint scanCode,
        byte[] keyState,
        [Out] StringBuilder buffer,
        int bufferSize,
        uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int ToUnicodeEx(
        uint virtualKey,
        uint scanCode,
        byte[] keyState,
        [Out] StringBuilder buffer,
        int bufferSize,
        uint flags,
        IntPtr keyboardLayout);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetKeyboardLayout(uint threadId);
}
