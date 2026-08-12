using TrafficRouteOptimizer.API.DTOs;

namespace TrafficRouteOptimizer.API.Services;

/// <summary>
/// Builds an ad hoc weighted graph from whatever worldwide locations the user
/// has picked (via WorldRoutingService/OSRM for real distance+time), layers on
/// simulated congestion and toll cost, and runs Dijkstra / A* over it — the
/// same algorithms as RouteService, but over a graph assembled at request time
/// instead of a fixed seeded city.
/// </summary>
public class WorldGraphService
{
    private readonly WorldRoutingService _routing;

    private static readonly string[] CongestionByBucket =
        { "Low", "Low", "Low", "Low", "Medium", "Medium", "Medium", "High", "High", "Severe" };

    public WorldGraphService(WorldRoutingService routing)
    {
        _routing = routing;
    }

    /// <summary>Fetches a real edge (distance/time/geometry) between every pair of nodes and layers on simulated congestion/toll.</summary>
    public async Task<List<EdgeWithGeometry>> BuildEdgesAsync(
        List<WorldGraphNodeInputDto> nodes, string profile, CancellationToken cancellationToken)
    {
        var edges = new List<EdgeWithGeometry>();

        for (var i = 0; i < nodes.Count; i++)
        {
            for (var j = 0; j < nodes.Count; j++)
            {
                if (i == j) continue;

                var a = nodes[i];
                var b = nodes[j];

                // Fetch each undirected pair once, reuse (reversed geometry) for the other direction.
                if (j < i)
                {
                    var reverseOf = edges.FirstOrDefault(e => e.Edge.FromId == b.Id && e.Edge.ToId == a.Id);
                    if (reverseOf is not null)
                    {
                        edges.Add(MakeReverse(a, b, reverseOf));
                        continue;
                    }
                }

                var segment = await _routing.GetSegmentAsync(a.Latitude, a.Longitude, b.Latitude, b.Longitude, profile, cancellationToken);
                if (!segment.Found) continue;

                var (congestion, toll) = SimulateTraffic(a.Id, b.Id, segment.DistanceKm);

                edges.Add(new EdgeWithGeometry
                {
                    Edge = new WorldGraphEdgeDto
                    {
                        FromId = a.Id,
                        ToId = b.Id,
                        DistanceKm = segment.DistanceKm,
                        BaseDurationMinutes = segment.DurationMinutes,
                        EffectiveDurationMinutes = Math.Round(segment.DurationMinutes * CongestionMultiplier(congestion), 1),
                        Congestion = congestion,
                        TollCost = toll,
                        IsClosed = false
                    },
                    Geometry = segment.Geometry
                });
            }
        }

        return edges;
    }

    public async Task<WorldGraphRouteResultDto> RouteAsync(
        List<WorldGraphNodeInputDto> nodes, List<EdgeWithGeometry> edges,
        string sourceId, string destinationId, string priority, bool useAStar, CancellationToken cancellationToken)
    {
        var nodeMap = nodes.ToDictionary(n => n.Id);
        if (!nodeMap.ContainsKey(sourceId) || !nodeMap.ContainsKey(destinationId))
            return Failure(priority, useAStar ? "A*" : "Dijkstra", "Source or destination node is not in the graph.");

        if (sourceId == destinationId)
            return Failure(priority, useAStar ? "A*" : "Dijkstra", "Source and destination must be different.");

        var adjacency = nodes.ToDictionary(n => n.Id, _ => new List<EdgeWithGeometry>());
        foreach (var e in edges.Where(e => !e.Edge.IsClosed))
        {
            if (adjacency.TryGetValue(e.Edge.FromId, out var list)) list.Add(e);
        }

        Func<WorldGraphEdgeDto, double> weightFn = useAStar
            ? e => e.DistanceKm
            : priority switch
            {
                "Cheapest" => e => e.TollCost + e.DistanceKm * 0.001,
                "Shortest" => e => e.DistanceKm,
                _ => e => e.EffectiveDurationMinutes
            };

        var distances = nodes.ToDictionary(n => n.Id, _ => double.PositiveInfinity);
        var previous = new Dictionary<string, EdgeWithGeometry>();
        var previousNode = new Dictionary<string, string>();
        var visited = new HashSet<string>();

        var target = nodeMap[destinationId];
        distances[sourceId] = 0;

        var pq = new PriorityQueue<string, double>();
        pq.Enqueue(sourceId, useAStar ? Haversine(nodeMap[sourceId], target) : 0);

        while (pq.Count > 0)
        {
            var current = pq.Dequeue();
            if (!visited.Add(current)) continue;
            if (current == destinationId) break;

            foreach (var e in adjacency[current])
            {
                var candidate = distances[current] + weightFn(e.Edge);
                if (candidate < distances[e.Edge.ToId])
                {
                    distances[e.Edge.ToId] = candidate;
                    previous[e.Edge.ToId] = e;
                    previousNode[e.Edge.ToId] = current;
                    var priorityKey = useAStar ? candidate + Haversine(nodeMap[e.Edge.ToId], target) : candidate;
                    pq.Enqueue(e.Edge.ToId, priorityKey);
                }
            }
        }

        if (double.IsPositiveInfinity(distances[destinationId]))
        {
            return Failure(priority, useAStar ? "A*" : "Dijkstra",
                "No route exists between these locations with the current closures.");
        }

        var path = new List<EdgeWithGeometry>();
        var node = destinationId;
        while (node != sourceId)
        {
            var edge = previous[node];
            path.Add(edge);
            node = previousNode[node];
        }
        path.Reverse();

        return BuildResult(nodeMap, path, priority, useAStar ? "A*" : "Dijkstra");
    }

