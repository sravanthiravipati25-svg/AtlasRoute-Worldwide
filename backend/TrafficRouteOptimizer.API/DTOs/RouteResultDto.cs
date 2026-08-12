namespace TrafficRouteOptimizer.API.DTOs;

public class RouteSegmentResultDto
{
    public int FromLocationId { get; set; }
    public string FromLocationName { get; set; } = string.Empty;
    public int ToLocationId { get; set; }
    public string ToLocationName { get; set; } = string.Empty;
    public string RoadName { get; set; } = string.Empty;
    public double DistanceKm { get; set; }
    public double TravelTimeMinutes { get; set; }
    public double TollCost { get; set; }
    public string Congestion { get; set; } = string.Empty;
}

public class RouteResultDto
{
    public bool Found { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;

    public List<int> PathLocationIds { get; set; } = new();
    public List<string> PathLocationNames { get; set; } = new();
    public List<RouteSegmentResultDto> Segments { get; set; } = new();

    public double TotalDistanceKm { get; set; }
    public double TotalTravelTimeMinutes { get; set; }
    public double TotalTollCost { get; set; }
    public int HopCount { get; set; }
}
