using TrafficRouteOptimizer.API.DTOs;
using TrafficRouteOptimizer.API.Models;

namespace TrafficRouteOptimizer.API.Services;

public interface IRouteService
{
    /// <summary>Dijkstra's algorithm with a weight function selected by RoutePriority.</summary>
    Task<RouteResultDto> FindOptimalRouteAsync(int sourceId, int destinationId, RoutePriority priority);

    /// <summary>A* search minimizing distance, using a haversine straight-line heuristic.</summary>
    Task<RouteResultDto> FindShortestRouteAStarAsync(int sourceId, int destinationId);

    /// <summary>BFS over the unweighted graph (open roads only) to find all locations within N hops.</summary>
    Task<ReachabilityResultDto> FindReachableLocationsAsync(int sourceId, int maxHops);

    Task<GraphDto> GetGraphAsync();
}
