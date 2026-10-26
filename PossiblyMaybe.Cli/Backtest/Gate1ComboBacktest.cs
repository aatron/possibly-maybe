using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

public enum BetMarketMode
{
    Moneyline = 0,
    Spread = 1,
    /// <summary>One bet per game: best EV between ML and ATS.</summary>
    ComboBest = 2,
}

public static class Gate1ComboBacktest
{
    public const string HoldoutSeason = "2024";

    public static async Task<(Gate1aResult TrainOos, Gate1aResult Holdout)> RunAsync(
        string dbPath,
        BetMarketMode mode,
        Gate1Model model = Gate1Model.MarketEloRefsLoad,
        double marketWeight = 0.7,
        double refWeight = 0.5,
        double loadWeight = 0.3,
        double minMlEv = 0.03,
        double minSpreadEv = 0.05,
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

        var odds = await db.OddsSnapshots.AsNoTracking().ToListAsync(ct);
        var byGame = odds.GroupBy(o => o.GameId).ToDictionary(g => g.Key, g => g.ToList());
        var refs = await db.RefereeAssignments.AsNoTracking().ToListAsync(ct);
        var byRefs = refs.GroupBy(r => r.GameId).ToDictionary(g => g.Key, g => g.ToList());

        var elo = new EloBook();
        var refFx = new RefEffects();
        var rest = new RestTracker();
        var train = new SliceAcc("train_oos");
        var hold = new SliceAcc("holdout_2023_24");

        foreach (var g in games)
        {
            byGame.TryGetValue(g.Id, out var snaps);
            snaps ??= [];
            byRefs.TryGetValue(g.Id, out var crew);
            crew ??= [];
            var homeWon = g.HomeScore > g.AwayScore;
            var hs = g.HomeScore!.Value;
            var ascore = g.AwayScore!.Value;

            var p = ModelHomeWinProb(g, snaps, crew, elo, refFx, rest, model, marketWeight, refWeight, loadWeight);
            var acc = g.Season == HoldoutSeason ? hold : train;
            acc.AddPrediction(p, homeWon);

            var cands = new List<Cand>();
            if (mode is BetMarketMode.Moneyline or BetMarketMode.ComboBest)
                AddMlCands(cands, snaps, p, homeWon, minMlEv);
            if (mode is BetMarketMode.Spread or BetMarketMode.ComboBest)
                AddSpreadCands(cands, snaps, p, hs, ascore, minSpreadEv);

            if (cands.Count > 0)
            {
                var pick = cands.OrderByDescending(c => c.Ev).First();
                acc.AddBet(pick);
            }

            Finish(g, homeWon, crew, elo, refFx, rest);
        }

        var unit = mode switch
        {
            BetMarketMode.Moneyline => "prob",
            BetMarketMode.Spread => "spread_pts",
            _ => "mixed",
        };
        return (train.ToResult(unit), hold.ToResult(unit));
    }

    private static void AddMlCands(List<Cand> cands, List<OddsSnapshot> snaps, double p, bool homeWon, double minEv)
    {
        var ml = snaps.Where(s => s.Market == "ML").ToList();
        var closeHome = Pick(ml, "home", PriceKind.Close);
        var closeAway = Pick(ml, "away", PriceKind.Close);
        var openHome = Pick(ml, "home", PriceKind.Open);
        var openAway = Pick(ml, "away", PriceKind.Open);
        if (closeHome is null || closeAway is null || openHome is null || openAway is null) return;
        if (openHome.AmericanOdds == closeHome.AmericanOdds && openAway.AmericanOdds == closeAway.AmericanOdds)
            return;

        var evH = OddsMath.ExpectedValue(p, openHome.AmericanOdds);
        var evA = OddsMath.ExpectedValue(1 - p, openAway.AmericanOdds);
        if (evH >= minEv)
        {
            cands.Add(new Cand(evH, Clv.ProbabilityClv(openHome.AmericanOdds, closeHome.AmericanOdds),
                homeWon, homeWon ? OddsMath.AmericanToDecimal(openHome.AmericanOdds) - 1 : -1, IsSpread: false));
        }
        if (evA >= minEv)
        {
            cands.Add(new Cand(evA, Clv.ProbabilityClv(openAway.AmericanOdds, closeAway.AmericanOdds),
                !homeWon, !homeWon ? OddsMath.AmericanToDecimal(openAway.AmericanOdds) - 1 : -1, IsSpread: false));
        }
    }

