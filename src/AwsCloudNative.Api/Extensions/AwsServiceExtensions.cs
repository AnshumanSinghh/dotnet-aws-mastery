//using Amazon.Extensions.NETCore.Setup;
using Amazon.SecurityToken;
using AwsCloudNative.Common.Options;

namespace AwsCloudNative.Api.Extensions
{
    /// <summary>
    /// WHY: Extension method pattern keeps Program.cs clean.
    /// All AWS DI wiring lives here, not scattered across the host file.
    /// OBJECTIVE: Register the AWS SDK correctly for both local dev
    /// and production (Lambda/ECS) without any environment branching.
    /// PITFALL: Never call `new AmazonSTSClient("key", "secret")`.
    /// Always let the credential chain resolve automatically.
    /// </summary>
    public static class AwsServiceExtensions
    {
        public static IServiceCollection AddProductionAws(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Bind and validate AwsOptions at startup.
            // If "Aws:Region" is missing from config, the app fails
            // immediately with a clear error — not silently at first request.
            services.AddOptions<AwsOptions>()
                .BindConfiguration(AwsOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Register the AWS SDK config using AWSSDK.Extensions.NETCore.Setup.
            // This reads "Aws:Region" and "Aws:Profile" from IConfiguration
            // automatically. In Lambda/ECS there is no profile — SDK falls
            // through to the execution role/task role credentials automatically.
            services.AddDefaultAWSOptions(configuration.GetAWSOptions());

            // Register the STS client for AssumeRole operations and diagnostics.
            // WHY IAmazonSecurityTokenService: STS is used to verify which
            // identity your code is currently running as (GetCallerIdentity).
            // In production it is also used for cross-account AssumeRole flows.
            services.AddAWSService<IAmazonSecurityTokenService>();

            return services;
        }
    }
}
