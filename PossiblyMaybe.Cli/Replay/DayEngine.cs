using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Cli.Paper;
using PossiblyMaybe.Data;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Replay;

/// <summary>
/// One day's decisions through the shared engine path (R9).
/// Live and replay both: AsOfView → Learn → slate → Decide → (shared layers stubbed).
/// R10: simulations touching vault seasons require --final + strategy hash (one use).
/// </summary>
public sealed record DayEngineResult(
    string Yyyymmdd,
    string StrategyId,
    DateTimeOffset AsOf,
    string ViewWatermark,
    IReadOnlyList<SlateCandidate> Candidates,
    IReadOnlyList<StrategyDecision> Decisions,
    IReadOnlyList<DayLeagueReportStub> ReportByLeague,
    string? TrialFooter = null);

/// <summary>shortcut: n / CLV / PnL stages stubbed — upgrade when R5 reporter wires settle closes.</summary>
public sealed record DayLeagueReportStub(string League, int N, double? ClvMean, double? FlatPnl);

public static class DayEngine
{
    /// <summary>
    /// Pure path used by fixture proofs: same candidates live would score → strategy.Decide under AsOfView.
    /// Reveal = view.FinalGames; Learn then Decide. Oregon / EV gates stay inside the strategy (Evaluate).
    /// When <paramref name="options"/>.IsSimulation, vault seasons are sealed unless Final.
    /// </summary>
    public static DayEngineResult Run(
        string yyyymmdd,
        IStrategy strategy,
        AsOfView view,
        IReadOnlyList<SlateCandidate> candidates,
        EngineRunOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(candidates);

        options ??= EngineRunOptions.Live(strategy.Id);
        ApplyVaultAndTrials(yyyymmdd, candidates, options);

        // Day step: reveal prior finals known as-of, then Learn (V0 no-op), then Decide.
        strategy.Learn(view.FinalGames());
        var decisions = strategy.Decide(new StrategySlate(candidates), view);
        // shortcut: Oregon / guards / venue pricing / staking / lock / settle not a separate pass yet —
        // Oregon + EV already inside Evaluate(V0); upgrade when shared post-Decide layers land.
        var footer = options.Trials?.FormatReportFooter();
        return new DayEngineResult(
            yyyymmdd,
            strategy.Id,
            view.AsOf,
            view.Watermark(),
            candidates,
            decisions,
            StubReport(decisions),
            footer);
    }

    /// <summary>
    /// Warehouse day run: AsOfView at slate tipoff cutoff + DailySlate candidates + strategy.Decide.
    /// Live path (no vault burn). Pass <paramref name="options"/> with IsSimulation for look-once sims.
    /// </summary>
    public static async Task<DayEngineResult> RunDayAsync(
        string yyyymmdd,
        string strategyId,
        string warehouseDbPath,
        EngineRunOptions? options = null,
        CancellationToken ct = default)
    {
        if (yyyymmdd.Length != 8)
            throw new ArgumentException("yyyymmdd must be 8 digits.", nameof(yyyymmdd));
        ArgumentException.ThrowIfNullOrWhiteSpace(warehouseDbPath);

        var strategy = StrategyRegistry.Require(strategyId);
        var asOf = PointInTime.SlateAsOf(yyyymmdd);
        // shortcut: SlateAsOf = UTC midnight of the day (same as DailySlate ratings cutoff), not wall-clock EOD;
        // upgrade to per-game tipoff decision times when live locks pre-tip.

        await using var db = OpenWarehouse(warehouseDbPath);
        var view = await AsOfViewFactory.FromDbAsync(db, asOf, ct: ct);
        var candidates = await BuildSlateCandidatesAsync(warehouseDbPath, yyyymmdd, ct);
        return Run(yyyymmdd, strategy, view, candidates, options ?? EngineRunOptions.Live(strategy.Id));
    }

    /// <summary>Same candidate builders as variants-shadow / live slate boards.</summary>
    public static async Task<List<SlateCandidate>> BuildSlateCandidatesAsync(
        string warehouseDbPath, string yyyymmdd, CancellationToken ct = default)
    {
        var candidates = new List<SlateCandidate>();
        candidates.AddRange(await DailySlate.BuildNbaCandidatesAsync(warehouseDbPath, yyyymmdd, ct));
        try
        {
            candidates.AddRange(await DailySlate.BuildNcaabCandidatesAsync(warehouseDbPath, yyyymmdd, ct));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"engine-day: NCAAB scoreboard skipped ({ex.Message})");
        }

        foreach (var sport in DailySlate.SoccerEspnPaths.Keys)
            candidates.AddRange(await DailySlate.BuildSoccerCandidatesAsync(sport, warehouseDbPath, yyyymmdd, ct));

        try
        {
            candidates.AddRange(await DailySlate.BuildNhlCandidatesAsync(warehouseDbPath, yyyymmdd, ct));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"engine-day: NHL scoreboard skipped ({ex.Message})");
        }

        return candidates;
    }

    public static void Print(DayEngineResult result)
    {
        Console.WriteLine(
            $"engine-day {result.Yyyymmdd} strategy={result.StrategyId} asOf={result.AsOf:u} " +
            $"candidates={result.Candidates.Count} decisions={result.Decisions.Count} " +
            $"wm={result.ViewWatermark[..Math.Min(12, result.ViewWatermark.Length)]}…");
        foreach (var d in result.Decisions.Where(x => x.Pick))
            Console.WriteLine(
                $"  GO {d.Sport} {d.Away}@{d.Home} {d.Side} {d.Line} @{d.AmericanOdds} " +
                $"p={d.ModelProb:F3} EV={d.Ev:P1} edge={d.AbsEdge:F2}");
        var skips = result.Decisions.Count(x => !x.Pick);
        if (skips > 0)
            Console.WriteLine($"  ({skips} skips)");
        foreach (var r in result.ReportByLeague)
            Console.WriteLine($"  report {r.League}: n={r.N} CLV=stub flatPnL=stub");
        if (!string.IsNullOrEmpty(result.TrialFooter))
            Console.WriteLine($"  {result.TrialFooter}");
    }

    static void ApplyVaultAndTrials(
        string yyyymmdd,
        IReadOnlyList<SlateCandidate> candidates,
        EngineRunOptions options)
    {
        if (options.IsSimulation)
        {
            var store = options.VaultStore ?? throw new InvalidOperationException(
                "Simulation requires a VaultStore (EngineRunOptions.Sim).");
            foreach (var league in candidates.Select(c => c.Sport).Distinct(StringComparer.OrdinalIgnoreCase))
                Vault.AuthorizeSimulationDate(store, league, yyyymmdd, options.StrategyHash, options.Final);
        }

        if (options.Trials is { } ledger && options.IsSimulation)
        {
            var from = options.RangeFrom ?? yyyymmdd;
            var to = options.RangeTo ?? yyyymmdd;
            ledger.Record(options.StrategyHash, options.ParamsJson, from, to, gridSize: 1);
        }
    }

    static IReadOnlyList<DayLeagueReportStub> StubReport(IReadOnlyList<StrategyDecision> decisions)
        => decisions
            .GroupBy(d => d.Sport, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new DayLeagueReportStub(g.Key, g.Count(x => x.Pick), ClvMean: null, FlatPnl: null))
            .ToList();

    static AppDbContext OpenWarehouse(string path)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        return new AppDbContext(options);
    }
}
