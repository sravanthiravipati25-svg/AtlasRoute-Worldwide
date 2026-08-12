namespace TrafficRouteOptimizer.API.Models;

/// <summary>
/// A directed edge in the road graph connecting two locations.
/// Bidirectional roads are represented as two RoadSegment rows.
/// </summary>
public class RoadSegment
{
    public int Id { get; set; }

    public int FromLocationId { get; set; }
    public Location FromLocation { get; set; } = null!;

    public int ToLocationId { get; set; }
    public Location ToLocation { get; set; } = null!;

    public double DistanceKm { get; set; }

    /// <summary>Free-flow travel time in minutes, before congestion is applied.</summary>
    public double BaseTravelTimeMinutes { get; set; }

    public CongestionLevel Congestion { get; set; } = CongestionLevel.Low;

    public double TollCost { get; set; }

    public bool IsClosed { get; set; }

    public string RoadName { get; set; } = string.Empty;

    /// <summary>Effective travel time in minutes, factoring in current congestion.</summary>
    public double EffectiveTravelTimeMinutes => BaseTravelTimeMinutes * Congestion.TimeMultiplier();
}
