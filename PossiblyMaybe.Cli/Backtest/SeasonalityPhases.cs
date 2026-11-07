namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// Pre-registered season-phase calendars (A5). Windows locked in
/// <c>research_notes/round4/a5_proof.md</c> before any scan — do not mine new ones here.
/// </summary>
public static class SeasonalityPhases
{
    public readonly record struct Window(int StartMonth, int StartDay, int EndMonth, int EndDay);

    public readonly record struct PhaseDef(string Sport, string PhaseId, IReadOnlyList<Window> Windows);

    /// <summary>All locked (sport, phase) defs; order within a sport = match priority.</summary>
    public static IReadOnlyList<PhaseDef> All { get; } = Build();

    public static IReadOnlyList<string> PhaseIdsFor(string sport)
        => All.Where(p => p.Sport.Equals(sport, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.PhaseId)
            .ToList();

    /// <summary>First matching phase for tip date, or null if out-of-phase.</summary>
    public static string? Resolve(string sport, string yyyymmdd)
    {
        if (yyyymmdd.Length != 8
            || !int.TryParse(yyyymmdd.AsSpan(4, 2), out var mm)
            || !int.TryParse(yyyymmdd.AsSpan(6, 2), out var dd))
            return null;

        foreach (var def in All)
        {
            if (!def.Sport.Equals(sport, StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (var w in def.Windows)
            {
                if (InWindow(mm, dd, w))
                    return def.PhaseId;
            }
        }
        return null;
    }

    public static bool TryParseGameKeyDay(string gameKey, out string yyyymmdd)
    {
        yyyymmdd = "";
        var sp = gameKey.IndexOf(' ');
        if (sp != 8) return false;
        yyyymmdd = gameKey[..8];
        return yyyymmdd.All(char.IsDigit);
    }

    static bool InWindow(int mm, int dd, Window w)
    {
        var t = mm * 100 + dd;
        var a = w.StartMonth * 100 + w.StartDay;
        var b = w.EndMonth * 100 + w.EndDay;
        // Winter wrap (e.g. 12-20 … 01-10, 12-01 … 02-28).
        if (a <= b)
            return t >= a && t <= b;
        return t >= a || t <= b;
    }

    static List<PhaseDef> Build()
    {
        // NBA — proof §NBA
        var nba = new List<PhaseDef>
        {
            new("NBA", "opening-weeks", [new(10, 1, 11, 14)]),
            new("NBA", "weather-winter", [new(12, 1, 2, 29)]),
            new("NBA", "tanking-stretch", [new(3, 1, 4, 15)]),
            new("NBA", "playoffs", [new(4, 16, 6, 30)]),
        };

        // MLS — proof §MLS
        var mls = new List<PhaseDef>
        {
            new("MLS", "opening-weeks", [new(2, 15, 3, 31)]),
            new("MLS", "summer-intl-break", [new(6, 1, 7, 31)]),
            new("MLS", "final-weeks", [new(9, 1, 10, 15)]),
            new("MLS", "playoffs", [new(10, 16, 12, 10)]),
        };

        // LIGUE1 / SERIEA — intl-break before opening so FIFA weeks win inside Aug–Sep.
        Window[] intl =
        [
            new(9, 1, 9, 10),
            new(10, 7, 10, 16),
            new(11, 11, 11, 20),
            new(3, 17, 3, 26),
        ];
        // promoted-first stays in a5_proof.md but is not calendar-matched until a
        // promoted-club list exists (would collide with opening-weeks).
        var euro = new List<PhaseDef>();
        foreach (var sport in new[] { "LIGUE1", "SERIEA" })
        {
            euro.Add(new(sport, "intl-break", intl));
            euro.Add(new(sport, "opening-weeks", [new(8, 1, 9, 30)]));
            euro.Add(new(sport, "winter-break", [new(12, 20, 1, 10)]));
            euro.Add(new(sport, "final-weeks", [new(4, 15, 5, 31)]));
        }

        // LIGAMX
        var mx = new List<PhaseDef>
        {
            new("LIGAMX", "apertura-opening", [new(7, 1, 8, 31)]),
            new("LIGAMX", "apertura-liguilla", [new(11, 1, 12, 15)]),
            new("LIGAMX", "clausura-opening", [new(1, 1, 2, 29)]),
            new("LIGAMX", "clausura-liguilla", [new(4, 15, 5, 31)]),
        };

        return [..nba, ..mls, ..euro, ..mx];
    }
}
