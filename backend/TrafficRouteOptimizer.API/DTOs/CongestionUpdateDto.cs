using System.ComponentModel.DataAnnotations;

namespace TrafficRouteOptimizer.API.DTOs;

public class CongestionUpdateDto
{
    /// <summary>"Low" | "Medium" | "High" | "Severe"</summary>
    [Required]
    public string Congestion { get; set; } = "Low";

    /// <summary>If true and the road is bidirectional, the reverse edge is updated too.</summary>
    public bool ApplyToReverseEdge { get; set; } = true;
}
