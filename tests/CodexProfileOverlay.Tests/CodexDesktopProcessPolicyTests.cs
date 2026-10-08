using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay.Tests;

public sealed class CodexDesktopProcessPolicyTests
{
    public const string Desktop = @"C:\Program Files\WindowsApps\OpenAI.Codex_26.1002.7124.0_x64__publisher\app\ChatGPT.exe";
    private static readonly DateTime Started = new(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(Desktop, null, null, null, true)]
    [InlineData(@"D:\Apps\Codex\Codex.exe", "Codex", "Codex", "OpenAI OpCo, LLC", true)]
    [InlineData(@"C:\Program Files\WindowsApps\OpenAI.ChatGPT-Desktop_1_x64__publisher\app\ChatGPT.exe", "ChatGPT", "ChatGPT", "OpenAI OpCo, LLC", false)]
    [InlineData(@"C:\Program Files\WindowsApps\OpenAI.Codex_1_x64__publisher\app\resources\codex.exe", "Codex", "Codex", "OpenAI OpCo, LLC", false)]
    [InlineData(@"C:\Users\user\.vscode\extensions\openai.chatgpt\codex.exe", "Codex", "Codex", "OpenAI OpCo, LLC", false)]
    [InlineData(@"C:\Tools\codex.exe", null, null, null, false)]
    [InlineData(@"C:\Tools\CodexHelper.exe", "Codex", "Codex", "OpenAI", false)]
    [InlineData(@"C:\Tools\ChatGPT.exe", "ChatGPT", "ChatGPT", "OpenAI", false)]
    [InlineData(@"C:\Tools\Codex.exe", "Codex", "Codex", "Someone Else", false)]
    [InlineData(null, "Codex", "Codex", "OpenAI", false)]
    public void OfficialDesktopRecognition_UsesInstallationIdentity(
        string? path, string? product, string? description, string? company, bool expected)
    {
        Assert.Equal(expected, CodexDesktopProcessPolicy.IsOfficialDesktopExecutable(path, product, description, company));
    }

    [Theory]
    [InlineData("OpenAI.Codex_2p2nqsd0c76g0!App", true)]
    [InlineData("OpenAI.ChatGPT-Desktop_2p2nqsd0c76g0!ChatGPT", false)]
    [InlineData("Local.Codex.IsolatedVSCode.Launcher", false)]
    [InlineData("OpenAI.Codex_publisher!App --something", false)]
    [InlineData(null, false)]
    public void AppIdRecognition_DoesNotDependOnTheChatGptDisplayName(string? appId, bool expected)
    {
        Assert.Equal(expected, CodexDesktopProcessPolicy.IsOfficialAppId(appId));
    }

    [Fact]
    public void SelectTargets_ClosesDesktopTreeAndPreservesEditorsHelpersAndUnrelatedCli()
    {
        DesktopProcessInfo[] snapshot =
        [
            Process(1, 0, "explorer"),
            Process(2, 1, "ChatGPT", desktop: true),
            Process(3, 2, "ChatGPT", desktop: true),
            Process(4, 2, "codex"),
            Process(5, 4, "node_repl"),
            Process(6, 5, "codex"),
            Process(7, 2, "Code"),
            Process(8, 7, "codex"),
            Process(9, 5, "CodexProfileOverlay"),
            Process(10, 9, "codex"),
            Process(11, 1, "codex"),
            Process(12, 1, "ChatGPT"),
            Process(13, 2, "msedge"),
            Process(14, 13, "browser-child"),
            Process(15, 4, "cmd"),
            Process(16, 15, "node"),
            Process(17, 15, "python"),
            Process(18, 4, "pwsh"),
        ];

        IReadOnlyList<DesktopProcessInfo> targets = CodexDesktopProcessPolicy.SelectTargets(snapshot, 9);

        Assert.Equal(new[] { 2, 3, 4, 5, 6 }, targets.Select(process => process.Id).Order().ToArray());
        Assert.True(targets.ToList().FindIndex(process => process.Id == 6) < targets.ToList().FindIndex(process => process.Id == 2));
    }

    [Fact]
    public void SelectTargets_DoesNotClaimOlderChildOfReusedParentPid()
    {
        var parent = Process(1, 0, "ChatGPT", desktop: true) with { StartTimeUtc = Started.AddSeconds(20) };
        var staleChild = Process(2, 1, "codex") with { StartTimeUtc = Started.AddSeconds(10) };

        Assert.Equal(new[] { 1 }, CodexDesktopProcessPolicy.SelectTargets([parent, staleChild], 99).Select(process => process.Id));
    }

    [Fact]
    public void SelectTargets_TracksOrphanedChildrenButRejectsReusedPid()
    {
        var remembered = Process(4, 2, "codex");
        var orphan = Process(4, 2, "codex");
        var child = Process(5, 4, "codex");
        Assert.Equal(new[] { 4, 5 }, CodexDesktopProcessPolicy.SelectTargets([orphan, child], 99, [remembered]).Select(process => process.Id).Order());

        var replacement = orphan with { StartTimeUtc = Started.AddHours(1) };
        Assert.Empty(CodexDesktopProcessPolicy.SelectTargets([replacement, child], 99, [remembered]));
    }

    [Fact]
    public void SelectTargets_NeverClosesOverlayEvenWhenLaunchedByDesktop()
    {
        Assert.Equal(new[] { 1 }, CodexDesktopProcessPolicy.SelectTargets(
            [Process(1, 0, "ChatGPT", desktop: true), Process(2, 1, "helper"), Process(3, 2, "codex")], 2).Select(process => process.Id));
    }

    internal static DesktopProcessInfo Process(int id, int parent, string name, bool desktop = false) =>
        new(id, parent, Started.AddSeconds(id), name, desktop ? Desktop : null, desktop);
}
