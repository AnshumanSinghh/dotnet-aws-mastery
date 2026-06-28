using AwsCloudNative.Api.HealthChecks;

namespace AwsCloudNative.Api.Extensions
{
    /// <summary>
    /// Registers ECS Fargate-specific concerns into the DI container:
    /// health checks, graceful shutdown timeout, and task metadata awareness.
    /// </summary>
    public static class EcsExtensions
    {
        /// <summary>
        /// Adds ECS-aware health checks and configures the host shutdown timeout.
        ///
        /// WHY configure shutdown timeout:
        /// When ECS sends SIGTERM during a rolling deploy, ASP.NET Core starts
        /// shutting down. The default graceful shutdown window is 5 seconds.
        /// The ALB deregistration delay is 30 seconds by default.
        /// If your API has long-running requests (> 5s), they get cut off.
        /// Setting ShutdownTimeout to match the deregistration delay ensures
        /// in-flight requests complete before the process exits.
        ///
        /// PITFALL: ShutdownTimeout must always be less than the ECS
        /// task stop timeout (default 30s). ECS sends SIGKILL after 30s
        /// regardless of what your application is doing.
        /// Recommended: ShutdownTimeout = 25s, ECS stop timeout = 30s.
        /// </summary>
        public static IServiceCollection AddProductionEcs(
            this IServiceCollection services) 
        {
            // Register custom ECS health check alongside the default liveness check.
            // Tags allow the ALB to target only "live" checks and internal
            // monitoring to query "ready" checks separately.
            services.AddHealthChecks()
                .AddCheck<EcsTaskHealthCheck>(
                    name: "ecs-task-metadata",
                    tags: ["live", "ecs"]);

            // Configure graceful shutdown window.
            // WHY 25 seconds: ECS stop timeout is 30s. We leave a 5s buffer
            // for the process to flush logs and close connections after
            // the host has stopped accepting new requests.
            services.Configure<HostOptions>(options => 
            { 
                options.ShutdownTimeout = TimeSpan.FromSeconds(25);
            });

            return services;
        }
    }
}
