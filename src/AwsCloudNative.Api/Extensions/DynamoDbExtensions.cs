using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;

namespace AwsCloudNative.Api.Extensions
{
    /// <summary>
    /// Registers Amazon DynamoDB SDK client and Object Persistence Model context
    /// into the DI (Dependency Injection) container.
    /// </summary>
    public static class DynamoDbExtensions
    {
        /// <summary>
        /// Adds IAmazonDynamoDB SDK client and IDynamoDBContext for the
        /// Object Persistence Model.
        ///
        /// WHY IDynamoDBContext as Singleton:
        /// DynamoDBContext is thread-safe and lightweight — it holds no
        /// connection state (DynamoDB is HTTP-based, not connection-based).
        /// Creating a new context per request is wasteful. Singleton is correct.
        ///
        /// CONTRAST with EF Core DbContext (Scoped):
        /// EF Core tracks entity state within a DbContext instance — sharing
        /// one instance across requests causes state corruption.
        /// DynamoDB context has no such state tracking — Singleton is safe.
        /// </summary>
        /// <param name="services">The DI service collection.</param>
        public static IServiceCollection AddProductionDynamoDb(this IServiceCollection services)
        {
            // IAmazonDynamoDB — the low-level SDK client.
            // Uses DefaultAWSOptions credential chain from AddProductionAws (Phase 1 Track 1).
            services.AddAWSService<IAmazonDynamoDB>();

            // IDynamoDBContext — the Object Persistence Model abstraction.
            // Wraps IAmazonDynamoDB and provides C# object ↔ DynamoDB item mapping.
            services.AddSingleton<IDynamoDBContext>(provider =>
            {
                var client = provider.GetRequiredService<IAmazonDynamoDB>();

                // Recommended: build the context via DynamoDBContextBuilder
                var context = new DynamoDBContextBuilder()
                    .WithDynamoDBClient(() => client)
                    // Optional: configure context settings if needed
                    // .ConfigureContext(cfg =>
                    // {
                    //     cfg.ConsistentRead = false;
                    //     cfg.IgnoreNullValues = true;
                    // })
                    .Build();

                return context;
            });

            return services;
        }
    }
}
