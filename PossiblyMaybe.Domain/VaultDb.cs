using Microsoft.Data.Sqlite;

namespace PossiblyMaybe.Domain;

/// <summary>
/// T1 durable vault + trial ledger: append-only SQLite file (default <c>data/vault.db</c>).
/// Burns and trial rows survive process restarts; UNIQUE on league×hash refuses a second final.
/// </summary>
public static class VaultDb
{
    public static string DefaultPath(string repoRoot)
        => Path.Combine(repoRoot, "data", "vault.db");

    /// <summary>Open a shared file-backed vault store and trial ledger (schema ensured).</summary>
    public static (VaultStore Vault, TrialLedger Trials) Open(string dbPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbPath);
        var dir = Path.GetDirectoryName(Path.GetFullPath(dbPath));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        EnsureSchema(dbPath);
        return (VaultStore.Open(dbPath), TrialLedger.Open(dbPath));
    }

    internal static void EnsureSchema(string dbPath)
    {
        using var con = OpenConnection(dbPath);
        using var cmd = con.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS VaultBurns (
                League TEXT NOT NULL,
                StrategyHash TEXT NOT NULL,
                BurnedAt TEXT NOT NULL,
                PRIMARY KEY (League, StrategyHash)
            );
            CREATE TABLE IF NOT EXISTS TrialEntries (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                StrategyHash TEXT NOT NULL,
                ParamsJson TEXT NOT NULL,
                DateFrom TEXT NOT NULL,
                DateTo TEXT NOT NULL,
                GridSize INTEGER NOT NULL,
                RecordedAt TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    internal static SqliteConnection OpenConnection(string dbPath)
    {
        var con = new SqliteConnection($"Data Source={dbPath}");
        con.Open();
        return con;
    }
}
