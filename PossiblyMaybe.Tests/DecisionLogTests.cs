using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Cli.Variants;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class DecisionLogTests
{
    [Test]
    public async Task Schema_Has_DecisionLog_And_Settlement_Tables()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pm-dlog-schema-{Guid.NewGuid():N}.db");
        try
        {
            var (db, _) = await VariantLedger.OpenAsync(path);
            await using (db)
            {
                await Assert.That(await TableExists(db, "DecisionLogEntries")).IsTrue();
                await Assert.That(await TableExists(db, "DecisionSettlements")).IsTrue();
            }
        }
        finally
        {
            try { File.Delete(path); } catch { /* */ }
        }
    }

    [Test]
    public async Task ReRun_Appends_New_Rows_Old_Unchanged()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pm-dlog-rerun-{Guid.NewGuid():N}.db");
        try
        {
            var c = new SlateCandidate(
                "NBA", "20261107", "BOS", "MIA",
                232, null, null, 220, 220, -110, -110,
                15, ChampConfig.MarketWeight, ChampConfig.MinAbsTotalEdge, ChampConfig.MinEv,
                Stale: false);

            await VariantEvaluate.RecordAsync(path, [c]);
            var (db1, _) = await VariantLedger.OpenAsync(path);
            List<DecisionLogEntry> first;
            await using (db1)
            {
                first = await db1.DecisionLogEntries.AsNoTracking().OrderBy(e => e.Id).ToListAsync();
                await Assert.That(first.Count).IsEqualTo(VariantRegistry.All.Count);
                var sample = first[0];
                await Assert.That(sample.RunId).IsNotEqualTo(Guid.Empty);
                await Assert.That(sample.GitSha.Length).IsGreaterThanOrEqualTo(7);
                await Assert.That(sample.RegistryVersion).IsEqualTo(VariantRegistry.RegistryVersion);
                await Assert.That(sample.VenueLinesJson).IsNotNull();
                await Assert.That(sample.InjuryRosterHash).IsNotNull();
                await Assert.That(sample.InjuryRosterJson).IsNotNull();
            }

            await VariantEvaluate.RecordAsync(path, [c]);
            var (db2, _) = await VariantLedger.OpenAsync(path);
            await using (db2)
            {
                var all = await db2.DecisionLogEntries.AsNoTracking().OrderBy(e => e.Id).ToListAsync();
                await Assert.That(all.Count).IsEqualTo(VariantRegistry.All.Count * 2);

                for (var i = 0; i < first.Count; i++)
                {
                    await Assert.That(all[i].Id).IsEqualTo(first[i].Id);
                    await Assert.That(all[i].RunId).IsEqualTo(first[i].RunId);
                    await Assert.That(all[i].DecidedAt).IsEqualTo(first[i].DecidedAt);
                    await Assert.That(all[i].GitSha).IsEqualTo(first[i].GitSha);
                    await Assert.That(all[i].VariantId).IsEqualTo(first[i].VariantId);
                    await Assert.That(all[i].VenueLinesJson).IsEqualTo(first[i].VenueLinesJson);
                    await Assert.That(all[i].InjuryRosterHash).IsEqualTo(first[i].InjuryRosterHash);
                }

                var runIds = all.Select(e => e.RunId).Distinct().ToList();
                await Assert.That(runIds.Count).IsEqualTo(2);
            }
        }
        finally
        {
            try { File.Delete(path); } catch { /* */ }
        }
    }

    [Test]
    public async Task Settlement_Goes_In_Separate_Table()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pm-dlog-settle-{Guid.NewGuid():N}.db");
        try
        {
            var (db, _) = await VariantLedger.OpenAsync(path);
            await using (db)
            {
                var d = new VariantDecision(true, "OVER", 220, -110, 0.55, 0.05, 6, null);
                var row = await VariantLedger.AppendDecisionAsync(
                    db, Guid.NewGuid(), "V0", "NBA", "20261107 MIA@BOS", d);
                var before = row.DecidedAt;
                var json = row.VenueLinesJson;

                await VariantLedger.AppendSettlementAsync(
                    db, row.Id, "W", 0.91, 221, 1.0, "DraftKings",
                    DateTimeOffset.UtcNow, 30, false);

                var reloaded = await db.DecisionLogEntries.AsNoTracking().SingleAsync(e => e.Id == row.Id);
                await Assert.That(reloaded.DecidedAt).IsEqualTo(before);
                await Assert.That(reloaded.VenueLinesJson).IsEqualTo(json);

                var settle = await db.DecisionSettlements.AsNoTracking().SingleAsync();
                await Assert.That(settle.DecisionLogId).IsEqualTo(row.Id);
                await Assert.That(settle.Result).IsEqualTo("W");
                await Assert.That(settle.ClvPts).IsEqualTo(1.0);
            }
        }
        finally
        {
            try { File.Delete(path); } catch { /* */ }
        }
    }

    static async Task<bool> TableExists(VariantDbContext db, string name)
    {
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$n";
        var p = cmd.CreateParameter();
        p.ParameterName = "$n";
        p.Value = name;
        cmd.Parameters.Add(p);
        return await cmd.ExecuteScalarAsync() is not null;
    }
}
