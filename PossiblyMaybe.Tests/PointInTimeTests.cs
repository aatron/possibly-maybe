using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Tests;

public class PointInTimeTests
{
    [Test]
    public async Task AsOf_Excludes_Tipoff_OnOrAfter_Cutoff()
    {
        var past = new Game { Id = 1, TipoffUtc = DateTimeOffset.Parse("2026-11-01T00:00:00Z"), HomeScore = 100, AwayScore = 90 };
        var future = new Game { Id = 2, TipoffUtc = DateTimeOffset.Parse("2026-11-08T00:00:00Z"), HomeScore = 110, AwayScore = 105 };
        var asOf = DateTimeOffset.Parse("2026-11-07T00:00:00Z");
        var kept = PointInTime.AsOf([past, future], g => g.TipoffUtc, asOf).ToList();
        await Assert.That(kept.Count).IsEqualTo(1);
        await Assert.That(kept[0].Id).IsEqualTo(1);
    }

    [Test]
    public async Task LeakTest_ScoringBook_Identical_With_Future_Final_In_Db()
    {
        var asOf = DateTimeOffset.Parse("2026-11-07T12:00:00Z");
        var history = new[]
        {
            Tip("AAA", "BBB", 110, 100, "2026-11-01T00:00:00Z"),
            Tip("AAA", "CCC", 120, 95, "2026-11-03T00:00:00Z"),
            Tip("BBB", "CCC", 105, 108, "2026-11-05T00:00:00Z"),
        };
        var future = Tip("AAA", "BBB", 150, 80, "2026-11-08T00:00:00Z");

        var without = Train(history, asOf);
        var withFuture = Train(history.Append(future), asOf);

        await Assert.That(without.ExpectedTotal("AAA", "BBB"))
            .IsEqualTo(withFuture.ExpectedTotal("AAA", "BBB")).Within(1e-12);
        await Assert.That(without.ExpectedTotal("CCC", "AAA"))
            .IsEqualTo(withFuture.ExpectedTotal("CCC", "AAA")).Within(1e-12);
    }

    [Test]
    public async Task LeakTest_DixonColes_And_Elo_Same_AsOf()
    {
        var asOf = DateTimeOffset.Parse("2026-11-07T12:00:00Z");
        var history = new[]
        {
            Tip("HOM", "AWY", 2, 1, "2026-10-01T00:00:00Z"),
            Tip("HOM", "AWY", 3, 0, "2026-10-15T00:00:00Z"),
        };
        var future = Tip("HOM", "AWY", 0, 5, "2026-11-10T00:00:00Z");

        var dcA = TrainDc(history, asOf);
        var dcB = TrainDc(history.Append(future), asOf);
        await Assert.That(dcA.ExpectedTotal("HOM", "AWY"))
            .IsEqualTo(dcB.ExpectedTotal("HOM", "AWY")).Within(1e-12);

        var eloA = TrainElo(history, asOf);
        var eloB = TrainElo(history.Append(future), asOf);
        await Assert.That(eloA.Rating("HOM")).IsEqualTo(eloB.Rating("HOM")).Within(1e-12);
        await Assert.That(eloA.HomeWinProbability("HOM", "AWY"))
            .IsEqualTo(eloB.HomeWinProbability("HOM", "AWY")).Within(1e-12);
    }

    static (string Home, string Away, int Hs, int As, DateTimeOffset Tip) Tip(
        string home, string away, int hs, int as_, string tip)
        => (home, away, hs, as_, DateTimeOffset.Parse(tip));

    static ScoringBook Train(
        IEnumerable<(string Home, string Away, int Hs, int As, DateTimeOffset Tip)> games,
        DateTimeOffset asOf)
    {
        var book = new ScoringBook(alpha: 0.2, leagueInit: 200);
        foreach (var g in PointInTime.AsOf(games, x => x.Tip, asOf).OrderBy(x => x.Tip))
            book.Update(g.Home, g.Away, g.Hs, g.As);
        return book;
    }

    static DixonColesBook TrainDc(
        IEnumerable<(string Home, string Away, int Hs, int As, DateTimeOffset Tip)> games,
        DateTimeOffset asOf)
    {
        var book = new DixonColesBook(alpha: 0.2);
        foreach (var g in PointInTime.AsOf(games, x => x.Tip, asOf).OrderBy(x => x.Tip))
            book.Update(g.Home, g.Away, g.Hs, g.As);
        return book;
    }

    static EloBook TrainElo(
        IEnumerable<(string Home, string Away, int Hs, int As, DateTimeOffset Tip)> games,
        DateTimeOffset asOf)
    {
        var book = new EloBook();
        foreach (var g in PointInTime.AsOf(games, x => x.Tip, asOf).OrderBy(x => x.Tip))
            book.Update(g.Home, g.Away, g.Hs > g.As);
        return book;
    }
}
