using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwsCloudNative.Common.Options
{
    /// <summary>
    /// Strongly-typed configuration for Amazon Cognito integration.
    /// Bound from the "Cognito" section in appsettings.json.
    /// Validated at startup via ValidateOnStart — missing values crash fast, not silently.
    /// </summary>
    public sealed class CognitoOptions
    {
        /// <summary>The configuration section key this class binds to.</summary>
        public const string SectionName = "Congnito";

        /// <summary>
        /// AWS region where the Cognito User Pool is hosted.
        /// Example: "ap-south-1"
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string Region { get; init; } = string.Empty;

        /// <summary>
        /// The Cognito User Pool ID.
        /// Format: {region}_{alphanumeric} — e.g. "ap-south-1_AbCdEf123"
        /// Found in AWS Console → Cognito → User Pools → Pool ID.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string UserPoolId { get; init; } = string.Empty;

        /// <summary>
        /// The App Client ID registered for this API in the User Pool.
        /// Used to validate the "aud" (audience) claim inside the JWT.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string AppClientId { get; init; } = string.Empty;

        /// <summary>
        /// Constructs the JWT Authority URL — the base URL the middleware uses
        /// to fetch the JWKS (JSON Web Key Set) public keys for signature verification.
        /// Format: https://cognito-idp.{region}.amazonaws.com/{userPoolId}
        /// </summary>
        public string Authority =>
            $"https://cognito-idp.{Region}.amazonaws.com/{UserPoolId}";

        /// <summary>
        /// Constructs the full JWKS endpoint URL.
        /// The JWT middleware fetches this once, caches the RSA public keys,
        /// and uses them to verify every incoming token's signature locally.
        /// </summary>
        public string JwksUri => $"{Authority}/.well-known/jwks.json";

    }
}
