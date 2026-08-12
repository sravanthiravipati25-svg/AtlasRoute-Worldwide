using TrafficRouteOptimizer.API.DTOs;
using TrafficRouteOptimizer.API.Models;

namespace TrafficRouteOptimizer.API.Services;

public class RouteService : IRouteService
{
    private readonly GraphService _graphService;

    public RouteService(GraphService graphService)
    {
        _graphService = graphService;
    }

    public async Task<GraphDto> GetGraphAsync()
    {
        var (locations, adjacency) = await _graphService.BuildGraphAsync(excludeClosed: false);

        var dto = new GraphDto
        {
            Locations = locations.Values
                .Select(l => new LocationDto { Id = l.Id, Name = l.Name, Latitude = l.Latitude, Longitude = l.Longitude })
                .OrderBy(l => l.Id)
                .ToList()
        };

        foreach (var segments in adjacency.Values)
        {
            foreach (var s in segments)
            {
                dto.Roads.Add(new RoadSegmentDto
                {
                    Id = s.Id,
                    FromLocationId = s.FromLocationId,
                    ToLocationId = s.ToLocationId,
                    RoadName = s.RoadName,
                    DistanceKm = s.DistanceKm,
                    BaseTravelTimeMinutes = s.BaseTravelTimeMinutes,
                    EffectiveTravelTimeMinutes = s.EffectiveTravelTimeMinutes,
                    Congestion = s.Congestion.ToString(),
                    TollCost = s.TollCost,
                    IsClosed = s.IsClosed
                });
            }
        }

        return dto;
    }

    /// <summary>
    /// Dijkstra's algorithm over the road graph. The edge weight function is
    /// selected based on the requested RoutePriority, so the same algorithm
    /// serves "Fastest", "Cheapest" and "Shortest" routing.
    /// Closed roads are excluded from the graph entirely.
    /// </summary>
    public async Task<RouteResultDto> FindOptimalRouteAsync(int sourceId, int destinationId, RoutePriority priority)
    {
        var (locations, adjacency) = await _graphService.BuildGraphAsync(excludeClosed: true);

        if (!ValidateEndpoints(locations, sourceId, destinationId, out var validationError))
        {
            return Failure(priority.ToString(), "Dijkstra", validationError);
        }

        Func<RoadSegment, double> weightFn = priority switch
        {
            RoutePriority.Fastest => s => s.EffectiveTravelTimeMinutes,
            RoutePriority.Cheapest => s => s.TollCost + s.DistanceKm * 0.001, // tiny tie-breaker
            RoutePriority.Shortest => s => s.DistanceKm,
            _ => s => s.EffectiveTravelTimeMinutes
        };

        var distances = locations.Keys.ToDictionary(id => id, _ => double.PositiveInfinity);
        var previousSegment = new Dictionary<int, RoadSegment>();
        var previousNode = new Dictionary<int, int>();
        var visited = new HashSet<int>();

        distances[sourceId] = 0;
        var pq = new PriorityQueue<int, double>();
        pq.Enqueue(sourceId, 0);

        while (pq.Count > 0)
        {
            var current = pq.Dequeue();
            if (!visited.Add(current)) continue;
            if (current == destinationId) break;

            foreach (var edge in adjacency[current])
            {
                var candidate = distances[current] + weightFn(edge);
                if (candidate < distances[edge.ToLocationId])
                {
                    distances[edge.ToLocationId] = candidate;
                    previousSegment[edge.ToLocationId] = edge;
                    previousNode[edge.ToLocationId] = current;
                    pq.Enqueue(edge.ToLocationId, candidate);
                }
            }
        }

        if (double.IsPositiveInfinity(distances[destinationId]))
        {
            return Failure(priority.ToString(), "Dijkstra", "No route exists between these locations (roads may be closed).");
        }

        var path = ReconstructPath(sourceId, destinationId, previousNode, previousSegment);
        return BuildResult(locations, path, priority.ToString(), "Dijkstra");
    }

    /// <summary>
    /// A* search minimizing total distance (km), using the haversine
    /// straight-line distance to the destination as an admissible heuristic.
    /// </summary>
    public async Task<RouteResultDto> FindShortestRouteAStarAsync(int sourceId, int destinationId)
    {
        var (locations, adjacency) = await _graphService.BuildGraphAsync(excludeClosed: true);

        if (!ValidateEndpoints(locations, sourceId, destinationId, out var validationError))
        {
            return Failure("Shortest", "A*", validationError);
        }

        var target = locations[destinationId];

        var gScore = locations.Keys.ToDictionary(id => id, _ => double.PositiveInfinity);
        var previousSegment = new Dictionary<int, RoadSegment>();
        var previousNode = new Dictionary<int, int>();
        var visited = new HashSet<int>();

        gScore[sourceId] = 0;
        var openSet = new PriorityQueue<int, double>();
        openSet.Enqueue(sourceId, GraphService.HaversineKm(locations[sourceId], target));

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();
            if (!visited.Add(current)) continue;
            if (current == destinationId) break;

            foreach (var edge in adjacency[current])
            {
                var tentativeG = gScore[current] + edge.DistanceKm;
                if (tentativeG < gScore[edge.ToLocationId])
                {
                    gScore[edge.ToLocationId] = tentativeG;
                    previousSegment[edge.ToLocationId] = edge;
                    previousNode[edge.ToLocationId] = current;
                    var fScore = tentativeG + GraphService.HaversineKm(locations[edge.ToLocationId], target);
                    openSet.Enqueue(edge.ToLocationId, fScore);
                }
            }
        }

