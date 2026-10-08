using Microsoft.Data.Sqlite;

namespace CodexProfileOverlay.Core.Services;

public sealed class SharedCodexStateMigrationService
{
    private const string LegacyStateDirectoryName = "codex-state";
    private const string MigrationMarkerFileName = "shared-codex-state-v1.migrated";

    private static readonly string[] SharedDirectories =
    [
        "archived_sessions",
        "attachments",
        "sessions",
    ];

    private static readonly string[] SharedDatabaseTables =
    [
        "threads",
        "thread_dynamic_tools",
        "thread_spawn_edges",
    ];

    private readonly AppPaths paths;

    public SharedCodexStateMigrationService(AppPaths paths)
    {
        this.paths = paths;
    }

    public SharedCodexStateMigrationResult MigrateLegacyProfileState()
    {
        string markerFile = Path.Combine(paths.ApplicationDataDirectory, MigrationMarkerFileName);
        if (File.Exists(markerFile))
        {
            return SharedCodexStateMigrationResult.AlreadyCompleted;
        }

        Directory.CreateDirectory(paths.SharedCodexDirectory);
        string[] legacyStateDirectories = Directory.Exists(paths.ProfilesDirectory)
            ? Directory.EnumerateDirectories(paths.ProfilesDirectory)
                .Select(profileDirectory => Path.Combine(profileDirectory, LegacyStateDirectoryName))
                .Where(Directory.Exists)
                .ToArray()
            : [];

        int copiedFiles = 0;
        int importedThreads = 0;
        foreach (string legacyStateDirectory in legacyStateDirectories)
        {
            foreach (string directoryName in SharedDirectories)
            {
                copiedFiles += CopyMissingFiles(
                    Path.Combine(legacyStateDirectory, directoryName),
                    Path.Combine(paths.SharedCodexDirectory, directoryName));
            }

            copiedFiles += MergeSessionIndex(legacyStateDirectory);
            importedThreads += MergeThreadDatabases(legacyStateDirectory);
        }

        Directory.CreateDirectory(paths.ApplicationDataDirectory);
        File.WriteAllText(
            markerFile,
            $"completedAt={DateTimeOffset.UtcNow:O}{Environment.NewLine}legacyProfiles={legacyStateDirectories.Length}{Environment.NewLine}");

        return new SharedCodexStateMigrationResult(
            WasAlreadyCompleted: false,
            LegacyProfileCount: legacyStateDirectories.Length,
            CopiedFileCount: copiedFiles,
            ImportedDatabaseRowCount: importedThreads);
    }

    private int MergeThreadDatabases(string legacyStateDirectory)
    {
        string? targetDatabase = FindLatestStateDatabase(paths.SharedCodexDirectory);
        if (targetDatabase is null)
        {
            return 0;
        }

        int importedRows = 0;
        foreach (string sourceDatabase in EnumerateStateDatabases(legacyStateDirectory))
        {
            if (Path.GetFullPath(sourceDatabase).Equals(Path.GetFullPath(targetDatabase), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            importedRows += MergeDatabase(targetDatabase, sourceDatabase);
        }

        return importedRows;
    }

    private static int MergeDatabase(string targetDatabase, string sourceDatabase)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = targetDatabase,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using (SqliteCommand busyTimeout = connection.CreateCommand())
        {
            busyTimeout.CommandText = "PRAGMA busy_timeout = 5000;";
            _ = busyTimeout.ExecuteNonQuery();
        }

        using (SqliteCommand attach = connection.CreateCommand())
        {
            attach.CommandText = "ATTACH DATABASE $source AS legacy;";
            _ = attach.Parameters.AddWithValue("$source", sourceDatabase);
            _ = attach.ExecuteNonQuery();
        }

        int importedRows = 0;
        try
        {
            using SqliteTransaction transaction = connection.BeginTransaction();
            foreach (string table in SharedDatabaseTables)
            {
                string[] targetColumns = ReadColumns(connection, "main", table);
                if (targetColumns.Length == 0)
                {
                    continue;
                }

                HashSet<string> sourceColumns = ReadColumns(connection, "legacy", table)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                string[] commonColumns = targetColumns.Where(sourceColumns.Contains).ToArray();
                if (commonColumns.Length == 0)
                {
                    continue;
                }

                string columns = string.Join(", ", commonColumns.Select(QuoteIdentifier));
                using SqliteCommand merge = connection.CreateCommand();
                merge.Transaction = transaction;
                merge.CommandText = $"INSERT OR IGNORE INTO main.{QuoteIdentifier(table)} ({columns}) SELECT {columns} FROM legacy.{QuoteIdentifier(table)};";
                importedRows += merge.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        finally
        {
            using SqliteCommand detach = connection.CreateCommand();
            detach.CommandText = "DETACH DATABASE legacy;";
            _ = detach.ExecuteNonQuery();
        }

        return importedRows;
    }

    private static string[] ReadColumns(SqliteConnection connection, string database, string table)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"PRAGMA {QuoteIdentifier(database)}.table_info({QuoteIdentifier(table)});";
        using SqliteDataReader reader = command.ExecuteReader();
        var columns = new List<string>();
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        return [.. columns];
    }

    private static string QuoteIdentifier(string value)
        => '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';

    private int MergeSessionIndex(string legacyStateDirectory)
    {
        string source = Path.Combine(legacyStateDirectory, "session_index.jsonl");
        if (!File.Exists(source))
        {
            return 0;
        }

        string destination = Path.Combine(paths.SharedCodexDirectory, "session_index.jsonl");
        var existing = File.Exists(destination)
            ? File.ReadLines(destination).Where(static line => !string.IsNullOrWhiteSpace(line)).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        string[] missing = File.ReadLines(source)
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Where(existing.Add)
            .ToArray();
        if (missing.Length == 0)
        {
            return 0;
        }

        Directory.CreateDirectory(paths.SharedCodexDirectory);
        File.AppendAllLines(destination, missing);
        return 1;
    }

    private static int CopyMissingFiles(string sourceDirectory, string destinationDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return 0;
        }

        int copiedFiles = 0;
        foreach (string sourceFile in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);
            string destinationFile = Path.Combine(destinationDirectory, relativePath);
            if (File.Exists(destinationFile))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(sourceFile, destinationFile, overwrite: false);
            copiedFiles++;
        }

        return copiedFiles;
    }

    private static string? FindLatestStateDatabase(string directory)
        => EnumerateStateDatabases(directory)
            .OrderByDescending(GetStateDatabaseVersion)
            .FirstOrDefault();

    private static IEnumerable<string> EnumerateStateDatabases(string directory)
        => Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "state_*.sqlite", SearchOption.TopDirectoryOnly)
            : [];

    private static int GetStateDatabaseVersion(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        return int.TryParse(name.AsSpan("state_".Length), out int version) ? version : -1;
    }
}

public sealed record SharedCodexStateMigrationResult(
    bool WasAlreadyCompleted,
    int LegacyProfileCount,
    int CopiedFileCount,
    int ImportedDatabaseRowCount)
{
    public static SharedCodexStateMigrationResult AlreadyCompleted { get; } = new(true, 0, 0, 0);
}
