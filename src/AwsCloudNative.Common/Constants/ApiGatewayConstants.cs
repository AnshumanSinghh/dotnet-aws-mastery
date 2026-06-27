using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwsCloudNative.Common.Constants
{
    /// <summary>
    /// Centralised constants for API Gateway configuration and response handling.
    /// WHY: HTTP status codes and header names scattered as magic numbers
    /// across Lambda functions cause silent contract breaks when routes change.
    /// </summary>
    public static class ApiGatewayConstants
    {
        /// <summary>
        /// API Gateway HTTP API route keys.
        /// Format: "{METHOD} {path}" — must match serverless.template route definitions exactly.
        /// PITFALL: A mismatch between the route key here and in the SAM template
        /// results in a 403 from API Gateway with no useful error message.
        /// </summary>
        public static class Routes
        {
            public const string PostOrders = "POST /orders";
            public const string GetOrders = "GET /orders/{orderId}";
            public const string HealthCheck = "GET /health";
        }

        /// <summary>
        /// HTTP (HyperText Transfer Protocol) response headers returned
        /// by Lambda Proxy Integration responses.
        /// </summary>
        public static class Headers
        {
            public const string ContentType = "Content-Type";
            public const string ApplicationJson = "application/json";
            public const string CorrelationId = "X-Correlation-Id";
            public const string RequestId = "X-Request-Id";
        }


        /// <summary>
        /// Throttling configuration values.We can do it in 2 ways:
        /// 1) Account-level: Default (Burst: 500 and Ratelimit: 1000 req/s)
        /// 2) Per-route throttle: Devs choice (using Burst: 200 and Ratelimit: 100 req/s)
        /// These must match what is configured in the SAM template
        /// and in the API Gateway console throttling settings.
        /// </summary>
        public static class Throttling
        {
            /// <summary>Sustained requests per second allowed on the orders route.</summary>
            public const int OrdersRateLimit = 100;

            /// <summary>
            /// Burst capacity — number of requests API Gateway absorbs
            /// before throttling kicks in during a sudden spike.
            /// </summary>
            public const int OrdersBurstLimit = 200;
        }

        /// <summary>
        /// JWT (JSON Web Token) authoriser claim keys as forwarded by
        /// API Gateway HTTP API in requestContext.authorizer.jwt.claims.
        /// These mirror AuthConstants.CognitoClaims from Phase 1 Track 2
        /// but are used in Lambda context where HttpContext is not available.
        /// </summary>
        public static class JwtClaims
        {
            public const string Subject = "sub";
            public const string Groups = "cognito:groups";
            public const string Email = "email";
        }
    }
}
