using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay.Tests;

public sealed class SharedDesktopProfileServiceTests
{
    [Fact]
    public void Inspect_DoesNotCreateLinksOrCacheAndPreservesAllExistingBytes()
    {
        using var temp = new TestLayout();
        ProfileInfo profile = AddAuthenticatedProfile(temp, "475");
        temp.WriteSharedAuth("synthetic shared credentials");
        temp.WriteSharedState("synthetic existing history");
        string accountDatabase = Path.Combine(profile.DirectoryPath, "state_5.sqlite");
        File.WriteAllBytes(accountDatabase, [0, 1, 255, 13, 10]);
        string[] existingFiles = Directory.EnumerateFiles(temp.UserProfile, "*", SearchOption.AllDirectories).ToArray();
        Dictionary<string, byte[]> bytesBefore = existingFiles.ToDictionary(path => path, File.ReadAllBytes);
        string[] directoriesBefore = Directory.EnumerateDirectories(temp.UserProfile, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).ToArray();
        var linker = new FakeDirectoryLinker();

        PreparedDesktopProfile result = new SharedDesktopProfileService(temp.Paths, linker).Inspect(profile);

        Assert.Equal(profile.DirectoryPath, result.CodexHome);
        Assert.Equal(temp.Paths.SharedCodexDirectory, result.SharedSqliteHome);
        Assert.Empty(linker.Links);
        Assert.Equal(0, linker.CreateCount);
        Assert.False(Directory.Exists(result.ElectronUserDataDirectory));
        Assert.False(Directory.Exists(Path.Combine(temp.Paths.ApplicationDataDirectory, "desktop-instances")));
        foreach (string directoryName in new[] { "sessions", "archived_sessions", "attachments", "thread-writer-locks" })
        {
            Assert.False(Directory.Exists(Path.Combine(profile.DirectoryPath, directoryName)));
        }

        Assert.Equal(directoriesBefore, Directory.EnumerateDirectories(temp.UserProfile, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(existingFiles.Order(StringComparer.Ordinal), Directory.EnumerateFiles(temp.UserProfile, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal));
        foreach ((string path, byte[] bytes) in bytesBefore)
        {
            Assert.Equal(bytes, File.ReadAllBytes(path));
        }
    }

    [Fact]
    public void Inspect_DoesNotCreateAbsentSharedRootOrDestinations()
    {
        using var temp = new TestLayout();
        ProfileInfo profile = AddAuthenticatedProfile(temp, "475");
        Directory.Delete(temp.Paths.SharedCodexDirectory, recursive: false);
        byte[] authBefore = File.ReadAllBytes(profile.AuthFilePath);
        var linker = new FakeDirectoryLinker();

        PreparedDesktopProfile result = new SharedDesktopProfileService(temp.Paths, linker).Inspect(profile);

        Assert.False(Directory.Exists(result.SharedSqliteHome));
        Assert.False(Directory.Exists(result.ElectronUserDataDirectory));
        Assert.Equal(authBefore, File.ReadAllBytes(profile.AuthFilePath));
        Assert.Empty(linker.Links);
        Assert.Equal(0, linker.CreateCount);
        Assert.Single(Directory.EnumerateFileSystemEntries(profile.DirectoryPath));
    }

    [Fact]
    public void Inspect_RefusesIndependentHistoryWithoutChangingDataOrCreatingCache()
    {
        using var temp = new TestLayout();
        ProfileInfo profile = AddAuthenticatedProfile(temp, "475");
        string privateHistory = Path.Combine(profile.DirectoryPath, "archived_sessions");
        Directory.CreateDirectory(privateHistory);
        string originalFile = Path.Combine(privateHistory, "original.jsonl");
        byte[] originalBytes = [123, 34, 1, 255, 10];
        File.WriteAllBytes(originalFile, originalBytes);
        byte[] authBefore = File.ReadAllBytes(profile.AuthFilePath);
        var linker = new FakeDirectoryLinker();

        Assert.Throws<InvalidOperationException>(() => new SharedDesktopProfileService(temp.Paths, linker).Inspect(profile));

        Assert.Equal(originalBytes, File.ReadAllBytes(originalFile));
        Assert.Equal(authBefore, File.ReadAllBytes(profile.AuthFilePath));
        Assert.Empty(linker.Links);
        Assert.Equal(0, linker.CreateCount);
        Assert.False(Directory.Exists(Path.Combine(profile.DirectoryPath, "sessions")));
        Assert.False(Directory.Exists(Path.Combine(temp.Paths.SharedCodexDirectory, "sessions")));
        Assert.False(Directory.Exists(Path.Combine(temp.Paths.ApplicationDataDirectory, "desktop-instances")));
    }

    [Fact]
    public void Prepare_SharesOnlyHistoryAndKeepsCredentialsAndExistingState()
    {
        using var temp = new TestLayout();
        ProfileInfo profile = AddAuthenticatedProfile(temp, "475");
        temp.WriteSharedAuth("original shared credentials");
        temp.WriteSharedState("original history");
        string localDatabase = Path.Combine(profile.DirectoryPath, "state_5.sqlite");
        string config = Path.Combine(profile.DirectoryPath, "config.toml");
        File.WriteAllText(localDatabase, "existing account-local database");
        File.WriteAllText(config, "original account config");
        string authBefore = File.ReadAllText(profile.AuthFilePath);
        var linker = new FakeDirectoryLinker();

        PreparedDesktopProfile result = new SharedDesktopProfileService(temp.Paths, linker).Prepare(profile);

        Assert.Equal(profile.DirectoryPath, result.CodexHome);
        Assert.Equal(temp.Paths.SharedCodexDirectory, result.SharedSqliteHome);
        Assert.True(Directory.Exists(result.ElectronUserDataDirectory));
        Assert.StartsWith(Path.Combine(temp.Paths.ApplicationDataDirectory, "desktop-instances") + Path.DirectorySeparatorChar,
            result.ElectronUserDataDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new[] { "archived_sessions", "attachments", "sessions", "thread-writer-locks" },
            linker.Links.Keys.Select(Path.GetFileName).Order(StringComparer.Ordinal));
        foreach ((string source, string target) in linker.Links)
        {
            Assert.Equal(Path.Combine(temp.Paths.SharedCodexDirectory, Path.GetFileName(source)), target);
        }

        Assert.Equal(authBefore, File.ReadAllText(profile.AuthFilePath));
        Assert.Equal("original shared credentials", temp.ReadSharedAuth());
        Assert.Equal("original history", temp.ReadSharedStateFile(".codex-global-state.json"));
        Assert.Equal("shared-db-original history", temp.ReadSharedStateFile("state_5.sqlite"));
        Assert.Equal("shared-index-original history", temp.ReadSharedStateFile("session_index.jsonl"));
        Assert.Equal("existing account-local database", File.ReadAllText(localDatabase));
        Assert.Equal("original account config", File.ReadAllText(config));
    }

    [Fact]
    public void Prepare_IsIdempotentAndUsesSeparateDesktopCaches()
    {
        using var temp = new TestLayout();
        ProfileInfo first = AddAuthenticatedProfile(temp, "first");
        ProfileInfo second = AddAuthenticatedProfile(temp, "second");
        var linker = new FakeDirectoryLinker();
        var service = new SharedDesktopProfileService(temp.Paths, linker);

        PreparedDesktopProfile firstResult = service.Prepare(first);
        PreparedDesktopProfile repeated = service.Prepare(first);
        PreparedDesktopProfile secondResult = service.Prepare(second);

        Assert.Equal(firstResult, repeated);
        Assert.NotEqual(firstResult.ElectronUserDataDirectory, secondResult.ElectronUserDataDirectory);
        Assert.Equal(firstResult.SharedSqliteHome, secondResult.SharedSqliteHome);
        Assert.Equal(8, linker.CreateCount);
    }

    [Fact]
    public void Prepare_SharedAttachmentsRemainAvailableWithoutCopyingOrOverwriting()
    {
        using var temp = new TestLayout();
        ProfileInfo profile = AddAuthenticatedProfile(temp, "475");
        string sharedAttachments = Path.Combine(temp.Paths.SharedCodexDirectory, "attachments");
        string originalImage = Path.Combine(sharedAttachments, "existing-thread", "original.png");
        Directory.CreateDirectory(Path.GetDirectoryName(originalImage)!);
        byte[] originalBytes = [137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3];
        File.WriteAllBytes(originalImage, originalBytes);
        var linker = new FakeDirectoryLinker();

        _ = new SharedDesktopProfileService(temp.Paths, linker).Prepare(profile);

        string profileAttachments = Path.Combine(profile.DirectoryPath, "attachments");
        string? linkedTarget = linker.GetDirectoryLinkTarget(profileAttachments);
        Assert.Equal(sharedAttachments, linkedTarget);
        Assert.Equal(originalBytes, File.ReadAllBytes(Path.Combine(linkedTarget!, "existing-thread", "original.png")));
        Assert.Equal(originalBytes, File.ReadAllBytes(originalImage));
        Assert.Single(Directory.EnumerateFiles(sharedAttachments, "*", SearchOption.AllDirectories));
        Assert.False(Directory.Exists(Path.Combine(profile.DirectoryPath, "plugins")));
        Assert.False(Directory.Exists(Path.Combine(profile.DirectoryPath, ".sandbox")));
        Assert.Equal(4, linker.CreateCount);
    }

    [Fact]
    public void Prepare_RefusesPrivateAttachmentsBeforeCreatingHistoryLinksOrCache()
    {
        using var temp = new TestLayout();
        ProfileInfo profile = AddAuthenticatedProfile(temp, "475");
        string attachments = Path.Combine(profile.DirectoryPath, "attachments");
        Directory.CreateDirectory(attachments);
        string privateImage = Path.Combine(attachments, "private-original.png");
        byte[] originalBytes = [137, 80, 78, 71, 99, 100];
        File.WriteAllBytes(privateImage, originalBytes);
        var linker = new FakeDirectoryLinker();

        Assert.Throws<InvalidOperationException>(() => new SharedDesktopProfileService(temp.Paths, linker).Prepare(profile));

        Assert.Equal(originalBytes, File.ReadAllBytes(privateImage));
        Assert.Empty(linker.Links);
        Assert.Equal(0, linker.CreateCount);
        Assert.False(Directory.Exists(Path.Combine(profile.DirectoryPath, "sessions")));
        Assert.False(Directory.Exists(Path.Combine(temp.Paths.SharedCodexDirectory, "attachments")));
        Assert.False(Directory.Exists(Path.Combine(temp.Paths.SharedCodexDirectory, "sessions")));
        Assert.False(Directory.Exists(Path.Combine(temp.Paths.ApplicationDataDirectory, "desktop-instances")));
    }

    [Fact]
    public void Prepare_RefusesIndependentHistoryBeforeChangingAnyDirectory()
    {
        using var temp = new TestLayout();
        ProfileInfo profile = AddAuthenticatedProfile(temp, "475");
        string conflict = Path.Combine(profile.DirectoryPath, "archived_sessions");
        Directory.CreateDirectory(conflict);
        File.WriteAllText(Path.Combine(conflict, "original.jsonl"), "unique original reply");
        var linker = new FakeDirectoryLinker();

        Assert.Throws<InvalidOperationException>(() => new SharedDesktopProfileService(temp.Paths, linker).Prepare(profile));

        Assert.Equal("unique original reply", File.ReadAllText(Path.Combine(conflict, "original.jsonl")));
        Assert.Empty(linker.Links);
        Assert.False(Directory.Exists(Path.Combine(profile.DirectoryPath, "sessions")));
        Assert.False(Directory.Exists(Path.Combine(temp.Paths.ApplicationDataDirectory, "desktop-instances")));
        Assert.False(Directory.Exists(Path.Combine(temp.Paths.SharedCodexDirectory, "sessions")));
    }

    [Fact]
    public void Prepare_RefusesLinkToAnotherHistoryBeforeChangingAnyDirectory()
    {
        using var temp = new TestLayout();
        ProfileInfo profile = AddAuthenticatedProfile(temp, "475");
        var linker = new FakeDirectoryLinker();
        string conflicting = Path.Combine(profile.DirectoryPath, "archived_sessions");
        string other = Path.Combine(temp.UserProfile, "other-history");
        linker.AddExistingLink(conflicting, other);

        Assert.Throws<InvalidOperationException>(() => new SharedDesktopProfileService(temp.Paths, linker).Prepare(profile));

        Assert.Equal(0, linker.CreateCount);
        Assert.Equal(other, linker.GetDirectoryLinkTarget(conflicting));
        Assert.False(Directory.Exists(Path.Combine(profile.DirectoryPath, "sessions")));
    }

    [Fact]
    public void Prepare_ReplacesOnlyEmptyOrdinaryDirectories()
    {
        using var temp = new TestLayout();
        ProfileInfo profile = AddAuthenticatedProfile(temp, "475");
        Directory.CreateDirectory(Path.Combine(profile.DirectoryPath, "sessions"));
        var linker = new FakeDirectoryLinker();

        _ = new SharedDesktopProfileService(temp.Paths, linker).Prepare(profile);

        Assert.Equal(Path.Combine(temp.Paths.SharedCodexDirectory, "sessions"),
            linker.GetDirectoryLinkTarget(Path.Combine(profile.DirectoryPath, "sessions")));
    }

    [Theory]
    [InlineData("outside")]
    [InlineData("alternate-auth")]
    [InlineData("nested")]
    public void Prepare_RefusesEscapedOrMismatchedProfilePaths(string kind)
    {
        using var temp = new TestLayout();
        ProfileInfo original = AddAuthenticatedProfile(temp, "475");
        ProfileInfo profile = kind switch
        {
            "outside" => original with { DirectoryPath = Path.Combine(temp.UserProfile, "outside") },
            "alternate-auth" => original with { AuthFilePath = temp.Paths.SharedAuthFile },
            _ => original with { DirectoryPath = Path.Combine(original.DirectoryPath, "nested") },
        };
        var linker = new FakeDirectoryLinker();

        Assert.Throws<InvalidOperationException>(() => new SharedDesktopProfileService(temp.Paths, linker).Prepare(profile));
        Assert.Equal(0, linker.CreateCount);
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("profiles-root")]
    [InlineData("auth")]
    public void Prepare_RefusesLinkedAccountRootsOrCredentials(string kind)
    {
        using var temp = new TestLayout();
        ProfileInfo profile = AddAuthenticatedProfile(temp, "475");
        var linker = new FakeDirectoryLinker();
        string linkedPath = kind switch
        {
            "profile" => profile.DirectoryPath,
            "profiles-root" => temp.Paths.ProfilesDirectory,
            _ => profile.AuthFilePath,
        };
        linker.AddExistingLink(linkedPath, Path.Combine(temp.UserProfile, "outside"));

        Assert.Throws<InvalidOperationException>(() => new SharedDesktopProfileService(temp.Paths, linker).Prepare(profile));
        Assert.Equal(0, linker.CreateCount);
    }

    [Theory]
    [InlineData("invalid-json")]
    [InlineData("{}")]
    [InlineData("{\"tokens\":{\"account_id\":\"\"}}")]
    public void Prepare_RefusesUnknownIdentityWithoutChangingFiles(string auth)
    {
        using var temp = new TestLayout();
        temp.AddProfile("475", auth);
        ProfileInfo profile = new ProfileDiscoveryService(temp.Paths.ProfilesDirectory).GetRequiredProfile("475");
        var linker = new FakeDirectoryLinker();

        Assert.Throws<InvalidOperationException>(() => new SharedDesktopProfileService(temp.Paths, linker).Prepare(profile));

        Assert.Equal(auth, File.ReadAllText(profile.AuthFilePath));
        Assert.Equal(0, linker.CreateCount);
        Assert.False(Directory.Exists(Path.Combine(temp.Paths.ApplicationDataDirectory, "desktop-instances")));
    }

    private static ProfileInfo AddAuthenticatedProfile(TestLayout temp, string name)
    {
        temp.AddProfile(name, "{\"tokens\":{\"account_id\":\"test-account-" + name + "\"}}");
        return new ProfileDiscoveryService(temp.Paths.ProfilesDirectory).GetRequiredProfile(name);
    }

    private sealed class FakeDirectoryLinker : ISharedProfileDirectoryLinker
    {
        public Dictionary<string, string> Links { get; } = new(StringComparer.OrdinalIgnoreCase);

        public int CreateCount { get; private set; }

        public string? GetDirectoryLinkTarget(string path) => Links.GetValueOrDefault(path);

        public void CreateDirectoryLink(string path, string target)
        {
            Assert.False(Directory.Exists(path));
            Directory.CreateDirectory(path);
            Links.Add(path, target);
            CreateCount++;
        }

        public void AddExistingLink(string path, string target) => Links.Add(path, target);
    }
}
