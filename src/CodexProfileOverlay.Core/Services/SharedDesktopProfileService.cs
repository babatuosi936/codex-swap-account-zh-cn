using System.Security.Cryptography;
using System.Text;
using CodexProfileOverlay.Core.Models;

namespace CodexProfileOverlay.Core.Services;

public interface ISharedProfileDirectoryLinker
{
    string? GetDirectoryLinkTarget(string path);

    void CreateDirectoryLink(string path, string target);
}

public sealed record PreparedDesktopProfile(
    string CodexHome,
    string SharedSqliteHome,
    string ElectronUserDataDirectory);

/// <summary>Share existing history without importing, rewriting, or merging it.</summary>
public sealed class SharedDesktopProfileService
{
    private static readonly string[] SharedDirectoryNames =
    [
        "sessions",
        "archived_sessions",
        "attachments",
        "thread-writer-locks",
    ];

    private readonly AppPaths paths;
    private readonly ISharedProfileDirectoryLinker directoryLinker;

    public SharedDesktopProfileService(AppPaths paths, ISharedProfileDirectoryLinker directoryLinker)
    {
        this.paths = paths;
        this.directoryLinker = directoryLinker;
    }

    public PreparedDesktopProfile Prepare(ProfileInfo profile)
    {
        PreparedDesktopProfile prepared = Inspect(profile);
        DirectoryLink[] links = GetSharedDirectoryLinks(prepared.CodexHome, prepared.SharedSqliteHome);

        Directory.CreateDirectory(prepared.SharedSqliteHome);
        foreach (DirectoryLink link in links)
        {
            Directory.CreateDirectory(link.Target);
            if (directoryLinker.GetDirectoryLinkTarget(link.Path) is not null)
            {
                continue;
            }

            // Repeat the check immediately before replacing an empty directory.
            ValidateLinkDestination(link);
            if (Directory.Exists(link.Path))
            {
                Directory.Delete(link.Path, recursive: false);
            }

            directoryLinker.CreateDirectoryLink(link.Path, link.Target);
            string? actualTarget = directoryLinker.GetDirectoryLinkTarget(link.Path);
            if (actualTarget is null || !SamePath(actualTarget, link.Target))
            {
                throw new InvalidOperationException("The shared history directory link could not be verified.");
            }
        }

        Directory.CreateDirectory(prepared.ElectronUserDataDirectory);
        return Inspect(profile);
    }

    /// <summary>Validate saved-account paths and history destinations without changing files or directories.</summary>
    public PreparedDesktopProfile Inspect(ProfileInfo profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        string name = ProfileName.RequireValid(profile.Name);
        string profileRoot = Normalize(Path.Combine(paths.ProfilesDirectory, name));
        if (!SamePath(profileRoot, profile.DirectoryPath)
            || !SamePath(Path.Combine(profileRoot, "auth.json"), profile.AuthFilePath))
        {
            throw new InvalidOperationException("The selected account path is outside its saved profile.");
        }

        RequireOrdinaryPath(profileRoot);
        if (!Directory.Exists(profileRoot))
        {
            throw new DirectoryNotFoundException("The selected account directory was not found.");
        }

        RequireOrdinaryPath(profile.AuthFilePath, requireDirectory: false);
        if (InstanceAccountIdentity.Read(profile.AuthFilePath) is null)
        {
            throw new InvalidOperationException("Cannot verify the selected account identity; sign in to this profile first.");
        }

        string sharedRoot = Normalize(paths.SharedCodexDirectory);
        RequireOrdinaryPath(sharedRoot);
        string cacheKey = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(profileRoot.ToUpperInvariant())))[..24].ToLowerInvariant();
        string cacheRoot = Path.Combine(paths.ApplicationDataDirectory, "desktop-instances", cacheKey);
        RequireOrdinaryPath(cacheRoot);

        // Inspect every destination before creating anything. Independent history
        // remains untouched and must never be hidden behind a newly created link.
        foreach (DirectoryLink link in GetSharedDirectoryLinks(profileRoot, sharedRoot))
        {
            RequireOrdinaryPath(link.Target);
            ValidateLinkDestination(link);
        }

        return new PreparedDesktopProfile(profileRoot, sharedRoot, cacheRoot);
    }

    private static DirectoryLink[] GetSharedDirectoryLinks(string profileRoot, string sharedRoot) =>
        SharedDirectoryNames.Select(directoryName => new DirectoryLink(
            Path.Combine(profileRoot, directoryName),
            Path.Combine(sharedRoot, directoryName))).ToArray();

    private void ValidateLinkDestination(DirectoryLink link)
    {
        string? existingTarget = directoryLinker.GetDirectoryLinkTarget(link.Path);
        if (existingTarget is not null)
        {
            if (!SamePath(existingTarget, link.Target))
            {
                throw new InvalidOperationException("This account already links to a different history directory; its history was preserved.");
            }

            return;
        }

        FileAttributes? attributes = ReadAttributes(link.Path);
        if (attributes is null)
        {
            return;
        }

        if ((attributes.Value & FileAttributes.ReparsePoint) != 0
            || (attributes.Value & FileAttributes.Directory) == 0)
        {
            throw new InvalidOperationException("A shared history path is occupied by an unsupported file or link.");
        }

        if (Directory.EnumerateFileSystemEntries(link.Path).Any())
        {
            throw new InvalidOperationException("This account contains independent history; it was preserved without copying or merging.");
        }
    }

    private void RequireOrdinaryPath(string path, bool requireDirectory = true)
    {
        string? current = Normalize(path);
        while (current is not null)
        {
            FileAttributes? attributes = ReadAttributes(current);
            if (directoryLinker.GetDirectoryLinkTarget(current) is not null
                || (attributes is not null && (attributes.Value & FileAttributes.ReparsePoint) != 0))
            {
                throw new InvalidOperationException("Account and data roots must not contain directory links or other reparse points.");
            }

            if (attributes is not null && !SamePath(current, path)
                && (attributes.Value & FileAttributes.Directory) == 0)
            {
                throw new InvalidOperationException("An account or data directory is occupied by a file.");
            }

            current = Directory.GetParent(current)?.FullName;
        }

        FileAttributes? leaf = ReadAttributes(path);
        if (requireDirectory && leaf is not null && (leaf.Value & FileAttributes.Directory) == 0)
        {
            throw new InvalidOperationException("An account or data directory is occupied by a file.");
        }
    }

    private static FileAttributes? ReadAttributes(string path)
    {
        try
        {
            return File.GetAttributes(path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool SamePath(string first, string second) =>
        string.Equals(Normalize(first), Normalize(second), StringComparison.OrdinalIgnoreCase);

    private sealed record DirectoryLink(string Path, string Target);
}
