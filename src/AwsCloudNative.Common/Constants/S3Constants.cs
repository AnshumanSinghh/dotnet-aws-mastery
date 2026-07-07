using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwsCloudNative.Common.Constants
{
    /// <summary>
    /// Centralised constants for Amazon S3 (Simple Storage Service) integration.
    /// WHY: Key prefixes, content type strings, and size limits scattered
    /// as magic strings across upload/download logic cause silent contract
    /// breaks when storage conventions change. One source of truth here.
    /// </summary>
    public static class S3Constants
    {
        /// <summary>
        /// S3 object key prefix conventions for this solution.
        /// Pattern: {category}/{entity}/{entityId}/{filename}
        /// WHY UUID in path: Distributes objects across S3 partitions
        /// to avoid hot-partition throttling at high request rates.
        /// </summary>
        public static class KeyPrefixes
        {
            /// <summary>Prefix for order-related file uploads.</summary>
            public const string OrderUploads = "uploads/orders/";

            /// <summary>Prefix for processed/generated order documents.</summary>
            public const string OrderDocuments = "documents/orders/";

            /// <summary>Prefix for temporary files — apply short lifecycle rule here.</summary>
            public const string Temp = "temp/";
        }

        /// <summary>
        /// Presigned URL expiry durations.
        /// PITFALL: Never set expiry longer than necessary.
        /// A leaked presigned URL grants access until it expires.
        /// Short expiry = smaller blast radius on accidental exposure.
        /// </summary>
        public static class PresignedUrlExpiry 
        {
            /// <summary>Upload URL expiry — 15 minutes is sufficient for most clients.</summary>
            public static readonly TimeSpan Upload = TimeSpan.FromMinutes(15);

            /// <summary>Download URL expiry — 1 hour for documents shared with users.</summary>
            public static readonly TimeSpan Download = TimeSpan.FromHours(1);
        }

        /// <summary>
        /// Allowed MIME (Multipurpose Internet Mail Extensions) content types
        /// for file uploads. Validated server-side before generating
        /// a presigned upload URL.
        /// PITFALL: Never rely on the file extension alone for type validation.
        /// Validate the Content-Type header and optionally the magic bytes.
        /// </summary>
        public static class AllowedContentTypes
        {
            public const string Pdf = "application/pdf";
            public const string Jpeg = "image/jpeg";
            public const string Png = "image/png";
            public const string Csv = "text/csv";

            /// <summary>
            /// All allowed types as a set for O(1) lookup validation.
            /// </summary>
            public static readonly IReadOnlySet<string> All = new HashSet<string>
            {
                Pdf, Jpeg, Png, Csv
            };
        }

        /// <summary>
        /// File size limits enforced server-side before upload.
        /// PITFALL: S3 enforces a 5TB max object size but has no
        /// built-in per-upload size limit. Your API must enforce
        /// application-level limits to prevent abuse.
        /// </summary>
        public static class SizeLimits
        {
            /// <summary>Maximum file size for direct server-side upload: 10 MB (Megabytes).</summary>
            public const long DirectUploadMaxBytes = 10 * 1024 * 1024;

            /// <summary>
            /// Threshold above which TransferUtility automatically uses
            /// multipart upload: 16 MB. SDK handles this transparently.
            /// </summary>
            public const long MultipartThresholdBytes = 16 * 1024 * 1024;
        }

        /// <summary>
        /// Standard S3 metadata key names attached to uploaded objects.
        /// Metadata keys are case-insensitive in S3 but use lowercase by convention.
        /// WHY store metadata: Allows filtering/searching objects without
        /// downloading content or maintaining a separate database index.
        /// </summary>
        public static class MetadataKeys
        {
            public const string UploadedBy = "x-amz-meta-uploaded-by";
            public const string OrderId = "x-amz-meta-order-id";
            public const string ContentHash = "x-amz-meta-content-hash";
        }
    }
}
