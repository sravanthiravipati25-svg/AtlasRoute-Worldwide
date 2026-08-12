using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Models;
using TrafficRouteOptimizer.API.Services;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                       ?? Environments.Production
});

// --------------------------------------------------
// Configuration
// Disable file watching / reloadOnChange for Render
// --------------------------------------------------
builder.Configuration.Sources.Clear();

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile(
        "appsettings.json",
        optional: false,
        reloadOnChange: false)
    .AddJsonFile(
        $"appsettings.{builder.Environment.EnvironmentName}.json",
        optional: true,
        reloadOnChange: false)
    .AddEnvironmentVariables();

// --------------------------------------------------
// HTTP Client
// Nominatim / OSRM
// --------------------------------------------------
builder.Services.AddHttpClient<WorldRoutingService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// --------------------------------------------------
// Worldwide Routing Services
// --------------------------------------------------
builder.Services.AddScoped<WorldGraphService>();

// --------------------------------------------------
// Controllers
// --------------------------------------------------
builder.Services.AddControllers();

// --------------------------------------------------
// Swagger
// --------------------------------------------------
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AtlasRoute — Intelligent Traffic Route Optimizer",
        Version = "v1",
        Description =
            "Worldwide OpenStreetMap routing with dynamic graphs, " +
            "Dijkstra, A*, traffic, tolls and road closures."
    });
});

// --------------------------------------------------
// CORS
// --------------------------------------------------
var allowedOrigins =
    builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

// --------------------------------------------------
// Build application
// --------------------------------------------------
var app = builder.Build();

// --------------------------------------------------
// Swagger
// --------------------------------------------------
app.UseSwagger();

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint(
        "/swagger/v1/swagger.json",
        "AtlasRoute API v1");
});

// --------------------------------------------------
// CORS
// IMPORTANT: before MapControllers()
// --------------------------------------------------
app.UseCors("AllowFrontend");

// --------------------------------------------------
// Authorization
// --------------------------------------------------
app.UseAuthorization();

// --------------------------------------------------
// Controllers
// --------------------------------------------------
app.MapControllers();

// --------------------------------------------------
// Start application
// --------------------------------------------------
app.Run();