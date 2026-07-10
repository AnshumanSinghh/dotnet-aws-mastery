using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwsCloudNative.Common.Constants
{
    /// <summary>
    /// Centralised constants for Amazon RDS (Relational Database Service)
    /// PostgreSQL integration.
    /// WHY: Schema names, table names, and connection configuration values
    /// scattered across DbContext, migrations, and health checks cause
    /// silent mismatches when schema conventions change.
    /// </summary>
    public sealed class RdsConstants
    {
        /// <summary>
        /// PostgreSQL schema name for the Orders domain.
        /// WHY explicit schema: Using the default "public" schema mixes all
        /// application tables in one namespace. Explicit schemas allow
        /// per-domain permissions and cleaner multi-service database sharing.
        /// </summary>
        public const string OrdersSchema = "orders";

        /// <summary>
        /// Table names within the Orders schema.
        /// These must match the values in entity configurations and migrations exactly.
        /// </summary>
        public static class Tables
        {
            public const string Orders = "orders";
        }

        /// <summary>
        /// EF Core retry-on-failure configuration.
        /// These values are tuned for RDS Multi-AZ failover scenarios
        /// where the standby promotion takes 60–120 seconds.
        /// </summary>
        public static class Resilience
        {
            /// <summary>
            /// Maximum number of retry attempts on transient failures.
            /// Set to 6 to cover the RDS failover window with exponential backoff.
            /// </summary>
            public const int MaxRetryCount = 6;

            /// <summary>
            /// Maximum delay between retry attempts in seconds.
            /// Caps the exponential backoff to prevent indefinite waits.
            /// </summary>
            public const int MaxRetryDelaySeconds = 30;
        }

        /// <summary>
        /// Npgsql connection pool configuration values.
        /// WHY explicit pool settings: Default Npgsql pool size (100 max)
        /// may be too high for Lambda (causes connection exhaustion on RDS)
        /// and too low for high-traffic ECS services.
        /// Tune per deployment target.
        /// </summary>
        public static class ConnectionPool
        {
            /// <summary>Minimum connections kept alive in the pool.</summary>
            public const int MinPoolSize = 1;

            /// <summary>
            /// Maximum connections per pool per process.
            /// For ECS: set based on (RDS max_connections / number of tasks).
            /// For Lambda: use RDS Proxy and set this low (2–5).
            /// </summary>
            public const int MaxPoolSize = 20;

            /// <summary>
            /// Seconds a connection can remain idle before being closed.
            /// Prevents stale connections accumulating during low-traffic periods.
            /// </summary>
            public const int ConnectionIdleLifetimeSeconds = 300;
        }
    }
}
