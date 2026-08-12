using Microsoft.AspNetCore.Mvc;
using TrafficRouteOptimizer.API.DTOs;
using TrafficRouteOptimizer.API.Models;
using TrafficRouteOptimizer.API.Services;

namespace TrafficRouteOptimizer.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoutesController : ControllerBase
{
    private readonly IRouteService _routeService;

    public RoutesController(IRouteService routeService)
    {
        _routeService = routeService;
    }

    /// <summary>GET /api/routes/graph — full graph (nodes + edges) for map visualization.</summary>
    [HttpGet("graph")]
    public async Task<ActionResult<GraphDto>> GetGraph()
    {
        return Ok(await _routeService.GetGraphAsync());
    }

    /// <summary>
    /// POST /api/routes/optimize — Dijkstra's algorithm with a selectable priority:
    /// Fastest (travel time incl. congestion), Cheapest (toll cost), or Shortest (distance).
    /// Closed roads are automatically excluded.
    /// </summary>
    [HttpPost("optimize")]
    public async Task<ActionResult<RouteResultDto>> Optimize([FromBody] RouteRequestDto request)
    {
        if (!Enum.TryParse<RoutePriority>(request.Priority, ignoreCase: true, out var priority))
        {
            return BadRequest(new { message = $"Invalid priority '{request.Priority}'. Use Fastest, Cheapest, or Shortest." });
        }

        var result = await _routeService.FindOptimalRouteAsync(request.SourceLocationId, request.DestinationLocationId, priority);
        if (!result.Found) return NotFound(result);

        return Ok(result);
    }

    /// <summary>
    /// POST /api/routes/astar — A* search minimizing distance, using a haversine
    /// heuristic. Demonstrates an informed-search alternative to Dijkstra.
    /// </summary>
    [HttpPost("astar")]
    public async Task<ActionResult<RouteResultDto>> AStar([FromBody] RouteRequestDto request)
    {
        var result = await _routeService.FindShortestRouteAStarAsync(request.SourceLocationId, request.DestinationLocationId);
        if (!result.Found) return NotFound(result);

        return Ok(result);
    }

    /// <summary>
    /// GET /api/routes/reachable/{sourceId}?maxHops=3 — BFS over the unweighted,
    /// open-roads-only graph. Returns every location reachable within N hops.
    /// </summary>
    [HttpGet("reachable/{sourceId:int}")]
    public async Task<ActionResult<ReachabilityResultDto>> Reachable(int sourceId, [FromQuery] int maxHops = 3)
    {
        if (maxHops < 1 || maxHops > 20)
        {
            return BadRequest(new { message = "maxHops must be between 1 and 20." });
        }

        var result = await _routeService.FindReachableLocationsAsync(sourceId, maxHops);
        return Ok(result);
    }
}
