using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrafficRouteOptimizer.API.Data;
using TrafficRouteOptimizer.API.DTOs;
using TrafficRouteOptimizer.API.Models;

namespace TrafficRouteOptimizer.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LocationsController : ControllerBase
{
    private readonly AppDbContext _context;

    public LocationsController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>GET /api/locations — list every location in the network.</summary>
    [HttpGet]
    public async Task<ActionResult<List<LocationDto>>> GetAll()
    {
        var locations = await _context.Locations
            .AsNoTracking()
            .OrderBy(l => l.Name)
            .Select(l => ToDto(l))
            .ToListAsync();

        return Ok(locations);
    }

    /// <summary>GET /api/locations/{id}</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<LocationDto>> GetById(int id)
    {
        var location = await _context.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
        if (location is null) return NotFound(new { message = $"Location {id} not found." });

        return Ok(ToDto(location));
    }

    /// <summary>POST /api/locations — add a new location to the network.</summary>
    [HttpPost]
    public async Task<ActionResult<LocationDto>> Create([FromBody] LocationUpsertDto dto)
    {
        var location = new Location { Name = dto.Name, Latitude = dto.Latitude, Longitude = dto.Longitude };
        _context.Locations.Add(location);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = location.Id }, ToDto(location));
    }

    /// <summary>PUT /api/locations/{id} — update a location's name or coordinates.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<LocationDto>> Update(int id, [FromBody] LocationUpsertDto dto)
    {
        var location = await _context.Locations.FirstOrDefaultAsync(l => l.Id == id);
        if (location is null) return NotFound(new { message = $"Location {id} not found." });

        location.Name = dto.Name;
        location.Latitude = dto.Latitude;
        location.Longitude = dto.Longitude;
        await _context.SaveChangesAsync();

        return Ok(ToDto(location));
    }

    /// <summary>
    /// DELETE /api/locations/{id} — remove a location. Rejected with 409 if any
    /// road segment still references it; delete those roads first.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var location = await _context.Locations.FirstOrDefaultAsync(l => l.Id == id);
        if (location is null) return NotFound(new { message = $"Location {id} not found." });

        var hasSegments = await _context.RoadSegments
            .AnyAsync(s => s.FromLocationId == id || s.ToLocationId == id);

        if (hasSegments)
        {
            return Conflict(new { message = $"Location {id} still has connected roads. Delete those road segments first." });
        }

        _context.Locations.Remove(location);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static LocationDto ToDto(Location l) => new()
    {
        Id = l.Id,
        Name = l.Name,
        Latitude = l.Latitude,
        Longitude = l.Longitude
    };
}
