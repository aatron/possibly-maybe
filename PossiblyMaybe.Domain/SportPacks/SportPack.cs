namespace PossiblyMaybe.Domain.SportPacks;

/// <summary>
/// Config-driven league pack. C1+ copies this shape; champ gates stay on *ChampConfig consts
/// (default-parameter sites) and the pack instance references them so numbers never diverge.
/// </summary>
public sealed record SportPack(
    string LeagueId,
    string ResultsSource,
    IReadOnlyList<string> OddsSources,
    string RatingBookKind,
    IReadOnlyList<string> OregonNames,
    Func<string?, string?> ResolveAlias,
    IReadOnlyList<string> SeasonPhases,
    IReadOnlyList<string> TierAFolds,
    string TrainMaxSeason,
    string TierCHoldoutSeason,
    string SkipSeason,
    string Market,
    string Model,
    double MarketWeight,
    double MinEv,
    double MinAbsTotalEdge,
    double EmaAlpha,
    double BankrollSeed)
{
    /// <summary>True when every pack Oregon name is excluded for this league id.</summary>
    public bool OregonNamesExcluded()
        => OregonNames.All(n => OregonExclusion.IsExcluded(LeagueId, n));
}
