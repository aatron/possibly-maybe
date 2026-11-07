namespace PossiblyMaybe.Domain.SportPacks;

/// <summary>League id → pack.</summary>
public static class SportPackRegistry
{
    static readonly Dictionary<string, SportPack> ById =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [EplSportPack.Instance.LeagueId] = EplSportPack.Instance,
            [MlbSportPack.Instance.LeagueId] = MlbSportPack.Instance,
            [MlsSportPack.Instance.LeagueId] = MlsSportPack.Instance,
            [NcaabSportPack.Instance.LeagueId] = NcaabSportPack.Instance,
            [NcaafSportPack.Instance.LeagueId] = NcaafSportPack.Instance,
            [NflSportPack.Instance.LeagueId] = NflSportPack.Instance,
        };

    public static SportPack Epl => EplSportPack.Instance;
    public static SportPack Mlb => MlbSportPack.Instance;
    public static SportPack Mls => MlsSportPack.Instance;
    public static SportPack Ncaab => NcaabSportPack.Instance;
    public static SportPack Ncaaf => NcaafSportPack.Instance;
    public static SportPack Nfl => NflSportPack.Instance;

    public static SportPack Get(string leagueId)
        => ById.TryGetValue(leagueId, out var p)
            ? p
            : throw new KeyNotFoundException($"No sport pack for '{leagueId}'.");

    public static bool TryGet(string leagueId, out SportPack pack)
        => ById.TryGetValue(leagueId, out pack!);
}
