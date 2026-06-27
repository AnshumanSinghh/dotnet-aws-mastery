using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using AwsCloudNative.Common.Constants;
using AwsCloudNative.Lambda.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

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
    /// Processes incoming order events via API Gateway HTTP API.
    /// Demonstrates the Lambda Annotations pattern with full DI support.
    /// <para>
    /// <b>Why Lambda Annotations:</b> The framework generates the Lambda entry point
    /// boilerplate via source generation. You write clean, testable methods
    /// with constructor-injected dependencies — identical to ASP.NET Core.
    /// </para>
    /// <para>
    /// <b>Pitfall — Concurrency model:</b> Lambda scales by creating multiple
    /// containers in parallel. Each container runs one invocation at a time.
    /// Do NOT use shared static mutable state — each container is isolated,
    /// but within one container, Singleton services persist across warm invocations.
    /// </para>
    /// <para>
    /// ========== PHASE 2 Track 2 ================
    /// </para>
    /// <para>
    /// <b>Why IHttpResult return type:</b> When Lambda is behind API Gateway HTTP API
    /// (Payload Format Version 2.0), returning IHttpResult lets you control
    /// the exact HTTP status code, headers, and body of the response.
    /// Returning a plain object works too but gives you no control over status codes.
    /// </para>
    /// <para>
    /// <b>Pitfall — Payload format version mismatch:</b>
    /// If your SAM template sets PayloadFormatVersion: "1.0" but your
    /// function uses HttpResults (v2.0 format), API Gateway will return
    /// a mangled response. Always match the payload version in both places.
    /// </para>
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
        /// Processes a single incoming order request forwarded by API Gateway.
        /// <para>
        /// <b>[LambdaFunction]:</b> Marks this method as a Lambda entry point. 
        /// The source generator uses this to create the Lambda handler bootstrap code.
        /// </para>
        /// <para>
        /// <b>[HttpApi(LambdaHttpMethod.Post, "/orders")]:</b> Wires this function to an API Gateway HTTP API route/event. 
        /// Route: <c>POST /orders</c>. This attribute is used in Track 2 (API Gateway integration).
        /// </para>
        /// <para>
        /// <b>The APIGatewayHttpApiV2ProxyRequest</b> gives access to the full API Gateway event, 
        /// including JWT claims from the authoriser.
        /// 
        /// </para>
        /// <para>
        /// <b>ILambdaContext / Context:</b> AWS-provided context object containing:
        /// <list type="bullet">
        /// <item><description>RemainingTime: how long before Lambda forcefully terminates this invocation.</description></item>
        /// <item><description>RequestId: unique ID for this specific invocation (use for tracing).</description></item>
        /// <item><description>FunctionName, MemoryLimitInMB, LogGroupName.</description></item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="request">The deserialised order payload from the request body/event JSON.</param>
        /// <param name="apiGatewayEvent">
        /// The raw API Gateway proxy event. 
        /// Contains headers, JWT claims, requestContext, and route info.
        /// </param>
        /// <param name="lambdaContext">AWS Lambda execution context for this invocation (remaining time, request ID).</param>
        [LambdaFunction(ResourceName = "OrderProcessorFunction")]
        [HttpApi(LambdaHttpMethod.Post, "/orders")]
        public async Task<IHttpResult> ProcessOrder(
            [FromBody] OrderRequest request,
            APIGatewayHttpApiV2ProxyRequest apiGatewayEvent,
            ILambdaContext lambdaContext)
        {
            var wasColdStart = !_isWarmStart;
            _isWarmStart = true; // mark warm for subsequent invocations on this container

            // Extract the caller's identity from JWT claims forwarded by API Gateway.
            // WHY read from requestContext: API Gateway has already validated the JWT.
            // The claims here are trusted — no re-validation needed in Lambda.
            // PITFALL: These claims are only present if a JWT authoriser is configured
            // on the route in the SAM template. Without it, this dictionary is empty.
            var claims = apiGatewayEvent.RequestContext.Authorizer?.Jwt?.Claims as IReadOnlyDictionary<string, string>
                            ?? new Dictionary<string, string>();
            var callerId = claims.GetValueOrDefault(ApiGatewayConstants.JwtClaims.Subject, "anonymous");
            var callerEmail = claims.GetValueOrDefault(ApiGatewayConstants.JwtClaims.Email, "unknown");

            // Correlation ID — passed by API Gateway from the X-Correlation-Id header
            // or generated from the Lambda request ID if not provided.
            // WHY: Allows tracing a single request across API Gateway logs,
            // Lambda logs, and any downstream service calls.
            var correlationId = (apiGatewayEvent.Headers as IReadOnlyDictionary<string, string>)
                ?.GetValueOrDefault(ApiGatewayConstants.Headers.CorrelationId)
                ?? lambdaContext.AwsRequestId;

            // WHY log RemainingTime: If processing is slow and approaches the
            // Lambda timeout, you want to know before the hard kill arrives.
            // Default timeout is 3 seconds — increase in Lambda config for heavy work.
            _logger.LogInformation(
                "Processing order. OrderId={OrderId} CallerId={CallerId} " +
                "CorrelationId={CorrelationId} ColdStart={ColdStart} " +
                "RemainingTime={RemainingTime}ms",
                request.OrderId,
                callerId,
                correlationId,
                wasColdStart,
                lambdaContext.RemainingTime.TotalMilliseconds);

            // Basic idempotency guard.
            // WHY: API Gateway and Lambda retry on failures. The same OrderId
            // could arrive more than once. In production this check would hit
            // DynamoDB to confirm the order was not already processed.
            if (string.IsNullOrWhiteSpace(request.OrderId))
            {
                _logger.LogWarning("Order rejected — missing OrderId");

                // IHttpResult lets you return exact HTTP status codes from Lambda.
                // HttpResults.BadRequest maps to HTTP 400.
                return HttpResults.BadRequest(JsonSerializer.Serialize(new
                {
                    Error = "OrderId is required."
                }));
            }

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
                "Order processed. OrderId={OrderId} CorrelationId={CorrelationId}",
                response.OrderId,
                correlationId);

            // Return HTTP 200 with correlation ID header for end-to-end tracing.
            return HttpResults.Ok(response)
                .AddHeader(ApiGatewayConstants.Headers.CorrelationId, correlationId)
                .AddHeader(ApiGatewayConstants.Headers.ContentType,
                           ApiGatewayConstants.Headers.ApplicationJson);
        }

        /// <summary>
        /// Public health check route — no JWT authoriser on this route.
        /// API Gateway calls this to verify the Lambda is responding.
        /// PITFALL: Never put business logic or auth checks here.
        /// The ALB (Application Load Balancer) and API Gateway
        /// health checks must always return 200 without credentials.
        /// </summary>
        [LambdaFunction(ResourceName = "OrderHealthFunction")]
        [HttpApi(LambdaHttpMethod.Get, "/health")]
        public IHttpResult Health(ILambdaContext context)
        {
            return HttpResults.Ok(new
            {
                Status = "Healthy",
                Function = context.FunctionName,
                WarmStart = _isWarmStart,
                TimestampUtc = DateTime.UtcNow
            });
        }
    }
}
