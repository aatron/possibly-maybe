using System.Globalization;
using System.Text;

namespace PossiblyMaybe.Domain;

/// <summary>
/// Cardinal rule: never bet on an Oregon-based team (either side of the game, any sport, any venue).
/// Hard rule, not a model parameter. Whole-string match on a normalized name/code, so "Ohio State",
/// "Oklahoma State" and bare "OSU" are NOT excluded.
/// </summary>
public static class OregonExclusion
{
    // Normalized (lowercase, no diacritics/punctuation). Short codes are sport-scoped; names apply to every sport.
    private static readonly HashSet<string> Names = new()
    {
        "portland", "portland trail blazers", "trail blazers", "blazers",
        "portland timbers", "timbers", "portland thorns", "portland thorns fc", "thorns",
        "oregon", "oregon ducks", "ducks", "oregon state", "oregon st", "oregon state beavers", "beavers",
        "portland state", "portland state vikings", "portland st",
    };

    private static readonly Dictionary<string, string[]> CodesBySport = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NBA"] = ["por"],
        ["MLS"] = ["por"],
        ["NWSL"] = ["por"],
        ["NCAAB"] = ["ore", "orst", "psu"],
        ["NCAAF"] = ["ore", "orst", "psu"],
        ["NCAA"] = ["ore", "orst", "psu"],
    };

    public static bool IsExcluded(string? sport, string? team)
    {
        var n = Normalize(team);
        if (n.Length == 0) return false;
        if (Names.Contains(n)) return true;
        // Unknown sport (e.g. legacy paper-bet): fail closed, test every sport's codes.
        return sport is null
            ? CodesBySport.Values.Any(c => c.Contains(n))
            : CodesBySport.TryGetValue(sport, out var codes) && codes.Contains(n);
    }

    public static bool IsExcluded(string? sport, string? home, string? away)
        => IsExcluded(sport, home) || IsExcluded(sport, away);

    /// <summary>Parse "[yyyymmdd ]AWAY@HOME" and test both sides.</summary>
    public static bool IsExcludedGameKey(string? sport, string gameKey)
    {
        var s = gameKey.Trim();
        var sp = s.IndexOf(' ');
        if (sp > 0 && s[..sp].All(char.IsDigit)) s = s[(sp + 1)..];
        var at = s.IndexOf('@');
        return at > 0 ? IsExcluded(sport, s[(at + 1)..], s[..at]) : IsExcluded(sport, s);
    }

    private static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var sb = new StringBuilder();
        foreach (var c in s.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
