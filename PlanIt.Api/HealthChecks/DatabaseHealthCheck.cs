using Microsoft.Extensions.Diagnostics.HealthChecks;
using PlanIt.Api.Data;

namespace PlanIt.Api.HealthChecks;

// Confirms the API is up and the DB is reachable, for Azure deployment health probes
// (planit-system-design-architecture.md §8).
public class DatabaseHealthCheck(PlanItDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Cannot connect to the database.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database health check threw an exception.", ex);
        }
    }
}
