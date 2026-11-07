using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Paper;

/// <summary>
/// Venue → closing book for CLV (R3). RH uses Kalshi ask + RH fee; cross-book closes are not CLV.
/// </summary>
public static class SameBookClose
{
    /// <summary>Warehouse book that counts as same-book for a paper venue.</summary>
    public static Book BookForVenue(string? venue) => PaperVenues.Normalize(venue) switch
    {
        PaperVenues.DraftKings => Book.DraftKings,
        PaperVenues.Kalshi => Book.Kalshi,
        PaperVenues.Robinhood => Book.Kalshi,
        PaperVenues.CryptoCom => Book.CryptoCom,
        _ => Book.Unknown,
    };

    /// <summary>Tracker <c>line_snapshots.venue</c> that feeds <see cref="BookForVenue"/>.</summary>
    public static string? TrackerVenueForBook(Book book) => book switch
    {
        Book.DraftKings => "espn",
        Book.Kalshi => "kalshi",
        Book.CryptoCom => "cryptocom",
        _ => null,
    };

    public static string BookLabel(Book book) => book switch
    {
        Book.DraftKings => "DraftKings",
        Book.Kalshi => "Kalshi",
        Book.CryptoCom => "CryptoCom",
        Book.Consensus => "Consensus",
        Book.Pinnacle => "Pinnacle",
        Book.FanDuel => "FanDuel",
        Book.Mgm => "Mgm",
        _ => book.ToString(),
    };

    /// <summary>Apply RH fee to a Kalshi-style American close when venue is Robinhood.</summary>
    public static int? AdjustCloseAmerican(int? american, string? venue)
    {
        if (american is null) return null;
        if (PaperVenues.Normalize(venue) != PaperVenues.Robinhood) return american;
        var buy = OddsMath.ImpliedProbabilityFromAmerican(american.Value);
        if (buy is <= 0 or >= 1) return american;
        return OddsMath.AmericanFromBuyPrice(OddsMath.EffectiveBuyPrice(buy, PaperVenues.Robinhood));
    }

    /// <summary>True when close was captured within <paramref name="maxMinutes"/> before tip.</summary>
    public static bool IsFresh(double? minutesBeforeStart, double maxMinutes = 15)
        => minutesBeforeStart is { } m && m > 0 && m <= maxMinutes;
}

/// <summary>Resolved market close for settle / CLV (R3).</summary>
public readonly record struct ResolvedSettleClose(
    double? CloseLine,
    int? CloseAmerican,
    double? FinalTotal,
    string? CloseBook,
    DateTimeOffset? CloseCapturedAt,
    double? MinutesBeforeStart,
    bool SameBook);
