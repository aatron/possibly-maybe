using System.Runtime.CompilerServices;

namespace PossiblyMaybe.Domain;

/// <summary>Params for V0 totals. Empty today — new approaches add knobs on their own record.</summary>
public sealed record V0TotalsParams;

/// <summary>
/// Champion V0 as an <see cref="IStrategy"/>: same path as <see cref="VariantRegistry.Evaluate"/>(V0).
/// Slate candidates are pre-built (DailySlate / R9); AsOfView is accepted for the contract but unused
/// until the day engine builds candidates from the view.
/// </summary>
public sealed class V0TotalsStrategy : IStrategy
{
    public const string StrategyId = "V0";

    readonly V0TotalsParams _params;

    public V0TotalsStrategy(V0TotalsParams? parameters = null)
        => _params = parameters ?? new V0TotalsParams();

    public string Id => StrategyId;

    // shortcut: V0 ratings live in pre-built SlateCandidates; Learn is for R9 online books.
    public void Learn(IReadOnlyList<FinalGameRow> newResults) { }

    public V0TotalsParams Parameters => _params;

    public IReadOnlyList<StrategyDecision> Decide(StrategySlate slate, AsOfView view)
    {
        ArgumentNullException.ThrowIfNull(slate);
        ArgumentNullException.ThrowIfNull(view);
        // view reserved: R9 builds candidates from AsOfView; V0 only filters/evaluates the slate.

        var list = new List<StrategyDecision>(slate.Candidates.Count);
        foreach (var c in slate.Candidates)
        {
            var d = VariantRegistry.Evaluate(VariantRegistry.V0, c);
            list.Add(new StrategyDecision(
                VariantRegistry.GameKey(c),
                c.Sport,
                c.Home,
                c.Away,
                d.Pick,
                d.Side,
                d.Line,
                d.AmericanOdds,
                d.ModelProb,
                d.Ev,
                d.AbsEdge,
                d.SkipReason,
                d.AssumedPrice));
        }

        return list;
    }

#pragma warning disable CA2255 // strategy self-register so a new approach is one folder
    [ModuleInitializer]
    internal static void Init() => StrategyRegistry.Register(new V0TotalsStrategy());
#pragma warning restore CA2255
}
