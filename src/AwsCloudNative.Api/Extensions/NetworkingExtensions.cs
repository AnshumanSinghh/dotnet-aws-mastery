using AwsCloudNative.Common.Options;

namespace AwsCloudNative.Api.Extensions
{
    /// <summary>
    /// Registers network-aware configuration and health check endpoints
    /// for VPC-deployed ASP.NET Core services.
    /// WHY: Services running in a private VPC subnet must correctly respond
    /// to Load Balancer health checks on the right port and path —
    /// otherwise the ALB (Application Load Balancer) marks the task
    /// as unhealthy and drains it immediately after deployment.
    /// </summary>
    public static class NetworkingExtensions
    {
        /// <summary>
        /// Binds NetworkOptions from IConfiguration and registers
        /// the ASP.NET Core health check middleware.
        /// The /health endpoint is what the ALB polls every 30 seconds.
        /// </summary>
        /// <param name="services">The DI (Dependency Injection) service collection.</param>
        public static IServiceCollection AddProductionNetworking(
            this IServiceCollection services)
        {
            // Bind VPC resource IDs from Parameter Store via IConfiguration.
            // These flow in through the AddProductionSecrets pipeline
            // registered in Track 3 — no additional SDK calls needed here.
            services.AddOptions<NetworkOptions>()
                .BindConfiguration(NetworkOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Register ASP.NET Core health checks.
            // The ALB health check target must match the path configured
            // in the ECS service Target Group settings.
            // PITFALL: If this path does not match what the ALB expects,
            // every new deployment will immediately be marked unhealthy
            // and roll back — even if the app is perfectly healthy.
            services.AddHealthChecks();

            return services;

        }

        /// <summary>
        /// Maps the /health endpoint used by the ALB health check.
        /// Must be called after app.UseRouting() in the middleware pipeline.
        /// PITFALL: Do not put [Authorize] on this endpoint.
        /// The ALB health check has no Bearer token — it will get 401
        /// and mark every task as unhealthy.
        /// </summary>
        /// <param name="app">The application builder.</param>
        public static IApplicationBuilder UseProductionNetworking(
            this IApplicationBuilder app)
        {
            app.UseHealthChecks("/health");
            return app;
        }
    }
}
