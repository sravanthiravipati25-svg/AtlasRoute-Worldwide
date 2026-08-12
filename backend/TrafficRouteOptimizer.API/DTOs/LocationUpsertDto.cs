using System.ComponentModel.DataAnnotations;

namespace TrafficRouteOptimizer.API.DTOs;

public class LocationUpsertDto
{
    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Range(-180, 180)]
    public double Longitude { get; set; }
}
