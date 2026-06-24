using AwsCloudNative.Common.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AwsCloudNative.Api.Controllers
{
    /// <summary>
    /// A minimal protected resource controller demonstrating how downstream
    /// controllers consume the authenticated identity after JWT validation.
    /// In a real system this would be your Orders, Products, or Users controller.
    /// </summary>
    [ApiController]
    [Route("api/protected")]
    [Authorize(Policy = AuthConstants.Policies.AuthenticatedUser)]
    public sealed class ProtectedController : ControllerBase
    {
        /// <summary>
        /// Returns a personalised response using the authenticated user's identity.
        /// Demonstrates reading the "sub" claim from the validated JWT.
        /// The controller has zero knowledge of tokens — it only reads HttpContext.User.
        /// This is the correct separation: middleware owns auth, controllers own logic.
        /// </summary>
        [HttpGet("hello")]
        public async Task<IActionResult> Hello()
        {
            // User.FindFirstValue resolves a claim by type from HttpContext.User.
            // This works because the JWT middleware already validated the token
            // and populated the ClaimsPrincipal before this action executes.
            var userId = User.FindFirstValue(AuthConstants.CognitoClaims.Subject);
            var email = User.FindFirstValue(AuthConstants.CognitoClaims.Email);

            return Ok(new
            {
                Message = $"Hello, {email}!",
                UserId = userId
            });
        }
    }
}
