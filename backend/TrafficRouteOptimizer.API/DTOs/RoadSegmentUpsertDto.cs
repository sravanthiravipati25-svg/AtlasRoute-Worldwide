using System.ComponentModel.DataAnnotations;

namespace TrafficRouteOptimizer.API.DTOs;

public class RoadSegmentUpsertDto
{
    [Required]
    public int FromLocationId { get; set; }

    [Required]
    public int ToLocationId { get; set; }

    [Required, MaxLength(120)]
    public string RoadName { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public double DistanceKm { get; set; }

    [Range(0.01, double.MaxValue)]
    public double BaseTravelTimeMinutes { get; set; }

    /// <summary>"Low" | "Medium" | "High" | "Severe"</summary>
    public string Congestion { get; set; } = "Low";

    [Range(0, double.MaxValue)]
    public double TollCost { get; set; }

    public bool IsClosed { get; set; }

    /// <summary>If true, the reverse edge (To → From) is created/updated alongside this one.</summary>
    public bool Bidirectional { get; set; } = true;
}
