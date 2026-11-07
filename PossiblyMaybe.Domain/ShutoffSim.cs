namespace PossiblyMaybe.Domain;

/// <summary>
/// Chronological flat-PnL walk with optional rolling-CLV pause.
/// Signal CLVs always advance (V3 stream); taken bets drive equity / drawdown.
/// </summary>
public static class ShutoffSim
{
    public static (double Pnl, double MaxDrawdown, int Taken) Run(
        IReadOnlyList<(double Clv, double Pnl)> signalBetsChrono,
        bool shutoff)
    {
        var window = new List<double>(Guards.ShutoffWindow);
        double equity = 0, peak = 0, maxDd = 0;
        var taken = 0;
        foreach (var (clv, pnl) in signalBetsChrono)
        {
            var paused = shutoff && Guards.LeaguePausedByRollingClv(window);
            if (!paused)
            {
                taken++;
                equity += pnl;
                if (equity > peak) peak = equity;
                var dd = peak - equity;
                if (dd > maxDd) maxDd = dd;
            }

            window.Add(clv);
            if (window.Count > Guards.ShutoffWindow)
                window.RemoveAt(0);
        }

        return (equity, maxDd, taken);
    }
}
