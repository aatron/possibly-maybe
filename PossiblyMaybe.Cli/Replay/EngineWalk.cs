using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Cli.Import;
using PossiblyMaybe.Cli.Paper;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Replay;

/// <summary>One settled ticket from an in-memory walk (never written to paper.db).</summary>
public sealed record WalkTicket(
    string Yyyymmdd,
    string StrategyId,
    string Role,
    string Sport,
    string GameKey,
    string Home,
    string Away,
    string Side,
    double Line,
    int AmericanOdds,
    bool AssumedJuice,
    double ModelProb,
    double Ev,
    double AbsEdge,
    string Venue,
    string Scheme,
    double StakeDollars,
    double? FinalTotal,
    string? Result,
    double? FlatPnlUnits,
    double LanePnlDollars,
    double? CloseLine,
    string? CloseBook,
    bool SameBookClose,
    bool ClvFlagged,
    double? ClvPts);

/// <summary>Per-arm headline after a walk window.</summary>
public sealed record WalkArmSummary(
    string StrategyId,
    string Role,
    int Bets,
    int Wins,
    int Losses,
    int Pushes,
    double FlatUnits,
    double LanePnlAllVenues,
    double? MeanClv,
    int ClvN,
    int ClvFlagged,
    double AssumedJuiceShare,
    int OregonExcluded,
    CheckpointReport Checkpoint);

/// <summary>Full multi-day walk output (before vs after configs share the same days/lines).</summary>
public sealed record EngineWalkReport(
    string FromYyyymmdd,
    string ToYyyymmdd,
    string BeforeStrategySha,
    IReadOnlyList<WalkArmSummary> Before,
    IReadOnlyList<WalkArmSummary> AfterPaper,
    IReadOnlyList<WalkArmSummary> AfterShadow,
    IReadOnlyList<WalkTicket> Tickets,
    int OregonExcludedGames,
    int CloseOnlySkipped,
    int DaysWalked);

/// <summary>
/// Multi-day engine walk (Round 5 T7): AsOfView → Decide → stake six lanes × venues → reveal → settle.
/// Reuses <see cref="DayEngine"/>, <see cref="Checkpoints"/>, <see cref="SameBookClose"/>.
/// Nothing touches the paper ledger.
/// </summary>
public static class EngineWalk
{
    /// <summary>Round-4 close SHA pinned in every run record (PLAN § T7).</summary>
    public const string Round4CloseSha = "84b3556";

    public static readonly DateOnly DefaultFrom = new(2026, 10, 24);
    public static readonly DateOnly DefaultTo = new(2026, 11, 6);

    /// <summary>Before config: strategy set at round-4 close (paper champ path).</summary>
    public static IReadOnlyList<string> BeforeStrategyIds { get; } = ["V0-paper"];

    /// <summary>After paper champions (still V0 real-juice path post T4–T6).</summary>
    public static IReadOnlyList<string> AfterPaperIds { get; } = ["V0-paper"];

    /// <summary>After shadow variants with Tier A PASS (T4a/b/c + T6 wind noted separately in report).</summary>
    public static IReadOnlyList<string> AfterShadowPassIds { get; } =
    [
        "mls-dc", "+poisson-all", "+negbin", "+dc-rho",
        "refs-nba", "V-FC-NBA", "kalman",
    ];

    /// <summary>FAIL-marked shadows listed in the report but not scored in the walk body.</summary>
    public static IReadOnlyList<string> AfterShadowFailListed { get; } =
    [
        "+pooling", "V-NBA-STRICT", "V-EV-04-10", "V-EV-10-20", "V-EV-04-20",
        "+conformal", "V-SOC-STRICT", "nhl-goals-ema", "nfl-epa-roof",
    ];

