namespace PossiblyMaybe.Domain;

/// <summary>
/// Parallel paper lanes through 2026-12-31. Each scheme starts at $1000 and sizes its own stake.
/// </summary>
public static class StakeTrial
{
    public const double SeedDollars = 1000;
    public static readonly DateOnly HorizonEnd = new(2026, 12, 31);
}

/// <summary>
/// Schemes that can stake above 2% (Kelly5) stay capped at 2% until live paper CLV clears.
/// Rule (R5): global — settled ClvPts rows reach <see cref="Checkpoints.PnlConfirmsN"/> and mean &gt; 0.
/// </summary>
public static class StakeGate
{
    public const int MinClvN = Checkpoints.PnlConfirmsN;
    public const double SoftCapFrac = 0.02;
    public const double WideCapFrac = 0.05;

    public static bool IsKelly5Promoted(int clvN, double meanClvPts)
        => Checkpoints.AllowsWideStake(clvN, meanClvPts);

    public static string StatusLine(int clvN, double meanClvPts)
    {
        var mean = clvN == 0 ? "n/a" : meanClvPts.ToString("+0.000;-0.000;0.000");
        return IsKelly5Promoted(clvN, meanClvPts)
            ? $"STAKE GATE: Kelly5 PROMOTED (clv_n={clvN}/{MinClvN} mean={mean})"
            : $"STAKE GATE: Kelly5 NOT PROMOTED (clv_n={clvN}/{MinClvN} mean={mean}; cap 2%)";
    }
}

/// <summary>Independent paper books — same schemes, separate $1000 seeds, separate GO decisions.</summary>
public static class PaperVenues
{
    public const string DraftKings = "DK";
    public const string Kalshi = "Kalshi";
    /// <summary>Robinhood event contracts — sports prices from Kalshi (CFTC partner); own bankroll.</summary>
    public const string Robinhood = "Robinhood";
    /// <summary>Crypto.com Predict — public data-api.crypto.com totals; own bankroll.</summary>
    public const string CryptoCom = "CryptoCom";

    public static IReadOnlyList<string> All { get; } =
        [DraftKings, Kalshi, Robinhood, CryptoCom];

    public static string Normalize(string? venue)
    {
        if (string.IsNullOrWhiteSpace(venue)) return DraftKings;
        if (venue.Equals("Kalshi", StringComparison.OrdinalIgnoreCase)) return Kalshi;
        if (venue.Equals("Robinhood", StringComparison.OrdinalIgnoreCase)
            || venue.Equals("RH", StringComparison.OrdinalIgnoreCase))
            return Robinhood;
        if (venue.Equals("CryptoCom", StringComparison.OrdinalIgnoreCase)
            || venue.Equals("Crypto", StringComparison.OrdinalIgnoreCase)
            || venue.Equals("Crypto.com", StringComparison.OrdinalIgnoreCase))
            return CryptoCom;
        if (venue.Equals("DK", StringComparison.OrdinalIgnoreCase)
            || venue.Equals("DraftKings", StringComparison.OrdinalIgnoreCase))
            return DraftKings;
        return venue.Trim();
    }

    public static string DisplayName(string venue) => Normalize(venue) switch
    {
        DraftKings => "DraftKings",
        Kalshi => "Kalshi",
        Robinhood => "Robinhood",
        CryptoCom => "Crypto.com",
        _ => venue,
    };
}

public readonly record struct StakeScheme(
    string Code,
    string DisplayName,
    string Rule);

/// <summary>Named staking schemes for multi-lane paper (level vs variable staking).</summary>
public static class StakeSchemes
{
    public static readonly StakeScheme Flat = new("Flat", "Level Flat", "$10 every GO");
    public static readonly StakeScheme Kelly2 = new("Kelly2", "Quarter Cap", "¼ Kelly, max 2% bankroll");
    public static readonly StakeScheme Kelly5 = new("Kelly5", "Quarter Wide", "¼ Kelly, max 5% bankroll");
    public static readonly StakeScheme EvProp = new("EvProp", "EV Proportional", "stake ∝ EV, floor 0.5%, cap 2%");
    public static readonly StakeScheme ConfBand = new("ConfBand", "Confidence Band", "|p−0.5| → 0.5–2% of bankroll");
    public static readonly StakeScheme EdgeBand = new("EdgeBand", "Edge Band", "|model−line| → 0.5–2% (sport-scaled)");

    public static IReadOnlyList<StakeScheme> All { get; } =
        [Flat, Kelly2, Kelly5, EvProp, ConfBand, EdgeBand];

    public static StakeScheme? TryGet(string code)
        => All.FirstOrDefault(s => s.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Stake in dollars for this scheme given current lane bankroll.
    /// <paramref name="absEdge"/> = |modelExp − line| (points/goals); used by EdgeBand.
    /// <paramref name="kelly5Promoted"/> must be true (live CLV gate) before Kelly5 may use the 5% cap.
    /// </summary>
    public static double StakeDollars(
        string schemeCode,
        double modelWinProb,
        int americanOdds,
        double bankroll,
        double absEdge = 0,
        string sport = "",
        bool kelly5Promoted = false)
    {
        if (bankroll <= 0 || modelWinProb is < 0 or > 1)
            return 0;

        var ev = OddsMath.ExpectedValue(modelWinProb, americanOdds);
        if (ev < 0.04)
            return 0;

        var kellyFrac = OddsMath.FractionalKelly(modelWinProb, americanOdds, 0.25);
        var kelly5Cap = kelly5Promoted ? StakeGate.WideCapFrac : StakeGate.SoftCapFrac;
        var code = schemeCode.ToUpperInvariant();
        return code switch
        {
            "FLAT" => 10.0,
            "KELLY2" => bankroll * Math.Min(StakeGate.SoftCapFrac, kellyFrac),
            "KELLY5" => bankroll * Math.Min(kelly5Cap, kellyFrac),
            "EVPROP" => bankroll * Math.Min(0.02, Math.Max(0.005, 0.35 * ev)),
            "CONFBAND" => bankroll * ConfFrac(modelWinProb),
            "EDGEBAND" => bankroll * EdgeFrac(absEdge, sport),
            _ => 0,
        };
    }

    static double ConfFrac(double p)
    {
        var conf = Math.Abs(p - 0.5);
        const double c0 = 0.02, c1 = 0.40, lo = 0.005, hi = 0.02;
        var x = (conf - c0) / (c1 - c0);
        x = Math.Clamp(x, 0, 1);
        return lo + (hi - lo) * x;
    }

    static double EdgeFrac(double absEdge, string sport)
    {
        var (e0, e1) = sport.ToUpperInvariant() switch
        {
            "NBA" => (6.0, 15.0),
            "SERIEA" => (0.35, 1.2),
            _ => (0.25, 1.0), // MLS / Ligue1 / LigaMX / default soccer
        };
        var x = (absEdge - e0) / (e1 - e0);
        x = Math.Clamp(x, 0, 1);
        const double lo = 0.005, hi = 0.02;
        return lo + (hi - lo) * x;
    }
}
