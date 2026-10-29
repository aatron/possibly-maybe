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

    /// <summary>Map ESPN / common names → warehouse Ligue 1 team strings.</summary>
    public static string? TryLigue1(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var t = raw.Trim();
        return Ligue1.TryGetValue(t, out var c) ? c : null;
    }

    /// <summary>Map ESPN / common names → warehouse Serie A team strings.</summary>
    public static string? TrySerieA(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var t = raw.Trim();
        return SerieA.TryGetValue(t, out var c) ? c : null;
    }

    /// <summary>Map ESPN / common names → warehouse Liga MX team strings.</summary>
    public static string? TryLigaMx(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var t = raw.Trim();
        return LigaMx.TryGetValue(t, out var c) ? c : null;
    }

    private static readonly Dictionary<string, string> Ligue1 = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AJ Auxerre"] = "AJ Auxerre", ["Auxerre"] = "AJ Auxerre", ["AUX"] = "AJ Auxerre",
        ["Angers"] = "Angers", ["Angers SCO"] = "Angers", ["ANG"] = "Angers",
        ["Le Havre"] = "Le Havre", ["Le Havre AC"] = "Le Havre", ["HAC"] = "Le Havre",
        ["Lens"] = "Lens", ["RC Lens"] = "Lens", ["RCL"] = "Lens",
        ["Lille"] = "Lille", ["LOSC Lille"] = "Lille", ["LILL"] = "Lille", ["LIL"] = "Lille",
        ["Lyon"] = "Lyon", ["Olympique Lyonnais"] = "Lyon", ["OL"] = "Lyon", ["LYON"] = "Lyon",
        ["Marseille"] = "Marseille", ["Olympique de Marseille"] = "Marseille", ["OLM"] = "Marseille", ["OM"] = "Marseille",
        ["Metz"] = "Metz", ["FC Metz"] = "Metz", ["MNS"] = "Metz",
        ["Monaco"] = "Monaco", ["AS Monaco"] = "Monaco", ["MON"] = "Monaco",
        ["Nantes"] = "Nantes", ["FC Nantes"] = "Nantes",
        ["Nice"] = "Nice", ["OGC Nice"] = "Nice", ["NICE"] = "Nice",
        ["Paris FC"] = "Paris FC", ["PAR"] = "Paris FC",
        ["Paris Saint Germain (PSG)"] = "Paris Saint Germain (PSG)",
        ["Paris Saint-Germain"] = "Paris Saint Germain (PSG)", ["PSG"] = "Paris Saint Germain (PSG)",
        ["Reims"] = "Reims", ["Stade de Reims"] = "Reims",
        ["Rennes"] = "Rennes", ["Stade Rennais"] = "Rennes", ["Stade Rennais FC"] = "Rennes", ["REN"] = "Rennes",
        ["Saint Etienne"] = "Saint Etienne", ["Saint-Étienne"] = "Saint Etienne", ["AS Saint-Étienne"] = "Saint Etienne",
        ["Stade Brestois"] = "Stade Brestois", ["Brest"] = "Stade Brestois",
        ["Strasbourg"] = "Strasbourg", ["RC Strasbourg"] = "Strasbourg", ["RC Strasbourg Alsace"] = "Strasbourg", ["STR"] = "Strasbourg",
        ["Toulouse"] = "Toulouse", ["Toulouse FC"] = "Toulouse", ["TOU"] = "Toulouse",
        ["Troyes"] = "Troyes", ["TRY"] = "Troyes",
        ["Lorient"] = "Lorient", ["Montpellier"] = "Montpellier", ["Clermont"] = "Clermont",
    };

    private static readonly Dictionary<string, string> SerieA = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AC Milan"] = "AC Milan", ["Milan"] = "AC Milan", ["MIL"] = "AC Milan",
        ["AS Roma"] = "AS Roma", ["Roma"] = "AS Roma", ["ROMA"] = "AS Roma",
        ["Atalanta"] = "Atalanta", ["Atalanta BC"] = "Atalanta", ["ATA"] = "Atalanta",
        ["Bologna"] = "Bologna", ["Bologna FC"] = "Bologna", ["BOL"] = "Bologna",
        ["Cagliari"] = "Cagliari", ["CAG"] = "Cagliari",
        ["Como"] = "Como", ["Como 1907"] = "Como", ["COMO"] = "Como",
        ["Cremonese"] = "Cremonese",
        ["Empoli"] = "Empoli",
        ["Fiorentina"] = "Fiorentina", ["ACF Fiorentina"] = "Fiorentina", ["FIO"] = "Fiorentina",
        ["Frosinone"] = "Frosinone", ["FRO"] = "Frosinone",
        ["Genoa"] = "Genoa", ["Genoa CFC"] = "Genoa", ["GEN"] = "Genoa",
        ["Inter Milan"] = "Inter Milan", ["Internazionale"] = "Inter Milan", ["Inter"] = "Inter Milan", ["INT"] = "Inter Milan",
        ["Juventus"] = "Juventus", ["JUV"] = "Juventus",
        ["Lazio"] = "Lazio", ["SS Lazio"] = "Lazio", ["LAZ"] = "Lazio",
        ["Lecce"] = "Lecce", ["US Lecce"] = "Lecce", ["LEC"] = "Lecce",
        ["Monza"] = "Monza", ["AC Monza"] = "Monza", ["MON"] = "Monza",
        ["Napoli"] = "Napoli", ["SSC Napoli"] = "Napoli", ["NAP"] = "Napoli",
        ["Parma"] = "Parma", ["Parma Calcio"] = "Parma", ["PAR"] = "Parma",
        ["Pisa"] = "Pisa",
        ["Sassuolo"] = "Sassuolo",
        ["Torino"] = "Torino", ["TOR"] = "Torino",
        ["Udinese"] = "Udinese", ["UDI"] = "Udinese",
        ["Venezia"] = "Venezia", ["Venezia FC"] = "Venezia", ["VEN"] = "Venezia",
        ["Verona"] = "Verona", ["Hellas Verona"] = "Verona",
    };

    private static readonly Dictionary<string, string> LigaMx = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Atlas"] = "Atlas", ["Atlas FC"] = "Atlas", ["ATS"] = "Atlas",
        ["Atletico San Luis"] = "Atletico San Luis", ["Atlético San Luis"] = "Atletico San Luis",
        ["San Luis"] = "Atletico San Luis", ["ASL"] = "Atletico San Luis",
        ["CDSyC Cruz Azul"] = "CDSyC Cruz Azul", ["Cruz Azul"] = "CDSyC Cruz Azul", ["CAZ"] = "CDSyC Cruz Azul",
        ["Chivas Guadalajara"] = "Chivas Guadalajara", ["Guadalajara"] = "Chivas Guadalajara",
        ["Chivas"] = "Chivas Guadalajara", ["CD Guadalajara"] = "Chivas Guadalajara", ["GDL"] = "Chivas Guadalajara",
        ["Club America"] = "Club America", ["América"] = "Club America", ["America"] = "Club America",
        ["CF América"] = "Club America", ["AME"] = "Club America",
        ["Club Leon"] = "Club Leon", ["León"] = "Club Leon", ["Leon"] = "Club Leon", ["LEO"] = "Club Leon",
        ["Club Tijuana"] = "Club Tijuana", ["Tijuana"] = "Club Tijuana", ["Xolos"] = "Club Tijuana", ["TIJ"] = "Club Tijuana",
        ["FC Juarez"] = "FC Juarez", ["Juárez"] = "FC Juarez", ["Juarez"] = "FC Juarez", ["JUA"] = "FC Juarez",
        ["Mazatlan FC"] = "Mazatlan FC", ["Mazatlán"] = "Mazatlan FC", ["Mazatlan"] = "Mazatlan FC",
        ["Monterrey"] = "Monterrey", ["CF Monterrey"] = "Monterrey", ["Rayados"] = "Monterrey", ["MTY"] = "Monterrey",
        ["Necaxa"] = "Necaxa", ["Club Necaxa"] = "Necaxa", ["NCX"] = "Necaxa",
        ["Pachuca"] = "Pachuca", ["CF Pachuca"] = "Pachuca", ["PAC"] = "Pachuca",
        ["Puebla"] = "Puebla", ["Puebla FC"] = "Puebla", ["PUE"] = "Puebla",
        ["Pumas U.N.A.M."] = "Pumas U.N.A.M.", ["Pumas"] = "Pumas U.N.A.M.", ["UNAM"] = "Pumas U.N.A.M.",
        ["Queretaro FC"] = "Queretaro FC", ["Querétaro"] = "Queretaro FC", ["Queretaro"] = "Queretaro FC", ["QRO"] = "Queretaro FC",
        ["Santos Laguna"] = "Santos Laguna", ["Santos"] = "Santos Laguna", ["SAN"] = "Santos Laguna",
        ["Tigres UANL"] = "Tigres UANL", ["Tigres"] = "Tigres UANL", ["UANL"] = "Tigres UANL",
        ["Toluca"] = "Toluca", ["Deportivo Toluca"] = "Toluca", ["TOL"] = "Toluca",
    };

    public static string GameKey(string sport, DateOnly date, string home, string away)
        => $"{sport.ToLowerInvariant()}:{date:yyyy-MM-dd}:{home}:{away}";
}
