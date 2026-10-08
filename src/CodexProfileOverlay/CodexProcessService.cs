using System.Diagnostics;
using System.IO;
using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay;

internal sealed class CodexProcessService
{
    private readonly SafeLogger logger;
    private readonly WindowsDesktopProcessRuntime desktopRuntime;

    public CodexProcessService(SafeLogger logger)
    {
        this.logger = logger;
        desktopRuntime = new WindowsDesktopProcessRuntime(logger);
    }

    public Task CloseCodexAsync(int gracefulTimeoutSeconds, bool allowForceClose, CancellationToken cancellationToken, int? attachedDesktopProcessId = null)
    {
        if (HasDesktopLifetimeDependency())
        {
            throw new DesktopLifetimeDependencyException();
        }
        if (attachedDesktopProcessId is int id)
        {
            DesktopProcessInfo? attached = desktopRuntime.Snapshot().FirstOrDefault(process => process.Id == id);
            if (attached is not null && !attached.IsDesktopExecutable)
            {
                throw new InvalidOperationException("Cannot verify the attached Codex Desktop process; authorization was not changed.");
            }
        }
        return new CodexDesktopCloseService(desktopRuntime, Environment.ProcessId)
            .CloseAsync(gracefulTimeoutSeconds, allowForceClose, cancellationToken);
    }

    public IReadOnlyList<DesktopProcessInfo> InspectDesktopProcesses() =>
        CodexDesktopProcessPolicy.SelectTargets(desktopRuntime.Snapshot(), Environment.ProcessId);

    public bool HasDesktopLifetimeDependency() => CodexDesktopProcessPolicy.HasLifetimeDependency(
        desktopRuntime.Snapshot(), Environment.ProcessId, WindowsDesktopProcessRuntime.CurrentJobMembers());

    public void LaunchCodex()
    {
        if (TryLaunchStartMenuApp())
        {
            return;
        }

        throw new InvalidOperationException("Could not find the installed Codex Desktop application.");
    }

    public async Task<bool> LoginProfileAsync(string profileDirectory, CancellationToken cancellationToken)
    {
        string fullProfileDirectory = Path.GetFullPath(profileDirectory);
        Directory.CreateDirectory(fullProfileDirectory);
        string executable = CodexCliLocator.FindExecutable()
            ?? throw new FileNotFoundException("codex executable was not found.");
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = fullProfileDirectory,
        };
        startInfo.ArgumentList.Add("login");
        startInfo.Environment["CODEX_HOME"] = fullProfileDirectory;

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start codex login.");
        Task standardOutputTask = DrainReaderAsync(process.StandardOutput);
        Task standardErrorTask = DrainReaderAsync(process.StandardError);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await Task.WhenAll(standardOutputTask, standardErrorTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await Task.WhenAll(standardOutputTask, standardErrorTask).ConfigureAwait(false);
            throw;
        }

        return process.ExitCode == 0 && File.Exists(Path.Combine(fullProfileDirectory, "auth.json"));
    }

    private static async Task DrainReaderAsync(TextReader reader)
    {
        char[] buffer = new char[4096];
        while (await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false) > 0)
        {
        }
    }

    private bool TryLaunchStartMenuApp()
    {
        string? appId = ResolveStartAppId();
        if (!string.IsNullOrWhiteSpace(appId))
        {
            logger.Info("Launching Codex through Start menu AppUserModelID.");
            var startInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = true,
            };
            startInfo.ArgumentList.Add($"shell:AppsFolder\\{appId}");
            _ = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start Codex through Start menu AppUserModelID.");
            return true;
        }

        string? shortcut = FindStartMenuShortcut();
        if (!string.IsNullOrWhiteSpace(shortcut))
        {
            logger.Info("Launching Codex through Start menu shortcut.");
            _ = Process.Start(new ProcessStartInfo
            {
                FileName = shortcut,
                UseShellExecute = true,
            }) ?? throw new InvalidOperationException("Could not start Codex through Start menu shortcut.");
            return true;
        }

        return false;
    }

    private static string? ResolveStartAppId()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -Command \"(Get-StartApps | Where-Object { $_.AppID -like 'OpenAI.Codex_*!*' } | Select-Object -First 1 -ExpandProperty AppID)\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            });

            if (process is null)
            {
                return null;
            }
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(5000))
            {
                process.Kill();
                return null;
            }
            string output = outputTask.GetAwaiter().GetResult().Trim();
            _ = errorTask.GetAwaiter().GetResult();
            return CodexDesktopProcessPolicy.IsOfficialAppId(output) ? output : null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static string? FindStartMenuShortcut()
    {
        string[] roots =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
        ];

        foreach (string root in roots.Where(Directory.Exists))
        {
            string? shortcut = Directory.EnumerateFiles(root, "Codex.lnk", SearchOption.AllDirectories)
                .OrderBy(static path => path.Length)
                .FirstOrDefault();
            if (shortcut is not null)
            {
                return shortcut;
            }
        }

        return null;
    }

}
