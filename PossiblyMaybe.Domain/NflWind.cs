namespace PossiblyMaybe.Domain;

/// <summary>
/// Narrow NFL totals arm: wind extremes only (not a full EPA rebuild).
/// Industry under lever outdoors when wind ≥ threshold; dome/roof stays outside this arm.
/// </summary>
public static class NflWind
{
    public const string ArmId = "nfl-wind";
    public const double WindMphThreshold = 15;
    public const double UnderBumpPoints = 3.0;

    /// <summary>Subtract under bump when wind is at/above threshold; no-op if missing.</summary>
    public static double AdjustExpectedTotal(double modelExp, double? windMph)
        => windMph is >= WindMphThreshold ? modelExp - UnderBumpPoints : modelExp;

    public static bool IsExtreme(double? windMph) => windMph is >= WindMphThreshold;
}
