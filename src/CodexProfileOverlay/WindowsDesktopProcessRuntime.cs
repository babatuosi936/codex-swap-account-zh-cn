using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CodexProfileOverlay.Core.Services;
using Microsoft.Win32.SafeHandles;

namespace CodexProfileOverlay;

internal sealed class WindowsDesktopProcessRuntime : IDesktopProcessRuntime
{
    private readonly SafeLogger logger;
    private readonly Dictionary<string, bool> identityCache = new(StringComparer.OrdinalIgnoreCase);

    public WindowsDesktopProcessRuntime(SafeLogger logger) => this.logger = logger;

    public IReadOnlyList<DesktopProcessInfo> Snapshot()
    {
        using SafeFileHandle snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not inspect the desktop process tree.");
        }

        var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
        var result = new List<DesktopProcessInfo>();
        if (!Process32First(snapshot, ref entry))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not enumerate the desktop process tree.");
        }
        do
        {
            string name = Path.GetFileNameWithoutExtension(entry.ExecutableName);
            DateTime? started = null;
            string? path = null;
            bool desktop = false;
            try
            {
                using Process process = Process.GetProcessById((int)entry.ProcessId);
                started = process.StartTime.ToUniversalTime();
                if (name.Equals("Codex", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase))
                {
                    path = process.MainModule?.FileName;
                    if (path is not null && !identityCache.TryGetValue(path, out desktop))
                    {
                        FileVersionInfo version = FileVersionInfo.GetVersionInfo(path);
                        desktop = CodexDesktopProcessPolicy.IsOfficialDesktopExecutable(path, version.ProductName, version.FileDescription, version.CompanyName);
                        identityCache[path] = desktop;
                    }
                }
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception or FileNotFoundException)
            {
                // Keep the parent relationship. Inaccessible selected processes fail
                // closed when IsRunning verifies their identity before any action.
            }
            result.Add(new DesktopProcessInfo((int)entry.ProcessId, (int)entry.ParentProcessId, started, name, path, desktop));
        } while (Process32Next(snapshot, ref entry));
        int error = Marshal.GetLastWin32Error();
        if (error != 18) // ERROR_NO_MORE_FILES
        {
            throw new Win32Exception(error, "The desktop process snapshot was incomplete.");
        }
        return result;
    }

    public bool IsRunning(DesktopProcessInfo expected)
    {
        using Process? process = OpenExpectedProcess(expected);
        return process is not null;
    }

    public void RequestClose(DesktopProcessInfo expected)
    {
        using Process? process = OpenExpectedProcess(expected);
        if (process is null || process.MainWindowHandle == IntPtr.Zero)
        {
            return;
        }
        logger.Info($"Requesting Codex Desktop close for process {process.Id}.");
        _ = process.CloseMainWindow();
    }

    public void Terminate(DesktopProcessInfo expected)
    {
        using Process? process = OpenExpectedProcess(expected);
        if (process is null)
        {
            return;
        }
        logger.Info($"Terminating selected Codex Desktop process {process.Id} after graceful timeout.");
        try
        {
            process.Kill(entireProcessTree: false);
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
        }
    }

    private static Process? OpenExpectedProcess(DesktopProcessInfo expected)
    {
        Process? process = null;
        try
        {
            process = Process.GetProcessById(expected.Id);
            if (process.HasExited || process.StartTime.ToUniversalTime() != expected.StartTimeUtc)
            {
                if (expected.StartTimeUtc is null && !process.HasExited)
                {
                    throw new InvalidOperationException("Cannot verify a selected desktop process; authorization was not changed.");
                }
                process.Dispose();
                return null;
            }
            return process;
        }
        catch (ArgumentException)
        {
            process?.Dispose();
            return null;
        }
        catch (InvalidOperationException) when (process?.HasExited == true)
        {
            process.Dispose();
            return null;
        }
        catch
        {
            process?.Dispose();
            throw;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeFileHandle CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(SafeFileHandle snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(SafeFileHandle snapshot, ref ProcessEntry entry);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public UIntPtr DefaultHeapId;
        public uint ModuleId;
        public uint ThreadCount;
        public uint ParentProcessId;
        public int BasePriority;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExecutableName;
    }
}
