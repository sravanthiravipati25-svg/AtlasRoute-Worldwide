using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrafficRouteOptimizer.API.Data;
using TrafficRouteOptimizer.API.DTOs;
using TrafficRouteOptimizer.API.Models;

namespace TrafficRouteOptimizer.API.Controllers;

/// <summary>
/// CRUD for individual road segments (directed edges), plus quick PATCH
/// endpoints for the two things that change constantly in a live traffic
/// system: congestion level and closure status.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RoadSegmentsController : ControllerBase
{
    private readonly AppDbContext _context;

    public RoadSegmentsController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>GET /api/roadsegments?locationId= — optionally filter to roads touching one location.</summary>
    [HttpGet]
    public async Task<ActionResult<List<RoadSegmentDto>>> GetAll([FromQuery] int? locationId)
    {
        var query = _context.RoadSegments.AsNoTracking().AsQueryable();
        if (locationId is not null)
        {
            query = query.Where(s => s.FromLocationId == locationId || s.ToLocationId == locationId);
        }

        var segments = await query.ToListAsync();
        return Ok(segments.Select(ToDto).ToList());
    }

    /// <summary>GET /api/roadsegments/{id}</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<RoadSegmentDto>> GetById(int id)
    {
        var segment = await _context.RoadSegments.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (segment is null) return NotFound(new { message = $"Road segment {id} not found." });

        return Ok(ToDto(segment));
    }

    /// <summary>
    /// POST /api/roadsegments — add a new road. If Bidirectional is true (default),
    /// both directions are created as two rows sharing the same RoadName.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<List<RoadSegmentDto>>> Create([FromBody] RoadSegmentUpsertDto dto)
    {
        var (isValid, error) = await LocationsExist(dto.FromLocationId, dto.ToLocationId);
        if (!isValid)
        {
            return BadRequest(new { message = error });
        }

        if (!Enum.TryParse<CongestionLevel>(dto.Congestion, ignoreCase: true, out var congestion))
        {
            return BadRequest(new { message = $"Invalid congestion level '{dto.Congestion}'. Use Low, Medium, High, or Severe." });
        }

        var created = new List<RoadSegment>
        {
            new()
            {
                FromLocationId = dto.FromLocationId,
                ToLocationId = dto.ToLocationId,
                RoadName = dto.RoadName,
                DistanceKm = dto.DistanceKm,
                BaseTravelTimeMinutes = dto.BaseTravelTimeMinutes,
                Congestion = congestion,
                TollCost = dto.TollCost,
                IsClosed = dto.IsClosed
            }
        };

        if (dto.Bidirectional)
        {
            created.Add(new RoadSegment
            {
                FromLocationId = dto.ToLocationId,
                ToLocationId = dto.FromLocationId,
                RoadName = dto.RoadName,
                DistanceKm = dto.DistanceKm,
                BaseTravelTimeMinutes = dto.BaseTravelTimeMinutes,
                Congestion = congestion,
                TollCost = dto.TollCost,
                IsClosed = dto.IsClosed
            });
        }

        _context.RoadSegments.AddRange(created);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = created[0].Id }, created.Select(ToDto).ToList());
    }

    /// <summary>PUT /api/roadsegments/{id} — update this specific directed edge's details.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<RoadSegmentDto>> Update(int id, [FromBody] RoadSegmentUpsertDto dto)
    {
        var segment = await _context.RoadSegments.FirstOrDefaultAsync(s => s.Id == id);
        if (segment is null) return NotFound(new { message = $"Road segment {id} not found." });

        var (isValid, error) = await LocationsExist(dto.FromLocationId, dto.ToLocationId);
        if (!isValid)
        {
            return BadRequest(new { message = error });
        }

        if (!Enum.TryParse<CongestionLevel>(dto.Congestion, ignoreCase: true, out var congestion))
        {
            return BadRequest(new { message = $"Invalid congestion level '{dto.Congestion}'. Use Low, Medium, High, or Severe." });
        }

        segment.FromLocationId = dto.FromLocationId;
        segment.ToLocationId = dto.ToLocationId;
        segment.RoadName = dto.RoadName;
        segment.DistanceKm = dto.DistanceKm;
        segment.BaseTravelTimeMinutes = dto.BaseTravelTimeMinutes;
        segment.Congestion = congestion;
        segment.TollCost = dto.TollCost;
        segment.IsClosed = dto.IsClosed;

        await _context.SaveChangesAsync();
        return Ok(ToDto(segment));
    }

    /// <summary>
    /// PATCH /api/roadsegments/{id}/congestion — live traffic update, without
    /// touching the road's static distance/toll/name fields.
    /// </summary>
    [HttpPatch("{id:int}/congestion")]
    public async Task<ActionResult<RoadSegmentDto>> UpdateCongestion(int id, [FromBody] CongestionUpdateDto dto)
    {
        var segment = await _context.RoadSegments.FirstOrDefaultAsync(s => s.Id == id);
        if (segment is null) return NotFound(new { message = $"Road segment {id} not found." });

        if (!Enum.TryParse<CongestionLevel>(dto.Congestion, ignoreCase: true, out var congestion))
        {
            return BadRequest(new { message = $"Invalid congestion level '{dto.Congestion}'. Use Low, Medium, High, or Severe." });
        }

        segment.Congestion = congestion;

        if (dto.ApplyToReverseEdge)
        {
            var reverse = await FindReverseSegment(segment);
            if (reverse is not null) reverse.Congestion = congestion;
        }

        await _context.SaveChangesAsync();
        return Ok(ToDto(segment));
    }

    /// <summary>PATCH /api/roadsegments/{id}/closure — open or close a road, e.g. for an accident or maintenance.</summary>
    [HttpPatch("{id:int}/closure")]
    public async Task<ActionResult<RoadSegmentDto>> UpdateClosure(int id, [FromBody] ClosureUpdateDto dto)
    {
        var segment = await _context.RoadSegments.FirstOrDefaultAsync(s => s.Id == id);
        if (segment is null) return NotFound(new { message = $"Road segment {id} not found." });

        segment.IsClosed = dto.IsClosed;

        if (dto.ApplyToReverseEdge)
        {
            var reverse = await FindReverseSegment(segment);
            if (reverse is not null) reverse.IsClosed = dto.IsClosed;
        }

        await _context.SaveChangesAsync();
        return Ok(ToDto(segment));
    }

    /// <summary>DELETE /api/roadsegments/{id}?alsoRemoveReverse=true — remove a road (and optionally its reverse direction).</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromQuery] bool alsoRemoveReverse = false)
    {
        var segment = await _context.RoadSegments.FirstOrDefaultAsync(s => s.Id == id);
        if (segment is null) return NotFound(new { message = $"Road segment {id} not found." });

        if (alsoRemoveReverse)
        {
            var reverse = await FindReverseSegment(segment);
            if (reverse is not null) _context.RoadSegments.Remove(reverse);
        }

        _context.RoadSegments.Remove(segment);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ---------- helpers ----------

    private async Task<RoadSegment?> FindReverseSegment(RoadSegment segment) =>
        await _context.RoadSegments.FirstOrDefaultAsync(s =>
            s.FromLocationId == segment.ToLocationId &&
            s.ToLocationId == segment.FromLocationId &&
            s.RoadName == segment.RoadName &&
            s.Id != segment.Id);

    private async Task<(bool IsValid, string Error)> LocationsExist(int fromId, int toId)
    {
        if (fromId == toId)
        {
            return (false, "FromLocationId and ToLocationId must be different.");
        }

        var count = await _context.Locations.CountAsync(l => l.Id == fromId || l.Id == toId);
        if (count < 2)
        {
            return (false, "FromLocationId or ToLocationId does not refer to an existing location.");
        }

        return (true, string.Empty);
    }

    private static RoadSegmentDto ToDto(RoadSegment s) => new()
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
    };
}
