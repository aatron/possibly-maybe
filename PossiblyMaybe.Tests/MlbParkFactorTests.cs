using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class MlbParkFactorTests
{
    [Test]
    public async Task Scale_uses_pf_over_100()
    {
        var park = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["COL"] = 125,
            ["SEA"] = 83,
        };
        await Assert.That(MlbParkFactors.Scale(10, "COL", park)).IsEqualTo(12.5);
        await Assert.That(MlbParkFactors.Scale(10, "SEA", park)).IsEqualTo(8.3);
        await Assert.That(MlbParkFactors.Scale(10, "NYY", park)).IsEqualTo(10.0);
    }

    [Test]
    public async Task Load_reads_pf_3yr()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "season,team,pf_3yr\n2024,COL,125\n2024,SD,92\n");
            var map = MlbParkFactors.Load(path);
            await Assert.That(map["COL"]).IsEqualTo(125);
            await Assert.That(map["SD"]).IsEqualTo(92);
        }
        finally { File.Delete(path); }
    }
}
