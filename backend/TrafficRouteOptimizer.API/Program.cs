using TrafficRouteOptimizer.API.Services;

var builder = WebApplication.CreateBuilder(args);

// HTTP client for Nominatim / OSRM
builder.Services.AddHttpClient<WorldRoutingService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Worldwide routing + dynamic graph services
builder.Services.AddScoped<WorldGraphService>();

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "AtlasRoute — Intelligent Traffic Route Optimizer",
        Version = "v1",
        Description =
            "Worldwide OpenStreetMap routing with dynamic graphs, " +
            "Dijkstra, A*, traffic, tolls and road closures."
    });
});

// CORS
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
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint(
            "/swagger/v1/swagger.json",
            "AtlasRoute API v1");
    });
}

app.UseCors("AllowFrontend");

app.UseAuthorization();

app.MapControllers();

app.Run();