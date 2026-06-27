using AwsCloudNative.Common.Constants;
using Microsoft.AspNetCore.Mvc;

namespace AwsCloudNative.Api.Controllers
{
    /// <summary>
    /// Demonstrates calling the deployed API Gateway HTTP API endpoint
    /// from the ASP.NET Core Web API — simulating a service-to-service call.
    /// WHY: In a microservices architecture, one service often calls another
    /// via its API Gateway URL. This shows the correct pattern including
    /// Bearer token forwarding and correlation ID propagation.
    /// PITFALL: Never hardcode the API Gateway URL — resolve from Parameter Store.
    /// </summary>
    [ApiController]
    [Route("api/diagnostics/apigateway")]
    public sealed class ApiGatewayDiagnosticsController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ApiGatewayDiagnosticsController> _logger;

        /// <summary>
        /// WHY IHttpClientFactory: Never instantiate HttpClient directly.
        /// IHttpClientFactory manages connection pooling and HttpMessageHandler
        /// lifetimes correctly — raw HttpClient disposal causes socket exhaustion.
        /// </summary>
        public ApiGatewayDiagnosticsController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ApiGatewayDiagnosticsController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Calls the deployed API Gateway /health endpoint and returns the result.
        /// No auth token required — /health is an open route in the SAM template.
        /// Use this to verify the API Gateway deployment is live and responding.
        /// </summary>
        [HttpGet("health")]
        public async Task<IActionResult> CheckApiGatewayHealth(CancellationToken ct)
        {
            // Resolve API Gateway base URL from Parameter Store via IConfiguration.
            // In appsettings.json (local dev) this is a placeholder.
            // In production it is overridden by Parameter Store at startup.
            var baseUrl = _configuration["ApiGateway:BaseUrl"];

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return BadRequest(new { Error = "ApiGateway:BaseUrl is not configured." });
            }

            var correlationId = Guid.NewGuid().ToString();
            var client = _httpClientFactory.CreateClient();

            // Propagate correlation ID so the Lambda log entry for this
            // call can be traced back to this originating API request.
            client.DefaultRequestHeaders.Add(
                ApiGatewayConstants.Headers.CorrelationId, correlationId);

            _logger.LogInformation(
                "Calling API Gateway health endpoint. BaseUrl={BaseUrl} CorrelationId={CorrelationId}",
                baseUrl, correlationId);

            var response = await client.GetAsync($"{baseUrl}/health", ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            return Ok(new
            {
                ApiGatewayStatusCode = (int)response.StatusCode,
                CorrelationId = correlationId,
                Response = body
            });
        }
    }
}
