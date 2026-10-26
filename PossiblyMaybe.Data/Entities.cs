namespace PossiblyMaybe.Data;

public enum PriceKind
{
    Open = 0,
    Close = 1,
}

public enum ClvQuality
{
    OpenClose = 0,
    CloseOnly = 1,
}

public enum Book
{
    Unknown = 0,
    Pinnacle = 1,
    Consensus = 2,
    DraftKings = 3,
    FanDuel = 4,
    Mgm = 5,
    Other = 99,
}

public sealed class Game
{
    public int Id { get; set; }
    public string Sport { get; set; } = "NBA";
    public string? Season { get; set; }
    /// <summary>Join key from Domain.TeamCodes.GameKey (nba:date:HOME:AWAY).</summary>
    public string SourceKey { get; set; } = "";
    /// <summary>ESPN event id when matched via espn_nba_schedules (for D officials).</summary>
    public string? EspnGameId { get; set; }
    public DateTimeOffset TipoffUtc { get; set; }
    public string HomeTeam { get; set; } = "";
    public string AwayTeam { get; set; } = "";
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }

    public List<OddsSnapshot> OddsSnapshots { get; set; } = [];
    public List<RefereeAssignment> RefereeAssignments { get; set; } = [];
}

public sealed class OddsSnapshot
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public Game? Game { get; set; }
    public Book Book { get; set; }
    public string Market { get; set; } = "ML";
    public string Side { get; set; } = "";
    public int AmericanOdds { get; set; }
    /// <summary>Home/away spread points (home negative if favored). Null on ML rows.</summary>
    public double? Line { get; set; }
    public PriceKind PriceKind { get; set; }
    public ClvQuality ClvQuality { get; set; }
    /// <summary>True when juice was missing and -110 was assumed.</summary>
    public bool AssumedJuice { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
}

public sealed class RefereeAssignment
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public Game? Game { get; set; }
    public string OfficialName { get; set; } = "";
    public string? Role { get; set; }
    /// <summary>Exclude from ref-effect fitting (e.g. Tim Donaghy era).</summary>
    public bool DonaghyExcludeFromFit { get; set; }
}

/// <summary>MLB starting pitchers + prior-season FIP approx (leakage-safe).</summary>
public sealed class GamePitching
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public string HomeSp { get; set; } = "";
    public string AwaySp { get; set; } = "";
    public double HomeFipPrior { get; set; }
    public double AwayFipPrior { get; set; }
}

/// <summary>NFL situational context from nflverse (roof/wind/rest).</summary>
public sealed class NflGameContext
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public string? Roof { get; set; }
    public double? WindMph { get; set; }
    public double? TempF { get; set; }
    public int HomeRest { get; set; }
    public int AwayRest { get; set; }
}

/// <summary>Team box inputs for pace / ORtg / DRtg (one row per game).</summary>
public sealed class GameBox
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public Game? Game { get; set; }
    public double FgaHome { get; set; }
    public double OrebHome { get; set; }
    public double TovHome { get; set; }
    public double FtaHome { get; set; }
    public double PtsHome { get; set; }
    public double Fg3aHome { get; set; }
    public double FgaAway { get; set; }
    public double OrebAway { get; set; }
    public double TovAway { get; set; }
    public double FtaAway { get; set; }
    public double PtsAway { get; set; }
    public double Fg3aAway { get; set; }
}

/// <summary>Paper/real ticket. Lives in paper.db (not the import warehouse).</summary>
public sealed class LedgerEntry
{
    public int Id { get; set; }
    public DateTimeOffset PlacedAt { get; set; }
    public string GameKey { get; set; } = "";
    public string Market { get; set; } = "SPREAD";
    /// <summary>HOME or AWAY (spread side).</summary>
    public string Side { get; set; } = "";
    /// <summary>Line on the side bet (home −3.5 or away +3.5).</summary>
    public double Line { get; set; }
    public int AmericanOdds { get; set; }
    public double StakeDollars { get; set; }
    public double ModelProb { get; set; }
    public double? CloseLine { get; set; }
    public int? CloseAmerican { get; set; }
    /// <summary>W, L, P, or null while open.</summary>
    public string? Result { get; set; }
    public double? PnL { get; set; }
    /// <summary>Spread CLV in points: betLine − closeLine (same side).</summary>
    public double? ClvPts { get; set; }
    public bool IsPaper { get; set; } = true;
}
