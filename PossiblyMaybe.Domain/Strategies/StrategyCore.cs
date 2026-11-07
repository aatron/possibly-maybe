namespace PossiblyMaybe.Domain;

/// <summary>
/// Plug-in strategy (R8). Sees only <see cref="AsOfView"/> — no DB handle.
/// New math approach = one class under Strategies/&lt;name&gt;/ + a params record.
/// </summary>
public interface IStrategy
{
    string Id { get; }

    /// <summary>Online update after a day's results are revealed (R9 engine). May be a no-op.</summary>
    void Learn(IReadOnlyList<FinalGameRow> newResults);

    /// <summary>
    /// Score the day's slate. Returns one decision per candidate (picks and skips),
    /// with model P and EV when a side was evaluated.
    /// </summary>
    IReadOnlyList<StrategyDecision> Decide(StrategySlate slate, AsOfView view);
}

/// <summary>Pre-built model/line rows for a day (engine / slate builders fill these; strategies do not hit the DB).</summary>
public sealed record StrategySlate(IReadOnlyList<SlateCandidate> Candidates);

/// <summary>One candidate's strategy output — mirrors <see cref="VariantDecision"/> plus game identity.</summary>
public sealed record StrategyDecision(
    string GameKey,
    string Sport,
    string Home,
    string Away,
    bool Pick,
    string? Side,
    double? Line,
    int AmericanOdds,
    double ModelProb,
    double Ev,
    double AbsEdge,
    string? SkipReason,
    bool AssumedPrice = false);

/// <summary>
/// Auto-registered strategies (ModuleInitializer under Strategies/&lt;name&gt;/).
/// Wave-B ports land as one folder each — no edit to other strategies.
/// </summary>
public static class StrategyRegistry
{
    private static readonly object Gate = new();
    private static readonly List<IStrategy> Bags = [];
    private static IReadOnlyList<IStrategy>? _all;

    public static void Register(IStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        lock (Gate)
        {
            if (Bags.Any(s => s.Id.Equals(strategy.Id, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Strategy '{strategy.Id}' already registered.");
            Bags.Add(strategy);
            _all = null;
        }
    }

    public static IStrategy V0 => Require(V0TotalsStrategy.StrategyId);

    public static IReadOnlyList<IStrategy> All
    {
        get
        {
            lock (Gate)
            {
                return _all ??= Bags.OrderBy(s => s.Id, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }
    }

    public static IStrategy Require(string id)
        => All.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
           ?? throw new ArgumentException($"Unknown strategy id '{id}'");
}
