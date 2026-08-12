using System.Text.Json;
using System.Text.Json.Serialization;
using TrafficRouteOptimizer.API.Models;

namespace TrafficRouteOptimizer.API.Data;

/// <summary>
/// Seeds the road network from SeedData/network-seed.json on first run, so the
/// API is usable immediately without manual data entry — but the network
/// itself lives in an editable JSON file, not hardcoded C#. Once seeded, the
/// database is the source of truth: use the Locations / RoadSegments CRUD
/// endpoints to add, edit, or remove nodes and roads at runtime, including
/// live congestion and closure updates.
/// </summary>
public static class SeedData
{
    private record SeedLocation(int Id, string Name, double Latitude, double Longitude);

    private record SeedRoad(
        int From, int To, string Name, double DistanceKm, double BaseTimeMin,
        string Congestion, double Toll, bool Closed, bool Bidirectional);

    private record SeedNetwork(List<SeedLocation> Locations, List<SeedRoad> Roads);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void EnsureSeeded(AppDbContext context, IWebHostEnvironment? env = null)
    {
        if (context.Locations.Any()) return;

        var network = LoadNetwork(env);
        if (network is null) return; 

        context.Locations.AddRange(network.Locations.Select(l => new Location
        {
            Name = l.Name,
            Latitude = l.Latitude,
            Longitude = l.Longitude
        }));
        context.SaveChanges();

        var segments = new List<RoadSegment>();
        int id = 1;
        foreach (var r in network.Roads)
        {
            var congestion = Enum.Parse<CongestionLevel>(r.Congestion, ignoreCase: true);

            segments.Add(new RoadSegment
            {
                FromLocationId = r.From,
                ToLocationId = r.To,
                RoadName = r.Name,
                DistanceKm = r.DistanceKm,
                BaseTravelTimeMinutes = r.BaseTimeMin,
                Congestion = congestion,
                TollCost = r.Toll,
                IsClosed = r.Closed
            });

            if (r.Bidirectional)
            {
                segments.Add(new RoadSegment
                {
                    FromLocationId = r.To,
                    ToLocationId = r.From,
                    RoadName = r.Name,
                    DistanceKm = r.DistanceKm,
                    BaseTravelTimeMinutes = r.BaseTimeMin,
                    Congestion = congestion,
                    TollCost = r.Toll,
                    IsClosed = r.Closed
                });
            }
        }

        context.RoadSegments.AddRange(segments);
        context.SaveChanges();
    }

    private static SeedNetwork? LoadNetwork(IWebHostEnvironment? env)
    {
        var candidatePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "SeedData", "network-seed.json"),
            env is not null ? Path.Combine(env.ContentRootPath, "SeedData", "network-seed.json") : null,
            Path.Combine(Directory.GetCurrentDirectory(), "SeedData", "network-seed.json"),
        };

        var path = candidatePaths.FirstOrDefault(p => p is not null && File.Exists(p));
        if (path is null) return null;

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SeedNetwork>(json, JsonOptions);
    }
}
