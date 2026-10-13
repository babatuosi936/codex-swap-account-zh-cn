using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay;

internal sealed class WindowsSharedDirectoryLinker : ISharedProfileDirectoryLinker
{
    public string? GetDirectoryLinkTarget(string path)
    {
        var directory = new DirectoryInfo(path);
        if (directory.LinkTarget is null) return null;
        string? target = directory.ResolveLinkTarget(returnFinalTarget: false)?.FullName;
        if (target is null) throw new IOException("The existing directory link cannot be resolved.");
        return target.StartsWith(@"\\?\", StringComparison.Ordinal) ? target[4..] : target;
    }

    public void CreateDirectoryLink(string path, string target)
    {
        string link = Path.GetFullPath(path);
        string destination = Path.GetFullPath(target);
        if (!Directory.Exists(destination) || Directory.Exists(link) || File.Exists(link))
            throw new IOException("The directory link destination is unavailable or occupied.");
        if (destination.StartsWith(@"\\", StringComparison.Ordinal))
            throw new IOException("Shared history must use a local disk.");
        Directory.CreateDirectory(link);
        try
        {
            byte[] substitute = Encoding.Unicode.GetBytes(@"\??\" + Path.TrimEndingDirectorySeparator(destination));
            byte[] display = Encoding.Unicode.GetBytes(Path.TrimEndingDirectorySeparator(destination));
            int pathLength = substitute.Length + display.Length + 4;
            byte[] buffer = new byte[16 + pathLength];
            BitConverter.GetBytes(0xA0000003u).CopyTo(buffer, 0);
            BitConverter.GetBytes(checked((ushort)(8 + pathLength))).CopyTo(buffer, 4);
            BitConverter.GetBytes(checked((ushort)substitute.Length)).CopyTo(buffer, 10);
            BitConverter.GetBytes(checked((ushort)(substitute.Length + 2))).CopyTo(buffer, 12);
            BitConverter.GetBytes(checked((ushort)display.Length)).CopyTo(buffer, 14);
            substitute.CopyTo(buffer, 16);
            display.CopyTo(buffer, 16 + substitute.Length + 2);
            using SafeFileHandle handle = CreateFileW(link, 0x40000000, 0, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
            if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the shared history link.");
            if (!DeviceIoControl(handle, 0x000900A4, buffer, buffer.Length, IntPtr.Zero, 0, out _, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the shared history junction.");
        }
        catch
        {
            // Only the directory created by this operation is removed, never its target.
            Directory.Delete(link, recursive: false);
            throw;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint sharing,
        IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, byte[] input, int inputLength,
        IntPtr output, int outputLength, out int returned, IntPtr overlapped);
}
