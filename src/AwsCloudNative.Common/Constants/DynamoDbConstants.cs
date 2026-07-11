using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwsCloudNative.Common.Constants
{
    /// <summary>
    /// Centralised constants for Amazon DynamoDB integration.
    /// WHY: Table names, key prefixes, index names, and attribute names
    /// scattered across service classes and query builders cause silent
    /// mismatches when table conventions change.
    /// One source of truth for all DynamoDB structural identifiers.
    /// </summary>
    public static class DynamoDbConstants
    {
        /// <summary>
        /// DynamoDB table names for this solution.
        /// Pattern: acn-{environment}-{domain}
        /// Full name assembled at runtime using environment config.
        /// PITFALL: DynamoDB table names are case-sensitive.
        /// "acn-orders-catalog" and "Acn-Orders-Catalog" are different tables.
        /// </summary>
        public static class Tables
        {
            /// <summary>
            /// Single table for the Orders domain product catalog.
            /// Holds Products, Categories, and Reviews in one table
            /// using the single-table design pattern.
            /// </summary>
            public const string OrdersCatalog = "orders-catalog";
        }

        /// <summary>
        /// PK (Partition Key) and SK (Sort Key) prefix conventions.
        /// Pattern: {ENTITY_TYPE}#{entityId}
        /// WHY prefixes: Without type prefixes, a product with id "001"
        /// and an order with id "001" would have the same PK — collision.
        /// Prefixes make item types explicit and enable begins_with queries.
        /// </summary>
        public static class KeyPrefixes
        {
            /// <summary>Prefix for product partition keys. Example: PRODUCT#shoes-001</summary>
            public const string Product = "PRODUCT#";

            /// <summary>Prefix for category partition keys. Example: CATEGORY#SHOES</summary>
            public const string Category = "CATEGORY#";

            /// <summary>Prefix for review sort keys. Example: REVIEW#usr-001</summary>
            public const string Review = "REVIEW#";

            /// <summary>Sort key value for the primary metadata item of any entity.</summary>
            public const string Metadata = "METADATA";
        }

        /// <summary>
        /// GSI (Global Secondary Index) names defined on the table.
        /// Must match the index names configured in the AWS Console or CDK stack exactly.
        /// PITFALL: Querying a non-existent GSI name throws ResourceNotFoundException
        /// at runtime — not at compile time. These constants prevent typos.
        /// </summary>
        public static class Indexes
        {
            /// <summary>
            /// GSI for querying products by category.
            /// GSI PK: CategoryPK (e.g. CATEGORY#SHOES)
            /// GSI SK: PK (product PK, for sorted product listing within category)
            /// </summary>
            public const string CategoryIndex = "CategoryIndex";
        }

        /// <summary>
        /// DynamoDB item attribute names used in low-level expressions.
        /// These match the property names on mapped C# model classes.
        /// Used in FilterExpressions and UpdateExpressions.
        /// </summary>
        public static class Attributes
        {
            public const string PK = "PK";
            public const string SK = "SK";
            public const string CategoryPk = "CategoryPK";
            public const string Name = "Name";
            public const string Price = "Price";
            public const string Stock = "Stock";
            public const string CreatedAt = "CreatedAtUtc";
        }
    }
}
