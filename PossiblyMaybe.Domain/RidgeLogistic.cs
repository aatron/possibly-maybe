namespace PossiblyMaybe.Domain;

/// <summary>
/// Binary ridge-logistic via IRLS (no extra NuGet). Intercept (β0) is unpenalized.
/// shortcut: dense O(n p²) Newton; fine for NBA n and p≤3 — switch to SGD if p grows.
/// </summary>
public static class RidgeLogistic
{
    public static double Sigmoid(double z)
    {
        if (z >= 30) return 1.0;
        if (z <= -30) return 0.0;
        return 1.0 / (1.0 + Math.Exp(-z));
    }

    public static double Predict(ReadOnlySpan<double> beta, ReadOnlySpan<double> x)
        => Sigmoid(Dot(beta, x));

    /// <summary>Fit β with L2 penalty λ on coefficients 1..p-1. Optional warm start.</summary>
    public static double[] Fit(
        IReadOnlyList<double[]> x,
        IReadOnlyList<double> y,
        double lambda,
        double[]? warm = null,
        int maxIter = 20)
    {
        if (x.Count == 0)
            throw new ArgumentException("Need at least one row.", nameof(x));
        if (x.Count != y.Count)
            throw new ArgumentException("x/y length mismatch.");

        var p = x[0].Length;
        var beta = warm is { Length: var len } && len == p
            ? (double[])warm.Clone()
            : new double[p];

        var hess = new double[p, p];
        var rhs = new double[p];

        for (var iter = 0; iter < maxIter; iter++)
        {
            Array.Clear(hess, 0, hess.Length);
            Array.Clear(rhs, 0, p);

            for (var i = 0; i < x.Count; i++)
            {
                var xi = x[i];
                if (xi.Length != p)
                    throw new ArgumentException($"Row {i} has length {xi.Length}, expected {p}.");
                var eta = Dot(beta, xi);
                var mu = Sigmoid(eta);
                var w = Math.Max(mu * (1.0 - mu), 1e-6);
                var zi = eta + (y[i] - mu) / w;
                for (var j = 0; j < p; j++)
                {
                    rhs[j] += xi[j] * w * zi;
                    for (var k = 0; k < p; k++)
                        hess[j, k] += xi[j] * w * xi[k];
                }
            }

            for (var j = 1; j < p; j++)
                hess[j, j] += lambda;

            beta = SolveSymmetric(hess, rhs);
        }

        return beta;
    }

    private static double Dot(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        double s = 0;
        for (var i = 0; i < a.Length; i++)
            s += a[i] * b[i];
        return s;
    }

    /// <summary>Gaussian elimination on a dense system (p is tiny).</summary>
    private static double[] SolveSymmetric(double[,] a, double[] b)
    {
        var p = b.Length;
        var m = new double[p, p + 1];
        for (var i = 0; i < p; i++)
        {
            for (var j = 0; j < p; j++)
                m[i, j] = a[i, j];
            m[i, p] = b[i];
        }

        for (var col = 0; col < p; col++)
        {
            var pivot = col;
            for (var r = col + 1; r < p; r++)
            {
                if (Math.Abs(m[r, col]) > Math.Abs(m[pivot, col]))
                    pivot = r;
            }

            if (Math.Abs(m[pivot, col]) < 1e-12)
            {
                // singular — leave that coord; continue
                continue;
            }

            if (pivot != col)
            {
                for (var j = col; j <= p; j++)
                    (m[col, j], m[pivot, j]) = (m[pivot, j], m[col, j]);
            }

            var div = m[col, col];
            for (var j = col; j <= p; j++)
                m[col, j] /= div;

            for (var r = 0; r < p; r++)
            {
                if (r == col) continue;
                var f = m[r, col];
                for (var j = col; j <= p; j++)
                    m[r, j] -= f * m[col, j];
            }
        }

        var x = new double[p];
        for (var i = 0; i < p; i++)
            x[i] = m[i, p];
        return x;
    }
}
