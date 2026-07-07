using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AwsCloudNative.Common.Options
{
    /// <summary>
    /// Strongly-typed configuration for Amazon S3 bucket bindings.
    /// Resolved from Parameter Store at startup via IConfiguration.
    /// WHY: Bucket names are environment-specific (acn-orders-files-dev vs prod).
    /// They must never be hardcoded — resolved from config at runtime.
    /// </summary>
    public sealed class S3Options
    {
        /// <summary>The IConfiguration section this class binds to.</summary>
        public const string SectionName = "S3";

        /// <summary>
        /// Primary bucket for order-related file uploads and documents.
        /// Example: acn-orders-files-dev / acn-orders-files-prod
        /// PITFALL: Bucket names must be globally unique across all AWS accounts.
        /// Use a company prefix + environment suffix to avoid conflicts.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string OrdersBucket { get; init; } = string.Empty;

        /// <summary>
        /// AWS region where the bucket is located.
        /// PITFALL: S3 presigned URLs are region-specific. If you generate
        /// a presigned URL with a different region than the bucket's actual
        /// region, the upload will fail with a 301 redirect or 403 error.
        /// Always use the bucket's actual region, not the application region.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string BucketRegion { get; init; } = string.Empty;
    }
}
