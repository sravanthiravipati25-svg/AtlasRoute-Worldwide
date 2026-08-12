using Microsoft.EntityFrameworkCore;
using TrafficRouteOptimizer.API.Data;
using TrafficRouteOptimizer.API.Models;

namespace TrafficRouteOptimizer.API.Services;

/// <summary>
/// Loads the road network from the database and exposes it as an in-memory
/// adjacency list for the pathfinding algorithms to consume.
/// </summary>
public class GraphService
{
    private readonly AppDbContext _context;

    public GraphService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(Dictionary<int, Location> Locations, Dictionary<int, List<RoadSegment>> Adjacency)> BuildGraphAsync(bool excludeClosed = true)
    {
        var locations = await _context.Locations.AsNoTracking().ToListAsync();
        var segmentsQuery = _context.RoadSegments.AsNoTracking().AsQueryable();

        if (excludeClosed)
        {
            segmentsQuery = segmentsQuery.Where(s => !s.IsClosed);
        }

        var segments = await segmentsQuery.ToListAsync();

        var locationMap = locations.ToDictionary(l => l.Id);
        var adjacency = locations.ToDictionary(l => l.Id, _ => new List<RoadSegment>());

        foreach (var segment in segments)
        {
            if (adjacency.TryGetValue(segment.FromLocationId, out var list))
            {
                list.Add(segment);
            }
        }

        return (locationMap, adjacency);
    }

    /// <summary>Haversine great-circle distance in kilometres — used as the A* heuristic.</summary>
    public static double HaversineKm(Location a, Location b)
    {
        const double earthRadiusKm = 6371.0;
        double dLat = ToRadians(b.Latitude - a.Latitude);
        double dLon = ToRadians(b.Longitude - a.Longitude);

        double lat1 = ToRadians(a.Latitude);
        double lat2 = ToRadians(b.Latitude);

        double h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1) * Math.Cos(lat2) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h));
        return earthRadiusKm * c;
    }

    private static double ToRadians(double deg) => deg * Math.PI / 180.0;
}
