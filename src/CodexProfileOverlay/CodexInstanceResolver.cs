using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay;

internal sealed class CodexInstanceResolver
{
    private readonly string defaultHome;
    private readonly Dictionary<int, (DateTime Started, DateTimeOffset Checked, string? Home)> cache = new();

    public CodexInstanceResolver(string defaultHome) => this.defaultHome = Path.GetFullPath(defaultHome);

    public string? ResolveHome(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            DateTime started = process.StartTime.ToUniversalTime();
            if (cache.TryGetValue(processId, out var known) && known.Started == started
                && DateTimeOffset.UtcNow - known.Checked < TimeSpan.FromSeconds(2)) return known.Home;
            string? home = TryReadHome(processId, out string? configuredHome)
                ? (string.IsNullOrWhiteSpace(configuredHome) ? defaultHome : Path.GetFullPath(configuredHome)) : null;
            if (home is not null && !Directory.Exists(home)) home = null;
            cache[processId] = (started, DateTimeOffset.UtcNow, home);
            if (cache.Count > 128) cache.Clear();
            return home;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException
            or System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException) { return null; }
    }

    public string? FindActiveProfile(int processId, IReadOnlyList<ProfileInfo> profiles)
    {
        string? home = ResolveHome(processId);
        if (home is null) return null;
        string? identity = InstanceAccountIdentity.Read(Path.Combine(home, "auth.json"));
        if (identity is null) return null;
        return profiles.FirstOrDefault(profile => string.Equals(identity,
            InstanceAccountIdentity.Read(profile.AuthFilePath), StringComparison.Ordinal))?.Name;
    }

    public bool IsDefaultHome(int processId) => string.Equals(ResolveHome(processId), defaultHome, StringComparison.OrdinalIgnoreCase);

    // Read only CODEX_HOME from the verified desktop process. Do not retain or log
    // other environment entries. A failed read is unknown, never the default home.
    internal static bool TryReadHome(int processId, out string? home)
    {
        home = null;
        if (!Environment.Is64BitProcess) return false;
        IntPtr process = OpenProcess(0x0410, false, processId);
        if (process == IntPtr.Zero) return false;
        try
        {
            byte[] basic = new byte[48];
            if (NtQueryInformationProcess(process, 0, basic, basic.Length, out _) != 0) return false;
            long peb = BitConverter.ToInt64(basic, 8);
            if (!ReadPointer(process, peb + 0x20, out long parameters)
                || !ReadPointer(process, parameters + 0x80, out long environment)) return false;
            var text = new StringBuilder();
            for (int offset = 0; offset < 1024 * 1024; offset += 256)
            {
                byte[] block = new byte[256];
                bool read = ReadProcessMemory(process, new IntPtr(environment + offset), block, block.Length, out IntPtr bytesRead);
                int count = checked((int)bytesRead.ToInt64());
                if (count <= 0 || count > block.Length || count % 2 != 0) return false;
                text.Append(Encoding.Unicode.GetString(block, 0, count));
                string entries = text.ToString();
                int end = entries.IndexOf("\0\0", StringComparison.Ordinal);
                if (end >= 0)
                {
                    string? entry = entries[..end].Split('\0').FirstOrDefault(value => value.StartsWith("CODEX_HOME=", StringComparison.OrdinalIgnoreCase));
                    home = entry?["CODEX_HOME=".Length..];
                    return true;
                }
                if (!read) return false;
            }
            return false;
        }
        finally { CloseHandle(process); }
    }

    private static bool ReadPointer(IntPtr process, long address, out long pointer)
    {
        byte[] bytes = new byte[8];
        bool success = ReadProcessMemory(process, new IntPtr(address), bytes, bytes.Length, out IntPtr count)
            && count.ToInt64() == bytes.Length;
        pointer = success ? BitConverter.ToInt64(bytes) : 0;
        return success && pointer != 0;
    }

    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, int processId);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ReadProcessMemory(IntPtr process, IntPtr address, byte[] buffer, int size, out IntPtr read);
    [DllImport("ntdll.dll")] private static extern int NtQueryInformationProcess(IntPtr process, int informationClass, byte[] information, int length, out int returned);
}
