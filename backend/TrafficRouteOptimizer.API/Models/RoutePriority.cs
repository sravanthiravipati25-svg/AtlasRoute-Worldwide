namespace TrafficRouteOptimizer.API.Models;

/// <summary>
/// The optimization objective the user wants applied when computing a route.
/// </summary>
public enum RoutePriority
{
    Fastest,
    Cheapest,
    Shortest
}
