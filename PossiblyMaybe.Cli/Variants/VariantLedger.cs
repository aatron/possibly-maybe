using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Variants;

/// <summary>Shadow (hypothetical) tickets — separate from paper.db. No user confirmation to settle.</summary>
public sealed class ShadowEntry
{
    public int Id { get; set; }
    public string VariantId { get; set; } = "";
    public string Sport { get; set; } = "";
    public string GameKey { get; set; } = "";
    public string? Side { get; set; }
    public double? Line { get; set; }
    public int AmericanOdds { get; set; }
    public double ModelProb { get; set; }
    public double Ev { get; set; }
    public double AbsEdge { get; set; }
    public double? OpenLine { get; set; }
    public double? CurrentLine { get; set; }
    public string? SkipReason { get; set; }
    public DateTimeOffset EvaluatedAt { get; set; }
    public string? Result { get; set; }
    public double? PnL { get; set; }
    public double? CloseLine { get; set; }
    public double? ClvPts { get; set; }
    /// <summary>Book label for CloseLine (R3). Shadow defaults to DraftKings.</summary>
    public string? CloseBook { get; set; }
    public DateTimeOffset? CloseCapturedAt { get; set; }
    public double? MinutesBeforeStart { get; set; }
    public bool CrossBookClose { get; set; }
    public DateTimeOffset? SettledAt { get; set; }
}

/// <summary>One stored model/line snapshot per game×market; variants are filters over these rows.</summary>
public sealed class CandidateEntry
{
    public int Id { get; set; }
    public string Sport { get; set; } = "";
    public string GameKey { get; set; } = "";
    /// <summary>TOTAL / SPREAD / ML — C7 extends store; no new tables.</summary>
    public string Market { get; set; } = "TOTAL";
    public string Yyyymmdd { get; set; } = "";
    public string Home { get; set; } = "";
    public string Away { get; set; } = "";
    public double ModelExp { get; set; }
    public double? LambdaHome { get; set; }
    public double? LambdaAway { get; set; }
    public double? Line { get; set; }
    public double? OpenLine { get; set; }
    public int? DkOverAmerican { get; set; }
    public int? DkUnderAmerican { get; set; }
    public double NbaSigma { get; set; }
    public double MarketWeight { get; set; }
    public double MinAbsEdge { get; set; }
    public double MinEv { get; set; }
    public bool Stale { get; set; }
    /// <summary>Placeholder for wave-B absence / injury guards.</summary>
    public bool? InjuryFlag { get; set; }
    /// <summary>Placeholder for wave-B roster-reset.</summary>
    public bool? RosterFlag { get; set; }
    /// <summary>Placeholder for seasonality / wave calendar.</summary>
    public string? SeasonPhase { get; set; }
    public DateTimeOffset StoredAt { get; set; }
}

/// <summary>Append-only decision row (R2). Never updated; settle via <see cref="DecisionSettlement"/>.</summary>
public sealed class DecisionLogEntry
{
    public long Id { get; set; }
    public Guid RunId { get; set; }
    public DateTimeOffset DecidedAt { get; set; }
    public string GitSha { get; set; } = "";
    public string VariantId { get; set; } = "";
    public int RegistryVersion { get; set; }
    public string Sport { get; set; } = "";
    public string GameKey { get; set; } = "";
    public string? Side { get; set; }
    public double? Line { get; set; }
    public int AmericanOdds { get; set; }
    public double ModelProb { get; set; }
    public double Ev { get; set; }
    public double AbsEdge { get; set; }
    public string? SkipReason { get; set; }
    public int? MaxGameId { get; set; }
    public DateTimeOffset? MaxTipoffIncluded { get; set; }
    /// <summary>Comma-separated odds snapshot ids used at decision time.</summary>
    public string? OddsSnapshotIds { get; set; }
    /// <summary>Compact venue line+price JSON (capture time included).</summary>
    public string? VenueLinesJson { get; set; }
    public string? InjuryRosterHash { get; set; }
    public string? InjuryRosterJson { get; set; }
}