        if (double.IsPositiveInfinity(gScore[destinationId]))
        {
            return Failure("Shortest", "A*", "No route exists between these locations (roads may be closed).");
        }

        var path = ReconstructPath(sourceId, destinationId, previousNode, previousSegment);
        return BuildResult(locations, path, "Shortest", "A*");
    }

    /// <summary>
    /// Breadth-first search over the unweighted, open-roads-only graph.
    /// Returns every location reachable from the source within maxHops.
    /// </summary>
    public async Task<ReachabilityResultDto> FindReachableLocationsAsync(int sourceId, int maxHops)
    {
        var (locations, adjacency) = await _graphService.BuildGraphAsync(excludeClosed: true);

        var result = new ReachabilityResultDto { SourceLocationId = sourceId, MaxHops = maxHops };

        if (!locations.ContainsKey(sourceId)) return result;

        var hopsFromSource = new Dictionary<int, int> { [sourceId] = 0 };
        var queue = new Queue<int>();
        queue.Enqueue(sourceId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var currentHops = hopsFromSource[current];
            if (currentHops >= maxHops) continue;

            foreach (var edge in adjacency[current])
            {
                if (!hopsFromSource.ContainsKey(edge.ToLocationId))
                {
                    hopsFromSource[edge.ToLocationId] = currentHops + 1;
                    queue.Enqueue(edge.ToLocationId);
                }
            }
        }

        result.Reachable = hopsFromSource
            .Where(kv => kv.Key != sourceId)
            .OrderBy(kv => kv.Value)
            .Select(kv => new ReachableLocationDto
            {
                LocationId = kv.Key,
                Name = locations[kv.Key].Name,
                Hops = kv.Value
            })
            .ToList();

        return result;
    }

    // ---------- helpers ----------

    private static bool ValidateEndpoints(Dictionary<int, Location> locations, int sourceId, int destinationId, out string error)
    {
        if (!locations.ContainsKey(sourceId))
        {
            error = $"Source location {sourceId} does not exist.";
            return false;
        }
        if (!locations.ContainsKey(destinationId))
        {
            error = $"Destination location {destinationId} does not exist.";
            return false;
        }
        if (sourceId == destinationId)
        {
            error = "Source and destination must be different locations.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private static List<RoadSegment> ReconstructPath(int sourceId, int destinationId,
        Dictionary<int, int> previousNode, Dictionary<int, RoadSegment> previousSegment)
    {
        var segments = new List<RoadSegment>();
        var current = destinationId;
        while (current != sourceId)
        {
            var segment = previousSegment[current];
            segments.Add(segment);
            current = previousNode[current];
        }
        segments.Reverse();
        return segments;
    }

    private static RouteResultDto BuildResult(Dictionary<int, Location> locations, List<RoadSegment> path, string priority, string algorithm)
    {
        var result = new RouteResultDto
        {
            Found = true,
            Priority = priority,
            Algorithm = algorithm,
            Message = "Route found.",
            HopCount = path.Count
        };

        if (path.Count == 0) return result;

        result.PathLocationIds.Add(path[0].FromLocationId);
        result.PathLocationNames.Add(locations[path[0].FromLocationId].Name);

        foreach (var segment in path)
        {
            result.PathLocationIds.Add(segment.ToLocationId);
            result.PathLocationNames.Add(locations[segment.ToLocationId].Name);

            result.Segments.Add(new RouteSegmentResultDto
            {
                FromLocationId = segment.FromLocationId,
                FromLocationName = locations[segment.FromLocationId].Name,
                ToLocationId = segment.ToLocationId,
                ToLocationName = locations[segment.ToLocationId].Name,
                RoadName = segment.RoadName,
                DistanceKm = segment.DistanceKm,
                TravelTimeMinutes = segment.EffectiveTravelTimeMinutes,
                TollCost = segment.TollCost,
                Congestion = segment.Congestion.ToString()
            });

            result.TotalDistanceKm += segment.DistanceKm;
            result.TotalTravelTimeMinutes += segment.EffectiveTravelTimeMinutes;
            result.TotalTollCost += segment.TollCost;
        }

        result.TotalDistanceKm = Math.Round(result.TotalDistanceKm, 2);
        result.TotalTravelTimeMinutes = Math.Round(result.TotalTravelTimeMinutes, 1);
        result.TotalTollCost = Math.Round(result.TotalTollCost, 2);

        return result;
    }

    private static RouteResultDto Failure(string priority, string algorithm, string message) => new()
    {
        Found = false,
        Priority = priority,
        Algorithm = algorithm,
        Message = message
    };
}
