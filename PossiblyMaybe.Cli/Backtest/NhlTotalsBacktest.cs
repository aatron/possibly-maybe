using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;
using PossiblyMaybe.Domain.SportPacks;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// NHL totals: goals EMA (<see cref="NhlShadowConfig"/>) vs SBRO open, CLV vs close.
/// Oregon sides skipped. Real juice when AmericanOdds present (over-only SBRO → under falls back −110).
/// </summary>
public static class NhlTotalsBacktest
{
    public static string HoldoutSeason => NhlSportPack.Instance.TierCHoldoutSeason;

    /// <summary>Score one season on filled O→C only (Tier A fold or look-once Tier C).</summary>
    public static async Task<Gate1aResult> RunSeasonFoldAsync(
        string dbPath, string season, CancellationToken ct = default)
    {
        var pack = NhlSportPack.Instance;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        await using var db = new AppDbContext(options);

        var games = (await db.Games.AsNoTracking()
                .Where(g => g.Sport == "NHL" && g.HomeScore != null && g.AwayScore != null)
                .ToListAsync(ct))
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id)
            .ToList();
        var odds = await db.OddsSnapshots.AsNoTracking()
            .Where(o => o.Market == "TOTAL")
            .ToListAsync(ct);
        var byOdds = odds.GroupBy(o => o.GameId).ToDictionary(g => g.Key, g => g.ToList());

        var scoring = new ScoringBook(alpha: pack.EmaAlpha, leagueInit: NhlShadowConfig.LeagueInit);
        var acc = new Acc($"nhl_fold_{season}");

        foreach (var g in games)
        {
            byOdds.TryGetValue(g.Id, out var snaps);
            snaps ??= [];
            var open = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Open
                                                 && s.ClvQuality == ClvQuality.OpenClose)
                       ?? snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Open);
            var close = snaps.FirstOrDefault(s => s.PriceKind == PriceKind.Close);

            var (lh, la) = scoring.ExpectedLambdas(g.HomeTeam, g.AwayTeam);
            var modelExp = lh + la;

            if (open?.Line is { } betLine && close?.Line is { } closeLine
                && open.ClvQuality == ClvQuality.OpenClose
                && g.Season == season)
            {
                acc.AddGame();
                if (!OregonExclusion.IsExcluded("NHL", g.HomeTeam, g.AwayTeam))
                {
                    var exp = pack.MarketWeight * betLine + (1 - pack.MarketWeight) * modelExp;
                    var edge = exp - betLine;
                    var hasClv = Math.Abs(betLine - closeLine) >= 0.25;
                    var clv = hasClv ? TotalsMath.OverClvPoints(betLine, closeLine) : 0;
                    var ou = TotalsMath.OuResult(g.HomeScore!.Value, g.AwayScore!.Value, betLine);
                    var overJuice = open.AmericanOdds == 0 ? -110 : open.AmericanOdds;
                    // SBRO NHL stores over juice only; under uses −110 assumed when no under snap.
                    var underSnap = snaps.FirstOrDefault(s =>
                        s.PriceKind == PriceKind.Open
                        && s.Side.Equals("under", StringComparison.OrdinalIgnoreCase));
                    var underJuice = underSnap != null && underSnap.AmericanOdds != 0
                        ? underSnap.AmericanOdds
                        : -110;

                    if (Math.Abs(edge) >= pack.MinAbsTotalEdge)
                    {
                        if (edge > 0)
                        {
                            var p = TotalsMath.PoissonTotalOverProb(lh, la, betLine);
                            if (OddsMath.ExpectedValue(p, overJuice) >= pack.MinEv)
                                acc.AddBet(clv, ou > 0, TotalsMath.OuPnL(overJuice, ou), hasClv);
                        }
                        else
                        {
                            var p = 1 - TotalsMath.PoissonTotalOverProb(lh, la, betLine);
                            if (OddsMath.ExpectedValue(p, underJuice) >= pack.MinEv)
                                acc.AddBet(hasClv ? -clv : 0, ou < 0, TotalsMath.OuPnL(underJuice, -ou), hasClv);
                        }
                    }
                }
            }

            scoring.Update(g.HomeTeam, g.AwayTeam, g.HomeScore!.Value, g.AwayScore!.Value);
        }

        return acc.ToResult();
    }

    private sealed class Acc(string slice)
    {
        private int _games, _bets, _hits, _clvN;
        private double _clv, _pnl;
        public void AddGame() => _games++;
        public void AddBet(double clv, bool won, double pnl, bool hasClv)
        {
            _bets++;
            _pnl += pnl;
            if (won) _hits++;
            if (hasClv) { _clv += clv; _clvN++; }
        }
        public Gate1aResult ToResult() => new()
        {
            Slice = slice, GamesScored = _games, Bets = _bets,
            MeanClv = _clvN == 0 ? 0 : _clv / _clvN, ClvUnit = "pts",
            HitRate = _bets == 0 ? 0 : (double)_hits / _bets, PaperPnlUnits = _pnl,
        };
    }
}
