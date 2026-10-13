// Pure launch-policy regression probe. Never launches Codex, reads authentication,
// opens a user's database, or invokes package activation.
using CodexProfileOverlay;
using System.Diagnostics;
using System.Xml.Linq;

string fixture = Path.Combine(Path.GetTempPath(), "overlay-launch-policy-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixture);
int checks = 0;
try
{
    string account = MakeDirectory("account");
    string shared = MakeDirectory("shared");
    string desktop = MakeDirectory("desktop");
    string package = MakeDirectory("package");
    string application = Path.Combine(package, "app", "ChatGPT.exe");
    Directory.CreateDirectory(Path.GetDirectoryName(application)!);
    File.WriteAllText(application, "test fixture only");
    WriteManifest("app\\ChatGPT.exe", duplicate: false);

    IsolatedDesktopLauncher.ValidateLocations(account, shared, desktop);
    Check(true, "Accepts prepared independent directories");
    ExpectFailure(() => IsolatedDesktopLauncher.ValidateLocations(account, shared, account),
        "Rejects desktop cache shared with account state");
    ExpectFailure(() => IsolatedDesktopLauncher.ValidateLocations(account, shared, shared),
        "Rejects desktop cache shared with history state");
    ExpectFailure(() => IsolatedDesktopLauncher.ValidateLocations("relative-account", shared, desktop),
        "Rejects relative account directories");
    ExpectFailure(() => IsolatedDesktopLauncher.ValidateLocations(account, shared, Path.Combine(fixture, "not-created")),
        "Rejects absent directories rather than opening the default profile");

    var request = new IsolatedDesktopLauncher.LaunchRequest("OpenAI.Codex_2p2nqsd0c76g0", "App", package,
        application, account, shared, desktop, Path.Combine(fixture, "unused.json"), Guid.NewGuid().ToString("N"));
    string? oldHome = Environment.GetEnvironmentVariable("CODEX_HOME");
    string? oldSqlite = Environment.GetEnvironmentVariable("CODEX_SQLITE_HOME");
    string? oldDesktop = Environment.GetEnvironmentVariable("CODEX_ELECTRON_USER_DATA_PATH");
    string? oldNode = Environment.GetEnvironmentVariable("ELECTRON_RUN_AS_NODE");
    string[] authVariables = ["OPENAI_API_KEY", "CODEX_API_KEY", "CODEX_ACCESS_TOKEN"];
    var originalAuthEnvironment = authVariables.ToDictionary(key => key, Environment.GetEnvironmentVariable);
    try
    {
        Environment.SetEnvironmentVariable("CODEX_HOME", "inherited-home");
        Environment.SetEnvironmentVariable("CODEX_SQLITE_HOME", "inherited-database");
        Environment.SetEnvironmentVariable("CODEX_ELECTRON_USER_DATA_PATH", "inherited-desktop");
        Environment.SetEnvironmentVariable("ELECTRON_RUN_AS_NODE", "1");
        foreach (string key in authVariables) Environment.SetEnvironmentVariable(key, "synthetic-parent-only");
        ProcessStartInfo launch = IsolatedDesktopLauncher.BuildChildStartInfo(request);
        Check(!launch.UseShellExecute, "Uses a process-local environment rather than shell activation");
        Check(launch.Environment["CODEX_HOME"] == account && launch.Environment["CODEX_SQLITE_HOME"] == shared
            && launch.Environment["CODEX_ELECTRON_USER_DATA_PATH"] == desktop,
            "Overrides all three inherited account/directory values");
        Check(!launch.Environment.ContainsKey("ELECTRON_RUN_AS_NODE"), "Clears Node-only Electron mode");
        Check(authVariables.All(key => !launch.Environment.ContainsKey(key)), "Does not inherit another account's API credentials");
        Check(authVariables.All(key => Environment.GetEnvironmentVariable(key) == "synthetic-parent-only"), "Leaves parent credentials unchanged");
        Check(launch.ArgumentList.SequenceEqual(["--user-data-dir=" + desktop]),
            "Passes exactly one desktop directory argument without shell quoting");
        Check(Environment.GetEnvironmentVariable("CODEX_HOME") == "inherited-home"
            && Environment.GetEnvironmentVariable("CODEX_SQLITE_HOME") == "inherited-database",
            "Does not mutate the launching application's environment");
    }
    finally
    {
        Environment.SetEnvironmentVariable("CODEX_HOME", oldHome);
        Environment.SetEnvironmentVariable("CODEX_SQLITE_HOME", oldSqlite);
        Environment.SetEnvironmentVariable("CODEX_ELECTRON_USER_DATA_PATH", oldDesktop);
        Environment.SetEnvironmentVariable("ELECTRON_RUN_AS_NODE", oldNode);
        foreach (string key in authVariables) Environment.SetEnvironmentVariable(key, originalAuthEnvironment[key]);
    }

    var selected = IsolatedDesktopLauncher.ReadGuiApplication(package);
    Check(selected.AppId == "App" && selected.ExecutablePath == application,
        "Identifies the application from the registered-package manifest");
    WriteManifest("app\\ChatGPT.exe", duplicate: true);
    ExpectFailure(() => IsolatedDesktopLauncher.ReadGuiApplication(package), "Rejects ambiguous GUI applications");
    string outside = Path.Combine(fixture, "ChatGPT.exe");
    File.WriteAllText(outside, "test fixture only");
    WriteManifest("..\\ChatGPT.exe", duplicate: false);
    ExpectFailure(() => IsolatedDesktopLauncher.ReadGuiApplication(package), "Rejects executable paths escaping the package");

    Check(!IsolatedDesktopLauncher.TryHandleHelper([], out _), "Normal app startup is not handled as a package helper");
    Check(IsolatedDesktopLauncher.TryHandleHelper([IsolatedDesktopLauncher.HelperArgument], out int missing) && missing == 2,
        "Malformed helper invocations fail before normal application startup");
    Check(IsolatedDesktopLauncher.TryHandleHelper([IsolatedDesktopLauncher.HelperArgument, "not base64"], out int malformed)
        && malformed == 2, "Malformed payloads never reach client creation");
    Check(IsolatedDesktopLauncher.TryHandleHelper(["unexpected", IsolatedDesktopLauncher.HelperArgument], out int displaced)
        && displaced == 2, "Misplaced helper arguments cannot activate the normal overlay");

    var linker = new WindowsSharedDirectoryLinker();
    string sharedPicture = Path.Combine(shared, "original.png");
    File.WriteAllBytes(sharedPicture, [2, 7, 11, 19]);
    string linked = Path.Combine(account, "共享 附件");
    linker.CreateDirectoryLink(linked, shared);
    Check(linker.GetDirectoryLinkTarget(linked) == shared, "Native junction resolves the exact shared directory");
    Check(File.ReadAllBytes(Path.Combine(linked, "original.png")).SequenceEqual(new byte[] { 2, 7, 11, 19 }),
        "Shared images are read through the link without copying their bytes");
    Directory.Delete(linked, recursive: false);
    Check(File.Exists(sharedPicture), "Removing only a test junction preserves its target");

    Console.WriteLine($"PASS: {checks} isolated desktop launch-policy checks. No client was launched.");
    return 0;

    string MakeDirectory(string name)
    {
        string path = Path.Combine(fixture, name);
        Directory.CreateDirectory(path);
        return path;
    }
    void WriteManifest(string executable, bool duplicate)
    {
        var apps = new XElement("Applications", new XElement("Application",
            new XAttribute("Id", "App"), new XAttribute("Executable", executable)));
        if (duplicate) apps.Add(new XElement("Application", new XAttribute("Id", "Other"), new XAttribute("Executable", executable)));
        new XDocument(new XElement("Package", apps)).Save(Path.Combine(package, "AppxManifest.xml"));
    }
    void Check(bool success, string name)
    {
        if (!success) throw new InvalidOperationException(name);
        checks++;
    }
    void ExpectFailure(Action action, string name)
    {
        try { action(); }
        catch (InvalidOperationException) { checks++; return; }
        throw new InvalidOperationException(name);
    }
}
finally
{
    // The exact fresh fixture is checked before deleting only our own test files.
    string fullFixture = Path.GetFullPath(fixture);
    string temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
    if (fullFixture.StartsWith(temp + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        && Path.GetFileName(fullFixture).StartsWith("overlay-launch-policy-test-", StringComparison.Ordinal))
        Directory.Delete(fullFixture, recursive: true);
}
