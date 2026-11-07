using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Cli.Import;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Paper;

public static class PaperLedger
{
    public static string DefaultDbPath(string repoRoot) => Path.Combine(repoRoot, "paper.db");

    public static async Task<(AppDbContext Db, string Path)> OpenAsync(string dbPath, CancellationToken ct = default)
    {
        var cs = $"Data Source={dbPath}";
        await AppDbContext.EnsureCreatedAsync(cs, ct);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(cs).Options;
        return (new AppDbContext(options), dbPath);
    }

    /// <summary>Settled equity (seed + realized PnL). Open stakes are still inside this number.</summary>
    public static async Task<double> EquityAsync(AppDbContext db, string scheme, string venue, CancellationToken ct = default)
    {
        venue = PaperVenues.Normalize(venue);
        var pnl = await db.LedgerEntries.AsNoTracking()
            .Where(r => r.Scheme == scheme && r.Venue == venue && r.PnL != null)
            .SumAsync(r => r.PnL!.Value, ct);
        return StakeTrial.SeedDollars + pnl;
    }

    /// <summary>Dollars tied up in unsettled tickets.</summary>
    public static async Task<double> ReservedAsync(AppDbContext db, string scheme, string venue, CancellationToken ct = default)
    {
        venue = PaperVenues.Normalize(venue);
        return await db.LedgerEntries.AsNoTracking()
            .Where(r => r.Scheme == scheme && r.Venue == venue && r.Result == null)
            .SumAsync(r => r.StakeDollars, ct);
    }

    /// <summary>Cash free to size the next bet = equity − open stakes.</summary>
    public static async Task<double> BankrollAsync(AppDbContext db, string scheme, string venue, CancellationToken ct = default)
        => Math.Max(0, await EquityAsync(db, scheme, venue, ct) - await ReservedAsync(db, scheme, venue, ct));

    /// <summary>Global live CLV: all settled paper rows with ClvPts (every venue/scheme).</summary>
    public static async Task<(int N, double Mean)> LiveClvStatsAsync(AppDbContext db, CancellationToken ct = default)
    {
        var pts = await db.LedgerEntries.AsNoTracking()
            .Where(r => r.Result != null && r.ClvPts != null)
            .Select(r => r.ClvPts!.Value)
            .ToListAsync(ct);
        if (pts.Count == 0)
            return (0, 0);
        return (pts.Count, pts.Average());
    }

    public static async Task<bool> Kelly5PromotedAsync(AppDbContext db, CancellationToken ct = default)
    {
        var (n, mean) = await LiveClvStatsAsync(db, ct);
        return StakeGate.IsKelly5Promoted(n, mean);
    }

    /// <summary>
    /// Independent multi-venue place: DK + Kalshi + Robinhood (Kalshi prices) + Crypto.com each EV-gate alone.
    /// </summary>
    public static async Task<(IReadOnlyList<LedgerEntry> Dk, IReadOnlyList<VenuePlaceNote> Others)> PlaceDualVenueAsync(
        AppDbContext db,
        string gameKey,
        string side,
        double line,
        int dkAmericanOdds,
        double modelProb,
        double absEdge,
        string sport,
        CancellationToken ct = default)
    {
        var dk = await PlaceAllSchemesAsync(
            db, gameKey, side, line, dkAmericanOdds, modelProb, absEdge, sport, PaperVenues.DraftKings, ct);

        var others = new List<VenuePlaceNote>();

        // Kalshi + Robinhood share Kalshi public asks; separate $1000 lane books.
        var kalshiQuote = await KalshiQuotes.TryGetTotalAsync(sport, gameKey, side, line, ct);
        BinaryAsk? kalAsk = kalshiQuote is { } kq
            ? new BinaryAsk(kq.Strike, kq.BuyPrice, kq.AmericanOdds, kq.SideLabel, kq.MarketTicker)
            : null;
        others.Add(await PlaceFromBinaryQuoteAsync(
            db, gameKey, side, modelProb, absEdge, sport, PaperVenues.Kalshi, kalAsk, ct));
        others.Add(await PlaceFromBinaryQuoteAsync(
            db, gameKey, side, modelProb, absEdge, sport, PaperVenues.Robinhood, kalAsk, ct));

        var crypto = await CryptoComQuotes.TryGetTotalAsync(sport, gameKey, side, line, ct);
        BinaryAsk? cryptoAsk = crypto is { } cq
            ? new BinaryAsk(cq.Strike, cq.BuyPrice, cq.AmericanOdds, cq.SideLabel, cq.EventId)
            : null;
        others.Add(await PlaceFromBinaryQuoteAsync(
            db, gameKey, side, modelProb, absEdge, sport, PaperVenues.CryptoCom, cryptoAsk, ct));

        return (dk, others);
    }

    public readonly record struct VenuePlaceNote(string Venue, IReadOnlyList<LedgerEntry> Placed, string Note);

    readonly record struct BinaryAsk(double Strike, double BuyPrice, int AmericanOdds, string SideLabel, string Ticker);

    static async Task<VenuePlaceNote> PlaceFromBinaryQuoteAsync(
        AppDbContext db,
        string gameKey,
        string side,
        double modelProb,
        double absEdge,
        string sport,
        string venue,
        BinaryAsk? quote,
        CancellationToken ct)
    {
        var label = PaperVenues.DisplayName(venue);
        if (quote is null)
            return new VenuePlaceNote(venue, [], $"{label} SKIP — no matching open total market");

        var q = quote.Value;
        // Fee-adjusted American drives EV gate, Kelly sizing, and settle payout (venue_placement #1).
        var effectiveBuy = OddsMath.EffectiveBuyPrice(q.BuyPrice, venue);
        var american = OddsMath.AmericanFromBuyPrice(effectiveBuy);
        var ev = OddsMath.ExpectedValue(modelProb, american);
        if (ev < 0.04)
        {
            return new VenuePlaceNote(
                venue, [],
                $"{label} SKIP — EV {ev:P1} < 4% at {q.SideLabel} ask=${q.BuyPrice:F3} all-in=${effectiveBuy:F3} (~{american:+0;-0}) strike={q.Strike}");
        }

        var placed = await PlaceAllSchemesAsync(
            db, gameKey, side, q.Strike, american, modelProb, absEdge, sport, venue, ct);
        var note = placed.Count == 0
            ? $"{label} SKIP — EV ok but all lanes dust/ruined @ {american:+0;-0}"
            : $"{label} GO — {q.SideLabel} ask=${q.BuyPrice:F3} all-in=${effectiveBuy:F3} (~{american:+0;-0}) strike={q.Strike} id={q.Ticker}";
        return new VenuePlaceNote(venue, placed, note);
    }

    /// <summary>Place the same GO across every stake scheme on one venue (each lane sizes from its own bankroll).</summary>
    public static async Task<IReadOnlyList<LedgerEntry>> PlaceAllSchemesAsync(
        AppDbContext db,
        string gameKey,
        string side,
        double line,
        int americanOdds,
        double modelProb,
        double absEdge,
        string sport,
        string venue = PaperVenues.DraftKings,
        CancellationToken ct = default)
    {
        side = side.ToUpperInvariant();
        venue = PaperVenues.Normalize(venue);
        if (side is not ("HOME" or "AWAY" or "OVER" or "UNDER"))
            throw new ArgumentException("side must be HOME, AWAY, OVER, or UNDER");
        if (modelProb is < 0 or > 1)
            throw new ArgumentException("modelProb must be in [0,1]");

        var already = await db.LedgerEntries.AsNoTracking()
            .AnyAsync(r => r.Result == null && r.Venue == venue && r.GameKey == gameKey, ct);
        if (already)
            return [];

        var group = Guid.NewGuid().ToString("N")[..12];
        var kelly5Promoted = await Kelly5PromotedAsync(db, ct);
        var placed = new List<LedgerEntry>();
        foreach (var scheme in StakeSchemes.All)
        {
            var bank = await BankrollAsync(db, scheme.Code, venue, ct);
            var stake = StakeSchemes.StakeDollars(
                scheme.Code, modelProb, americanOdds, bank, absEdge, sport, kelly5Promoted);
            if (stake < 0.50)
                continue; // skip dust / ruined / no-EV

            var row = new LedgerEntry
            {
                PlacedAt = DateTimeOffset.UtcNow,
                GameKey = gameKey,
                Market = ChampConfig.Market,
                Side = side,
                Line = line,
                AmericanOdds = americanOdds,
                StakeDollars = Math.Round(stake, 2),
                ModelProb = modelProb,
                IsPaper = true,
                Scheme = scheme.Code,
                TicketGroup = group,
                AbsEdge = absEdge,
                Sport = sport.ToUpperInvariant(),
                Venue = venue,
            };
            db.LedgerEntries.Add(row);
            placed.Add(row);
        }

        await db.SaveChangesAsync(ct);
        return placed;
    }

