using Amazon;
using Amazon.Extensions.NETCore.Setup;
using AwsCloudNative.Common.Constants;
using AwsCloudNative.Common.Options;

namespace AwsCloudNative.Api.Extensions
{
    /// <summary>
    /// Plugs AWS Secrets Manager and SSM Parameter Store into the
    /// ASP.NET Core IConfiguration pipeline at application startup.
    /// WHY: By integrating at the IConfiguration level, all downstream
    /// code (IOptions, controllers, services) reads secrets identically
    /// to how it reads appsettings.json — zero special-case code.
    /// </summary>
    public static class SecretsManagerExtensions
    {
        /// <summary>
        /// Adds AWS Secrets Manager and Parameter Store as IConfiguration sources.
        /// Call this on WebApplicationBuilder.Configuration, before builder.Build().
        /// </summary>
        /// <param name="configBuilder">The application's configuration builder.</param>
        /// <param name="environment">
        /// The current hosting environment. Used to resolve environment-specific
        /// secret paths (e.g. "acn/prod/orders/database" vs "acn/dev/orders/database").
        /// </param>
        /// <param name="region">
        /// The AWS region where secrets and parameters are stored.
        /// Resolved from AwsOptions — never hardcoded.
        /// </param>
        public static IConfigurationBuilder AddProductionSecrets(
            this IConfigurationBuilder configBuilder,
            IHostEnvironment environment,
            string region)
        {
            var env = environment.EnvironmentName.ToLower(); // "dev", "staging", "prod"
            var regionEndpoint = RegionEndpoint.GetBySystemName(region);


            // ── Parameter Store ──────────────────────────────────────────────────
            // Loads all parameters under /acn/{env}/orders/ into IConfiguration.
            // Example: /acn/prod/orders/queue-url
            //          → IConfiguration key: "queue-url"
            //          → After AddOptions binding: OrdersParameterOptions.QueueUrl
            //
            // WHY path-based loading: loads all config for a service in one call
            // instead of fetching each parameter individually.
            // PITFALL: The path prefix is stripped from the key. So
            // /acn/prod/orders/queue-url becomes just "queue-url" in IConfiguration.
            // Make sure your Options SectionName matches the stripped key structure.
            configBuilder.AddSystemsManager(config =>
            {
                config.Path = SecretsConstants.Parameters.OrdersPrefix
                                .Replace("{env}", env);

                config.ReloadAfter = TimeSpan.FromMinutes(5); // poll for changes
                config.Optional = environment.IsDevelopment(); // non-fatal in dev
                config.AwsOptions = new AWSOptions
                {
                    Region = regionEndpoint
                };
            });


            // ── Secrets Manager ───────────────────────────────────────────────────
            // Loads the Orders database secret JSON and flattens it into IConfiguration.
            // The secret value is a JSON object:
            //   { "Username": "...", "Password": "...", "Host": "...", ... }
            // Each JSON key becomes an IConfiguration key under the secret name prefix.
            // PITFALL: Secrets Manager values are cached by the SDK.
            // Rotation does NOT automatically propagate to a running process
            // unless you set ReloadAfter or restart the service.
            configBuilder.AddSystemsManager(config => 
            {
                config.Path = "/secretsmanager/"
                               + SecretsConstants.Secrets.OrdersDatabase
                                    .Replace("{env}", env);
                config.ReloadAfter = TimeSpan.FromMinutes(15); // secrets rotate less often
                config.Optional = environment.IsDevelopment();
                config.AwsOptions = new AWSOptions
                {
                    Region = regionEndpoint
                };                
            });

            // NOTE: Secret Json Keys are injected as root-level IConfiguration keys.
            // Means it will not bind to our DataBaseSecretOptions.To bind it correctly
            // We have thre options:
            // 1. Nest the secret JSON under a section key (recommended)
            // EX: {
            // "OrdersDatabase": {
            //      "Username": "orders_user",
            //      "Password": "Xk92a!mP3qR",
            //      "Host":     "prod-db.rds.amazonaws.com",
            //      "Port":     "5432",
            //      "Database": "orders"
            //     }
            //  }
            // Now the SDK flattens it as: OrdersDatabase:Username  →  orders_user
            // This matches BindConfiguration("OrdersDatabase") exactly. ✓

            // 2. Change the binding to root level
            // // Binds to root-level keys: Username, Host, Password etc.
            // services
            //    .AddOptions<DatabaseSecretOptions>()
            //    .BindConfiguration(string.Empty)  // root level

            // 3. Store the key name with prefix of section key. For Ex:Username --> "OrdersDatabase:Username" 
            // SECRET JSON STRING:
            // {
            //      "OrdersDatabase:Username": "orders_user",
            //      "OrdersDatabase:Password": "Xk92a!mP3qR",
            //      "OrdersDatabase:Host":     "prod-db.rds.amazonaws.com",
            //      "OrdersDatabase:Port":     "5432",
            //      "OrdersDatabase:Database": "orders"
            // }
            //
            // Now the SDK flattens it as: OrdersDatabase:Username  →  orders_user

            return configBuilder;
        }

        /// <summary>
        /// Registers the strongly-typed Options classes that bind to values
        /// resolved from Secrets Manager and Parameter Store.
        /// Call this in IServiceCollection registration (Program.cs).
        /// </summary>
        /// <param name="services">The DI service collection.</param>
        /// <param name="configuration">The fully-built IConfiguration instance.</param>
        public static IServiceCollection AddProductionSecretsOptions(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Bind database credentials from Secrets Manager.
            // ValidateOnStart ensures missing or malformed secrets crash the
            // app at startup, not silently at the first database call.
            services.AddOptions<DatabaseSecretOptions>()
                .BindConfiguration(DatabaseSecretOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Bind non-secret parameters from Parameter Store.
            services.AddOptions<OrdersParameterOptions>()
                .BindConfiguration(OrdersParameterOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            return services;
        }
    }
}