    public async Task<WorldGraphCompareResultDto> CompareAsync(
        List<WorldGraphNodeInputDto> nodes, List<EdgeWithGeometry> edges,
        string sourceId, string destinationId, CancellationToken cancellationToken)
    {
        return new WorldGraphCompareResultDto
        {
            Fastest = await RouteAsync(nodes, edges, sourceId, destinationId, "Fastest", false, cancellationToken),
            Cheapest = await RouteAsync(nodes, edges, sourceId, destinationId, "Cheapest", false, cancellationToken),
            Shortest = await RouteAsync(nodes, edges, sourceId, destinationId, "Shortest", false, cancellationToken),
            AStar = await RouteAsync(nodes, edges, sourceId, destinationId, "Shortest", true, cancellationToken)
        };
    }

    public static void ApplyClosures(List<EdgeWithGeometry> edges, List<string> closedEdgeKeys)
    {
        if (closedEdgeKeys.Count == 0) return;
        var closed = new HashSet<string>(closedEdgeKeys);
        foreach (var e in edges)
        {
            var key = $"{e.Edge.FromId}-{e.Edge.ToId}";
            var reverseKey = $"{e.Edge.ToId}-{e.Edge.FromId}";
            if (closed.Contains(key) || closed.Contains(reverseKey)) e.Edge.IsClosed = true;
        }
    }

    // ---------- helpers ----------

    /// <summary>Deterministic simulated congestion/toll for a node pair — same inputs always produce the same values within a run.</summary>
    private static (string Congestion, double Toll) SimulateTraffic(string idA, string idB, double distanceKm)
    {
        var key = string.CompareOrdinal(idA, idB) <= 0 ? $"{idA}|{idB}" : $"{idB}|{idA}";
        var hash = StableHash(key);

        var congestion = CongestionByBucket[hash % 10];
        var toll = distanceKm > 20 && hash % 3 == 0 ? Math.Round(distanceKm * 1.5, 0) : 0;

        return (congestion, toll);
    }

    private static double CongestionMultiplier(string level) => level switch
    {
        "Medium" => 1.3,
        "High" => 1.75,
        "Severe" => 2.5,
        _ => 1.0
    };

    private static uint StableHash(string s)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var c in s)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash;
        }
    }

    private static double Haversine(WorldGraphNodeInputDto a, WorldGraphNodeInputDto b)
    {
        const double earthRadiusKm = 6371.0;
        double ToRad(double deg) => deg * Math.PI / 180.0;

        var dLat = ToRad(b.Latitude - a.Latitude);
        var dLon = ToRad(b.Longitude - a.Longitude);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(a.Latitude)) * Math.Cos(ToRad(b.Latitude)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h));
    }

    private static EdgeWithGeometry MakeReverse(WorldGraphNodeInputDto a, WorldGraphNodeInputDto b, EdgeWithGeometry forward)
    {
        return new EdgeWithGeometry
        {
            Edge = new WorldGraphEdgeDto
            {
                FromId = a.Id,
                ToId = b.Id,
                DistanceKm = forward.Edge.DistanceKm,
                BaseDurationMinutes = forward.Edge.BaseDurationMinutes,
                EffectiveDurationMinutes = forward.Edge.EffectiveDurationMinutes,
                Congestion = forward.Edge.Congestion,
                TollCost = forward.Edge.TollCost,
                IsClosed = false
            },
            Geometry = Enumerable.Reverse(forward.Geometry).ToList()
        };
    }

    private static WorldGraphRouteResultDto BuildResult(
        Dictionary<string, WorldGraphNodeInputDto> nodeMap, List<EdgeWithGeometry> path, string priority, string algorithm)
    {
        var result = new WorldGraphRouteResultDto
        {
            Found = true,
            Message = "Route found.",
            Priority = priority,
            Algorithm = algorithm
        };

        if (path.Count == 0) return result;

        result.PathNodeIds.Add(path[0].Edge.FromId);
        result.PathNodeNames.Add(nodeMap[path[0].Edge.FromId].Name);

        foreach (var e in path)
        {
            result.PathNodeIds.Add(e.Edge.ToId);
            result.PathNodeNames.Add(nodeMap[e.Edge.ToId].Name);

            result.Steps.Add(new WorldGraphRouteStepDto
            {
                FromId = e.Edge.FromId,
                FromName = nodeMap[e.Edge.FromId].Name,
                ToId = e.Edge.ToId,
                ToName = nodeMap[e.Edge.ToId].Name,
                DistanceKm = e.Edge.DistanceKm,
                DurationMinutes = e.Edge.EffectiveDurationMinutes,
                TollCost = e.Edge.TollCost,
                Congestion = e.Edge.Congestion
            });

            result.Geometry.AddRange(e.Geometry);
            result.TotalDistanceKm += e.Edge.DistanceKm;
            result.TotalDurationMinutes += e.Edge.EffectiveDurationMinutes;
            result.TotalTollCost += e.Edge.TollCost;
        }

        result.TotalDistanceKm = Math.Round(result.TotalDistanceKm, 2);
        result.TotalDurationMinutes = Math.Round(result.TotalDurationMinutes, 1);
        result.TotalTollCost = Math.Round(result.TotalTollCost, 2);

        return result;
    }

    private static WorldGraphRouteResultDto Failure(string priority, string algorithm, string message) => new()
    {
        Found = false,
        Priority = priority,
        Algorithm = algorithm,
        Message = message
    };

    public class EdgeWithGeometry
    {
        public WorldGraphEdgeDto Edge { get; set; } = new();
        public List<GeoPointDto> Geometry { get; set; } = new();
    }
}
