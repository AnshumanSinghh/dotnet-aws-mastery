using AwsCloudNative.Common.Constants;
using AwsCloudNative.Common.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AwsCloudNative.Data.Extensions
{
    /// <summary>
    /// Registers EF Core and OrdersDbContext into the DI container.
    /// WHY in AwsCloudNative.Data and not AwsCloudNative.Api:
    /// The Data project owns its own registration — callers only reference
    /// the extension method, not the internal DbContext configuration.
    /// The API project does not need to know about Npgsql or connection pooling.
    /// </summary>
    public static class DataExtensions
    {
        /// <summary>
        /// Adds OrdersDbContext with Npgsql provider, connection resilience,
        /// and optimised connection pool settings.
        ///
        /// Connection string is resolved from DatabaseSecretOptions which
        /// was populated from Secrets Manager in Phase 1 Track 3.
        /// No credentials appear in this code.
        /// </summary>
        /// <param name="services">The DI service collection.</param>
        /// <param name="configuration">
        /// The fully-built IConfiguration instance — secrets already resolved.
        /// </param>
        public static IServiceCollection AddProductionDatabase(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Resolve the database secret options that were loaded from
            // Secrets Manager at startup (Phase 1 Track 3).
            // By this point, ValidateOnStart has confirmed all required
            // properties are populated — no null checks needed.
            var dbOptions = configuration
                    .GetSection(DatabaseSecretOptions.SectionName)
                    .Get<DatabaseSecretOptions>()!;

            // Build Npgsql connection string with explicit pool settings.
            // WHY NpgsqlConnectionStringBuilder over raw string:
            // Type-safe, handles escaping of special characters in passwords,
            // and makes each parameter explicit and readable.
            var connectionString = new Npgsql.NpgsqlConnectionStringBuilder
            {
                Host = dbOptions.Host,
                Port = dbOptions.Port,
                Database = dbOptions.Database,
                Username = dbOptions.Username,
                Password = dbOptions.Password,

                // Connection pool settings from RdsConstants
                MinPoolSize = RdsConstants.ConnectionPool.MinPoolSize,
                MaxPoolSize = RdsConstants.ConnectionPool.MaxPoolSize,

                // Close idle connections after 5 minutes.
                ConnectionIdleLifetime = RdsConstants.ConnectionPool.ConnectionIdleLifetimeSeconds,

                // WHY SSL mode require:
                // RDS enforces SSL by default. Explicitly requiring it
                // ensures the connection fails loudly if SSL is somehow
                // misconfigured — rather than silently connecting unencrypted.
                // Npgsql.SslMode.Require --[Not Needed] 'VerifyCA' will do both 'Encrypts Data?' & 'Validates CA Chain?'
                SslMode = Npgsql.SslMode.VerifyCA,

                // Trust the RDS server certificate — RDS uses AWS-managed certs.
                // In enterprise scenarios, pin to the RDS CA bundle instead.
                // TrustServerCertificate = true -- OBSOLETE
            }.ConnectionString;

            services.AddDbContext<OrdersDbContext>(options => 
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    // Enable EF Core's built-in retry-on-failure for transient errors.
                    // This handles brief network blips and RDS Multi-AZ failover.
                    // WHY EnableRetryOnFailure over Polly here:
                    // Npgsql's retry strategy is PostgreSQL-aware — it knows which
                    // error codes are transient (connection timeout, temp unavailable)
                    // vs permanent (constraint violation, syntax error).
                    // Polly is not PostgreSQL-aware and would retry on permanent errors too.
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: RdsConstants.Resilience.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(RdsConstants.Resilience.MaxRetryDelaySeconds),
                        errorCodesToAdd: null); // null = use Npgsql's default transient error list

                    // Set the migrations history table schema to match our schema convention.
                    npgsqlOptions.MigrationsHistoryTable(
                        "__EFMigrationsHistory",
                        RdsConstants.OrdersSchema);
                });

                // WHY disable lazy loading proxies:
                // Lazy loading causes N+1 query problems — each navigation property
                // access triggers a separate database round-trip silently.
                // Always use explicit Include() for eager loading.
                options.UseLazyLoadingProxies(false);
            });

            return services;
        }
    }
}
