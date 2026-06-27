using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AwsCloudNative.Lambda.Models
{
    /// <summary>
    /// The input payload for the OrderProcessor Lambda function.
    /// Serialised as JSON by the Lambda runtime using System.Text.Json.
    /// WHY record: Immutable by default — Lambda inputs must never be
    /// mutated mid-execution as the same object could be reused in warm starts.
    /// </summary>
    public sealed record OrderRequest
    {
        /// <summary>
        /// Unique identifier for the order being processed.
        /// Used as the idempotency key — if the same OrderId arrives
        /// twice (Lambda retry), processing must be safe to repeat.
        /// </summary>
        [Required]
        public string OrderId { get; init; } = string.Empty;

        /// <summary>The customer placing the order.</summary>
        [Required]
        public string CustomerId { get; init; } = string.Empty;

        /// <summary>Total order amount in the account's base currency.</summary>
        [Range(0.01, double.MaxValue)]
        public decimal Amount { get; init; }

        /// <summary>UTC (Coordinated Universal Time) timestamp when the order was created.</summary>
        public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    }
}
