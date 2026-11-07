using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Replay;

/// <summary>
/// Builds <see cref="AsOfView"/> from in-memory snapshots or the warehouse DB.
/// Factory may hold a DbContext; strategies never do (R7).
/// </summary>
public static class AsOfViewFactory
{
    /// <summary>Pure filter path — used by leak tests and the day engine once snapshots are loaded.</summary>
    public static AsOfView FromSnapshots(
        DateTimeOffset asOf,
        IEnumerable<FinalGameRow>? finals = null,
        IEnumerable<OddsAsOfRow>? odds = null,
        IEnumerable<InjuryRosterRow>? injuries = null,
        string? seasonPhase = null)
        => new(asOf, finals, odds, injuries, seasonPhase);

    /// <summary>
    /// Load scored games + odds from the warehouse, then cut at asOf via <see cref="AsOfView"/>.
    /// shortcut: no injury/roster table yet — injuries stay empty until a store lands; upgrade with B1 live feed.
    /// shortcut: loads all scored games then filters in memory; upgrade to tipoff-range SQL when R9 walks seasons.
    /// </summary>
    public static async Task<AsOfView> FromDbAsync(
        AppDbContext db,
        DateTimeOffset asOf,
        string? sport = null,
        string? seasonPhase = null,
        CancellationToken ct = default)
    {
        // Pull scored rows (and odds captures) then let AsOfView re-filter — SQLite struggles with
        // DateTimeOffset + nullable score predicates in one translated Where.
        var gamesQ = db.Games.AsNoTracking().Where(g => g.HomeScore != null && g.AwayScore != null);
        if (sport is not null)
            gamesQ = gamesQ.Where(g => g.Sport == sport);

        var games = await gamesQ.ToListAsync(ct);
        var finals = PointInTime.AsOf(games, g => g.TipoffUtc, asOf)
            .Select(g => new FinalGameRow(
                g.Id, g.Sport, g.Season, g.TipoffUtc, g.HomeTeam, g.AwayTeam,
                g.HomeScore!.Value, g.AwayScore!.Value));

        var oddsQ = db.OddsSnapshots.AsNoTracking().AsQueryable();
        if (sport is not null)
        {
            oddsQ =
                from o in db.OddsSnapshots.AsNoTracking()
                join g in db.Games.AsNoTracking() on o.GameId equals g.Id
                where g.Sport == sport
                select o;
        }

        var oddsRows = await oddsQ.ToListAsync(ct);
        var odds = oddsRows
            .Where(o => o.CapturedAt <= asOf)
            .Select(o => new OddsAsOfRow(
                o.Id, o.GameId, o.Book.ToString(), o.Market, o.Side,
                o.AmericanOdds, o.Line, o.CapturedAt));

        return FromSnapshots(asOf, finals, odds, injuries: null, seasonPhase);
    }
}
