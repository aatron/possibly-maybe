namespace PossiblyMaybe.Domain;

/// <summary>
/// Walk-forward player production + tip-time DNP adjustment for totals.
/// Uses same-game DNP/inactive flags (availability card), never post-game minutes of active players for the decision.
/// </summary>
public sealed class AbsenceBook
{
    readonly Dictionary<string, PlayerState> _players = new(StringComparer.Ordinal);
    readonly double _alpha;
    readonly double _minMpg;
    readonly int _minPriorGames;

    public AbsenceBook(double alpha = 0.2, double minMpg = 18, int minPriorGames = 5)
    {
        _alpha = alpha;
        _minMpg = minMpg;
        _minPriorGames = minPriorGames;
    }

    /// <summary>Points to subtract from pace expected total given tonight's DNPs (both teams).</summary>
    public double MissingPtsAdjustment(IEnumerable<PlayerLine> tipLines, double scale)
    {
        if (scale == 0) return 0;
        double missing = 0;
        foreach (var line in tipLines)
        {
            if (!line.DidNotPlay) continue;
            if (string.IsNullOrEmpty(line.PlayerId)) continue;
            if (!_players.TryGetValue(line.PlayerId, out var st)) continue;
            if (st.N < _minPriorGames || st.Mpg < _minMpg) continue;
            missing += st.Ppg;
        }

        return -scale * missing;
    }

    /// <summary>Leakage-safe: call AFTER the betting decision for this tip.</summary>
    public void UpdateAfterGame(IEnumerable<PlayerLine> lines)
    {
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line.PlayerId)) continue;
            if (line.DidNotPlay || line.Minutes <= 0) continue;

            if (!_players.TryGetValue(line.PlayerId, out var st))
                st = new PlayerState();

            if (st.N == 0)
            {
                st.Ppg = line.Pts;
                st.Mpg = line.Minutes;
                st.N = 1;
            }
            else
            {
                st.Ppg = (1 - _alpha) * st.Ppg + _alpha * line.Pts;
                st.Mpg = (1 - _alpha) * st.Mpg + _alpha * line.Minutes;
                st.N++;
            }

            _players[line.PlayerId] = st;
        }
    }

    public readonly record struct PlayerLine(
        string PlayerId,
        string Team,
        double Minutes,
        double Pts,
        bool DidNotPlay);

    struct PlayerState
    {
        public double Ppg;
        public double Mpg;
        public int N;
    }
}
