using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace VehicleTracking.Infrastructure.Persistence;

internal sealed class DatabaseHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using NpgsqlCommand command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (NpgsqlException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy($"Veritabanına ulaşılamadı: {ex.Message}", ex);
        }
    }
}
