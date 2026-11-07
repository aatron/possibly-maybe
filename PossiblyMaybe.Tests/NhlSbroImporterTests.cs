using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Cli.Import;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class NhlSbroImporterTests
{
    static string? ResolveNhlDir()
    {
        var here = AppContext.BaseDirectory;
        for (var dir = new DirectoryInfo(here); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "data", "raw", "round3_odds", "archive_org", "sbro_nhl");
            if (Directory.Exists(candidate) && Directory.EnumerateFiles(candidate, "nhl_odds_2018-19.xlsx").Any())
                return candidate;
        }

        var sibling = Path.GetFullPath(Path.Combine(here, "..", "..", "..", "..", "..", "possibly-maybe",
            "data", "raw", "round3_odds", "archive_org", "sbro_nhl"));
        return Directory.Exists(sibling) ? sibling : null;
    }

    [Test]
    public async Task Import_OneSeason_WritesConsensusOpenClose_RealJuice_NoOregon()
    {
        var nhlDir = ResolveNhlDir();
        var src = nhlDir is null ? null : Path.Combine(nhlDir, "nhl_odds_2018-19.xlsx");
        if (src is null || !File.Exists(src))
            return; // gitignored archive; skip when absent

        var tmpDir = Path.Combine(Path.GetTempPath(), $"pm-nhl-sbro-dir-{Guid.NewGuid():N}");
        var dbPath = Path.Combine(Path.GetTempPath(), $"pm-nhl-sbro-{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(tmpDir);
        try
        {
            File.Copy(src, Path.Combine(tmpDir, Path.GetFileName(src)));
            // Placebo skip-target must not be imported even if present
            File.Copy(src, Path.Combine(tmpDir, "nhl_odds_2022-23.xlsx"));

            var coverage = await NhlSbroImporter.ImportAsync(tmpDir, dbPath);
            // 2018-19 only — 2022-23 filename is skipped
            await Assert.That(coverage.Count).IsEqualTo(1);
            await Assert.That(coverage[0].Season).IsEqualTo("2018-2019");
            await Assert.That(coverage[0].OpenCloseGames).IsEqualTo(coverage[0].Games);
            await Assert.That(coverage[0].Games).IsGreaterThan(1000);

            var cs = $"Data Source={dbPath}";
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(cs).Options;
            await using var db = new AppDbContext(options);

            var games = await db.Games.AsNoTracking().Where(g => g.Sport == "NHL").ToListAsync();
            await Assert.That(games.Count).IsEqualTo(coverage[0].Games);
            await Assert.That(games.Any(g => OregonExclusion.IsExcluded("NHL", g.HomeTeam, g.AwayTeam))).IsFalse();
            await Assert.That(games.Any(g => g.HomeTeam == "ANA" || g.AwayTeam == "ANA")).IsTrue();

            var snaps = await db.OddsSnapshots.AsNoTracking()
                .Where(o => o.Market == "TOTAL")
                .ToListAsync();
            await Assert.That(snaps.Count).IsEqualTo(coverage[0].Games * 2);
            await Assert.That(snaps.All(s => s.Book == Book.Consensus)).IsTrue();
            await Assert.That(snaps.All(s => s.ClvQuality == ClvQuality.OpenClose)).IsTrue();
            await Assert.That(snaps.Count(s => s.PriceKind == PriceKind.Open)).IsEqualTo(coverage[0].Games);
            await Assert.That(snaps.Count(s => s.PriceKind == PriceKind.Close)).IsEqualTo(coverage[0].Games);
            await Assert.That(snaps.Count(s => !s.AssumedJuice)).IsGreaterThan(coverage[0].Games);
        }
        finally
        {
            if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }
}
