using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using PossiblyMaybe.Domain.SportPacks;

namespace PossiblyMaybe.Domain;

/// <summary>
/// R10 look-once vault: each league's <see cref="SportPack.TierCHoldoutSeason"/> is sealed.
/// Simulation may touch it only with <c>--final</c> + a strategy hash; that pair is one-use.
/// SkipSeason stays out of train folds (see <see cref="NestedWalkForward"/>) but is not a vault burn.
/// T1: file-backed via <see cref="Open"/> so burns survive process restarts.
/// </summary>
public sealed class VaultStore
{
    readonly HashSet<string> _used = new(StringComparer.Ordinal);
    readonly string? _dbPath;

    public VaultStore() : this(null) { }

    VaultStore(string? dbPath) => _dbPath = dbPath;

    /// <summary>Load burns from an append-only vault DB (schema ensured by caller or here).</summary>
    public static VaultStore Open(string dbPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbPath);
        VaultDb.EnsureSchema(dbPath);
        var store = new VaultStore(dbPath);
        store.Load();
        return store;
    }

    static string Key(string league, string strategyHash)
        => $"{league.Trim().ToUpperInvariant()}|{strategyHash.Trim().ToLowerInvariant()}";

    static (string League, string Hash) Normalize(string league, string strategyHash)
        => (league.Trim().ToUpperInvariant(), strategyHash.Trim().ToLowerInvariant());

    public bool HasUsed(string league, string strategyHash)
        => _used.Contains(Key(league, strategyHash));

    public void RecordUse(string league, string strategyHash)
    {
        var (lg, hash) = Normalize(league, strategyHash);
        var key = Key(lg, hash);

        if (_dbPath is null)
        {
            if (!_used.Add(key))
                throw new VaultAlreadyUsedException(league, strategyHash);
            return;
        }

        // File is authority: UNIQUE(League, StrategyHash) refuses a second burn across restarts.
        try
        {
            using var con = VaultDb.OpenConnection(_dbPath);
            using var cmd = con.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO VaultBurns (League, StrategyHash, BurnedAt)
                VALUES ($league, $hash, $at);
                """;
            cmd.Parameters.AddWithValue("$league", lg);
            cmd.Parameters.AddWithValue("$hash", hash);
            cmd.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
            cmd.ExecuteNonQuery();
            _used.Add(key);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // SQLITE_CONSTRAINT
        {
            _used.Add(key);
            throw new VaultAlreadyUsedException(league, strategyHash);
        }
    }

    void Load()
    {
        if (_dbPath is null) return;
        using var con = VaultDb.OpenConnection(_dbPath);
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT League, StrategyHash FROM VaultBurns;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            _used.Add(Key(reader.GetString(0), reader.GetString(1)));
    }
}

public sealed class VaultSealedException : InvalidOperationException
{
    public VaultSealedException(string league, string season)
        : base(
            $"Vault sealed: {league} season {season} is look-once. " +
            "Re-run with --final and a registered strategy hash (one use per league×hash).")
    {
        League = league;
        Season = season;
    }

    public string League { get; }
    public string Season { get; }
}

public sealed class VaultAlreadyUsedException : InvalidOperationException
{
    public VaultAlreadyUsedException(string league, string strategyHash)
        : base(
            $"Vault already used: league={league} strategyHash={strategyHash}. " +
            "A look-once season cannot be scored twice for the same strategy.")
    {
        League = league;
        StrategyHash = strategyHash;
    }

    public string League { get; }
    public string StrategyHash { get; }
}

public static class Vault
{
    /// <summary>True when <paramref name="season"/> equals the pack's Tier C holdout (and holdout is set).</summary>
    public static bool IsSealed(SportPack pack, string season)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var h = pack.TierCHoldoutSeason;
        if (string.IsNullOrWhiteSpace(h) || h.Equals("none", StringComparison.OrdinalIgnoreCase))
            return false;
        return season.Equals(h, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSealed(string league, string season)
        => SportPackRegistry.TryGet(league, out var pack) && IsSealed(pack, season);

    /// <summary>
    /// Infer the pack's season label for a calendar day. Matches holdout string shape
    /// (calendar year, <c>YYYY-YY</c>, or <c>YYYY-YYYY</c>).
    /// </summary>
    public static string InferSeason(SportPack pack, string yyyymmdd)
    {
        ArgumentNullException.ThrowIfNull(pack);
        if (yyyymmdd.Length != 8 || !yyyymmdd.All(char.IsDigit))
            throw new ArgumentException("yyyymmdd must be 8 digits.", nameof(yyyymmdd));

        var y = int.Parse(yyyymmdd.AsSpan(0, 4));
        var m = int.Parse(yyyymmdd.AsSpan(4, 2));
        var sample = pack.TierCHoldoutSeason;
        if (string.IsNullOrWhiteSpace(sample) || sample.Equals("none", StringComparison.OrdinalIgnoreCase))
            return y.ToString();

        // European / academic seasons: Jul–Jun (month ≥ 7 starts the new label).
        if (sample.Contains('-', StringComparison.Ordinal))
        {
            var start = m >= 7 ? y : y - 1;
            var parts = sample.Split('-', 2);
            if (parts.Length == 2 && parts[1].Length == 4)
                return $"{start}-{start + 1}";
            if (parts.Length == 2 && parts[1].Length == 2)
                return $"{start}-{(start + 1) % 100:D2}";
        }

        return y.ToString();
    }

    public static string InferSeason(string league, string yyyymmdd)
        => InferSeason(SportPackRegistry.Get(league), yyyymmdd);

    /// <summary>SHA-256 hex (16 chars) over strategy id + params — registered fingerprint for --final.</summary>
    public static string StrategyHash(string strategyId, string? paramsJson = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyId);
        var payload = strategyId.Trim() + "\n" + (paramsJson ?? "");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant()[..16];
    }

    /// <summary>
    /// Simulation touching a sealed season: require <paramref name="final"/> and record one use.
    /// Second --final for the same league×hash throws <see cref="VaultAlreadyUsedException"/>.
    /// </summary>
    public static void AuthorizeSimulationDate(
        VaultStore store,
        string league,
        string yyyymmdd,
        string strategyHash,
        bool final)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (!SportPackRegistry.TryGet(league, out var pack))
            return;

        var season = InferSeason(pack, yyyymmdd);
        if (!IsSealed(pack, season))
            return;

        if (!final)
            throw new VaultSealedException(league, season);

        ArgumentException.ThrowIfNullOrWhiteSpace(strategyHash);
        store.RecordUse(league, strategyHash);
    }
}
