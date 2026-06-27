using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwsCloudNative.Common.Constants
{
    /// <summary>
    /// Centralised constants for AWS Lambda function configuration.
    /// WHY: Lambda function names and environment variable keys scattered
    /// as magic strings across invocation code cause silent failures
    /// when a function is renamed or moved between environments.
    /// </summary>
    public static class LambdaConstants
    {
        /// <summary>
        /// Lambda function names as deployed in AWS.
        /// Convention: {app}-{environment}-{purpose}
        /// These are resolved at runtime from AwsEnvironmentContext
        /// combined with these base names — never hardcoded with env suffix.
        /// </summary>
        public static class Functions
        {
            /// <summary>
            /// Order processor Lambda function base name.
            /// Full name is assembled at runtime:
            /// acn-{env}-order-processor
            /// </summary>
            public const string OrderProcessor = "order-processor";
        }

        /// <summary>
        /// Environment variable keys injected by AWS into every
        /// Lambda execution environment automatically.
        /// You never set these — AWS sets them. Read them to understand
        /// the execution context your function is running in.
        /// </summary>
        public static class EnvironmentVariables
        {
            /// <summary>The AWS region this Lambda is deployed in. Example: "ap-south-1"</summary>
            public const string AwsRegion = "AWS_REGION";

            /// <summary>The name of this Lambda function as registered in AWS.</summary>
            public const string FunctionName = "AWS_LAMBDA_FUNCTION_NAME";

            /// <summary>The memory limit configured for this function in MB (Megabytes).</summary>
            public const string FunctionMemorySize = "AWS_LAMBDA_FUNCTION_MEMORY_SIZE";

            /// <summary>
            /// The runtime identifier. Presence of this variable confirms
            /// the process is running inside an actual Lambda environment.
            /// </summary>
            public const string ExecutionEnv = "AWS_EXECUTION_ENV";

            /// <summary>
            /// The initialisation type of this Lambda container.
            /// "on-demand" = standard cold start.
            /// "snap-start" = restored from SnapStart snapshot.
            /// </summary>
            public const string InitType = "AWS_LAMBDA_INITIALIZATION_TYPE";
        }

        /// <summary>
        /// Standard Lambda invocation type values.
        /// Used when invoking Lambda from the AWS SDK.
        /// </summary>
        public static class InvocationTypes
        {
            /// <summary>
            /// Synchronous invocation — caller waits for the response.
            /// Use for request/response flows where the result is needed immediately.
            /// </summary>
            public const string RequestResponse = "RequestResponse";

            /// <summary>
            /// Asynchronous invocation — caller fires and forgets.
            /// Lambda executes in the background. No response is returned.
            /// Use for fire-and-forget event processing.
            /// </summary>
            public const string Event = "Event";
        }
    }
}
