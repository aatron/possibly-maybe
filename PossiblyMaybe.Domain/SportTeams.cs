namespace PossiblyMaybe.Domain;

/// <summary>NFL / MLB team name → short code (for multi-sport Game keys).</summary>
public static class SportTeams
{
    private static readonly Dictionary<string, string> Nfl = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Arizona Cardinals"] = "ARI", ["Atlanta Falcons"] = "ATL", ["Baltimore Ravens"] = "BAL",
        ["Buffalo Bills"] = "BUF", ["Carolina Panthers"] = "CAR", ["Chicago Bears"] = "CHI",
        ["Cincinnati Bengals"] = "CIN", ["Cleveland Browns"] = "CLE", ["Dallas Cowboys"] = "DAL",
        ["Denver Broncos"] = "DEN", ["Detroit Lions"] = "DET", ["Green Bay Packers"] = "GB",
        ["Houston Texans"] = "HOU", ["Indianapolis Colts"] = "IND", ["Jacksonville Jaguars"] = "JAX",
        ["Kansas City Chiefs"] = "KC", ["Las Vegas Raiders"] = "LV", ["Oakland Raiders"] = "LV",
        ["Los Angeles Chargers"] = "LAC", ["San Diego Chargers"] = "LAC",
        ["Los Angeles Rams"] = "LAR", ["St. Louis Rams"] = "LAR",
        ["Miami Dolphins"] = "MIA", ["Minnesota Vikings"] = "MIN", ["New England Patriots"] = "NE",
        ["New Orleans Saints"] = "NO", ["New York Giants"] = "NYG", ["New York Jets"] = "NYJ",
        ["Philadelphia Eagles"] = "PHI", ["Pittsburgh Steelers"] = "PIT",
        ["San Francisco 49ers"] = "SF", ["Seattle Seahawks"] = "SEA",
        ["Tampa Bay Buccaneers"] = "TB", ["Tennessee Titans"] = "TEN", ["Washington Commanders"] = "WAS",
        ["Washington Football Team"] = "WAS", ["Washington Redskins"] = "WAS",
        // short / pfr-ish
        ["ARI"] = "ARI", ["ATL"] = "ATL", ["BAL"] = "BAL", ["BUF"] = "BUF", ["CAR"] = "CAR",
        ["CHI"] = "CHI", ["CIN"] = "CIN", ["CLE"] = "CLE", ["DAL"] = "DAL", ["DEN"] = "DEN",
        ["DET"] = "DET", ["GB"] = "GB", ["HOU"] = "HOU", ["IND"] = "IND", ["JAX"] = "JAX", ["JAC"] = "JAX",
        ["KC"] = "KC", ["LV"] = "LV", ["LAC"] = "LAC", ["LAR"] = "LAR", ["LA"] = "LAR",
        ["MIA"] = "MIA", ["MIN"] = "MIN", ["NE"] = "NE", ["NO"] = "NO", ["NYG"] = "NYG", ["NYJ"] = "NYJ",
        ["PHI"] = "PHI", ["PIT"] = "PIT", ["SF"] = "SF", ["SEA"] = "SEA", ["TB"] = "TB",
        ["TEN"] = "TEN", ["WAS"] = "WAS", ["WSH"] = "WAS",
    };

    private static readonly Dictionary<string, string> Mlb = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Arizona Diamondbacks"] = "ARI", ["Atlanta Braves"] = "ATL", ["Baltimore Orioles"] = "BAL",
        ["Boston Red Sox"] = "BOS", ["Chicago Cubs"] = "CHC", ["Chicago White Sox"] = "CWS",
        ["Cincinnati Reds"] = "CIN", ["Cleveland Guardians"] = "CLE", ["Cleveland Indians"] = "CLE",
        ["Colorado Rockies"] = "COL", ["Detroit Tigers"] = "DET", ["Houston Astros"] = "HOU",
        ["Kansas City Royals"] = "KC", ["Los Angeles Angels"] = "LAA", ["Los Angeles Dodgers"] = "LAD",
        ["Miami Marlins"] = "MIA", ["Milwaukee Brewers"] = "MIL", ["Minnesota Twins"] = "MIN",
        ["New York Mets"] = "NYM", ["New York Yankees"] = "NYY", ["Oakland Athletics"] = "OAK",
        ["Athletics"] = "OAK", ["Philadelphia Phillies"] = "PHI", ["Pittsburgh Pirates"] = "PIT",
        ["San Diego Padres"] = "SD", ["San Francisco Giants"] = "SF", ["Seattle Mariners"] = "SEA",
        ["St. Louis Cardinals"] = "STL", ["Tampa Bay Rays"] = "TB", ["Texas Rangers"] = "TEX",
        ["Toronto Blue Jays"] = "TOR", ["Washington Nationals"] = "WSH",
    };

    public static string? TryNfl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var t = raw.Trim();
        return Nfl.TryGetValue(t, out var c) ? c : null;
    }

    public static string? TryMlb(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var t = raw.Trim();
        return Mlb.TryGetValue(t, out var c) ? c : null;
    }

    /// <summary>MLS: stable short codes from full Kaggle names (or pass-through if already short).</summary>
    public static string? TryMls(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var t = raw.Trim();
        return Mls.TryGetValue(t, out var c) ? c : null;
    }

    private static readonly Dictionary<string, string> Mls = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Atlanta United"] = "ATL", ["Atlanta United FC"] = "ATL", ["Austin FC"] = "ATX",
        ["CF Montreal"] = "MTL", ["CF Montréal"] = "MTL",
        ["Charlotte FC"] = "CLT", ["Chicago Fire"] = "CHI", ["Chicago Fire FC"] = "CHI",
        ["Colorado Rapids"] = "COL", ["Columbus Crew"] = "CLB",
        ["DC United"] = "DC", ["D.C. United"] = "DC",
        ["FC Cincinnati"] = "CIN", ["FC Dallas"] = "DAL",
        ["Houston Dynamo"] = "HOU", ["Houston Dynamo FC"] = "HOU",
        ["Inter Miami CF"] = "MIA", ["Los Angeles FC"] = "LAFC", ["LAFC"] = "LAFC",
        ["Los Angeles Galaxy"] = "LAG", ["LA Galaxy"] = "LAG",
        ["Minnesota United FC"] = "MIN", ["Nashville SC"] = "NSH",
        ["New England Revolution"] = "NE", ["New York City FC"] = "NYC",
        ["New York Red Bulls"] = "RBNY", ["Red Bull New York"] = "RBNY",
        ["Orlando City"] = "ORL", ["Orlando City SC"] = "ORL",
        ["Philadelphia Union"] = "PHI", ["Portland Timbers"] = "POR",
        ["Real Salt Lake"] = "RSL", ["San Diego FC"] = "SD",
        ["San Jose Earthquakes"] = "SJ",
        ["Seattle Sounders"] = "SEA", ["Seattle Sounders FC"] = "SEA",
        ["Sporting Kansas City"] = "SKC",
        ["St. Louis City"] = "STL", ["St. Louis CITY SC"] = "STL",
        ["Toronto FC"] = "TOR",
        ["Vancouver Whitecaps"] = "VAN", ["Vancouver Whitecaps FC"] = "VAN",
        // ESPN abbreviations / short codes
        ["ATL"] = "ATL", ["ATX"] = "ATX", ["CHI"] = "CHI", ["CIN"] = "CIN", ["CLB"] = "CLB",
        ["CLT"] = "CLT", ["COL"] = "COL", ["DAL"] = "DAL", ["DC"] = "DC", ["HOU"] = "HOU",
        ["LAG"] = "LAG", ["MIA"] = "MIA", ["MIN"] = "MIN", ["MTL"] = "MTL", ["NE"] = "NE",
        ["NSH"] = "NSH", ["NYC"] = "NYC", ["ORL"] = "ORL", ["PHI"] = "PHI", ["POR"] = "POR",
        ["RBNY"] = "RBNY", ["RSL"] = "RSL", ["SD"] = "SD", ["SEA"] = "SEA", ["SJ"] = "SJ",
        ["SKC"] = "SKC", ["STL"] = "STL", ["TOR"] = "TOR", ["VAN"] = "VAN",
    };

    public static string GameKey(string sport, DateOnly date, string home, string away)
        => $"{sport.ToLowerInvariant()}:{date:yyyy-MM-dd}:{home}:{away}";
}
