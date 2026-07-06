using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using AwsCloudNative.Common.Constants;
using AwsCloudNative.Common.Services;
using AwsCloudNative.Workflows.Extensions;
using AwsCloudNative.Workflows.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace AwsCloudNative.Api.Controllers
{
    /// <summary>
    /// HTTP entry point for the OrderProcessing Step Functions workflow.
    /// The controller is deliberately thin — it starts executions and polls status.
    /// All workflow logic lives in ASL (OrderWorkflow.asl.json),
    /// not in this controller or in application code.
    ///
    /// WHY thin controller + Step Functions:
    /// The controller's job is to translate HTTP into a workflow execution.
    /// The workflow's job is to coordinate the business steps reliably.
    /// This separation means the business process survives API restarts,
    /// Lambda timeouts, and transient failures without application-level retry code.
    /// </summary>
    [ApiController]
    [Route("api/workflows")]
    [Authorize(Policy = AuthConstants.Policies.AuthenticatedUser)]
    public sealed class WorkflowController : ControllerBase
    {
        private readonly IAmazonStepFunctions _sfnClient;
        private readonly AwsEnvironmentContext _awsCtx;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WorkflowController> _logger;

        /// <summary>
        /// Initialises with Step Functions client, environment context,
        /// and configuration for environment name resolution.
        /// </summary>
        public WorkflowController(
            IAmazonStepFunctions sfnClient,
            AwsEnvironmentContext awsCtx,
            IConfiguration configuration,
            ILogger<WorkflowController> logger)
        {
            _sfnClient = sfnClient;
            _awsCtx = awsCtx;
            _configuration = configuration;
            _logger = logger;
        }


        /// <summary>
        /// Starts a new order processing workflow execution.
        /// Returns immediately with the ExecutionArn — does NOT wait for completion.
        ///
        /// WHY return immediately: Standard Workflows can run for minutes or hours.
        /// Blocking an HTTP request for workflow completion would exhaust
        /// connection pools and time out load balancers.
        /// The caller uses the ExecutionArn to poll GET /api/workflows/{executionArn}/status.
        ///
        /// PITFALL: If the same OrderId is submitted twice, Step Functions
        /// throws ExecutionAlreadyExists — handled below as 409 Conflict.
        /// This is intentional idempotency behaviour, not an error.
        /// </summary>
        [HttpPost("orders")]
        public async Task<IActionResult> StartOrderWorkflow(
        [FromBody] WorkflowInput input,
        CancellationToken ct)
        {
            var env = _configuration["ASPNETCORE_ENVIRONMENT"]?.ToLower() ?? "dev";

            try
            {
                var result = await _sfnClient.StartOrderWorkflowAsync(
                    input, _awsCtx, env, _logger, ct);

                // 202 Accepted — request received and workflow started.
                // Not 200 OK — the order is not yet processed, only initiated.
                return Accepted(new
                {
                    ExecutionArn = result.ExecutionArn,
                    OrderId = result.OrderId,
                    Status = result.Status,
                    StartedAtUtc = result.StartedAtUtc,
                    // Tell the caller exactly where to poll for completion status.
                    StatusUrl = Url.Action(
                        nameof(GetWorkflowStatus),
                        new { executionArn = Uri.EscapeDataString(result.ExecutionArn) })
                });
            }
            catch (ExecutionAlreadyExistsException)
            {
                // Idempotency: same OrderId submitted twice.
                // Return 409 Conflict — not 500. The caller can poll status
                // using the OrderId as the execution name.
                _logger.LogWarning(
                    "Workflow execution already exists for OrderId={OrderId}", input.OrderId);

                return Conflict(new
                {
                    Error = "A workflow execution for this OrderId already exists.",
                    OrderId = input.OrderId
                });
            }
        }

        /// <summary>
        /// Polls the status of a running or completed workflow execution.
        /// The executionArn is URL-encoded in the path — decode before use.
        ///
        /// Returns:
        ///   RUNNING   → workflow still in progress, poll again later
        ///   SUCCEEDED → order processed successfully
        ///   FAILED    → order processing failed, check ErrorMessage
        ///   TIMED_OUT → workflow exceeded maximum duration
        ///   ABORTED   → execution was manually stopped
        /// </summary>
        [HttpGet("orders/{executionArn}/status")]
        public async Task<IActionResult> GetWorkflowStatus(
        [FromRoute] string executionArn,
        CancellationToken ct)
        {
            var decodedArn = Uri.UnescapeDataString(executionArn);

            _logger.LogInformation(
                "Polling workflow status. ExecutionArn={ExecutionArn}", decodedArn);

            var result = await _sfnClient.GetExecutionStatusAsync(decodedArn, ct);

            return Ok(result);
        }
    }
}
