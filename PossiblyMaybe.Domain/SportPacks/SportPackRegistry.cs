namespace PossiblyMaybe.Domain.SportPacks;

/// <summary>League id → pack. C1+ registers beside MLS.</summary>
public static class SportPackRegistry
{
    static readonly Dictionary<string, SportPack> ById =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [MlsSportPack.Instance.LeagueId] = MlsSportPack.Instance,
            [LaLigaSportPack.Instance.LeagueId] = LaLigaSportPack.Instance,
            [EflSportPack.Instance.LeagueId] = EflSportPack.Instance,
        };

    public static SportPack Mls => MlsSportPack.Instance;
    public static SportPack LaLiga => LaLigaSportPack.Instance;
    public static SportPack Efl => EflSportPack.Instance;

    public static SportPack Get(string leagueId)
        => ById.TryGetValue(leagueId, out var p)
            ? p
            : throw new KeyNotFoundException($"No sport pack for '{leagueId}'.");

    public static bool TryGet(string leagueId, out SportPack pack)
        => ById.TryGetValue(leagueId, out pack!);
}
