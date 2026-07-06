using AwsCloudNative.Workflows.Extensions;
namespace AwsCloudNative.Api.Extensions
{
    /// <summary>
    /// Registers Step Functions workflow services into the API's DI container.
    /// Delegates to the Workflows project extension method — the API only
    /// wires the dependency, it does not own the registration logic.
    /// </summary>
    public static class WorkflowClientExtensions
    {
        /// <summary>
        /// Adds IAmazonStepFunctions SDK client for workflow execution from the API layer.
        /// </summary>
        /// <param name="services">The DI service collection.</param>
        public static IServiceCollection AddProductionWorkflows(
            this IServiceCollection services)
        {
            services.AddProductionStepFunctions();
            return services;
        }
    }
}
