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
    public DateTimeOffset? SettledAt { get; set; }
}

public sealed class VariantDbContext(DbContextOptions<VariantDbContext> options) : DbContext(options)
{
    public DbSet<ShadowEntry> ShadowEntries => Set<ShadowEntry>();

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
        return (db, dbPath);
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
