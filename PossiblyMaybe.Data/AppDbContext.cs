using Microsoft.EntityFrameworkCore;

namespace PossiblyMaybe.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Game> Games => Set<Game>();
    public DbSet<OddsSnapshot> OddsSnapshots => Set<OddsSnapshot>();
    public DbSet<RefereeAssignment> RefereeAssignments => Set<RefereeAssignment>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Game>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Sport).HasMaxLength(32).IsRequired();
            e.Property(x => x.SourceKey).HasMaxLength(64).IsRequired();
            e.Property(x => x.EspnGameId).HasMaxLength(32);
            e.Property(x => x.HomeTeam).HasMaxLength(64).IsRequired();
            e.Property(x => x.AwayTeam).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.SourceKey).IsUnique();
            e.HasIndex(x => x.EspnGameId);
            e.HasIndex(x => new { x.Sport, x.TipoffUtc });
        });

        modelBuilder.Entity<OddsSnapshot>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Market).HasMaxLength(32).IsRequired();
            e.Property(x => x.Side).HasMaxLength(32).IsRequired();
            e.HasOne(x => x.Game).WithMany(g => g.OddsSnapshots).HasForeignKey(x => x.GameId);
            e.HasIndex(x => new { x.GameId, x.Book, x.Market, x.Side, x.PriceKind });
        });

        modelBuilder.Entity<RefereeAssignment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.OfficialName).HasMaxLength(128).IsRequired();
            e.HasOne(x => x.Game).WithMany(g => g.RefereeAssignments).HasForeignKey(x => x.GameId);
            e.HasIndex(x => x.GameId);
        });

        modelBuilder.Entity<LedgerEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.GameKey).HasMaxLength(64).IsRequired();
            e.Property(x => x.Market).HasMaxLength(16).IsRequired();
            e.Property(x => x.Side).HasMaxLength(8).IsRequired();
            e.Property(x => x.Result).HasMaxLength(4);
            e.HasIndex(x => x.PlacedAt);
        });
    }

    public static async Task EnsureCreatedAsync(string connectionString, CancellationToken ct = default)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync(ct);
    }
}
