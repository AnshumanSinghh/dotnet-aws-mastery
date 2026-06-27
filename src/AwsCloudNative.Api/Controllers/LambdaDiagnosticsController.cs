using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.Model;
using AwsCloudNative.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace AwsCloudNative.Api.Controllers
{
    /// <summary>
    /// Demonstrates direct Lambda invocation from an ASP.NET Core API
    /// using the AWS SDK IAmazonLambda client.
    /// WHY direct invocation: Used for synchronous request/response flows
    /// where you need the Lambda result immediately in the same HTTP response.
    /// For fire-and-forget work, use SQS instead (Phase 4).
    /// </summary>
    [ApiController]
    [Route("api/diagnostics/lambda")]
    public sealed class LambdaDiagnosticsController : ControllerBase
    {
        private readonly IAmazonLambda _lambdaClient;
        private readonly ILambdaContext _awsCtx;
        private readonly ILogger<LambdaDiagnosticsController> _logger;

        /// <summary>Initialises controller with Lambda SDK client and environment context.</summary>
        public LambdaDiagnosticsController(
            IAmazonLambda lambdaClient,
            ILambdaContext awsCtx,
            ILogger<LambdaDiagnosticsController> logger)
        {
            _lambdaClient = lambdaClient;
            _awsCtx = awsCtx;
            _logger = logger;
        }

        /// <summary>
        /// Invokes the OrderProcessor Lambda function synchronously and returns its response.
        /// Demonstrates RequestResponse invocation type — caller waits for the result.
        ///
        /// PITFALL: Direct Lambda invocation from an API adds latency —
        /// your API's response time is now coupled to Lambda's execution time
        /// including any cold start. Consider async invocation (InvocationType=Event)
        /// for non-blocking work.
        /// </summary>
        [HttpPost("invoke-order-processor")]
        public async Task<IActionResult> InvokeOrderProcessor(
            [FromBody] object payload,
            CancellationToken ct)
        {
            var environmentName = "dev"; // hardcoded for now

            // Build the function name dynamically using AwsEnvironmentContext.
            // Pattern: acn-{env}-order-processor
            // WHY: Same code runs in dev (acn-dev-order-processor) and
            // prod (acn-prod-order-processor) without changes.
            var functionName = $"acn-{environmentName}-{LambdaConstants.Functions.OrderProcessor}";

            _logger.LogInformation(
                "Invoking Lambda function {FunctionName}", functionName);

            var request = new InvokeRequest
            {
                FunctionName = functionName,
                // RequestResponse = synchronous — we wait for the result.
                // Event = asynchronous — fire and forget, returns 202 immediately.
                InvocationType = LambdaConstants.InvocationTypes.RequestResponse,
                Payload = JsonSerializer.Serialize(payload)
            };

            var response = await _lambdaClient.InvokeAsync(request, ct);

            // WHY check FunctionError: Lambda can return HTTP 200 but still
            // indicate a function-level error via this header.
            // "Handled" = function threw an exception that was caught.
            // "Unhandled" = function crashed without catching the exception.
            if (!string.IsNullOrEmpty(response.FunctionError))
            {
                _logger.LogError(
               "Lambda invocation returned function error: {Error}",
               response.FunctionError);

                // PITFALL: Never forward the raw Lambda error payload to the client.
                // It may contain stack traces, internal ARNs, or sensitive config.
                return StatusCode(502, new { Error = "Function execution failed." });
            }

            // Deserialise the Lambda response payload.
            var resultJson = Encoding.UTF8.GetString(
                response.Payload.ToArray());

            _logger.LogInformation(
                "Lambda invocation successful. StatusCode={StatusCode}",
                response.StatusCode);

            return Ok(new
            {
                LambdaStatusCode = response.StatusCode,
                Result = JsonSerializer.Deserialize<object>(resultJson)
            });
        }
    }
}
