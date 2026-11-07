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
    // Normalized (lowercase, no diacritics/punctuation; CamelCase split). Short codes are sport-scoped; names apply to every sport.
    private static readonly HashSet<string> Names = new()
    {
        "portland", "portland trail blazers", "trail blazers", "blazers",
        "portland timbers", "timbers", "portland thorns", "portland thorns fc", "thorns",
        "oregon", "oregon ducks", "ducks", "oregon state", "oregon st", "oregon state beavers", "beavers",
        "portland state", "portland state vikings", "portland st",
        // University of Portland Pilots (SBRO: PortlandU / Portland U)
        "university of portland", "portland pilots", "portland u", "pilots",
        "u of portland", "portland university", "portland univ",
        // Formal university names
        "university of oregon", "oregon state university", "oregon st beavers", "portland state university",
        // Other Oregon pro / minor-league teams. No bare "fire" (Chicago Fire) or "vikings" (Minnesota Vikings).
        "portland fire", "portland winterhawks", "winterhawks", "hillsboro hops", "eugene emeralds",
        "portland pickles",
    };

    // NCAA: ore=Oregon, orst=Oregon State, port=Portland State, up/portlandu=Univ. of Portland.
    // Do not use "psu" — that is Penn State in ESPN/GetSTAM (over-exclusion).
    private static readonly Dictionary<string, string[]> CodesBySport = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NBA"] = ["por"],
        ["MLS"] = ["por"],
        ["NWSL"] = ["por"],
        ["WNBA"] = ["por"],
        ["NCAAB"] = ["ore", "orst", "port", "up", "portlandu"],
        ["NCAAF"] = ["ore", "orst", "port", "up", "portlandu"],
        ["NCAA"] = ["ore", "orst", "port", "up", "portlandu"],
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
        char prev = '\0';
        foreach (var c in s.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c))
            {
                // SBRO-style CamelCase ("OregonState") → "oregon state"
                if (char.IsUpper(c) && prev != '\0' && (char.IsLower(prev) || char.IsDigit(prev)))
                    sb.Append(' ');
                sb.Append(char.ToLowerInvariant(c));
                prev = c;
            }
            else
            {
                sb.Append(' ');
                prev = '\0';
            }
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