    private static void AddSpreadCands(
        List<Cand> cands, List<OddsSnapshot> snaps, double p, int homeScore, int awayScore, double minEv)
    {
        var sp = snaps.Where(s => s.Market == "SPREAD").ToList();
        var openHome = Pick(sp, "home", PriceKind.Open);
        var closeHome = Pick(sp, "home", PriceKind.Close);
        if (openHome?.Line is null || closeHome?.Line is null) return;
        if (Math.Abs(openHome.Line.Value - closeHome.Line.Value) < 0.25) return; // need real move

        var expMargin = SpreadMath.ExpectedHomeMargin(p);
        var pCover = SpreadMath.HomeCoverProb(expMargin, openHome.Line.Value);
        var juiceH = openHome.AmericanOdds == 0 ? -110 : openHome.AmericanOdds;
        var openAway = Pick(sp, "away", PriceKind.Open);
        var juiceA = openAway is null || openAway.AmericanOdds == 0 ? -110 : openAway.AmericanOdds;

        var evH = OddsMath.ExpectedValue(pCover, juiceH);
        var evA = OddsMath.ExpectedValue(1 - pCover, juiceA);
        var ats = SpreadMath.HomeAtsResult(homeScore, awayScore, openHome.Line.Value);
        var clvH = SpreadMath.HomeSpreadClvPoints(openHome.Line.Value, closeHome.Line.Value);

        if (evH >= minEv)
            cands.Add(new Cand(evH, clvH, ats > 0, SpreadMath.AtsPnL(juiceH, ats), IsSpread: true));
        if (evA >= minEv)
            cands.Add(new Cand(evA, -clvH, ats < 0, SpreadMath.AtsPnL(juiceA, -ats), IsSpread: true));
    }

    private static double ModelHomeWinProb(
        Game g, List<OddsSnapshot> snaps, List<RefereeAssignment> crew,
        EloBook elo, RefEffects refFx, RestTracker rest, Gate1Model model,
        double marketWeight, double refWeight, double loadWeight)
    {
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

        var p = marketWeight * mHome + (1.0 - marketWeight) * elo.HomeWinProbability(g.HomeTeam, g.AwayTeam);
        if (model >= Gate1Model.MarketEloRefs)
            p = Math.Clamp(p + refWeight * refFx.CrewHomeBias(crew.Select(c => c.OfficialName)), 0.01, 0.99);
        if (model >= Gate1Model.MarketEloRefsLoad)
        {
            var load = rest.HomeRestEdge(g.HomeTeam, g.AwayTeam, DateOnly.FromDateTime(g.TipoffUtc.UtcDateTime));
            p = Math.Clamp(p + loadWeight * load, 0.01, 0.99);
        }
        return p;
    }

    private static void Finish(
        Game g, bool homeWon, List<RefereeAssignment> crew, EloBook elo, RefEffects refFx, RestTracker rest)
    {
        refFx.Observe(crew.Select(c => c.OfficialName), homeWon, crew.Any(c => c.DonaghyExcludeFromFit));
        elo.Update(g.HomeTeam, g.AwayTeam, homeWon);
        var d = DateOnly.FromDateTime(g.TipoffUtc.UtcDateTime);
        rest.Played(g.HomeTeam, d);
        rest.Played(g.AwayTeam, d);
    }

    private static OddsSnapshot? Pick(List<OddsSnapshot> snaps, string side, PriceKind kind)
        => snaps.Where(s => s.Side == side && s.PriceKind == kind)
            .OrderBy(s => s.ClvQuality)
            .ThenBy(s => s.Book is Book.Consensus or Book.Mgm ? 0 : 1)
            .FirstOrDefault();

    private readonly record struct Cand(double Ev, double Clv, bool Won, double Pnl, bool IsSpread);

    private sealed class SliceAcc(string name)
    {
        private int _nPred, _bets, _hits, _mlBets, _spBets;
        private double _logLoss, _brier, _clvSum, _pnl, _mlClv, _spClv;

        public void AddPrediction(double pHome, bool homeWon)
        {
            var p = Math.Clamp(pHome, 1e-6, 1 - 1e-6);
            var y = homeWon ? 1.0 : 0.0;
            _logLoss += -(y * Math.Log(p) + (1 - y) * Math.Log(1 - p));
            _brier += (p - y) * (p - y);
            _nPred++;
        }

        public void AddBet(Cand c)
        {
            _bets++;
            if (c.Won) _hits++;
            _clvSum += c.Clv;
            _pnl += c.Pnl;
            if (c.IsSpread) { _spBets++; _spClv += c.Clv; }
            else { _mlBets++; _mlClv += c.Clv; }
        }

        public Gate1aResult ToResult(string unit) => new()
        {
            Slice = name,
            GamesScored = _nPred,
            Bets = _bets,
            MeanClv = _bets == 0 || unit == "mixed" ? 0 : _clvSum / _bets,
            ClvUnit = unit,
            MlBets = _mlBets,
            SpreadBets = _spBets,
            MeanMlClv = _mlBets == 0 ? 0 : _mlClv / _mlBets,
            MeanSpreadClvPts = _spBets == 0 ? 0 : _spClv / _spBets,
            HitRate = _bets == 0 ? 0 : (double)_hits / _bets,
            LogLoss = _nPred == 0 ? 0 : _logLoss / _nPred,
            Brier = _nPred == 0 ? 0 : _brier / _nPred,
            PaperPnlUnits = _pnl,
        };
    }
}
