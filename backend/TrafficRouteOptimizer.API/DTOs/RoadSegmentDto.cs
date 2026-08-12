namespace TrafficRouteOptimizer.API.DTOs;

public class RoadSegmentDto
{
    public int Id { get; set; }
    public int FromLocationId { get; set; }
    public int ToLocationId { get; set; }
    public string RoadName { get; set; } = string.Empty;
    public double DistanceKm { get; set; }
    public double BaseTravelTimeMinutes { get; set; }
    public double EffectiveTravelTimeMinutes { get; set; }
    public string Congestion { get; set; } = string.Empty;
    public double TollCost { get; set; }
    public bool IsClosed { get; set; }
}
