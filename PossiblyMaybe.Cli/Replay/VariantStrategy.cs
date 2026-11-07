using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Replay;

/// <summary>
/// Adapts a <see cref="Variant"/> to <see cref="IStrategy"/> so DayEngine can score shadow arms.
/// Learn is a no-op — ratings live on pre-built <see cref="SlateCandidate"/>s (same as V0).
/// </summary>
public sealed class VariantStrategy : IStrategy
{
    readonly Variant _variant;

    public VariantStrategy(Variant variant)
    {
        ArgumentNullException.ThrowIfNull(variant);
        _variant = variant;
    }

    public string Id => _variant.Id;

    public Variant Variant => _variant;

    public void Learn(IReadOnlyList<FinalGameRow> newResults) { }

    public IReadOnlyList<StrategyDecision> Decide(StrategySlate slate, AsOfView view)
    {
        ArgumentNullException.ThrowIfNull(slate);
        ArgumentNullException.ThrowIfNull(view);

        var list = new List<StrategyDecision>(slate.Candidates.Count);
        foreach (var c in slate.Candidates)
        {
            var d = VariantRegistry.Evaluate(_variant, c);
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

    /// <summary>Resolve a strategy id: registry IStrategy, else VariantRegistry id, else PaperChamp alias.</summary>
    public static IStrategy Resolve(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (id.Equals("V0-paper", StringComparison.OrdinalIgnoreCase)
            || id.Equals("PaperChamp", StringComparison.OrdinalIgnoreCase))
            return new VariantStrategy(VariantRegistry.PaperChamp with { Id = "V0-paper", Label = "paper-champ" });

        var known = StrategyRegistry.All.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (known is not null) return known;

        return new VariantStrategy(VariantRegistry.Require(id));
    }
}
