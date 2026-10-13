using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using CodexProfileOverlay.Core.Services;
using Microsoft.Win32.SafeHandles;

namespace CodexProfileOverlay;

internal sealed record IsolatedDesktopLaunchResult(
    int? ProcessId,
    string PackageFamilyName,
    string ExecutablePath,
    bool PackageIdentityVerified,
    bool ActivationUncertain);

/// <summary>
/// Creates a desktop client with account-specific environment inside its registered
/// Store package. History preparation and finding/focusing a window belong to the caller.
/// </summary>
internal sealed class IsolatedDesktopLauncher
{
    internal const string HelperArgument = "--isolated-desktop-package-helper";
    private const string OfficialFamily = "OpenAI.Codex_2p2nqsd0c76g0";
    private const string ReceiptDirectoryPrefix = "CodexProfileOverlay-desktop-launch-";
    private readonly SafeLogger logger;

    public IsolatedDesktopLauncher(SafeLogger logger) => this.logger = logger;

    public async Task<IsolatedDesktopLaunchResult> LaunchAsync(
        string codexHome, string sharedSqliteHome, string electronUserDataDirectory,
        CancellationToken cancellationToken)
    {
        ValidateLocations(codexHome, sharedSqliteHome, electronUserDataDirectory);
        cancellationToken.ThrowIfCancellationRequested();
        RegisteredDesktop package = await FindRegisteredDesktopAsync(cancellationToken).ConfigureAwait(false);
        string helper = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not locate the desktop launcher executable.");
        if (!Path.GetFileName(helper).Equals("CodexProfileOverlay.exe", StringComparison.OrdinalIgnoreCase)
            || !File.Exists(helper))
        {
            throw new InvalidOperationException("Start the published overlay executable before opening an account window.");
        }

        string nonce = Guid.NewGuid().ToString("N");
        string directory = Path.Combine(Path.GetTempPath(), ReceiptDirectoryPrefix + nonce);
        Directory.CreateDirectory(directory);
        string receiptPath = Path.Combine(directory, "result.json");
        var request = new LaunchRequest(package.FamilyName, package.AppId, package.PackageRoot,
            package.ExecutablePath, Path.GetFullPath(codexHome), Path.GetFullPath(sharedSqliteHome),
            Path.GetFullPath(electronUserDataDirectory), receiptPath, nonce);
        string payload = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(request));
        if (payload.Length > 24000)
        {
            TryRemoveReceiptDirectory(directory);
            throw new InvalidOperationException("The selected account paths are too long for package activation.");
        }

        try
        {
            // Package activation recreates the environment; pass the three explicit
            // directory values to the helper, which creates the official client.
            string script = "$ErrorActionPreference = 'Stop'\n"
                + "Invoke-CommandInDesktopPackage -PackageFamilyName " + PowerShellLiteral(package.FamilyName)
                + " -AppId " + PowerShellLiteral(package.AppId)
                + " -PreventBreakaway -Command " + PowerShellLiteral(helper)
                + " -Args " + PowerShellLiteral(HelperArgument + " " + payload);
            logger.Info("Opening an isolated Codex desktop account window using its registered package.");
            PowerShellResult activation = await RunPowerShellAsync(script, TimeSpan.FromSeconds(20), cancellationToken)
                .ConfigureAwait(false);

            Stopwatch receiptClock = Stopwatch.StartNew();
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                LaunchReceipt? receipt = TryReadReceipt(receiptPath, nonce);
                if (receipt is not null)
                {
                    if (!receipt.Started)
                    {
                        throw new InvalidOperationException("The isolated desktop helper could not start Codex ("
                            + receipt.FailureCode + "). No default account was opened.");
                    }
                    bool verified = receipt.ProcessId is int pid
                        && VerifyProcess(pid, receipt.StartTimeUtc, package);
                    logger.Info("Isolated desktop activation completed; returned process verified: " + verified + ".");
                    return new IsolatedDesktopLaunchResult(verified ? receipt.ProcessId : null,
                        package.FamilyName, package.ExecutablePath, verified, !verified);
                }
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            } while (receiptClock.Elapsed < TimeSpan.FromSeconds(6));

