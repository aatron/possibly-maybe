namespace PossiblyMaybe.Domain;

/// <summary>Normalize team tokens from A/B/C/ESPN into a single 3-letter code.</summary>
public static class TeamCodes
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["atl"] = "ATL", ["atlanta"] = "ATL", ["atlanta hawks"] = "ATL",
        ["bos"] = "BOS", ["boston"] = "BOS", ["boston celtics"] = "BOS",
        ["bkn"] = "BKN", ["brooklyn"] = "BKN", ["brooklyn nets"] = "BKN", ["nj"] = "BKN", ["njn"] = "BKN", ["brkn"] = "BKN",
        ["cha"] = "CHA", ["charlotte"] = "CHA", ["charlotte hornets"] = "CHA", ["cho"] = "CHA", ["chh"] = "CHA",
        ["chi"] = "CHI", ["chicago"] = "CHI", ["chicago bulls"] = "CHI",
        ["cle"] = "CLE", ["cleveland"] = "CLE", ["cleveland cavaliers"] = "CLE",
        ["dal"] = "DAL", ["dallas"] = "DAL", ["dallas mavericks"] = "DAL",
        ["den"] = "DEN", ["denver"] = "DEN", ["denver nuggets"] = "DEN",
        ["det"] = "DET", ["detroit"] = "DET", ["detroit pistons"] = "DET",
        ["gs"] = "GSW", ["gsw"] = "GSW", ["golden state"] = "GSW", ["golden state warriors"] = "GSW", ["goldenstate"] = "GSW",
        ["hou"] = "HOU", ["houston"] = "HOU", ["houston rockets"] = "HOU",
        ["ind"] = "IND", ["indiana"] = "IND", ["indiana pacers"] = "IND",
        ["lac"] = "LAC", ["la clippers"] = "LAC", ["los angeles clippers"] = "LAC", ["clippers"] = "LAC",
        ["lal"] = "LAL", ["la lakers"] = "LAL", ["los angeles lakers"] = "LAL", ["lakers"] = "LAL",
        ["mem"] = "MEM", ["memphis"] = "MEM", ["memphis grizzlies"] = "MEM",
        ["mia"] = "MIA", ["miami"] = "MIA", ["miami heat"] = "MIA",
        ["mil"] = "MIL", ["milwaukee"] = "MIL", ["milwaukee bucks"] = "MIL",
        ["min"] = "MIN", ["minnesota"] = "MIN", ["minnesota timberwolves"] = "MIN",
        ["no"] = "NOP", ["nop"] = "NOP", ["nor"] = "NOP", ["nok"] = "NOP", ["noh"] = "NOP",
        ["new orleans"] = "NOP", ["new orleans pelicans"] = "NOP",
        ["ny"] = "NYK", ["nyk"] = "NYK", ["new york"] = "NYK", ["new york knicks"] = "NYK",
        // shortcut: SEA→OKC merges franchises; split if Elo continuity must be audited separately
        ["okc"] = "OKC", ["oklahoma city"] = "OKC", ["oklahoma city thunder"] = "OKC", ["sea"] = "OKC",
        ["orl"] = "ORL", ["orlando"] = "ORL", ["orlando magic"] = "ORL",
        ["phi"] = "PHI", ["philadelphia"] = "PHI", ["philadelphia 76ers"] = "PHI",
        ["phx"] = "PHX", ["phoenix"] = "PHX", ["phoenix suns"] = "PHX", ["pho"] = "PHX",
        ["por"] = "POR", ["portland"] = "POR", ["portland trail blazers"] = "POR",
        ["sa"] = "SAS", ["sas"] = "SAS", ["san antonio"] = "SAS", ["san antonio spurs"] = "SAS",
        ["sac"] = "SAC", ["sacramento"] = "SAC", ["sacramento kings"] = "SAC",
        ["tor"] = "TOR", ["toronto"] = "TOR", ["toronto raptors"] = "TOR",
        ["uta"] = "UTA", ["utah"] = "UTA", ["utah jazz"] = "UTA", ["uth"] = "UTA",
        ["was"] = "WAS", ["washington"] = "WAS", ["washington wizards"] = "WAS", ["wsh"] = "WAS",
    };

    public static string? TryNormalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var key = raw.Trim();
        if (key.Length == 3 && Map.Values.Contains(key.ToUpperInvariant()))
            return key.ToUpperInvariant();
        if (Map.TryGetValue(key, out var code))
            return code;
        // strip punctuation / collapse spaces
        key = key.Replace(".", "").Replace("-", " ").Trim();
        if (Map.TryGetValue(key, out code))
            return code;
        return null;
    }

    public static string Normalize(string raw)
        => TryNormalize(raw) ?? throw new ArgumentException($"Unknown team token: '{raw}'");

    public static bool IsDonaghy(string officialName)
        => officialName.Contains("Donaghy", StringComparison.OrdinalIgnoreCase);

    public static string GameKey(DateOnly date, string home, string away)
        => $"nba:{date:yyyy-MM-dd}:{home}:{away}";
}
