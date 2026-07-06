namespace AwsCloudNative.Common.Constants
{
    /// <summary>
    /// Centralised constants for AWS Step Functions (SFN) state machine definitions,
    /// state names, and error types.
    /// WHY: State names referenced in both ASL JSON and .NET code must match exactly.
    /// A single character mismatch causes silent workflow routing failures.
    /// </summary>
    public static class StepFunctionsConstants
    {
        /// <summary>
        /// State machine name conventions.
        /// Pattern: {app}-{environment}-{workflow-name}
        /// Full name assembled at runtime using AwsEnvironmentContext.
        /// </summary>
        public static class StateMachines
        {
            /// <summary>Base name for the order processing workflow.</summary>
            public const string OrderProcessing = "order-processing";
        }

        /// <summary>
        /// Individual state names within the OrderProcessing state machine.
        /// These must match the state names in OrderWorkflow.asl.json exactly.
        /// PITFALL: Step Functions state names are case-sensitive.
        /// "ValidateOrder" and "validateOrder" are different states.
        /// </summary>
        public static class States
        {
            public const string ValidateOrder = "ValidateOrder";
            public const string ReserveInventory = "ReserveInventory";
            public const string ProcessPayment = "ProcessPayment";
            public const string SendConfirmation = "SendConfirmation";
            public const string CompensateInventory = "CompensateInventory";
            public const string OrderSucceeded = "OrderSucceeded";
            public const string OrderFailed = "OrderFailed";
        }

        /// <summary>
        /// ASL (Amazon States Language) built-in error codes used in Retry and Catch blocks.
        /// AWS defines these — they are not custom. Reference them in ASL JSON
        /// and in any .NET code that inspects execution failure causes.
        /// </summary>
        public static class Errors
        {
            /// <summary>Matches any error. Use in the last Catch block as a catch-all.</summary>
            public const string All = "States.ALL";

            /// <summary>State execution timed out per the TimeoutSeconds setting.</summary>
            public const string Timeout = "States.Timeout";

            /// <summary>Lambda invocation failed at the service level (throttle, service error).</summary>
            public const string LambdaServiceException = "Lambda.ServiceException";

            /// <summary>Lambda function itself threw an unhandled exception.</summary>
            public const string LambdaFunctionError = "Lambda.AWSLambdaException";

            /// <summary>Lambda invocation was throttled — too many concurrent executions.</summary>
            public const string LambdaThrottled = "Lambda.TooManyRequestsException";

            /// <summary>
            /// Custom error thrown by your Lambda business logic.
            /// Thrown when order validation fails — routes to OrderFailed state.
            /// </summary>
            public const string ValidationFailed = "OrderValidationFailed";

            /// <summary>
            /// Custom error thrown when payment processing fails.
            /// Routes to CompensateInventory state before OrderFailed.
            /// </summary>
            public const string PaymentFailed = "PaymentProcessingFailed";
        }

        /// <summary>
        /// Step Functions execution status values returned by the SDK.
        /// Used when polling execution status from the WorkflowController.
        /// </summary>
        public static class ExecutionStatus
        {
            public const string Running = "RUNNING";
            public const string Succeeded = "SUCCEEDED";
            public const string Failed = "FAILED";
            public const string TimedOut = "TIMED_OUT";
            public const string Aborted = "ABORTED";
        }
    }
}
