using Microsoft.Data.Sqlite;
using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay.Tests;

public sealed class SharedCodexStateMigrationServiceTests
{
    [Fact]
    public void MigrateLegacyProfileState_MergesSessionsIndexAndThreadsOnce()
    {
        using var temp = new TestLayout();
        temp.AddProfile("current", "current-auth");
        temp.AddProfile("legacy", "legacy-auth");

        string sharedSession = Path.Combine(temp.Paths.SharedCodexDirectory, "sessions", "shared.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(sharedSession)!);
        File.WriteAllText(sharedSession, "shared");
        File.WriteAllText(Path.Combine(temp.Paths.SharedCodexDirectory, "session_index.jsonl"), "shared-index" + Environment.NewLine);

        string legacyState = Path.Combine(temp.Paths.ProfilesDirectory, "legacy", "codex-state");
        string legacySession = Path.Combine(legacyState, "sessions", "legacy.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(legacySession)!);
        File.WriteAllText(legacySession, "legacy");
        File.WriteAllText(Path.Combine(legacyState, "session_index.jsonl"), "shared-index" + Environment.NewLine + "legacy-index" + Environment.NewLine);

        string targetDatabase = Path.Combine(temp.Paths.SharedCodexDirectory, "state_5.sqlite");
        string sourceDatabase = Path.Combine(legacyState, "state_5.sqlite");
        CreateThreadDatabase(targetDatabase, "shared-thread");
        CreateThreadDatabase(sourceDatabase, "legacy-thread");

        var service = new SharedCodexStateMigrationService(temp.Paths);
        SharedCodexStateMigrationResult result = service.MigrateLegacyProfileState();
        SharedCodexStateMigrationResult second = service.MigrateLegacyProfileState();

        Assert.False(result.WasAlreadyCompleted);
        Assert.Equal(1, result.ImportedDatabaseRowCount);
        Assert.True(File.Exists(Path.Combine(temp.Paths.SharedCodexDirectory, "sessions", "legacy.jsonl")));
        Assert.Equal(new[] { "shared-index", "legacy-index" }, File.ReadAllLines(Path.Combine(temp.Paths.SharedCodexDirectory, "session_index.jsonl")));
        Assert.Equal(new[] { "legacy-thread", "shared-thread" }, ReadThreadIds(targetDatabase));
        Assert.True(second.WasAlreadyCompleted);
    }

    private static void CreateThreadDatabase(string path, string threadId)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE threads (id TEXT PRIMARY KEY, title TEXT NOT NULL); INSERT INTO threads (id, title) VALUES ($id, $id);";
        _ = command.Parameters.AddWithValue("$id", threadId);
        _ = command.ExecuteNonQuery();
    }

    private static string[] ReadThreadIds(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM threads ORDER BY id;";
        using SqliteDataReader reader = command.ExecuteReader();
        var values = new List<string>();
        while (reader.Read())
        {
            values.Add(reader.GetString(0));
        }

        return [.. values];
    }
}
