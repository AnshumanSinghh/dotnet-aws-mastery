using Amazon.StepFunctions;
using AwsCloudNative.Workflows.Models;
using Microsoft.Extensions.DependencyInjection;

using Amazon.StepFunctions.Model;
using AwsCloudNative.Common.Constants;
using AwsCloudNative.Common.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AwsCloudNative.Workflows.Extensions
{
    /// <summary>
    /// Extension methods for registering Step Functions services
    /// and executing state machine workflows.
    /// WHY separate from the API project: Workflow orchestration logic
    /// belongs in its own project — the API is only the HTTP entry point.
    /// Any consumer (background service, Lambda, CLI) can reference this
    /// project and execute workflows without the API layer.
    /// </summary>
    public static class StepFunctionsExtensions
    {
        /// <summary>
        /// Registers the IAmazonStepFunctions SDK client into the DI container.
        /// Uses the execution role credential chain from Phase 1 Track 1.
        /// </summary>
        /// <param name="services">The DI service collection.</param>
        public static IServiceCollection AddProductionStepFunctions(
            this IServiceCollection services)
        {
            // WHY AddAWSService: resolves region and credentials from
            // the DefaultAWSOptions registered in AddProductionAws (Track 1).
            // Zero explicit credential configuration needed.
            services.AddAWSService<IAmazonStepFunctions>();
            return services;
        }

        /// <summary>
        /// Starts a new OrderProcessing state machine execution.
        /// Uses the OrderId as the execution name for searchability in the AWS Console.
        ///
        /// WHY execution name = OrderId:
        /// Step Functions enforces unique execution names per state machine.
        /// Using OrderId makes executions findable by business key without
        /// storing a separate executionArn → orderId mapping.
        ///
        /// PITFALL: If you start two executions with the same name,
        /// Step Functions throws ExecutionAlreadyExists.
        /// This is your idempotency guard — the same order cannot be
        /// processed twice as long as the first execution is still running.
        /// </summary>
        /// <param name="sfnClient">The Step Functions SDK client.</param>
        /// <param name="input">The workflow input payload.</param>
        /// <param name="awsCtx">Resolved AWS environment context for ARN construction.</param>
        /// <param name="env">Current environment name (dev/staging/prod).</param>
        /// <param name="logger">Logger for execution tracing.</param>
        /// <param name="ct">Cancellation token.</param>
        public static async Task<WorkflowOutput> StartOrderWorkflowAsync(
            this IAmazonStepFunctions sfnClient,
            WorkflowInput input,
            AwsEnvironmentContext awsCtx,
            string env,
            ILogger logger,
            CancellationToken ct = default)
        {
            var stateMachineArn = awsCtx.StepFunctionsStateMachineArn(
                $"acn-{env}-{StepFunctionsConstants.StateMachines.OrderProcessing}");

            logger.LogInformation(
                "Starting workflow execution. OrderId={OrderId} StateMachineArn={Arn}",
                input.OrderId, stateMachineArn);

            var startRequest = new StartExecutionRequest
            {
                StateMachineArn = stateMachineArn,

                // Execution name = OrderId — searchable, idempotent, human-readable.
                Name = input.OrderId,

                // Serialise the full WorkflowInput as the state machine's initial input.
                // Every state receives this document unless filtered by InputPath/OutputPath.
                Input = JsonSerializer.Serialize(input)
            };

            var response = await sfnClient.StartExecutionAsync(startRequest, ct);

            logger.LogInformation(
                "Workflow execution started. ExecutionArn={ExecutionArn}",
                response.ExecutionArn);

            return new WorkflowOutput
            {
                ExecutionArn = response.ExecutionArn,
                Status = StepFunctionsConstants.ExecutionStatus.Running,
                OrderId = input.OrderId,
                StartedAtUtc = response.StartDate.ToUniversalTime()
            };
        }

        /// <summary>
        /// Polls the current status of a running or completed state machine execution.
        /// WHY poll instead of wait: Step Functions Standard Workflows can run for up to
        /// 1 year. Your API request must return immediately — never block waiting
        /// for a long-running workflow. Return the ExecutionArn and let the
        /// client poll or use a callback pattern (SNS/SQS) for completion notification.
        /// </summary>
        /// <param name="sfnClient">The Step Functions SDK client.</param>
        /// <param name="executionArn">The execution ARN returned by StartOrderWorkflowAsync.</param>
        /// <param name="ct">Cancellation token.</param>
        public static async Task<WorkflowOutput> GetExecutionStatusAsync(
        this IAmazonStepFunctions sfnClient,
        string executionArn,
        CancellationToken ct = default)
        {
            var response = await sfnClient.DescribeExecutionAsync(
            new DescribeExecutionRequest { ExecutionArn = executionArn }, ct);

            return new WorkflowOutput
            {
                ExecutionArn = response.ExecutionArn,
                Status = response.Status.Value,
                OrderId = response.Name,
                StartedAtUtc = response.StartDate.ToUniversalTime(),
                CompletedAtUtc = response.StopDate == default
                    ? null
                    : response.StopDate.ToUniversalTime(),

                // PITFALL: response.Cause may contain internal Lambda stack traces.
                // Never forward raw Cause to the client — log it internally only.
                ErrorMessage = response.Status == Amazon.StepFunctions.ExecutionStatus.FAILED
                    ? "Order processing failed. Reference execution ARN for details."
                    : null
            };
        }
    }
}
