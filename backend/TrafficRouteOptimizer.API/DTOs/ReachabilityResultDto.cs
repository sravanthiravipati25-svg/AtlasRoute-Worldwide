namespace TrafficRouteOptimizer.API.DTOs;

public class ReachableLocationDto
{
    public int LocationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Hops { get; set; }
}

public class ReachabilityResultDto
{
    public int SourceLocationId { get; set; }
    public int MaxHops { get; set; }
    public List<ReachableLocationDto> Reachable { get; set; } = new();
}
