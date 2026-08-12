namespace TrafficRouteOptimizer.API.Models;

/// <summary>
/// A node in the road graph (intersection, landmark, area, etc.)
/// </summary>
public class Location
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public ICollection<RoadSegment> OutgoingSegments { get; set; } = new List<RoadSegment>();
    public ICollection<RoadSegment> IncomingSegments { get; set; } = new List<RoadSegment>();
}
