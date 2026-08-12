using Microsoft.EntityFrameworkCore;
using TrafficRouteOptimizer.API.Models;

namespace TrafficRouteOptimizer.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Location> Locations => Set<Location>();
    public DbSet<RoadSegment> RoadSegments => Set<RoadSegment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Location>()
            .HasIndex(l => l.Name);

        modelBuilder.Entity<RoadSegment>()
            .HasOne(r => r.FromLocation)
            .WithMany(l => l.OutgoingSegments)
            .HasForeignKey(r => r.FromLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RoadSegment>()
            .HasOne(r => r.ToLocation)
            .WithMany(l => l.IncomingSegments)
            .HasForeignKey(r => r.ToLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RoadSegment>()
            .Property(r => r.Congestion)
            .HasConversion<string>();

        base.OnModelCreating(modelBuilder);
    }
}
