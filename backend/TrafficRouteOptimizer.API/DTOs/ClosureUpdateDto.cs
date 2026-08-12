namespace TrafficRouteOptimizer.API.DTOs;

public class ClosureUpdateDto
{
    public bool IsClosed { get; set; }

    /// <summary>If true and the road is bidirectional, the reverse edge is updated too.</summary>
    public bool ApplyToReverseEdge { get; set; } = true;
}
