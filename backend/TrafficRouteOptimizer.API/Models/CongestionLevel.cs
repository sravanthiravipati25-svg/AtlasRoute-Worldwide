namespace TrafficRouteOptimizer.API.Models;

public enum CongestionLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
    Severe = 3
}

public static class CongestionLevelExtensions
{
    /// <summary>
    /// Multiplier applied to a road segment's base travel time to reflect
    /// real-world slowdown caused by traffic congestion.
    /// </summary>
    public static double TimeMultiplier(this CongestionLevel level) => level switch
    {
        CongestionLevel.Low => 1.0,
        CongestionLevel.Medium => 1.3,
        CongestionLevel.High => 1.75,
        CongestionLevel.Severe => 2.5,
        _ => 1.0
    };
}
