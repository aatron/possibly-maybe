using Microsoft.EntityFrameworkCore;
using PossiblyMaybe.Cli.Variants;
using PossiblyMaybe.Domain;

namespace PossiblyMaybe.Cli.Backtest;

public sealed record SeasonalityCell(
    string Sport,
    string PhaseId,
    int N,
    double MeanClv,
    double PValue,
    double BhAdjustedP,
    bool BhSignificant,
    int NTrials);

/// <summary>
/// Historical CLV by pre-registered season phase (A5). BH FDR q=0.10; prints N_trials.
/// Uses A1 shadow + candidate store; date from GameKey when candidate row missing.
/// </summary>
public static class SeasonalityScan
{
    public const double BhQ = 0.10;
    public const string SignalVariantId = Guards.ShutoffSignalVariantId; // V3
    public const int MinN = 20;

    public static async Task<IReadOnlyList<SeasonalityCell>> RunAsync(
        string variantsDbPath,
        bool writeBackPhase = true,
        TextWriter? output = null,
        CancellationToken ct = default)
    {
        var (db, _) = await VariantLedger.OpenAsync(variantsDbPath, ct);
        await using (db)
        {
            var shadows = await db.ShadowEntries.AsNoTracking()
                .Where(e => e.VariantId == SignalVariantId
                            && e.Result != null
                            && e.ClvPts != null
                            && e.SkipReason == null)
                .Select(e => new { e.Sport, e.GameKey, e.ClvPts })
                .ToListAsync(ct);

            var candByKey = await db.CandidateEntries.AsNoTracking()
                .Select(c => new { c.Sport, c.GameKey, c.Yyyymmdd })
                .ToListAsync(ct);
            var dayBySportKey = candByKey.ToDictionary(
                c => (c.Sport, c.GameKey),
                c => c.Yyyymmdd,
                new SportKeyComparer());

            var tagged = new List<(string Sport, string Phase, double Clv)>();
            var phaseBySportKey = new Dictionary<(string Sport, string GameKey), string>(new SportKeyComparer());

            foreach (var s in shadows)
            {
                if (!dayBySportKey.TryGetValue((s.Sport, s.GameKey), out var day)
                    && !SeasonalityPhases.TryParseGameKeyDay(s.GameKey, out day))
                    continue;
                var phase = SeasonalityPhases.Resolve(s.Sport, day);
                if (phase is null)
                    continue;
                tagged.Add((s.Sport, phase, s.ClvPts!.Value));
                phaseBySportKey[(s.Sport, s.GameKey)] = phase;
            }

            if (writeBackPhase && phaseBySportKey.Count > 0)
                await WriteBackPhasesAsync(db, phaseBySportKey, ct);

            var cells = Score(tagged);
            Print(cells, output ?? Console.Out);
            return cells;
        }
    }

    /// <summary>Pure core: group tagged CLVs → BH table. N_trials = cell count.</summary>
    public static IReadOnlyList<SeasonalityCell> Score(
        IEnumerable<(string Sport, string Phase, double Clv)> tagged)
    {
        var groups = tagged
            .GroupBy(t => (Sport: t.Sport.ToUpperInvariant(), t.Phase))
            .OrderBy(g => g.Key.Sport)
            .ThenBy(g => g.Key.Phase)
            .ToList();

        var nTrials = groups.Count;
        var raw = new List<(string Sport, string Phase, int N, double Mean, double P)>(groups.Count);
        foreach (var g in groups)
        {
            var clvs = g.Select(x => x.Clv).ToList();
            var n = clvs.Count;
            var mean = n == 0 ? 0 : clvs.Average();
            var p = n < MinN ? 1.0 : OneSidedMeanPositiveP(clvs);
            raw.Add((g.Key.Sport, g.Key.Phase, n, mean, p));
        }

        var adj = BhAdjustedPValues(raw.Select(r => r.P).ToList());
        var reject = BenjaminiHochbergReject(raw.Select(r => r.P).ToList(), BhQ);

        var cells = new List<SeasonalityCell>(raw.Count);
        for (var i = 0; i < raw.Count; i++)
        {
            var r = raw[i];
            cells.Add(new SeasonalityCell(
                r.Sport, r.Phase, r.N, r.Mean, r.P, adj[i], reject[i], nTrials));
        }
        return cells;
    }

