namespace TrafficRouteOptimizer.API.DTOs;

/// <summary>A worldwide location the user picked via search, used as a node in an ad hoc graph.</summary>
public class WorldGraphNodeInputDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class WorldGraphEdgeDto
{
    public string FromId { get; set; } = string.Empty;
    public string ToId { get; set; } = string.Empty;
    public double DistanceKm { get; set; }
    public double BaseDurationMinutes { get; set; }
    public double EffectiveDurationMinutes { get; set; }
    public string Congestion { get; set; } = string.Empty;
    public double TollCost { get; set; }
    public bool IsClosed { get; set; }
}

/// <summary>POST /api/world/graph/build — turn N picked worldwide locations into a complete weighted graph.</summary>
public class WorldGraphBuildRequestDto
{
    public List<WorldGraphNodeInputDto> Nodes { get; set; } = new();
    public string Profile { get; set; } = "driving";
}

public class WorldGraphDto
{
    public List<WorldGraphNodeInputDto> Nodes { get; set; } = new();
    public List<WorldGraphEdgeDto> Edges { get; set; } = new();
    public string Note { get; set; } =
        "Distance and travel time come from live OpenStreetMap road routing. " +
        "Congestion and toll cost are simulated per edge (deterministic per node pair) " +
        "since free real-time traffic/toll data isn't available.";
}

/// <summary>POST /api/world/graph/route — Dijkstra or A* over the dynamic graph.</summary>
public class WorldGraphRouteRequestDto
{
    public List<WorldGraphNodeInputDto> Nodes { get; set; } = new();
    public string SourceId { get; set; } = string.Empty;
    public string DestinationId { get; set; } = string.Empty;
    /// <summary>"Fastest" | "Cheapest" | "Shortest". Ignored when UseAStar is true.</summary>
    public string Priority { get; set; } = "Fastest";
    public bool UseAStar { get; set; }
    public string Profile { get; set; } = "driving";
    /// <summary>Edge keys ("fromId-toId") the user has marked closed, e.g. from an accident or maintenance.</summary>
    public List<string> ClosedEdgeKeys { get; set; } = new();
}

public class WorldGraphRouteStepDto
{
    public string FromId { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string ToId { get; set; } = string.Empty;
    public string ToName { get; set; } = string.Empty;
    public double DistanceKm { get; set; }
    public double DurationMinutes { get; set; }
    public double TollCost { get; set; }
    public string Congestion { get; set; } = string.Empty;
}

public class WorldGraphRouteResultDto
{
    public bool Found { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;
    public List<string> PathNodeIds { get; set; } = new();
    public List<string> PathNodeNames { get; set; } = new();
    public List<WorldGraphRouteStepDto> Steps { get; set; } = new();
    public List<GeoPointDto> Geometry { get; set; } = new();
    public double TotalDistanceKm { get; set; }
    public double TotalDurationMinutes { get; set; }
    public double TotalTollCost { get; set; }
}

/// <summary>POST /api/world/graph/compare — run Fastest, Cheapest, Shortest and A* together.</summary>
public class WorldGraphCompareRequestDto
{
    public List<WorldGraphNodeInputDto> Nodes { get; set; } = new();
    public string SourceId { get; set; } = string.Empty;
    public string DestinationId { get; set; } = string.Empty;
    public string Profile { get; set; } = "driving";
    public List<string> ClosedEdgeKeys { get; set; } = new();
}

public class WorldGraphCompareResultDto
{
    public WorldGraphRouteResultDto Fastest { get; set; } = new();
    public WorldGraphRouteResultDto Cheapest { get; set; } = new();
    public WorldGraphRouteResultDto Shortest { get; set; } = new();
    public WorldGraphRouteResultDto AStar { get; set; } = new();
}
