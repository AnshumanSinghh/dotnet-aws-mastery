using Amazon.S3;
using Amazon.S3.Transfer;
using AwsCloudNative.Common.Constants;
using AwsCloudNative.Common.Options;

namespace AwsCloudNative.Api.Extensions
{
    /// <summary>
    /// Registers Amazon S3 SDK client and TransferUtility into the DI container.
    /// WHY TransferUtility as a Singleton:
    /// TransferUtility wraps IAmazonS3 and handles multipart upload/download
    /// automatically above the configured threshold. It is thread-safe and
    /// designed to be reused — creating a new instance per request is wasteful.
    /// </summary>
    public static class S3Extensions
    {

        /// <summary>
        /// Adds IAmazonS3 SDK client, S3Options binding, and TransferUtility.
        /// </summary>
        /// <param name="services">The DI (Dependency Injection) service collection.</param>
        public static IServiceCollection AddProductionS3(this IServiceCollection services)
        {
            services
                .AddOptions<S3Options>()
                .BindConfiguration(S3Options.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Register the S3 SDK client.
            // Uses DefaultAWSOptions region + execution role credentials from Track 1.
            services.AddAWSService<IAmazonS3>();

            // Register TransferUtility as Singleton.
            // WHY factory registration: TransferUtility requires IAmazonS3
            // which is already registered — resolve it from the container.
            services.AddSingleton<ITransferUtility>(provider =>
            {
                var s3Client = provider.GetRequiredService<IAmazonS3>();
                return new TransferUtility(s3Client, new TransferUtilityConfig 
                {
                    // WHY set MinSizeBeforePartUpload:
                    // Files above this threshold automatically use multipart upload.
                    // Multipart is more reliable for large files — individual parts
                    // can be retried without restarting the entire upload.
                    MinSizeBeforePartUpload = S3Constants.SizeLimits.MultipartThresholdBytes
                });
            });

            return services;
        }
    }
}
