using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;
using AwsCloudNative.Lambda.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
// WHY assembly-level attribute: Tells the Lambda runtime which JSON
// serialiser to use for all functions in this assembly.
// System.Text.Json is faster and lighter than Newtonsoft.Json.
// PITFALL: If you mix serialisers across functions, you will get
// silent deserialisation failures on complex types.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
namespace AwsCloudNative.Lambda.Functions
{
    /// <summary>
    /// Processes incoming order events.
    /// Demonstrates the Lambda Annotations pattern with full DI support.
    ///
    /// WHY Lambda Annotations: The framework generates the Lambda entry point
    /// boilerplate via source generation. You write clean, testable methods
    /// with constructor-injected dependencies — identical to ASP.NET Core.
    ///
    /// PITFALL — Concurrency model: Lambda scales by creating multiple
    /// containers in parallel. Each container runs one invocation at a time.
    /// Do NOT use shared static mutable state — each container is isolated,
    /// but within one container, Singleton services persist across warm invocations.
    /// </summary>
    public class OrderProcessorFunction
    {
        private readonly ILogger<OrderProcessorFunction> _logger;

        // WHY track cold start: Helps identify latency spikes in production.
        // Static field persists across warm invocations on the same container.
        // On a cold start this is false initially, then set to true after first run.
        private static bool _isWarmStart = false;

        /// <summary>
        /// Constructor runs during the cold start INIT phase.
        /// All DI-injected services are resolved here — once per container lifetime.
        /// </summary>
        public OrderProcessorFunction(ILogger<OrderProcessorFunction> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Processes a single order request.
        ///
        /// [LambdaFunction] — marks this method as a Lambda entry point.
        /// Source generator creates the Lambda handler bootstrap code.
        ///
        /// [HttpApi] — wires this function to an API Gateway HTTP API event.
        /// Route: POST /orders
        /// This attribute is used in Track 2 (API Gateway integration).
        ///
        /// ILambdaContext — AWS-provided context object containing:
        ///   - RemainingTime: how long before Lambda forcefully terminates this invocation
        ///   - RequestId: unique ID for this specific invocation (use for tracing)
        ///   - FunctionName, MemoryLimitInMB, LogGroupName
        /// </summary>
        /// <param name="request">The order payload deserialised from the event JSON.</param>
        /// <param name="lambdaContext">AWS Lambda execution context for this invocation.</param>
        [LambdaFunction(ResourceName = "OrderProcessorFunction")]
        [HttpApi(LambdaHttpMethod.Post, "/orders")]
        public async Task<OrderResponse> ProcessOrder(
            [FromBody] OrderRequest request,
            ILambdaContext lambdaContext)
        {
            var wasColdStart = !_isWarmStart;
            _isWarmStart = true; // mark warm for subsequent invocations on this container

            // WHY log RemainingTime: If processing is slow and approaches the
            // Lambda timeout, you want to know before the hard kill arrives.
            // Default timeout is 3 seconds — increase in Lambda config for heavy work.
            _logger.LogInformation(
                "Processing order. OrderId={OrderId} CustomerId={CustomerId} " +
                "RemainingTime={RemainingTime}ms ColdStart={ColdStart} RequestId={RequestId}",
                request.OrderId,
                request.CustomerId,
                lambdaContext.RemainingTime.TotalMilliseconds,
                wasColdStart,
                lambdaContext.AwsRequestId);

            // Simulate processing work.
            // In a real function this would call DynamoDB, SQS, or RDS.
            await Task.Delay(50);

            var response = new OrderResponse
            {
                OrderId = request.OrderId,
                Status = "Processed",
                ProcessedBy = lambdaContext.FunctionName,
                ProcessedAtUtc = DateTime.UtcNow,
                WasColdStart = wasColdStart
            };

            _logger.LogInformation(
                "Order processed successfully. OrderId={OrderId} Status={Status}",
                response.OrderId,
                response.Status);

            return response;
        }
    }
}
