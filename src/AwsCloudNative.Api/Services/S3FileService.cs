using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using AwsCloudNative.Common.Constants;
using AwsCloudNative.Common.Options;
using Microsoft.Extensions.Options;

namespace AwsCloudNative.Api.Services
{
    /// <summary>
    /// Encapsulates all S3 file operations for the Orders domain.
    /// WHY a service class and not directly in the controller:
    /// S3 operations involve error handling, metadata construction,
    /// key naming conventions, and content type validation —
    /// none of which belong in a controller. The controller stays thin.
    /// </summary>
    public sealed class S3FileService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly ITransferUtility _transferUtility;
        private readonly S3Options _options;
        private readonly ILogger<S3FileService> _logger;

        /// <summary>
        /// Initialises with S3 SDK client, TransferUtility, and resolved S3Options.
        /// </summary>
        public S3FileService(
            IAmazonS3 s3Client,
            ITransferUtility transferUtility,
            IOptions<S3Options> options,
            ILogger<S3FileService> logger)
        {
            _s3Client = s3Client;
            _transferUtility = transferUtility;
            _options = options.Value;
            _logger = logger;
        }

        /// <summary>
        /// Uploads a file to S3 using TransferUtility.
        /// Automatically switches to multipart upload above the configured threshold.
        /// Attaches order metadata to the object for searchability.
        ///
        /// WHY TransferUtility over PutObjectAsync directly:
        /// TransferUtility handles stream buffering, multipart splitting,
        /// parallel part uploads, and retry per part automatically.
        /// PutObjectAsync is a single-shot call — one failure = full retry.
        /// </summary>
        /// <param name="stream">The file content stream.</param>
        /// <param name="fileName">Original file name — used to build the S3 key.</param>
        /// <param name="contentType">MIME content type of the file.</param>
        /// <param name="orderId">Order ID associated with this file — stored as metadata.</param>
        /// <param name="uploadedBy">User identity from JWT sub claim — stored as metadata.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The S3 object key of the uploaded file.</returns>
        public async Task<string> UploadOrderFileAsync(
            Stream stream,
            string fileName,
            string contentType,
            string orderId,
            string uploadedBy,
            CancellationToken ct = default)
        {
            // Build a unique key using UUID to avoid hot-partition issues
            // and prevent filename collisions across orders.
            var objectKey = $"{S3Constants.KeyPrefixes.OrderUploads}" +
                            $"{orderId}/{Guid.NewGuid()}/{fileName}";

            _logger.LogInformation(
                "Uploading file to S3. Bucket={Bucket} Key={Key} ContentType={ContentType}",
                _options.OrdersBucket, objectKey, contentType);

            var uploadRequest = new TransferUtilityUploadRequest
            {
                BucketName = _options.OrdersBucket,
                Key = objectKey,
                InputStream = stream,
                ContentType = contentType,

                // SSE-S3 (Server-Side Encryption with S3-managed keys).
                // Encrypts every object at rest. Default since 2023 on new buckets
                // but explicitly setting it makes the intent clear in code.
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,

                // Attach business metadata to the object.
                // WHY: Enables filtering by orderId or uploadedBy in S3 Inventory
                // without downloading the objects or querying a separate DB.
            };

            uploadRequest.Metadata.Add(S3Constants.MetadataKeys.OrderId, orderId);
            uploadRequest.Metadata.Add(S3Constants.MetadataKeys.UploadedBy, uploadedBy);

            await _transferUtility.UploadAsync(uploadRequest, ct);

            _logger.LogInformation(
                "File uploaded successfully. Key={Key}", objectKey);

            return objectKey;
        }

        /// <summary>
        /// Generates a presigned URL for direct client-to-S3 upload.
        /// The client uses this URL to PUT the file directly to S3 —
        /// the API server is not in the data path.
        ///
        /// WHY presigned upload over direct server upload:
        /// Large files (10MB+) uploaded via the server consume API bandwidth,
        /// memory, and connection slots. Presigned URLs offload this entirely
        /// to S3 — the server only generates a URL (microseconds of work).
        ///
        /// PITFALL: The presigned URL allows ANY client to upload to that key
        /// until it expires. Keep expiry short. Do not reuse URLs across clients.
        /// </summary>
        /// <param name="fileName">Original file name — used to build the S3 key.</param>
        /// <param name="contentType">
        /// MIME type the client must use when uploading.
        /// The presigned URL is bound to this content type — a mismatch returns 403.
        /// </param>
        /// <param name="orderId">Order ID — used in key construction.</param>
        /// <returns>Presigned URL and the S3 key the file will be stored under.</returns>
        public (string PresignedUrl, string ObjectKey) GeneratePresignedUploadUrl(
        string fileName,
        string contentType,
        string orderId)
        {
            var objectKey = $"{S3Constants.KeyPrefixes.OrderUploads}" +
                            $"{orderId}/{Guid.NewGuid()}/{fileName}";

            var request = new GetPreSignedUrlRequest
            {
                BucketName = _options.OrdersBucket,
                Key = objectKey,
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.Add(S3Constants.PresignedUrlExpiry.Upload),
                ContentType = contentType
            };

            var presignedUrl = _s3Client.GetPreSignedURL(request);

            _logger.LogInformation(
                "Generated presigned upload URL. Key={Key} Expiry={Expiry}",
                objectKey,
                S3Constants.PresignedUrlExpiry.Upload);

            // PITFALL: Never log the full presigned URL — it is a temporary credential.
            // Log the key and expiry only.
            return (presignedUrl, objectKey);
        }

        /// <summary>
        /// Generates a presigned URL for direct client-to-S3 download.
        /// The client fetches the file directly from S3 —
        /// the API is not in the download data path.
        /// </summary>
        /// <param name="objectKey">The S3 object key to generate a download URL for.</param>
        /// <returns>A time-limited presigned download URL.</returns>
        public string GeneratePresignedDownloadUrl(string objectKey)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _options.OrdersBucket,
                Key = objectKey,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(S3Constants.PresignedUrlExpiry.Download)
            };

            var presignedUrl = _s3Client.GetPreSignedURL(request);

            _logger.LogInformation(
                "Generated presigned download URL. Key={Key} Expiry={Expiry}",
                objectKey,
                S3Constants.PresignedUrlExpiry.Download);

            return presignedUrl;
        }

        /// <summary>
        /// Deletes an S3 object by key.
        /// WHY soft-check before delete: DeleteObjectAsync does not throw
        /// if the key does not exist — it silently succeeds.
        /// Log the intent explicitly so the audit trail is clear.
        /// </summary>
        /// <param name="objectKey">The S3 object key to delete.</param>
        /// <param name="ct">Cancellation token.</param>
        public async Task DeleteFileAsync(string objectKey, CancellationToken ct = default)
        {
            _logger.LogInformation(
                "Deleting S3 object. Bucket={Bucket} Key={Key}",
                _options.OrdersBucket, objectKey);

            await _s3Client.DeleteObjectAsync(
                _options.OrdersBucket, objectKey, ct);

            _logger.LogInformation("S3 object deleted. Key={Key}", objectKey);
        }
    }
}
