namespace PossiblyMaybe.Domain;

/// <summary>
/// Walk-forward Platt on model residual: P(over)=σ(a + b·(modelExp−line)).
/// Fits only on past games; falls back to fixed-sharpness OverProb until MinFit.
/// </summary>
public sealed class OuPlattCalibrator
{
    public const int MinFit = 50;
    private readonly List<(double Edge, double Y)> _hist = [];
    private readonly double _fallbackSharpness;
    private double _a;
    private double _b;

    /// <param name="fallbackSharpness">Pre-fit OverProb sharpness (NBA≈0.25, soccer≈1.2).</param>
    public OuPlattCalibrator(double fallbackSharpness = 1.2)
    {
        _fallbackSharpness = fallbackSharpness;
        _b = fallbackSharpness;
    }

    public double OverProb(double modelExp, double line)
    {
        var edge = modelExp - line;
        if (_hist.Count < MinFit)
            return TotalsMath.OverProb(modelExp, line, sharpness: _fallbackSharpness);
        return Sigmoid(_a + _b * edge);
    }

    /// <summary>1 = over, -1 = under, 0 = push (ignored).</summary>
    public void Observe(double modelExp, double line, int ouResult)
    {
        if (ouResult == 0) return;
        _hist.Add((modelExp - line, ouResult > 0 ? 1.0 : 0.0));
        if (_hist.Count >= MinFit)
            Refit();
    }

    private void Refit()
    {
        // Newton on 2-param logistic + tiny ridge (stable when edges cluster near 0)
        const double ridge = 1e-2;
        double a = _a, b = _b;
        for (var iter = 0; iter < 25; iter++)
        {
            double gA = ridge * a, gB = ridge * b;
            double hAA = ridge, hBB = ridge, hAB = 0;
            foreach (var (edge, y) in _hist)
            {
                var p = Sigmoid(a + b * edge);
                var w = p * (1 - p);
                var r = y - p;
                gA += -r;
                gB += -r * edge;
                hAA += w;
                hBB += w * edge * edge;
                hAB += w * edge;
            }
            var det = hAA * hBB - hAB * hAB;
            if (Math.Abs(det) < 1e-12) break;
            var da = (hBB * gA - hAB * gB) / det;
            var db = (hAA * gB - hAB * gA) / det;
            a -= da;
            b -= db;
            if (Math.Abs(da) + Math.Abs(db) < 1e-8) break;
        }
        _a = a;
        _b = b;
    }

    private static double Sigmoid(double z)
    {
        if (z >= 30) return 1 - 1e-12;
        if (z <= -30) return 1e-12;
        return 1.0 / (1.0 + Math.Exp(-z));
    }
}
