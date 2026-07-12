using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwsCloudNative.Common.Constants
{
    /// <summary>
    /// Centralised constants for ElastiCache (Valkey/Redis) caching.
    /// WHY: Cache key strings and TTL (Time To Live) values scattered
    /// across service classes cause stale data bugs and key collision
    /// between different cached entities. One source of truth here.
    /// </summary>
    public static class CacheConstants
    {
        /// <summary>
        /// Cache key prefix patterns for each entity type.
        /// Pattern: {domain}:{entity}:{identifier}
        /// WHY prefixes: Prevents key collision between product IDs
        /// and order IDs that may share the same string value.
        /// Also enables bulk invalidation by pattern (SCAN + DEL).
        /// </summary>
        public static class Keys
        {
            /// <summary>
            /// Product item cache key.
            /// Full key: product:item:{productId}
            /// Example:  product:item:shoes-001
            /// </summary>
            public const string ProductItem = "product:item:{0}";

            /// <summary>
            /// Product category listing cache key.
            /// Full key: product:category:{categoryName}
            /// Example:  product:category:SHOES
            /// </summary>
            public const string ProductCategory = "product:category:{0}";

            /// <summary>
            /// Distributed lock key for stampede protection.
            /// Full key: lock:product:category:{categoryName}
            /// Acquired before DB query on cache miss, released after cache population.
            /// </summary>
            public const string CategoryLock = "lock:product:category:{0}";
        }

        /// <summary>
        /// TTL (Time To Live) values per cached entity type.
        /// Chosen based on how frequently the data changes in production.
        /// PITFALL: Never use TimeSpan.Zero as TTL — it means "no expiry" in
        /// some Redis clients, not "expire immediately". Use explicit values always.
        /// </summary>
        public static class Ttl
        {
            /// <summary>
            /// Single product item TTL — 10 minutes.
            /// Products change infrequently. Invalidated explicitly on update/delete.
            /// </summary>
            public static readonly TimeSpan ProductItem = TimeSpan.FromMinutes(10);

            /// <summary>
            /// Product category listing TTL — 5 minutes.
            /// Category lists change when products are added/removed.
            /// Shorter TTL reduces stale listing risk without heavy invalidation logic.
            /// </summary>
            public static readonly TimeSpan ProductCategory = TimeSpan.FromMinutes(5);

            /// <summary>
            /// Distributed lock TTL — 30 seconds.
            /// If the lock holder crashes before releasing, the lock auto-expires
            /// preventing permanent deadlock.
            /// PITFALL: Must be longer than your DB query timeout.
            /// If the DB query takes longer than lock TTL, the lock expires
            /// while the query is still running — multiple requests proceed.
            /// </summary>
            public static readonly TimeSpan LockExpiry = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// StackExchange.Redis connection configuration values.
        /// Referenced in CacheOptions and connection string construction.
        /// </summary>
        public static class Connection
        {
            /// <summary>
            /// Default Redis/Valkey port.
            /// ElastiCache uses 6379 (unencrypted) or 6380 (TLS).
            /// Always use TLS (6380) in production.
            /// </summary>
            public const int DefaultPort = 6379;
            public const int TlsPort = 6380;

            /// <summary>
            /// Connection attempt timeout in milliseconds.
            /// 5 seconds allows for transient network blips without
            /// blocking the application too long.
            /// </summary>
            public const int ConnectTimeout = 5000;

            /// <summary>
            /// Synchronous operation timeout in milliseconds.
            /// Individual GET/SET operations should complete in < 1ms on a warm cache.
            /// 5 seconds is a generous safety margin for rare slow operations.
            /// </summary>
            public const int SyncTimeout = 5000;
        }
    }
}