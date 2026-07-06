using System.ComponentModel.DataAnnotations;

namespace AwsCloudNative.Workflows.Models
{
    /// <summary>
    /// Input payload for the OrderProcessing state machine execution.
    /// Serialised as JSON and passed as the initial input to the state machine.
    /// Every state in the workflow receives this document (or a filtered version).
    ///
    /// WHY record: Workflow inputs are immutable data contracts.
    /// Step Functions stores the input verbatim in execution history —
    /// mutable objects could produce inconsistent audit logs.
    /// </summary>
    public sealed record WorkflowInput
    {
        /// <summary>
        /// Unique order identifier. Used as the Step Functions execution name.
        /// WHY as execution name: Makes executions searchable by order ID in
        /// the AWS Console and via SDK without needing a separate lookup.
        /// Execution names must be unique per state machine — OrderId achieves this.
        /// PITFALL: Execution names have a 80-character limit and must be
        /// alphanumeric plus hyphens only. Validate before starting execution.
        /// </summary>
        [Required]
        [MaxLength(80)]
        [RegularExpression(@"^[a-zA-Z0-9\-]+$",
        ErrorMessage = "ExecutionName must be alphanumeric with hyphens only.")]
        public string OrderId { get; init; } = string.Empty;

        /// <summary>Customer placing the order.</summary>
        [Required]
        public string CustomerId { get; init; } = string.Empty;

        /// <summary>Order amount in the account base currency.</summary>
        [Range(0.01, double.MaxValue)]
        public decimal Amount { get; init; }

        /// <summary>
        /// Idempotency key for payment processing.
        /// Passed to the payment Lambda to prevent double charges
        /// if the state is retried after a transient failure.
        /// </summary>
        public string IdempotencyKey { get; init; } = Guid.NewGuid().ToString();

        /// <summary>UTC timestamp when the workflow was initiated.</summary>
        public DateTime InitiatedAtUtc { get; init; } = DateTime.UtcNow;
    }
}
