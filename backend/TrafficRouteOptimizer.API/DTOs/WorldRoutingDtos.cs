namespace TrafficRouteOptimizer.API.DTOs;

public class WorldPlaceDto
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
}

public class WorldRouteRequestDto
{
    public double SourceLatitude { get; set; }
    public double SourceLongitude { get; set; }
    public double DestinationLatitude { get; set; }
    public double DestinationLongitude { get; set; }
    public string Profile { get; set; } = "driving";
}

public class GeoPointDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class RouteStepDto
{
    public string Instruction { get; set; } = string.Empty;
    public string RoadName { get; set; } = string.Empty;
    public double DistanceKm { get; set; }
    public double DurationMinutes { get; set; }
}

public class WorldRouteDto
{
    public bool Found { get; set; }
    public string Provider { get; set; } = "OpenStreetMap / OSRM";
    public string Message { get; set; } = string.Empty;
    public double TotalDistanceKm { get; set; }
    public double TotalDurationMinutes { get; set; }
    public List<GeoPointDto> Geometry { get; set; } = new();
    public List<RouteStepDto> Steps { get; set; } = new();
}
