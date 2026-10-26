using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>ATS-only walk-forward with min EV and min |expected margin vs line|.</summary>
public static class SpreadEdgeBacktest
{
    public const string HoldoutSeason = "2024";

    public static async Task<(Gate1aResult Train, Gate1aResult Hold)> RunAsync(
        string dbPath,
        Gate1Model model,
        double minEv,
        double minAbsEdge,
        CancellationToken ct = default)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        await using var db = new AppDbContext(options);

        var games = (await db.Games.AsNoTracking()
                .Where(g => g.HomeScore != null && g.AwayScore != null)
                .ToListAsync(ct))
            .OrderBy(g => g.TipoffUtc).ThenBy(g => g.Id)
            .ToList();

        var odds = await db.OddsSnapshots.AsNoTracking()
            .Where(o => o.Market == "SPREAD" || o.Market == "ML")
            .ToListAsync(ct);
        var byGame = odds.GroupBy(o => o.GameId).ToDictionary(g => g.Key, g => g.ToList());
        var refs = await db.RefereeAssignments.AsNoTracking().ToListAsync(ct);
        var byRefs = refs.GroupBy(r => r.GameId).ToDictionary(g => g.Key, g => g.ToList());

        var elo = new EloBook();
        var refFx = new RefEffects();
        var rest = new RestTracker();
        var train = new Acc("train_oos");
        var hold = new Acc("holdout_2023_24");

        foreach (var g in games)
        {
            byGame.TryGetValue(g.Id, out var snaps);
            snaps ??= [];
            byRefs.TryGetValue(g.Id, out var crew);
            crew ??= [];
            var homeWon = g.HomeScore > g.AwayScore;
            var p = HomeWinProb(g, snaps, crew, elo, refFx, rest, model);
            var acc = g.Season == HoldoutSeason ? hold : train;
            acc.AddPred(p, homeWon);

            var sp = snaps.Where(s => s.Market == "SPREAD").ToList();
            var openHome = Pick(sp, "home", PriceKind.Open);
            var closeHome = Pick(sp, "home", PriceKind.Close);
            if (openHome?.Line is not null && closeHome?.Line is not null
                && Math.Abs(openHome.Line.Value - closeHome.Line.Value) >= 0.25)
            {
                var line = openHome.Line.Value;
                var exp = SpreadMath.ExpectedHomeMargin(p);
                var edge = exp + line; // >0 favors home cover
                var pCover = SpreadMath.HomeCoverProb(exp, line);
                var juiceH = openHome.AmericanOdds == 0 ? -110 : openHome.AmericanOdds;
                var openAway = Pick(sp, "away", PriceKind.Open);
                var juiceA = openAway is null || openAway.AmericanOdds == 0 ? -110 : openAway.AmericanOdds;
                var ats = SpreadMath.HomeAtsResult(g.HomeScore!.Value, g.AwayScore!.Value, line);
                var clvH = SpreadMath.HomeSpreadClvPoints(line, closeHome.Line.Value);

                Cand? best = null;
                if (Math.Abs(edge) >= minAbsEdge)
                {
                    if (edge > 0)
                    {
                        var ev = OddsMath.ExpectedValue(pCover, juiceH);
                        if (ev >= minEv)
                            best = new Cand(ev, clvH, ats > 0, SpreadMath.AtsPnL(juiceH, ats));
                    }
                    else
                    {
                        var ev = OddsMath.ExpectedValue(1 - pCover, juiceA);
                        if (ev >= minEv)
                            best = new Cand(ev, -clvH, ats < 0, SpreadMath.AtsPnL(juiceA, -ats));
                    }
                }

                if (best is { } b)
                    acc.AddBet(b.Clv, b.Won, b.Pnl);
            }

            refFx.Observe(crew.Select(c => c.OfficialName), homeWon, crew.Any(x => x.DonaghyExcludeFromFit));
            elo.Update(g.HomeTeam, g.AwayTeam, homeWon);
            var d = DateOnly.FromDateTime(g.TipoffUtc.UtcDateTime);
            rest.Played(g.HomeTeam, d);
            rest.Played(g.AwayTeam, d);
        }

        return (train.ToResult(), hold.ToResult());
    }

    private static double HomeWinProb(
        Game g, List<OddsSnapshot> snaps, List<RefereeAssignment> crew,
        EloBook elo, RefEffects refFx, RestTracker rest, Gate1Model model)
    {
        const double marketWeight = 0.7, refWeight = 0.5, loadWeight = 0.3;
        var ml = snaps.Where(s => s.Market == "ML").ToList();
        var closeHome = Pick(ml, "home", PriceKind.Close);
        var closeAway = Pick(ml, "away", PriceKind.Close);
        var openHome = Pick(ml, "home", PriceKind.Open);
        var openAway = Pick(ml, "away", PriceKind.Open);
        double mHome = 0.5;
        if (closeHome is not null && closeAway is not null)
        {
            var ha = openHome?.AmericanOdds ?? closeHome.AmericanOdds;
            var aa = openAway?.AmericanOdds ?? closeAway.AmericanOdds;
            (mHome, _) = OddsMath.NoVigTwoWay(ha, aa);
        }
        var p = marketWeight * mHome + (1 - marketWeight) * elo.HomeWinProbability(g.HomeTeam, g.AwayTeam);
        if (model >= Gate1Model.MarketEloRefs)
            p = Math.Clamp(p + refWeight * refFx.CrewHomeBias(crew.Select(c => c.OfficialName)), 0.01, 0.99);
        if (model >= Gate1Model.MarketEloRefsLoad)
        {
            var load = rest.HomeRestEdge(g.HomeTeam, g.AwayTeam, DateOnly.FromDateTime(g.TipoffUtc.UtcDateTime));
            p = Math.Clamp(p + loadWeight * load, 0.01, 0.99);
        }
        return p;
    }

    private static OddsSnapshot? Pick(List<OddsSnapshot> snaps, string side, PriceKind kind)
        => snaps.Where(s => s.Side == side && s.PriceKind == kind)
            .OrderBy(s => s.ClvQuality)
            .ThenBy(s => s.Book is Book.Consensus or Book.Mgm ? 0 : 1)
            .FirstOrDefault();

    private readonly record struct Cand(double Ev, double Clv, bool Won, double Pnl);

    private sealed class Acc(string name)
    {
        private int _n, _bets, _hits;
        private double _ll, _br, _clv, _pnl;

        public void AddPred(double pHome, bool homeWon)
        {
            var p = Math.Clamp(pHome, 1e-6, 1 - 1e-6);
            var y = homeWon ? 1.0 : 0.0;
            _ll += -(y * Math.Log(p) + (1 - y) * Math.Log(1 - p));
            _br += (p - y) * (p - y);
            _n++;
        }

        public void AddBet(double clv, bool won, double pnl)
        {
            _bets++;
            if (won) _hits++;
            _clv += clv;
            _pnl += pnl;
        }

        public Gate1aResult ToResult() => new()
        {
            Slice = name,
            GamesScored = _n,
            Bets = _bets,
            MeanClv = _bets == 0 ? 0 : _clv / _bets,
            ClvUnit = "spread_pts",
            SpreadBets = _bets,
            MeanSpreadClvPts = _bets == 0 ? 0 : _clv / _bets,
            HitRate = _bets == 0 ? 0 : (double)_hits / _bets,
            LogLoss = _n == 0 ? 0 : _ll / _n,
            Brier = _n == 0 ? 0 : _br / _n,
            PaperPnlUnits = _pnl,
        };
    }
}
