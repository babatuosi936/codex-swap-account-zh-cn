using System.Text;
using System.Text.Json;
using CodexProfileOverlay.Core.Services;
using Xunit;

namespace CodexProfileOverlay.Tests;

public sealed class MultiInstanceTests
{
    [Fact]
    public void RefreshedTokensStillIdentifyTheSameAccount()
    {
        string directory = Path.Combine(Path.GetTempPath(), "overlay-instance-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string first = Path.Combine(directory, "one.json"), second = Path.Combine(directory, "two.json");
            File.WriteAllText(first, "{\"tokens\":{\"account_id\":\"account-one\",\"access_token\":\"old\"}}");
            File.WriteAllText(second, "{\"tokens\":{\"account_id\":\"account-one\",\"access_token\":\"new\"}}");
            Assert.Equal(InstanceAccountIdentity.Read(first), InstanceAccountIdentity.Read(second));
            File.WriteAllText(second, "{\"tokens\":{\"account_id\":\"account-two\"}}");
            Assert.NotEqual(InstanceAccountIdentity.Read(first), InstanceAccountIdentity.Read(second));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void MissingAndMalformedAuthorizationAreUnknown()
    {
        string file = Path.Combine(Path.GetTempPath(), "overlay-auth-test-" + Guid.NewGuid().ToString("N"));
        Assert.Null(InstanceAccountIdentity.Read(file));
        try
        {
            foreach (string content in new[] { "broken", "{}", "[]", "null", "{\"tokens\":null}", "{\"tokens\":{\"id_token\":\"bad.jwt.token\"}}" })
            {
                File.WriteAllText(file, content);
                Assert.Null(InstanceAccountIdentity.Read(file));
            }
            string claim = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"https://api.openai.com/auth\":{\"chatgpt_account_id\":\"fallback-account\"}}"))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            File.WriteAllText(file, JsonSerializer.Serialize(new { tokens = new { id_token = "header." + claim + ".signature" } }));
            Assert.Equal("fallback-account", InstanceAccountIdentity.Read(file));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void ClosingSelectedInstanceExcludesOtherInstanceAndReusedPid()
    {
        DateTime started = DateTime.UtcNow;
        var root = new DesktopProcessInfo(10, 1, started, "ChatGPT", "Codex.exe", true);
        var child = new DesktopProcessInfo(11, 10, started.AddSeconds(1), "codex", "codex.exe");
        var other = new DesktopProcessInfo(20, 1, started, "ChatGPT", "Codex.exe", true);
        var otherChild = new DesktopProcessInfo(21, 20, started.AddSeconds(1), "codex", "codex.exe");
        Assert.Equal(new[] { 11, 10 }, CodexDesktopProcessPolicy.SelectTargets([root, child, other, otherChild], 99, selectedRoot: root).Select(p => p.Id));
        var reused = root with { StartTimeUtc = started.AddMinutes(1) };
        Assert.Empty(CodexDesktopProcessPolicy.SelectTargets([reused, other], 99, selectedRoot: root));
        Assert.Equal(new[] { 11 }, CodexDesktopProcessPolicy.SelectTargets([child, other, otherChild], 99, [child], root).Select(p => p.Id));
    }
}
