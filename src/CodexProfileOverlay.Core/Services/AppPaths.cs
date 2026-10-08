namespace CodexProfileOverlay.Core.Services;

public sealed class AppPaths
{
    public AppPaths(string userProfile, string localAppData, string? applicationDataDirectory = null)
    {
        UserProfile = RequireDirectoryLikePath(userProfile, nameof(userProfile));
        LocalAppData = RequireDirectoryLikePath(localAppData, nameof(localAppData));
        SharedCodexDirectory = Path.Combine(UserProfile, ".codex");
        SharedAuthFile = Path.Combine(SharedCodexDirectory, "auth.json");
        ProfilesDirectory = Path.Combine(UserProfile, ".codex-profiles");
        ApplicationDataDirectory = applicationDataDirectory is null ? Path.Combine(LocalAppData, "CodexProfileOverlay")
            : RequireDirectoryLikePath(applicationDataDirectory, nameof(applicationDataDirectory));
        SettingsFile = Path.Combine(ApplicationDataDirectory, "settings.json");
        ProfilesMetadataFile = Path.Combine(ApplicationDataDirectory, "profiles.json");
        ActiveProfileFile = Path.Combine(ApplicationDataDirectory, "active-profile.txt");
        BackupDirectory = Path.Combine(ApplicationDataDirectory, "backups");
        LogDirectory = Path.Combine(ApplicationDataDirectory, "logs");
        RemovedProfilesDirectory = Path.Combine(ApplicationDataDirectory, "removed-profiles");
        PreflightBackupDirectory = Path.Combine(ApplicationDataDirectory, "preflight-backups");
        ProfileStatusFile = Path.Combine(ApplicationDataDirectory, "profile-status.json");
    }

    public string UserProfile { get; }

    public string LocalAppData { get; }

    public string SharedCodexDirectory { get; }

    public string SharedAuthFile { get; }

    public string ProfilesDirectory { get; }

    public string ApplicationDataDirectory { get; }

    public string SettingsFile { get; }

    public string ProfilesMetadataFile { get; }

    public string ActiveProfileFile { get; }

    public string BackupDirectory { get; }

    public string LogDirectory { get; }

    public string RemovedProfilesDirectory { get; }

    public string PreflightBackupDirectory { get; }

    public string ProfileStatusFile { get; }

    public static AppPaths FromEnvironment()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new AppPaths(userProfile, localAppData, ResolveApplicationDataDirectory(localAppData));
    }

    public static string ResolveApplicationDataDirectory(string localAppData)
    {
        string packaged = Path.Combine(localAppData, "Packages", "OpenAI.Codex_2p2nqsd0c76g0", "LocalCache", "Local", "CodexProfileOverlay");
        // Explicitly reuse the established data, whether launched by packaged Codex or Explorer.
        if (File.Exists(Path.Combine(packaged, "settings.json")) && File.Exists(Path.Combine(packaged, "active-profile.txt")))
        {
            return packaged;
        }
        return Path.Combine(localAppData, "CodexProfileOverlay");
    }

    private static string RequireDirectoryLikePath(string path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be empty.", parameterName);
        }

        return Path.GetFullPath(path);
    }
}
