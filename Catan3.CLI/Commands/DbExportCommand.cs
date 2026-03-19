using Microsoft.Data.Sqlite;

namespace Catan3.CLI.Commands;

/// <summary>
/// Exports saved games from the local SQLite database as .catan seed files.
/// Use this to promote a locally-played game into Default Data/Games/ so it
/// gets seeded on every fresh database install.
///
/// Usage examples:
///   catan_cli db-export --name "Test One"
///   catan_cli db-export --all
///   catan_cli db-export --name "My Game" --db path/to/catan.db --out path/to/Games/
/// </summary>
public static class DbExportCommand
{
    private const string DefaultDbPath = "Catan3.GameService/Data/catan.db";
    private const string DefaultOutDir = "Catan3.GameService/Default Data/Games";

    /// <summary>
    /// Exports one or all saved games from the database to .catan files.
    /// </summary>
    public static async Task RunAsync(string? name, bool all, string dbPath, string outDir)
    {
        var resolvedDb = ResolveFromRepoRoot(dbPath);
        var resolvedOut = ResolveFromRepoRoot(outDir);

        if (!File.Exists(resolvedDb))
        {
            Console.Error.WriteLine($"❌ Database not found: {resolvedDb}");
            Environment.Exit(1);
        }

        Directory.CreateDirectory(resolvedOut);

        if (!all && string.IsNullOrWhiteSpace(name))
        {
            Console.Error.WriteLine("❌ Specify --name <game-name> or --all.");
            Environment.Exit(1);
        }

        var games = await LoadGamesAsync(resolvedDb, name, all);

        if (games.Count == 0)
        {
            var qualifier = all ? "any saved games" : $"a game named \"{name}\"";
            Console.Error.WriteLine($"❌ Database contains no {qualifier}.");
            Environment.Exit(1);
        }

        foreach (var (gameName, data) in games)
        {
            var safeName = MakeSafeFileName(gameName);
            var outPath = Path.Combine(resolvedOut, $"{safeName}.catan");
            await File.WriteAllBytesAsync(outPath, data);
            Console.WriteLine($"✅ Exported \"{gameName}\" → {outPath} ({data.Length / 1024.0:F1} KB)");
        }
    }

    private static async Task<List<(string Name, byte[] Data)>> LoadGamesAsync(
        string dbPath, string? name, bool all)
    {
        var results = new List<(string, byte[])>();

        var connectionString = $"Data Source={dbPath}";
        await using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync();

        var sql = all
            ? """
              SELECT m.GameName, d.CompressedData
              FROM GameSaveMetadata m
              JOIN GameSaveData d ON m.GameDataId = d.Id
              ORDER BY m.SavedAt DESC
              """
            : """
              SELECT m.GameName, d.CompressedData
              FROM GameSaveMetadata m
              JOIN GameSaveData d ON m.GameDataId = d.Id
              WHERE m.GameName = @name
              ORDER BY m.SavedAt DESC
              LIMIT 1
              """;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (!all)
            cmd.Parameters.AddWithValue("@name", name!);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var gameName = reader.GetString(0);
            var data = (byte[])reader.GetValue(1);
            results.Add((gameName, data));
        }

        return results;
    }

    /// <summary>
    /// Resolves a path relative to the repo root (two levels up from the CLI bin directory,
    /// or relative to the current working directory when run via dotnet run / catan.ps1).
    /// </summary>
    private static string ResolveFromRepoRoot(string path)
    {
        if (Path.IsPathRooted(path))
            return path;

        // When invoked from repo root (catan.ps1 or dotnet run from repo root), CWD is correct.
        // Fall back to walking up from the assembly location if CWD doesn't look right.
        var fromCwd = Path.GetFullPath(path);
        if (File.Exists(fromCwd) || Directory.Exists(fromCwd) || path.EndsWith(".db"))
            return fromCwd;

        // Walk up from assembly to find the repo root (contains catan.ps1)
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 6; i++)
        {
            var candidate = Path.Combine(dir, path);
            if (File.Exists(candidate) || Directory.Exists(Path.GetDirectoryName(candidate)!))
                return Path.GetFullPath(candidate);
            dir = Path.GetDirectoryName(dir) ?? dir;
        }

        return fromCwd;
    }

    private static string MakeSafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c)).Trim();
    }
}
