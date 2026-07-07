using AwsCloudNative.Api.Services;
using AwsCloudNative.Common.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AwsCloudNative.Api.Controllers
{    
    /// <summary>
    /// Handles file upload and download operations backed by Amazon S3.
    /// Controller is deliberately thin — all S3 logic lives in S3FileService.
    /// </summary>
    [ApiController]
    [Route("api/files")]
    [Authorize(Policy = AuthConstants.Policies.AuthenticatedUser)]
    public sealed class FileController : ControllerBase
    {
        private readonly S3FileService _fileService;
        private readonly ILogger<FileController> _logger;

        /// <summary>Initialises with S3FileService and logger.</summary>
        public FileController(
            S3FileService fileService,
            ILogger<FileController> logger)
        {
            _fileService = fileService;
            _logger = logger;
        }

        /// <summary>
        /// Direct file upload — receives the file on the server and uploads to S3.
        /// Use for small files (under 10 MB) where the client cannot use presigned URLs.
        /// For larger files, use POST /api/files/presigned-upload instead.
        ///
        /// PITFALL: IFormFile buffers the entire file in memory or temp disk.
        /// Never accept direct uploads without enforcing a size limit.
        /// Size limit is configured via RequestSizeLimitAttribute and validated
        /// against S3Constants.SizeLimits.DirectUploadMaxBytes.
        /// </summary>
        [HttpPost("upload")]
        [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB hard limit at ASP.NET Core level
        public async Task<IActionResult> UploadFile(
            IFormFile file,
            [FromQuery] string orderId,
            CancellationToken ct)
        {
            if (file.Length == 0)
                return BadRequest(new { Error = "File is empty." });

            if (file.Length > S3Constants.SizeLimits.DirectUploadMaxBytes)
                return BadRequest(new { Error = "File exceeds the 10 MB direct upload limit. Use presigned upload instead." });

            if (!S3Constants.AllowedContentTypes.All.Contains(file.ContentType))
                return BadRequest(new { Error = $"Content type '{file.ContentType}' is not allowed." });

            // Resolve the caller's identity from the validated JWT.
            // Sub claim = unique user ID in Cognito.
            var uploadedBy = User.FindFirstValue(AuthConstants.CognitoClaims.Subject)
                             ?? "unknown";

            await using var stream = file.OpenReadStream();

            var objectKey = await _fileService.UploadOrderFileAsync(
                stream,
                file.FileName,
                file.ContentType,
                orderId,
                uploadedBy,
                ct);

            return Ok(new
            {
                ObjectKey = objectKey,
                FileName = file.FileName,
                SizeBytes = file.Length,
                ContentType = file.ContentType
            });
        }

        /// <summary>
        /// Generates a presigned URL for direct client-to-S3 upload.
        /// The client receives a URL and PUTs the file directly to S3
        /// — this server is not in the upload data path.
        ///
        /// Flow:
        ///   1. Client calls this endpoint with filename and content type
        ///   2. Server returns presigned URL + object key
        ///   3. Client PUTs file directly to the presigned URL
        ///   4. Client notifies server of completion with the object key
        /// </summary>
        [HttpPost("presigned-upload")]
        public IActionResult GetPresignedUploadUrl(
            [FromQuery] string fileName,
            [FromQuery] string contentType,
            [FromQuery] string orderId)
        {
            if (!S3Constants.AllowedContentTypes.All.Contains(contentType))
                return BadRequest(new { Error = $"Content type '{contentType}' is not allowed." });

            var (presignedUrl, objectKey) = _fileService.GeneratePresignedUploadUrl(
                fileName, contentType, orderId);

            return Ok(new
            {
                // The URL the client must PUT the file to directly.
                PresignedUrl = presignedUrl,

                // The key the file will be stored under after upload.
                // Client must send this back to confirm the upload completed.
                ObjectKey = objectKey,

                // Tell the client exactly how long they have.
                ExpiresInSeconds = (int)S3Constants.PresignedUrlExpiry.Upload.TotalSeconds
            });
        }

        /// <summary>
        /// Generates a presigned URL for direct client-to-S3 download.
        /// The client downloads the file directly from S3 — server not in data path.
        /// PITFALL: Validate that the objectKey belongs to this user/order
        /// before generating the URL. Never generate download URLs
        /// for arbitrary keys without an ownership check.
        /// </summary>
        [HttpGet("presigned-download")]
        public IActionResult GetPresignedDownloadUrl([FromQuery] string objectKey)
        {
            if (string.IsNullOrWhiteSpace(objectKey))
                return BadRequest(new { Error = "objectKey is required." });

            // WHY ownership check matters:
            // Without this, any authenticated user could download any file
            // by guessing or constructing an object key.
            // In production, verify the objectKey exists in your database
            // and belongs to an order owned by the calling user.
            if (!objectKey.StartsWith(S3Constants.KeyPrefixes.OrderUploads))
                return Forbid();

            var presignedUrl = _fileService.GeneratePresignedDownloadUrl(objectKey);

            return Ok(new
            {
                PresignedUrl = presignedUrl,
                ExpiresInSeconds = (int)S3Constants.PresignedUrlExpiry.Download.TotalSeconds
            });
        }
    }
}
