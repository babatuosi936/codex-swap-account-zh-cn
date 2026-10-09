namespace CodexProfileOverlay.Core.Services;

public sealed record DesktopProcessInfo(
    int Id,
    int ParentId,
    DateTime? StartTimeUtc,
    string Name,
    string? ExecutablePath,
    bool IsDesktopExecutable = false,
    int Depth = 0);

public sealed class DesktopLifetimeDependencyException : InvalidOperationException
{
    public DesktopLifetimeDependencyException() : base("The helper shares the desktop lifetime. Start it from File Explorer before switching; authorization was not changed.") { }
}

public static class CodexDesktopProcessPolicy
{
    public static bool HasLifetimeDependency(IReadOnlyList<DesktopProcessInfo> snapshot, int helperId, IEnumerable<int> jobMembers)
    {
        var targets = SelectTargets(snapshot, helperId).Select(process => process.Id).ToHashSet();
        if (jobMembers.Any(targets.Contains)) return true;
        var byId = snapshot.ToDictionary(process => process.Id);
        var visited = new HashSet<int>();
        int id = helperId;
        while (byId.TryGetValue(id, out DesktopProcessInfo? process) && visited.Add(id))
        {
            if (targets.Contains(process.ParentId)) return true;
            id = process.ParentId;
        }
        return false;
    }

    private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Code", "devenv", "explorer", "chrome", "msedge", "firefox", "WindowsTerminal", "CodexProfileOverlay",
    };
    private static readonly HashSet<string> DesktopWorkerNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "codex", "node_repl", "codex-code-mode-host", "codex-computer-use-swift",
    };

    public static bool IsOfficialDesktopExecutable(string? path, string? productName, string? fileDescription, string? companyName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string normalized = path.Replace('/', '\\');
        string[] parts = normalized.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        string filename = parts.LastOrDefault() ?? string.Empty;
        if (!filename.Equals("ChatGPT.exe", StringComparison.OrdinalIgnoreCase)
            && !filename.Equals("Codex.exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // The Store package currently calls its Electron executable ChatGPT.exe.
        int windowsApps = Array.FindIndex(parts, part => part.Equals("WindowsApps", StringComparison.OrdinalIgnoreCase));
        if (windowsApps >= 0)
        {
            return parts.Length == windowsApps + 4
                && parts[windowsApps + 1].StartsWith("OpenAI.Codex_", StringComparison.OrdinalIgnoreCase)
                && parts[windowsApps + 2].Equals("app", StringComparison.OrdinalIgnoreCase);
        }

        if (parts.Any(part => part.Equals(".vscode", StringComparison.OrdinalIgnoreCase)
            || part.Equals("extensions", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        bool codexProduct = string.Equals(productName, "Codex", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileDescription, "Codex", StringComparison.OrdinalIgnoreCase);
        bool openAiCompany = string.Equals(companyName, "OpenAI", StringComparison.OrdinalIgnoreCase)
            || companyName?.StartsWith("OpenAI ", StringComparison.OrdinalIgnoreCase) == true;
        return codexProduct && openAiCompany;
    }

    public static bool IsOfficialAppId(string? appId) =>
        appId?.StartsWith("OpenAI.Codex_", StringComparison.OrdinalIgnoreCase) == true
        && appId.Contains('!')
        && appId.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '.' or '-' or '!');

    public static IReadOnlyList<DesktopProcessInfo> SelectTargets(
        IReadOnlyList<DesktopProcessInfo> snapshot,
        int overlayProcessId,
        IEnumerable<DesktopProcessInfo>? tracked = null,
        DesktopProcessInfo? selectedRoot = null)
    {
        var byId = snapshot.ToDictionary(process => process.Id);
        var children = snapshot.GroupBy(process => process.ParentId).ToDictionary(group => group.Key, group => group.ToArray());
        var blocked = new HashSet<int>();
        var blockQueue = new Queue<int>(snapshot.Where(process => process.Id == overlayProcessId
            || Path.GetFileNameWithoutExtension(process.Name).Equals("CodexProfileOverlay", StringComparison.OrdinalIgnoreCase)).Select(process => process.Id));
        while (blockQueue.TryDequeue(out int id))
        {
            if (!blocked.Add(id) || !children.TryGetValue(id, out DesktopProcessInfo[]? childProcesses))
            {
                continue;
            }
            foreach (DesktopProcessInfo child in childProcesses)
            {
                blockQueue.Enqueue(child.Id);
            }
        }

        var roots = snapshot.Where(process => process.IsDesktopExecutable
            && (!byId.TryGetValue(process.ParentId, out DesktopProcessInfo? parent) || !parent.IsDesktopExecutable)).ToList();
        if (selectedRoot is not null)
        {
            roots = roots.Where(process => process.Id == selectedRoot.Id
                && process.StartTimeUtc == selectedRoot.StartTimeUtc).ToList();
        }
        // Keep watching known descendants even after their desktop parent has exited.
        foreach (DesktopProcessInfo remembered in tracked ?? [])
        {
            if (byId.TryGetValue(remembered.Id, out DesktopProcessInfo? current)
                && current.StartTimeUtc == remembered.StartTimeUtc)
            {
                roots.Add(current with { Depth = remembered.Depth });
            }
        }

        var result = new Dictionary<int, DesktopProcessInfo>();
        var queue = new Queue<DesktopProcessInfo>(roots);
        while (queue.TryDequeue(out DesktopProcessInfo? process))
        {
            if (blocked.Contains(process.Id) || result.ContainsKey(process.Id)
                || ProtectedNames.Contains(Path.GetFileNameWithoutExtension(process.Name)))
            {
                continue;
            }
            result.Add(process.Id, process);
            if (!children.TryGetValue(process.Id, out DesktopProcessInfo[]? childProcesses))
            {
                continue;
            }
            foreach (DesktopProcessInfo child in childProcesses)
            {
                // A reused parent PID must not claim an older, unrelated process.
                if (child.StartTimeUtc is not null && process.StartTimeUtc is not null
                    && child.StartTimeUtc < process.StartTimeUtc)
                {
                    continue;
                }
                queue.Enqueue(child with { Depth = process.Depth + 1 });
            }
        }

        // Traverse intermediate shells, but keep project servers and terminals alive.
        return result.Values.Where(process => process.IsDesktopExecutable
                || DesktopWorkerNames.Contains(Path.GetFileNameWithoutExtension(process.Name)))
            .OrderByDescending(process => process.Depth).ToArray();
    }
}
