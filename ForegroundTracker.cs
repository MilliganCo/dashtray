using System.Diagnostics;

namespace DashTray;

internal static class ForegroundTracker
{
    public static string? LastExternalProcessName { get; private set; }

    public static void Observe(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return;
        }

        try
        {
            NativeMethods.GetWindowThreadProcessId(window, out var processId);
            if (processId == 0)
            {
                return;
            }

            using var process = Process.GetProcessById((int)processId);
            if (process.ProcessName.Equals("DashTray", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            LastExternalProcessName = process.ProcessName;
        }
        catch
        {
            // Ignore transient process lookup failures.
        }
    }
}
