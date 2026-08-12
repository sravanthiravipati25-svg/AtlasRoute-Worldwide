using Microsoft.EntityFrameworkCore;
using TrafficRouteOptimizer.API.Data;
using TrafficRouteOptimizer.API.Services;

var builder = WebApplication.CreateBuilder(args);

var useInMemoryDb = builder.Configuration.GetValue<bool>("UseInMemoryDb");

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient<WorldRoutingService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddScoped<GraphService>();
builder.Services.AddScoped<IRouteService, RouteService>();
builder.Services.AddScoped<WorldGraphService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "AtlasRoute — Intelligent Traffic Route Optimizer",
        Version = "v1",
        Description = "Worldwide OpenStreetMap routing plus Dijkstra, A* and BFS graph algorithms."
    });
});

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (useInMemoryDb)
    {
        context.Database.EnsureCreated();
        SeedData.EnsureSeeded(context, app.Environment);
    }
    else
    {
        context.Database.Migrate();
        SeedData.EnsureSeeded(context, app.Environment);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "AtlasRoute API v1"));
}

app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

app.Run();
