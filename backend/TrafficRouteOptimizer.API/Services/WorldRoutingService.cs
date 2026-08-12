using System.Net.Http.Headers;
using System.Text.Json;
using TrafficRouteOptimizer.API.DTOs;

namespace TrafficRouteOptimizer.API.Services;

/// <summary>
/// Live worldwide geocoding and road routing backed by OpenStreetMap services.
/// Keep this service behind the API so the browser never has to manage provider
/// headers/rate limits directly.
/// </summary>
public class WorldRoutingService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WorldRoutingService> _logger;

    public WorldRoutingService(
        HttpClient http,
        IConfiguration configuration,
        ILogger<WorldRoutingService> logger)
    {
        _http = http;
        _configuration = configuration;
        _logger = logger;

        var userAgent = _configuration["OpenStreetMap:UserAgent"]
                        ?? "TrafficRouteOptimizer/1.0 (development)";
        _http.DefaultRequestHeaders.UserAgent.Clear();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<List<WorldPlaceDto>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<WorldPlaceDto>();

        var baseUrl = (_configuration["OpenStreetMap:NominatimUrl"]
                      ?? "https://nominatim.openstreetmap.org").TrimEnd('/');

        var url =
            $"{baseUrl}/search?q={Uri.EscapeDataString(query.Trim())}" +
            "&format=jsonv2&addressdetails=1&limit=6&accept-language=en";

        using var response = await _http.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Nominatim returned HTTP {StatusCode}.", response.StatusCode);
            return new List<WorldPlaceDto>();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var results = new List<WorldPlaceDto>();

        foreach (var item in document.RootElement.EnumerateArray())
        {
            var address = item.TryGetProperty("address", out var addressNode)
                ? addressNode
                : default;

            var name = item.TryGetProperty("name", out var nameNode)
                ? nameNode.GetString() ?? ""
                : "";

            var country = GetString(address, "country");
            var countryCode = GetString(address, "country_code");

            results.Add(new WorldPlaceDto
            {
                Id = $"{GetString(item, "osm_type")}{GetString(item, "osm_id")}",
                DisplayName = GetString(item, "display_name"),
                Name = string.IsNullOrWhiteSpace(name)
                    ? GetString(item, "display_name").Split(',')[0]
                    : name,
                Latitude = GetDouble(item, "lat"),
                Longitude = GetDouble(item, "lon"),
                Type = GetString(item, "type"),
                Category = GetString(item, "category"),
                Country = country,
                CountryCode = countryCode
            });
        }

        return results;
    }

    public async Task<WorldRouteDto> RouteAsync(
        WorldRouteRequestDto request,
        CancellationToken cancellationToken)
    {
        ValidateCoordinate(request.SourceLatitude, request.SourceLongitude);
        ValidateCoordinate(request.DestinationLatitude, request.DestinationLongitude);

        var profile = request.Profile.ToLowerInvariant() switch
        {
            "cycling" or "bike" => "cycling",
            "walking" or "foot" => "foot",
            _ => "driving"
        };

        var baseUrl = (_configuration["OpenStreetMap:OsrmUrl"]
                      ?? "https://router.project-osrm.org").TrimEnd('/');

        var coordinates =
            $"{request.SourceLongitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
            $"{request.SourceLatitude.ToString(System.Globalization.CultureInfo.InvariantCulture)};" +
            $"{request.DestinationLongitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
            $"{request.DestinationLatitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

        var url =
            $"{baseUrl}/route/v1/{profile}/{coordinates}" +
            "?alternatives=true&steps=true&geometries=geojson&overview=full";

        using var response = await _http.GetAsync(url, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var root = document.RootElement;
        var code = GetString(root, "code");

        if (!response.IsSuccessStatusCode || !string.Equals(code, "Ok", StringComparison.OrdinalIgnoreCase))
        {
            return new WorldRouteDto
            {
                Found = false,
                Message = GetString(root, "message") switch
                {
                    { Length: > 0 } message => message,
                    _ => $"No {profile} route could be found for these coordinates."
                }
            };
        }

        var route = root.GetProperty("routes")[0];
        var result = new WorldRouteDto
        {
            Found = true,
            TotalDistanceKm = Math.Round(GetDouble(route, "distance") / 1000.0, 2),
            TotalDurationMinutes = Math.Round(GetDouble(route, "duration") / 60.0, 1),
            Message = "Live route calculated from the OpenStreetMap road network."
        };

        if (route.TryGetProperty("geometry", out var geometry) &&
            geometry.TryGetProperty("coordinates", out var coordinatesNode))
        {
            foreach (var point in coordinatesNode.EnumerateArray())
            {
                if (point.GetArrayLength() < 2) continue;

                result.Geometry.Add(new GeoPointDto
                {
                    Longitude = point[0].GetDouble(),
                    Latitude = point[1].GetDouble()
                });
            }
        }

        if (route.TryGetProperty("legs", out var legs))
        {
            foreach (var leg in legs.EnumerateArray())
            {
                if (!leg.TryGetProperty("steps", out var steps)) continue;

                foreach (var step in steps.EnumerateArray())
                {
                    var maneuver = step.TryGetProperty("maneuver", out var maneuverNode)
                        ? maneuverNode
                        : default;

                    var type = GetString(maneuver, "type");
                    var modifier = GetString(maneuver, "modifier");
                    var roadName = GetString(step, "name");

                    result.Steps.Add(new RouteStepDto
                    {
                        Instruction = BuildInstruction(type, modifier, roadName),
                        RoadName = string.IsNullOrWhiteSpace(roadName) ? "Unnamed road" : roadName,
                        DistanceKm = Math.Round(GetDouble(step, "distance") / 1000.0, 2),
                        DurationMinutes = Math.Round(GetDouble(step, "duration") / 60.0, 1)
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Lean OSRM fetch used by WorldGraphService to build one edge of a dynamic
    /// graph — distance, duration and route geometry only (no turn-by-turn steps).
    /// </summary>
    public async Task<(bool Found, double DistanceKm, double DurationMinutes, List<GeoPointDto> Geometry)> GetSegmentAsync(
        double lat1, double lon1, double lat2, double lon2, string profile, CancellationToken cancellationToken)
    {
        var normalizedProfile = profile.ToLowerInvariant() switch
        {
            "cycling" or "bike" => "cycling",
            "walking" or "foot" => "foot",
            _ => "driving"
        };

        var baseUrl = (_configuration["OpenStreetMap:OsrmUrl"]
                      ?? "https://router.project-osrm.org").TrimEnd('/');

        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var coordinates = $"{lon1.ToString(inv)},{lat1.ToString(inv)};{lon2.ToString(inv)},{lat2.ToString(inv)}";

        var url = $"{baseUrl}/route/v1/{normalizedProfile}/{coordinates}?overview=full&geometries=geojson";

        using var response = await _http.GetAsync(url, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var root = document.RootElement;
        if (!response.IsSuccessStatusCode ||
            !string.Equals(GetString(root, "code"), "Ok", StringComparison.OrdinalIgnoreCase))
        {
            return (false, 0, 0, new List<GeoPointDto>());
        }

        var route = root.GetProperty("routes")[0];
        var geometry = new List<GeoPointDto>();

        if (route.TryGetProperty("geometry", out var geometryNode) &&
            geometryNode.TryGetProperty("coordinates", out var coordinatesNode))
        {
            foreach (var point in coordinatesNode.EnumerateArray())
            {
                if (point.GetArrayLength() < 2) continue;
                geometry.Add(new GeoPointDto { Longitude = point[0].GetDouble(), Latitude = point[1].GetDouble() });
            }
        }

        return (
            true,
            Math.Round(GetDouble(route, "distance") / 1000.0, 2),
            Math.Round(GetDouble(route, "duration") / 60.0, 1),
            geometry
        );
    }

    private static string BuildInstruction(string type, string modifier, string roadName)
    {
        var road = string.IsNullOrWhiteSpace(roadName) ? "the road" : roadName;

        return type switch
        {
            "depart" => $"Start on {road}",
            "arrive" => "Arrive at your destination",
            "roundabout" or "rotary" => $"Enter the roundabout toward {road}",
            "merge" => $"Merge onto {road}",
            "on ramp" => $"Take the ramp toward {road}",
            "off ramp" => $"Take the exit toward {road}",
            "fork" => $"Keep {modifier} at the fork onto {road}",
            "new name" => $"Continue onto {road}",
            _ when !string.IsNullOrWhiteSpace(modifier) =>
                $"{Cap(modifier)} onto {road}",
            _ => $"Continue on {road}"
        };
    }

    private static string Cap(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private static void ValidateCoordinate(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(latitude), "Invalid latitude/longitude.");
    }

    private static string GetString(JsonElement node, string property)
    {
        if (node.ValueKind != JsonValueKind.Object ||
            !node.TryGetProperty(property, out var value))
            return string.Empty;

        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : value.ToString();
    }

    private static double GetDouble(JsonElement node, string property)
    {
        if (node.ValueKind != JsonValueKind.Object ||
            !node.TryGetProperty(property, out var value))
            return 0;

        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)
            ? number
            : double.TryParse(value.ToString(), out var parsed) ? parsed : 0;
    }
}
