using System;
using System.Collections.Generic;
using System.Text;

namespace AwsCloudNative.Lambda.Models
{
    /// <summary>
    /// The output payload returned by the OrderProcessor Lambda function.
    /// </summary>
    public sealed record OrderResponse
    {
        /// <summary>The order ID echoed back to confirm which order was processed.</summary>
        public string OrderId { get; init; } = string.Empty;

        /// <summary>Processing status returned to the caller.</summary>
        public string Status { get; init; } = string.Empty;

        /// <summary>The Lambda function name that processed this order.
        /// Useful for tracing which function version handled a request.</summary>
        public string ProcessedBy { get; init; } = string.Empty;

        /// <summary>UTC timestamp when processing completed inside Lambda.</summary>
        public DateTime ProcessedAtUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Whether this was a cold start execution.
        /// WHY track this: Cold starts affect latency SLAs (Service Level Agreements).
        /// Logging this in production helps identify when to use SnapStart
        /// or Provisioned Concurrency.
        /// </summary>
        public bool WasColdStart { get; init; }
    }
}
