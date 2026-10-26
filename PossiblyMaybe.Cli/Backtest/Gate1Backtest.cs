using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

public sealed class Gate1aResult
{
    public string Slice { get; init; } = "";
    public int GamesScored { get; init; }
    public int Bets { get; init; }
    public double MeanClv { get; init; }
    /// <summary>prob | spread_pts | mixed</summary>
    public string ClvUnit { get; init; } = "prob";
    public int MlBets { get; init; }
    public int SpreadBets { get; init; }
    public double MeanMlClv { get; init; }
    public double MeanSpreadClvPts { get; init; }
    public double HitRate { get; init; }
    public double LogLoss { get; init; }
    public double Brier { get; init; }
    public double PaperPnlUnits { get; init; }
}

public enum Gate1Model
{
    MarketElo = 0,
    MarketEloRefs = 1,
    MarketEloRefsLoad = 2,
}

public static class Gate1Backtest
{
    public const string HoldoutSeason = "2024";

    public static async Task<(Gate1aResult TrainOos, Gate1aResult Holdout)> RunAsync(
        string dbPath,
        Gate1Model model,
        double marketWeight = 0.7,
        double refWeight = 0.5,
        double loadWeight = 0.3,
        double minEv = 0.02,
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
            .Where(o => o.Market == "ML")
            .ToListAsync(ct);
        var byGameOdds = odds.GroupBy(o => o.GameId).ToDictionary(g => g.Key, g => g.ToList());

        var refs = await db.RefereeAssignments.AsNoTracking().ToListAsync(ct);
        var byGameRefs = refs.GroupBy(r => r.GameId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var elo = new EloBook();
        var refFx = new RefEffects();
        var rest = new RestTracker();
        var train = new SliceAcc("train_oos");
        var hold = new SliceAcc("holdout_2023_24");

        foreach (var g in games)
        {
            byGameOdds.TryGetValue(g.Id, out var snaps);
            snaps ??= [];
            byGameRefs.TryGetValue(g.Id, out var crew);
            crew ??= [];

            var closeHome = Pick(snaps, "home", PriceKind.Close);
            var closeAway = Pick(snaps, "away", PriceKind.Close);
            var openHome = Pick(snaps, "home", PriceKind.Open);
            var openAway = Pick(snaps, "away", PriceKind.Open);
            var homeWon = g.HomeScore > g.AwayScore;

            if (closeHome is null || closeAway is null)
            {
                FinishGame(g, homeWon, crew, elo, refFx, rest);
                continue;
            }

            var mHomeAmer = openHome?.AmericanOdds ?? closeHome.AmericanOdds;
            var mAwayAmer = openAway?.AmericanOdds ?? closeAway.AmericanOdds;
            var (mHome, _) = OddsMath.NoVigTwoWay(mHomeAmer, mAwayAmer);
            var eloHome = elo.HomeWinProbability(g.HomeTeam, g.AwayTeam);
            var p = marketWeight * mHome + (1.0 - marketWeight) * eloHome;

            if (model >= Gate1Model.MarketEloRefs)
            {
                var bias = refFx.CrewHomeBias(crew.Select(c => c.OfficialName));
                p = Math.Clamp(p + refWeight * bias, 0.01, 0.99);
            }

            if (model >= Gate1Model.MarketEloRefsLoad)
            {
                var load = rest.HomeRestEdge(g.HomeTeam, g.AwayTeam, DateOnly.FromDateTime(g.TipoffUtc.UtcDateTime));
                p = Math.Clamp(p + loadWeight * load, 0.01, 0.99);
            }

            var pAway = 1.0 - p;
            var acc = g.Season == HoldoutSeason ? hold : train;
            acc.AddPrediction(p, homeWon);

            var hasTrueOpen = openHome is not null && openAway is not null
                && (openHome.AmericanOdds != closeHome.AmericanOdds || openAway.AmericanOdds != closeAway.AmericanOdds);
            if (hasTrueOpen)
            {
                var evHome = OddsMath.ExpectedValue(p, openHome!.AmericanOdds);
                var evAway = OddsMath.ExpectedValue(pAway, openAway!.AmericanOdds);
                if (evHome >= minEv && evHome >= evAway)
                {
                    var won = homeWon;
                    acc.AddBet(Clv.ProbabilityClv(openHome.AmericanOdds, closeHome.AmericanOdds), won,
                        won ? OddsMath.AmericanToDecimal(openHome.AmericanOdds) - 1 : -1);
                }
                else if (evAway >= minEv)
                {
                    var won = !homeWon;
                    acc.AddBet(Clv.ProbabilityClv(openAway.AmericanOdds, closeAway.AmericanOdds), won,
                        won ? OddsMath.AmericanToDecimal(openAway.AmericanOdds) - 1 : -1);
                }
            }

            FinishGame(g, homeWon, crew, elo, refFx, rest);
        }

        return (train.ToResult(), hold.ToResult());
    }

    private static void FinishGame(
        Game g, bool homeWon, List<RefereeAssignment> crew, EloBook elo, RefEffects refFx, RestTracker rest)
    {
        var exclude = crew.Any(c => c.DonaghyExcludeFromFit);
        refFx.Observe(crew.Select(c => c.OfficialName), homeWon, exclude);
        elo.Update(g.HomeTeam, g.AwayTeam, homeWon);
        var d = DateOnly.FromDateTime(g.TipoffUtc.UtcDateTime);
        rest.Played(g.HomeTeam, d);
        rest.Played(g.AwayTeam, d);
    }

    private static OddsSnapshot? Pick(List<OddsSnapshot> snaps, string side, PriceKind kind)
        => snaps.Where(s => s.Side == side && s.PriceKind == kind)
            .OrderBy(s => s.ClvQuality) // OpenClose before CloseOnly
            .ThenBy(s => s.Book == Book.Consensus || s.Book == Book.Mgm ? 0 : 1)
            .FirstOrDefault();

    private sealed class SliceAcc(string name)
    {
        private int _nPred;
        private double _logLoss;
        private double _brier;
        private int _bets;
        private int _hits;
        private double _clvSum;
        private double _pnl;

        public void AddPrediction(double pHome, bool homeWon)
        {
            var p = Math.Clamp(pHome, 1e-6, 1 - 1e-6);
            var y = homeWon ? 1.0 : 0.0;
            _logLoss += -(y * Math.Log(p) + (1 - y) * Math.Log(1 - p));
            _brier += (p - y) * (p - y);
            _nPred++;
        }

        public void AddBet(double clv, bool won, double pnl)
        {
            _bets++;
            if (won) _hits++;
            _clvSum += clv;
            _pnl += pnl;
        }

        public Gate1aResult ToResult() => new()
        {
            Slice = name,
            GamesScored = _nPred,
            Bets = _bets,
            MeanClv = _bets == 0 ? 0 : _clvSum / _bets,
            HitRate = _bets == 0 ? 0 : (double)_hits / _bets,
            LogLoss = _nPred == 0 ? 0 : _logLoss / _nPred,
            Brier = _nPred == 0 ? 0 : _brier / _nPred,
            PaperPnlUnits = _pnl,
        };
    }
}

/// <summary>B2B / rest days from prior games only.</summary>
public sealed class RestTracker
{
    private readonly Dictionary<string, DateOnly> _last = new(StringComparer.Ordinal);

    public void Played(string team, DateOnly date) => _last[team] = date;

    /// <summary>Positive favors home (more rest than away).</summary>
    public double HomeRestEdge(string home, string away, DateOnly date)
    {
        return RestScore(home, date) - RestScore(away, date);
    }

    /// <summary>1 if team played yesterday (or same day), else 0. Unknown schedule ⇒ 0.</summary>
    public int B2bFlag(string team, DateOnly date)
    {
        if (!_last.TryGetValue(team, out var prev)) return 0;
        return date.DayNumber - prev.DayNumber <= 1 ? 1 : 0;
    }

    /// <summary>Count of B2B sides (0–2). Cheap totals load feature from TipoffUtc only.</summary>
    public int B2bCount(string home, string away, DateOnly date)
        => B2bFlag(home, date) + B2bFlag(away, date);

    private double RestScore(string team, DateOnly date)
    {
        if (!_last.TryGetValue(team, out var prev))
            return 0.02; // slight unknown/long-rest prior
        var days = date.DayNumber - prev.DayNumber;
        if (days <= 1) return -0.03; // B2B
        if (days == 2) return 0;
        if (days == 3) return 0.01;
        return 0.02;
    }
}
