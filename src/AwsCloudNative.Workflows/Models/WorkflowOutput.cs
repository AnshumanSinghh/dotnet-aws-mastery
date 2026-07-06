namespace AwsCloudNative.Workflows.Models
{
    /// <summary>
    /// Represents the result of a Step Functions execution —
    /// either a running status poll result or a completed execution outcome.
    /// </summary>
    public sealed record WorkflowOutput
    {
        /// <summary>The Step Functions execution ARN (Amazon Resource Name).</summary>
        public string ExecutionArn { get; init; } = string.Empty;

        /// <summary>
        /// Current execution status.
        /// One of: RUNNING, SUCCEEDED, FAILED, TIMED_OUT, ABORTED.
        /// </summary>
        public string Status { get; init; } = string.Empty;

        /// <summary>The OrderId used as the execution name.</summary>
        public string OrderId { get; init; } = string.Empty;

        /// <summary>UTC timestamp when the execution started.</summary>
        public DateTime StartedAtUtc { get; init; }

        /// <summary>
        /// UTC timestamp when the execution completed.
        /// Null if the execution is still RUNNING.
        /// </summary>
        public DateTime? CompletedAtUtc { get; init; }

        /// <summary>
        /// Error message if the execution FAILED or TIMED_OUT.
        /// PITFALL: Never return the raw Step Functions error cause to the client —
        /// it may contain internal ARNs, Lambda stack traces, or sensitive data.
        /// Sanitise before returning in the API response.
        /// </summary>
        public string? ErrorMessage { get; init; }
    }
}
