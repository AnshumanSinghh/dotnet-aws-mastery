using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwsCloudNative.Common.Options
{
    /// <summary>
    /// Strongly-typed binding for database credentials resolved
    /// from AWS Secrets Manager at application startup.
    /// PITFALL: Never log or expose any property of this class
    /// in responses, logs or exception messages.
    /// </summary>
    public sealed class DatabaseSecretOptions
    {
        /// <summary>The IConfiguration section this class binds to.</summary>
        public const string SectionName = "OrdersDatabase";

        /// <summary>Database server hostname or RDS (Relational Database Service) cluster endpoint.</summary>
        [Required(AllowEmptyStrings = false)]
        public string Host { get; init; } = string.Empty;

        /// <summary>Database port. Default 5432 for PostgreSQL.</summary>
        [Required]
        public int Port { get; init; }

        /// <summary>Database name on the server.</summary>
        [Required(AllowEmptyStrings = false)]
        public string Database { get; init; } = string.Empty;

        /// <summary>
        /// Database username resolved from Secrets Manager.
        /// Never hardcoded. Never in appsettings.json.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string Username { get; init; } = string.Empty;

        /// <summary>
        /// Database password resolved from Secrets Manager.
        /// PITFALL: Never log this. Never include in any response.
        /// Mark with [JsonIgnore] if this class is ever serialised.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string Password { get; init; } = string.Empty;

        /// <summary>
        /// Builds a safe connection string from resolved secret values.
        /// WHY: Connection string is assembled at runtime from secret parts —
        /// never stored as a full connection string in any config source.
        /// </summary>
        public string ToConnectionString() 
            => $"Host={Host};Port={Port};Database={Database};" +
            $"Username={Username};Password={Password}";
    }

    /// <summary>
    /// Strongly-typed binding for non-secret configuration values
    /// resolved from SSM Parameter Store at application startup.
    /// These are environment-specific but not sensitive enough for Secrets Manager.
    /// </summary>
    public sealed class OrdersParameterOptions
    {
        /// <summary>The IConfiguration section this class binds to.</summary>
        public const string SectionName = "Orders";

        /// <summary>
        /// SQS (Simple Queue Service) queue URL for order events.
        /// Resolved from Parameter Store — differs per environment.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string QueueUrl { get; init; } = string.Empty;

        /// <summary>
        /// Feature flag controlling the new checkout flow.
        /// Toggled via Parameter Store without redeployment.
        /// </summary>
        public bool FeatureNewCheckout { get; init; }
    }
}
