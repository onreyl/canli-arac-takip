using Microsoft.EntityFrameworkCore;
using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Infrastructure.Persistence;

public sealed class VehicleTrackingDbContext(DbContextOptions<VehicleTrackingDbContext> options) : DbContext(options)
{
    public DbSet<VehiclePosition> VehiclePositions => Set<VehiclePosition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VehicleTrackingDbContext).Assembly);
    }
}