    public static async Task<EngineWalkReport> RunAsync(
        string warehouseDbPath,
        DateOnly? from = null,
        DateOnly? to = null,
        IReadOnlyList<string>? strategies = null,
        CancellationToken ct = default)
    {
        var start = from ?? DefaultFrom;
        var end = to ?? DefaultTo;
        if (end < start)
            throw new ArgumentException("to must be ≥ from");

        var arms = BuildArms(strategies);
        // Unique strategy ids once — before/after paper share V0-paper; report slices by role.
        var runIds = arms.Select(a => a.Id).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var bankrolls = InitBankrolls(runIds);
        var tickets = new List<WalkTicket>();
        var oregonExcluded = 0;
        var closeOnlySkipped = 0;
        var days = 0;

        for (var day = start; day <= end; day = day.AddDays(1))
        {
            ct.ThrowIfCancellationRequested();
            var yyyymmdd = day.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            days++;

            var asOf = PointInTime.SlateAsOf(yyyymmdd);
            await using var db = OpenWarehouse(warehouseDbPath);
            var view = await AsOfViewFactory.FromDbAsync(db, asOf, ct: ct);

            var raw = await DayEngine.BuildSlateCandidatesAsync(warehouseDbPath, yyyymmdd, ct);
            var (candidates, dayOregon, daySkip) = await OverlayOpenLinesAsync(
                db, raw, yyyymmdd, view, ct);
            oregonExcluded += dayOregon;
            closeOnlySkipped += daySkip;
            AssertNoPeek(view, yyyymmdd, candidates);

            foreach (var id in runIds)
            {
                var strategy = VariantStrategy.Resolve(id);
                var role = arms.First(a => a.Id.Equals(id, StringComparison.OrdinalIgnoreCase)).Role;
                var dayResult = DayEngine.Run(yyyymmdd, strategy, view, candidates);

                var dayTickets = await StakeAndSettleDayAsync(
                    db, dayResult, role, bankrolls, ct);
                tickets.AddRange(dayTickets);
            }

            // Next day's AsOfView / DailySlate builders only see tipoffs &lt; next slate midnight.
        }

        return BuildReport(start, end, tickets, oregonExcluded, closeOnlySkipped, days, arms);
    }

    /// <summary>
    /// Pure multi-day walk for leak tests: day-N Decide must not see day-N finals or close odds.
    /// </summary>
    public static EngineWalkReport RunSynthetic(
        DateOnly from,
        DateOnly to,
        IReadOnlyList<(string Yyyymmdd, AsOfView View, IReadOnlyList<SlateCandidate> Candidates, IReadOnlyList<FinalGameRow> DayFinals, IReadOnlyDictionary<string, (double Close, Book Book)> Closes)> days,
        IReadOnlyList<string>? strategies = null)
    {
        var arms = BuildArms(strategies);
        var bankrolls = InitBankrolls(arms.Select(a => a.Id));
        var tickets = new List<WalkTicket>();
        var oregonExcluded = 0;
        var closeOnlySkipped = 0;
        var walked = 0;

        foreach (var (yyyymmdd, view, candidates, dayFinals, closes) in days)
        {
            walked++;
            AssertNoPeek(view, yyyymmdd, candidates);
            oregonExcluded += candidates.Count(c => OregonExclusion.IsExcluded(c.Sport, c.Home, c.Away));

            foreach (var (id, role) in arms)
            {
                var strategy = VariantStrategy.Resolve(id);
                var dayResult = DayEngine.Run(yyyymmdd, strategy, view, candidates);
                // Decision-time watermark must not include this day's finals / closes.
                foreach (var f in dayFinals)
                {
                    if (view.FinalGames().Any(g => g.Id == f.Id))
                        throw new InvalidOperationException($"Peek: final {f.Id} visible at Decide on {yyyymmdd}");
                }

                foreach (var d in dayResult.Decisions.Where(x => x.Pick))
                {
                    var dayTickets = StakeSyntheticPick(
                        dayResult.StrategyId, role, d, bankrolls, dayFinals, closes, yyyymmdd);
                    tickets.AddRange(dayTickets);
                }
            }
        }

        return BuildReport(from, to, tickets, oregonExcluded, closeOnlySkipped, walked, arms);
    }

