using AwsCloudNative.Common.Options;
using StackExchange.Redis;

namespace AwsCloudNative.Api.Extensions
{
    /// <summary>
    /// Registers ElastiCache (Valkey/Redis) distributed caching
    /// and IConnectionMultiplexer into the DI container.
    ///
    /// WHY register both IDistributedCache and IConnectionMultiplexer:
    /// IDistributedCache (from Microsoft.Extensions.Caching) is the
    /// high-level abstraction — used for simple GET/SET operations via CacheService.
    /// IConnectionMultiplexer is the low-level StackExchange.Redis connection —
    /// used directly for distributed locking (SET NX) which IDistributedCache
    /// does not expose.
    /// </summary>
    public static class CacheExtensions
    {
        /// <summary>
        /// Adds ElastiCache Valkey/Redis as the IDistributedCache implementation
        /// and registers IConnectionMultiplexer as a Singleton for lock operations.
        /// </summary>
        /// <param name="services">The DI service collection.</param>
        public static IServiceCollection AddProductionCache(
            this IServiceCollection services) 
        {
            // Bind and validate CacheOptions from Parameter Store via IConfiguration.
            services.AddOptions<CacheOptions>()
                .BindConfiguration(CacheOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Register IConnectionMultiplexer as Singleton.
            // WHY Singleton: StackExchange.Redis ConnectionMultiplexer is designed
            // to be shared across the entire application — it manages its own
            // internal connection pool. Creating multiple multiplexers is wasteful
            // and causes connection exhaustion on ElastiCache.
            // PITFALL: Do NOT create a new ConnectionMultiplexer per request.
            // This is the most common StackExchange.Redis anti-pattern.
            services.AddSingleton<IConnectionMultiplexer>(provider =>
            {
                var options = provider
                    .GetRequiredService<Microsoft.Extensions.Options.IOptions<CacheOptions>>()
                    .Value;

                var connectionString = options.ToConnectionString();

                // abortConnect=false in the connection string means Connect()
                // does not throw if ElastiCache is unavailable at startup —
                // it retries in the background. Essential for graceful startup.
                return ConnectionMultiplexer.Connect(connectionString);
            });

            // Register IDistributedCache backed by StackExchange.Redis.
            // This powers the standard ASP.NET Core distributed cache abstraction —
            // session, response caching, and our CacheService all use this.
            services.AddStackExchangeRedisCache(options =>
            {
                // WHY resolve from IServiceProvider instead of re-reading config:
                // Reuses the already-registered IConnectionMultiplexer Singleton.
                // Prevents creating a second connection to ElastiCache.
                options.ConnectionMultiplexerFactory = () =>
                {
                    var multiplexer = services
                        .BuildServiceProvider()
                        .GetRequiredService<IConnectionMultiplexer>();
                    return Task.FromResult(multiplexer);
                };

                // Key prefix isolates this application's keys from other apps
                // sharing the same ElastiCache cluster.
                // Format: {appName}:
                options.InstanceName = "acn-orders:";
            });

            return services;
        }
    }
}
