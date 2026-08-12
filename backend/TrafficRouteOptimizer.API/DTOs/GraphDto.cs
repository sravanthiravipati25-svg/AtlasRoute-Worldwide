namespace TrafficRouteOptimizer.API.DTOs;

public class GraphDto
{
    public List<LocationDto> Locations { get; set; } = new();
    public List<RoadSegmentDto> Roads { get; set; } = new();
}
