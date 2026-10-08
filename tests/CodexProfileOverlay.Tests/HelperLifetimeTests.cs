using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay.Tests;

public sealed class HelperLifetimeTests
{
    [Fact]
    public void SameDesktopJobIsRejectedButIndependentExplorerLaunchIsAllowed()
    {
        DesktopProcessInfo[] snapshot = [new(10, 1, null, "ChatGPT", null, true), new(20, 1, null, "CodexProfileOverlay", null)];
        Assert.True(CodexDesktopProcessPolicy.HasLifetimeDependency(snapshot, 20, [10, 20]));
        Assert.False(CodexDesktopProcessPolicy.HasLifetimeDependency(snapshot, 20, [20]));
    }

    [Fact]
    public void DesktopAncestorIsRejectedEvenAcrossIntermediateShell()
    {
        DesktopProcessInfo[] snapshot = [new(10, 1, null, "ChatGPT", null, true), new(11, 10, null, "pwsh", null), new(20, 11, null, "CodexProfileOverlay", null)];
        Assert.True(CodexDesktopProcessPolicy.HasLifetimeDependency(snapshot, 20, []));
    }

    [Fact]
    public void EstablishedPackagedDataIsUsedFromEitherLaunchContext()
    {
        using var temp = new TempDirectory();
        string defaultPath = Path.Combine(temp.Path, "CodexProfileOverlay");
        Assert.Equal(defaultPath, AppPaths.ResolveApplicationDataDirectory(temp.Path));
        string packaged = Path.Combine(temp.Path, "Packages", "OpenAI.Codex_2p2nqsd0c76g0", "LocalCache", "Local", "CodexProfileOverlay");
        Directory.CreateDirectory(packaged);
        File.WriteAllText(Path.Combine(packaged, "settings.json"), "{}");
        Assert.Equal(defaultPath, AppPaths.ResolveApplicationDataDirectory(temp.Path));
        File.WriteAllText(Path.Combine(packaged, "active-profile.txt"), "test");
        Assert.Equal(packaged, AppPaths.ResolveApplicationDataDirectory(temp.Path));
        var paths = new AppPaths(temp.Path, temp.Path, packaged);
        Assert.Equal(packaged, paths.ApplicationDataDirectory);
        Assert.Equal(Path.Combine(temp.Path, ".codex", "auth.json"), paths.SharedAuthFile);
        Assert.Equal(Path.Combine(temp.Path, ".codex-profiles"), paths.ProfilesDirectory);
    }
}