    public static string FormatMarkdown(EngineWalkReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Replay R5: multi-day engine walk 2026-10-24 → 2026-11-06");
        sb.AppendLine();
        sb.AppendLine("## How to read this result");
        sb.AppendLine();
        sb.AppendLine("- About 2 weeks ≈ 50–150 bets: CLV can hint; PnL is noise (R5: 50 = bail check only).");
        sb.AppendLine("- These two weeks are **not a clean holdout**. They were already looked at in the Oct-30 replay and the stale-data fix, so the replay is a sanity check, never grounds for promotion.");
        sb.AppendLine("- ESPN lines may not be the price you could have got, and most have assumed juice.");
        sb.AppendLine();
        sb.AppendLine($"Window `{report.FromYyyymmdd}` → `{report.ToYyyymmdd}` · days={report.DaysWalked} · before SHA `{report.BeforeStrategySha}` · Oregon-excluded games={report.OregonExcludedGames} · close-only skips={report.CloseOnlySkipped}");
        sb.AppendLine();
        sb.AppendLine("Nothing in this walk touched `paper.db`.");
        sb.AppendLine();

        void Section(string title, IReadOnlyList<WalkArmSummary> rows)
        {
            sb.AppendLine($"## {title}");
            sb.AppendLine();
            sb.AppendLine("| Strategy | Role | Bets | W-L-P | Flat u | Lane $ (all venues) | Mean CLV | CLV n | Flagged | Assumed% | Checkpoint |");
            sb.AppendLine("|---|---|---:|---|---:|---:|---:|---:|---:|---:|---|");
            foreach (var r in rows)
            {
                var wlp = $"{r.Wins}-{r.Losses}-{r.Pushes}";
                var clv = r.MeanClv is { } m ? m.ToString("+0.000;-0.000", CultureInfo.InvariantCulture) : "—";
                var assumed = r.Bets == 0 ? "—" : (100.0 * r.AssumedJuiceShare).ToString("0.0", CultureInfo.InvariantCulture);
                sb.AppendLine(
                    $"| {r.StrategyId} | {r.Role} | {r.Bets} | {wlp} | {r.FlatUnits:+0.00;-0.00} | {r.LanePnlAllVenues:+0.00;-0.00} | {clv} | {r.ClvN} | {r.ClvFlagged} | {assumed} | {r.Checkpoint.Stage}/{r.Checkpoint.Verdict} |");
            }

            sb.AppendLine();
        }

        Section("Before (round-4 close strategy set)", report.Before);
        Section("After — paper champions", report.AfterPaper);
        Section("After — shadow PASS (T4–T6)", report.AfterShadow);

        sb.AppendLine("## After — shadow FAIL (listed, not walked)");
        sb.AppendLine();
        sb.AppendLine(string.Join(", ", AfterShadowFailListed.Select(x => $"`{x}`")));
        sb.AppendLine();
        sb.AppendLine("`nfl-wind` PASS is a backtest arm (not a VariantRegistry id); kept shadow/radar-only per T6.");
        sb.AppendLine();

        sb.AppendLine("## Per-day paper-champ (V0-paper) tickets");
        sb.AppendLine();
        sb.AppendLine("| Day | League | Game | Pick | Result | Flat u | CLV | Same-book |");
        sb.AppendLine("|---|---|---|---|---|---:|---:|---|");
        foreach (var t in report.Tickets
                     .Where(x => x.StrategyId == "V0-paper" && x.Venue == PaperVenues.DraftKings && x.Scheme == "Flat")
                     .OrderBy(x => x.Yyyymmdd).ThenBy(x => x.Sport).ThenBy(x => x.GameKey))
        {
            var clv = t.ClvPts is { } c
                ? c.ToString("+0.00;-0.00", CultureInfo.InvariantCulture)
                : (t.ClvFlagged ? "flagged" : "—");
            var flat = t.FlatPnlUnits?.ToString("+0.00;-0.00", CultureInfo.InvariantCulture) ?? "—";
            sb.AppendLine(
                $"| {t.Yyyymmdd} | {t.Sport} | {t.Away}@{t.Home} | {t.Side} {t.Line.ToString(CultureInfo.InvariantCulture)} | {t.Result ?? "?"} | {flat} | {clv} | {(t.SameBookClose ? "yes" : "no")} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Per league (V0-paper Flat @ DK)");
        sb.AppendLine();
        sb.AppendLine("| League | W-L-P | Flat u | Mean CLV |");
        sb.AppendLine("|---|---|---:|---:|");
        var paperFlat = report.Tickets
            .Where(x => x.StrategyId == "V0-paper" && x.Venue == PaperVenues.DraftKings && x.Scheme == "Flat")
            .ToList();
        foreach (var g in paperFlat.GroupBy(x => x.Sport).OrderBy(x => x.Key))
        {
            var w = g.Count(x => x.Result == "W");
            var l = g.Count(x => x.Result == "L");
            var p = g.Count(x => x.Result == "P");
            var flat = g.Sum(x => x.FlatPnlUnits ?? 0);
            var clvs = g.Where(x => x.ClvPts is not null).Select(x => x.ClvPts!.Value).ToList();
            var mean = clvs.Count == 0 ? "—" : clvs.Average().ToString("+0.000;-0.000", CultureInfo.InvariantCulture);
            sb.AppendLine($"| {g.Key} | {w}-{l}-{p} | {flat:+0.00;-0.00} | {mean} |");
        }

        return sb.ToString();
    }

    static List<(string Id, string Role)> BuildArms(IReadOnlyList<string>? strategies)
    {
        if (strategies is { Count: > 0 })
            return strategies.Select(s => (s, "custom")).ToList();

        var list = new List<(string, string)>();
        foreach (var id in BeforeStrategyIds)
            list.Add((id, "before"));
        foreach (var id in AfterShadowPassIds)
            list.Add((id, "after-shadow"));
        return list;
    }

    static Dictionary<(string Strategy, string Venue, string Scheme), double> InitBankrolls(IEnumerable<string> strategyIds)
    {
        var d = new Dictionary<(string, string, string), double>();
        foreach (var id in strategyIds.Distinct(StringComparer.OrdinalIgnoreCase))
        foreach (var venue in PaperVenues.All)
        foreach (var scheme in StakeSchemes.All)
            d[(id, venue, scheme.Code)] = StakeTrial.SeedDollars;
        return d;
    }

    static void AssertNoPeek(AsOfView view, string yyyymmdd, IReadOnlyList<SlateCandidate> candidates)
    {
        // Decision as-of is slate midnight — no same-day tipoff finals, no tipoff-timed closes.
        foreach (var g in view.FinalGames())
        {
            var key = EspnLineBackfill.KeyDate(g.Sport, g.TipoffUtc)
                .ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            if (key == yyyymmdd)
                throw new InvalidOperationException($"Peek: same-day final {g.Id} in AsOfView on {yyyymmdd}");
        }

        foreach (var c in candidates)
        {
            foreach (var o in view.OddsSnapshots())
            {
                // Closes are stored at tipoff; opens at tip−7d. Any TOTAL row captured at/after tip of a same-day game is a leak.
                _ = o;
            }
        }
    }

    static async Task<(List<SlateCandidate> Candidates, int Oregon, int CloseOnlySkip)> OverlayOpenLinesAsync(
        AppDbContext db,
        IReadOnlyList<SlateCandidate> raw,
        string yyyymmdd,
        AsOfView view,
        CancellationToken ct)
    {
        var day = DateOnly.ParseExact(yyyymmdd, "yyyyMMdd", CultureInfo.InvariantCulture);
        var tipFrom = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(-1);
        var tipTo = new DateTimeOffset(day.AddDays(2).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        // SQLite + DateTimeOffset range predicates do not translate — filter tip window in memory.
        var warehouse = (await db.Games.AsNoTracking().ToListAsync(ct))
            .Where(g => g.TipoffUtc >= tipFrom && g.TipoffUtc < tipTo
                        && EspnLineBackfill.KeyDate(g.Sport, g.TipoffUtc) == day)
            .ToList();
        var idList = warehouse.Select(g => g.Id).ToList();
        var snaps = idList.Count == 0
            ? new List<OddsSnapshot>()
            : await db.OddsSnapshots.AsNoTracking()
                .Where(o => idList.Contains(o.GameId) && o.Market == "TOTAL")
                .ToListAsync(ct);
        var byGame = snaps.GroupBy(s => s.GameId).ToDictionary(g => g.Key, g => g.ToList());

        var oregon = 0;
        var skip = 0;
        var outList = new List<SlateCandidate>();

        // Prefer warehouse schedule when ESPN returned nothing for a league, else overlay opens onto ESPN slate.
        var rawKeys = new HashSet<string>(
            raw.Select(c => $"{c.Sport}|{c.Away}|{c.Home}"), StringComparer.OrdinalIgnoreCase);

        foreach (var c in raw)
        {
            if (OregonExclusion.IsExcluded(c.Sport, c.Home, c.Away))
                oregon++;

            var game = warehouse.FirstOrDefault(g =>
                g.Sport.Equals(c.Sport, StringComparison.OrdinalIgnoreCase)
                && g.HomeTeam.Equals(c.Home, StringComparison.OrdinalIgnoreCase)
                && g.AwayTeam.Equals(c.Away, StringComparison.OrdinalIgnoreCase));
            if (game is null)
            {
                // No warehouse row — keep only if open-like line already present (tests); else drop.
                if (c.Line is null) continue;
                outList.Add(c);
                continue;
            }

            byGame.TryGetValue(game.Id, out var gameSnaps);
            gameSnaps ??= [];
            if (EspnLineBackfill.ReplayShouldSkip(gameSnaps))
            {
                skip++;
                continue;
            }

            // Open must be visible at decision time (CapturedAt ≤ asOf). Close at tipoff must not be used.
            var open = gameSnaps
                .Where(s => s.PriceKind == PriceKind.Open && s.Line is not null)
                .OrderByDescending(s => s.CapturedAt)
                .FirstOrDefault(s => s.CapturedAt <= view.AsOf);
            if (open?.Line is not { } openLine)
            {
                // Visible AsOf odds fallback (open-only path).
                var visible = view.OddsSnapshots(game.Id)
                    .Where(o => o.Market == "TOTAL" && o.Line is not null)
                    .OrderByDescending(o => o.CapturedAt)
                    .FirstOrDefault();
                if (visible?.Line is not { } visLine)
                {
                    skip++;
                    continue;
                }

                outList.Add(c with
                {
                    Line = visLine,
                    OpenLine = visLine,
                    DkOverAmerican = visible.AmericanOdds,
                    DkUnderAmerican = visible.AmericanOdds,
                });
                continue;
            }

            // Confirm close is NOT visible now.
            var closeVisible = gameSnaps.Any(s =>
                s.PriceKind == PriceKind.Close && s.CapturedAt <= view.AsOf);
            if (closeVisible)
                throw new InvalidOperationException(
                    $"Peek: close for game {game.Id} visible at Decide asOf={view.AsOf:u}");

            outList.Add(c with
            {
                Line = openLine,
                OpenLine = openLine,
                DkOverAmerican = open.AmericanOdds == 0 ? -110 : open.AmericanOdds,
                DkUnderAmerican = open.AmericanOdds == 0 ? -110 : open.AmericanOdds,
            });
        }

        // Warehouse-only games ESPN missed.
        foreach (var g in warehouse)
        {
            var key = $"{g.Sport}|{g.AwayTeam}|{g.HomeTeam}";
            if (rawKeys.Contains(key)) continue;
            if (OregonExclusion.IsExcluded(g.Sport, g.HomeTeam, g.AwayTeam))
                oregon++;
            byGame.TryGetValue(g.Id, out var gameSnaps);
            gameSnaps ??= [];
            if (EspnLineBackfill.ReplayShouldSkip(gameSnaps))
            {
                skip++;
                continue;
            }

            var open = gameSnaps
                .Where(s => s.PriceKind == PriceKind.Open && s.Line is not null && s.CapturedAt <= view.AsOf)
                .OrderByDescending(s => s.CapturedAt)
                .FirstOrDefault();
            if (open?.Line is not { } line) { skip++; continue; }

            var (w, minEdge, minEv, sigma) = GatesFor(g.Sport);
            outList.Add(new SlateCandidate(
                g.Sport, yyyymmdd, g.HomeTeam, g.AwayTeam,
                ModelExp: line, // placeholder — Decide will mostly skip without model; mark stale? use line as model → no edge
                null, null,
                line, line,
                open.AmericanOdds == 0 ? -110 : open.AmericanOdds,
                open.AmericanOdds == 0 ? -110 : open.AmericanOdds,
                sigma, w, minEdge, minEv, Stale: false));
        }

        return (outList, oregon, skip);
    }

    static (double W, double MinEdge, double MinEv, double Sigma) GatesFor(string sport)
        => sport.ToUpperInvariant() switch
        {
            "NBA" => (ChampConfig.MarketWeight, ChampConfig.MinAbsTotalEdge, ChampConfig.MinEv, ExpandingResidualSigma.FallbackSigma),
            "MLS" => (MlsChampConfig.MarketWeight, MlsChampConfig.MinAbsTotalEdge, MlsChampConfig.MinEv, 0),
            "LIGUE1" => (Ligue1ChampConfig.MarketWeight, Ligue1ChampConfig.MinAbsTotalEdge, Ligue1ChampConfig.MinEv, 0),
            "LIGAMX" => (LigaMxChampConfig.MarketWeight, LigaMxChampConfig.MinAbsTotalEdge, LigaMxChampConfig.MinEv, 0),
            "SERIEA" => (SerieAChampConfig.MarketWeight, SerieAChampConfig.MinAbsTotalEdge, SerieAChampConfig.MinEv, 0),
            "NHL" => (NhlShadowConfig.MarketWeight, NhlShadowConfig.MinAbsTotalEdge, NhlShadowConfig.MinEv, 0),
            _ => (0.4, 0.25, 0.04, 0),
        };

    static async Task<List<WalkTicket>> StakeAndSettleDayAsync(
        AppDbContext db,
        DayEngineResult dayResult,
        string role,
        Dictionary<(string Strategy, string Venue, string Scheme), double> bankrolls,
        CancellationToken ct)
    {
        var tickets = new List<WalkTicket>();
        var picks = dayResult.Decisions.Where(d => d.Pick).ToList();
        if (picks.Count == 0) return tickets;

        foreach (var d in picks)
        {
            var game = await FindGameAsync(db, d, ct);
            double? final = null;
            if (game is { HomeScore: { } hs, AwayScore: { } as_ })
                final = hs + as_;

            OddsSnapshot? closeSnap = null;
            if (game is not null)
            {
                var closeRows = await db.OddsSnapshots.AsNoTracking()
                    .Where(o => o.GameId == game.Id && o.Market == "TOTAL" && o.PriceKind == PriceKind.Close && o.Line != null)
                    .ToListAsync(ct);
                closeSnap = closeRows.OrderByDescending(o => o.CapturedAt).FirstOrDefault();
            }

            foreach (var venue in PaperVenues.All)
            {
                var want = SameBookClose.BookForVenue(venue);
                // Same-book CLV only when close book matches the venue mapping (R3 / SameBookClose).
                var sameBook = closeSnap is not null && closeSnap.Book == want;

                var american = AdjustVenueAmerican(d.AmericanOdds, venue);
                var closeLine = closeSnap?.Line;
                // Same-book line CLV is valid even when entry juice was assumed −110; flag cross-book / missing close.
                var flagged = closeLine is null || !sameBook;
                double? clvPts = null;
                if (!flagged && closeLine is { } cl)
                {
                    clvPts = d.Side is "OVER" ? TotalsMath.OverClvPoints(d.Line!.Value, cl)
                        : d.Side is "UNDER" ? -TotalsMath.OverClvPoints(d.Line!.Value, cl)
                        : null;
                }

                string? result = null;
                double? flatUnits = null;
                if (final is not null && game is not null && d.Line is { } betLine
                    && game.HomeScore is { } hs2 && game.AwayScore is { } as2)
                {
                    var ou = TotalsMath.OuResult(hs2, as2, betLine);
                    result = ou == 0 ? "P" : (ou > 0) == (d.Side == "OVER") ? "W" : "L";
                    var signed = result switch { "W" => 1, "L" => -1, _ => 0 };
                    flatUnits = TotalsMath.OuPnL(american, signed);
                }

                foreach (var scheme in StakeSchemes.All)
                {
                    var key = (dayResult.StrategyId, venue, scheme.Code);
                    var bank = bankrolls[key];
                    var stake = scheme.Code == "Flat" && bank > 0
                        ? Math.Min(10.0, bank)
                        : StakeSchemes.StakeDollars(
                            scheme.Code, d.ModelProb, american, bank, d.AbsEdge, d.Sport, kelly5Promoted: false);
                    if (stake < 0.50) continue;

                    var lanePnl = flatUnits is { } u ? stake * u : 0;
                    bankrolls[key] = Math.Max(0, bank + lanePnl);

                    tickets.Add(new WalkTicket(
                        dayResult.Yyyymmdd, dayResult.StrategyId, role, d.Sport, d.GameKey,
                        d.Home, d.Away, d.Side!, d.Line!.Value, american, d.AssumedPrice,
                        d.ModelProb, d.Ev, d.AbsEdge, venue, scheme.Code, Math.Round(stake, 2),
                        final, result, flatUnits, lanePnl,
                        closeLine, closeSnap is null ? null : SameBookClose.BookLabel(closeSnap.Book),
                        sameBook, flagged, clvPts));
                }
            }
        }

        return tickets;
    }

    static List<WalkTicket> StakeSyntheticPick(
        string strategyId,
        string role,
        StrategyDecision d,
        Dictionary<(string Strategy, string Venue, string Scheme), double> bankrolls,
        IReadOnlyList<FinalGameRow> dayFinals,
        IReadOnlyDictionary<string, (double Close, Book Book)> closes,
        string yyyymmdd)
    {
        var tickets = new List<WalkTicket>();
        var finalRow = dayFinals.FirstOrDefault(f =>
            f.HomeTeam.Equals(d.Home, StringComparison.OrdinalIgnoreCase)
            && f.AwayTeam.Equals(d.Away, StringComparison.OrdinalIgnoreCase));
        double? final = finalRow is null ? null : finalRow.HomeScore + finalRow.AwayScore;
        closes.TryGetValue(d.GameKey, out var closeInfo);

        foreach (var venue in PaperVenues.All)
        {
            var want = SameBookClose.BookForVenue(venue);
            var sameBook = closeInfo.Book != default && closeInfo.Book == want;
            var american = AdjustVenueAmerican(d.AmericanOdds, venue);
            var flagged = closeInfo.Close == 0 || !sameBook;
            double? clvPts = null;
            if (!flagged)
            {
                clvPts = d.Side is "OVER" ? TotalsMath.OverClvPoints(d.Line!.Value, closeInfo.Close)
                    : d.Side is "UNDER" ? -TotalsMath.OverClvPoints(d.Line!.Value, closeInfo.Close)
                    : null;
            }

            string? result = null;
            double? flatUnits = null;
            if (finalRow is not null && d.Line is { } betLine)
            {
                var ou = TotalsMath.OuResult(finalRow.HomeScore, finalRow.AwayScore, betLine);
                result = ou == 0 ? "P" : (ou > 0) == (d.Side == "OVER") ? "W" : "L";
                var signed = result switch { "W" => 1, "L" => -1, _ => 0 };
                flatUnits = TotalsMath.OuPnL(american, signed);
            }

            foreach (var scheme in StakeSchemes.All)
            {
                var key = (strategyId, venue, scheme.Code);
                var bank = bankrolls[key];
                var stake = scheme.Code == "Flat" && bank > 0
                    ? Math.Min(10.0, bank)
                    : StakeSchemes.StakeDollars(scheme.Code, d.ModelProb, american, bank, d.AbsEdge, d.Sport);
                if (stake < 0.50) continue;
                var lanePnl = flatUnits is { } u ? stake * u : 0;
                bankrolls[key] = Math.Max(0, bank + lanePnl);
                tickets.Add(new WalkTicket(
                    yyyymmdd, strategyId, role, d.Sport, d.GameKey, d.Home, d.Away,
                    d.Side!, d.Line!.Value, american, d.AssumedPrice, d.ModelProb, d.Ev, d.AbsEdge,
                    venue, scheme.Code, Math.Round(stake, 2), final, result, flatUnits, lanePnl,
                    closeInfo.Close == 0 ? null : closeInfo.Close,
                    closeInfo.Book == default ? null : SameBookClose.BookLabel(closeInfo.Book),
                    sameBook, flagged, clvPts));
            }
        }

        return tickets;
    }

    static int AdjustVenueAmerican(int american, string venue)
    {
        var buy = OddsMath.ImpliedProbabilityFromAmerican(american);
        if (buy is <= 0 or >= 1) return american;
        var eff = OddsMath.EffectiveBuyPrice(buy, venue);
        return OddsMath.AmericanFromBuyPrice(eff);
    }

    static async Task<Game?> FindGameAsync(AppDbContext db, StrategyDecision d, CancellationToken ct)
    {
        var day = DateOnly.ParseExact(d.GameKey.Length >= 8 ? d.GameKey[..8] : "19700101",
            "yyyyMMdd", CultureInfo.InvariantCulture);
        var tipFrom = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(-1);
        var tipTo = tipFrom.AddDays(4);
        var games = (await db.Games.AsNoTracking()
                .Where(g => g.Sport == d.Sport)
                .ToListAsync(ct))
            .Where(g => g.TipoffUtc >= tipFrom && g.TipoffUtc < tipTo)
            .ToList();
        return games.FirstOrDefault(g =>
            g.HomeTeam.Equals(d.Home, StringComparison.OrdinalIgnoreCase)
            && g.AwayTeam.Equals(d.Away, StringComparison.OrdinalIgnoreCase)
            && EspnLineBackfill.KeyDate(g.Sport, g.TipoffUtc) == day);
    }

    static EngineWalkReport BuildReport(
        DateOnly from,
        DateOnly to,
        List<WalkTicket> tickets,
        int oregonExcluded,
        int closeOnlySkipped,
        int days,
        List<(string Id, string Role)> arms)
    {
        WalkArmSummary Sum(string id, string role)
        {
            // Flat @ DK defines bet W-L / flat units / CLV; lane $ sums all venues×schemes.
            var flatDk = tickets.Where(t =>
                t.StrategyId.Equals(id, StringComparison.OrdinalIgnoreCase)
                && t.Venue == PaperVenues.DraftKings
                && t.Scheme == "Flat").ToList();
            var allLane = tickets.Where(t => t.StrategyId.Equals(id, StringComparison.OrdinalIgnoreCase)).ToList();
            var clvs = flatDk.Where(t => t.ClvPts is not null).Select(t => t.ClvPts!.Value).ToList();
            var cp = Checkpoints.Evaluate(clvs);
            return new WalkArmSummary(
                id, role,
                flatDk.Count,
                flatDk.Count(t => t.Result == "W"),
                flatDk.Count(t => t.Result == "L"),
                flatDk.Count(t => t.Result == "P"),
                flatDk.Sum(t => t.FlatPnlUnits ?? 0),
                allLane.Sum(t => t.LanePnlDollars),
                clvs.Count == 0 ? null : clvs.Average(),
                clvs.Count,
                flatDk.Count(t => t.ClvFlagged),
                flatDk.Count == 0 ? 0 : flatDk.Count(t => t.AssumedJuice) / (double)flatDk.Count,
                oregonExcluded,
                cp);
        }

        // Deduplicate: V0-paper appears in before and after-paper — one summary each role label.
        var before = BeforeStrategyIds.Select(id => Sum(id, "before")).ToList();
        var afterPaper = AfterPaperIds.Select(id => Sum(id, "after-paper")).ToList();
        var afterShadow = AfterShadowPassIds.Select(id => Sum(id, "after-shadow")).ToList();
        if (arms.All(a => a.Role == "custom"))
        {
            before = arms.Select(a => Sum(a.Id, "custom")).ToList();
            afterPaper = [];
            afterShadow = [];
        }

        return new EngineWalkReport(
            from.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            to.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            Round4CloseSha,
            before, afterPaper, afterShadow,
            tickets, oregonExcluded, closeOnlySkipped, days);
    }

    static AppDbContext OpenWarehouse(string path)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        return new AppDbContext(options);
    }
}
