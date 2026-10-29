using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

public static class EspnPlayerBoxLoader
{
    public static Dictionary<string, List<AbsenceBook.PlayerLine>> LoadByEspnGameId(string csvPath)
    {
        var map = new Dictionary<string, List<AbsenceBook.PlayerLine>>(StringComparer.Ordinal);
        if (!File.Exists(csvPath))
            return map;

        using var sr = new StreamReader(csvPath);
        var header = sr.ReadLine();
        if (header is null) return map;
        var cols = header.Split(',');
        int Ix(string name) => Array.FindIndex(cols, c => c.Equals(name, StringComparison.OrdinalIgnoreCase));
        var iEid = Ix("espn_game_id");
        var iTeam = Ix("team");
        var iPid = Ix("player_id");
        var iMin = Ix("minutes");
        var iPts = Ix("pts");
        var iDnp = Ix("dnp");
        if (iEid < 0 || iPid < 0 || iMin < 0 || iPts < 0 || iDnp < 0)
            throw new InvalidOperationException($"Unexpected player box header in {csvPath}");

        while (sr.ReadLine() is { } line)
        {
            var p = SplitCsv(line);
            if (p.Length <= Math.Max(iEid, Math.Max(iPid, Math.Max(iMin, Math.Max(iPts, iDnp)))))
                continue;
            var eid = p[iEid];
            if (string.IsNullOrEmpty(eid)) continue;
            if (!map.TryGetValue(eid, out var list))
            {
                list = [];
                map[eid] = list;
            }

            list.Add(new AbsenceBook.PlayerLine(
                p[iPid],
                iTeam >= 0 ? p[iTeam] : "",
                double.TryParse(p[iMin], out var mn) ? mn : 0,
                double.TryParse(p[iPts], out var pts) ? pts : 0,
                p[iDnp] is "1" or "true" or "True"));
        }

        return map;
    }

    static string[] SplitCsv(string line)
    {
        // Simple splitter; our export does not quote commas in critical fields.
        return line.Split(',');
    }
}
