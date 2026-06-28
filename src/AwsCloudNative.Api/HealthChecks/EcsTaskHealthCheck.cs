using AwsCloudNative.Common.Constants;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AwsCloudNative.Api.HealthChecks
{
    /// <summary>
    /// Custom health check that verifies this process is running correctly
    /// inside an ECS Fargate task and reports key runtime metadata.
    ///
    /// WHY a custom health check beyond the default:
    /// The default ASP.NET Core health check returns "Healthy" as long as
    /// the process is alive. A custom check can verify deeper concerns —
    /// is the ECS metadata endpoint reachable? Are downstream dependencies up?
    ///
    /// PITFALL: Never make this check too strict.
    /// If your health check calls DynamoDB and DynamoDB has a blip,
    /// ECS will mark your task unhealthy and kill it — making an outage worse.
    /// Health checks must only verify what THIS PROCESS needs to serve traffic.
    /// </summary>
    public sealed class EcsTaskHealthCheck : IHealthCheck
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<EcsTaskHealthCheck> _logger;

        public EcsTaskHealthCheck(
            IHttpClientFactory httpClientFactory,
            ILogger<EcsTaskHealthCheck> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Checks whether the ECS task metadata endpoint is reachable.
        /// On local dev this returns Healthy with a "running locally" note —
        /// the metadata endpoint does not exist outside ECS.
        /// Inside ECS — confirms the Fargate task networking is healthy.
        /// </summary>
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, 
            CancellationToken cancellationToken = default)
        {
            var metadataUri = Environment.GetEnvironmentVariable(
            EcsConstants.EnvironmentVariables.TaskMetadataUri);

            // Not running in ECS — local dev. Report healthy with context.
            if (string.IsNullOrEmpty(metadataUri))
            {
                return HealthCheckResult.Healthy(
                    "Running outside ECS — metadata endpoint not available locally.");
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                var endpoint = $"{metadataUri}{EcsConstants.MetadataEndpoints.Task}";

                // WHY short timeout: Health checks must respond within
                // EcsConstants.HealthCheck.TimeoutSeconds (5s).
                // If the metadata call takes 4s, we have no time left
                // for the HTTP response to reach the ALB.
                using var cts = CancellationTokenSource
                    .CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(2));

                var response = await client.GetAsync(endpoint, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    return HealthCheckResult.Healthy(
                        "ECS task metadata endpoint reachable.");
                }

                _logger.LogWarning(
                    "ECS metadata endpoint returned {StatusCode}", response.StatusCode);

                return HealthCheckResult.Degraded(
                    $"ECS metadata endpoint returned {(int)response.StatusCode}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                               "ECS metadata endpoint unreachable");

                return HealthCheckResult.Unhealthy(
                    "ECS metadata endpoint unreachable.", ex);
            }
        }
    }
}
