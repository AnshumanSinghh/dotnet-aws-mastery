using AwsCloudNative.Common.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AwsCloudNative.Api.Controllers
{
    /// <summary>
    /// Diagnostic endpoint to verify secrets and parameters were
    /// resolved correctly from AWS at startup.
    /// PITFALL: Never return actual secret values — not even partially.
    /// Return only structural proof that the value was resolved.
    /// Remove or gate this endpoint before production promotion.
    /// </summary>
    [Route("api/diagnostics/secrets")]
    [ApiController]
    public class SecretsDiagnosticsController : ControllerBase
    {
        private readonly DatabaseSecretOptions _dbSecret;
        private readonly OrdersParameterOptions _ordersParams;
        private readonly ILogger<SecretsDiagnosticsController> _logger;

        /// <summary>
        /// Injects resolved Options instances.
        /// By the time this constructor runs, ValidateOnStart has already
        /// confirmed both options are valid — no null checks needed.
        /// </summary>
        public SecretsDiagnosticsController(
        IOptions<DatabaseSecretOptions> dbSecret,
        IOptions<OrdersParameterOptions> ordersParams,
        ILogger<SecretsDiagnosticsController> logger)
        {
            _dbSecret = dbSecret.Value;
            _ordersParams = ordersParams.Value;
            _logger = logger;
        }


        /// <summary>
        /// Returns proof-of-resolution for secrets and parameters.
        /// Shows that values were loaded without exposing actual secret content.
        /// Password is masked — only its length is returned as confirmation.
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            _logger.LogInformation(
                "Secrets diagnostics requested. DbHost={Host} QueueUrl={QueueUrl}",
                _dbSecret.Host,
                _ordersParams.QueueUrl);

            return Ok(new
            {
                Database = new
                {
                    // Safe to show — not sensitive
                    Host = _dbSecret.Host,
                    Port = _dbSecret.Port,
                    Database = _dbSecret.Database,
                    Username = _dbSecret.Username,

                    // NEVER return the actual password.
                    // Show length as proof it was resolved.
                    // If length is 0, the secret was not loaded.
                    PasswordResolved = _dbSecret.Password.Length > 0,
                    PasswordLength = _dbSecret.Password.Length
                },
                Parameters = new
                {
                    QueueUrl = _ordersParams.QueueUrl,
                    FeatureNewCheckout = _ordersParams.FeatureNewCheckout
                }
            });
        }
    }
}
