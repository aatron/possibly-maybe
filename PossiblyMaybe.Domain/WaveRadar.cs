namespace PossiblyMaybe.Domain;

/// <summary>
/// Settled-pick CLV observation for one league × market × variant cell.
/// Built behind this shape so A3 does not depend on A1 candidate-store schema.
/// </summary>
public readonly record struct ClvObservation(
    string League,
    string Market,
    string VariantId,
    DateTimeOffset At,
    double ClvPts);

public enum WaveState
{
    Off = 0,
    On = 1,
}

/// <summary>Promotion ladder stage. Paper→real is user-only (never auto).</summary>
public enum LadderStage
{
    Shadow = 0,
    Paper = 1,
    /// <summary>User flagged for real-money consideration; never auto-set by radar.</summary>
    RealCandidate = 2,
}

public sealed record WaveCellResult(
    string League,
    string Market,
    string VariantId,
    int N,
    double MeanClv,
    double RollingClv,
    double Cusum,
    int? CusumFirstAlarmIndex,
    double PValue,
    bool BhSignificant,
    WaveState Wave,
    LadderStage Stage,
    int WeeksOn,
    int NTrials,
    bool PromoteToPaperEligible,
    bool Demote);

/// <summary>
/// Wave radar: CUSUM on CLV residuals + Benjamini–Hochberg across cells + promotion ladder.
/// shortcut: normal-approx one-sided p (n≥MinN); swap to Student-t CDF if small-n cells matter.
/// </summary>
public static class WaveRadar
{
    public const double BhQ = 0.10;
    public const string DefaultMarket = "TOTAL";
    public const int MinN = 20;
    public const int RollingWindow = 40;
    /// <summary>CUSUM allowance (half of +2pt target shift).</summary>
    public const double CusumK = 1.0;
    /// <summary>CUSUM decision threshold (tuned for null FA ≤ 10% at σ≈3, n=80).</summary>
    public const double CusumH = 10.0;
    public const int PersistenceWeeks = 3;

    public static IReadOnlyList<WaveCellResult> Scan(
        IEnumerable<ClvObservation> observations,
        IReadOnlyDictionary<string, LadderStage>? priorStages = null,
        DateTimeOffset? asOf = null,
        double bhQ = BhQ)
    {
        var now = asOf ?? DateTimeOffset.UtcNow;
        var groups = observations
            .GroupBy(o => CellKey(o.League, o.Market, o.VariantId), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var ordered = g.OrderBy(x => x.At).ThenBy(x => x.ClvPts).ToList();
                var head = ordered[0];
                return (head.League, Market: string.IsNullOrEmpty(head.Market) ? DefaultMarket : head.Market,
                    head.VariantId, Series: ordered);
            })
            .OrderBy(x => x.League, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.VariantId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var nTrials = groups.Count;
        var raw = new List<(string League, string Market, string VariantId, int N, double Mean,
            double Rolling, double Cusum, int? AlarmIdx, double P, WaveState Wave, int WeeksOn,
            IReadOnlyList<ClvObservation> Series)>(groups.Count);

        foreach (var g in groups)
        {
            var clvs = g.Series.Select(s => s.ClvPts).ToList();
            var n = clvs.Count;
            var mean = n == 0 ? 0 : clvs.Average();
            var rolling = RollingMean(clvs, RollingWindow);
            var (cusum, alarmIdx, alarmed) = RunCusum(clvs, CusumK, CusumH);
            var p = n < MinN ? 1.0 : OneSidedMeanPositiveP(clvs);
            var wave = ResolveWave(alarmed, rolling, mean);
            var weeksOn = wave == WaveState.On ? WeeksSinceFirstAlarm(g.Series, alarmIdx, now) : 0;
            raw.Add((g.League, g.Market, g.VariantId, n, mean, rolling, cusum, alarmIdx, p, wave, weeksOn, g.Series));
        }

        var pValues = raw.Select(r => r.P).ToList();
        var reject = BenjaminiHochberg(pValues, bhQ);

        var results = new List<WaveCellResult>(raw.Count);
        for (var i = 0; i < raw.Count; i++)
        {
            var r = raw[i];
            var key = CellKey(r.League, r.Market, r.VariantId);
            var prior = LadderStage.Shadow;
            priorStages?.TryGetValue(key, out prior);
            // RealCandidate is sticky until user clears; never auto-grant.
            var stage = prior == LadderStage.RealCandidate
                ? LadderStage.RealCandidate
                : ApplyLadder(prior, r.Wave, reject[i], r.WeeksOn, r.Rolling);
            var demote = prior is LadderStage.Paper or LadderStage.RealCandidate
                         && (r.Wave == WaveState.Off || r.Rolling < 0);
            var promoteEligible = r.Wave == WaveState.On && reject[i] && r.WeeksOn >= PersistenceWeeks
                                  && prior == LadderStage.Shadow;
            results.Add(new WaveCellResult(
                r.League, r.Market, r.VariantId, r.N, r.Mean, r.Rolling, r.Cusum, r.AlarmIdx, r.P,
                reject[i], r.Wave, stage, r.WeeksOn, nTrials, promoteEligible, demote));
        }

        return results;
    }

