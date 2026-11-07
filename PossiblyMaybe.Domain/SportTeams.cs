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
        // flancast / SBRO nicknames
        ["Bears"] = "CHI", ["Bengals"] = "CIN", ["Bills"] = "BUF", ["Broncos"] = "DEN",
        ["Browns"] = "CLE", ["Buccaneers"] = "TB", ["Cardinals"] = "ARI", ["Chargers"] = "LAC",
        ["Chiefs"] = "KC", ["Colts"] = "IND", ["Commanders"] = "WAS", ["Cowboys"] = "DAL",
        ["Dolphins"] = "MIA", ["Eagles"] = "PHI", ["Falcons"] = "ATL", ["Fortyniners"] = "SF",
        ["Giants"] = "NYG", ["Jaguars"] = "JAX", ["Jets"] = "NYJ", ["Lions"] = "DET",
        ["Packers"] = "GB", ["Panthers"] = "CAR", ["Patriots"] = "NE", ["Raiders"] = "LV",
        ["Rams"] = "LAR", ["Ravens"] = "BAL", ["Saints"] = "NO", ["Seahawks"] = "SEA",
        ["Steelers"] = "PIT", ["Texans"] = "HOU", ["Titans"] = "TEN", ["Vikings"] = "MIN",
        ["BuffaloBills"] = "BUF", ["KCChiefs"] = "KC", ["LVRaiders"] = "LV", ["Oakland"] = "LV",
        ["SanDiego"] = "LAC", ["St.Louis"] = "LAR", ["Tampa"] = "TB", ["Washingtom"] = "WAS",
        // SBRO city / CamelCase
        ["Arizona"] = "ARI", ["Atlanta"] = "ATL", ["Baltimore"] = "BAL", ["Buffalo"] = "BUF",
        ["Carolina"] = "CAR", ["Chicago"] = "CHI", ["Cincinnati"] = "CIN", ["Cleveland"] = "CLE",
        ["Dallas"] = "DAL", ["Denver"] = "DEN", ["Detroit"] = "DET",
        ["Green Bay"] = "GB", ["GreenBay"] = "GB",
        ["Houston"] = "HOU", ["Houston Texans"] = "HOU", ["HoustonTexans"] = "HOU",
        ["Indianapolis"] = "IND", ["Jacksonville"] = "JAX",
        ["Kansas City"] = "KC", ["KansasCity"] = "KC", ["Kansas"] = "KC",
        ["Miami"] = "MIA", ["Minnesota"] = "MIN",
        ["New England"] = "NE", ["NewEngland"] = "NE",
        ["New Orleans"] = "NO", ["NewOrleans"] = "NO",
        ["NY Giants"] = "NYG", ["NYGiants"] = "NYG", ["NY Jets"] = "NYJ", ["NYJets"] = "NYJ",
        ["Philadelphia"] = "PHI", ["Pittsburgh"] = "PIT",
        ["San Diego"] = "LAC", ["San Francisco"] = "SF", ["SanFrancisco"] = "SF",
        ["Seattle"] = "SEA", ["St. Louis"] = "LAR",
        ["Tampa Bay"] = "TB", ["TampaBay"] = "TB",
        ["Tennessee"] = "TEN", ["Washington"] = "WAS",
        ["LA Rams"] = "LAR", ["LARams"] = "LAR", ["LA Chargers"] = "LAC", ["LAChargers"] = "LAC",
        ["Las Vegas"] = "LV", ["LasVegas"] = "LV",
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
        // flancast / SBRO nicknames + short junk codes
        ["Angels"] = "LAA", ["Astros"] = "HOU", ["Blue Jays"] = "TOR", ["Braves"] = "ATL",
        ["Brewers"] = "MIL", ["Cardinals"] = "STL", ["Cubs"] = "CHC", ["Diamondbacks"] = "ARI",
        ["Dodgers"] = "LAD", ["Indians"] = "CLE", ["Guardians"] = "CLE", ["Mariners"] = "SEA",
        ["Marlins"] = "MIA", ["Mets"] = "NYM", ["Nationals"] = "WSH", ["Orioles"] = "BAL",
        ["Phillies"] = "PHI", ["Pirates"] = "PIT", ["Rangers"] = "TEX", ["Red Sox"] = "BOS",
        ["Reds"] = "CIN", ["Rockies"] = "COL", ["Tigers"] = "DET", ["Twins"] = "MIN",
        ["White Sox"] = "CWS", ["Yankees"] = "NYY", ["Rays"] = "TB",
        ["TAM"] = "TB", ["CUB"] = "CHC", ["BRS"] = "BOS", ["KAN"] = "KC",
        ["SDG"] = "SD", ["SFG"] = "SF", ["SFO"] = "SF", ["LOS"] = "LAD",
        ["ARI"] = "ARI", ["ATL"] = "ATL", ["BAL"] = "BAL", ["BOS"] = "BOS", ["CHC"] = "CHC",
        ["CWS"] = "CWS", ["CIN"] = "CIN", ["CLE"] = "CLE", ["COL"] = "COL", ["DET"] = "DET",
        ["HOU"] = "HOU", ["KC"] = "KC", ["LAA"] = "LAA", ["LAD"] = "LAD", ["MIA"] = "MIA",
        ["MIL"] = "MIL", ["MIN"] = "MIN", ["NYM"] = "NYM", ["NYY"] = "NYY", ["OAK"] = "OAK",
        ["PHI"] = "PHI", ["PIT"] = "PIT", ["SD"] = "SD", ["SF"] = "SF", ["SEA"] = "SEA",
        ["STL"] = "STL", ["TB"] = "TB", ["TEX"] = "TEX", ["TOR"] = "TOR", ["WSH"] = "WSH",
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

    /// <summary>NHL: ESPN abbr / full name → short code. Unmapped clubs keep their own name via ResolveNhl.</summary>
    public static string? TryNhl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var t = raw.Trim();
        return Nhl.TryGetValue(t, out var c) ? c : null;
    }

    /// <summary>
    /// Canonical NHL team code from ESPN fields. Names win over abbreviations; unknown clubs keep
    /// their own display name (never aliased to another club). Winterhawks stay excluded via OregonExclusion.
    /// </summary>
    public static string? ResolveNhl(string? abbr, string? display, string? shortName)
    {
        var hit = TryNhl(display) ?? TryNhl(shortName);
        if (hit is not null) return hit;
        if (string.IsNullOrWhiteSpace(display)) return TryNhl(abbr) ?? abbr;
        return TryNhl(abbr) ?? display.Trim();
    }

    private static readonly Dictionary<string, string> Nhl = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Anaheim Ducks"] = "ANA", ["Ducks"] = "ANA", ["ANA"] = "ANA",
        ["Boston Bruins"] = "BOS", ["Bruins"] = "BOS", ["BOS"] = "BOS",
        ["Buffalo Sabres"] = "BUF", ["Sabres"] = "BUF", ["BUF"] = "BUF",
        ["Calgary Flames"] = "CGY", ["Flames"] = "CGY", ["CGY"] = "CGY",
        ["Carolina Hurricanes"] = "CAR", ["Hurricanes"] = "CAR", ["CAR"] = "CAR",
        ["Chicago Blackhawks"] = "CHI", ["Blackhawks"] = "CHI", ["CHI"] = "CHI",
        ["Colorado Avalanche"] = "COL", ["Avalanche"] = "COL", ["COL"] = "COL",
        ["Columbus Blue Jackets"] = "CBJ", ["Blue Jackets"] = "CBJ", ["CBJ"] = "CBJ",
        ["Dallas Stars"] = "DAL", ["Stars"] = "DAL", ["DAL"] = "DAL",
        ["Detroit Red Wings"] = "DET", ["Red Wings"] = "DET", ["DET"] = "DET",
        ["Edmonton Oilers"] = "EDM", ["Oilers"] = "EDM", ["EDM"] = "EDM",
        ["Florida Panthers"] = "FLA", ["Panthers"] = "FLA", ["FLA"] = "FLA",
        ["Los Angeles Kings"] = "LA", ["LA Kings"] = "LA", ["Kings"] = "LA", ["LA"] = "LA", ["LAK"] = "LA",
        ["Minnesota Wild"] = "MIN", ["Wild"] = "MIN", ["MIN"] = "MIN",
        ["Montreal Canadiens"] = "MTL", ["Montréal Canadiens"] = "MTL", ["Canadiens"] = "MTL", ["MTL"] = "MTL",
        ["Nashville Predators"] = "NSH", ["Predators"] = "NSH", ["NSH"] = "NSH",
        ["New Jersey Devils"] = "NJ", ["Devils"] = "NJ", ["NJ"] = "NJ", ["NJD"] = "NJ",
        ["New York Islanders"] = "NYI", ["Islanders"] = "NYI", ["NYI"] = "NYI",
        ["New York Rangers"] = "NYR", ["Rangers"] = "NYR", ["NYR"] = "NYR",
        ["Ottawa Senators"] = "OTT", ["Senators"] = "OTT", ["OTT"] = "OTT",
        ["Philadelphia Flyers"] = "PHI", ["Flyers"] = "PHI", ["PHI"] = "PHI",
        ["Pittsburgh Penguins"] = "PIT", ["Penguins"] = "PIT", ["PIT"] = "PIT",
        ["San Jose Sharks"] = "SJ", ["Sharks"] = "SJ", ["SJ"] = "SJ", ["SJS"] = "SJ",
        ["Seattle Kraken"] = "SEA", ["Kraken"] = "SEA", ["SEA"] = "SEA",
        ["St. Louis Blues"] = "STL", ["St Louis Blues"] = "STL", ["Blues"] = "STL", ["STL"] = "STL",
        ["Tampa Bay Lightning"] = "TB", ["Lightning"] = "TB", ["TB"] = "TB", ["TBL"] = "TB",
        ["Toronto Maple Leafs"] = "TOR", ["Maple Leafs"] = "TOR", ["TOR"] = "TOR",
        ["Utah Hockey Club"] = "UTA", ["Utah"] = "UTA", ["UTA"] = "UTA",
        ["Vancouver Canucks"] = "VAN", ["Canucks"] = "VAN", ["VAN"] = "VAN",
        ["Vegas Golden Knights"] = "VGK", ["Golden Knights"] = "VGK", ["VGK"] = "VGK",
        ["Winnipeg Jets"] = "WPG", ["Jets"] = "WPG", ["WPG"] = "WPG",
        ["Washington Capitals"] = "WSH", ["Capitals"] = "WSH", ["WSH"] = "WSH", ["WAS"] = "WSH",
    };

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
        ["Philadelphia Union"] = "PHI", ["Portland Timbers"] = "POR", ["Portland Timbers FC"] = "POR",
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

    /// <summary>Map ESPN / common names → warehouse EPL (Premier League) team strings.</summary>
    public static string? TryEpl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var t = raw.Trim();
        return Epl.TryGetValue(t, out var c) ? c : null;
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
        ["Metz"] = "Metz", ["FC Metz"] = "Metz",
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
        ["Le Mans"] = "Le Mans", ["Lorient"] = "Lorient", ["Montpellier"] = "Montpellier", ["Clermont"] = "Clermont",
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
        ["Atlante"] = "Atlante", ["Atlas"] = "Atlas", ["Atlas FC"] = "Atlas", ["ATS"] = "Atlas",
        ["Atletico San Luis"] = "Atletico San Luis", ["Atlético San Luis"] = "Atletico San Luis", ["Atlético de San Luis"] = "Atletico San Luis", ["Atl. San Luis"] = "Atletico San Luis",
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
        ["Pumas U.N.A.M."] = "Pumas U.N.A.M.", ["Pumas UNAM"] = "Pumas U.N.A.M.", ["Pumas"] = "Pumas U.N.A.M.", ["UNAM"] = "Pumas U.N.A.M.",
        ["Queretaro FC"] = "Queretaro FC", ["Querétaro"] = "Queretaro FC", ["Queretaro"] = "Queretaro FC", ["QRO"] = "Queretaro FC",
        ["Santos Laguna"] = "Santos Laguna", ["Santos"] = "Santos Laguna", ["SAN"] = "Santos Laguna",
        ["Tigres UANL"] = "Tigres UANL", ["Tigres"] = "Tigres UANL", ["UANL"] = "Tigres UANL",
        ["Toluca"] = "Toluca", ["Deportivo Toluca"] = "Toluca", ["TOL"] = "Toluca",
    };

    private static readonly Dictionary<string, string> Epl = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AFC Bournemouth"] = "AFC Bournemouth", ["Bournemouth"] = "AFC Bournemouth", ["BOU"] = "AFC Bournemouth",
        ["Arsenal"] = "Arsenal", ["ARS"] = "Arsenal",
        ["Aston Villa"] = "Aston Villa", ["Villa"] = "Aston Villa", ["AVL"] = "Aston Villa",
        ["Brentford"] = "Brentford", ["BRE"] = "Brentford",
        ["Brighton Hove Albion"] = "Brighton Hove Albion",
        ["Brighton & Hove Albion"] = "Brighton Hove Albion", ["Brighton"] = "Brighton Hove Albion", ["BHA"] = "Brighton Hove Albion",
        ["Burnley"] = "Burnley", ["BUR"] = "Burnley",
        ["Cardiff City"] = "Cardiff City", ["Cardiff"] = "Cardiff City",
        ["Chelsea"] = "Chelsea", ["CHE"] = "Chelsea",
        ["Crystal Palace"] = "Crystal Palace", ["Palace"] = "Crystal Palace", ["CRY"] = "Crystal Palace",
        ["Everton"] = "Everton", ["EVE"] = "Everton",
        ["Fulham"] = "Fulham", ["FUL"] = "Fulham",
        ["Huddersfield Town"] = "Huddersfield Town", ["Huddersfield"] = "Huddersfield Town",
        ["Ipswich Town"] = "Ipswich Town", ["Ipswich"] = "Ipswich Town", ["IPS"] = "Ipswich Town",
        ["Leeds United"] = "Leeds United", ["Leeds"] = "Leeds United", ["LEE"] = "Leeds United",
        ["Leicester City"] = "Leicester City", ["Leicester"] = "Leicester City", ["LEI"] = "Leicester City",
        ["Liverpool"] = "Liverpool", ["LIV"] = "Liverpool",
        ["Luton Town"] = "Luton Town", ["Luton"] = "Luton Town",
        ["Manchester City"] = "Manchester City", ["Man City"] = "Manchester City", ["MCI"] = "Manchester City",
        ["Manchester United"] = "Manchester United", ["Man United"] = "Manchester United",
        ["Man Utd"] = "Manchester United", ["MUN"] = "Manchester United",
        ["Newcastle United"] = "Newcastle United", ["Newcastle"] = "Newcastle United", ["NEW"] = "Newcastle United",
        ["Norwich City"] = "Norwich City", ["Norwich"] = "Norwich City",
        ["Nottingham Forest"] = "Nottingham Forest", ["Nott'm Forest"] = "Nottingham Forest",
        ["Nott Forest"] = "Nottingham Forest", ["NFO"] = "Nottingham Forest",
        ["Sheffield United"] = "Sheffield United", ["Sheffield Utd"] = "Sheffield United", ["SHU"] = "Sheffield United",
        ["Southampton"] = "Southampton", ["SOU"] = "Southampton",
        ["Sunderland A.F.C"] = "Sunderland A.F.C", ["Sunderland"] = "Sunderland A.F.C", ["Sunderland AFC"] = "Sunderland A.F.C",
        ["Tottenham Hotspur"] = "Tottenham Hotspur", ["Tottenham"] = "Tottenham Hotspur",
        ["Spurs"] = "Tottenham Hotspur", ["TOT"] = "Tottenham Hotspur",
        ["Watford"] = "Watford", ["WAT"] = "Watford",
        ["West Bromwich(WBA)"] = "West Bromwich(WBA)",
        ["West Bromwich Albion"] = "West Bromwich(WBA)", ["West Brom"] = "West Bromwich(WBA)", ["WBA"] = "West Bromwich(WBA)",
        ["West Ham United"] = "West Ham United", ["West Ham"] = "West Ham United", ["WHU"] = "West Ham United",
        ["Wolves"] = "Wolves", ["Wolverhampton Wanderers"] = "Wolves", ["Wolverhampton"] = "Wolves", ["WOL"] = "Wolves",
    };

    /// <summary>
    /// Canonical soccer team name from ESPN fields. Names win over abbreviations (ESPN reuses/changes codes, e.g. Le Mans = MNS
    /// once collided with Metz); a club not in the table keeps its own display name (new team, default rating) and is never
    /// aliased to another club.
    /// </summary>
    public static string? ResolveSoccer(string sport, string? abbr, string? display, string? shortName)
    {
        Func<string?, string?> map = sport.ToUpperInvariant() switch
        {
            "MLS" => TryMls, "LIGUE1" => TryLigue1, "SERIEA" => TrySerieA, "LIGAMX" => TryLigaMx,
            "EPL" => TryEpl, _ => _ => null,
        };
        var hit = map(display) ?? map(shortName);
        if (hit is not null) return hit;
        if (string.IsNullOrWhiteSpace(display)) return map(abbr) ?? abbr;
        return display.Trim();
    }

    public static string GameKey(string sport, DateOnly date, string home, string away)
        => $"{sport.ToLowerInvariant()}:{date:yyyy-MM-dd}:{home}:{away}";

    /// <summary>
    /// Minimal NCAA school → short code for GameKey. Oregon spellings map to exclusion codes;
    /// everything else gets a stable sanitized token from the SBRO/ESPN name.
    /// Unmapped schools never resolve onto another school's code (only Oregon cardinal aliases are listed).
    /// </summary>
    public static string NcaaCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "unk";
        var spaced = SplitCamel(raw.Trim());
        if (Ncaa.TryGetValue(spaced, out var code) || Ncaa.TryGetValue(raw.Trim(), out code))
            return code;
        return SanitizeNcaa(spaced);
    }

    /// <summary>Known NCAA alias → code, or null (never invent a hit onto another club).</summary>
    public static string? TryNcaa(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var spaced = SplitCamel(raw.Trim());
        if (Ncaa.TryGetValue(spaced, out var code) || Ncaa.TryGetValue(raw.Trim(), out code))
            return code;
        return null;
    }

    /// <summary>ESPN / display → warehouse NCAAB code. Unknown clubs keep their own sanitized name.</summary>
    public static string ResolveNcaa(string? abbr, string? display, string? shortName)
    {
        var hit = TryNcaa(display) ?? TryNcaa(shortName) ?? TryNcaa(abbr);
        if (hit is not null) return hit;
        if (!string.IsNullOrWhiteSpace(display)) return NcaaCode(display);
        if (!string.IsNullOrWhiteSpace(shortName)) return NcaaCode(shortName);
        return NcaaCode(abbr);
    }

    static string SplitCamel(string raw)
    {
        var sb = new System.Text.StringBuilder(raw.Length + 4);
        char prev = '\0';
        foreach (var ch in raw)
        {
            if (char.IsUpper(ch) && prev != '\0' && (char.IsLower(prev) || char.IsDigit(prev)))
                sb.Append(' ');
            sb.Append(ch);
            prev = ch;
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    static string SanitizeNcaa(string spaced)
    {
        var sb = new System.Text.StringBuilder(spaced.Length);
        foreach (var ch in spaced.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        }
        var s = sb.ToString();
        return s.Length == 0 ? "unk" : s.Length <= 16 ? s : s[..16];
    }

    // Only Oregon-cardinal aliases. Do not add other schools here — SanitizeNcaa keeps them distinct.
    private static readonly Dictionary<string, string> Ncaa = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Oregon"] = "ore", ["Oregon Ducks"] = "ore", ["Ducks"] = "ore",
        ["University of Oregon"] = "ore", ["ORE"] = "ore",
        ["Oregon State"] = "orst", ["OregonState"] = "orst", ["Oregon St"] = "orst",
        ["Oregon St."] = "orst", ["Oregon State Beavers"] = "orst", ["Beavers"] = "orst",
        ["Oregon State University"] = "orst", ["ORST"] = "orst",
        ["Portland State"] = "port", ["PortlandState"] = "port", ["Portland St"] = "port",
        ["Portland St."] = "port", ["Portland State Vikings"] = "port",
        ["Portland State University"] = "port", ["PORT"] = "port",
        ["Oregon St."] = "orst", ["OregonSt"] = "orst", ["Oregon State Beavers"] = "orst",
        ["Oregon St Beavers"] = "orst", ["Beavers"] = "orst",
        ["Portland State"] = "port", ["PortlandState"] = "port", ["Portland St"] = "port",
        ["Portland St."] = "port", ["PortlandSt"] = "port", ["Portland State Vikings"] = "port",
        ["Portland U"] = "up", ["PortlandU"] = "up", ["University of Portland"] = "up",
        ["Portland Pilots"] = "up", ["Pilots"] = "up", ["Portlandu"] = "up",
        ["U of Portland"] = "up", ["UP"] = "up", ["PORTLANDU"] = "up",
    };
}
