using System.Globalization;

namespace PossiblyMaybe.Domain;

/// <summary>Prior-year Savant 3yr runs index (100 = neutral). Missing team → 100.</summary>
public static class MlbParkFactors
{
    public static Dictionary<string, double> Load(string? path)
    {
        var map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return map;
        using var reader = new StreamReader(path);
        var header = reader.ReadLine() ?? "";
        var cols = header.Split(',');
        int I(string n) => Array.FindIndex(cols, c => c.Equals(n, StringComparison.OrdinalIgnoreCase));
        var iTeam = I("team");
        var iPf = I("pf_3yr");
        if (iTeam < 0 || iPf < 0) return map;
        while (reader.ReadLine() is { } line)
        {
            var c = line.Split(',');
            if (c.Length <= Math.Max(iTeam, iPf)) continue;
            if (double.TryParse(c[iPf], CultureInfo.InvariantCulture, out var pf))
                map[c[iTeam].Trim()] = pf;
        }
        return map;
    }

    public static double Scale(double modelExp, string homeTeam, IReadOnlyDictionary<string, double> park)
        => modelExp * (park.TryGetValue(homeTeam, out var pf) ? pf : 100.0) / 100.0;
}
