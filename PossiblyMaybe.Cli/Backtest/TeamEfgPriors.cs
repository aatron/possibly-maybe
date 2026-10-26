using System.Globalization;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

/// <summary>
/// Prior-season team eFG% from nba_api LeagueDashTeamPtShot CSVs
/// (data/raw/nba_tracking/team_pt_shot_YYYY_YY.csv).
/// Game.Season end-year E uses file (E-2)_(E-1) — e.g. 2025 → 2023_24.
/// </summary>
public sealed class TeamEfgPriors
{
    private readonly Dictionary<string, SeasonBook> _byPriorKey = new(StringComparer.Ordinal);

    public static TeamEfgPriors Load(string trackingDir)
    {
        var priors = new TeamEfgPriors();
        if (!Directory.Exists(trackingDir))
            return priors;

        foreach (var path in Directory.EnumerateFiles(trackingDir, "team_pt_shot_*.csv"))
        {
            var name = Path.GetFileNameWithoutExtension(path); // team_pt_shot_2023_24
            var key = name.Replace("team_pt_shot_", "", StringComparison.Ordinal);
            var book = ReadSeason(path);
            if (book is not null)
                priors._byPriorKey[key] = book;
        }

        return priors;
    }

    public double AdjPts(string? seasonEndYear, string home, string away, double scale = EfgPriorMath.DefaultScale)
    {
        if (!int.TryParse(seasonEndYear, NumberStyles.Integer, CultureInfo.InvariantCulture, out var e))
            return 0;
        var key = EfgPriorMath.PriorFileKey(e);
        if (!_byPriorKey.TryGetValue(key, out var book))
            return 0;

        var efgH = book.TeamEfg.TryGetValue(home, out var h) ? h : book.LeagueEfg;
        var efgA = book.TeamEfg.TryGetValue(away, out var a) ? a : book.LeagueEfg;
        return EfgPriorMath.AdjPts(efgH, efgA, book.LeagueEfg, scale);
    }

    private static SeasonBook? ReadSeason(string path)
    {
        using var reader = new StreamReader(path);
        var header = reader.ReadLine();
        if (header is null) return null;
        var cols = header.Split(',');
        var iAbbr = Array.FindIndex(cols, c => c.Equals("TEAM_ABBREVIATION", StringComparison.OrdinalIgnoreCase));
        var iEfg = Array.FindIndex(cols, c => c.Equals("EFG_PCT", StringComparison.OrdinalIgnoreCase));
        if (iAbbr < 0 || iEfg < 0) return null;

        var team = new Dictionary<string, double>(StringComparer.Ordinal);
        var sum = 0.0;
        var n = 0;
        while (reader.ReadLine() is { } line)
        {
            var c = line.Split(',');
            if (c.Length <= Math.Max(iAbbr, iEfg)) continue;
            var abbr = TeamCodes.TryNormalize(c[iAbbr].Trim());
            if (abbr is null) continue;
            if (!double.TryParse(c[iEfg], NumberStyles.Float, CultureInfo.InvariantCulture, out var efg))
                continue;
            team[abbr] = efg;
            sum += efg;
            n++;
        }

        if (n == 0) return null;
        return new SeasonBook(team, sum / n);
    }

    private sealed record SeasonBook(Dictionary<string, double> TeamEfg, double LeagueEfg);
}
