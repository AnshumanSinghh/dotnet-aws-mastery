using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwsCloudNative.Common.Constants
{
    /// <summary>
    /// Centralised path constants for AWS Secrets Manager and
    /// SSM (Systems Manager) Parameter Store.
    /// WHY: Hardcoding secret names as strings across the codebase
    /// makes rotation and environment changes error-prone.
    /// One change here propagates everywhere.
    /// </summary>
    public static class SecretsConstants
    {       
        /// <summary>
        /// The environment prefix used in all secret and parameter paths.
        /// Matches the deployment environment: "dev", "staging", "prod".
        /// Injected at runtime from the ASPNETCORE_ENVIRONMENT variable.
        /// </summary>
        public const string EnvironmentPlaceholder = "{env}";

        /// <summary>
        /// AWS Secrets Manager secret name paths.
        /// Convention: {app}/{environment}/{service}/{secret-type} <br/>
        /// PITFALL: Never use the same secret name across environments.
        /// Each environment must have its own isolated secret.<br/>
        /// <b>NOTE:</b> Include {env} in the secret path to enforce strict cross-environment 
        /// isolation, prevent data corruption, and enable granular IAM security policies.
        /// </summary>
        public static class Secrets
        {
            /// <summary>
            /// Database credentials for the Orders service.
            /// Stored as a JSON object with Username, Password, Host, Port, Database keys.
            /// </summary>
            public const string OrdersDatabase = "acn/{env}/orders/database";            
        }

        /// <summary>
        /// SSM Parameter Store path prefixes.
        /// Convention: /{app}/{environment}/{service}/{parameter-name}
        /// The SDK strips this prefix and maps remainder to IConfiguration keys.
        /// </summary>
        public static class Parameters
        {
            /// <summary>
            /// Root path prefix for all Orders service parameters.
            /// All parameters under this path are loaded into IConfiguration
            /// with the prefix stripped.
            /// </summary>
            public const string OrdersPrefix = "/acn/{env}/orders/";

            /// <summary>Individual parameter paths within the Orders prefix.</summary>
            public const string OrdersQueueUrl = "/acn/{env}/orders/queue-url";
            public const string OrdersFeatureNewCheckout = "/acn/{env}/orders/feature-new-checkout";
        }
    }
}