    public static string CellKey(string league, string market, string variantId)
        => $"{league}|{(string.IsNullOrEmpty(market) ? DefaultMarket : market)}|{variantId}";

    /// <summary>One-sided CUSUM on residuals vs 0 with allowance k; alarm when S &gt; h.</summary>
    public static (double FinalS, int? FirstAlarmIndex, bool AlarmedNow) RunCusum(
        IReadOnlyList<double> clvPts, double k = CusumK, double h = CusumH)
    {
        double s = 0;
        int? first = null;
        for (var i = 0; i < clvPts.Count; i++)
        {
            s = Math.Max(0, s + clvPts[i] - k);
            if (s > h && first is null)
                first = i;
        }
        return (s, first, s > h);
    }

    /// <summary>BH FDR control: returns reject flags aligned with input p-values.</summary>
    public static IReadOnlyList<bool> BenjaminiHochberg(IReadOnlyList<double> pValues, double q = BhQ)
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
        // Normal upper-tail ≈ erfc(z/√2)/2
        return 0.5 * Erfc(z / Math.Sqrt(2.0));
    }

    static WaveState ResolveWave(bool cusumAlarmed, double rollingClv, double meanClv)
    {
        // Demotion path mirrors W7: negative rolling CLV kills the wave.
        if (rollingClv < 0)
            return WaveState.Off;
        if (cusumAlarmed && meanClv > 0)
            return WaveState.On;
        return WaveState.Off;
    }

    static LadderStage ApplyLadder(
        LadderStage prior, WaveState wave, bool bhSig, int weeksOn, double rollingClv)
    {
        if (prior == LadderStage.RealCandidate)
        {
            // Auto-demote real candidate on wave death / neg CLV; re-promote needs user again.
            if (wave == WaveState.Off || rollingClv < 0)
                return LadderStage.Shadow;
            return LadderStage.RealCandidate;
        }

        if (prior == LadderStage.Paper)
        {
            if (wave == WaveState.Off || rollingClv < 0)
                return LadderStage.Shadow;
            return LadderStage.Paper;
        }

        // Shadow → paper when wave + BH + persistence.
        if (wave == WaveState.On && bhSig && weeksOn >= PersistenceWeeks)
            return LadderStage.Paper;
        return LadderStage.Shadow;
    }

    static double RollingMean(IReadOnlyList<double> clvs, int window)
    {
        if (clvs.Count == 0)
            return 0;
        var take = Math.Min(window, clvs.Count);
        double sum = 0;
        for (var i = clvs.Count - take; i < clvs.Count; i++)
            sum += clvs[i];
        return sum / take;
    }

    static int WeeksSinceFirstAlarm(IReadOnlyList<ClvObservation> series, int? alarmIdx, DateTimeOffset now)
    {
        if (alarmIdx is null || alarmIdx.Value < 0 || alarmIdx.Value >= series.Count)
            return 0;
        var first = series[alarmIdx.Value].At;
        var days = (now - first).TotalDays;
        return days < 0 ? 0 : (int)(days / 7.0);
    }

    /// <summary>Complementary error function (Abramowitz–Stegun 7.1.26).</summary>
    static double Erfc(double x)
    {
        // erfc(-x) = 2 - erfc(x)
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
        var erfc = tau;
        return neg ? 2.0 - erfc : erfc;
    }
}
