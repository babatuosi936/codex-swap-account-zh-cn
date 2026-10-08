using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay.Tests;

internal sealed class TestLayout : IDisposable
{
    private readonly TempDirectory tempDirectory = new();

    public TestLayout()
    {
        UserProfile = Path.Combine(tempDirectory.Path, "user");
        LocalAppData = Path.Combine(tempDirectory.Path, "local");
        Paths = new AppPaths(UserProfile, LocalAppData);
        Directory.CreateDirectory(Paths.SharedCodexDirectory);
        Directory.CreateDirectory(Paths.ProfilesDirectory);
        ActiveProfileStore = new ActiveProfileStore(Paths.ActiveProfileFile);
    }

    public string UserProfile { get; }

    public string LocalAppData { get; }

    public AppPaths Paths { get; }

    public ActiveProfileStore ActiveProfileStore { get; }

    public AuthSwitchService CreateSwitchService(
        IAtomicFileReplacer? replacer = null,
        BackupMaintenanceService? backups = null)
    {
        return new AuthSwitchService(
            Paths,
            new ProfileDiscoveryService(Paths.ProfilesDirectory),
            ActiveProfileStore,
            replacer,
            backups);
    }

    public void AddProfile(string name, string authContent)
    {
        string directory = Path.Combine(Paths.ProfilesDirectory, name);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "auth.json"), authContent);
    }

    public void WriteSharedAuth(string authContent)
    {
        File.WriteAllText(Paths.SharedAuthFile, authContent);
    }

    public string ReadSharedAuth() => File.ReadAllText(Paths.SharedAuthFile);

    public string ReadProfileAuth(string name)
    {
        return File.ReadAllText(Path.Combine(Paths.ProfilesDirectory, name, "auth.json"));
    }

    public void WriteSharedState(string content)
    {
        File.WriteAllText(Path.Combine(Paths.SharedCodexDirectory, ".codex-global-state.json"), content);
        File.WriteAllText(Path.Combine(Paths.SharedCodexDirectory, "session_index.jsonl"), "shared-index-" + content);
        File.WriteAllText(Path.Combine(Paths.SharedCodexDirectory, "state_5.sqlite"), "shared-db-" + content);
        string sessionDirectory = Path.Combine(Paths.SharedCodexDirectory, "sessions", "2026", "07", "04");
        Directory.CreateDirectory(sessionDirectory);
        File.WriteAllText(Path.Combine(sessionDirectory, "rollout.jsonl"), "shared-session-" + content);
    }

    public void WriteProfileState(string name, string content)
    {
        string stateDirectory = Path.Combine(Paths.ProfilesDirectory, name, "codex-state");
        Directory.CreateDirectory(stateDirectory);
        File.WriteAllText(Path.Combine(stateDirectory, ".codex-global-state.json"), content);
        File.WriteAllText(Path.Combine(stateDirectory, "session_index.jsonl"), "profile-index-" + content);
        File.WriteAllText(Path.Combine(stateDirectory, "state_5.sqlite"), "profile-db-" + content);
        string sessionDirectory = Path.Combine(stateDirectory, "sessions", "2026", "07", "04");
        Directory.CreateDirectory(sessionDirectory);
        File.WriteAllText(Path.Combine(sessionDirectory, "rollout.jsonl"), "profile-session-" + content);
    }

    public string ReadSharedStateFile(string fileName)
    {
        return File.ReadAllText(Path.Combine(Paths.SharedCodexDirectory, fileName));
    }

    public string ReadProfileStateFile(string profileName, string fileName)
    {
        return File.ReadAllText(Path.Combine(Paths.ProfilesDirectory, profileName, "codex-state", fileName));
    }

    public bool SharedStateFileExists(string fileName)
    {
        return File.Exists(Path.Combine(Paths.SharedCodexDirectory, fileName));
    }

    public bool SharedStateDirectoryExists(string directoryName)
    {
        return Directory.Exists(Path.Combine(Paths.SharedCodexDirectory, directoryName));
    }

    public void Dispose()
    {
        tempDirectory.Dispose();
    }
}
