using System.Diagnostics;
using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay;

internal static class ScreenshotWindowDetector
{
    public static bool IsCaptureActive()
    {
        bool active = false;
        var processNames = new Dictionary<uint, string>();
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hwnd) || NativeMethods.IsIconic(hwnd)
                || !NativeMethods.GetWindowRect(hwnd, out NativeRect rect))
            {
                return true;
            }
            // QQ's capture canvas covers a monitor. Exclude small persistent
            // toolbars and pinned images from the same screenshot process.
            var monitor = System.Windows.Forms.Screen.FromHandle(hwnd).Bounds;
            if (rect.Left > monitor.Left + 4 || rect.Top > monitor.Top + 4
                || rect.Right < monitor.Right - 4 || rect.Bottom < monitor.Bottom - 4)
            {
                return true;
            }
            NativeMethods.GetWindowThreadProcessId(hwnd, out uint processId);
            if (!processNames.TryGetValue(processId, out string? name))
            {
                try
                {
                    using Process process = Process.GetProcessById((int)processId);
                    name = process.ProcessName;
                    processNames[processId] = name;
                }
                catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    return true;
                }
            }
            if (ScreenshotWindowPolicy.IsDedicatedCaptureProcess(name))
            {
                active = true;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return active;
    }
}