/// <summary>Settlement for a <see cref="DecisionLogEntry"/> — separate table, never mutates the log.</summary>
public sealed class DecisionSettlement
{
    public long Id { get; set; }
    public long DecisionLogId { get; set; }
    public string Result { get; set; } = "";
    public double? PnL { get; set; }
    public double? CloseLine { get; set; }
    public double? ClvPts { get; set; }
    public string? CloseBook { get; set; }
    public DateTimeOffset? CloseCapturedAt { get; set; }
    public double? MinutesBeforeStart { get; set; }
    public bool CrossBookClose { get; set; }
    public DateTimeOffset SettledAt { get; set; }
}

public sealed class VariantDbContext(DbContextOptions<VariantDbContext> options) : DbContext(options)
{
    public DbSet<ShadowEntry> ShadowEntries => Set<ShadowEntry>();
    public DbSet<CandidateEntry> CandidateEntries => Set<CandidateEntry>();
    public DbSet<DecisionLogEntry> DecisionLogEntries => Set<DecisionLogEntry>();
    public DbSet<DecisionSettlement> DecisionSettlements => Set<DecisionSettlement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShadowEntry>(e =>
        {
            e.HasIndex(x => new { x.VariantId, x.GameKey }).IsUnique();
            e.Property(x => x.VariantId).HasMaxLength(32);
            e.Property(x => x.Sport).HasMaxLength(16);
            e.Property(x => x.GameKey).HasMaxLength(128);
            e.Property(x => x.SkipReason).HasMaxLength(32);
        });
        modelBuilder.Entity<CandidateEntry>(e =>
        {
            e.HasIndex(x => new { x.Sport, x.GameKey, x.Market }).IsUnique();
            e.Property(x => x.Sport).HasMaxLength(16);
            e.Property(x => x.GameKey).HasMaxLength(128);
            e.Property(x => x.Market).HasMaxLength(16);
            e.Property(x => x.Yyyymmdd).HasMaxLength(8);
            e.Property(x => x.Home).HasMaxLength(64);
            e.Property(x => x.Away).HasMaxLength(64);
            e.Property(x => x.SeasonPhase).HasMaxLength(32);
        });
        modelBuilder.Entity<DecisionLogEntry>(e =>
        {
            e.HasIndex(x => x.RunId);
            e.HasIndex(x => new { x.VariantId, x.GameKey, x.DecidedAt });
            e.Property(x => x.VariantId).HasMaxLength(32);
            e.Property(x => x.Sport).HasMaxLength(16);
            e.Property(x => x.GameKey).HasMaxLength(128);
            e.Property(x => x.GitSha).HasMaxLength(64);
            e.Property(x => x.SkipReason).HasMaxLength(32);
            e.Property(x => x.InjuryRosterHash).HasMaxLength(64);
        });
        modelBuilder.Entity<DecisionSettlement>(e =>
        {
            e.HasIndex(x => x.DecisionLogId).IsUnique();
            e.Property(x => x.Result).HasMaxLength(8);
            e.Property(x => x.CloseBook).HasMaxLength(32);
        });
    }
}

public static class VariantLedger
{
    public static string DefaultDbPath(string repoRoot) => Path.Combine(repoRoot, "data", "variants.db");

