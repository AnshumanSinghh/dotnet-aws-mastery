using AwsCloudNative.Common.Constants;
using AwsCloudNative.Common.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace AwsCloudNative.Api.Extensions
{
    /// <summary>
    /// Registers Amazon Cognito JWT (JSON Web Token) authentication
    /// and authorisation policies into the ASP.NET Core DI (Dependency Injection) container.
    /// </summary>
    public static class CognitoAuthExtensions
    {         
        /// <summary>
        /// Adds Cognito-backed JWT Bearer authentication and named authorisation policies.
        /// Call this in Program.cs before app.Build().
        /// </summary>
        /// <param name="services">The DI service collection.</param>
        /// <param name="configuration">The application configuration root.</param>
        public static IServiceCollection AddProductionCognitoAuth(
            this IServiceCollection services,
            IConfiguration configuration) 
        {
            // Bind and validate CognitoOptions at startup.
            // If UserPoolId or Region is missing, the app refuses to start.
            // WHY: Silent misconfiguration in auth leads to every request
            // being rejected at runtime with a cryptic 401 — fail fast instead.
            services.AddOptions<CognitoOptions>()
                .BindConfiguration(CognitoOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            var cognitoOptions = configuration
                .GetSection(CognitoOptions.SectionName)
                .Get<CognitoOptions>() ?? new CognitoOptions();

            services
                .AddAuthentication(options => 
                {
                    // Set JWT Bearer as the default scheme for both
                    // authentication (who are you?) and challenge (prove it).
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                
                .AddJwtBearer(options =>
                {
                    // Authority: The middleware fetches the JWKS public keys from
                    // {Authority}/.well-known/jwks.json on first use, then caches them.
                    // It also validates the token's "iss" (issuer) claim matches this URL.
                    options.Authority = cognitoOptions.Authority;

                    // PITFALL: Cognito access tokens do NOT include an "aud" claim.
                    // Only "id" tokens carry it. If you leave this true (the default),
                    // every access token will be rejected with "audience validation failed".
                    // This is the single most common Cognito + .NET integration mistake.
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,  // Verify RSA signature against JWKS
                        ValidateIssuer = true,  // "iss" must match Cognito pool URL
                        ValidIssuer = cognitoOptions.Authority,
                        ValidateAudience = false, // Cognito access tokens have no "aud"
                        ValidateLifetime = true,  // Reject expired tokens automatically

                        // Map Cognito's "sub" claim to ClaimTypes.NameIdentifier
                        // so User.FindFirstValue(ClaimTypes.NameIdentifier) works in controllers.
                        NameClaimType = AuthConstants.CognitoClaims.Subject,

                        // Use UTC clock for expiry validation — never local time.
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };

                    options.Events = new JwtBearerEvents 
                    { 
                        OnTokenValidated = context =>
                        {
                            // Enforce token_use = "access".
                            // Reject "id" tokens that a frontend accidentally sent to the API.
                            // WHY: "id" tokens are for reading user profile info on the client.
                            // They must never be accepted as API authentication tokens.
                            var tokenUse = context.Principal?
                                .FindFirstValue(AuthConstants.CognitoClaims.TokenUse);

                            if (tokenUse != AuthConstants.CognitoClaims.TokenUseAccess)
                            {
                                context.Fail(
                                    "Invalid token_use. API requires an access token, not an id token.");
                            }

                            return Task.CompletedTask;
                        },

                        OnAuthenticationFailed = context =>
                        {
                            // Log the failure reason internally — but never forward
                            // exception details to the client response.
                            // PITFALL: Exposing token validation errors leaks
                            // information about your auth configuration to attackers.
                            var logger = context.HttpContext.RequestServices
                                .GetRequiredService<ILogger<JwtBearerEvents>>();

                            logger.LogWarning(
                                context.Exception,
                                "JWT authentication failed: {Reason}",
                                context.Exception.Message);

                            return Task.CompletedTask;
                        }
                    };
                });

            // Register named authorisation policies.
            // Controllers reference these via [Authorize(Policy = AuthConstants.Policies.AdminOnly)]
            services.AddAuthorization(options => 
            {
                // Any valid Cognito access token passes this policy.
                options.AddPolicy(AuthConstants.Policies.AuthenticatedUser, policy =>
                    policy.RequireAuthenticatedUser());

                // User must belong to the Cognito "Admins" group.
                // Cognito encodes group membership in the "cognito:groups" claim.
                options.AddPolicy(AuthConstants.Policies.AdminOnly, policy => 
                    policy.RequireClaim(
                        AuthConstants.CognitoClaims.Groups, "Admins"));
            });

            return services;
        }
    }
}
