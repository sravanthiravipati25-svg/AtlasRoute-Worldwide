using System.ComponentModel.DataAnnotations;

namespace TrafficRouteOptimizer.API.DTOs;

public class RouteRequestDto
{
    [Required]
    public int SourceLocationId { get; set; }

    [Required]
    public int DestinationLocationId { get; set; }

    /// <summary>"Fastest" | "Cheapest" | "Shortest". Defaults to Fastest.</summary>
    public string Priority { get; set; } = "Fastest";
}
