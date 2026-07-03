using AwsCloudNative.Common.Constants;

namespace AwsCloudNative.Api.Extensions
{
    /// <summary>
    /// Registers EC2-specific services and HTTP client configuration
    /// for IMDS (Instance Metadata Service) v2 interaction.
    /// WHY a dedicated extension: IMDS calls require specific headers
    /// and a two-step token + metadata flow that must not be
    /// scattered across individual controllers or services.
    /// </summary>
    public static class Ec2Extensions
    {
        /// <summary>
        /// Registers a named HttpClient pre-configured for IMDSv2 calls.
        /// The client targets the IMDS base URL and handles the
        /// token acquisition pattern correctly.
        /// </summary>
        /// <param name="services">The DI (Dependency Injection) service collection.</param>
        public static IServiceCollection AddProductionEc2(
            this IServiceCollection services)
        {
            // Named HttpClient for IMDS calls.
            // WHY named client: IMDS has a specific base URL and short timeout
            // that must not be shared with general outbound HTTP clients.
            // A named client isolates these concerns cleanly.
            services.AddHttpClient(Ec2HttpClients.Imds, client =>
            {
                client.BaseAddress = new Uri(Ec2Constants.ImdsBaseUrl);

                // WHY 2 second timeout: IMDS is a local link-local endpoint.
                // If it does not respond in 2 seconds, the instance has a
                // serious networking issue — fail fast rather than wait.
                client.Timeout = TimeSpan.FromSeconds(2);
            });

            return services;
        }
    }

    /// <summary>
    /// Named HttpClient identifiers for EC2-related HTTP clients.
    /// WHY constants: IHttpClientFactory resolves clients by string name.
    /// Magic strings here cause silent failures — the factory returns
    /// a default unconfigured client instead of throwing an error.
    /// </summary>
    public static class Ec2HttpClients
    {
        /// <summary>Named client for IMDSv2 metadata endpoint calls.</summary>
        public const string Imds = "imds-v2";
    }
}
