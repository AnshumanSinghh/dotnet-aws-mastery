using AwsCloudNative.Api.Extensions;
using AwsCloudNative.Common.Constants;
using Microsoft.AspNetCore.Mvc;

namespace AwsCloudNative.Api.Controllers
{
    /// <summary>
    /// Exposes EC2 instance identity metadata resolved via IMDSv2
    /// (Instance Metadata Service version 2).
    ///
    /// WHY IMDSv2 explicitly: IMDSv1 requires no token and is vulnerable
    /// to SSRF (Server-Side Request Forgery) attacks where a malicious
    /// request tricks the server into leaking its own IAM credentials.
    /// IMDSv2 requires a PUT-acquired session token — SSRF cannot forge PUT requests.
    ///
    /// PITFALL: IMDS is only reachable inside an actual EC2 instance.
    /// This controller returns graceful local-dev notices when called outside EC2.
    /// Gate this behind internal-only access in production.
    /// </summary>
    [ApiController]
    [Route("api/diagnostics/ec2")]
    public class Ec2DiagnosticsController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<Ec2DiagnosticsController> _logger;

        /// <summary>Initialises with IHttpClientFactory for IMDS HTTP calls.</summary>
        public Ec2DiagnosticsController(
            IHttpClientFactory httpClientFactory,
            ILogger<Ec2DiagnosticsController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Returns EC2 instance identity metadata using the IMDSv2 two-step flow:
        /// Step 1 — PUT to token endpoint to acquire a session token.
        /// Step 2 — GET each metadata path with the session token header.
        ///
        /// Returns a local-dev notice if IMDS is unreachable (running outside EC2).
        /// </summary>
        [HttpGet("instance-metadata")]
        public async Task<IActionResult> GetInstanceMetadata(CancellationToken ct)
        {
            try
            {
                var client = _httpClientFactory.CreateClient(Ec2HttpClients.Imds);

                // ── Step 1: Acquire IMDSv2 session token ─────────────────────────
                // WHY PUT not GET: The PUT verb cannot be forged by a browser-based
                // SSRF attack — browsers do not send cross-origin PUT requests
                // without CORS preflight. This is the core IMDSv2 security mechanism.
                var tokenRequest = new HttpRequestMessage(
                    HttpMethod.Put,
                    Ec2Constants.ImdsTokenPath);

                tokenRequest.Headers.Add(
                    Ec2Constants.ImdsTokenTtlHeader,
                    Ec2Constants.ImdsTokenTtlSeconds.ToString());

                var tokenResponse = await client.SendAsync(tokenRequest, ct);
                tokenResponse.EnsureSuccessStatusCode();

                var sessionToken = await tokenResponse.Content.ReadAsStringAsync(ct);

                _logger.LogInformation("IMDSv2 session token acquired successfully.");


                // ── Step 2: Fetch metadata using session token ────────────────────
                var instanceId = await GetMetadataAsync(client, sessionToken, Ec2Constants.MetadataPaths.InstanceId, ct);
                var instanceType = await GetMetadataAsync(client, sessionToken, Ec2Constants.MetadataPaths.InstanceType, ct);
                var az = await GetMetadataAsync(client, sessionToken, Ec2Constants.MetadataPaths.AvailabilityZone, ct);
                var privateIp = await GetMetadataAsync(client, sessionToken, Ec2Constants.MetadataPaths.PrivateIpv4, ct);
                var iamRole = await GetMetadataAsync(client, sessionToken, Ec2Constants.MetadataPaths.IamRole, ct);

                _logger.LogInformation(
                    "EC2 metadata resolved. InstanceId={InstanceId} AZ={AZ} Role={Role}",
                    instanceId, az, iamRole);

                return Ok(new
                {
                    InstanceId = instanceId,
                    InstanceType = instanceType,
                    AZ = az,
                    PrivateIp = privateIp,
                    IamRole = iamRole,

                    // WHY surface this: Confirms the Instance Profile is attached
                    // and the SDK credential chain will resolve correctly.
                    // If IamRole is empty — no Instance Profile is attached —
                    // every AWS SDK call from this instance will fail with 403.
                    InstanceProfileAttached = !string.IsNullOrEmpty(iamRole)
                });
            }
            catch (HttpRequestException ex)
            {
                // IMDS unreachable — running locally or IMDS is disabled on the instance.
                _logger.LogWarning(
                    "IMDS endpoint unreachable. Running outside EC2 or IMDSv2 is disabled. {Message}",
                    ex.Message);

                return Ok(new
                {
                    Environment = "local-dev or non-EC2",
                    Message = "IMDS endpoint unreachable. Deploy to EC2 to see instance metadata.",
                    Hint = "If running on EC2 and seeing this, verify IMDSv2 is enabled " +
                                  "and HttpPutResponseHopLimit >= 2 for container workloads."
                });
            }
        }

        /// <summary>
        /// Fetches a single IMDS metadata value using an acquired IMDSv2 session token.
        /// </summary>
        /// <param name="client">The named IMDS HttpClient.</param>
        /// <param name="sessionToken">IMDSv2 session token from the PUT token endpoint.</param>
        /// <param name="path">IMDS metadata path to fetch.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw string value returned by IMDS, or empty string on failure.</returns>
        private static async Task<string> GetMetadataAsync(
        HttpClient client,
        string sessionToken,
        string path,
        CancellationToken ct)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);

            // Every IMDSv2 metadata request must carry the session token.
            // Without this header, IMDS returns 401 when IMDSv1 is disabled.
            request.Headers.Add(Ec2Constants.ImdsTokenHeader, sessionToken);

            var response = await client.SendAsync(request, ct);

            return response.IsSuccessStatusCode
                ? await response.Content.ReadAsStringAsync(ct)
                : string.Empty;
        }
    }
}