    public static async Task<LedgerEntry> PlaceAsync(
        AppDbContext db,
        string gameKey,
        string side,
        double line,
        int americanOdds,
        double stakeDollars,
        double modelProb,
        CancellationToken ct = default)
    {
        // Legacy single-lane helper → Flat only
        side = side.ToUpperInvariant();
        if (side is not ("HOME" or "AWAY" or "OVER" or "UNDER"))
            throw new ArgumentException("side must be HOME, AWAY, OVER, or UNDER");
        if (stakeDollars <= 0)
            throw new ArgumentException("stake must be > 0");

        var row = new LedgerEntry
        {
            PlacedAt = DateTimeOffset.UtcNow,
            GameKey = gameKey,
            Market = ChampConfig.Market,
            Side = side,
            Line = line,
            AmericanOdds = americanOdds,
            StakeDollars = stakeDollars,
            ModelProb = modelProb,
            IsPaper = true,
            Scheme = StakeSchemes.Flat.Code,
            TicketGroup = Guid.NewGuid().ToString("N")[..12],
            Venue = PaperVenues.DraftKings,
        };
        db.LedgerEntries.Add(row);
        await db.SaveChangesAsync(ct);
        return row;
    }

    public static async Task<LedgerEntry> SettleAsync(
        AppDbContext db,
        int id,
        string result,
        double? finalTotal,
        double? closeLine,
        int? closeAmerican,
        ResolvedSettleClose? meta = null,
        CancellationToken ct = default)
    {
        result = result.ToUpperInvariant();
        if (result is not ("W" or "L" or "P"))
            throw new ArgumentException("result must be W, L, or P");

        var row = await db.LedgerEntries.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"ledger id {id} not found");
        ApplySettle(row, result, finalTotal, closeLine, closeAmerican, meta);
        await db.SaveChangesAsync(ct);
        return row;
    }

    /// <summary>
    /// Settle every scheme lane for a ticket group or game key.
    /// When <paramref name="closeLine"/> is null and warehouse paths are set, resolves same-book close per venue.
    /// </summary>
    public static async Task<IReadOnlyList<LedgerEntry>> SettleGroupAsync(
        AppDbContext db,
        string groupOrGameKey,
        string result,
        double? finalTotal,
        double? closeLine,
        int? closeAmerican,
        string? warehouseDbPath = null,
        string? snapshotsDbPath = null,
        CancellationToken ct = default)
    {
        result = result.ToUpperInvariant();
        if (result is not ("W" or "L" or "P"))
            throw new ArgumentException("result must be W, L, or P");

        var rows = await db.LedgerEntries
            .Where(r => r.Result == null && (r.TicketGroup == groupOrGameKey || r.GameKey == groupOrGameKey))
            .ToListAsync(ct);
        if (rows.Count == 0)
            throw new InvalidOperationException($"no open tickets for '{groupOrGameKey}'");

        foreach (var venueGroup in rows.GroupBy(r => PaperVenues.Normalize(r.Venue)))
        {
            ResolvedSettleClose? meta = null;
            double? cl = closeLine;
            int? ca = closeAmerican;
            double? ft = finalTotal;
            if (!string.IsNullOrEmpty(warehouseDbPath) && (cl is null || ft is null))
            {
                var sample = venueGroup.First();
                var resolved = await ResolveSettleLinesAsync(
                    warehouseDbPath!, snapshotsDbPath, sample.GameKey, sample.Sport, venueGroup.Key, ct);
                ft ??= resolved.FinalTotal;
                if (cl is null)
                {
                    meta = resolved;
                    cl = resolved.CloseLine;
                    ca ??= resolved.CloseAmerican;
                }
            }

            foreach (var row in venueGroup)
                ApplySettle(row, result, ft, cl, ca, meta);
        }

        await db.SaveChangesAsync(ct);
        return rows;
    }

    /// <summary>
    /// FinalTotal = score total. CloseLine stored from market; ClvPts only on same-book close (R3).
    /// </summary>
    public static void ApplySettle(
        LedgerEntry row, string result, double? finalTotal, double? closeLine, int? closeAmerican,
        ResolvedSettleClose? meta = null)
    {
        row.Result = result;
        row.FinalTotal = finalTotal;
        row.CloseLine = closeLine;
        row.CloseAmerican = closeAmerican;
        row.PnL = result switch
        {
            "W" => row.StakeDollars * OddsMath.AmericanToDecimal(row.AmericanOdds) - row.StakeDollars,
            "L" => -row.StakeDollars,
            _ => 0,
        };
        row.ClvPts = null;
        row.CloseBook = meta?.CloseBook;
        row.CloseCapturedAt = meta?.CloseCapturedAt;
        row.MinutesBeforeStart = meta?.MinutesBeforeStart;
        row.CrossBookClose = meta is { SameBook: false, CloseLine: not null };
        // Explicit CLI close with no meta → treat as same-book for the ticket venue.
        var sameBook = meta?.SameBook ?? closeLine is not null;
        if (sameBook && closeLine is { } cl)
        {
            row.CrossBookClose = false;
            row.CloseBook ??= SameBookClose.BookLabel(SameBookClose.BookForVenue(row.Venue));
            row.ClvPts = row.Side is "OVER" ? cl - row.Line
                : row.Side is "UNDER" ? row.Line - cl
                : row.Line - cl;
        }
    }

    /// <summary>
    /// Market close + final for a paper game key, preferring the ticket venue's book (R3).
    /// Cross-book closes are returned with <see cref="ResolvedSettleClose.SameBook"/> = false.
    /// </summary>
    public static async Task<ResolvedSettleClose> ResolveSettleLinesAsync(
        string warehouseDbPath,
        string? snapshotsDbPath,
        string gameKey,
        string sport,
        string? venue = null,
        CancellationToken ct = default)
    {
        var want = SameBookClose.BookForVenue(venue);
        double? final = null;
        OddsSnapshot? same = null;
        OddsSnapshot? other = null;
        DateTimeOffset? tip = null;

        if (File.Exists(warehouseDbPath))
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={warehouseDbPath}").Options;
            await using var wdb = new AppDbContext(options);
            var game = await FindWarehouseGameAsync(wdb, sport, gameKey, ct);
            if (game is not null)
            {
                tip = game.TipoffUtc;
                if (game.HomeScore is { } hs && game.AwayScore is { } as_)
                    final = hs + as_;

                var snaps = await wdb.OddsSnapshots.AsNoTracking()
                    .Where(o => o.GameId == game.Id && o.Market == "TOTAL" && o.Line != null)
                    .ToListAsync(ct);
                // Prefer labelled Close; else last capture at/before tip for that book.
                same = PickBookClose(snaps, want, game.TipoffUtc);
                if (same is null)
                    other = snaps
                        .Where(s => s.PriceKind == PriceKind.Close || s.CapturedAt <= game.TipoffUtc)
                        .OrderByDescending(s => s.PriceKind == PriceKind.Close)
                        .ThenByDescending(s => s.CapturedAt)
                        .FirstOrDefault();
            }
        }

        if (same is null && !string.IsNullOrEmpty(snapshotsDbPath)
            && LineTrackerImporter.TryCloseFromSnapshots(
                snapshotsDbPath!, sport, gameKey, SameBookClose.TrackerVenueForBook(want)) is { } tr)
        {
            var mins = tr.MinutesToStart;
            var captured = tip is { } t && mins is { } m ? t.AddMinutes(-m) : (DateTimeOffset?)null;
            var am = SameBookClose.AdjustCloseAmerican(tr.American, venue);
            return new ResolvedSettleClose(
                tr.Line, am, final, SameBookClose.BookLabel(want), captured, mins, SameBook: true);
        }

        if (same is { Line: { } sameLine })
        {
            var mins = tip is { } t ? (t - same.CapturedAt).TotalMinutes : (double?)null;
            var am = SameBookClose.AdjustCloseAmerican(same.AmericanOdds, venue);
            return new ResolvedSettleClose(
                sameLine, am, final, SameBookClose.BookLabel(same.Book), same.CapturedAt, mins, SameBook: true);
        }

        if (other is { Line: { } otherLine })
        {
            var mins = tip is { } t ? (t - other.CapturedAt).TotalMinutes : (double?)null;
            return new ResolvedSettleClose(
                otherLine, other.AmericanOdds, final, SameBookClose.BookLabel(other.Book),
                other.CapturedAt, mins, SameBook: false);
        }

        return new ResolvedSettleClose(null, null, final, null, null, null, SameBook: false);
    }

    static OddsSnapshot? PickBookClose(
        IReadOnlyList<OddsSnapshot> snaps, Book want, DateTimeOffset tipoff)
    {
        if (want == Book.Unknown) return null;
        var bookSnaps = snaps.Where(s => s.Book == want).ToList();
        if (bookSnaps.Count == 0) return null;
        var labelled = bookSnaps
            .Where(s => s.PriceKind == PriceKind.Close)
            .OrderByDescending(s => s.CapturedAt)
            .FirstOrDefault();
        if (labelled is not null) return labelled;
        return bookSnaps
            .Where(s => s.CapturedAt <= tipoff)
            .OrderByDescending(s => s.CapturedAt)
            .FirstOrDefault();
    }

    /// <summary>
    /// Null fake CloseLine/ClvPts (finals masquerading as closes); set FinalTotal from scores when known;
    /// refill CloseLine only when a market close is knowable. Returns (settled, withSameBookClv).
    /// </summary>
    public static async Task<(int Settled, int WithTrueClose)> BackfillLiveClvAsync(
        AppDbContext paperDb,
        string warehouseDbPath,
        string? snapshotsDbPath,
        CancellationToken ct = default)
    {
        var rows = await paperDb.LedgerEntries
            .Where(r => r.Result != null)
            .ToListAsync(ct);
        var withClose = 0;
        foreach (var group in rows.GroupBy(r => (r.GameKey, r.Sport, Venue: PaperVenues.Normalize(r.Venue))))
        {
            var resolved = await ResolveSettleLinesAsync(
                warehouseDbPath, snapshotsDbPath, group.Key.GameKey, group.Key.Sport, group.Key.Venue, ct);
            foreach (var row in group)
            {
                if (resolved.FinalTotal is null && row.FinalTotal is null
                    && row.CloseLine is { } oldCl && resolved.CloseLine is null)
                    row.FinalTotal = oldCl; // prior settles stuffed the final into CloseLine
                else if (resolved.FinalTotal is not null)
                    row.FinalTotal = resolved.FinalTotal;

                ApplyCloseFields(row, resolved);
                if (resolved.SameBook && resolved.CloseLine is not null)
                    withClose++;
            }
        }

        await paperDb.SaveChangesAsync(ct);
        return (rows.Count, withClose);
    }

    static void ApplyCloseFields(LedgerEntry row, ResolvedSettleClose resolved)
    {
        if (resolved.CloseLine is { } cl)
        {
            row.CloseLine = cl;
            row.CloseAmerican = resolved.CloseAmerican;
            row.CloseBook = resolved.CloseBook;
            row.CloseCapturedAt = resolved.CloseCapturedAt;
            row.MinutesBeforeStart = resolved.MinutesBeforeStart;
            row.CrossBookClose = !resolved.SameBook;
            if (resolved.SameBook)
            {
                row.ClvPts = row.Side is "OVER" ? cl - row.Line
                    : row.Side is "UNDER" ? row.Line - cl
                    : row.Line - cl;
            }
            else
                row.ClvPts = null;
        }
        else
        {
            row.CloseLine = null;
            row.CloseAmerican = null;
            row.CloseBook = null;
            row.CloseCapturedAt = null;
            row.MinutesBeforeStart = null;
            row.CrossBookClose = false;
            row.ClvPts = null;
        }
    }

    public static async Task<(int Settled, int WithTrueClose)> TrueCloseStatsAsync(
        AppDbContext db, CancellationToken ct = default)
    {
        var settled = await db.LedgerEntries.AsNoTracking()
            .Where(r => r.Result != null).ToListAsync(ct);
        var with = settled.Count(r => r.CloseLine is not null && r.ClvPts is not null && !r.CrossBookClose);
        return (settled.Count, with);
    }

    /// <summary>Same-book close ≤15 min before tip, per sport (R3 proof).</summary>
    public static async Task<IReadOnlyList<(string Sport, int Settled, int SameBook, int Fresh15)>>
        SameBookCloseStatsAsync(AppDbContext db, CancellationToken ct = default)
    {
        var settled = await db.LedgerEntries.AsNoTracking()
            .Where(r => r.Result != null).ToListAsync(ct);
        return settled
            .GroupBy(r => string.IsNullOrEmpty(r.Sport) ? "?" : r.Sport)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var n = g.Count();
                var same = g.Count(r => r.CloseLine is not null && !r.CrossBookClose && r.ClvPts is not null);
                var fresh = g.Count(r =>
                    r.CloseLine is not null && !r.CrossBookClose && SameBookClose.IsFresh(r.MinutesBeforeStart));
                return (g.Key, n, same, fresh);
            })
            .ToList();
    }

    static async Task<Game?> FindWarehouseGameAsync(
        AppDbContext db, string sport, string gameKey, CancellationToken ct)
    {
        if (!TryParseGameKey(gameKey, out var ymd, out var away, out var home))
            return null;
        if (!DateTime.TryParseExact(ymd, "yyyyMMdd", null,
                System.Globalization.DateTimeStyles.None, out var day))
            return null;

        // Exact team match in SQL; ±1 day tipoff in memory (SQLite DateTimeOffset ranges flake).
        // shortcut: no fuzzy team aliasing — pass finalTotal on settle-go when names diverge.
        var q = db.Games.AsNoTracking().Where(g => g.HomeTeam == home && g.AwayTeam == away);
        if (!string.IsNullOrWhiteSpace(sport))
            q = q.Where(g => g.Sport == sport);
        var games = await q.ToListAsync(ct);

        var start = new DateTimeOffset(DateTime.SpecifyKind(day.AddDays(-1), DateTimeKind.Utc));
        var end = start.AddDays(3);
        var hits = games.Where(g => g.TipoffUtc >= start && g.TipoffUtc < end).ToList();
        if (hits.Count == 0) return null;
        var dayOnly = DateOnly.FromDateTime(day);
        return hits
            .OrderBy(g => Math.Abs(
                DateOnly.FromDateTime(g.TipoffUtc.UtcDateTime).DayNumber - dayOnly.DayNumber))
            .ThenBy(g => g.TipoffUtc)
            .First();
    }

    static bool TryParseGameKey(string key, out string ymd, out string away, out string home)
    {
        ymd = away = home = "";
        var sp = key.IndexOf(' ');
        if (sp <= 0) return false;
        ymd = key[..sp];
        var rest = key[(sp + 1)..];
        var at = rest.IndexOf('@');
        if (at <= 0) return false;
        away = rest[..at].Trim();
        home = rest[(at + 1)..].Trim();
        return ymd.Length == 8 && away.Length > 0 && home.Length > 0;
    }

    public static async Task PrintStatusAsync(AppDbContext db, CancellationToken ct = default)
    {
        Console.WriteLine($"Stake trial → {StakeTrial.HorizonEnd:yyyy-MM-dd}  seed ${StakeTrial.SeedDollars:F0}/lane/venue");
        var (clvN, clvMean) = await LiveClvStatsAsync(db, ct);
        Console.WriteLine(StakeGate.StatusLine(clvN, clvMean));
        var (settledN, trueCloseN) = await TrueCloseStatsAsync(db, ct);
        var pct = settledN == 0 ? 0.0 : 100.0 * trueCloseN / settledN;
        Console.WriteLine($"Same-book CLV: {trueCloseN}/{settledN} settled ({pct:0.#}%) have same-book CloseLine");
        foreach (var (sport, n, same, fresh) in await SameBookCloseStatsAsync(db, ct))
        {
            var sp = n == 0 ? 0.0 : 100.0 * same / n;
            var fp = n == 0 ? 0.0 : 100.0 * fresh / n;
            Console.WriteLine(
                $"  {sport}: same-book {same}/{n} ({sp:0.#}%); ≤15m {fresh}/{n} ({fp:0.#}%)");
        }
        foreach (var venue in PaperVenues.All)
        {
            Console.WriteLine($"--- {venue} leaderboard ---");
            var board = new List<(string Code, string Name, double Equity, double Reserved, double Avail, int N, int Open)>();
            foreach (var s in StakeSchemes.All)
            {
                var rows = await db.LedgerEntries.AsNoTracking()
                    .Where(r => r.Scheme == s.Code && r.Venue == venue).ToListAsync(ct);
                var settled = rows.Where(r => r.Result is not null).ToList();
                var openRows = rows.Where(r => r.Result is null).ToList();
                var equity = StakeTrial.SeedDollars + settled.Sum(r => r.PnL ?? 0);
                var reserved = openRows.Sum(r => r.StakeDollars);
                board.Add((s.Code, s.DisplayName, equity, reserved, Math.Max(0, equity - reserved), settled.Count, openRows.Count));
            }

            foreach (var b in board.OrderByDescending(x => x.Equity))
                Console.WriteLine(
                    $"  {b.Code,-10} {b.Name,-18} equity=${b.Equity:F2}  at-risk=${b.Reserved:F2}  avail=${b.Avail:F2}  settled={b.N}  open={b.Open}");
        }

        var open = (await db.LedgerEntries.AsNoTracking()
                .Where(r => r.Result == null)
                .ToListAsync(ct))
            .OrderBy(r => r.Id)
            .ToList();
        Console.WriteLine($"--- open tickets ({open.Count}) ---");
        foreach (var g in open.GroupBy(r => r.Venue + "|" + r.TicketGroup + "|" + r.GameKey).TakeLast(30))
        {
            var sample = g.First();
            var stakes = string.Join(" ", g.Select(r => $"{r.Scheme}=${r.StakeDollars:F2}"));
            Console.WriteLine(
                $"  [{sample.Venue}] {sample.GameKey} {sample.Side} {sample.Line} @{sample.AmericanOdds}  group={sample.TicketGroup}  {stakes}");
        }
    }

    public static async Task MigrateLegacyAsync(AppDbContext db, CancellationToken ct = default)
    {
        var legacy = await db.LedgerEntries
            .Where(r => r.Scheme == null || r.Scheme == "" || r.Scheme == "Flat")
            .ToListAsync(ct);
        // Existing $20 MLS opens were ~2% of $1000 → tag Kelly2
        foreach (var r in legacy.Where(r => Math.Abs(r.StakeDollars - 20) < 0.01 && r.Result == null))
        {
            r.Scheme = StakeSchemes.Kelly2.Code;
            if (string.IsNullOrEmpty(r.TicketGroup))
                r.TicketGroup = Guid.NewGuid().ToString("N")[..12];
            if (string.IsNullOrEmpty(r.Sport))
                r.Sport = "MLS";
        }

        foreach (var r in await db.LedgerEntries.Where(r => r.Venue == null || r.Venue == "").ToListAsync(ct))
            r.Venue = PaperVenues.DraftKings;

        await db.SaveChangesAsync(ct);
        await FanOutOpenTicketsAsync(db, ct);
    }

    /// <summary>
    /// Within each venue, every open GO must exist on every scheme lane.
    /// Does not copy DK tickets onto Kalshi (venues decide independently).
    /// </summary>
    public static async Task FanOutOpenTicketsAsync(AppDbContext db, CancellationToken ct = default)
    {
        var open = await db.LedgerEntries.Where(r => r.Result == null).ToListAsync(ct);
        foreach (var game in open
                     .GroupBy(r => PaperVenues.Normalize(r.Venue) + "\0" + r.GameKey)
                     .OrderBy(g => g.Min(x => x.Id)))
        {
            var sample = game.OrderBy(r => r.Id).First();
            var venue = PaperVenues.Normalize(sample.Venue);
            var group = game.Select(r => r.TicketGroup).FirstOrDefault(g => !string.IsNullOrEmpty(g))
                        ?? Guid.NewGuid().ToString("N")[..12];
            foreach (var r in game)
            {
                r.Venue = venue;
                if (r.TicketGroup != group)
                    r.TicketGroup = group;
            }

            var have = game.Select(r => r.Scheme).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var kelly5Promoted = await Kelly5PromotedAsync(db, ct);
            foreach (var scheme in StakeSchemes.All)
            {
                if (have.Contains(scheme.Code))
                    continue;

                var bank = await BankrollAsync(db, scheme.Code, venue, ct);
                var stake = StakeSchemes.StakeDollars(
                    scheme.Code, sample.ModelProb, sample.AmericanOdds, bank, sample.AbsEdge, sample.Sport,
                    kelly5Promoted);
                if (stake < 0.50)
                    continue;
                db.LedgerEntries.Add(new LedgerEntry
                {
                    PlacedAt = sample.PlacedAt,
                    GameKey = sample.GameKey,
                    Market = sample.Market,
                    Side = sample.Side,
                    Line = sample.Line,
                    AmericanOdds = sample.AmericanOdds,
                    StakeDollars = Math.Round(stake, 2),
                    ModelProb = sample.ModelProb,
                    IsPaper = true,
                    Scheme = scheme.Code,
                    TicketGroup = group,
                    AbsEdge = sample.AbsEdge,
                    Sport = sample.Sport,
                    Venue = venue,
                });
            }

            await db.SaveChangesAsync(ct);
        }
    }

    public static async Task WriteDashboardAsync(AppDbContext db, string uiDir, CancellationToken ct = default)
    {
        Directory.CreateDirectory(uiDir);
        var rows = (await db.LedgerEntries.AsNoTracking().ToListAsync(ct))
            .OrderBy(r => r.PlacedAt).ThenBy(r => r.Id)
            .ToList();

        var venues = new Dictionary<string, object>();
        foreach (var venue in PaperVenues.All)
            venues[venue] = BuildVenuePayload(rows.Where(r => PaperVenues.Normalize(r.Venue) == venue).ToList());

        var json = JsonSerializer.Serialize(new
        {
            generatedAt = DateTimeOffset.UtcNow.ToString("o"),
            horizonEnd = StakeTrial.HorizonEnd.ToString("yyyy-MM-dd"),
            seed = StakeTrial.SeedDollars,
            venues,
        }, new JsonSerializerOptions { WriteIndented = true });

        var jsonPath = Path.Combine(uiDir, "data.json");
        await File.WriteAllTextAsync(jsonPath, json, ct);
        var htmlPath = Path.Combine(uiDir, "index.html");
        await File.WriteAllTextAsync(htmlPath, DashboardHtml(json), ct);
        Console.WriteLine($"Dashboard → {htmlPath}");
        Console.WriteLine($"Data      → {jsonPath}");
        Console.WriteLine("Open ui/index.html — tabs: All | DraftKings | Kalshi | Robinhood | Crypto.com (re-run `dashboard` after settles).");
    }

    static object BuildVenuePayload(List<LedgerEntry> rows)
    {
        var lanes = new List<object>();
        foreach (var s in StakeSchemes.All)
        {
            var mine = rows.Where(r => r.Scheme == s.Code).ToList();
            var settled = mine.Where(r => r.Result != null)
                .OrderBy(r => r.PlacedAt).ThenBy(r => r.Id).ToList();
            var openRows = mine.Where(r => r.Result == null).ToList();
            var reserved = openRows.Sum(r => r.StakeDollars);

            double bank = StakeTrial.SeedDollars;
            var t0 = settled.Count > 0
                ? settled[0].PlacedAt
                : mine.OrderBy(r => r.PlacedAt).Select(r => (DateTimeOffset?)r.PlacedAt).FirstOrDefault()
                  ?? DateTimeOffset.UtcNow;
            var equity = new List<object> { new { t = t0.ToString("o"), bank = StakeTrial.SeedDollars } };
            foreach (var r in settled)
            {
                bank += r.PnL ?? 0;
                equity.Add(new { t = r.PlacedAt.ToString("o"), bank = Math.Round(bank, 2) });
            }

            if (equity.Count == 1)
                equity.Add(new { t = DateTimeOffset.UtcNow.ToString("o"), bank = StakeTrial.SeedDollars });

            var peak = StakeTrial.SeedDollars;
            var maxDd = 0.0;
            var run = StakeTrial.SeedDollars;
            foreach (var r in settled)
            {
                run += r.PnL ?? 0;
                peak = Math.Max(peak, run);
                maxDd = Math.Max(maxDd, peak - run);
            }

            var tickets = mine
                .OrderByDescending(r => r.PlacedAt).ThenByDescending(r => r.Id)
                .Select(r => new
                {
                    id = r.Id,
                    placedAt = r.PlacedAt.ToString("o"),
                    gameKey = r.GameKey,
                    sport = r.Sport,
                    side = r.Side,
                    line = r.Line,
                    odds = r.AmericanOdds,
                    stake = Math.Round(r.StakeDollars, 2),
                    modelProb = r.ModelProb,
                    absEdge = r.AbsEdge,
                    result = r.Result,
                    pnl = r.PnL is { } p ? Math.Round(p, 2) : (double?)null,
                    closeLine = r.CloseLine,
                    finalTotal = r.FinalTotal,
                    clvPts = r.ClvPts is { } c ? Math.Round(c, 2) : (double?)null,
                    group = r.TicketGroup,
                    venue = r.Venue,
                })
                .ToList();

            lanes.Add(new
            {
                code = s.Code,
                name = s.DisplayName,
                rule = s.Rule,
                bank = Math.Round(bank, 2),
                reserved = Math.Round(reserved, 2),
                available = Math.Round(Math.Max(0, bank - reserved), 2),
                pnl = Math.Round(bank - StakeTrial.SeedDollars, 2),
                settled = settled.Count,
                open = openRows.Count,
                wins = settled.Count(r => r.Result == "W"),
                losses = settled.Count(r => r.Result == "L"),
                maxDrawdown = Math.Round(maxDd, 2),
                equity,
                tickets,
            });
        }

        return new { lanes };
    }

    static string DashboardHtml(string embeddedJson) => $$"""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8"/>
<meta name="viewport" content="width=device-width, initial-scale=1"/>
<title>PossiblyMaybe — Stake lanes</title>
<script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/chartjs-adapter-date-fns@3.0.0/dist/chartjs-adapter-date-fns.bundle.min.js"></script>
<style>
  :root { --bg:#0f1419; --panel:#1a222d; --ink:#e7ecf3; --muted:#8b9bb4; --line:#2a3544; --up:#3ecf8e; --down:#f07178; --accent:#6cb6ff; }
  * { box-sizing: border-box; }
  body { margin:0; font-family: "IBM Plex Sans", "Segoe UI", sans-serif; background: radial-gradient(1200px 600px at 10% -10%, #1c2a3a, var(--bg)); color: var(--ink); }
  header { padding: 1.5rem 2rem 0.5rem; }
  header h1 { margin:0; font-size:1.4rem; letter-spacing:0.02em; }
  header p { margin:0.35rem 0 0; color:var(--muted); font-size:0.95rem; }
  .tabs { display:flex; gap:0.4rem; padding: 0.75rem 2rem 0; flex-wrap:wrap; }
  .tab { background:transparent; border:1px solid var(--line); color:var(--muted); border-radius:8px 8px 0 0; padding:0.45rem 1rem; cursor:pointer; font:inherit; }
  .tab.active { color:var(--ink); border-color:var(--accent); background:var(--panel); }
  .filters { display:flex; flex-wrap:wrap; gap:0.45rem; align-items:center; padding: 0.65rem 2rem 0; }
  .filters .label { color:var(--muted); font-size:0.8rem; text-transform:uppercase; letter-spacing:0.06em; margin-right:0.25rem; }
  .chip { background:transparent; border:1px solid var(--line); color:var(--muted); border-radius:999px; padding:0.35rem 0.75rem; cursor:pointer; font:inherit; font-size:0.85rem; }
  .chip:hover { border-color: var(--accent); color:var(--ink); }
  .chip.active { color:var(--ink); border-color:var(--accent); background:rgba(108,182,255,0.12); }
  .chip .pnl { font-variant-numeric: tabular-nums; margin-left:0.35rem; font-size:0.8rem; }
  main { padding: 1rem 2rem 3rem; display:grid; gap:1.25rem; }
  .board { display:grid; grid-template-columns: repeat(auto-fit, minmax(180px,1fr)); gap:0.75rem; }
  .card { background:var(--panel); border:1px solid var(--line); border-radius:10px; padding:0.9rem 1rem; cursor:pointer; }
  .card:hover { border-color: var(--accent); }
  .card.active { border-color: var(--accent); box-shadow: inset 0 0 0 1px var(--accent); }
  .card .code { font-size:0.75rem; color:var(--accent); text-transform:uppercase; letter-spacing:0.08em; }
  .card .name { font-weight:600; margin-top:0.15rem; }
  .card .bank { font-size:1.35rem; margin-top:0.4rem; font-variant-numeric: tabular-nums; }
  .card .meta { color:var(--muted); font-size:0.8rem; margin-top:0.25rem; }
  .pos { color:var(--up); } .neg { color:var(--down); }
  .chart-wrap { background:var(--panel); border:1px solid var(--line); border-radius:10px; padding:1rem; }
  .charts { display:grid; grid-template-columns: 1fr; gap:1.25rem; }
  @media (min-width: 1100px) { .charts { grid-template-columns: 1fr 1fr; } }
  table { width:100%; border-collapse:collapse; font-size:0.9rem; }
  th, td { text-align:left; padding:0.55rem 0.6rem; border-bottom:1px solid var(--line); font-variant-numeric: tabular-nums; }
  th { color:var(--muted); font-weight:500; font-size:0.75rem; text-transform:uppercase; letter-spacing:0.06em; }
  .rule { color:var(--muted); font-size:0.8rem; }
  #board-table tbody tr { cursor:pointer; }
  #board-table tbody tr:hover td { background: rgba(108,182,255,0.06); }
  #board-table tbody tr.active td { background: rgba(108,182,255,0.12); }
  #detail { display:block; }
  .detail-head { display:flex; align-items:baseline; justify-content:space-between; gap:1rem; margin-bottom:0.75rem; }
  .detail-head h2 { margin:0; font-size:1.05rem; font-weight:600; }
  .detail-clear { background:transparent; border:1px solid var(--line); color:var(--muted); border-radius:6px; padding:0.3rem 0.7rem; cursor:pointer; font:inherit; }
  .detail-clear[hidden] { display:none; }
  .ticket-scroll { max-height: 22rem; overflow:auto; }
  .badge { display:inline-block; min-width:1.6rem; text-align:center; padding:0.1rem 0.35rem; border-radius:4px; font-size:0.75rem; font-weight:600; }
  .badge.open { background:#2a3544; color:var(--muted); }
  .badge.W { background:rgba(62,207,142,0.2); color:var(--up); }
  .badge.L { background:rgba(240,113,120,0.2); color:var(--down); }
  .badge.P { background:rgba(230,180,80,0.2); color:#e6b450; }
  .cal-head { display:flex; align-items:center; justify-content:space-between; gap:1rem; margin-bottom:0.85rem; flex-wrap:wrap; }
  .cal-head h2 { margin:0; font-size:1.05rem; font-weight:600; }
  .cal-nav { display:flex; gap:0.4rem; align-items:center; }
  .cal-nav button { background:transparent; border:1px solid var(--line); color:var(--muted); border-radius:6px; padding:0.3rem 0.65rem; cursor:pointer; font:inherit; }
  .cal-grid { display:grid; grid-template-columns: repeat(7, minmax(0, 1fr)); gap:0.5rem; }
  @media (max-width: 900px) { .cal-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
  .cal-day { background:rgba(0,0,0,0.18); border:1px solid var(--line); border-radius:8px; padding:0.55rem 0.6rem; min-height:7.5rem; }
  .cal-day.today { border-color: var(--accent); }
  .cal-day.empty { opacity:0.55; }
  .cal-day .dow { font-size:0.7rem; color:var(--muted); text-transform:uppercase; letter-spacing:0.06em; }
  .cal-day .dom { font-size:1.05rem; font-weight:600; margin:0.1rem 0 0.45rem; }
  .cal-bet { font-size:0.78rem; line-height:1.25; padding:0.35rem 0; border-top:1px solid var(--line); }
  .cal-bet .pick { font-weight:600; }
  .cal-bet .stake { color:var(--muted); }
  .cal-none { color:var(--muted); font-size:0.78rem; }
</style>
</head>
<body>
<header>
  <h1>Stake lane leaderboard</h1>
  <p id="sub">Loading…</p>
</header>
<div class="tabs" id="tabs">
  <button type="button" class="tab active" data-venue="All">All</button>
  <button type="button" class="tab" data-venue="DK">DraftKings</button>
  <button type="button" class="tab" data-venue="Kalshi">Kalshi</button>
  <button type="button" class="tab" data-venue="Robinhood">Robinhood</button>
  <button type="button" class="tab" data-venue="CryptoCom">Crypto.com</button>
</div>
<div class="filters" id="league-filters" aria-label="League filter"></div>
<main>
  <section class="board" id="cards"></section>
  <section class="chart-wrap" id="calendar">
    <div class="cal-head">
      <div>
        <h2>Bet calendar</h2>
        <div class="hint rule" id="cal-range">Next 7 days</div>
      </div>
      <div class="cal-nav">
        <button type="button" id="cal-prev">Prev</button>
        <button type="button" id="cal-today">This week</button>
        <button type="button" id="cal-next">Next</button>
      </div>
    </div>
    <div class="cal-grid" id="cal-grid"></div>
  </section>
  <section class="charts">
    <div class="chart-wrap"><canvas id="equity" height="140"></canvas></div>
    <div class="chart-wrap"><canvas id="league-success" height="140"></canvas></div>
  </section>
  <section class="chart-wrap">
    <table id="board-table">
      <thead>
        <tr><th>Lane</th><th>Rule</th><th>Equity</th><th>At risk</th><th>Available</th><th>PnL</th><th>W-L</th><th>Open</th><th>Max DD</th></tr>
      </thead>
      <tbody id="table"></tbody>
    </table>
  </section>
  <section class="chart-wrap" id="detail">
    <div class="detail-head">
      <div>
        <h2 id="detail-title">Tickets</h2>
        <div class="hint rule" id="detail-hint"></div>
      </div>
      <button type="button" class="detail-clear" id="detail-clear" hidden>All lanes</button>
    </div>
    <div class="ticket-scroll">
      <table>
        <thead>
          <tr>
            <th>Venue</th><th>Lane</th><th>#</th><th>When</th><th>Game</th><th>Side</th><th>Line</th><th>Odds</th>
            <th>Stake</th><th>p</th><th>Edge</th><th>Result</th><th>PnL</th><th>CLV</th>
          </tr>
        </thead>
        <tbody id="tickets"></tbody>
      </table>
    </div>
  </section>
</main>
<script>
const DATA = {{embeddedJson}};
const colors = ['#6cb6ff','#3ecf8e','#e6b450','#d2a8ff','#f07178','#89ddff'];
const sportColors = {
  NBA:'#6cb6ff', MLS:'#3ecf8e', LIGUE1:'#e6b450', LIGAMX:'#d2a8ff', SERIEA:'#f07178',
  EFL:'#89ddff', LALIGA:'#ff9f7a', NFL:'#9cdcfe', MLB:'#c5e478'
};
let venue = 'All';
let selected = null;
let sportFilter = 'ALL';
let calOffset = 0;
let equityChart;
let leagueChart;

const VENUE_ORDER = ['DK', 'Kalshi', 'Robinhood', 'CryptoCom'];
const VENUE_LABEL = { DK: 'DraftKings', Kalshi: 'Kalshi', Robinhood: 'Robinhood', CryptoCom: 'Crypto.com', All: 'All venues' };

/** Merge every venue's tickets per scheme — "soft spots across all markets at once". */
function allVenuesBlock() {
  const byCode = new Map();
  for (const v of VENUE_ORDER) {
    const block = (DATA.venues && DATA.venues[v]) || { lanes: [] };
    for (const lane of (block.lanes || [])) {
      if (!byCode.has(lane.code)) {
        byCode.set(lane.code, {
          code: lane.code,
          name: lane.name,
          rule: lane.rule,
          tickets: [],
        });
      }
      for (const t of (lane.tickets || [])) {
        byCode.get(lane.code).tickets.push({ ...t, venue: t.venue || v });
      }
    }
  }
  return { lanes: [...byCode.values()] };
}

function venueBlock() {
  if (venue === 'All') return allVenuesBlock();
  return (DATA.venues && DATA.venues[venue]) || { lanes: [] };
}
function rawLanes() { return venueBlock().lanes || []; }
function seed() {
  const one = Number(DATA.seed) || 1000;
  // All = sum of independent $1000 books (one seed per venue).
  return venue === 'All' ? one * VENUE_ORDER.length : one;
}
function venueTitle() { return VENUE_LABEL[venue] || venue; }

function sportKey(t) { return (t && t.sport) ? String(t.sport).toUpperCase() : 'UNKNOWN'; }
function matchSport(t) { return sportFilter === 'ALL' || sportKey(t) === sportFilter; }

function allSports() {
  const set = new Set();
  for (const l of rawLanes()) for (const t of (l.tickets || [])) if (t.sport) set.add(sportKey(t));
  return [...set].sort();
}

function ticketDay(t) {
  const m = String(t.gameKey || '').match(/^(\d{8})\b/);
  if (m) return m[1];
  const d = new Date(t.placedAt);
  return isNaN(d) ? ymdLocal(new Date()) : ymdLocal(d);
}
function dayLabel(ymd) {
  const y = +ymd.slice(0, 4), mo = +ymd.slice(4, 6) - 1, da = +ymd.slice(6, 8);
  return new Date(y, mo, da).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}

/** Rebuild lane stats from tickets (respects league filter). Equity = one point per game-day. */
function computeLane(raw) {
  const tickets = (raw.tickets || []).filter(matchSport)
    .slice().sort((a,b) => String(a.placedAt).localeCompare(String(b.placedAt)) || (a.id - b.id));
  const settled = tickets.filter(t => t.result != null)
    .sort((a,b) => ticketDay(a).localeCompare(ticketDay(b)) || String(a.placedAt).localeCompare(String(b.placedAt)) || (a.id - b.id));
  const openRows = tickets.filter(t => t.result == null);

  const byDay = new Map();
  for (const r of settled) {
    const d = ticketDay(r);
    if (!byDay.has(d)) byDay.set(d, []);
    byDay.get(d).push(r);
  }
  const days = [...byDay.keys()].sort();
  let bank = seed();
  let peak = seed(), maxDd = 0, run = seed();
  const equity = []; // { day, label, bank } — one per day with settles
  for (const d of days) {
    for (const r of byDay.get(d)) {
      const p = Number(r.pnl) || 0;
      bank += p;
      run += p;
      peak = Math.max(peak, run);
      maxDd = Math.max(maxDd, peak - run);
    }
    equity.push({ day: d, label: dayLabel(d), bank: Math.round(bank * 100) / 100 });
  }
  if (equity.length === 0) {
    const d = ymdLocal(new Date());
    equity.push({ day: d, label: dayLabel(d), bank: seed() });
  }

  const reserved = openRows.reduce((a,t) => a + Number(t.stake || 0), 0);
  return {
    ...raw,
    tickets,
    bank: Math.round(bank * 100) / 100,
    reserved: Math.round(reserved * 100) / 100,
    available: Math.round(Math.max(0, bank - reserved) * 100) / 100,
    pnl: Math.round((bank - seed()) * 100) / 100,
    settled: settled.length,
    open: openRows.length,
    wins: settled.filter(t => t.result === 'W').length,
    losses: settled.filter(t => t.result === 'L').length,
    maxDrawdown: Math.round(maxDd * 100) / 100,
    equity,
  };
}

function lanes() { return rawLanes().map(computeLane); }
function byCode() { return Object.fromEntries(lanes().map(l => [l.code, l])); }

/** Tickets for current venue, respecting lane + league filters. */
function visibleTickets() {
  const rows = [];
  for (const l of rawLanes()) {
    if (selected && l.code !== selected) continue;
    for (const t of (l.tickets || [])) {
      if (!matchSport(t)) continue;
      rows.push({ ...t, scheme: l.code });
    }
  }
  return rows.sort((a,b) => String(a.placedAt).localeCompare(String(b.placedAt)) || (a.id - b.id));
}

/** Per-league PnL for chips: all lanes unless a lane card is selected. */
function leagueTotals() {
  const map = {};
  for (const l of rawLanes()) {
    if (selected && l.code !== selected) continue;
    for (const t of (l.tickets || [])) {
      const s = sportKey(t);
      if (!map[s]) map[s] = { sport: s, pnl: 0, wins: 0, losses: 0, open: 0, n: 0 };
      map[s].n++;
      if (t.result == null) map[s].open++;
      else {
        map[s].pnl += Number(t.pnl) || 0;
        if (t.result === 'W') map[s].wins++;
        if (t.result === 'L') map[s].losses++;
      }
    }
  }
  return Object.values(map).sort((a,b) => a.sport.localeCompare(b.sport));
}

function filterNote() {
  const league = sportFilter === 'ALL' ? 'all leagues' : sportFilter;
  const lane = selected || 'all lanes';
  return `${lane} · ${league}`;
}

function refreshSub() {
  const seedNote = venue === 'All'
    ? `Seed $${seed()} / lane (= $${DATA.seed} × ${VENUE_ORDER.length} venues)`
    : `Seed $${DATA.seed} / lane / venue`;
  document.getElementById('sub').textContent =
    `${seedNote} · through ${DATA.horizonEnd} · updated ${new Date(DATA.generatedAt).toLocaleString()} · All = every soft spot taken across DK+Kalshi+RH+Crypto`;
}
refreshSub();

document.getElementById('tabs').addEventListener('click', e => {
  const btn = e.target.closest('[data-venue]');
  if (!btn) return;
  venue = btn.dataset.venue;
  document.querySelectorAll('.tab').forEach(t => t.classList.toggle('active', t.dataset.venue === venue));
  selected = null;
  sportFilter = 'ALL';
  refreshSub();
  renderAll();
});

document.getElementById('league-filters').addEventListener('click', e => {
  const btn = e.target.closest('[data-sport]');
  if (!btn) return;
  const next = btn.dataset.sport;
  sportFilter = (next !== 'ALL' && sportFilter === next) ? 'ALL' : next;
  renderAll();
});

function ymdLocal(d) {
  return `${d.getFullYear()}${String(d.getMonth()+1).padStart(2,'0')}${String(d.getDate()).padStart(2,'0')}`;
}
function parseGameDay(gameKey) {
  const m = String(gameKey||'').match(/^(\d{8})\b/);
  return m ? m[1] : null;
}
function startOfLocalDay(d) { return new Date(d.getFullYear(), d.getMonth(), d.getDate()); }
function allTickets() {
  return lanes().flatMap(l => (l.tickets||[]).map(t => ({ ...t, scheme: l.code })));
}
function betsForDay(ymd) {
  const tickets = allTickets().filter(t => parseGameDay(t.gameKey) === ymd);
  const filtered = selected ? tickets.filter(t => t.scheme === selected) : tickets;
  const byGame = new Map();
  for (const t of filtered) {
    // All tab: same game on two venues = two soft spots — keep venue in the key.
    const mapKey = venue === 'All' ? `${t.gameKey}||${t.venue || ''}` : t.gameKey;
    if (!byGame.has(mapKey)) {
      byGame.set(mapKey, {
        gameKey: t.gameKey, sport: t.sport, side: t.side, line: t.line,
        result: t.result, venue: t.venue, stakes: [],
      });
    }
    byGame.get(mapKey).stakes.push({ scheme: t.scheme, stake: t.stake, result: t.result, venue: t.venue });
  }
  return [...byGame.values()];
}
function renderCalendar() {
  const today = startOfLocalDay(new Date());
  const start = new Date(today); start.setDate(start.getDate() + calOffset * 7);
  const days = [];
  for (let i = 0; i < 7; i++) { const d = new Date(start); d.setDate(start.getDate() + i); days.push(d); }
  const fmt = d => d.toLocaleDateString(undefined, { month:'short', day:'numeric' });
  const laneNote = selected ? ` · ${selected}` : ` · ${venueTitle()} all lanes`;
  document.getElementById('cal-range').textContent = `${fmt(days[0])} – ${fmt(days[6])}${laneNote} · ${filterNote()}`;
  const todayYmd = ymdLocal(today);
  document.getElementById('cal-grid').innerHTML = days.map(d => {
    const ymd = ymdLocal(d);
    const bets = betsForDay(ymd);
    const body = bets.length === 0 ? `<div class="cal-none">No bets</div>` : bets.map(b => {
      const match = b.gameKey.replace(/^\d{8}\s*/, '');
      const res = b.result || 'open';
      const stakeTxt = selected
        ? `$${Number(b.stakes[0].stake).toFixed(2)}`
        : `${b.stakes.length} lanes · $${b.stakes.reduce((a,s)=>a+Number(s.stake),0).toFixed(0)} total`;
      const vLab = venue === 'All' && b.venue ? ` · ${VENUE_LABEL[b.venue] || b.venue}` : '';
      return `<div class="cal-bet"><div class="pick">${b.side} ${Number(b.line).toFixed(1)} <span class="badge ${res}">${res}</span></div>
        <div>${match}${b.sport?` · ${b.sport}`:''}${vLab}</div><div class="stake">${stakeTxt}</div></div>`;
    }).join('');
    return `<div class="cal-day${ymd===todayYmd?' today':''}${bets.length===0?' empty':''}">
      <div class="dow">${d.toLocaleDateString(undefined,{weekday:'short'})}</div>
      <div class="dom">${d.getDate()}</div>${body}</div>`;
  }).join('');
}
document.getElementById('cal-prev').onclick = () => { calOffset--; renderCalendar(); };
document.getElementById('cal-next').onclick = () => { calOffset++; renderCalendar(); };
document.getElementById('cal-today').onclick = () => { calOffset = 0; renderCalendar(); };

function fmtWhen(iso) {
  const d = new Date(iso);
  return isNaN(d) ? iso : d.toLocaleString(undefined, { month:'short', day:'numeric', hour:'2-digit', minute:'2-digit' });
}
function fmtNum(v, digits=2) { return v == null || v === '' ? '—' : Number(v).toFixed(digits); }
function fmtMoney(v) {
  const n = Number(v) || 0;
  return (n >= 0 ? '+' : '') + n.toFixed(2);
}

function renderFilters() {
  const totals = leagueTotals();
  const allPnl = totals.reduce((a,x) => a + x.pnl, 0);
  const sports = allSports();
  const chips = [
    { sport: 'ALL', label: 'All', pnl: allPnl },
    ...sports.map(s => {
      const row = totals.find(t => t.sport === s) || { pnl: 0 };
      return { sport: s, label: s, pnl: row.pnl };
    })
  ];
  const laneHint = selected || 'all lanes';
  document.getElementById('league-filters').innerHTML =
    `<span class="label">League</span>` +
    chips.map(c => {
      const cls = c.pnl >= 0 ? 'pos' : 'neg';
      return `<button type="button" class="chip${sportFilter===c.sport?' active':''}" data-sport="${c.sport}">${c.label}<span class="pnl ${cls}">${fmtMoney(c.pnl)}</span></button>`;
    }).join('') +
    `<span class="rule">PnL chips = ${laneHint}</span>`;
}

function ticketRowHtml(t) {
  const res = t.result || 'open';
  const pnlCls = t.pnl == null ? '' : (t.pnl >= 0 ? 'pos' : 'neg');
  const pnlTxt = t.pnl == null ? '—' : ((t.pnl>=0?'+':'') + t.pnl.toFixed(2));
  const clv = t.clvPts == null ? '—' : ((t.clvPts>=0?'+':'') + t.clvPts.toFixed(1));
  const v = VENUE_LABEL[t.venue] || t.venue || venueTitle();
  return `<tr>
    <td>${v}</td><td>${t.scheme || '—'}</td><td>${t.id}</td><td>${fmtWhen(t.placedAt)}</td>
    <td>${t.gameKey}${t.sport?` <span class="rule">${t.sport}</span>`:''}</td>
    <td>${t.side}</td><td>${Number(t.line).toFixed(1)}</td>
    <td>${t.odds > 0 ? '+'+t.odds : t.odds}</td>
    <td>$${Number(t.stake).toFixed(2)}</td>
    <td>${fmtNum(t.modelProb, 3)}</td><td>${fmtNum(t.absEdge, 2)}</td>
    <td><span class="badge ${res}">${res}</span></td>
    <td class="${pnlCls}">${pnlTxt}</td><td>${clv}</td></tr>`;
}

function renderTickets() {
  const rows = visibleTickets();
  const clearBtn = document.getElementById('detail-clear');
  if (selected) {
    const l = byCode()[selected];
    document.getElementById('detail-title').textContent = `${venueTitle()} · ${selected}${l ? ` — ${l.name}` : ''}`;
    clearBtn.hidden = false;
  } else {
    document.getElementById('detail-title').textContent = `${venueTitle()} · all lanes`;
    clearBtn.hidden = true;
  }
  document.getElementById('detail-hint').textContent =
    `${rows.length} ticket(s) · ${filterNote()}${selected && byCode()[selected] ? ` · ${byCode()[selected].rule}` : ''}`;
  document.getElementById('tickets').innerHTML = rows.length === 0
    ? `<tr><td colspan="14" class="rule">No tickets for this filter on ${venueTitle()}.</td></tr>`
    : rows.map(ticketRowHtml).join('');
}

function showLane(code) {
  if (selected === code) { clearLane(); return; }
  if (!byCode()[code]) return;
  selected = code;
  renderAll();
}
function clearLane() {
  selected = null;
  renderAll();
}

function renderBoard() {
  const ranked = [...lanes()].sort((a,b) => b.bank - a.bank);
  document.getElementById('cards').innerHTML = ranked.map((l,i) => `
    <div class="card" data-code="${l.code}" role="button" tabindex="0">
      <div class="code">#${i+1} ${l.code}</div>
      <div class="name">${l.name}</div>
      <div class="bank ${l.pnl>=0?'pos':'neg'}">$${l.bank.toFixed(2)}</div>
      <div class="meta">avail $${(l.available??l.bank).toFixed(2)}${l.reserved>0?` · at risk $${l.reserved.toFixed(2)}`:''}</div>
      <div class="meta">${l.pnl>=0?'+':''}${l.pnl.toFixed(2)} · DD $${l.maxDrawdown.toFixed(2)}${l.open?` · ${l.open} open`:''}</div>
    </div>`).join('');
  document.getElementById('table').innerHTML = ranked.map(l => `
    <tr data-code="${l.code}">
      <td><strong>${l.code}</strong> ${l.name}</td>
      <td class="rule">${l.rule}</td>
      <td>$${l.bank.toFixed(2)}</td>
      <td>$${(l.reserved||0).toFixed(2)}</td>
      <td>$${(l.available??l.bank).toFixed(2)}</td>
      <td class="${l.pnl>=0?'pos':'neg'}">${l.pnl>=0?'+':''}${l.pnl.toFixed(2)}</td>
      <td>${l.wins}-${l.losses}</td>
      <td>${l.open}</td>
      <td>$${l.maxDrawdown.toFixed(2)}</td>
    </tr>`).join('');
  if (selected) {
    document.querySelectorAll('.card').forEach(el => el.classList.toggle('active', el.dataset.code === selected));
    document.querySelectorAll('#board-table tbody tr').forEach(el => el.classList.toggle('active', el.dataset.code === selected));
  }
}

function renderCharts() {
  const ls = selected ? lanes().filter(l => l.code === selected) : lanes();
  const daySet = new Set();
  for (const l of ls) for (const p of l.equity) daySet.add(p.day);
  let days = [...daySet].sort();
  if (days.length === 0) days = [ymdLocal(new Date())];
  const labels = days.map(dayLabel);

  const eqDatasets = ls.map((l, i) => {
    const byDay = Object.fromEntries(l.equity.map(p => [p.day, p.bank]));
    let last = seed();
    const data = days.map(d => {
      if (byDay[d] != null) last = byDay[d];
      return last;
    });
    const color = colors[rawLanes().findIndex(r => r.code === l.code) % colors.length] || colors[i % colors.length];
    return {
      label: l.code,
      data,
      borderColor: color,
      backgroundColor: color,
      tension: 0,
      pointRadius: 5,
      pointHoverRadius: 7,
      showLine: data.length > 1,
    };
  });
  if (equityChart) equityChart.destroy();
  equityChart = new Chart(document.getElementById('equity'), {
    type: 'line',
    data: { labels, datasets: eqDatasets },
    options: {
      interaction: { mode: 'index', intersect: false },
      scales: {
        x: { grid: { color: '#2a3544' }, ticks: { color: '#8b9bb4' }, title: { display: true, text: 'Game day', color: '#8b9bb4' } },
        y: { grid: { color: '#2a3544' }, ticks: { color: '#8b9bb4', callback: v => '$'+v }, title: { display: true, text: venue + ' bankroll (EOD)', color: '#8b9bb4' } }
      },
      plugins: {
        legend: { labels: { color: '#e7ecf3' } },
        title: { display: true, text: `${venue} equity by day · ${filterNote()}`, color: '#e7ecf3' }
      }
    }
  });

  // League PnL: all lanes (or selected lane) — one cumulative point per game-day per sport.
  const tickets = visibleTickets().filter(t => t.result != null);
  const sports = sportFilter === 'ALL'
    ? [...new Set(tickets.map(sportKey))].sort()
    : [sportFilter];
  const dayPnl = {};
  for (const t of tickets) {
    const s = sportKey(t);
    const d = ticketDay(t);
    if (!dayPnl[s]) dayPnl[s] = {};
    dayPnl[s][d] = (dayPnl[s][d] || 0) + (Number(t.pnl) || 0);
  }
  let leagueDays = [...new Set(tickets.map(ticketDay))].sort();
  if (leagueDays.length === 0) leagueDays = [ymdLocal(new Date())];
  const leagueLabels = leagueDays.map(dayLabel);
  const leagueDatasets = (sports.length ? sports : ['—']).map((s, i) => {
    let cum = 0;
    const data = leagueDays.map(d => {
      if (dayPnl[s] && dayPnl[s][d] != null) cum += dayPnl[s][d];
      return Math.round(cum * 100) / 100;
    });
    return {
      label: s,
      data,
      borderColor: sportColors[s] || colors[i % colors.length],
      backgroundColor: sportColors[s] || colors[i % colors.length],
      tension: 0,
      pointRadius: 5,
      pointHoverRadius: 7,
      showLine: data.length > 1,
    };
  });
  if (leagueChart) leagueChart.destroy();
  leagueChart = new Chart(document.getElementById('league-success'), {
    type: 'line',
    data: { labels: leagueLabels, datasets: leagueDatasets },
    options: {
      interaction: { mode: 'index', intersect: false },
      scales: {
        x: { grid: { color: '#2a3544' }, ticks: { color: '#8b9bb4' }, title: { display: true, text: 'Game day', color: '#8b9bb4' } },
        y: { grid: { color: '#2a3544' }, ticks: { color: '#8b9bb4', callback: v => '$'+v }, title: { display: true, text: 'Cumulative PnL', color: '#8b9bb4' } }
      },
      plugins: {
        legend: { labels: { color: '#e7ecf3' } },
        title: { display: true, text: `League PnL by day · ${filterNote()}`, color: '#e7ecf3' }
      }
    }
  });
}

function renderAll() {
  renderFilters();
  renderBoard();
  renderCharts();
  renderCalendar();
  renderTickets();
}

document.getElementById('cards').addEventListener('click', e => {
  const card = e.target.closest('[data-code]');
  if (card) showLane(card.dataset.code);
});
document.getElementById('table').addEventListener('click', e => {
  const row = e.target.closest('tr[data-code]');
  if (row) showLane(row.dataset.code);
});
document.getElementById('detail-clear').addEventListener('click', clearLane);

renderAll();
</script>
</body>
</html>
""";
}
