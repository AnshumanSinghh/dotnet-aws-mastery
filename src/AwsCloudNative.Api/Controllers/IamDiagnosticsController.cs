using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Microsoft.AspNetCore.Mvc;

namespace AwsCloudNative.Api.Controllers
{
    /// <summary>
    /// WHY: This controller exists purely for learning and local dev verification.
    /// In a real system you would gate this behind an internal-only route
    /// or remove it before production promotion.
    /// OBJECTIVE: Prove that the credential chain is resolving correctly
    /// without exposing any credential values to the response.
    /// PITFALL: Never return AccessKeyId, SecretKey, or SessionToken
    /// in any API response — even in diagnostics endpoints.
    /// </summary>
    [ApiController]
    [Route("api/diagnostics/iam")]
    public class IamDiagnosticsController : ControllerBase
    {
        private readonly IAmazonSecurityTokenService _sts;
        private readonly ILogger<IamDiagnosticsController> _logger;

        public IamDiagnosticsController(
            IAmazonSecurityTokenService sts, 
            ILogger<IamDiagnosticsController> logger)
        {
            _sts = sts;
            _logger = logger;
        }

        [HttpGet("identity")]        
        public async Task<IActionResult> GetCallerIdentity(CancellationToken ct)
        {
            // WHY GetCallerIdentity: It is the one STS call that requires
            // NO IAM permissions. Even a role with zero policies attached
            // can call it. Invaluable for confirming your execution role
            // is actually being assumed by your service.
            var response = await _sts.GetCallerIdentityAsync(
                new GetCallerIdentityRequest(), ct);

            // PITFALL: Do not log or return the full response object.
            // The SDK response includes internal metadata. Return only
            // what you need for diagnosis.
            var result = new
            {
                Account = response.Account,
                UserId = response.UserId,

                // The ARN tells you exactly which role is in use:
                // arn:aws:sts::123456789012:assumed-role/OrderLambdaRole/...
                Arn = response.Arn,

                // Derive a human-readable source label from the ARN shape.
                Source = DeriveCredentialSource(response.Arn)
            };

            _logger.LogInformation(
                "IAM identity resolved. Account={Account} Arn={Arn}",
                result.Account, result.Arn);

            return Ok(result);
        }

        // Infers the credential source from the ARN structure.
        // assumed-role → execution role (Lambda) or task role (ECS)
        // user          → IAM user (local dev with long-term creds — warn)
        // root          → account root (should never happen — alert)
        private static string DeriveCredentialSource(string arn) => arn switch
        {
            var a when a.Contains(":assumed-role/") => "ExecutionRole / TaskRole (correct for Lambda/ECS)",
            var a when a.Contains(":user/") => "IAM User (local dev — ensure MFA is enabled)",
            var a when a.Contains(":root") => "ROOT ACCOUNT — this should never happen in application code",
            _ => $"Unknown"
        };    
    }
}
