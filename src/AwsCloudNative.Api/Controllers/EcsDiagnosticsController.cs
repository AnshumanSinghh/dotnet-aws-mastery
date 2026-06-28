using AwsCloudNative.Common.Constants;
using Microsoft.AspNetCore.Mvc;

namespace AwsCloudNative.Api.Controllers
{
    /// <summary>
    /// Exposes ECS Fargate task runtime metadata for diagnostics.
    /// Useful for verifying which task revision is serving traffic
    /// and confirming correct subnet and cluster placement.
    /// PITFALL: Gate this behind internal-only access before production.
    /// Task ARNs and cluster names are not secrets but expose
    /// infrastructure topology — limit visibility accordingly.
    /// </summary>
    public sealed class EcsDiagnosticsController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<EcsDiagnosticsController> _logger;

        /// <summary>Initialises with HttpClientFactory for task metadata calls.</summary>
        public EcsDiagnosticsController(
            IHttpClientFactory httpClientFactory,
            ILogger<EcsDiagnosticsController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Returns ECS task metadata for the currently running container.
        /// Calls the ECS task metadata v4 endpoint — available only inside Fargate.
        /// Returns a local-dev notice when running outside ECS.
        ///
        /// The metadata includes:
        /// - Task ARN — uniquely identifies this running task instance
        /// - Cluster — which ECS cluster this task belongs to
        /// - Family — the task definition family name
        /// - Revision — the task definition revision currently running
        /// - Containers — image, CPU/memory limits, network interfaces
        /// </summary>
        [HttpGet("task-metadata")]
        public async Task<IActionResult> GetTaskMetadata(CancellationToken ct)
        {
            var metadataUri = Environment.GetEnvironmentVariable(
                EcsConstants.EnvironmentVariables.TaskMetadataUri);

            if (string.IsNullOrEmpty(metadataUri))
            {
                return Ok(new
                {
                    Environment = "local-dev",
                    Message = "ECS task metadata endpoint not available outside Fargate.",
                    Hint = "Deploy to ECS to see real task metadata here."
                });
            }

            var client = _httpClientFactory.CreateClient();
            var endpoint = $"{metadataUri}{EcsConstants.MetadataEndpoints.Task}";

            _logger.LogInformation(
                "Fetching ECS task metadata from {Endpoint}", endpoint);

            var response = await client.GetAsync(endpoint, ct);
            response.EnsureSuccessStatusCode();

            // Return raw metadata JSON from ECS.
            // In production, parse and return only the fields you need —
            // the full metadata contains internal ECS identifiers.
            var metadata = await response.Content.ReadAsStringAsync(ct);

            return Content(metadata, "application/json");
        }
    }
}