            // A failed activation command can still have started the helper/client.
            // The caller must inspect windows rather than activate a second time.
            logger.Info("Isolated desktop activation has no confirmed receipt; inspecting windows is required. Command completed: "
                + (!activation.TimedOut && activation.ExitCode == 0) + ".");
            return new IsolatedDesktopLaunchResult(null, package.FamilyName, package.ExecutablePath, false, true);
        }
        finally
        {
            TryRemoveReceiptDirectory(directory);
        }
    }

    /// <summary>Call before the normal application singleton and account initialization.</summary>
    public static bool TryHandleHelper(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (!args.Contains(HelperArgument, StringComparer.Ordinal)) return false;
        exitCode = 2;
        if (args.Length != 2 || args[0] != HelperArgument || args[1].Length > 24000) return true;
        LaunchRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<LaunchRequest>(Convert.FromBase64String(args[1]));
            if (request is null || !ValidateRequest(request)) return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException
            or IOException or UnauthorizedAccessException)
        {
            return true;
        }

        // The helper never enters the profile manager, reads auth.json, or changes
        // shared records. Replies contain only native launch status, not secrets.
        LaunchReceipt receipt;
        try
        {
            if (!ValidateCurrentPackage(request))
            {
                receipt = new LaunchReceipt(request.Nonce, false, null, null, "package-entry-changed");
            }
            else
            {
                using Process process = StartClientWithNullStreams(request);
                DateTime? started = null;
                try { started = process.StartTime.ToUniversalTime(); }
                catch (Exception exception) when (exception is InvalidOperationException or Win32Exception) { }
                receipt = new LaunchReceipt(request.Nonce, true, process.Id, started, null);
            }
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException
            or IOException or UnauthorizedAccessException or ArgumentException or System.Xml.XmlException)
        {
            receipt = new LaunchReceipt(request.Nonce, false, null, null, "client-create-failed");
        }

        try
        {
            WriteReceipt(request.ReceiptPath, receipt);
            exitCode = receipt.Started ? 0 : 1;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The parent treats absent receipts as uncertain; it never retries blindly.
            exitCode = 3;
        }
        return true;
    }

    internal static void ValidateLocations(string codexHome, string sharedSqliteHome, string electronUserDataDirectory)
    {
        RequireDirectory(codexHome);
        RequireDirectory(sharedSqliteHome);
        RequireDirectory(electronUserDataDirectory);
        string desktop = Path.TrimEndingDirectorySeparator(Path.GetFullPath(electronUserDataDirectory));
        if (SamePath(desktop, codexHome) || SamePath(desktop, sharedSqliteHome))
            throw new InvalidOperationException("The desktop cache must have its own directory.");
    }

    internal static ProcessStartInfo BuildChildStartInfo(LaunchRequest request)
    {
        var info = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            WorkingDirectory = Path.GetDirectoryName(request.ExecutablePath)!,
            UseShellExecute = false,
        };
        info.Environment.Remove("ELECTRON_RUN_AS_NODE");
        info.Environment.Remove("OPENAI_API_KEY");
        info.Environment.Remove("CODEX_API_KEY");
        info.Environment.Remove("CODEX_ACCESS_TOKEN");
        info.Environment["CODEX_HOME"] = request.CodexHome;
        info.Environment["CODEX_SQLITE_HOME"] = request.SharedSqliteHome;
        info.Environment["CODEX_ELECTRON_USER_DATA_PATH"] = request.ElectronUserDataDirectory;
        info.ArgumentList.Add("--user-data-dir=" + request.ElectronUserDataDirectory);
        return info;
    }

    private static Process StartClientWithNullStreams(LaunchRequest request)
    {
        // The activation control process redirects its output. The long-lived client
        // must not inherit those pipes, or reading control-process output never ends.
        // This is only called in the dedicated helper, before normal application startup.
        var attributes = new SecurityAttributes
        {
            Length = Marshal.SizeOf<SecurityAttributes>(),
            InheritHandle = true,
        };
        using SafeFileHandle nullStream = CreateFile("NUL", 0xC0000000, 3, ref attributes, 3, 0x80, IntPtr.Zero);
        if (nullStream.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        int[] channels = [-10, -11, -12];
        IntPtr[] previous = channels.Select(GetStdHandle).ToArray();
        try
        {
            foreach (int channel in channels)
                if (!SetStdHandle(channel, nullStream.DangerousGetHandle()))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            return Process.Start(BuildChildStartInfo(request))
                ?? throw new InvalidOperationException("Client creation returned no process.");
        }
        finally
        {
            for (int index = 0; index < channels.Length; index++)
                _ = SetStdHandle(channels[index], previous[index]);
        }
    }

    private static async Task<RegisteredDesktop> FindRegisteredDesktopAsync(CancellationToken token)
    {
        string script = "$ErrorActionPreference = 'Stop'\n"
            + "$p = Get-AppxPackage -Name 'OpenAI.Codex' | Where-Object { $_.PackageFamilyName -eq '"
            + OfficialFamily + "' } | Sort-Object Version -Descending | Select-Object -First 1\n"
            + "if (-not $p) { throw 'Registered Codex package not found' }\n"
            + "[pscustomobject]@{ FamilyName=$p.PackageFamilyName; PackageRoot=$p.InstallLocation } | ConvertTo-Json -Compress";
        PowerShellResult query = await RunPowerShellAsync(script, TimeSpan.FromSeconds(12), token).ConfigureAwait(false);
        if (query.TimedOut || query.ExitCode != 0)
            throw new InvalidOperationException("Could not inspect the registered Codex desktop package. No account window was opened.");
        PackageRegistration? registration;
        try { registration = JsonSerializer.Deserialize<PackageRegistration>(query.Output.Trim()); }
        catch (JsonException) { throw new InvalidOperationException("The desktop package registration could not be validated."); }
        if (registration is null || registration.FamilyName != OfficialFamily
            || !Path.IsPathFullyQualified(registration.PackageRoot) || !Directory.Exists(registration.PackageRoot))
            throw new InvalidOperationException("The registered Codex desktop package is not valid.");
        string root = Path.GetFullPath(registration.PackageRoot);
        (string appId, string executable) = ReadGuiApplication(root);
        return new RegisteredDesktop(registration.FamilyName, appId, root, executable);
    }

    internal static (string AppId, string ExecutablePath) ReadGuiApplication(string packageRoot)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(packageRoot));
        string manifestPath = Path.Combine(root, "AppxManifest.xml");
        if (new FileInfo(manifestPath).Length > 1024 * 1024)
            throw new InvalidOperationException("The desktop package manifest is too large.");
        XDocument manifest = XDocument.Load(manifestPath, LoadOptions.None);
        var matches = new List<(string, string)>();
        foreach (XElement application in manifest.Descendants().Where(element => element.Name.LocalName == "Application"))
        {
            string? appId = (string?)application.Attribute("Id");
            string? relative = (string?)application.Attribute("Executable");
            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(relative)
                || appId.Any(value => !(char.IsAsciiLetterOrDigit(value) || value is '_' or '.' or '-'))
                || Path.IsPathRooted(relative) || relative.Contains(':')) continue;
            string path = Path.GetFullPath(Path.Combine(root, relative));
            string name = Path.GetFileName(path);
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || (!name.Equals("ChatGPT.exe", StringComparison.OrdinalIgnoreCase)
                    && !name.Equals("Codex.exe", StringComparison.OrdinalIgnoreCase))
                || !File.Exists(path)) continue;
            matches.Add((appId, path));
        }
        if (matches.Count != 1)
            throw new InvalidOperationException("Could not identify one registered Codex desktop application.");
        return matches[0];
    }

    private static bool ValidateRequest(LaunchRequest request)
    {
        if (request.FamilyName != OfficialFamily || string.IsNullOrWhiteSpace(request.AppId)
            || !Guid.TryParseExact(request.Nonce, "N", out _) || !Path.IsPathFullyQualified(request.PackageRoot)
            || !Path.IsPathFullyQualified(request.ExecutablePath) || !Path.IsPathFullyQualified(request.ReceiptPath)) return false;
        ValidateLocations(request.CodexHome, request.SharedSqliteHome, request.ElectronUserDataDirectory);
        string? directory = Path.GetDirectoryName(Path.GetFullPath(request.ReceiptPath));
        if (directory is null || Path.GetFileName(request.ReceiptPath) != "result.json"
            || Path.GetFileName(directory) != ReceiptDirectoryPrefix + request.Nonce || !Directory.Exists(directory)
            || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) return false;
        string[] tempRoots = [Path.GetTempPath(), Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "Temp")];
        return tempRoots.Any(root => SamePath(Path.GetDirectoryName(directory)!, root));
    }

    private static bool ValidateCurrentPackage(LaunchRequest request)
    {
        if (!string.Equals(CurrentPackageValue(family: true), request.FamilyName, StringComparison.OrdinalIgnoreCase)
            || !SamePath(CurrentPackageValue(family: false), request.PackageRoot)) return false;
        (string appId, string executable) = ReadGuiApplication(request.PackageRoot);
        return appId == request.AppId && SamePath(executable, request.ExecutablePath);
    }

    private static bool VerifyProcess(int id, DateTime? expectedStart, RegisteredDesktop package)
    {
        try
        {
            if (expectedStart is null) return false;
            using Process process = Process.GetProcessById(id);
            if (process.HasExited || process.StartTime.ToUniversalTime() != expectedStart
                || !SamePath(process.MainModule?.FileName ?? string.Empty, package.ExecutablePath)) return false;
            uint length = 0;
            if (GetPackageFamilyName(process.SafeHandle.DangerousGetHandle(), ref length, null) != 122
                || length is 0 or > 32768) return false;
            var value = new StringBuilder((int)length);
            return GetPackageFamilyName(process.SafeHandle.DangerousGetHandle(), ref length, value) == 0
                && value.ToString().Equals(package.FamilyName, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception
            or UnauthorizedAccessException or IOException) { return false; }
    }

    private static string CurrentPackageValue(bool family)
    {
        uint length = 0;
        int status = family ? GetCurrentPackageFamilyName(ref length, null) : GetCurrentPackagePath(ref length, null);
        if (status != 122 || length is 0 or > 32768)
            throw new InvalidOperationException("The launch helper has no valid package identity.");
        var value = new StringBuilder((int)length);
        status = family ? GetCurrentPackageFamilyName(ref length, value) : GetCurrentPackagePath(ref length, value);
        if (status != 0) throw new Win32Exception(status);
        return value.ToString();
    }

    private static async Task<PowerShellResult> RunPowerShellAsync(string script, TimeSpan timeout, CancellationToken token)
    {
        var info = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add("-NoProfile");
        info.ArgumentList.Add("-NonInteractive");
        info.ArgumentList.Add("-EncodedCommand");
        info.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
        using Process process = Process.Start(info) ?? throw new InvalidOperationException("Could not inspect desktop registration.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
        bool timedOut = false;
        try { await process.WaitForExitAsync(linked.Token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            timedOut = deadline.IsCancellationRequested && !token.IsCancellationRequested;
            // Terminate only our PowerShell control process, never its official client
            // descendants. Activation may already have created a window.
            try { if (!process.HasExited) process.Kill(entireProcessTree: false); }
            catch (InvalidOperationException) { }
            if (!timedOut) throw;
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        string text;
        try
        {
            await Task.WhenAll(output, errors).WaitAsync(TimeSpan.FromSeconds(2), token).ConfigureAwait(false);
            text = output.Result;
        }
        catch (TimeoutException)
        {
            // No duplicate launch even if another package process kept a pipe open.
            timedOut = true;
            text = string.Empty;
        }
        return new PowerShellResult(process.ExitCode, timedOut, text);
    }

    private static LaunchReceipt? TryReadReceipt(string path, string nonce)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 4096) return null;
            LaunchReceipt? receipt = JsonSerializer.Deserialize<LaunchReceipt>(File.ReadAllText(path));
            return receipt?.Nonce == nonce ? receipt : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    private static void WriteReceipt(string path, LaunchReceipt receipt)
    {
        string pending = path + ".pending";
        using (var file = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            JsonSerializer.Serialize(file, receipt);
        File.Move(pending, path, overwrite: false);
    }

    private static void TryRemoveReceiptDirectory(string directory)
    {
        try
        {
            File.Delete(Path.Combine(directory, "result.json"));
            File.Delete(Path.Combine(directory, "result.json.pending"));
            Directory.Delete(directory, recursive: false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }

    private static void RequireDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !Directory.Exists(path))
            throw new InvalidOperationException("Prepare the selected account and desktop directories before launching.");
    }

    private static bool SamePath(string left, string right) => string.Equals(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)), StringComparison.OrdinalIgnoreCase);

    private static string PowerShellLiteral(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    internal sealed record LaunchRequest(string FamilyName, string AppId, string PackageRoot,
        string ExecutablePath, string CodexHome, string SharedSqliteHome, string ElectronUserDataDirectory,
        string ReceiptPath, string Nonce);
    private sealed record PackageRegistration(string FamilyName, string PackageRoot);
    private sealed record RegisteredDesktop(string FamilyName, string AppId, string PackageRoot, string ExecutablePath);
    private sealed record LaunchReceipt(string Nonce, bool Started, int? ProcessId, DateTime? StartTimeUtc, string? FailureCode);
    private sealed record PowerShellResult(int ExitCode, bool TimedOut, string Output);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetCurrentPackageFamilyName(ref uint length, StringBuilder? value);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetCurrentPackagePath(ref uint length, StringBuilder? value);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetPackageFamilyName(IntPtr process, ref uint length, StringBuilder? value);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateFileW")]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint sharing,
        ref SecurityAttributes security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int channel);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetStdHandle(int channel, IntPtr handle);
    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int Length;
        public IntPtr SecurityDescriptor;
        [MarshalAs(UnmanagedType.Bool)] public bool InheritHandle;
    }
}