    public static async Task<(VariantDbContext Db, string Path)> OpenAsync(string dbPath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbPath))!);
        var cs = $"Data Source={dbPath}";
        var options = new DbContextOptionsBuilder<VariantDbContext>().UseSqlite(cs).Options;
        var db = new VariantDbContext(options);
        await db.Database.EnsureCreatedAsync(ct);
        // EnsureCreated is a no-op when ShadowEntries already exists — add Candidates additively.
        await EnsureCandidateTableAsync(db, ct);
        await EnsureShadowCloseColumnsAsync(db, ct);
        await EnsureDecisionLogTablesAsync(db, ct);
        return (db, dbPath);
    }

    static async Task EnsureDecisionLogTablesAsync(VariantDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "DecisionLogEntries" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_DecisionLogEntries" PRIMARY KEY AUTOINCREMENT,
                "RunId" TEXT NOT NULL,
                "DecidedAt" TEXT NOT NULL,
                "GitSha" TEXT NOT NULL,
                "VariantId" TEXT NOT NULL,
                "RegistryVersion" INTEGER NOT NULL,
                "Sport" TEXT NOT NULL,
                "GameKey" TEXT NOT NULL,
                "Side" TEXT NULL,
                "Line" REAL NULL,
                "AmericanOdds" INTEGER NOT NULL,
                "ModelProb" REAL NOT NULL,
                "Ev" REAL NOT NULL,
                "AbsEdge" REAL NOT NULL,
                "SkipReason" TEXT NULL,
                "MaxGameId" INTEGER NULL,
                "MaxTipoffIncluded" TEXT NULL,
                "OddsSnapshotIds" TEXT NULL,
                "VenueLinesJson" TEXT NULL,
                "InjuryRosterHash" TEXT NULL,
                "InjuryRosterJson" TEXT NULL
            );
            """, ct);
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "DecisionSettlements" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_DecisionSettlements" PRIMARY KEY AUTOINCREMENT,
                "DecisionLogId" INTEGER NOT NULL,
                "Result" TEXT NOT NULL,
                "PnL" REAL NULL,
                "CloseLine" REAL NULL,
                "ClvPts" REAL NULL,
                "CloseBook" TEXT NULL,
                "CloseCapturedAt" TEXT NULL,
                "MinutesBeforeStart" REAL NULL,
                "CrossBookClose" INTEGER NOT NULL DEFAULT 0,
                "SettledAt" TEXT NOT NULL
            );
            """, ct);
        await db.Database.ExecuteSqlRawAsync(
            """CREATE INDEX IF NOT EXISTS "IX_DecisionLogEntries_RunId" ON "DecisionLogEntries" ("RunId");""", ct);
        await db.Database.ExecuteSqlRawAsync(
            """CREATE UNIQUE INDEX IF NOT EXISTS "IX_DecisionSettlements_DecisionLogId" ON "DecisionSettlements" ("DecisionLogId");""", ct);
    }

    static string? _gitSha;

    public static string ResolveGitSha()
    {
        if (_gitSha is not null) return _gitSha;
        var env = Environment.GetEnvironmentVariable("GIT_COMMIT")
                  ?? Environment.GetEnvironmentVariable("GITHUB_SHA");
        if (!string.IsNullOrWhiteSpace(env))
            return _gitSha = env.Trim();
        try
        {
            var psi = new ProcessStartInfo("git", "rev-parse HEAD")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var p = Process.Start(psi);
            var sha = p?.StandardOutput.ReadToEnd().Trim() ?? "";
            p?.WaitForExit(2000);
            if (sha.Length >= 7) return _gitSha = sha;
        }
        catch { /* */ }
        return _gitSha = "unknown";
    }

    public static string HashPayload(string payload)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>Append-only decision insert. Never updates prior rows.</summary>
    public static async Task<DecisionLogEntry> AppendDecisionAsync(
        VariantDbContext db,
        Guid runId,
        string variantId,
        string sport,
        string gameKey,
        VariantDecision d,
        SlateCandidate? candidate = null,
        int? maxGameId = null,
        DateTimeOffset? maxTipoffIncluded = null,
        string? oddsSnapshotIds = null,
        CancellationToken ct = default)
    {
        var decidedAt = DateTimeOffset.UtcNow;
        // shortcut: venue capture times = decided_at until tracker snapshot ids are threaded through AsOfView
        var venue = new Dictionary<string, object?>
        {
            ["draftkings"] = new
            {
                line = candidate?.Line ?? d.Line,
                openLine = candidate?.OpenLine,
                overAmerican = candidate?.DkOverAmerican,
                underAmerican = candidate?.DkUnderAmerican,
                capturedAt = decidedAt,
            },
        };
        var venueJson = JsonSerializer.Serialize(venue);
        var injuryJson = JsonSerializer.Serialize(new
        {
            injuryFlag = candidate?.InjuryFlag,
            rosterFlag = candidate?.RosterFlag,
            seasonPhase = candidate?.SeasonPhase,
        });
        var row = new DecisionLogEntry
        {
            RunId = runId,
            DecidedAt = decidedAt,
            GitSha = ResolveGitSha(),
            VariantId = variantId,
            RegistryVersion = VariantRegistry.RegistryVersion,
            Sport = sport,
            GameKey = gameKey,
            Side = d.Side,
            Line = d.Line,
            AmericanOdds = d.AmericanOdds,
            ModelProb = d.ModelProb,
            Ev = d.Ev,
            AbsEdge = d.AbsEdge,
            SkipReason = d.Pick ? null : d.SkipReason,
            MaxGameId = maxGameId,
            MaxTipoffIncluded = maxTipoffIncluded,
            OddsSnapshotIds = oddsSnapshotIds,
            VenueLinesJson = venueJson,
            InjuryRosterJson = injuryJson,
            InjuryRosterHash = HashPayload(injuryJson),
        };
        db.DecisionLogEntries.Add(row);
        await db.SaveChangesAsync(ct);
        return row;
    }

    public static async Task AppendSettlementAsync(
        VariantDbContext db,
        long decisionLogId,
        string result,
        double? pnl,
        double? closeLine,
        double? clvPts,
        string? closeBook,
        DateTimeOffset? closeCapturedAt,
        double? minutesBeforeStart,
        bool crossBookClose,
        CancellationToken ct = default)
    {
        if (await db.DecisionSettlements.AnyAsync(s => s.DecisionLogId == decisionLogId, ct))
            return;
        db.DecisionSettlements.Add(new DecisionSettlement
        {
            DecisionLogId = decisionLogId,
            Result = result,
            PnL = pnl,
            CloseLine = closeLine,
            ClvPts = clvPts,
            CloseBook = closeBook,
            CloseCapturedAt = closeCapturedAt,
            MinutesBeforeStart = minutesBeforeStart,
            CrossBookClose = crossBookClose,
            SettledAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }

    static async Task EnsureShadowCloseColumnsAsync(VariantDbContext db, CancellationToken ct)
    {
        foreach (var sql in new[]
                 {
                     "ALTER TABLE ShadowEntries ADD COLUMN CloseBook TEXT",
                     "ALTER TABLE ShadowEntries ADD COLUMN CloseCapturedAt TEXT",
                     "ALTER TABLE ShadowEntries ADD COLUMN MinutesBeforeStart REAL",
                     "ALTER TABLE ShadowEntries ADD COLUMN CrossBookClose INTEGER NOT NULL DEFAULT 0",
                 })
        {
            try { await db.Database.ExecuteSqlRawAsync(sql, ct); }
            catch { /* duplicate column */ }
        }
    }

    /// <summary>EnsureCreated never ALTERs / never adds tables to an existing DB.</summary>
    static async Task EnsureCandidateTableAsync(VariantDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "CandidateEntries" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_CandidateEntries" PRIMARY KEY AUTOINCREMENT,
                "Sport" TEXT NOT NULL,
                "GameKey" TEXT NOT NULL,
                "Market" TEXT NOT NULL DEFAULT 'TOTAL',
                "Yyyymmdd" TEXT NOT NULL,
                "Home" TEXT NOT NULL,
                "Away" TEXT NOT NULL,
                "ModelExp" REAL NOT NULL,
                "LambdaHome" REAL NULL,
                "LambdaAway" REAL NULL,
                "Line" REAL NULL,
                "OpenLine" REAL NULL,
                "DkOverAmerican" INTEGER NULL,
                "DkUnderAmerican" INTEGER NULL,
                "NbaSigma" REAL NOT NULL,
                "MarketWeight" REAL NOT NULL,
                "MinAbsEdge" REAL NOT NULL,
                "MinEv" REAL NOT NULL,
                "Stale" INTEGER NOT NULL,
                "InjuryFlag" INTEGER NULL,
                "RosterFlag" INTEGER NULL,
                "SeasonPhase" TEXT NULL,
                "StoredAt" TEXT NOT NULL
            );
            """, ct);
        // Additive C7: Market column on DBs created by A1 without it.
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "CandidateEntries" ADD COLUMN "Market" TEXT NOT NULL DEFAULT 'TOTAL';""", ct);
        }
        catch
        {
            // Column already present.
        }
        await db.Database.ExecuteSqlRawAsync(
            """DROP INDEX IF EXISTS "IX_CandidateEntries_Sport_GameKey";""", ct);
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_CandidateEntries_Sport_GameKey_Market"
            ON "CandidateEntries" ("Sport", "GameKey", "Market");
            """, ct);
    }

    public static SlateCandidate ToSlate(CandidateEntry e) => new(
        e.Sport, e.Yyyymmdd, e.Home, e.Away,
        e.ModelExp, e.LambdaHome, e.LambdaAway,
        e.Line, e.OpenLine, e.DkOverAmerican, e.DkUnderAmerican,
        e.NbaSigma, e.MarketWeight, e.MinAbsEdge, e.MinEv, e.Stale,
        e.InjuryFlag, e.RosterFlag, e.SeasonPhase, Market: e.Market);

    public static async Task UpsertCandidateAsync(
        VariantDbContext db, SlateCandidate c, CancellationToken ct = default)
    {
        var key = VariantRegistry.GameKey(c);
        var market = string.IsNullOrEmpty(c.Market) ? "TOTAL" : c.Market;
        var row = await db.CandidateEntries
            .FirstOrDefaultAsync(x => x.Sport == c.Sport && x.GameKey == key && x.Market == market, ct);
        if (row is null)
        {
            row = new CandidateEntry { Sport = c.Sport, GameKey = key, Market = market };
            db.CandidateEntries.Add(row);
        }

        row.Market = market;
        row.Yyyymmdd = c.Yyyymmdd;
        row.Home = c.Home;
        row.Away = c.Away;
        row.ModelExp = c.ModelExp;
        row.LambdaHome = c.LambdaHome;
        row.LambdaAway = c.LambdaAway;
        row.Line = c.Line;
        row.OpenLine = c.OpenLine;
        row.DkOverAmerican = c.DkOverAmerican;
        row.DkUnderAmerican = c.DkUnderAmerican;
        row.NbaSigma = c.NbaSigma;
        row.MarketWeight = c.MarketWeight;
        row.MinAbsEdge = c.MinAbsEdge;
        row.MinEv = c.MinEv;
        row.Stale = c.Stale;
        row.InjuryFlag = c.InjuryFlag;
        row.RosterFlag = c.RosterFlag;
        row.SeasonPhase = c.SeasonPhase;
        row.StoredAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Sports whose last <see cref="Guards.ShutoffWindow"/> settled CLV pts (signal variant) average &lt; 0.
    /// </summary>
    public static async Task<HashSet<string>> PausedLeaguesAsync(
        VariantDbContext db,
        string signalVariantId = Guards.ShutoffSignalVariantId,
        CancellationToken ct = default)
    {
        var paused = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sports = await db.ShadowEntries.AsNoTracking()
            .Where(e => e.VariantId == signalVariantId
                        && e.Result != null
                        && e.ClvPts != null
                        && e.SkipReason == null)
            .Select(e => e.Sport)
            .Distinct()
            .ToListAsync(ct);
        foreach (var sport in sports)
        {
            var clvs = await db.ShadowEntries.AsNoTracking()
                .Where(e => e.VariantId == signalVariantId
                            && e.Sport == sport
                            && e.Result != null
                            && e.ClvPts != null
                            && e.SkipReason == null)
                .OrderByDescending(e => e.SettledAt ?? e.EvaluatedAt)
                .ThenByDescending(e => e.Id)
                .Take(Guards.ShutoffWindow)
                .Select(e => e.ClvPts!.Value)
                .ToListAsync(ct);
            if (Guards.LeaguePausedByRollingClv(clvs))
                paused.Add(sport);
        }
        return paused;
    }

    public static async Task UpsertAsync(
        VariantDbContext db,
        string variantId,
        string sport,
        string gameKey,
        VariantDecision d,
        double? openLine,
        double? currentLine,
        CancellationToken ct = default)
    {
        var row = await db.ShadowEntries
            .FirstOrDefaultAsync(x => x.VariantId == variantId && x.GameKey == gameKey, ct);
        if (row is null)
        {
            row = new ShadowEntry { VariantId = variantId, GameKey = gameKey };
            db.ShadowEntries.Add(row);
        }
        else if (row.Result is not null)
        {
            // Keep settled outcome; refresh lines/CLV only if still open was already settled.
            return;
        }

        row.Sport = sport;
        row.Side = d.Side;
        row.Line = d.Line;
        row.AmericanOdds = d.AmericanOdds;
        row.ModelProb = d.ModelProb;
        row.Ev = d.Ev;
        row.AbsEdge = d.AbsEdge;
        row.OpenLine = openLine;
        row.CurrentLine = currentLine;
        row.SkipReason = d.Pick ? null : d.SkipReason;
        row.EvaluatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
