using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwsCloudNative.Common.Constants
{    

    /// <summary>
    /// Centralised constants for authentication and authorisation.
    /// Prevents magic strings from being scattered across controllers and middleware.
    /// </summary>
    public static class AuthConstants
    {
        /// <summary>
        /// JWT claim type names as issued by Amazon Cognito.
        /// Use these instead of raw strings when reading claims from HttpContext.User.
        /// </summary>
        public static class CognitoClaims
        {
            /// <summary>
            /// The unique subject identifier for the user. Always use this as the
            /// user's identity key in your database — never email, which can change.
            /// </summary>
            public const string Subject = "sub";

            /// <summary>
            /// Cognito groups the user belongs to. Used for role-based authorisation.
            /// Example value: "Admins", "ReadOnly"
            /// </summary>
            public const string Groups = "cognito:groups";

            /// <summary>
            /// Indicates token type. Must be "access" for API calls.
            /// Reject tokens where this value is "id" — those are for frontends only.
            /// </summary>
            public const string TokenUse = "token_use";

            /// <summary>Expected value of TokenUse for valid API access tokens.</summary>
            public const string TokenUseAccess = "access";

            /// <summary>The user's email address as registered in the Cognito User Pool.</summary>
            public const string Email = "email";
        }

        /// <summary>
        /// ASP.NET Core authorisation policy names.
        /// Register these in AddAuthorization and reference them in [Authorize(Policy = "...")] attributes.
        /// </summary>
        public static class Policies
        {
            /// <summary>Any authenticated user with a valid Cognito access token.</summary>
            public const string AuthenticatedUser = "AuthenticatedUser";

            /// <summary>Users who belong to the Cognito "Admins" group.</summary>
            public const string AdminOnly = "AdminOnly";
        }

        /// <summary>
        /// The authentication scheme name used throughout the pipeline.
        /// Matches the scheme registered in AddJwtBearer.
        /// </summary>
        public const string JwtBearerScheme = "Bearer";
    }
}
