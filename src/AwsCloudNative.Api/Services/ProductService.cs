using Amazon.DynamoDBv2.DataModel;
using AwsCloudNative.Api.Models;
using AwsCloudNative.Common.Constants;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AwsCloudNative.Api.Services
{
    /// <summary>
    /// Provides product catalog operations backed by Amazon DynamoDB.
    /// Uses the Object Persistence Model (DynamoDBContext) for
    /// clean C#-to-DynamoDB item mapping without raw AttributeValue dictionaries.
    ///
    /// WHY a service class not direct controller usage:
    /// DynamoDB operations require table name configuration, key prefix
    /// construction, and GSI query setup — too much for a thin controller.
    /// The service encapsulates all DynamoDB access patterns for the product domain.
    /// </summary>
    public sealed class ProductService
    {
        private readonly IDynamoDBContext _dynamoDbContext;
        private readonly string _tableName;
        private readonly ILogger<ProductService> _logger;

        /// <summary>
        /// Initialises with DynamoDB context and resolves the environment-specific table name.
        /// WHY inject IConfiguration for table name:
        /// Table names follow the pattern acn-{env}-orders-catalog.
        /// The environment suffix differs per deployment — resolved from config,
        /// never hardcoded.
        /// </summary>
        public ProductService(
            IDynamoDBContext dynamoDbContext,
            IConfiguration configuration,
            ILogger<ProductService> logger)
        {
            _dynamoDbContext = dynamoDbContext;
            _logger = logger;

            // Assemble table name from environment config.
            // Value from appsettings: "acn-dev" / "acn-prod"
            var appPrefix = configuration["AppPrefix"] ?? "acn-dev";
            _tableName = $"{appPrefix}-{DynamoDbConstants.Tables.OrdersCatalog}";
        }

        /// <summary>
        /// Retrieves a single product by its business ProductId.
        /// Uses GetAsync — the most efficient DynamoDB read (single-item lookup by PK + SK).
        ///
        /// WHY GetAsync is fastest:
        /// DynamoDB can locate any item in O(1) time using the PK hash.
        /// GetAsync bypasses indexes and goes directly to the partition.
        /// This is why designing your access patterns around PK is critical.
        /// </summary>
        /// <param name="productId">The business product identifier.</param>
        /// <param name="ct">Cancellation token.</param>
        public async Task<ProductItem> GetProductAsync(string productId, CancellationToken ct) 
        {
            var pk = $"{DynamoDbConstants.KeyPrefixes.Product}{productId}";
            var sk = DynamoDbConstants.KeyPrefixes.Metadata;

            _logger.LogInformation(
            "Fetching product from DynamoDB. PK={PK} Table={Table}",
            pk, _tableName);

            // ======================= [OBSOLETE] ============================
            // DynamoDBOperationConfig.OverrideTableName injects the runtime
            // table name — overrides the placeholder in [DynamoDBTable] attribute.
            //var config = new DynamoDBOperationConfig
            //{
            //    OverrideTableName = _tableName,
            //    // WHY ConsistentRead = false (default):
            //    // Product catalog is read-heavy and can tolerate eventual consistency.
            //    // Eventually consistent reads cost half the read capacity units.
            //    ConsistentRead = false
            //};

            var config = new LoadConfig
            {
                OverrideTableName = _tableName,
                ConsistentRead = false
            };

            return await _dynamoDbContext.LoadAsync<ProductItem>(pk, sk, config, ct);
        }


        /// <summary>
        /// Creates or replaces a product item in DynamoDB.
        /// Uses SaveAsync which maps to PutItem — creates if not exists,
        /// replaces entirely if exists (not a partial update).
        ///
        /// PITFALL — PutItem replaces the entire item:
        /// If you load a product, change one attribute, and SaveAsync —
        /// all other attributes are preserved because you loaded the full item.
        /// But if you SaveAsync a partially-populated ProductItem,
        /// attributes not set on the C# object will be DELETED from DynamoDB.
        /// Always load first if doing partial updates, or use UpdateAsync.
        /// </summary>
        /// <param name="product">The product item to persist.</param>
        /// <param name="ct">Cancellation token.</param>
        public async Task SaveProductAsync(ProductItem product, CancellationToken ct = default)
        {
            // Construct PK and SK from the productId following naming convention.
            product.PK = $"{DynamoDbConstants.KeyPrefixes.Product}{product.ProductId}";
            product.SK = DynamoDbConstants.KeyPrefixes.Metadata;

            // Build GSI partition key for category-based querying.
            product.CategoryPK = $"{DynamoDbConstants.KeyPrefixes.Category}{product.Category.ToUpperInvariant()}";
            product.CreatedAtUtc = DateTime.UtcNow.ToString("O"); // ISO 8601 format

            _logger.LogInformation(
                "Saving product to DynamoDB. ProductId={ProductId} Table={Table}",
                product.ProductId, _tableName);

            var config = new SaveConfig
            {
                OverrideTableName = _tableName
            };

            await _dynamoDbContext.SaveAsync(product, config, ct);

            _logger.LogInformation(
                "Product saved. ProductId={ProductId}", product.ProductId);
        }

        /// <summary>
        /// Queries all products in a given category using the CategoryIndex GSI.
        ///
        /// WHY Query over Scan:
        /// Scan reads EVERY item in the table and filters client-side.
        /// For a table with 1 million items, Scan reads all 1 million
        /// regardless of how many match your filter.
        /// Query uses the GSI index to read only matching items directly.
        /// Always prefer Query. Scan is only acceptable for small tables
        /// or administrative/migration operations.
        /// </summary>
        /// <param name="category">Category name to filter by.</param>
        /// <param name="ct">Cancellation token.</param>
        public async Task<IReadOnlyList<ProductItem>> GetProductsByCategoryAsync(
        string category,
        CancellationToken ct = default)
        {
            var categoryPk = $"{DynamoDbConstants.KeyPrefixes.Category}{category.ToUpperInvariant()}";

            _logger.LogInformation(
                "Querying products by category. Category={Category} GSI={GSI}",
                category, DynamoDbConstants.Indexes.CategoryIndex);

            var config = new QueryConfig
            {
                OverrideTableName = _tableName,
                IndexName = DynamoDbConstants.Indexes.CategoryIndex,
                // ConsistentRead must be false for GSI queries.
                // PITFALL: Setting ConsistentRead = true on a GSI query throws
                // a ValidationException from DynamoDB. GSIs do not support
                // strongly consistent reads — they are always eventually consistent.
                ConsistentRead = false
            };

            // QueryAsync with a hash key condition queries the GSI by CategoryPK.
            // Returns all products in the category sorted by SK (product PK).
            var search = _dynamoDbContext.QueryAsync<ProductItem>(categoryPk, config);
            var results = await search.GetRemainingAsync(ct);

            _logger.LogInformation(
                "Category query complete. Category={Category} Count={Count}",
                category, results.Count);

            return results.AsReadOnly();
        }


        /// <summary>
        /// Deletes a product item from DynamoDB by ProductId.
        /// DeleteAsync is idempotent — deleting a non-existent item succeeds silently.
        /// </summary>
        /// <param name="productId">The business product identifier to delete.</param>
        /// <param name="ct">Cancellation token.</param>
        public async Task DeleteProductAsync(string productId, CancellationToken ct = default)
        {
            var pk = $"{DynamoDbConstants.KeyPrefixes.Product}{productId}";
            var sk = DynamoDbConstants.KeyPrefixes.Metadata;

            _logger.LogInformation(
                "Deleting product from DynamoDB. ProductId={ProductId}", productId);

            var config = new DeleteConfig
            {
                OverrideTableName = _tableName
            };

            await _dynamoDbContext.DeleteAsync<ProductItem>(pk, sk, config, ct);
        }
    }
}
