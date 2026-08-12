using Microsoft.AspNetCore.Mvc;
using TrafficRouteOptimizer.API.DTOs;
using TrafficRouteOptimizer.API.Services;

namespace TrafficRouteOptimizer.API.Controllers;

[ApiController]
[Route("api/world")]
public class WorldController : ControllerBase
{
    private readonly WorldRoutingService _service;
    private readonly WorldGraphService _graphService;

    public WorldController(WorldRoutingService service, WorldGraphService graphService)
    {
        _service = service;
        _graphService = graphService;
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<WorldPlaceDto>>> Search(
        [FromQuery] string q,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Ok(Array.Empty<WorldPlaceDto>());

        return Ok(await _service.SearchAsync(q, cancellationToken));
    }

    [HttpPost("route")]
    public async Task<ActionResult<WorldRouteDto>> Route(
        [FromBody] WorldRouteRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.RouteAsync(request, cancellationToken);

            if (!result.Found)
                return UnprocessableEntity(result);

            return Ok(result);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new
            {
                message = "The live routing provider is temporarily unavailable. Please try again."
            });
        }
    }

    /// <summary>
    /// POST /api/world/graph/build — Phase 2/3: turn N worldwide locations into a
    /// complete weighted graph using live OSRM distance/time plus simulated
    /// congestion and toll per edge.
    /// </summary>
    [HttpPost("graph/build")]
    public async Task<ActionResult<WorldGraphDto>> BuildGraph(
        [FromBody] WorldGraphBuildRequestDto request, CancellationToken cancellationToken)
    {
        if (request.Nodes.Count < 2)
            return BadRequest(new { message = "Add at least two locations to build a graph." });
        if (request.Nodes.Count > 8)
            return BadRequest(new { message = "Graph Lab supports up to 8 locations at once (to keep routing calls reasonable)." });

        var edges = await _graphService.BuildEdgesAsync(request.Nodes, request.Profile, cancellationToken);

        return Ok(new WorldGraphDto
        {
            Nodes = request.Nodes,
            Edges = edges.Select(e => e.Edge).ToList()
        });
    }

    /// <summary>
    /// POST /api/world/graph/route — Phase 4/5/6: Dijkstra (priority-based) or A*
    /// over the dynamic graph, honoring any closed edges.
    /// </summary>
    [HttpPost("graph/route")]
    public async Task<ActionResult<WorldGraphRouteResultDto>> RouteOnGraph(
        [FromBody] WorldGraphRouteRequestDto request, CancellationToken cancellationToken)
    {
        if (request.Nodes.Count < 2)
            return BadRequest(new { message = "Add at least two locations to build a graph." });

        var edges = await _graphService.BuildEdgesAsync(request.Nodes, request.Profile, cancellationToken);
        WorldGraphService.ApplyClosures(edges, request.ClosedEdgeKeys);

        var effectivePriority = request.UseAStar ? "Shortest" : request.Priority;
        var result = await _graphService.RouteAsync(
            request.Nodes, edges, request.SourceId, request.DestinationId,
            effectivePriority, request.UseAStar, cancellationToken);

        if (!result.Found) return UnprocessableEntity(result);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/world/graph/compare — Phase 7: Fastest, Cheapest, Shortest and
    /// A* computed together over the same dynamic graph for side-by-side comparison.
    /// </summary>
    [HttpPost("graph/compare")]
    public async Task<ActionResult<WorldGraphCompareResultDto>> CompareOnGraph(
        [FromBody] WorldGraphCompareRequestDto request, CancellationToken cancellationToken)
    {
        if (request.Nodes.Count < 2)
            return BadRequest(new { message = "Add at least two locations to build a graph." });

        var edges = await _graphService.BuildEdgesAsync(request.Nodes, request.Profile, cancellationToken);
        WorldGraphService.ApplyClosures(edges, request.ClosedEdgeKeys);

        var result = await _graphService.CompareAsync(
            request.Nodes, edges, request.SourceId, request.DestinationId, cancellationToken);

        return Ok(result);
    }
}
