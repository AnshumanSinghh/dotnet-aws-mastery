namespace AwsCloudNative.Api.Extensions
{
    /// <summary>
    /// Registers the AWS Lambda client for direct function invocation
    /// from the Web API. Used by LambdaDiagnosticsController.
    /// WHY: The API can invoke Lambda directly via SDK for synchronous
    /// request/response flows — without going through API Gateway.
    /// </summary>
    public static class LambdaClientExtensions
    {
        /// <summary>
        /// Adds the IAmazonLambda SDK client to the DI container.
        /// Uses the execution role credential chain from Track 1.
        /// </summary>
        public static IServiceCollection AddProductionLambdaClient(
            this IServiceCollection services)
        {
            // IAmazonLambda — the SDK client for Lambda management and invocation.
            // WHY AddAWSService: Registers as Singleton with correct credential
            // chain and region resolution — same pattern as IAmazonSecurityTokenService.
            services.AddAWSService<IAmazonLambda>();
            return services;
        }
    }
}
