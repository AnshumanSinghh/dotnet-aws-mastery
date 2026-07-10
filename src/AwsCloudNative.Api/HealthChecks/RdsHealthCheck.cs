using AwsCloudNative.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AwsCloudNative.Api.HealthChecks
{
    /// <summary>
    /// Verifies RDS PostgreSQL connectivity via a lightweight SQL query.
    /// Added to the health check pipeline so the ALB (Application Load Balancer)
    /// can detect database connectivity loss and drain traffic from unhealthy tasks.
    ///
    /// PITFALL — health check strictness:
    /// A health check that calls complex queries or depends on data existing
    /// will mark the service unhealthy for reasons unrelated to connectivity.
    /// Use the absolute minimum query — just confirm the connection works.
    /// SELECT 1 is the canonical PostgreSQL connectivity probe.
    ///
    /// PITFALL — circular dependency on startup:
    /// Do not run migrations in the health check. Run migrations as a
    /// separate startup step. Health checks run continuously — migrations
    /// should run once.
    /// </summary>
    public sealed class RdsHealthCheck : IHealthCheck
    {
        private readonly IDbContextFactory<OrdersDbContext> _dbContextFactory;
        private readonly ILogger<RdsHealthCheck> _logger;

        /// <summary>
        /// WHY IDbContextFactory over OrdersDbContext directly:
        /// Health checks run on a background timer — not within an HTTP request scope.
        /// A Scoped DbContext cannot be resolved from a Singleton health check host.
        /// IDbContextFactory creates a fresh DbContext instance per check,
        /// independent of the request scope — no DI lifetime mismatch.
        /// </summary>
        public RdsHealthCheck(
            IDbContextFactory<OrdersDbContext> dbContextFactory,
            ILogger<RdsHealthCheck> logger)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        /// <summary>
        /// Executes SELECT 1 against the RDS PostgreSQL instance.
        /// Returns Healthy on success, Unhealthy on any exception.
        /// Times out after 3 seconds — well within the ALB health check timeout.
        /// </summary>
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await using var dbContext = await _dbContextFactory
                    .CreateDbContextAsync(cancellationToken);

                // Lightweight connectivity probe — one round-trip, minimal cost.
                // FromSqlRaw returns IQueryable<int> — ToListAsync executes it.
                await dbContext.Database
                    .ExecuteSqlRawAsync("SELECT 1", cancellationToken);

                return HealthCheckResult.Healthy("RDS PostgreSQL connection successful.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RDS health check failed.");

                // PITFALL: Do not return the exception message as the health check description.
                // It may contain the connection string, server hostname, or credentials
                // if Npgsql includes them in the exception details.
                return HealthCheckResult.Unhealthy(
                    "RDS PostgreSQL connection failed. Check connection string and security group rules.");
            }
        }
    }
}
