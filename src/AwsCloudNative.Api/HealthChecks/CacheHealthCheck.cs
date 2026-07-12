using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace AwsCloudNative.Api.HealthChecks
{
    /// <summary>
    /// Verifies ElastiCache Valkey/Redis connectivity via a PING command.
    /// PING is the lightest possible health probe — one round-trip,
    /// no data read/write, no memory allocation on the Redis server.
    ///
    /// WHY PING over a SET/GET probe:
    /// A SET/GET probe consumes a key slot and write capacity.
    /// PING simply confirms the connection and server responsiveness —
    /// exactly what a health check needs.
    /// </summary>
    public sealed class CacheHealthCheck : IHealthCheck
    {
        private readonly IConnectionMultiplexer _multiplexer;
        private readonly ILogger<CacheHealthCheck> _logger;

        /// <summary>
        /// Initialises with the Singleton IConnectionMultiplexer.
        /// No factory needed — multiplexer is safe to use from any lifetime.
        /// </summary>
        public CacheHealthCheck(
            IConnectionMultiplexer multiplexer,
            ILogger<CacheHealthCheck> logger)
        {
            _multiplexer = multiplexer;
            _logger = logger;
        }

        /// <summary>
        /// Sends a PING to the ElastiCache server and awaits PONG.
        /// Reports the round-trip latency as health check data.
        /// Useful for detecting latency degradation before it affects users.
        /// </summary>
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var db = _multiplexer.GetDatabase();
                var start = DateTime.UtcNow;

                await db.PingAsync();

                var latency = DateTime.UtcNow - start;

                _logger.LogDebug(
                    "Cache health check passed. Latency={LatencyMs}ms",
                    latency.TotalMilliseconds);

                // Include latency in health check data for monitoring dashboards.
                var data = new Dictionary<string, object>
                {
                    ["latency_ms"] = latency.TotalMilliseconds
                };

                // Warn if PING latency exceeds 50ms — indicates network
                // or ElastiCache performance degradation.
                return latency.TotalMilliseconds > 50
                    ? HealthCheckResult.Degraded(
                        $"Cache responding slowly. Latency={latency.TotalMilliseconds:F1}ms",
                        data: data)
                    : HealthCheckResult.Healthy(
                        $"Cache healthy. Latency={latency.TotalMilliseconds:F1}ms",
                        data: data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache health check failed.");

                return HealthCheckResult.Unhealthy(
                    "ElastiCache connection failed. Check security group and endpoint configuration.",
                    ex);
            }
        }
    }
}
