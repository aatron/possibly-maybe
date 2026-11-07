using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Variants;

/// <summary>Durable trial registry + wave cell state beside the shadow ledger.</summary>
public sealed class RadarCellRow
{
    public int Id { get; set; }
    public string League { get; set; } = "";
    public string Market { get; set; } = "";
    public string VariantId { get; set; } = "";
    public int N { get; set; }
    public double MeanClv { get; set; }
    public double RollingClv { get; set; }
    public double Cusum { get; set; }
    public double PValue { get; set; }
    public bool BhSignificant { get; set; }
    public string Wave { get; set; } = "Off";
    public string Stage { get; set; } = "Shadow";
    public int WeeksOn { get; set; }
    public int NTrials { get; set; }
    public bool PromoteEligible { get; set; }
    public bool Demote { get; set; }
    public bool UserRealFlag { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class RadarDbContext(DbContextOptions<RadarDbContext> options) : DbContext(options)
{
    public DbSet<RadarCellRow> Cells => Set<RadarCellRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RadarCellRow>(e =>
        {
            e.HasIndex(x => new { x.League, x.Market, x.VariantId }).IsUnique();
            e.Property(x => x.League).HasMaxLength(16);
            e.Property(x => x.Market).HasMaxLength(16);
            e.Property(x => x.VariantId).HasMaxLength(32);
            e.Property(x => x.Wave).HasMaxLength(8);
            e.Property(x => x.Stage).HasMaxLength(16);
        });
    }
}

public static class RadarStore
{
    public static string DefaultDbPath(string repoRoot) => Path.Combine(repoRoot, "data", "radar.db");

    public static async Task<(RadarDbContext Db, string Path)> OpenAsync(
        string dbPath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbPath))!);
        var options = new DbContextOptionsBuilder<RadarDbContext>()
            .UseSqlite($"Data Source={dbPath}").Options;
        var db = new RadarDbContext(options);
        await db.Database.EnsureCreatedAsync(ct);
        return (db, dbPath);
    }

    public static async Task<Dictionary<string, LadderStage>> LoadStagesAsync(
        RadarDbContext db, CancellationToken ct = default)
    {
        var rows = await db.Cells.AsNoTracking().ToListAsync(ct);
        var map = new Dictionary<string, LadderStage>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
        {
            var stage = ParseStage(r.Stage);
            if (r.UserRealFlag)
                stage = LadderStage.RealCandidate;
            map[WaveRadar.CellKey(r.League, r.Market, r.VariantId)] = stage;
        }
        return map;
    }

    public static async Task SaveAsync(
        RadarDbContext db, IReadOnlyList<WaveCellResult> results, CancellationToken ct = default)
    {
        var existing = await db.Cells.ToListAsync(ct);
        var byKey = existing.ToDictionary(
            r => WaveRadar.CellKey(r.League, r.Market, r.VariantId),
            StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;

        foreach (var r in results)
        {
            var key = WaveRadar.CellKey(r.League, r.Market, r.VariantId);
            byKey.TryGetValue(key, out var row);
            if (row is null)
            {
                row = new RadarCellRow
                {
                    League = r.League,
                    Market = r.Market,
                    VariantId = r.VariantId,
                };
                db.Cells.Add(row);
            }

            // Preserve user real flag; stage RealCandidate only via flag.
            var userReal = row.UserRealFlag;
            row.N = r.N;
            row.MeanClv = r.MeanClv;
            row.RollingClv = r.RollingClv;
            row.Cusum = r.Cusum;
            row.PValue = r.PValue;
            row.BhSignificant = r.BhSignificant;
            row.Wave = r.Wave.ToString();
            row.Stage = userReal && r.Stage != LadderStage.Shadow
                ? nameof(LadderStage.RealCandidate)
                : r.Stage.ToString();
            if (userReal && r.Demote)
            {
                row.UserRealFlag = false;
                row.Stage = nameof(LadderStage.Shadow);
            }
            row.WeeksOn = r.WeeksOn;
            row.NTrials = r.NTrials;
            row.PromoteEligible = r.PromoteToPaperEligible;
            row.Demote = r.Demote;
            row.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
    }

    public static async Task<IReadOnlyList<RadarCellRow>> LoadCellsAsync(
        string radarDbPath, CancellationToken ct = default)
    {
        if (!File.Exists(radarDbPath))
            return [];
        var (db, _) = await OpenAsync(radarDbPath, ct);
        await using (db)
            return await db.Cells.AsNoTracking()
                .OrderBy(c => c.League).ThenBy(c => c.VariantId)
                .ToListAsync(ct);
    }

    static LadderStage ParseStage(string s) => s switch
    {
        nameof(LadderStage.Paper) => LadderStage.Paper,
        nameof(LadderStage.RealCandidate) => LadderStage.RealCandidate,
        _ => LadderStage.Shadow,
    };
}
