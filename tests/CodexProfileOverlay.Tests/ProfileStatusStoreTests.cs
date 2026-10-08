using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay.Tests;

public sealed class ProfileStatusStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsStatusDocument()
    {
        using var temp = new TempDirectory();
        string statusFile = Path.Combine(temp.Path, "profile-status.json");
        var store = new ProfileStatusStore(statusFile);

        var document = new ProfileStatusDocument
        {
            Profiles =
            {
                new ProfileStatusMetadata { ProfileId = "work", ManualLabel = "Ready", ManualNote = "Test note" },
                new ProfileStatusMetadata { ProfileId = "personal", ManualEmoji = "🟡" },
            },
            Snapshots =
            {
                ["work"] = new UsageSnapshot
                {
                    Windows =
                    {
                        new UsageLimitWindow { Name = "5h", Duration = TimeSpan.FromHours(5), RemainingPercent = 75 },
                        new UsageLimitWindow { Name = "7d", Duration = TimeSpan.FromDays(7), RemainingPercent = 50 },
                    },
                    CapturedAt = DateTimeOffset.UtcNow,
                },
            },
        };
        store.Save(document);

        var loaded = store.Load();

        Assert.Equal(2, loaded.Profiles.Count);
        Assert.Equal("work", loaded.Profiles[0].ProfileId);
        Assert.Equal("Ready", loaded.Profiles[0].ManualLabel);
        Assert.Equal("Test note", loaded.Profiles[0].ManualNote);
        Assert.Single(loaded.Snapshots);
        Assert.Equal(3, loaded.SchemaVersion);
        Assert.Equal(75, loaded.Snapshots["work"].Windows[0].RemainingPercent);
    }

    [Fact]
    public void Load_VersionTwoEnablesAutomaticRefreshOnceAndPreservesLaterOptOut()
    {
        using var temp = new TempDirectory();
        string statusFile = Path.Combine(temp.Path, "profile-status.json");
        File.WriteAllText(statusFile, """
            {
              "schemaVersion": 2,
              "profiles": [
                { "profileId": "work", "automaticRefreshEnabled": false }
              ],
              "snapshots": {}
            }
            """);
        var store = new ProfileStatusStore(statusFile);

        ProfileStatusDocument migrated = store.Load();
        Assert.Equal(3, migrated.SchemaVersion);
        Assert.True(migrated.Profiles.Single().AutomaticRefreshEnabled);

        migrated.Profiles.Single().AutomaticRefreshEnabled = false;
        store.Save(migrated);

        Assert.False(store.Load().Profiles.Single().AutomaticRefreshEnabled);
    }

    [Fact]
    public void Load_NonExistentFileReturnsEmptyDocument()
    {
        using var temp = new TempDirectory();
        string statusFile = Path.Combine(temp.Path, "profile-status.json");
        var store = new ProfileStatusStore(statusFile);

        var loaded = store.Load();

        Assert.Empty(loaded.Profiles);
        Assert.Empty(loaded.Snapshots);
    }

    [Fact]
    public void GetOrCreateStatus_CreatesWhenMissing()
    {
        using var temp = new TempDirectory();
        var store = new ProfileStatusStore(Path.Combine(temp.Path, "status.json"));
        var document = new ProfileStatusDocument();

        var status = store.GetOrCreateStatus(document, "new-profile");

        Assert.Equal("new-profile", status.ProfileId);
        Assert.Single(document.Profiles);
    }

    [Fact]
    public void GetOrCreateStatus_ReturnsExistingWhenPresent()
    {
        using var temp = new TempDirectory();
        var store = new ProfileStatusStore(Path.Combine(temp.Path, "status.json"));
        var document = new ProfileStatusDocument
        {
            Profiles = { new ProfileStatusMetadata { ProfileId = "existing", ManualLabel = "Ready" } },
        };

        var status = store.GetOrCreateStatus(document, "existing");

        Assert.Equal("existing", status.ProfileId);
        Assert.Equal("Ready", status.ManualLabel);
        Assert.Single(document.Profiles); // No duplicate created
    }
}
