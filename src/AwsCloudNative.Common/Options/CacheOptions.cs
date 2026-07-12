using AwsCloudNative.Common.Constants;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwsCloudNative.Common.Options
{
    /// <summary>
    /// Strongly-typed configuration for ElastiCache Valkey/Redis connection.
    /// Resolved from Parameter Store at startup via IConfiguration.
    /// WHY: The ElastiCache endpoint changes between environments and
    /// when a cluster is replaced. It must never be hardcoded.
    /// </summary>
    public sealed class CacheOptions
    {
        /// <summary>The IConfiguration section this class binds to.</summary>
        public const string SectionName = "Cache";

        /// <summary>
        /// ElastiCache primary endpoint hostname.
        /// Example: acn-orders-cache.abc123.ng.0001.apse1.cache.amazonaws.com
        /// Resolved from Parameter Store — changes per environment and cluster.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string Endpoint { get; init; } = string.Empty;

        /// <summary>
        /// ElastiCache port.
        /// Use 6380 for TLS in production, 6379 for local dev without TLS.
        /// </summary>
        [Range(1, 65535)]
        public int Port { get; init; } = CacheConstants.Connection.DefaultPort;

        /// <summary>
        /// Whether to use TLS (Transport Layer Security) for the connection.
        /// Always true in production — ElastiCache in-transit encryption.
        /// False for local Redis/Valkey running in Docker without TLS.
        /// </summary>
        public bool UseTls { get; init; } = true;

        /// <summary>
        /// Builds the StackExchange.Redis connection string from resolved options.
        /// WHY a computed property: Connection string must not be stored in config —
        /// it combines endpoint + port + TLS setting at runtime.
        /// </summary>
        public string ToConnectionString() =>
            $"{Endpoint}:{Port}," +
            $"abortConnect=false," +
            $"connectRetry=3," +
            $"connectTimeout={CacheConstants.Connection.ConnectTimeout}," +
            $"syncTimeout={CacheConstants.Connection.SyncTimeout}," +
            $"ssl={(UseTls ? "true" : "false")}";
    }
}
