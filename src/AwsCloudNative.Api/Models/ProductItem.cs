using Amazon.DynamoDBv2.DataModel;

namespace AwsCloudNative.Api.Models
{            
    /// <summary>
    /// Represents a Product item stored in the DynamoDB orders-catalog table.
    /// Uses the Object Persistence Model — the DynamoDB SDK maps this class
    /// to DynamoDB items automatically using the attributes below.
    ///
    /// WHY "Item" suffix instead of "Entity":
    /// DynamoDB stores "items" not "rows". The suffix reflects the DynamoDB
    /// terminology and distinguishes this from EF Core entities.
    ///
    /// Single-table design: This class maps to one item type in a table
    /// that also holds categories, reviews, and other entity types.
    /// The PK prefix "PRODUCT#" distinguishes product items from others.
    /// </summary>
    [DynamoDBTable("placeholder")]
    // WHY "placeholder": The actual table name is injected at runtime
    // using DynamoDBOperationConfig.OverrideTableName.
    // This allows environment-specific table names (acn-dev-orders-catalog)
    // without hardcoding in the attribute.
    public sealed class ProductItem
    {
        /// <summary>
        /// Partition Key — uniquely identifies the product partition.
        /// Format: PRODUCT#{productId}
        /// Example: PRODUCT#shoes-001
        /// All items for this product (metadata, reviews) share this PK.
        /// </summary>
        [DynamoDBHashKey("PK")]
        public string PK { get; set; } = string.Empty;

        /// <summary>
        /// Sort Key — identifies the item type within the product partition.
        /// For the main product item: always "METADATA"
        /// For reviews: "REVIEW#{userId}"
        /// </summary>
        [DynamoDBRangeKey("SK")]
        public string SK { get; set; } = string.Empty;

        /// <summary>
        /// GSI (Global Secondary Index) Partition Key for CategoryIndex.
        /// Format: CATEGORY#{categoryName}
        /// Example: CATEGORY#SHOES
        /// Allows querying all products in a category via the CategoryIndex GSI.
        /// </summary>
        [DynamoDBProperty("CategoryPK")]
        public string CategoryPK { get; set; } = string.Empty;

        /// <summary>Business-level product identifier. Stored as an attribute, not the PK.</summary>
        [DynamoDBProperty("ProductId")]
        public string ProductId { get; set; } = string.Empty;

        /// <summary>Human-readable product name.</summary>
        [DynamoDBProperty("Name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Product description.</summary>
        [DynamoDBProperty("Description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Product price stored as a string in DynamoDB.
        /// WHY string not decimal: DynamoDB's Number type loses trailing zeros
        /// (99.90 becomes 99.9). Storing as string preserves exact representation.
        /// Parse to decimal in the service layer before business logic.
        /// </summary>
        [DynamoDBProperty("Price")]
        public string Price { get; set; } = "0.00";

        /// <summary>Category name. Denormalised here for direct access without a GSI query.</summary>
        [DynamoDBProperty("Category")]
        public string Category { get; set; } = string.Empty;

        /// <summary>Available stock count.</summary>
        [DynamoDBProperty("Stock")]
        public int Stock { get; set; }

        /// <summary>ISO 8601 UTC timestamp string when the product was created.</summary>
        [DynamoDBProperty("CreatedAtUtc")]
        public string CreatedAtUtc { get; set; } = string.Empty;
    }
}
