using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwsCloudNative.Common.Constants
{
    /// <summary>
    /// Centralised constants for ECS (Elastic Container Service) Fargate configuration.
    /// WHY: ECS metadata endpoint paths and environment variable keys used across
    /// health checks and diagnostics must never be magic strings in application code.
    /// </summary>
    public static class EcsConstants
    {
        /// <summary>
        /// Environment variables injected by AWS into every Fargate task container.
        /// Your application code reads these to understand its own execution context.
        /// You never set these — ECS sets them automatically.
        /// </summary>
        public static class EnvironmentVariables
        {
            /// <summary>
            /// URI (Uniform Resource Identifier) of the ECS task metadata v4 endpoint.
            /// Available only inside Fargate tasks — null on local dev machines.
            /// Used to detect whether the process is running inside ECS.
            /// Example value: http://169.254.170.2/v4
            /// </summary>
            public const string TaskMetadataUri = "ECS_CONTAINER_METADATA_URI_V4";

            /// <summary>
            /// The ECS cluster ARN (Amazon Resource Name) this task belongs to.
            /// Injected automatically by Fargate.
            /// </summary>
            public const string ClusterArn = "ECS_CLUSTER";
        }

        /// <summary>
        /// ECS task metadata endpoint paths relative to ECS_CONTAINER_METADATA_URI_V4.
        /// Querying these paths returns live runtime info about the running task.
        /// </summary>
        public static class MetadataEndpoints
        {
            /// <summary>
            /// Returns full task metadata: task ARN, cluster, container details, limits.
            /// GET {ECS_CONTAINER_METADATA_URI_V4}/task
            /// </summary>
            public const string Task = "/task";

            /// <summary>
            /// Returns container-level stats: CPU usage, memory usage, network I/O.
            /// GET {ECS_CONTAINER_METADATA_URI_V4}/stats
            /// </summary>
            public const string Stats = "/stats";
        }

        /// <summary>
        /// ECS service naming conventions for this solution.
        /// Pattern: {app}-{environment}-{service}
        /// </summary>
        public static class Services
        {
            /// <summary>Base name for the Orders API ECS service.</summary>
            public const string OrdersApi = "orders-api";
        }

        /// <summary>
        /// Health check configuration values.
        /// These must match the ALB Target Group health check settings exactly.
        /// PITFALL: A mismatch causes the ALB to mark every new task as
        /// unhealthy immediately after deployment, triggering an infinite restart loop.
        /// </summary>
        public static class HealthCheck
        {
            /// <summary>The path the ALB polls. Must match app.UseHealthChecks() path.</summary>
            public const string Path = "/health";

            /// <summary>HTTP port the ALB sends health check requests to.</summary>
            public const int Port = 8080;

            /// <summary>Seconds between ALB health check requests.</summary>
            public const int IntervalSeconds = 30;

            /// <summary>Consecutive failures before the ALB marks the target unhealthy.</summary>
            public const int UnhealthyThreshold = 3;

            /// <summary>
            /// Seconds ALB waits for a health check response before counting it as failed.
            /// Must be less than IntervalSeconds.
            /// </summary>
            public const int TimeoutSeconds = 5;
        }
    }
}
 