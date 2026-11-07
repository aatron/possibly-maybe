namespace PossiblyMaybe.Domain.SportPacks;

/// <summary>
/// NFL sub-markets for live shadow via line tracker (C3).
/// Full-game TOTAL has Tier A path; 1H / team totals are shadow-ready series notes only
/// until free O→C history exists (book_round2: IGNORE for money gates).
/// </summary>
public static class NflShadowMarkets
{
    public const string FullGameTotal = "TOTAL";
    public const string FirstHalfTotal = "1H_TOTAL";
    public const string TeamTotal = "TEAM_TOTAL";

    /// <summary>Kalshi series ids (public Trade API). Tracker polls full-game today.</summary>
    public static readonly IReadOnlyList<string> KalshiSeries =
    [
        "KXNFLTOTAL",
        "KXNFL1HTOTAL",
        "KXNFL1QTOTAL",
        "KXNFL2QTOTAL",
        "KXNFL3QTOTAL",
        "KXNFL4QTOTAL",
        "KXNFLTEAMTOTAL",
    ];

    /// <summary>Series the live tracker should capture for C3 shadow (full + 1H + team).</summary>
    public static readonly IReadOnlyList<string> ShadowTrackerSeries =
    [
        "KXNFLTOTAL",
        "KXNFL1HTOTAL",
        "KXNFLTEAMTOTAL",
    ];

    // shortcut: scripts/line_tracker.py SPORTS["NFL"] only sets kalshi=KXNFLTOTAL;
    // when A2 generalises, add 1H/team as sibling series rows → OddsSnapshots Market 1H_TOTAL / TEAM_TOTAL.
}