    public static void Print(IReadOnlyList<SeasonalityCell> cells, TextWriter w)
    {
        var nTrials = cells.Count == 0 ? 0 : cells[0].NTrials;
        w.WriteLine($"seasonality-scan: N_trials={nTrials}  BH q={BhQ:0.00}  signal={SignalVariantId}  MinN={MinN}");
        w.WriteLine($"{"Sport",-8} {"Phase",-20} {"N",5} {"MeanClv",8} {"P",8} {"P_BH",8} {"BH",4}");
        foreach (var c in cells)
        {
            w.WriteLine(
                $"{c.Sport,-8} {c.PhaseId,-20} {c.N,5} {c.MeanClv,8:0.000} {c.PValue,8:0.0000} {c.BhAdjustedP,8:0.0000} {(c.BhSignificant ? "Y" : "."),4}");
        }
    }

    /// <summary>BH FDR reject flags (same rule as round-4 wave radar).</summary>
    public static IReadOnlyList<bool> BenjaminiHochbergReject(IReadOnlyList<double> pValues, double q = BhQ)
    {
        var m = pValues.Count;
        var reject = new bool[m];
        if (m == 0 || q <= 0)
            return reject;

        var order = Enumerable.Range(0, m).OrderBy(i => pValues[i]).ToArray();
        var maxK = -1;
        for (var rank = 1; rank <= m; rank++)
        {
            var idx = order[rank - 1];
            if (pValues[idx] <= (rank / (double)m) * q)
                maxK = rank;
        }
        for (var rank = 1; rank <= maxK; rank++)
            reject[order[rank - 1]] = true;
        return reject;
    }

    /// <summary>BH-adjusted p-values (step-up, right-to-left monotone).</summary>
    public static IReadOnlyList<double> BhAdjustedPValues(IReadOnlyList<double> pValues)
    {
        var m = pValues.Count;
        var adj = new double[m];
        if (m == 0)
            return adj;

        var order = Enumerable.Range(0, m).OrderBy(i => pValues[i]).ToArray();
        var running = 1.0;
        for (var rank = m; rank >= 1; rank--)
        {
            var idx = order[rank - 1];
            var cand = Math.Min(1.0, pValues[idx] * m / rank);
            running = Math.Min(running, cand);
            adj[idx] = running;
        }
        return adj;
    }

    public static double OneSidedMeanPositiveP(IReadOnlyList<double> x)
    {
        var n = x.Count;
        if (n < 2)
            return 1.0;
        var mean = x.Average();
        if (mean <= 0)
            return 1.0;
        double ss = 0;
        for (var i = 0; i < n; i++)
        {
            var d = x[i] - mean;
            ss += d * d;
        }
        var var = ss / (n - 1);
        if (var < 1e-18)
            return 0.0;
        var z = mean / Math.Sqrt(var / n);
        return 0.5 * Erfc(z / Math.Sqrt(2.0));
    }

    static async Task WriteBackPhasesAsync(
        VariantDbContext db,
        Dictionary<(string Sport, string GameKey), string> phaseBySportKey,
        CancellationToken ct)
    {
        var keys = phaseBySportKey.Keys.Select(k => k.GameKey).Distinct().ToList();
        var rows = await db.CandidateEntries
            .Where(c => keys.Contains(c.GameKey))
            .ToListAsync(ct);
        var n = 0;
        foreach (var row in rows)
        {
            if (!phaseBySportKey.TryGetValue((row.Sport, row.GameKey), out var phase))
                continue;
            if (row.SeasonPhase == phase)
                continue;
            row.SeasonPhase = phase;
            n++;
        }
        if (n > 0)
            await db.SaveChangesAsync(ct);
    }

    // Abramowitz–Stegun 7.1.26
    static double Erfc(double x)
    {
        var neg = x < 0;
        var z = Math.Abs(x);
        var t = 1.0 / (1.0 + 0.5 * z);
        var tau = t * Math.Exp(-z * z - 1.26551223
            + t * (1.00002368
            + t * (0.37409196
            + t * (0.09678418
            + t * (-0.18628806
            + t * (0.27886807
            + t * (-1.13520398
            + t * (1.48851587
            + t * (-0.82215223
            + t * 0.17087277)))))))));
        return neg ? 2.0 - tau : tau;
    }

    sealed class SportKeyComparer : IEqualityComparer<(string Sport, string GameKey)>
    {
        public bool Equals((string Sport, string GameKey) x, (string Sport, string GameKey) y)
            => x.Sport.Equals(y.Sport, StringComparison.OrdinalIgnoreCase)
               && x.GameKey.Equals(y.GameKey, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Sport, string GameKey) obj)
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Sport),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.GameKey));
    }
}
