using AwsCloudNative.Common.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AwsCloudNative.Api.Controllers
{
    /// <summary>
    /// Diagnostic endpoints for verifying Cognito JWT authentication during development.
    /// Returns resolved claims from the validated token — never the raw token itself.
    /// PITFALL: Gate this behind an internal route or remove before production promotion.
    /// </summary>
    [ApiController]
    [Route("api/diagnostics/auth")]
    public sealed class AuthDiagnosticsController : ControllerBase
    {
        private readonly ILogger<AuthDiagnosticsController> _logger;

        /// <summary>Initialises the controller with a scoped logger.</summary>
        public AuthDiagnosticsController(ILogger<AuthDiagnosticsController> logger)
        {
            _logger = logger;
        }


        /// <summary>
        /// Returns all claims resolved from the validated Cognito access token.
        /// Use this to verify the JWT middleware is correctly parsing the token
        /// and populating HttpContext.User with the expected claims.
        /// Requires a valid Bearer token — returns 401 if unauthenticated.
        /// </summary>
        [HttpGet("claims")]
        [Authorize(Policy = AuthConstants.Policies.AuthenticatedUser)]
        public async Task<IActionResult> GetClaims()
        {
            // HttpContext.User is populated by the JWT middleware after
            // successful token validation. Each claim is a key-value pair
            // extracted from the token payload.
            var claims = User.Claims.Select(claim => new
            {
                Type = claim.Type,
                Value = claim.Value,
            });

            var userId = User.FindFirstValue(AuthConstants.CognitoClaims.Subject);

            _logger.LogInformation(
                "Auth diagnostics requested by user {UserId}", userId);

            return Ok(new
            {
                UserId = userId,
                Email = User.FindFirstValue(AuthConstants.CognitoClaims.Email),
                Claims = claims
            });
        }

        /// <summary>
        /// Admin-only diagnostic endpoint.
        /// Verifies that the "AdminOnly" policy correctly evaluates
        /// the "cognito:groups" claim from the access token.
        /// Returns 403 Forbidden if the user is not in the Admins group.
        /// </summary>
        [HttpGet("admin-check")]
        [Authorize(Policy = AuthConstants.Policies.AdminOnly)]
        public async Task<IActionResult> AdminCheck()
        {
            var userId = User.FindFirstValue(AuthConstants.CognitoClaims.Subject);

            _logger.LogInformation(
                "Admin check passed for user {UserId}", userId);

            return Ok(new 
            {
                Message = "You have Admin access.",
                UserId = userId
            });
        }
    }
}
